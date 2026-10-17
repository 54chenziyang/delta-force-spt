using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace DeltaForceInstaller
{
    internal static class Program
    {
        internal const string PayloadFileName = "payload.zip";
        internal const string PackVersion = "1.2.0";

        [STAThread]
        private static void Main(string[] args)
        {
            bool silent = false;
            string root = null;
            string payload = null;
            bool doClient = true;
            bool doServer = true;

            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                if (string.Equals(a, "--silent", StringComparison.OrdinalIgnoreCase)) { silent = true; continue; }
                if (string.Equals(a, "--no-client", StringComparison.OrdinalIgnoreCase)) { doClient = false; continue; }
                if (string.Equals(a, "--no-server", StringComparison.OrdinalIgnoreCase)) { doServer = false; continue; }
                if (string.Equals(a, "--payload", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length) { payload = args[i + 1]; i++; continue; }
                if (a.Length > 0 && a[0] != '-') { root = a; }
            }

            if (silent)
            {
                StringBuilder sb = new StringBuilder();
                int code;
                try
                {
                    code = Installer.Run(root, doClient, doServer, payload, delegate(string line)
                    {
                        sb.AppendLine(line);
                        Console.WriteLine(line);
                    }, null);
                }
                catch (Exception ex)
                {
                    sb.AppendLine("[异常] " + ex);
                    code = 1;
                }
                try
                {
                    string logPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "DeltaForce_install.log");
                    File.WriteAllText(logPath, sb.ToString(), new UTF8Encoding(false));
                }
                catch
                {
                }
                Environment.Exit(code);
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }

        internal static string PayloadPath(string explicitPath)
        {
            if (!string.IsNullOrEmpty(explicitPath) && File.Exists(explicitPath)) return explicitPath;
            string dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string candidate = Path.Combine(dir, PayloadFileName);
            if (File.Exists(candidate)) return candidate;
            candidate = Path.Combine(Environment.CurrentDirectory, PayloadFileName);
            if (File.Exists(candidate)) return candidate;
            return null;
        }
    }

    internal static class Installer
    {
        private static readonly string[] RootFiles = new string[]
        {
            "winhttp.dll",
            "doorstop_config.ini",
            ".doorstop_version"
        };

        public static int Run(string rootInput, bool doClient, bool doServer, string payloadPath, Action<string> log, Action<int, int> progress)
        {
            log("Delta Force Full Pack v" + Program.PackVersion + " 安装器");
            log("------------------------------------------------------------");

            string root = NormalizeRoot(rootInput);
            if (root == null)
            {
                log("[错误] 未找到有效的游戏根目录。");
                log("       请选择包含 EscapeFromTarkov.exe 的文件夹，例如 D:\\escape\\EscapeFromTarkov");
                return 2;
            }
            log("游戏根目录：" + root);

            string zipPath = Program.PayloadPath(payloadPath);
            if (zipPath == null)
            {
                log("[错误] 找不到 payload.zip。");
                log("       请确认 payload.zip 与本安装程序位于同一目录，或使用 --payload 指定路径。");
                return 3;
            }
            log("载荷文件：" + zipPath + "（" + Math.Round(new FileInfo(zipPath).Length / 1024d / 1024d, 1) + " MB）");

            string serverDir = FindServerDir(root);
            if (doServer && serverDir == null)
            {
                serverDir = Path.Combine(root, "SPT_Runtime");
                log("[提示] 未检测到现有 SPT 服务端，将新建 SPT_Runtime 目录。");
            }

            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

            using (FileStream fs = new FileStream(zipPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (ZipArchive zip = new ZipArchive(fs, ZipArchiveMode.Read))
            {
                List<string> prefixes = new List<string>();
                if (doClient)
                {
                    prefixes.Add("BepInEx/");
                    prefixes.Add("EscapeFromTarkov_Data/");
                    for (int i = 0; i < RootFiles.Length; i++) prefixes.Add(RootFiles[i]);
                }
                if (doServer) prefixes.Add("SPT_Runtime/");

                int total = 0;
                long totalBytes = 0;
                foreach (ZipArchiveEntry entry in zip.Entries)
                {
                    if (entry.FullName.EndsWith("/", StringComparison.Ordinal)) continue;
                    if (!Matches(entry, prefixes)) continue;
                    total++;
                    totalBytes += entry.Length;
                }

                if (!CheckFreeSpace(root, totalBytes, log)) return 4;

                int done = 0;

                if (doClient)
                {
                    log("[1/2] 覆盖客户端环境（BepInEx / Assembly-CSharp.dll / doorstop）...");
                    done = Extract(zip, "BepInEx/", Path.Combine(root, "BepInEx"), stamp, log, done, total, progress);
                    done = Extract(zip, "EscapeFromTarkov_Data/", Path.Combine(root, "EscapeFromTarkov_Data"), stamp, log, done, total, progress);
                    for (int i = 0; i < RootFiles.Length; i++)
                    {
                        done = Extract(zip, RootFiles[i], root, stamp, log, done, total, progress);
                    }
                    DisablePlugins(root, stamp, log);
                }

                if (doServer)
                {
                    log("[2/2] 覆盖服务端环境（SPT_Runtime 全量）-> " + serverDir);
                    log("      说明：不包含 user\\profiles 存档、logs、cache 与旧备份。");
                    done = Extract(zip, "SPT_Runtime/", serverDir, stamp, log, done, total, progress);

                    string legacy = Path.Combine(serverDir, "user", "mods", "Haavk.Server", "HaavkHideoutPay.dll");
                    if (File.Exists(legacy))
                    {
                        string backup = MoveToBackup(legacy, stamp);
                        log("[注意] 旧版 HaavkHideoutPay.dll 已重命名为 " + Path.GetFileName(backup));
                        log("       旧文件与新独立模组同时存在会导致藏身处重复扣钱，必须移除。");
                    }
                }
            }

            if (progress != null) progress(1, 1);

            log("------------------------------------------------------------");
            log("安装完成。被替换的原文件均已备份为 *.bak_deltaforce_" + stamp);
            log("请先关闭游戏，然后重启 SPT 服务端与游戏即可生效。");
            return 0;
        }

        private static bool Matches(ZipArchiveEntry entry, List<string> prefixes)
        {
            string name = entry.FullName.Replace('\\', '/');
            for (int i = 0; i < prefixes.Count; i++)
            {
                string prefix = prefixes[i];
                if (prefix.EndsWith("/", StringComparison.Ordinal))
                {
                    if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return true;
                }
                else
                {
                    if (string.Equals(name, prefix, StringComparison.OrdinalIgnoreCase)) return true;
                }
            }
            return false;
        }

        private static bool CheckFreeSpace(string root, long needBytes, Action<string> log)
        {
            try
            {
                string driveRoot = Path.GetPathRoot(Path.GetFullPath(root));
                DriveInfo drive = new DriveInfo(driveRoot);
                long free = drive.AvailableFreeSpace;
                long need = needBytes + (long)(512L * 1024 * 1024);
                log("需要空间：约 " + Math.Round(needBytes / 1024d / 1024d / 1024d, 2) + " GB，目标磁盘剩余 " + Math.Round(free / 1024d / 1024d / 1024d, 2) + " GB");
                if (free < need)
                {
                    log("[错误] 目标磁盘空间不足，已停止安装。");
                    log("       若目标盘已有相同文件，实际占用会更少；可先清理空间后重试。");
                    return false;
                }
                return true;
            }
            catch
            {
                return true;
            }
        }

        internal static string NormalizeRoot(string input)
        {
            if (string.IsNullOrEmpty(input)) return null;
            string text = input.Trim().Trim('"').Trim();
            if (text.Length == 0) return null;
            try { text = Path.GetFullPath(text); }
            catch { return null; }
            if (!Directory.Exists(text)) return null;

            DirectoryInfo dir = new DirectoryInfo(text);
            for (int i = 0; i < 3 && dir != null; i++)
            {
                if (File.Exists(Path.Combine(dir.FullName, "EscapeFromTarkov.exe"))) return dir.FullName;
                dir = dir.Parent;
            }
            if (Directory.Exists(Path.Combine(text, "SPT_Runtime"))) return text;
            return null;
        }

        private static string FindServerDir(string root)
        {
            string[] candidates = new string[]
            {
                Path.Combine(root, "SPT_Runtime"),
                Path.Combine(root, "SPT"),
                root
            };
            foreach (string candidate in candidates)
            {
                if (Directory.Exists(Path.Combine(candidate, "user", "mods"))) return candidate;
                if (File.Exists(Path.Combine(candidate, "SPT.Server.exe"))) return candidate;
                if (Directory.Exists(Path.Combine(candidate, "SPT_Data"))) return candidate;
            }
            return null;
        }

        private static int Extract(ZipArchive zip, string prefix, string targetRoot, string stamp, Action<string> log, int done, int total, Action<int, int> progress)
        {
            bool isPrefix = prefix.EndsWith("/", StringComparison.Ordinal);
            if (!Directory.Exists(targetRoot)) Directory.CreateDirectory(targetRoot);

            foreach (ZipArchiveEntry entry in zip.Entries)
            {
                string relative = entry.FullName.Replace('\\', '/');
                if (relative.Length == 0 || relative.EndsWith("/", StringComparison.Ordinal)) continue;

                string sub;
                if (isPrefix)
                {
                    if (!relative.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                    sub = relative.Substring(prefix.Length);
                }
                else
                {
                    if (!string.Equals(relative, prefix, StringComparison.OrdinalIgnoreCase)) continue;
                    sub = relative;
                }
                if (sub.Length == 0) continue;

                string destination = Path.Combine(targetRoot, sub.Replace('/', Path.DirectorySeparatorChar));
                string directory = Path.GetDirectoryName(destination);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory)) Directory.CreateDirectory(directory);

                done++;
                if (progress != null && (done % 20 == 0 || done == total)) progress(done, total);

                if (File.Exists(destination))
                {
                    if (EntryEqualsFile(entry, destination))
                    {
                        continue;
                    }
                    string backup = MoveToBackup(destination, stamp);
                    log("  覆盖：" + sub + "（原文件 -> " + Path.GetFileName(backup) + "）");
                }

                using (Stream input = entry.Open())
                using (FileStream output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, 1048576))
                {
                    input.CopyTo(output, 1048576);
                }
                try { File.SetLastWriteTime(destination, entry.LastWriteTime.LocalDateTime); }
                catch { }
            }
            return done;
        }

        private static bool EntryEqualsFile(ZipArchiveEntry entry, string path)
        {
            FileInfo info = new FileInfo(path);
            if (!info.Exists || info.Length != entry.Length) return false;

            using (Stream left = entry.Open())
            using (FileStream right = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 1048576))
            {
                byte[] a = new byte[1048576];
                byte[] b = new byte[1048576];
                while (true)
                {
                    int na = ReadFull(left, a);
                    int nb = ReadFull(right, b);
                    if (na != nb) return false;
                    if (na == 0) return true;
                    for (int i = 0; i < na; i++)
                    {
                        if (a[i] != b[i]) return false;
                    }
                }
            }
        }

        private static int ReadFull(Stream stream, byte[] buffer)
        {
            int total = 0;
            while (total < buffer.Length)
            {
                int read = stream.Read(buffer, total, buffer.Length - total);
                if (read <= 0) break;
                total += read;
            }
            return total;
        }

        private static void DisablePlugins(string root, string stamp, Action<string> log)
        {
            string[] files = new string[]
            {
                Path.Combine(root, "BepInEx", "plugins", "AnanasCharles-StatDeltaPreview", "StatDeltaPreview.dll"),
                Path.Combine(root, "BepInEx", "plugins", "DeltaStatusHud", "DeltaStatusHud.dll"),
                Path.Combine(root, "BepInEx", "plugins", "RZDirectReload", "RZDirectReload.dll")
            };

            foreach (string path in files)
            {
                if (!File.Exists(path)) continue;
                string destination = path + ".disabled";
                int index = 2;
                while (File.Exists(destination))
                {
                    destination = path + ".disabled_" + index;
                    index++;
                }
                try
                {
                    File.Move(path, destination);
                    log("  已禁用冲突插件：" + Path.GetFileName(path) + " -> " + Path.GetFileName(destination));
                }
                catch (Exception ex)
                {
                    log("[警告] 禁用 " + Path.GetFileName(path) + " 失败：" + ex.Message);
                }
            }
        }

        private static string MoveToBackup(string path, string stamp)
        {
            string backup = path + ".bak_deltaforce_" + stamp;
            int index = 2;
            while (File.Exists(backup))
            {
                backup = path + ".bak_deltaforce_" + stamp + "_" + index;
                index++;
            }
            File.Move(path, backup);
            return backup;
        }
    }

    internal sealed class MainForm : Form
    {
        private readonly TextBox _pathBox;
        private readonly CheckBox _clientCheck;
        private readonly CheckBox _serverCheck;
        private readonly TextBox _logBox;
        private readonly ProgressBar _bar;
        private readonly Button _installButton;
        private readonly Button _browseButton;

        public MainForm()
        {
            Text = "Delta Force Full Pack v" + Program.PackVersion + " 安装器";
            ClientSize = new Size(660, 560);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Microsoft YaHei UI", 9F);

            Label title = new Label();
            title.Text = "Delta Force Full Pack";
            title.Font = new Font("Microsoft YaHei UI", 16F, FontStyle.Bold);
            title.AutoSize = true;
            title.Location = new Point(18, 14);

            Label subtitle = new Label();
            subtitle.Text = "全量覆盖：客户端 BepInEx + Assembly-CSharp.dll，服务端 SPT_Runtime（不含存档）";
            subtitle.ForeColor = Color.DimGray;
            subtitle.AutoSize = true;
            subtitle.Location = new Point(21, 52);

            Label pathLabel = new Label();
            pathLabel.Text = "游戏根目录：";
            pathLabel.AutoSize = true;
            pathLabel.Location = new Point(21, 88);

            _pathBox = new TextBox();
            _pathBox.Location = new Point(108, 84);
            _pathBox.Width = 425;

            _browseButton = new Button();
            _browseButton.Text = "浏览…";
            _browseButton.Location = new Point(543, 82);
            _browseButton.Size = new Size(96, 28);
            _browseButton.Click += BrowseClick;

            _clientCheck = new CheckBox();
            _clientCheck.Text = "客户端环境（BepInEx 全部插件与配置 + Assembly-CSharp.dll + doorstop）";
            _clientCheck.Checked = true;
            _clientCheck.AutoSize = true;
            _clientCheck.Location = new Point(21, 128);

            _serverCheck = new CheckBox();
            _serverCheck.Text = "服务端环境（SPT_Runtime 全量：本体 + 数据库 + 全部模组，不含存档）";
            _serverCheck.Checked = true;
            _serverCheck.AutoSize = true;
            _serverCheck.Location = new Point(21, 156);

            Label hint = new Label();
            hint.Text = "payload.zip（约 1.6 GB）必须与本程序放在同一目录。";
            hint.ForeColor = Color.DimGray;
            hint.AutoSize = true;
            hint.Location = new Point(21, 186);

            Label logLabel = new Label();
            logLabel.Text = "安装日志：";
            logLabel.AutoSize = true;
            logLabel.Location = new Point(21, 214);

            _logBox = new TextBox();
            _logBox.Multiline = true;
            _logBox.ReadOnly = true;
            _logBox.ScrollBars = ScrollBars.Vertical;
            _logBox.Location = new Point(21, 234);
            _logBox.Size = new Size(618, 236);
            _logBox.Font = new Font("Consolas", 8.5F);
            _logBox.BackColor = Color.White;

            _bar = new ProgressBar();
            _bar.Location = new Point(21, 482);
            _bar.Size = new Size(618, 18);
            _bar.Minimum = 0;
            _bar.Maximum = 100;

            _installButton = new Button();
            _installButton.Text = "开始安装";
            _installButton.Location = new Point(489, 508);
            _installButton.Size = new Size(150, 34);
            _installButton.Click += InstallClick;

            Controls.Add(title);
            Controls.Add(subtitle);
            Controls.Add(pathLabel);
            Controls.Add(_pathBox);
            Controls.Add(_browseButton);
            Controls.Add(_clientCheck);
            Controls.Add(_serverCheck);
            Controls.Add(hint);
            Controls.Add(logLabel);
            Controls.Add(_logBox);
            Controls.Add(_bar);
            Controls.Add(_installButton);

            try
            {
                string detected = Installer.NormalizeRoot(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));
                if (detected != null) _pathBox.Text = detected;
            }
            catch
            {
            }
        }

        private void BrowseClick(object sender, EventArgs e)
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "请选择游戏根目录（包含 EscapeFromTarkov.exe 的文件夹）";
                dialog.ShowNewFolderButton = false;
                if (Directory.Exists(_pathBox.Text)) dialog.SelectedPath = _pathBox.Text;
                if (dialog.ShowDialog(this) == DialogResult.OK) _pathBox.Text = dialog.SelectedPath;
            }
        }

        private void InstallClick(object sender, EventArgs e)
        {
            string root = Installer.NormalizeRoot(_pathBox.Text);
            if (root == null)
            {
                MessageBox.Show(this, "路径无效：请选择包含 EscapeFromTarkov.exe 的游戏根目录。", "Delta Force", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (Program.PayloadPath(null) == null)
            {
                MessageBox.Show(this, "找不到 payload.zip。\n\n请把 payload.zip 与本安装程序放在同一目录。", "Delta Force", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            _pathBox.Text = root;
            _logBox.Clear();
            _bar.Value = 0;
            _installButton.Enabled = false;
            _browseButton.Enabled = false;
            _clientCheck.Enabled = false;
            _serverCheck.Enabled = false;

            bool doClient = _clientCheck.Checked;
            bool doServer = _serverCheck.Checked;

            Thread worker = new Thread(delegate()
            {
                int code = -1;
                try
                {
                    code = Installer.Run(root, doClient, doServer, null, AppendLog, UpdateProgress);
                }
                catch (Exception ex)
                {
                    AppendLog("[异常] " + ex);
                }

                int finalCode = code;
                try
                {
                    BeginInvoke((Action)delegate()
                    {
                        _installButton.Enabled = true;
                        _browseButton.Enabled = true;
                        _clientCheck.Enabled = true;
                        _serverCheck.Enabled = true;

                        if (finalCode == 0)
                        {
                            _bar.Value = 100;
                            MessageBox.Show(this, "安装完成！\n\n请关闭游戏并重启 SPT 服务端后生效。", "Delta Force", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                        else
                        {
                            MessageBox.Show(this, "安装未完成，请查看日志中的错误信息。", "Delta Force", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    });
                }
                catch
                {
                }
            });
            worker.IsBackground = true;
            worker.Start();
        }

        private void AppendLog(string line)
        {
            if (InvokeRequired)
            {
                try { BeginInvoke((Action<string>)AppendLog, line); }
                catch { }
                return;
            }
            _logBox.AppendText(line + Environment.NewLine);
        }

        private void UpdateProgress(int done, int total)
        {
            if (InvokeRequired)
            {
                try { BeginInvoke((Action<int, int>)UpdateProgress, done, total); }
                catch { }
                return;
            }
            if (total <= 0) return;
            int value = (int)Math.Round(done * 100d / total);
            if (value < 0) value = 0;
            if (value > 100) value = 100;
            _bar.Value = value;
        }
    }
}

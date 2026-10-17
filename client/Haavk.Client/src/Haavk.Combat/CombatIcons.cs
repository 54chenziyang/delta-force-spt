using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Haavk.Core;

namespace Haavk.Combat;

internal static class CombatIcons
{
	private static readonly RgbaImage[] Images = new RgbaImage[5];

	private static readonly string[] Stems = new string[5] { null, "armor_blocked", "armor_broken", "helmet_blocked", "helmet_broken" };

	private static readonly RgbaImage[] SkullImages = new RgbaImage[2];

	private static readonly string[] SkullStems = new string[2] { "skull", "skull_headshot" };

	private static volatile string _summary;

	private static volatile bool _done;

	private static bool _started;

	internal static bool Done => _done;

	internal static string Summary => _summary;

	internal static void Start()
	{
		if (!_started)
		{
			_started = true;
			string dir = IconsDir();
			Task.Run((Action)(() =>
			{
				LoadAll(dir);
			}));
		}
	}

	internal static RgbaImage Source(ArmorIcon icon, out ArmorIcon drawnAs)
	{
		drawnAs = icon;
		if (!_done || icon == ArmorIcon.None)
		{
			return null;
		}
		RgbaImage rgbaImage = Images[(int)icon];
		if (rgbaImage != null || !ArmorIcons.IsHelmet(icon))
		{
			return rgbaImage;
		}
		drawnAs = ((!ArmorIcons.IsBroken(icon)) ? ArmorIcon.Blocked : ArmorIcon.Broken);
		return Images[(int)drawnAs];
	}

	internal static RgbaImage Skull(bool head, out bool drawnAsHead)
	{
		drawnAsHead = head;
		if (!_done)
		{
			return null;
		}
		RgbaImage rgbaImage = SkullImages[head ? 1u : 0u];
		if (rgbaImage != null)
		{
			return rgbaImage;
		}
		drawnAsHead = !head;
		return SkullImages[(!head) ? 1u : 0u];
	}

	private static void LoadAll(string dir)
	{
		StringBuilder stringBuilder = new StringBuilder();
		int ok = 0;
		try
		{
			for (int i = 1; i < Stems.Length; i++)
			{
				Images[i] = LoadOne(dir, Stems[i], stringBuilder, ref ok);
			}
			if (Images[3] == null && Images[1] != null)
			{
				stringBuilder.Append(" 头盔挡住退回背心图标");
			}
			if (Images[4] == null && Images[2] != null)
			{
				stringBuilder.Append(" 碎头退回碎甲图标");
			}
			for (int j = 0; j < SkullStems.Length; j++)
			{
				SkullImages[j] = LoadOne(dir, SkullStems[j], stringBuilder, ref ok);
			}
			if (SkullImages[0] == null != (SkullImages[1] == null))
			{
				stringBuilder.Append(" 两张骷髅缺一张，互相顶替");
			}
		}
		finally
		{
			_summary = ok + "/6" + ((stringBuilder.Length > 0) ? ("（" + stringBuilder.ToString().Trim() + "）") : "") + "，目录 " + dir;
			_done = true;
		}
	}

	private static RgbaImage LoadOne(string dir, string stem, StringBuilder notes, ref int ok)
	{
		string text = Path.Combine(dir, stem + ".png");
		try
		{
			if (!File.Exists(text))
			{
				notes.Append(' ').Append(stem).Append("=缺");
				return null;
			}
			RgbaImage result = PngDecoder.Decode(File.ReadAllBytes(text));
			ok++;
			return result;
		}
		catch (Exception ex)
		{
			notes.Append(' ').Append(stem).Append("=解码失败(")
				.Append(((MemberInfo)ex.GetType()).Name)
				.Append(": ")
				.Append(ex.Message)
				.Append(')');
			return null;
		}
	}

	private static string IconsDir()
	{
		try
		{
			string directoryName = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
			return Path.Combine(string.IsNullOrEmpty(directoryName) ? "." : directoryName, "icons");
		}
		catch (Exception)
		{
			return "icons";
		}
	}
}

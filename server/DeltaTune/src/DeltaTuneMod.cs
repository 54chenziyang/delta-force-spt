using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers.Profile;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Server.Core.Models.Spt.Tables;

namespace DeltaTune;

[Injectable(InjectionType.Transient, 1000200)]
public class DeltaTuneMod : IOnLoad
{
    private readonly ISptLogger<DeltaTuneMod> _logger;
    private readonly TemplateTable _tables;
    private readonly GlobalTable _globals;
    private readonly BotTable _bots;
    private readonly ProfileHelper _profiles;
    private readonly ModHelper _modHelper;

    public DeltaTuneMod(
        ISptLogger<DeltaTuneMod> logger,
        TemplateTable tables,
        GlobalTable globals,
        BotTable bots,
        ProfileHelper profiles,
        ModHelper modHelper)
    {
        _logger = logger;
        _tables = tables;
        _globals = globals;
        _bots = bots;
        _profiles = profiles;
        _modHelper = modHelper;
    }

    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        try
        {
            Load();
        }
        catch (Exception ex)
        {
            _logger?.Error("[DeltaTune] 加载失败: " + ex.Message, ex);
        }

        return Task.CompletedTask;
    }

    private void Load()
    {
        string modFolder = _modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
        ModConfig cfg = _modHelper.GetJsonDataFromFile<ModConfig>(modFolder, "config.json") ?? new ModConfig();
        Tuning.EnsureDefaults(cfg);

        PatchContext.Init(cfg, _logger, _profiles, _tables);

        if (cfg.Knife != null && cfg.Knife.Enabled)
        {
            KnifeTuner.Apply(_tables, cfg, _logger);
        }

        if (cfg.Combat != null && cfg.Combat.Enabled)
        {
            CombatTuner.Apply(_globals, _tables, cfg, _logger);
        }

        if (cfg.BotHealth != null && cfg.BotHealth.Enabled)
        {
            BotHealthTuner.Apply(_bots, cfg.BotHealth, _logger);
        }

        if (cfg.BotScatter != null && cfg.BotScatter.Enabled)
        {
            BotScatterTuner.Apply(_bots, cfg.BotScatter, _logger);
        }

        if (cfg.Loot != null && cfg.Loot.Enabled)
        {
            new StartRaidPatch().Enable();
            new EndRaidPatch().Enable();
            new LooseMultiplierPatch().Enable();
            new StaticMultiplierPatch().Enable();
            new ContainerLootPatch().Enable();
            new DynamicLootPatch().Enable();
            _logger?.Info("[DeltaTune] 动态爆率补丁已启用（普通/机密/绝密档位 + 战损/击杀幸运值）");
        }
    }
}

namespace DeltaTune;

/// <summary>
/// 单局运行态：由 StartLocalRaid 前置补丁按战备值算好，
/// 供战利品生成过程中的各倍率钩子读取（这些方法只收 location 字符串，拿不到 sessionId）。
/// </summary>
public static class RaidState
{
    public static bool Active { get; set; }

    public static string SessionId { get; set; } = "";

    /// <summary>当前档位名（普通/机密/绝密）。</summary>
    public static string TierName { get; set; } = "普通";

    /// <summary>开局战备估值（卢布）。</summary>
    public static double GearValue { get; set; }

    /// <summary>本局生效的幸运值。</summary>
    public static double Luck { get; set; }

    /// <summary>散落点数量倍率。</summary>
    public static double LooseMultiplier { get; set; } = 1.0;

    /// <summary>容器内件数倍率。</summary>
    public static double StaticMultiplier { get; set; } = 1.0;

    /// <summary>「金」档藏品权重倍率。</summary>
    public static double GoldFactor { get; set; } = 1.0;

    /// <summary>「红」档藏品权重倍率。</summary>
    public static double RedFactor { get; set; } = 1.0;

    /// <summary>开局快照：OverallCounters["Kills"]。</summary>
    public static double StartKills { get; set; }

    /// <summary>开局快照：Victims 数量。</summary>
    public static int StartVictims { get; set; }

    public static void Reset()
    {
        Active = false;
        SessionId = "";
        TierName = "普通";
        GearValue = 0.0;
        Luck = 0.0;
        LooseMultiplier = 1.0;
        StaticMultiplier = 1.0;
        GoldFactor = 1.0;
        RedFactor = 1.0;
        StartKills = 0.0;
        StartVictims = 0;
    }
}

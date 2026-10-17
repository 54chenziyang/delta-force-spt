namespace Haavk.Server;

public class ModConfig
{
	public string[] TargetLocales { get; set; }

	public bool SuppressDescriptionPrice { get; set; } = true;

	public string MoneyFormat { get; set; } = "plain";

	public bool MoneySpaceAfterSymbol { get; set; } = true;

	// 把品级写进物品名开头（如「【极品】AK-74M」）。跳蚤搜索框会同时匹配物品全名与短名，
	// 所以输入品级词（劣质/普通/良好/优秀/精良/极品）即可按品质筛选。弹药与金币不受影响。
	public bool QualityTagInName { get; set; } = true;

	public bool ColourNormalItems { get; set; }

	public bool ColourFleaBannedItems { get; set; }

	public double BadItemPerSlotMaxValue { get; set; }

	public double PoorItemPerSlotMaxValue { get; set; }

	public double FairItemPerSlotMaxValue { get; set; }

	public double GoodItemPerSlotMaxValue { get; set; }

	public double VeryGoodItemPerSlotMaxValue { get; set; }

	public double BadTraderItemPerSlotMaxValue { get; set; }

	public double PoorTraderItemPerSlotMaxValue { get; set; }

	public double FairTraderItemPerSlotMaxValue { get; set; }

	public double GoodTraderItemPerSlotMaxValue { get; set; }

	public double VeryGoodTraderItemPerSlotMaxValue { get; set; }

	public bool ColourKeys { get; set; }

	public bool ColourFleaBannedKeys { get; set; }

	public double BadKeyMaxValue { get; set; }

	public double PoorKeyMaxValue { get; set; }

	public double FairKeyMaxValue { get; set; }

	public double GoodKeyMaxValue { get; set; }

	public double VeryGoodKeyMaxValue { get; set; }

	public bool DamageAndPenStatsInName { get; set; }

	public bool ColourAmmo { get; set; }

	public bool ColourFleaBannedAmmo { get; set; }

	public double BadAmmoMaxScore { get; set; }

	public double PoorAmmoMaxScore { get; set; }

	public double FairAmmoMaxScore { get; set; }

	public double GoodAmmoMaxScore { get; set; }

	public double VeryGoodAmmoMaxScore { get; set; }

	public bool AmmoGradesInDescription { get; set; } = true;

	public int AmmoPenGrade1Max { get; set; } = 9;

	public int AmmoPenGrade2Max { get; set; } = 19;

	public int AmmoPenGrade3Max { get; set; } = 29;

	public int AmmoPenGrade4Max { get; set; } = 39;

	public int AmmoPenGrade5Max { get; set; } = 49;

	public int AmmoDamageGrade1Max { get; set; } = 39;

	public int AmmoDamageGrade2Max { get; set; } = 59;

	public int AmmoDamageGrade3Max { get; set; } = 79;

	public int AmmoDamageGrade4Max { get; set; } = 99;

	public int AmmoDamageGrade5Max { get; set; } = 149;

	// 显示用伤害倍率：把弹药模板里的原始伤害换算成游戏内实际生效值。
	// 默认 0.27 = DeltaTune.AmmoDamageMultiplier(0.9) × DeltaBallistics.Damage Multiplier(0.30)。
	// 名字后缀 [伤害/穿透]、伤害等级、子弹综合分都按换算后的伤害计算，
	// 伤害等级阈值(39/59/79/99/149)按同一倍率缩放，改这一个数就能整体跟随武器伤害倍率。
	public double AmmoDamageDisplayMultiplier { get; set; } = 0.27;

	public bool ColourWeapons { get; set; }

	public bool ColourFleaBannedWeapons { get; set; }

	public double BadWeaponMaxValue { get; set; }

	public double PoorWeaponMaxValue { get; set; }

	public double FairWeaponMaxValue { get; set; }

	public double GoodWeaponMaxValue { get; set; }

	public double VeryGoodWeaponMaxValue { get; set; }

	public bool ColourArmours { get; set; }

	public bool ColourFleaBannedArmour { get; set; }

	public int BadArmourMaxPlateClass { get; set; }

	public int PoorArmourMaxPlateClass { get; set; }

	public int FairArmourMaxPlateClass { get; set; }

	public int GoodArmourMaxPlateClass { get; set; }

	public int VeryGoodArmourMaxPlateClass { get; set; }

	// 背包/胸挂/容器的品质按「性能指标 = 总容量格数（全部子网格 cellsH×cellsV 之和）」定级，
	// 不再用单格价值。阈值均为左闭区间上界，档位与护甲一致：白/绿/蓝/紫/金/红。
	public bool ColourBackpacks { get; set; }

	public bool ColourFleaBannedBackpacks { get; set; }

	public double BadBackpackCapacityMax { get; set; }

	public double PoorBackpackCapacityMax { get; set; }

	public double FairBackpackCapacityMax { get; set; }

	public double GoodBackpackCapacityMax { get; set; }

	public double VeryGoodBackpackCapacityMax { get; set; }

	public bool ColourVests { get; set; }

	public bool ColourFleaBannedVests { get; set; }

	public double BadVestCapacityMax { get; set; }

	public double PoorVestCapacityMax { get; set; }

	public double FairVestCapacityMax { get; set; }

	public double GoodVestCapacityMax { get; set; }

	public double VeryGoodVestCapacityMax { get; set; }

	public bool ColourContainers { get; set; }

	public bool ColourFleaBannedContainers { get; set; }

	public double BadContainerCapacityMax { get; set; }

	public double PoorContainerCapacityMax { get; set; }

	public double FairContainerCapacityMax { get; set; }

	public double GoodContainerCapacityMax { get; set; }

	public double VeryGoodContainerCapacityMax { get; set; }

	public string BadColour { get; set; }

	public string PoorColour { get; set; }

	public string FairColour { get; set; }

	public string GoodColour { get; set; }

	public string VeryGoodColour { get; set; }

	public string ExceptionalColour { get; set; }

	public string FleaBannedColour { get; set; }

	public string FleaBannedTextColour { get; set; }
}

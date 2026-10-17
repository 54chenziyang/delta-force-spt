namespace Haavk.Core;

public sealed class ArmourClassColouring
{
	private readonly TierLadder _ladder;

	private readonly string[] _palette;

	private ArmourClassColouring(TierLadder ladder, string[] palette)
	{
		_ladder = ladder;
		_palette = palette;
	}

	public static bool TryCreate(double[] classTiers, string[] palette, out ArmourClassColouring result, out string error)
	{
		result = null;
		if (classTiers == null)
		{
			error = "服务端没发防级阈值（ArmourClassTiers 为 null，多半是服务端半边版本旧）";
			return false;
		}
		if (classTiers.Length != 5)
		{
			error = $"防级阈值必须是 5 个，实际 {classTiers.Length} 个";
			return false;
		}
		if (palette == null)
		{
			error = "服务端没发档位色板（TierColours 为 null）";
			return false;
		}
		if (palette.Length != 6)
		{
			error = $"档位色板必须是 6 个，实际 {palette.Length} 个";
			return false;
		}
		for (int i = 0; i < palette.Length; i++)
		{
			if (!PerSlotColouring.IsHexColour(palette[i]))
			{
				error = $"档位色板第 {i} 项不是 hex 色（\"{palette[i]}\"）" + "——多半是没装 ColorConverterAPI，服务端把色板降级成了颜色名";
				return false;
			}
		}
		result = new ArmourClassColouring(new TierLadder(classTiers, inclusive: true), palette);
		error = null;
		return true;
	}

	public int Tier(int armourClass)
	{
		return _ladder.Pick(armourClass);
	}

	public string Colour(int armourClass)
	{
		return _palette[Tier(armourClass)];
	}
}

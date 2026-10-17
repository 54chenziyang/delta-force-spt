namespace Haavk.Core;

public static class ArmorIcons
{
	public static bool IsBroken(ArmorIcon icon)
	{
		if (icon != ArmorIcon.Broken)
		{
			return icon == ArmorIcon.HelmetBroken;
		}
		return true;
	}

	public static bool IsHelmet(ArmorIcon icon)
	{
		if (icon != ArmorIcon.HelmetBlocked)
		{
			return icon == ArmorIcon.HelmetBroken;
		}
		return true;
	}

	public static int Rank(ArmorIcon icon)
	{
		if (icon != ArmorIcon.None)
		{
			if (!IsBroken(icon))
			{
				return 1;
			}
			return 2;
		}
		return 0;
	}

	public static string Name(ArmorIcon icon)
	{
		return icon switch
		{
			ArmorIcon.Blocked => "背心", 
			ArmorIcon.Broken => "碎甲", 
			ArmorIcon.HelmetBlocked => "头盔", 
			ArmorIcon.HelmetBroken => "碎头", 
			_ => "无", 
		};
	}
}

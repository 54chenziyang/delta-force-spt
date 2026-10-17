namespace Haavk.Core;

public static class DescriptionLine
{
	public static string Price(string label, string formattedPrice, string tierColour)
	{
		if (string.IsNullOrEmpty(tierColour) || tierColour[0] != '#')
		{
			return label + " " + formattedPrice;
		}
		return label + " <color=" + tierColour + ">" + formattedPrice + "</color>";
	}

	public static string TotalAndPerSlot(double total, int width, int height, string totalLabel, string perSlotLabel, string tierColour, PriceLineMode mode, MoneyStyle style)
	{
		string text = Price(totalLabel, PriceBook.FormatRoubles(total, style), tierColour);
		if (mode == PriceLineMode.仅总价)
		{
			return text;
		}
		int num = width * height;
		if (num <= 0)
		{
			return text;
		}
		if (mode == PriceLineMode.总价加每格 && num <= 1)
		{
			return text;
		}
		string text2 = Price(perSlotLabel, PriceBook.FormatRoubles(ValuationMath.PerSlotPrice(total, height, width), style), tierColour);
		if (mode != PriceLineMode.仅每格)
		{
			return text + " · " + text2;
		}
		return text2;
	}

	public static string Banned(string label, string textColour)
	{
		return "<color=" + textColour + ">" + label + "</color>";
	}

	public static string AmmoTaggedName(string originalName, int? damage, int? penetration, int? level, string levelColour)
	{
		string text = "";
		if (level.HasValue)
		{
			bool flag = !string.IsNullOrEmpty(levelColour) && levelColour[0] == '#';
			text = (flag ? ("<color=" + levelColour + ">" + level.Value + "级弹</color>") : (level.Value + "级弹")) + " ";
		}
		return $"{originalName} {text}<color=#808080>[{damage}/{penetration}]</color>";
	}

	public static string AmmoGrades(int penLevel, int dmgLevel, string textColour)
	{
		string text = "穿透等级 " + penLevel + " · 伤害等级 " + dmgLevel;
		if (string.IsNullOrEmpty(textColour) || textColour[0] != '#')
		{
			return text;
		}
		return "<color=" + textColour + ">" + text + "</color>";
	}
}

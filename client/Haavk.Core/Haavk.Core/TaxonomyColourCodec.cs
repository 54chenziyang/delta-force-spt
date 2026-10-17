using System.Globalization;

namespace Haavk.Core;

public static class TaxonomyColourCodec
{
	public static bool TryDecodeCustomHex(int taxonomyValue, int namedColourCount, out string hex)
	{
		hex = null;
		int num = taxonomyValue - namedColourCount;
		if (num < 0)
		{
			return false;
		}
		string text = num.ToString("X6");
		if (text.Length != 6 && text.Length != 8)
		{
			return false;
		}
		hex = "#" + text;
		return true;
	}

	public static bool TryEncodeCustomHex(string hex, int namedColourCount, out int taxonomyValue)
	{
		taxonomyValue = 0;
		if (string.IsNullOrEmpty(hex) || hex[0] != '#')
		{
			return false;
		}
		string text = hex.Substring(1);
		if (text.Length != 6 && text.Length != 8)
		{
			return false;
		}
		if (!int.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var result))
		{
			return false;
		}
		if (result < 0)
		{
			return false;
		}
		taxonomyValue = result + namedColourCount;
		return true;
	}
}

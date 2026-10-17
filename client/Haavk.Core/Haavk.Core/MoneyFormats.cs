using System;

namespace Haavk.Core;

public static class MoneyFormats
{
	public const string TokenPlain = "plain";

	public const string TokenWanW = "wan";

	public const string TokenWanCn = "wanCn";

	public const string TokenKM = "km";

	public static readonly string[] AllTokens = new string[4] { "plain", "wan", "wanCn", "km" };

	public static bool TryParse(string token, out MoneyFormat format)
	{
		format = MoneyFormat.原样;
		if (string.IsNullOrEmpty(token))
		{
			return false;
		}
		string text = token.Trim();
		if (text.Length == 0)
		{
			return false;
		}
		if (Is(text, "plain"))
		{
			format = MoneyFormat.原样;
			return true;
		}
		if (Is(text, "wan"))
		{
			format = MoneyFormat.万w;
			return true;
		}
		if (Is(text, "wanCn"))
		{
			format = MoneyFormat.万汉字;
			return true;
		}
		if (Is(text, "km"))
		{
			format = MoneyFormat.K与M;
			return true;
		}
		return false;
	}

	private static bool Is(string a, string b)
	{
		return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
	}
}

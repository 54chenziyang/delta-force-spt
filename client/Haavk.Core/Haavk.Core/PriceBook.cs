using System;
using System.Collections.Generic;
using System.Globalization;

namespace Haavk.Core;

public class PriceBook
{
	private Dictionary<string, double> _flea;

	private readonly bool _honourFleaBan;

	private const string Symbol = "₽";

	private const double WanDivisor = 10000.0;

	private const double KiloDivisor = 1000.0;

	private const double MegaDivisor = 1000000.0;

	private static readonly NumberFormatInfo CompactNumbers = CreateCompactNumbers();

	public bool Loaded => _flea != null;

	public PriceBook()
		: this(honourFleaBan: true)
	{
	}

	public PriceBook(bool honourFleaBan)
	{
		_honourFleaBan = honourFleaBan;
	}

	public void Load(Dictionary<string, double> flea)
	{
		_flea = flea;
	}

	public double Resolve(string templateId, bool canSellOnFlea, double handbookPrice)
	{
		if (_honourFleaBan && !canSellOnFlea)
		{
			return handbookPrice;
		}
		if (_flea != null && _flea.TryGetValue(templateId, out var value))
		{
			return value;
		}
		return handbookPrice;
	}

	private static NumberFormatInfo CreateCompactNumbers()
	{
		return new NumberFormatInfo
		{
			NumberDecimalSeparator = ".",
			NumberGroupSeparator = ","
		};
	}

	public static string FormatRoubles(double value, MoneyStyle style)
	{
		switch (style.Format)
		{
		case MoneyFormat.万w:
			return Compact(value, style, 10000.0, "w");
		case MoneyFormat.万汉字:
			return Compact(value, style, 10000.0, "万");
		case MoneyFormat.K与M:
			if (!(value >= 1000000.0))
			{
				return Compact(value, style, 1000.0, "K");
			}
			return Compact(value, style, 1000000.0, "M");
		default:
			return Plain(value, style.SpaceAfterSymbol);
		}
	}

	private static string Plain(double value, bool spaceAfterSymbol)
	{
		NumberFormatInfo numberFormatInfo = new NumberFormatInfo();
		numberFormatInfo.CurrencySymbol = "₽";
		numberFormatInfo.CurrencyDecimalSeparator = ".";
		numberFormatInfo.CurrencyGroupSeparator = ",";
		numberFormatInfo.CurrencyDecimalDigits = 0;
		if (spaceAfterSymbol)
		{
			numberFormatInfo.CurrencyPositivePattern = 2;
		}
		return value.ToString("C", numberFormatInfo);
	}

	private static string Compact(double value, MoneyStyle style, double divisor, string suffix)
	{
		if (value < divisor)
		{
			return Plain(value, style.SpaceAfterSymbol);
		}
		double num = Math.Round(value / divisor, 1, MidpointRounding.AwayFromZero);
		return "₽" + (style.SpaceAfterSymbol ? " " : string.Empty) + num.ToString("#,##0.0", CompactNumbers) + suffix;
	}
}

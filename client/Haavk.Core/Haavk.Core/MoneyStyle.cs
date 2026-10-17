namespace Haavk.Core;

public readonly struct MoneyStyle
{
	public MoneyFormat Format { get; }

	public bool SpaceAfterSymbol { get; }

	public static MoneyStyle Plain => new MoneyStyle(MoneyFormat.原样, spaceAfterSymbol: false);

	public MoneyStyle(MoneyFormat format, bool spaceAfterSymbol)
	{
		Format = format;
		SpaceAfterSymbol = spaceAfterSymbol;
	}
}

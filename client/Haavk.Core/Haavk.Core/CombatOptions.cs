namespace Haavk.Core;

public struct CombatOptions
{
	public bool Marker;

	public bool ArmorIcons;

	public bool KillNotice;

	public bool Sounds;

	public static CombatOptions All => new CombatOptions
	{
		Marker = true,
		ArmorIcons = true,
		KillNotice = true,
		Sounds = true
	};
}

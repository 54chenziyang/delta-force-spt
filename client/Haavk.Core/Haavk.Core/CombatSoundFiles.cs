namespace Haavk.Core;

public static class CombatSoundFiles
{
	public static readonly CombatSound[] Slots = new CombatSound[6]
	{
		CombatSound.Hit,
		CombatSound.Kill,
		CombatSound.KillHead,
		CombatSound.ArmorBreak,
		CombatSound.ExplosiveHit,
		CombatSound.ExplosiveKill
	};

	public static string Stem(CombatSound sound)
	{
		return sound switch
		{
			CombatSound.Hit => "combat_hit", 
			CombatSound.Kill => "combat_kill", 
			CombatSound.KillHead => "combat_kill_head", 
			CombatSound.ArmorBreak => "combat_armor_break", 
			CombatSound.ExplosiveHit => "combat_explosive_hit", 
			CombatSound.ExplosiveKill => "combat_explosive_kill", 
			_ => null, 
		};
	}
}

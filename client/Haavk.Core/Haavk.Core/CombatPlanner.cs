using System.Collections.Generic;

namespace Haavk.Core;

public sealed class CombatPlanner
{
	private sealed class Shot
	{
		public MarkerShape Marker;

		public bool Lethal;

		public bool AtCenter;

		public HitFact Point;

		public ArmorIcon Icon;

		public CombatSound Primary;

		public bool Break;
	}

	public const double BlastWindowSeconds = 1.0;

	public static readonly CombatSound[] SoundOrder = new CombatSound[6]
	{
		CombatSound.KillHead,
		CombatSound.Kill,
		CombatSound.ExplosiveKill,
		CombatSound.ArmorBreak,
		CombatSound.Hit,
		CombatSound.ExplosiveHit
	};

	private readonly HashSet<string> _announced = new HashSet<string>();

	private readonly Dictionary<string, double> _blasts = new Dictionary<string, double>();

	public int AnnouncedCount => _announced.Count;

	public void Reset()
	{
		_announced.Clear();
		_blasts.Clear();
	}

	public CombatPlan Plan(IReadOnlyList<HitFact> facts, CombatOptions options, double nowSeconds)
	{
		CombatPlan combatPlan = new CombatPlan();
		PurgeBlasts(nowSeconds);
		if (facts == null || facts.Count == 0)
		{
			return combatPlan;
		}
		Dictionary<string, HitFact> dictionary = new Dictionary<string, HitFact>();
		List<string> list = new List<string>();
		for (int i = 0; i < facts.Count; i++)
		{
			HitFact hitFact = facts[i];
			if (hitFact == null || !hitFact.Lethal || string.IsNullOrEmpty(hitFact.VictimId) || _announced.Contains(hitFact.VictimId))
			{
				continue;
			}
			if (dictionary.TryGetValue(hitFact.VictimId, out var value))
			{
				if (value.ShotKey == null && hitFact.ShotKey != null)
				{
					dictionary[hitFact.VictimId] = hitFact;
				}
			}
			else
			{
				dictionary[hitFact.VictimId] = hitFact;
				list.Add(hitFact.VictimId);
			}
		}
		for (int j = 0; j < list.Count; j++)
		{
			string text = list[j];
			_announced.Add(text);
			if (options.KillNotice)
			{
				combatPlan.Kills.Add(ToNotice(dictionary[text]));
			}
		}
		List<string> list2 = new List<string>();
		Dictionary<string, List<HitFact>> dictionary2 = new Dictionary<string, List<HitFact>>();
		for (int k = 0; k < facts.Count; k++)
		{
			HitFact hitFact2 = facts[k];
			if (hitFact2 != null && hitFact2.ShotKey != null && hitFact2.Source != HitSource.Other)
			{
				if (!dictionary2.TryGetValue(hitFact2.ShotKey, out var value2))
				{
					value2 = new List<HitFact>();
					dictionary2[hitFact2.ShotKey] = value2;
					list2.Add(hitFact2.ShotKey);
				}
				value2.Add(hitFact2);
			}
		}
		Dictionary<string, int> dictionary3 = new Dictionary<string, int>();
		HashSet<CombatSound> hashSet = new HashSet<CombatSound>();
		Shot shot = null;
		ArmorIcon icon = ArmorIcon.None;
		for (int l = 0; l < list2.Count; l++)
		{
			Shot shot2 = Evaluate(list2[l], dictionary2[list2[l]], l, dictionary, dictionary3, nowSeconds);
			if (shot2.Primary != CombatSound.None)
			{
				hashSet.Add(shot2.Primary);
			}
			if (shot2.Break)
			{
				hashSet.Add(CombatSound.ArmorBreak);
			}
			if (ArmorIcons.Rank(shot2.Icon) > ArmorIcons.Rank(icon))
			{
				icon = shot2.Icon;
			}
			if (shot2.Marker != MarkerShape.None && (shot == null || shot2.Lethal || !shot.Lethal))
			{
				shot = shot2;
			}
		}
		for (int m = 0; m < list.Count; m++)
		{
			if (!dictionary3.ContainsKey(list[m]))
			{
				hashSet.Add(KillSoundFor(dictionary[list[m]]));
			}
		}
		if (options.Marker && shot != null)
		{
			combatPlan.Marker = shot.Marker;
			combatPlan.MarkerLethal = shot.Lethal;
			combatPlan.MarkerAtCenter = shot.AtCenter;
			if (shot.Point != null)
			{
				combatPlan.HasHitPoint = true;
				combatPlan.HitX = shot.Point.HitX;
				combatPlan.HitY = shot.Point.HitY;
				combatPlan.HitZ = shot.Point.HitZ;
			}
		}
		if (options.ArmorIcons)
		{
			combatPlan.Icon = icon;
		}
		if (options.Sounds)
		{
			Normalize(hashSet, combatPlan.Sounds);
		}
		return combatPlan;
	}

	private Shot Evaluate(string key, List<HitFact> g, int index, Dictionary<string, HitFact> fresh, Dictionary<string, int> claimedBy, double now)
	{
		Shot shot = new Shot();
		bool flag = false;
		bool flag2 = false;
		bool flag3 = false;
		bool flag4 = false;
		bool flag5 = true;
		for (int i = 0; i < g.Count; i++)
		{
			HitFact hitFact = g[i];
			flag |= hitFact.Head;
			flag3 |= hitFact.ArmorBroke;
			flag4 |= hitFact.ArmorBroke && hitFact.Head;
			flag5 &= hitFact.Blocked;
			if (shot.Point == null && hitFact.HasHitPoint)
			{
				shot.Point = hitFact;
			}
			if (hitFact.Lethal && !string.IsNullOrEmpty(hitFact.VictimId) && fresh.ContainsKey(hitFact.VictimId))
			{
				if (!claimedBy.TryGetValue(hitFact.VictimId, out var value))
				{
					claimedBy[hitFact.VictimId] = index;
					value = index;
				}
				if (value == index)
				{
					shot.Lethal = true;
					flag2 |= hitFact.Head;
				}
			}
		}
		if (g[0].Source == HitSource.Explosive)
		{
			bool flag6 = _blasts.ContainsKey(key);
			if (!flag6)
			{
				_blasts[key] = now;
			}
			shot.AtCenter = true;
			shot.Point = null;
			shot.Marker = ((!flag6) ? MarkerShape.Single : MarkerShape.None);
			shot.Primary = (shot.Lethal ? CombatSound.ExplosiveKill : ((!flag6) ? CombatSound.ExplosiveHit : CombatSound.None));
			return shot;
		}
		shot.Marker = ((!flag) ? MarkerShape.Single : MarkerShape.Double);
		shot.Break = flag3;
		if (flag3)
		{
			shot.Icon = (flag4 ? ArmorIcon.HelmetBroken : ArmorIcon.Broken);
		}
		else if (flag5 && !shot.Lethal)
		{
			shot.Icon = ((!flag) ? ArmorIcon.Blocked : ArmorIcon.HelmetBlocked);
		}
		else
		{
			shot.Icon = ArmorIcon.None;
		}
		shot.Primary = ((!shot.Lethal) ? CombatSound.Hit : (flag2 ? CombatSound.KillHead : CombatSound.Kill));
		return shot;
	}

	private static KillNotice ToNotice(HitFact f)
	{
		return new KillNotice
		{
			VictimId = f.VictimId,
			Name = f.VictimName,
			Head = (f.Head && IsAimed(f.Source)),
			Source = f.Source,
			DistanceMeters = f.DistanceMeters
		};
	}

	private static CombatSound KillSoundFor(HitFact f)
	{
		if (f.Source == HitSource.Explosive)
		{
			return CombatSound.ExplosiveKill;
		}
		if (!f.Head || !IsAimed(f.Source))
		{
			return CombatSound.Kill;
		}
		return CombatSound.KillHead;
	}

	private static bool IsAimed(HitSource s)
	{
		if (s != HitSource.Bullet)
		{
			return s == HitSource.Melee;
		}
		return true;
	}

	private static void Normalize(HashSet<CombatSound> set, List<CombatSound> into)
	{
		if (set.Contains(CombatSound.KillHead))
		{
			set.Remove(CombatSound.Kill);
		}
		if (set.Contains(CombatSound.Kill) || set.Contains(CombatSound.KillHead))
		{
			set.Remove(CombatSound.Hit);
		}
		if (set.Contains(CombatSound.ExplosiveKill))
		{
			set.Remove(CombatSound.ExplosiveHit);
		}
		for (int i = 0; i < SoundOrder.Length; i++)
		{
			if (set.Contains(SoundOrder[i]))
			{
				into.Add(SoundOrder[i]);
			}
		}
	}

	private void PurgeBlasts(double now)
	{
		if (_blasts.Count == 0)
		{
			return;
		}
		List<string> list = null;
		foreach (KeyValuePair<string, double> blast in _blasts)
		{
			if (!(now - blast.Value <= 1.0))
			{
				if (list == null)
				{
					list = new List<string>();
				}
				list.Add(blast.Key);
			}
		}
		if (list != null)
		{
			for (int i = 0; i < list.Count; i++)
			{
				_blasts.Remove(list[i]);
			}
		}
	}
}

using System;
using System.Collections.Generic;
using System.Reflection;
using Comfort.Common;
using EFT;
using EFT.Ballistics;
using EFT.HealthSystem;
using EFT.InventoryLogic;
using Haavk.Core;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine;

namespace Haavk.Combat.Patches;

internal static class DamagePatches
{
	private enum Attacker
	{
		NotMe,
		Bridge,
		WeaponOwner
	}

	private static class ArmorBreak
	{
		private static int _frame = -1;

		private static string _profileId;

		internal static void Mark(string profileId)
		{
			_frame = Time.frameCount;
			_profileId = profileId;
		}

		internal static bool Take(string profileId)
		{
			if (_frame != Time.frameCount || _profileId != profileId)
			{
				return false;
			}
			_frame = -1;
			return true;
		}
	}

	private enum AmmoKind
	{
		Other,
		Fragment,
		GrenadeRound
	}

	private static int _a1Calls;

	private static int _a1Mine;

	private static int _a1ByBridge;

	private static int _a1ByWeapon;

	private static int _a1Irrelevant;

	private static int _a1NotMine;

	private static int _a1Self;

	private static int _a1Corpse;

	private static int _a1NoWorld;

	private static int _a1ExplosiveNotMine;

	private static int _explosiveNotes;

	private static int _a1Fragments;

	private static int _a1FragmentsByAmmo;

	private static int _a1GrenadeDirect;

	private static int _a3Calls;

	private static int _a3Breaks;

	private static int _a3Marked;

	private static int _breaksTaken;

	private static int _a4Calls;

	private static int _a4Mine;

	private static int _errors;

	private const int ErrorBudget = 5;

	private static bool _tripped;

	private static readonly HashSet<string> FragmentAmmo = new HashSet<string> { "5996f6fc86f7745e585b4de3", "63b35f281745dd52341e5da7", "5996f6cb86f774678763a6ca", "67654a6759116d347b0bfb86", "67ade494d748873e5f0161df" };

	private static readonly HashSet<string> GrenadeRounds = new HashSet<string> { "5f0c892565703e5c461894e9", "5656eb674bdc2d35148b457c", "5ede4739e0350d05467f73e8", "5ede475b549eed7c6d5c18fb", "5ede47405b097655935d7d16", "5ede474b0c226a66f5402622", "5d70e500a4b9364de70d38ce", "67446fdd752be02c220f27b3" };

	internal static int Installed { get; private set; }

	internal static void Enable(Harmony harmony)
	{
		Type typeFromHandle = typeof(DamagePatches);
		Installed += TryPatch(harmony, "A1 ActiveHealthController.ApplyDamage", AccessTools.DeclaredMethod(typeof(ActiveHealthController), "ApplyDamage", (Type[])null, (Type[])null), typeFromHandle.GetMethod("ApplyDamagePrefix", (BindingFlags)40), typeFromHandle.GetMethod("ApplyDamagePostfix", (BindingFlags)40));
		Installed += TryPatch(harmony, "A3 ArmorComponent.ApplyDurabilityDamage", AccessTools.DeclaredMethod(typeof(ArmorComponent), "ApplyDurabilityDamage", (Type[])null, (Type[])null), typeFromHandle.GetMethod("DurabilityPrefix", (BindingFlags)40), typeFromHandle.GetMethod("DurabilityPostfix", (BindingFlags)40));
		Installed += TryPatch(harmony, "A4 Player.OnDead", AccessTools.DeclaredMethod(typeof(Player), "OnDead", (Type[])null, (Type[])null), null, typeFromHandle.GetMethod("OnDeadPostfix", (BindingFlags)40));
	}

	private static int TryPatch(Harmony harmony, string what, MethodInfo target, MethodInfo prefix, MethodInfo postfix)
	{
		if (target == (MethodInfo)null)
		{
			HaavkIl2CppPlugin.Instance.Log.LogError("[Haavk|Combat] " + what + " 没找到，这一路不启用");
			return 0;
		}
		try
		{
			harmony.Patch((MethodBase)(object)target, (prefix == (MethodInfo)null) ? null : new HarmonyMethod(prefix), (postfix == (MethodInfo)null) ? null : new HarmonyMethod(postfix), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
			return 1;
		}
		catch (Exception ex)
		{
			HaavkIl2CppPlugin.Instance.Log.LogError("[Haavk|Combat] " + what + " 挂不上: " + ex);
			return 0;
		}
	}

	private static void ApplyDamagePrefix(ActiveHealthController __instance, out bool __state)
	{
		__state = false;
		try
		{
			if (!_tripped && !(__instance == null))
			{
				__state = __instance.IsAlive;
			}
		}
		catch
		{
		}
	}

	private unsafe static void ApplyDamagePostfix(ActiveHealthController __instance, EBodyPart bodyPart, DamageInfo damageInfo, bool __state)
	{
		//IL_0028: Expected Obj, but got VT
		//IL_0041: Expected Ref, but got VT
		//IL_0067: Expected Ref, but got VT
		//IL_01c6: Expected Ref, but got VT
		//IL_026a: Expected Ref, but got VT
		//IL_0299: Expected Ref, but got VT
		//IL_021f: Expected Ref, but got VT
		//IL_02ce: Expected Ref, but got VT
		try
		{
			if (_tripped)
			{
				return;
			}
			_a1Calls++;
			if (__instance == null || false)
			{
				_a1Irrelevant++;
				return;
			}
			HitSource hitSource = SourceOf(damageInfo.DamageType);
			if (hitSource == HitSource.Other)
			{
				_a1Irrelevant++;
				return;
			}
			string text = null;
			if (hitSource == HitSource.Bullet)
			{
				if (IsThrowable(damageInfo.Weapon))
				{
					text = " 破片(报Bullet)";
					_a1Fragments++;
				}
				else
				{
					switch (KindOf(damageInfo))
					{
					case AmmoKind.Fragment:
						text = " 破片(报Bullet,按破片模板)";
						_a1Fragments++;
						_a1FragmentsByAmmo++;
						break;
					case AmmoKind.GrenadeRound:
						text = " 榴弹直击(报Bullet)";
						_a1GrenadeDirect++;
						break;
					}
				}
				if (text != null)
				{
					hitSource = HitSource.Explosive;
				}
			}
			Player player = __instance.Player;
			string text2 = ((player == null) ? null : player.ProfileId);
			bool flag = ArmorBreak.Take(text2) && hitSource != HitSource.Explosive;
			Player player2 = MainPlayer();
			if (player2 == null)
			{
				_a1NoWorld++;
				return;
			}
			string profileId = player2.ProfileId;
			Attacker attacker = WhoShot(damageInfo, profileId);
			if (attacker == Attacker.NotMe)
			{
				_a1NotMine++;
				if (hitSource == HitSource.Explosive)
				{
					NoteExplosiveNotMine(damageInfo, text2, profileId);
				}
				return;
			}
			if (string.IsNullOrEmpty(text2) || text2 == profileId)
			{
				_a1Self++;
				return;
			}
			if (!__state)
			{
				_a1Corpse++;
				return;
			}
			_a1Mine++;
			if (attacker == Attacker.Bridge)
			{
				_a1ByBridge++;
			}
			else
			{
				_a1ByWeapon++;
			}
			bool flag2 = !__instance.IsAlive;
			string raw = null;
			Item weapon = damageInfo.Weapon;
			string text3 = ((weapon == null) ? "-" : weapon.Id);
			int frameCount = Time.frameCount;
			HitFact hitFact = new HitFact();
			hitFact.Source = hitSource;
			ref string shotKey = ref hitFact.ShotKey;
			string text4;
			if (hitSource != HitSource.Explosive)
			{
				text4 = frameCount + "|" + text3 + "|" + damageInfo.FireIndex;
			}
			else
			{
				text4 = ((weapon != null) ? ("E|" + text3) : ("E|-|" + frameCount + "|" + damageInfo.FireIndex));
			}
			shotKey = text4;
			hitFact.VictimId = text2;
			hitFact.Head = bodyPart == EBodyPart.Head;
			hitFact.Blocked = damageInfo.Blunt;
			hitFact.Lethal = flag2;
			HitFact hitFact2 = hitFact;
			if (hitSource != HitSource.Explosive)
			{
				hitFact2.ArmorBroke = flag;
				if (flag)
				{
					_breaksTaken++;
				}
				Vector3 hitPoint = damageInfo.HitPoint;
				hitFact2.HasHitPoint = true;
				hitFact2.HitX = hitPoint.x;
				hitFact2.HitY = hitPoint.y;
				hitFact2.HitZ = hitPoint.z;
			}
			if (flag2)
			{
				hitFact2.VictimName = Nickname(player, out raw);
				hitFact2.DistanceMeters = Vector3.Distance(player.Position, player2.Position);
			}
			if (CombatFeature.TraceOn)
			{
				hitFact2.Note = "A1 认人=" + ((attacker == Attacker.Bridge) ? "桥" : "武器归属") + " 帧=" + frameCount + text + RawSuffix(raw, hitFact2.VictimName);
			}
			FactQueue.Push(hitFact2);
		}
		catch (Exception e)
		{
			Fail("A1 后缀", e);
		}
	}

	private unsafe static Attacker WhoShot(DamageInfo d, string mainId)
	{
		//IL_0006: Expected Ref, but got VT
		//IL_003c: Expected Ref, but got VT
		string text = (d.Player?.iPlayer)?.ProfileId;
		if (!string.IsNullOrEmpty(text))
		{
			if (!(text == mainId))
			{
				return Attacker.NotMe;
			}
			return Attacker.Bridge;
		}
		string text2 = OwnerId(d.Weapon);
		if (text2 == null || !(text2 == mainId))
		{
			return Attacker.NotMe;
		}
		return Attacker.WeaponOwner;
	}

	private unsafe static void NoteExplosiveNotMine(DamageInfo d, string victimId, string mainId)
	{
		//IL_002f: Expected Ref, but got VT
		//IL_0043: Expected Ref, but got VT
		_a1ExplosiveNotMine++;
		if (CombatFeature.TraceOn && _explosiveNotes < 30)
		{
			_explosiveNotes++;
			IObserverToPlayerBridge player = d.Player;
			IPlayer player2 = player?.iPlayer;
			Item weapon = d.Weapon;
			CombatFeature.Trace("爆炸物没认出是我：桥=" + ((player == null) ? "空" : WhoIs(player2?.ProfileId, mainId)) + " 武器=" + ((weapon == null) ? "空" : weapon.Id) + " 主人=" + WhoIs(OwnerId(weapon), mainId) + " 打中=" + WhoIs(victimId, mainId) + " 帧=" + Time.frameCount);
		}
	}

	private static string WhoIs(string id, string mainId)
	{
		if (!string.IsNullOrEmpty(id))
		{
			if (!(id == mainId))
			{
				return id;
			}
			return "我";
		}
		return "空";
	}

	private static void DurabilityPrefix(ArmorComponent __instance, out float __state)
	{
		__state = -1f;
		try
		{
			if (!_tripped && !(__instance == null))
			{
				RepairableComponent repairable = __instance.Repairable;
				if (repairable != null)
				{
					__state = repairable.Durability;
				}
			}
		}
		catch
		{
		}
	}

	private static void DurabilityPostfix(ArmorComponent __instance, float armorDamage, float __state)
	{
		try
		{
			if (_tripped)
			{
				return;
			}
			_a3Calls++;
			if (__instance == null || armorDamage <= 0f || __state <= 0f)
			{
				return;
			}
			RepairableComponent repairable = __instance.Repairable;
			if (!(repairable == null) && !(repairable.Durability > 0f))
			{
				_a3Breaks++;
				string text = OwnerId(((ItemComponent)__instance).Item);
				if (text != null)
				{
					ArmorBreak.Mark(text);
					_a3Marked++;
				}
			}
		}
		catch (Exception e)
		{
			Fail("A3 后缀", e);
		}
	}

	private static void OnDeadPostfix(Player __instance, EDamageType damageType)
	{
		try
		{
			if (_tripped)
			{
				return;
			}
			_a4Calls++;
			if (__instance == null)
			{
				return;
			}
			Player player = MainPlayer();
			if (player == null)
			{
				return;
			}
			string profileId = player.ProfileId;
			string profileId2 = __instance.ProfileId;
			if (string.IsNullOrEmpty(profileId2) || profileId2 == profileId)
			{
				return;
			}
			string text = PrivAccess.PlayerLastAggressorId(__instance);
			if (text != null && !(text != profileId))
			{
				_a4Mine++;
				string text2 = Nickname(__instance, out var raw);
				HitFact hitFact = new HitFact
				{
					Source = SourceOf(damageType),
					ShotKey = null,
					VictimId = profileId2,
					VictimName = text2,
					Head = (__instance.LastDamagedBodyPart == EBodyPart.Head),
					Lethal = true,
					DistanceMeters = Vector3.Distance(__instance.Position, player.Position)
				};
				if (CombatFeature.TraceOn)
				{
					hitFact.Note = "A4 类型=" + damageType.ToString() + " 帧=" + Time.frameCount + RawSuffix(raw, text2);
				}
				FactQueue.Push(hitFact);
			}
		}
		catch (Exception e)
		{
			Fail("A4 后缀", e);
		}
	}

	private static HitSource SourceOf(EDamageType t)
	{
		switch (t)
		{
		case EDamageType.Bullet:
		case EDamageType.Blunt:
			return HitSource.Bullet;
		case EDamageType.Melee:
			return HitSource.Melee;
		case EDamageType.Explosion:
		case EDamageType.GrenadeFragment:
		case EDamageType.Landmine:
		case EDamageType.ThermobaricExplosion:
			return HitSource.Explosive;
		default:
			return HitSource.Other;
		}
	}

	private static Player MainPlayer()
	{
		if (!Singleton<GameWorld>.Instantiated)
		{
			return null;
		}
		GameWorld instance = Singleton<GameWorld>.Instance;
		if (!(instance == null))
		{
			return instance.MainPlayer;
		}
		return null;
	}

	private static bool IsThrowable(Item weapon)
	{
		if (weapon != null)
		{
			return Il2CppCast.As<ThrowWeap>(weapon) != null;
		}
		return false;
	}

	private unsafe static AmmoKind KindOf(DamageInfo d)
	{
		//IL_0006: Expected Ref, but got VT
		//IL_0021: Expected Ref, but got VT
		string sourceId = d.SourceId;
		if (sourceId == null)
		{
			return AmmoKind.Other;
		}
		if (GrenadeRounds.Contains(sourceId))
		{
			return AmmoKind.GrenadeRound;
		}
		if (d.FireIndex == 1 && FragmentAmmo.Contains(sourceId))
		{
			return AmmoKind.Fragment;
		}
		return AmmoKind.Other;
	}

	private static string OwnerId(Item item)
	{
		if (item == null)
		{
			return null;
		}
		InventoryController inventoryController = Il2CppCast.As<InventoryController>(item.Owner);
		return ((inventoryController == null) ? null : inventoryController.Profile)?.ProfileId;
	}

	private static string Nickname(Player p, out string raw)
	{
		raw = null;
		try
		{
			Profile profile = p.Profile;
			if (profile == null)
			{
				return p.ProfileId;
			}
			raw = profile.Nickname;
			string text = null;
			try
			{
				text = profile.Nickname;
			}
			catch (Exception e)
			{
				CombatFeature.WarnOnce("读 CorrectedNickname 失败，退回原名", e);
			}
			if (!string.IsNullOrEmpty(text))
			{
				return text;
			}
			return string.IsNullOrEmpty(raw) ? p.ProfileId : raw;
		}
		catch
		{
			return null;
		}
	}

	private static string RawSuffix(string raw, string shown)
	{
		if (raw == null || !(raw != shown))
		{
			return "";
		}
		return " 原名=" + raw;
	}

	private static void Fail(string where, Exception e)
	{
		_errors++;
		if (_errors == 1)
		{
			HaavkIl2CppPlugin.Instance.Log.LogError("[Haavk|Combat] " + where + " 抛了（之后只计数）: " + e);
		}
		if (_errors >= 5 && !_tripped)
		{
			_tripped = true;
			HaavkIl2CppPlugin.Instance.Log.LogError("[Haavk|Combat] 挂点连抛 " + _errors + " 次，本局停用战斗反馈（熔断，换局复位）");
		}
	}

	internal static void ResetRaid()
	{
		_a1Calls = (_a1Mine = (_a1ByBridge = (_a1ByWeapon = 0)));
		_a1Irrelevant = (_a1NotMine = (_a1Self = (_a1Corpse = (_a1NoWorld = 0))));
		_a1ExplosiveNotMine = (_explosiveNotes = (_a1Fragments = (_a1FragmentsByAmmo = (_a1GrenadeDirect = 0))));
		_a3Calls = (_a3Breaks = (_a3Marked = (_breaksTaken = 0)));
		_a4Calls = (_a4Mine = 0);
		_errors = 0;
		_tripped = false;
	}

	internal static string Summary()
	{
		return "A1 " + _a1Calls + " 次（入队 " + _a1Mine + "：桥 " + _a1ByBridge + " / 武器归属 " + _a1ByWeapon + "；破片报 Bullet 改判 " + _a1Fragments + "（其中按破片模板 " + _a1FragmentsByAmmo + "）、榴弹直击改判 " + _a1GrenadeDirect + "；没入队：类型不相关 " + _a1Irrelevant + " / 不是我打的 " + _a1NotMine + "（其中爆炸物 " + _a1ExplosiveNotMine + "） / 打的是自己 " + _a1Self + " / 鞭尸 " + _a1Corpse + " / 局外 " + _a1NoWorld + "），A3 " + _a3Calls + " 次（碎 " + _a3Breaks + "，认出主人 " + _a3Marked + "，被 A1 认领 " + _breaksTaken + "），A4 " + _a4Calls + " 次（我打死的 " + _a4Mine + "），队列满丢弃 " + FactQueue.Dropped + "，抛错 " + _errors + (_tripped ? "（已熔断）" : "");
	}

	internal static string Owners()
	{
		try
		{
			return "A1=" + OwnersOf((MethodBase)(object)AccessTools.DeclaredMethod(typeof(ActiveHealthController), "ApplyDamage", (Type[])null, (Type[])null)) + " A3=" + OwnersOf((MethodBase)(object)AccessTools.DeclaredMethod(typeof(ArmorComponent), "ApplyDurabilityDamage", (Type[])null, (Type[])null)) + " A4=" + OwnersOf((MethodBase)(object)AccessTools.DeclaredMethod(typeof(Player), "OnDead", (Type[])null, (Type[])null));
		}
		catch (Exception ex)
		{
			return "查不到(" + ((MemberInfo)ex.GetType()).Name + ")";
		}
	}

	private static string OwnersOf(MethodBase m)
	{
		if (m == (MethodBase)null)
		{
			return "<方法解析成 null>";
		}
		HarmonyLib.Patches patchInfo = Harmony.GetPatchInfo(m);
		if (patchInfo == null)
		{
			return "<没有任何 patch>";
		}
		List<string> list = new List<string>();
		foreach (Patch prefix in patchInfo.Prefixes)
		{
			list.Add("pre:" + prefix.owner);
		}
		foreach (Patch postfix in patchInfo.Postfixes)
		{
			list.Add("post:" + postfix.owner);
		}
		if (list.Count != 0)
		{
			return "[" + string.Join(",", (IEnumerable<string>)list) + "]";
		}
		return "<没有任何 patch>";
	}
}

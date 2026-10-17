using System;
using System.Collections.Generic;
using Comfort.Common;
using EFT;
using EFT.Animations;
using Haavk.Combat.Patches;
using Haavk.Core;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine;

namespace Haavk.Combat;

internal static class CombatRuntime
{
	private static readonly CombatPlanner Planner = new CombatPlanner();

	private static readonly List<HitFact> Batch = new List<HitFact>();

	private static readonly MarkerTrack Marker = new MarkerTrack();

	private static readonly IconTrack Icon = new IconTrack();

	private static readonly KillFeed Feed = new KillFeed();

	private static nint _player;

	private static bool _assetsReported;

	internal static void Draw()
	{
		bool flag = Marker.Shape != MarkerShape.None || Icon.Icon != ArmorIcon.None;
		bool visible = CrosshairRuntime.Frame.Visible;
		if (!flag && !Feed.Active && !visible)
		{
			return;
		}
		Event current = Event.current;
		if (!(current == null) && current.type == EventType.Repaint)
		{
			double now = Time.unscaledTime;
			float uiScale = CombatFeature.UiScale;
			if (visible)
			{
				CrosshairDraw.Frame(CrosshairRuntime.Frame, CrosshairRuntime.AtCenter, CrosshairRuntime.X, CrosshairRuntime.Y, uiScale);
			}
			if (flag)
			{
				CombatDraw.Frame(Marker, Icon, now, uiScale);
			}
			if (Feed.Active)
			{
				KillFeedDraw.Frame(Feed, now, uiScale, CombatFeature.FarMeters);
			}
		}
	}

	internal static void Tick()
	{
		if (!MainMenuReadyPatch.Reached)
		{
			return;
		}
		ReportAssetsOnce();
		TrackRaid();
		Feed.Update(Time.unscaledTime);
		CrosshairRuntime.Tick(Time.unscaledTime);
		Batch.Clear();
		FactQueue.DrainTo(Batch);
		if (Batch.Count == 0 || _player == IntPtr.Zero)
		{
			return;
		}
		double num = Time.unscaledTime;
		CombatPlan combatPlan = Planner.Plan((IReadOnlyList<HitFact>)Batch, CombatFeature.Options, num);
		string text = Apply(combatPlan, num);
		if (CombatFeature.TraceOn)
		{
			for (int i = 0; i < Batch.Count; i++)
			{
				CombatFeature.Trace("事实 " + Batch[i].Describe());
			}
			if (!combatPlan.IsEmpty)
			{
				CombatFeature.Trace("反馈 " + combatPlan.Describe() + text);
			}
		}
	}

	private static string Apply(CombatPlan plan, double now)
	{
		string result = "";
		if (plan.Marker != MarkerShape.None)
		{
			bool flag = !plan.MarkerAtCenter && IsAiming();
			bool flag2 = flag && CrosshairRuntime.AimPointValid;
			float x = 0f;
			float y = 0f;
			if (flag2)
			{
				x = CrosshairRuntime.AimX;
				y = CrosshairRuntime.AimY;
			}
			bool flag3 = flag2 || (!plan.MarkerAtCenter && !flag && plan.HasHitPoint && ScreenProjection.TryProject(new Vector3(plan.HitX, plan.HitY, plan.HitZ), out x, out y));
			Marker.Start(plan.Marker, plan.MarkerLethal, !flag3, x, y, now);
			if (CombatFeature.TraceOn)
			{
				if (flag2)
				{
					result = " 落点=红点(开镜)(" + x.ToString("0") + "," + y.ToString("0") + ")";
				}
				else if (flag3)
				{
					result = " 落点=(" + x.ToString("0") + "," + y.ToString("0") + ")/屏" + Screen.width + "×" + Screen.height;
				}
				else if (flag)
				{
					result = " 落点=准心(开镜，红点算不出)";
				}
				else
				{
					result = (plan.MarkerAtCenter ? " 落点=中心(爆炸物)" : " 落点=中心(投影失败)");
				}
			}
		}
		if (plan.Icon != ArmorIcon.None)
		{
			Icon.Start(plan.Icon, now);
		}
		for (int i = 0; i < plan.Kills.Count; i++)
		{
			KillNotice killNotice = plan.Kills[i];
			Feed.Add(killNotice.Head, killNotice.Name, killNotice.DistanceMeters, now);
		}
		for (int j = 0; j < plan.Sounds.Count; j++)
		{
			CombatSpeaker.Play(plan.Sounds[j]);
		}
		return result;
	}

	private static bool IsAiming()
	{
		try
		{
			if (!Singleton<GameWorld>.Instantiated)
			{
				return false;
			}
			GameWorld instance = Singleton<GameWorld>.Instance;
			Player player = ((instance == null) ? null : instance.MainPlayer);
			ProceduralWeaponAnimation proceduralWeaponAnimation = ((player == null) ? null : player.ProceduralWeaponAnimation);
			return proceduralWeaponAnimation != null && proceduralWeaponAnimation.IsAiming;
		}
		catch (Exception e)
		{
			CombatFeature.WarnOnce("读开镜状态失败", e);
			return false;
		}
	}

	private static void ReportAssetsOnce()
	{
		if (!_assetsReported)
		{
			string firstPassSummary = CombatClips.FirstPassSummary;
			if (firstPassSummary != null && CombatIcons.Done)
			{
				_assetsReported = true;
				HaavkIl2CppPlugin.Instance.Log.LogInfo("[Haavk|Combat] 战斗素材：音效 " + firstPassSummary + "；图标 " + CombatIcons.Summary);
			}
		}
	}

	private static void TrackRaid()
	{
		nint num = IntPtr.Zero;
		if (Singleton<GameWorld>.Instantiated)
		{
			GameWorld instance = Singleton<GameWorld>.Instance;
			Player player = ((instance == null) ? null : instance.MainPlayer);
			if (player != null)
			{
				num = (player).Pointer();
			}
		}
		if (num == _player)
		{
			return;
		}
		if (_player != IntPtr.Zero && CombatFeature.TraceOn)
		{
			CombatFeature.Trace("本局结束：" + DamagePatches.Summary() + "，击败 " + Planner.AnnouncedCount + " 个，放音 " + CombatSpeaker.Played + " 声（缺文件没放 " + CombatSpeaker.Missing + "），画了 " + CombatDraw.Draws + " 帧" + (CombatDraw.Faulted ? "（画熄火了）" : "") + "，击败提示画了 " + KillFeedDraw.Draws + " 帧" + (KillFeedDraw.Faulted ? "（画熄火了）" : "") + "，准星画了 " + CrosshairDraw.Draws + " 帧（投影失败 " + CrosshairRuntime.ProjectFails + "、算落点抛错 " + CrosshairRuntime.PlaceFails + "、深度跳变平滑 " + CrosshairRuntime.DepthJumps + " 次）" + (CrosshairDraw.Faulted ? "（准星画熄火了）" : ""));
		}
		_player = num;
		Planner.Reset();
		FactQueue.Clear();
		DamagePatches.ResetRaid();
		Marker.Clear();
		Icon.Clear();
		Feed.Clear();
		CombatSpeaker.ResetCounters();
		CombatDraw.Draws = 0;
		CombatDraw.Faulted = false;
		KillFeedDraw.ResetRaid();
		CrosshairRuntime.ResetRaid();
		CrosshairDraw.ResetRaid();
		CombatFeature.ResetWarnings();
		if (num != IntPtr.Zero)
		{
			CombatDraw.Prewarm(CombatFeature.UiScale);
			KillFeedDraw.Prewarm(CombatFeature.UiScale);
			if (CombatFeature.TraceOn)
			{
				CombatFeature.Trace("本局开始：挂点 " + DamagePatches.Owners());
			}
		}
	}
}

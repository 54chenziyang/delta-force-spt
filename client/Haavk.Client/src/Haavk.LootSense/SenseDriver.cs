using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Comfort.Common;
using EFT;
using EFT.Interactive;
using EFT.InventoryLogic;
using Haavk.Core;
using Haavk.Looting;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace Haavk.LootSense;

internal static class SenseDriver
{
	private sealed class Candidate
	{
		internal int Id;

		internal SenseTarget Target;

		internal Vector3 Anchor;

		internal bool Dynamic;

		internal float NextAnchor;

		internal SenseMarker Marker;

		internal bool Rejected;
	}

	private static class Stats
	{
		internal static double Discover;

		internal static double Visibility;

		internal static double Update;

		private static double _total;

		private static double _peak;

		private static int _frames;

		private static float _nextLog;

		private static readonly double MsPerTick = 1000.0 / (double)Stopwatch.Frequency;

		internal static long Lap(ref double bucket, long since)
		{
			long timestamp = Stopwatch.GetTimestamp();
			bucket += (double)(timestamp - since) * MsPerTick;
			return timestamp;
		}

		internal static void EndFrame(long start)
		{
			//IL_0072: Unknown result type (might be due to invalid IL or missing references)
			//IL_014d: Unknown result type (might be due to invalid IL or missing references)
			double num = (double)(Stopwatch.GetTimestamp() - start) * MsPerTick;
			_total += num;
			if (num > _peak)
			{
				_peak = num;
			}
			_frames++;
			float time = Time.time;
			if (!(time < _nextLog))
			{
				_nextLog = time + 10f;
				if (_frames > 0 && Candidates.Count > 0)
				{
					object[] buffer = new object[9];
					buffer[0] = (object)Candidates.Count;
					buffer[1] = (object)Live.Count;
					buffer[2] = (object)_litCount;
					buffer[3] = (object)(_total / (double)_frames);
					buffer[4] = (object)_peak;
					buffer[5] = (object)(Discover / (double)_frames);
					buffer[6] = (object)(Visibility / (double)_frames);
					buffer[7] = (object)(Update / (double)_frames);
					buffer[8] = (object)_frames;
					LootingFeature.Trace(string.Format("局内感知：候选 {0} 个，标记 {1} 个（点灯 {2}），主循环每帧平均 {3:F3} ms / 峰值 {4:F2} ms（发现 {5:F3} / 显隐 {6:F3} / 推标记 {7:F3}），统计 {8} 帧", buffer));
				}
				_total = 0.0;
				_peak = 0.0;
				_frames = 0;
				Discover = 0.0;
				Visibility = 0.0;
				Update = 0.0;
			}
		}
	}

	private const int BufferSize = 512;

	private const float DiscoverEvery = 1.5f;

	private const float DiscoverMargin = 5f;

	private const int MaxCollidersPerFrame = 96;

	private const int MaxIdentifyPerFrame = 24;

	private const float CorpseDiscoverEvery = 3f;

	private const float CorpseAnchorEvery = 1f;

	private const float StaticAnchorEvery = 3f;

	private const float VisibilityEvery = 0.25f;

	private const int MaxEvaluationsPerFrame = 8;

	private const float LightBudgetEvery = 0.25f;

	private static readonly Dictionary<int, Candidate> Candidates = new Dictionary<int, Candidate>();

	private static readonly HashSet<int> NotTargets = new HashSet<int>();

	private static readonly List<Candidate> Live = new List<Candidate>();

	private static readonly List<int> DoomedIds = new List<int>();

	private static Collider[] _buffer;

	private static int _pendingCount;

	private static int _pendingIndex;

	private static int _mask;

	private static bool _maskResolved;

	private static nint _player;

	private static float _lastPress = -10f;

	private static float _lastTrigger = -10f;

	private static float _nextDiscover;

	private static float _nextCorpseDiscover;

	private static float _nextVisibility;

	private static float _nextLightBudget;

	private static bool _pulsePending;

	private static float _pulseTime;

	private static int _evaluationsThisFrame;

	private static volatile bool _resetRequested;

	private static bool _tickFailWarned;

	private static readonly List<KeyValuePair<float, SenseMarker>> LightOrder = new List<KeyValuePair<float, SenseMarker>>();

	private static readonly Comparison<KeyValuePair<float, SenseMarker>> ByDistance = (KeyValuePair<float, SenseMarker> x, KeyValuePair<float, SenseMarker> y) => x.Key.CompareTo(y.Key);

	private static int _litCount;

	private static readonly List<int> TakenLayers = new List<int>();

	internal static void RequestReset()
	{
		_resetRequested = true;
	}

	internal static IEnumerator Loop()
	{
		yield return null;
		while (true)
		{
			Tick();
			yield return null;
		}
	}

	private static void Tick()
	{
		bool verboseLog = LootingFeature.VerboseLog;
		long start = (verboseLog ? Stopwatch.GetTimestamp() : 0);
		try
		{
			TickCore(verboseLog);
		}
		catch (Exception ex)
		{
			string data = "[Haavk|Sense] 主循环出错: " + ex;
			if (_tickFailWarned)
			{
				HaavkIl2CppPlugin.Instance.Log.LogDebug(data);
			}
			else
			{
				_tickFailWarned = true;
				HaavkIl2CppPlugin.Instance.Log.LogError(data);
			}
		}
		if (verboseLog)
		{
			Stats.EndFrame(start);
		}
	}

	private static void TickCore(bool measure)
	{
		if (_resetRequested)
		{
			_resetRequested = false;
			ClearAll();
		}
		if (!LootSenseFeature.Enabled.Value)
		{
			if (Candidates.Count > 0 || NotTargets.Count > 0)
			{
				ClearAll();
			}
			return;
		}
		Player player = LocalPlayer();
		if (player == null)
		{
			if (_player != IntPtr.Zero)
			{
				ClearAll();
				_player = IntPtr.Zero;
			}
			return;
		}
		if ((player).Pointer() != _player)
		{
			ClearAll();
			_player = (player).Pointer();
		}
		float time = Time.time;
		Vector3 position = player.Position;
		float value = LootSenseFeature.Radius.Value;
		_evaluationsThisFrame = 0;
		long since = (measure ? Stopwatch.GetTimestamp() : 0);
		if (LootSenseFeature.Trigger.Value == SenseTrigger.按键)
		{
			if (KeyTriggered(time))
			{
				StartDiscovery(position, value);
				ContinueDiscovery(int.MaxValue, int.MaxValue);
				if (LootSenseFeature.ShowCorpses.Value)
				{
					DiscoverCorpses(time);
				}
				_pulsePending = true;
				_pulseTime = time;
			}
			if (measure)
			{
				since = Stats.Lap(ref Stats.Discover, since);
			}
			if (_pulsePending)
			{
				_pulsePending = !Visibility(player, position, value, time, pulse: true, _pulseTime);
			}
			if (measure)
			{
				since = Stats.Lap(ref Stats.Visibility, since);
			}
		}
		else
		{
			if (time >= _nextDiscover && _pendingIndex >= _pendingCount)
			{
				_nextDiscover = time + 1.5f;
				StartDiscovery(position, value + 5f);
			}
			ContinueDiscovery(96, 24);
			if (LootSenseFeature.ShowCorpses.Value && time >= _nextCorpseDiscover)
			{
				_nextCorpseDiscover = time + 3f;
				DiscoverCorpses(time);
			}
			if (measure)
			{
				since = Stats.Lap(ref Stats.Discover, since);
			}
			if (time >= _nextVisibility)
			{
				_nextVisibility = (Visibility(player, position, value, time, pulse: false, time) ? (time + 0.25f) : time);
			}
			if (measure)
			{
				since = Stats.Lap(ref Stats.Visibility, since);
			}
		}
		UpdateMarkers(player, position, time, Time.deltaTime);
		if (measure)
		{
			Stats.Lap(ref Stats.Update, since);
		}
	}

	private static Player LocalPlayer()
	{
		if (!Singleton<GameWorld>.Instantiated)
		{
			return null;
		}
		GameWorld instance = Singleton<GameWorld>.Instance;
		if (instance == null)
		{
			return null;
		}
		Player mainPlayer = instance.MainPlayer;
		if (mainPlayer == null)
		{
			return null;
		}
		if (Il2CppCast.As<HideoutPlayer>(mainPlayer) != null)
		{
			return null;
		}
		return mainPlayer;
	}

	private static bool KeyTriggered(float now)
	{
		if (!LootSenseFeature.Key.Value.IsDown())
		{
			return false;
		}
		if (LootSenseFeature.DoubleTap.Value)
		{
			bool flag = SenseRule.IsDoubleTap(_lastPress, now);
			_lastPress = now;
			if (!flag)
			{
				return false;
			}
			_lastPress = -10f;
		}
		if (now - _lastTrigger < 2f)
		{
			return false;
		}
		_lastTrigger = now;
		return true;
	}

	private static void StartDiscovery(Vector3 feet, float radius)
	{
		int num = Mask();
		if (num == 0)
		{
			_pendingCount = 0;
			_pendingIndex = 0;
			return;
		}
		if (_buffer == null)
		{
			_buffer = new Collider[512];
		}
		_pendingCount = Physics.OverlapSphereNonAlloc(feet, radius, _buffer, num, QueryTriggerInteraction.Collide);
		_pendingIndex = 0;
	}

	private static void ContinueDiscovery(int maxColliders, int maxIdentify)
	{
		int num = 0;
		int num2 = 0;
		while (_pendingIndex < _pendingCount && num < maxColliders && num2 < maxIdentify)
		{
			Collider collider = _buffer[_pendingIndex++];
			num++;
			if (collider == null)
			{
				continue;
			}
			int instanceID = collider.GetInstanceID();
			if (NotTargets.Contains(instanceID) || Candidates.ContainsKey(instanceID))
			{
				continue;
			}
			num2++;
			try
			{
				SenseTarget senseTarget = Identify(collider, instanceID);
				if (senseTarget != null)
				{
					Candidates[instanceID] = new Candidate
					{
						Id = instanceID,
						Target = senseTarget,
						Anchor = senseTarget.Anchor(),
						Dynamic = false
					};
				}
			}
			catch (Exception e)
			{
				NotTargets.Add(instanceID);
				LootSenseFeature.WarnOnce("有一个碰撞体认不出来，已跳过", e);
			}
		}
	}

	private static SenseTarget Identify(Collider collider, int id)
	{
		GameObject gameObject = collider.gameObject;
		ObservedLootItem component = gameObject.GetComponent<ObservedLootItem>();
		if (component != null)
		{
			if (!LootSenseFeature.ShowLoose.Value)
			{
				NotTargets.Add(id);
				return null;
			}
			Item item = RootItem(component);
			if (item == null)
			{
				NotTargets.Add(id);
				return null;
			}
			if (Il2CppCast.As<InventoryEquipment>(item) != null)
			{
				NotTargets.Add(id);
				return null;
			}
			return new LooseTarget(component, collider, item);
		}
		LootableContainer component2 = gameObject.GetComponent<LootableContainer>();
		if (component2 != null)
		{
			if (!LootSenseFeature.ShowContainers.Value)
			{
				NotTargets.Add(id);
				return null;
			}
			return new ContainerTarget(component2, collider);
		}
		NotTargets.Add(id);
		return null;
	}

	private static void DiscoverCorpses(float now)
	{
		foreach (Corpse item in UnityEngine.Object.FindObjectsOfType<Corpse>())
		{
			if (item == null)
			{
				continue;
			}
			int instanceID = item.GetInstanceID();
			if (!NotTargets.Contains(instanceID) && !Candidates.ContainsKey(instanceID))
			{
				try
				{
					CorpseTarget corpseTarget = new CorpseTarget(item);
					Candidates[instanceID] = new Candidate
					{
						Id = instanceID,
						Target = corpseTarget,
						Anchor = corpseTarget.Anchor(),
						Dynamic = true,
						NextAnchor = now + 1f
					};
				}
				catch (Exception e)
				{
					NotTargets.Add(instanceID);
					LootSenseFeature.WarnOnce("有一具尸体认不出来，已跳过", e);
				}
			}
		}
	}

	private static int Mask()
	{
		if (_maskResolved)
		{
			return _mask;
		}
		_maskResolved = true;
		_mask = LayerMask.GetMask("Interactive");
		if (_mask == 0)
		{
			HaavkIl2CppPlugin.Instance.Log.LogError("[Haavk|Sense] 找不到 Interactive 物理层，局内感知扫不到任何散落物");
		}
		return _mask;
	}

	private static Item RootItem(ObservedLootItem loot)
	{
		ItemController itemOwner = ((LootItem)loot).ItemOwner;
		return ((itemOwner == null) ? null : itemOwner.RootItem) ?? loot.Item;
	}

	private static bool Visibility(Player player, Vector3 feet, float radius, float now, bool pulse, float pulseTime)
	{
		bool result = true;
		float num = radius * radius;
		int minTierClamped = LootSenseFeature.MinTierClamped;
		DoomedIds.Clear();
		foreach (Candidate value in Candidates.Values)
		{
			if (value.Rejected)
			{
				continue;
			}
			if ((value.Dynamic || value.Marker != null) && now >= value.NextAnchor)
			{
				value.NextAnchor = now + (value.Dynamic ? 1f : 3f);
				if (!value.Target.Alive)
				{
					DoomedIds.Add(value.Id);
					continue;
				}
				value.Anchor = value.Target.Anchor();
				if (value.Marker != null)
				{
					value.Marker.SetAnchor(value.Anchor);
				}
			}
			float num2 = SenseMath.SqrDist(value.Anchor, feet);
			if (num2 > num || !SenseRule.InHeightWindow(value.Anchor.y, feet.y))
			{
				continue;
			}
			Window((float)Math.Sqrt((double)num2), pulse, pulseTime, out var from, out var until);
			if (value.Marker != null)
			{
				value.Marker.Timeline.Extend(from, until);
				continue;
			}
			if (_evaluationsThisFrame >= 8)
			{
				result = false;
				continue;
			}
			_evaluationsThisFrame++;
			if (!value.Target.Alive)
			{
				DoomedIds.Add(value.Id);
			}
			else
			{
				Create(value, player, from, until, now, minTierClamped);
			}
		}
		foreach (int doomedId in DoomedIds)
		{
			Forget(doomedId);
		}
		DoomedIds.Clear();
		return result;
	}

	private static void Create(Candidate cand, Player player, float from, float until, float now, int minTier)
	{
		SenseFacts senseFacts;
		try
		{
			senseFacts = cand.Target.Evaluate(player);
		}
		catch (Exception e)
		{
			cand.Rejected = true;
			LootSenseFeature.WarnOnce("有一个目标算不出显示内容，已跳过", e);
			return;
		}
		if (senseFacts == null)
		{
			cand.Rejected = true;
			return;
		}
		if (!SenseRule.Passes(senseFacts.Tier, minTier))
		{
			if (senseFacts.Settled)
			{
				cand.Rejected = true;
			}
			return;
		}
		cand.Anchor = cand.Target.Anchor();
		SenseMarker senseMarker = SenseMarker.Create(cand.Id, cand.Target, senseFacts, new SenseTimeline(from, until), now, cand.Anchor, FreeLayerNear(cand.Anchor));
		if (senseMarker == null)
		{
			cand.Rejected = true;
			return;
		}
		cand.Marker = senseMarker;
		cand.NextAnchor = now + 3f;
		Live.Add(cand);
	}

	private static int FreeLayerNear(Vector3 anchor)
	{
		TakenLayers.Clear();
		foreach (Candidate item in Live)
		{
			Vector3 anchorPosition = item.Marker.AnchorPosition;
			if (SenseRule.AreNeighbours(anchorPosition.x - anchor.x, anchorPosition.y - anchor.y, anchorPosition.z - anchor.z))
			{
				TakenLayers.Add(item.Marker.Layer);
			}
		}
		return SenseRule.FirstFreeLayer((IEnumerable<int>)TakenLayers);
	}

	private static void Window(float distance, bool pulse, float pulseTime, out float from, out float until)
	{
		if (pulse)
		{
			from = pulseTime + SenseRule.PulseDelay(distance);
			until = from + LootSenseFeature.Duration.Value;
		}
		else
		{
			from = pulseTime;
			until = pulseTime + 1f;
		}
	}

	private static void UpdateMarkers(Player player, Vector3 feet, float now, float dt)
	{
		if (Live.Count == 0)
		{
			_litCount = 0;
			return;
		}
		Camera main = Camera.main;
		bool flag = main != null;
		Vector3 cameraPosition = default;
		Quaternion cameraRotation = default;
		if (flag)
		{
			Transform transform = main.transform;
			cameraPosition = transform.position;
			cameraRotation = transform.rotation;
		}
		bool nightVision = NightVisionOn(player);
		if (now >= _nextLightBudget)
		{
			_nextLightBudget = now + 0.25f;
			AssignLights(feet);
		}
		int minTierClamped = LootSenseFeature.MinTierClamped;
		for (int num = Live.Count - 1; num >= 0; num--)
		{
			Candidate candidate = Live[num];
			SenseMarker marker = candidate.Marker;
			try
			{
				if (!marker.IsAlive(now))
				{
					Drop(num);
					Forget(candidate.Id);
				}
				else
				{
					if (marker.DueForRefresh(now, feet) && _evaluationsThisFrame < 8)
					{
						_evaluationsThisFrame++;
						SenseFacts senseFacts = marker.Target.Evaluate(player);
						if (senseFacts == null || !SenseRule.Passes(senseFacts.Tier, minTierClamped))
						{
							marker.Timeline.ExpireAt(now);
							candidate.Rejected = true;
							senseFacts = null;
						}
						marker.Refresh(senseFacts, now);
					}
					marker.Timeline.Step(now, dt);
					if (marker.Timeline.Finished(now))
					{
						Drop(num);
					}
					else
					{
						marker.Apply(flag, cameraPosition, cameraRotation, nightVision);
					}
				}
			}
			catch (Exception e)
			{
				Drop(num);
				candidate.Rejected = true;
				LootSenseFeature.WarnOnce("刷新标记失败，已拆掉这一个", e);
			}
		}
	}

	private static void Drop(int i)
	{
		Candidate candidate = Live[i];
		if (candidate.Marker != null)
		{
			candidate.Marker.Destroy();
		}
		candidate.Marker = null;
		int index = Live.Count - 1;
		Live[i] = Live[index];
		Live.RemoveAt(index);
	}

	private static void Forget(int id)
	{
		if (Candidates.TryGetValue(id, out var value) && value.Marker != null)
		{
			int num = Live.IndexOf(value);
			if (num >= 0)
			{
				Drop(num);
			}
		}
		Candidates.Remove(id);
		NotTargets.Add(id);
	}

	private static void AssignLights(Vector3 feet)
	{
		int value = LootSenseFeature.MaxLights.Value;
		LightOrder.Clear();
		foreach (Candidate item in Live)
		{
			SenseMarker marker = item.Marker;
			marker.LightAllowed = false;
			LightOrder.Add(new KeyValuePair<float, SenseMarker>(SenseMath.SqrDist(marker.AnchorPosition, feet), marker));
		}
		LightOrder.Sort(ByDistance);
		int num = Math.Min(value, LightOrder.Count);
		for (int i = 0; i < num; i++)
		{
			LightOrder[i].Value.LightAllowed = true;
		}
		_litCount = num;
		LightOrder.Clear();
	}

	private static bool NightVisionOn(Player player)
	{
		try
		{
			Player.SlotObserver<NightVisionComponent> nightVisionObserver = player.NightVisionObserver;
			if (nightVisionObserver == null)
			{
				return false;
			}
			NightVisionComponent component = nightVisionObserver.Component;
			if (component == null)
			{
				return false;
			}
			TogglableComponent togglable = component.Togglable;
			return togglable != null && togglable.On;
		}
		catch (Exception)
		{
			return false;
		}
	}

	private static void ClearAll()
	{
		foreach (Candidate item in Live)
		{
			if (item.Marker != null)
			{
				item.Marker.Destroy();
			}
			item.Marker = null;
		}
		Live.Clear();
		Candidates.Clear();
		NotTargets.Clear();
		_pendingCount = 0;
		_pendingIndex = 0;
		_pulsePending = false;
		_nextDiscover = 0f;
		_nextCorpseDiscover = 0f;
		_nextVisibility = 0f;
		_nextLightBudget = 0f;
		_litCount = 0;
	}
}

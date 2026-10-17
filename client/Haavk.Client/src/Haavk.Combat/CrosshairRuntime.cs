using System;
using Comfort.Common;
using EFT;
using EFT.Animations;
using EFT.HealthSystem;
using EFT.InventoryLogic;
using Haavk.Core;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine;

namespace Haavk.Combat;

internal static class CrosshairRuntime
{
	private const float FarMeters = 100f;

	private const float MaxRayMeters = 1000f;

	private static readonly string[] RayLayers = new string[6] { "Terrain", "HighPolyCollider", "TransparentCollider", "Water", "Deadbody", "HitCollider" };

	private const int SelfSkips = 3;

	internal static bool AimPointValid;

	internal static float AimX;

	internal static float AimY;

	private const int ErrorBudget = 5;

	private static int _readErrors;

	private static int _placeErrors;

	private static readonly CrosshairModel Model = new CrosshairModel();

	private static readonly DepthSmoother Depth = new DepthSmoother();

	private static int _mask = -1;

	internal static CrosshairFrame Frame;

	internal static bool AtCenter = true;

	internal static float X;

	internal static float Y;

	internal static int ProjectFails;

	internal static int PlaceFails;

	private static CrosshairState _lastState = CrosshairState.Hidden;

	private static MoveLevel _lastLevel = MoveLevel.Still;

	private static string _lastWhy;

	private static bool _placeReported;

	private static bool _wasVisible;

	private static float _dirLength;

	internal static int DepthJumps => Depth.Jumps;

	internal static void ResetRaid()
	{
		Model.Reset();
		Depth.Reset();
		Frame = default;
		AtCenter = true;
		ProjectFails = (PlaceFails = 0);
		_readErrors = (_placeErrors = 0);
		_lastState = CrosshairState.Hidden;
		_lastLevel = MoveLevel.Still;
		_lastWhy = null;
		_placeReported = false;
		_wasVisible = false;
		_mask = -1;
		AimPointValid = false;
	}

	internal static void Tick(double now)
	{
		CrosshairInput input = Read(out var main, out var fc, out var why);
		Frame = Model.Update(input, now);
		AtCenter = true;
		AimPointValid = false;
		if (Frame.Visible && !_wasVisible)
		{
			Depth.Snap();
		}
		if (Frame.Visible)
		{
			Place(main, fc, now);
		}
		_wasVisible = Frame.Visible;
		if (input.Aiming && fc != null)
		{
			PlaceAim(fc);
		}
		if (CombatFeature.TraceOn)
		{
			TraceChange(input, why);
		}
	}

	private static CrosshairInput Read(out Player main, out Player.FirearmController fc, out string why)
	{
		CrosshairInput result = default;
		main = null;
		fc = null;
		why = null;
		if (_readErrors >= 5)
		{
			why = "读状态连抛，本局停用";
			return result;
		}
		try
		{
			main = (Singleton<GameWorld>.Instantiated ? Singleton<GameWorld>.Instance.MainPlayer : null);
			if (main == null)
			{
				why = "局外";
				return result;
			}
			ActiveHealthController activeHealthController = main.ActiveHealthController;
			if (activeHealthController != null && !activeHealthController.IsAlive)
			{
				why = "死了";
				return result;
			}
			if (main.IsInventoryOpened)
			{
				why = "背包开着";
				return result;
			}
			if (Cursor.lockState != CursorLockMode.Locked && Cursor.visible)
			{
				why = "光标没锁（菜单 / 控制台 / F12）";
				return result;
			}
			fc = Il2CppCast.As<Player.FirearmController>(main.HandsController);
			if (fc == null)
			{
				why = "手上不是枪";
				return result;
			}
			Weapon item = fc.Item;
			result.WeapClass = ((item == null) ? null : item.WeapClass);
			MovementContext movementContext = main.MovementContext;
			result.Sprint = movementContext != null && movementContext.IsSprintEnabled;
			ProceduralWeaponAnimation proceduralWeaponAnimation = main.ProceduralWeaponAnimation;
			result.Aiming = proceduralWeaponAnimation != null && proceduralWeaponAnimation.IsAiming;
			Vector3 velocity = main.Velocity;
			result.Speed = new Vector2(velocity.x, velocity.z).magnitude;
			result.Crouch = main.PoseLevel < 0.5f || main.IsInPronePose;
			result.Show = CombatFeature.CrosshairOn;
			if (!result.Show)
			{
				why = "F12 关了";
			}
		}
		catch (Exception e)
		{
			_readErrors++;
			CombatFeature.WarnOnce("读准星状态失败", e);
			why = "读状态抛了";
			result = default;
			fc = null;
		}
		return result;
	}

	private static void Place(Player main, Player.FirearmController fc, double now)
	{
		if (fc == null || CombatFeature.CrosshairAt == CrosshairPlace.屏幕中心 || _placeErrors >= 5)
		{
			return;
		}
		try
		{
			Muzzle(fc, out var pos, out var dir);
			int num = Mask();
			int num2 = SelfRoot(main);
			bool flag = false;
			float meters = 100f;
			Vector3 origin = pos;
			float num3 = 1000f;
			for (int i = 0; i <= 3; i++)
			{
				if (num == 0)
				{
					break;
				}
				if (!(num3 > 0f))
				{
					break;
				}
				RaycastHit hitInfo = default;
				if (!Physics.Raycast(origin, dir, out hitInfo, num3, num, QueryTriggerInteraction.Ignore))
				{
					break;
				}
				Collider collider = hitInfo.collider;
				if (num2 == 0 || collider == null || collider.transform.root.GetInstanceID() != num2)
				{
					meters = 1000f - num3 + hitInfo.distance;
					flag = true;
					break;
				}
				float num4 = hitInfo.distance + 0.01f;
				origin += dir * num4;
				num3 -= num4;
			}
			float num5 = Depth.Update(meters, now);
			if (ScreenProjection.TryProject(pos + dir * num5, out var x, out var y))
			{
				X = Mathf.Round(x);
				Y = Mathf.Round(y);
				AtCenter = false;
			}
			else
			{
				ProjectFails++;
				Frame.Visible = false;
			}
			if (!_placeReported && !AtCenter && CombatFeature.TraceOn)
			{
				_placeReported = true;
				CombatFeature.Trace("准星首帧：R0=" + Model.R0Em.ToString("0.00") + "em 枪口=" + V(pos) + " 方向=" + V(dir) + "（归一前模长 " + _dirLength.ToString("0.0000") + "） 射线=" + (flag ? ("命中 " + meters.ToString("0.0") + "m") : "没命中") + " 平滑后=" + num5.ToString("0.0") + "m 层掩码=0x" + num.ToString("X") + " 自己=" + num2 + " 落点=(" + X + "," + Y + ")/屏" + Screen.width + "×" + Screen.height);
			}
		}
		catch (Exception e)
		{
			PlaceFails++;
			_placeErrors++;
			CombatFeature.WarnOnce("算枪口指向点失败，退回屏幕中心", e);
		}
	}

	private static void PlaceAim(Player.FirearmController fc)
	{
		if (fc == null || _placeErrors >= 5)
		{
			return;
		}
		try
		{
			Muzzle(fc, out var pos, out var dir);
			if (ScreenProjection.TryProject(pos + dir * 100f, out var x, out var y))
			{
				AimX = Mathf.Round(x);
				AimY = Mathf.Round(y);
				AimPointValid = true;
			}
		}
		catch (Exception e)
		{
			PlaceFails++;
			_placeErrors++;
			CombatFeature.WarnOnce("算开镜落点失败，命中 X 退回屏幕中心", e);
		}
	}

	private static void Muzzle(Player.FirearmController fc, out Vector3 pos, out Vector3 dir)
	{
		pos = fc.CurrentFireport.position;
		dir = fc.WeaponDirection;
		fc.AdjustShotVectors(ref pos, ref dir);
		float num = (_dirLength = (float)Math.Sqrt((double)(dir.x * dir.x + dir.y * dir.y + dir.z * dir.z)));
		if (num > 0.0001f)
		{
			dir = new Vector3(dir.x / num, dir.y / num, dir.z / num);
		}
	}

	private static int SelfRoot(Player main)
	{
		if (main == null)
		{
			return 0;
		}
		Transform transform = ((Component)main).transform;
		if (!(transform == null))
		{
			return transform.root.GetInstanceID();
		}
		return 0;
	}

	private static string V(Vector3 v)
	{
		return "(" + v.x.ToString("0.000") + "," + v.y.ToString("0.000") + "," + v.z.ToString("0.000") + ")";
	}

	private static int Mask()
	{
		if (_mask >= 0)
		{
			return _mask;
		}
		int num = 0;
		for (int i = 0; i < RayLayers.Length; i++)
		{
			int num2 = LayerMask.NameToLayer(RayLayers[i]);
			if (num2 >= 0)
			{
				num |= 1 << num2;
			}
		}
		_mask = num;
		return num;
	}

	private static void TraceChange(CrosshairInput input, string why)
	{
		if (Model.State != _lastState || Model.Level != _lastLevel || !(why == _lastWhy))
		{
			_lastState = Model.State;
			_lastLevel = Model.Level;
			_lastWhy = why;
			CombatFeature.Trace("准星 " + Model.State.ToString() + " 档=" + Model.Level.ToString() + " 速度=" + input.Speed.ToString("0.00") + " 蹲=" + (input.Crouch ? "是" : "否") + " 枪种=" + (input.WeapClass ?? "-") + " R0=" + Model.R0Em.ToString("0.00") + "em" + ((why != null) ? (" 不画=" + why) : ""));
		}
	}
}

using System;

namespace Haavk.Core;

public static class Crosshair
{
	public const float LineEm = 0.75f;

	public const float WidthEm = 0.115f;

	public const float CrouchMul = 0.82f;

	public const float WalkMul = 1.24f;

	public const float RunMul = 1.6f;

	public const float CrouchBelowPose = 0.5f;

	public const float WalkEnter = 0.5f;

	public const float WalkExit = 0.3f;

	public const float RunEnter = 4f;

	public const float RunExit = 3.5f;

	public const float AimSeconds = 0.12f;

	public const float AimMinScale = 0.5f;

	private const float R0Small = 1f;

	private const float R0Rifle = 1.45f;

	private const float R0Marksman = 1.63f;

	private const float R0Heavy = 1.74f;

	private const float R0Sniper = 2.32f;

	private const float TweenBase = 0.2f;

	private const float TweenPerR0 = 0.2f;

	public const int DefaultCustomChannel = 200;

	public static float BaseRadiusEm(string weapClass)
	{
		if (weapClass == null)
		{
			return 1.45f;
		}
		switch (weapClass.ToLowerInvariant())
		{
		case "smg":
		case "pistol":
			return 1f;
		case "marksmanrifle":
			return 1.63f;
		case "machinegun":
		case "shotgun":
			return 1.74f;
		case "sniperrifle":
			return 2.32f;
		default:
			return 1.45f;
		}
	}

	public static MoveLevel Level(MoveLevel previous, float speed)
	{
		if ((previous == MoveLevel.Run) ? (speed >= 3.5f) : (speed >= 4f))
		{
			return MoveLevel.Run;
		}
		if (!((previous == MoveLevel.Still) ? (speed >= 0.5f) : (speed >= 0.3f)))
		{
			return MoveLevel.Still;
		}
		return MoveLevel.Walk;
	}

	public static float TransitionSeconds(float fromEm, float toEm, float r0Em)
	{
		if (!(r0Em > 0f))
		{
			return 0.2f;
		}
		return 0.2f + 0.2f * Math.Abs(toEm - fromEm) / r0Em;
	}

	public static int Rgb(CrosshairColor color, int r, int g, int b)
	{
		return color switch
		{
			CrosshairColor.绿 => 65280, 
			CrosshairColor.蓝 => 2003199, 
			CrosshairColor.紫 => 11819775, 
			CrosshairColor.红 => 16711680, 
			CrosshairColor.黄 => 16776960, 
			CrosshairColor.自定义 => (Channel(r) << 16) | (Channel(g) << 8) | Channel(b), 
			_ => 16777215, 
		};
	}

	private static int Channel(int v)
	{
		if (v >= 0)
		{
			if (v <= 255)
			{
				return v;
			}
			return 255;
		}
		return 0;
	}
}

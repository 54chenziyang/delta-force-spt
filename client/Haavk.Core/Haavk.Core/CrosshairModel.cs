namespace Haavk.Core;

public sealed class CrosshairModel
{
	private enum AimPhase
	{
		None,
		In,
		Aimed,
		Out
	}

	private bool _fresh = true;

	private double _from;

	private double _to;

	private double _tweenStart;

	private double _tweenSeconds;

	private AimPhase _aim;

	private double _aimStart;

	private bool _lastAiming;

	public CrosshairState State { get; private set; }

	public MoveLevel Level { get; private set; }

	public float R0Em { get; private set; }

	public CrosshairFrame Update(CrosshairInput input, double now)
	{
		if (!input.Show)
		{
			State = CrosshairState.Hidden;
			_fresh = true;
			return default;
		}
		float num = Crosshair.BaseRadiusEm(input.WeapClass);
		bool flag = num != R0Em;
		R0Em = num;
		Level = Crosshair.Level(Level, input.Speed);
		float num2;
		if (Level == MoveLevel.Walk)
		{
			num2 = 1.24f;
		}
		else
		{
			num2 = ((Level == MoveLevel.Run) ? 1.6f : 1f);
		}
		double num3 = (double)num * (double)(input.Crouch ? 0.82f : 1f) * (double)num2;
		if ((_fresh || input.Sprint) | flag)
		{
			_from = (_to = num3);
			_tweenSeconds = 0.0;
		}
		else if (num3 != _to)
		{
			double num4 = (_from = RadiusAt(now));
			_to = num3;
			_tweenStart = now;
			_tweenSeconds = Crosshair.TransitionSeconds((float)num4, (float)num3, num);
		}
		if (input.Sprint)
		{
			State = CrosshairState.Sprint;
			_fresh = true;
			return default;
		}
		if (_fresh)
		{
			_aim = (input.Aiming ? AimPhase.Aimed : AimPhase.None);
		}
		else if (input.Aiming != _lastAiming)
		{
			_aim = (input.Aiming ? AimPhase.In : AimPhase.Out);
			_aimStart = now;
		}
		_lastAiming = input.Aiming;
		_fresh = false;
		double num5 = 1.0;
		double num6 = 1.0;
		switch (_aim)
		{
		case AimPhase.In:
		{
			double num8 = (now - _aimStart) / 0.11999999731779099;
			if (num8 >= 1.0)
			{
				_aim = AimPhase.Aimed;
				goto case AimPhase.Aimed;
			}
			if (num8 < 0.0)
			{
				num8 = 0.0;
			}
			num5 = 1.0 + -0.5 * num8;
			num6 = 1.0 - num8;
			State = CrosshairState.AimIn;
			break;
		}
		case AimPhase.Aimed:
			State = CrosshairState.Aimed;
			return default;
		case AimPhase.Out:
		{
			double num7 = (now - _aimStart) / 0.11999999731779099;
			if (num7 >= 1.0)
			{
				_aim = AimPhase.None;
				State = CrosshairState.Hip;
				break;
			}
			if (num7 < 0.0)
			{
				num7 = 0.0;
			}
			num5 = 0.5 + 0.5 * num7 * num7;
			num6 = Clamp01(num7 / 0.5);
			State = CrosshairState.AimOut;
			break;
		}
		default:
			State = CrosshairState.Hip;
			break;
		}
		return new CrosshairFrame
		{
			Visible = true,
			Alpha = (float)num6,
			InnerEm = (float)(RadiusAt(now) * num5),
			LineEm = (float)(0.75 * num5)
		};
	}

	public void Reset()
	{
		_fresh = true;
		_from = (_to = 0.0);
		_tweenStart = 0.0;
		_tweenSeconds = 0.0;
		_aim = AimPhase.None;
		_aimStart = 0.0;
		_lastAiming = false;
		State = CrosshairState.Hidden;
		Level = MoveLevel.Still;
		R0Em = 0f;
	}

	private double RadiusAt(double now)
	{
		if (!(_tweenSeconds > 0.0))
		{
			return _to;
		}
		return _from + (_to - _from) * Clamp01((now - _tweenStart) / _tweenSeconds);
	}

	private static double Clamp01(double v)
	{
		if (!(v < 0.0))
		{
			if (!(v > 1.0))
			{
				return v;
			}
			return 1.0;
		}
		return 0.0;
	}
}

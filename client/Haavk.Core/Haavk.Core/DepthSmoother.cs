using System;

namespace Haavk.Core;

public sealed class DepthSmoother
{
	public const double TauSeconds = 0.04;

	public const float MinMeters = 0.25f;

	public const float MaxMeters = 1000f;

	public const double GapSeconds = 0.2;

	public const double JumpInverse = 0.1;

	private bool _has;

	private double _inv;

	private double _rawInv;

	private double _last;

	public int Jumps { get; private set; }

	public float Update(float meters, double now)
	{
		double num = ((double.IsNaN(meters) || meters > 1000f) ? 1000f : ((meters < 0.25f) ? 0.25f : meters));
		double num2 = 1.0 / num;
		double num3 = now - _last;
		if (!_has || !(num3 >= 0.0) || !(num3 <= 0.2))
		{
			_inv = num2;
		}
		else
		{
			if (Math.Abs(num2 - _rawInv) > 0.1)
			{
				Jumps++;
			}
			_inv += (num2 - _inv) * (1.0 - Math.Exp((0.0 - num3) / 0.04));
		}
		_has = true;
		_rawInv = num2;
		_last = now;
		return (float)(1.0 / _inv);
	}

	public void Snap()
	{
		_has = false;
	}

	public void Reset()
	{
		_has = false;
		_inv = (_rawInv = 0.0);
		_last = 0.0;
		Jumps = 0;
	}
}

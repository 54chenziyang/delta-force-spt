using System;

namespace Haavk.Core;

public sealed class TierLadder
{
	private readonly double[] _maxes;

	private readonly bool _inclusive;

	public int Count => _maxes.Length + 1;

	public TierLadder(double[] maxes, bool inclusive)
	{
		if (maxes == null || maxes.Length == 0)
		{
			throw new ArgumentException("阈值表不能为空", "maxes");
		}
		_maxes = maxes;
		_inclusive = inclusive;
	}

	public int Pick(double value)
	{
		for (int i = 0; i < _maxes.Length; i++)
		{
			if (_inclusive ? (value <= _maxes[i]) : (value < _maxes[i]))
			{
				return i;
			}
		}
		return _maxes.Length;
	}
}

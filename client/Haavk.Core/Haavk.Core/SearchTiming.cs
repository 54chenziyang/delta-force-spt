namespace Haavk.Core;

public sealed class SearchTiming
{
	public const int ExpectedTierCount = 6;

	private const int FallbackTier = 2;

	private readonly float[] _ms;

	public int TierCount => _ms.Length;

	public float FallbackMs => _ms[2];

	private SearchTiming(float[] ms)
	{
		_ms = ms;
	}

	public static bool TryCreate(double[] secondsPerTier, out SearchTiming result, out string error)
	{
		result = null;
		if (secondsPerTier == null)
		{
			error = $"没给档位耗时表（为 null），必须是 {6} 个";
			return false;
		}
		if (secondsPerTier.Length != 6)
		{
			error = $"档位耗时必须是 {6} 个，实际 {secondsPerTier.Length} 个";
			return false;
		}
		float[] array = new float[secondsPerTier.Length];
		for (int i = 0; i < secondsPerTier.Length; i++)
		{
			double num = secondsPerTier[i];
			if (double.IsNaN(num) || double.IsInfinity(num))
			{
				error = $"第 {i} 档耗时不是有限数（{num}）——它会被转成 int 喂给 Task.Delay，非有限值在那里是未定义行为";
				return false;
			}
			if (num < 0.0)
			{
				error = $"第 {i} 档耗时是负数（{num}）——Task.Delay 收到负数会抛，且异常只进 Player.log";
				return false;
			}
			array[i] = (float)(num * 1000.0);
		}
		result = new SearchTiming(array);
		error = null;
		return true;
	}

	public float DelayMs(int tier)
	{
		if (tier < 0 || tier >= _ms.Length)
		{
			return FallbackMs;
		}
		return _ms[tier];
	}
}

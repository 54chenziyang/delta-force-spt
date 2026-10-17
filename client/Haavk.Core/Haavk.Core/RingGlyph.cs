using System;

namespace Haavk.Core;

public static class RingGlyph
{
	public const float MinLinePx = 1.2f;

	public static int SizeFor(float radiusPx, float lineWidthPx)
	{
		if (float.IsNaN(radiusPx) || radiusPx < 0f)
		{
			radiusPx = 0f;
		}
		float num = HalfLine(lineWidthPx);
		return 2 * (int)Math.Ceiling(radiusPx + num) + 4;
	}

	public static byte[] Render(float radiusPx, float lineWidthPx, out int size)
	{
		if (!(radiusPx > 0f))
		{
			throw new ArgumentOutOfRangeException("radiusPx");
		}
		size = SizeFor(radiusPx, lineWidthPx);
		float num = HalfLine(lineWidthPx);
		byte[] array = new byte[size * size * 4];
		float num2 = (float)size * 0.5f;
		for (int i = 0; i < size; i++)
		{
			float num3 = (float)i + 0.5f - num2;
			for (int j = 0; j < size; j++)
			{
				float num4 = (float)j + 0.5f - num2;
				float num5 = (float)Math.Sqrt(num4 * num4 + num3 * num3);
				float num6 = Clamp01(num + 0.5f - Math.Abs(num5 - radiusPx));
				int num7 = (i * size + j) * 4;
				array[num7] = byte.MaxValue;
				array[num7 + 1] = byte.MaxValue;
				array[num7 + 2] = byte.MaxValue;
				array[num7 + 3] = (byte)Math.Round(num6 * 255f);
			}
		}
		return array;
	}

	public static float[] Steps(float fromPx, float toPx, int count)
	{
		if (count < 2)
		{
			throw new ArgumentOutOfRangeException("count");
		}
		if (!(fromPx > 0f))
		{
			throw new ArgumentOutOfRangeException("fromPx");
		}
		if (!(toPx > 0f))
		{
			throw new ArgumentOutOfRangeException("toPx");
		}
		float[] array = new float[count];
		double x = (double)toPx / (double)fromPx;
		for (int i = 0; i < count; i++)
		{
			array[i] = (float)((double)fromPx * Math.Pow(x, (double)i / (double)(count - 1)));
		}
		array[0] = fromPx;
		array[count - 1] = toPx;
		return array;
	}

	public static int Nearest(float[] steps, float radiusPx)
	{
		if (steps == null)
		{
			throw new ArgumentNullException("steps");
		}
		if (steps.Length == 0)
		{
			throw new ArgumentOutOfRangeException("steps");
		}
		if (!(radiusPx > 0f))
		{
			return 0;
		}
		double num = Math.Log(radiusPx);
		int result = 0;
		double num2 = double.MaxValue;
		for (int i = 0; i < steps.Length; i++)
		{
			if (steps[i] > 0f)
			{
				double num3 = Math.Abs(Math.Log(steps[i]) - num);
				if (num3 < num2)
				{
					num2 = num3;
					result = i;
				}
			}
		}
		return result;
	}

	private static float HalfLine(float lineWidthPx)
	{
		if (float.IsNaN(lineWidthPx))
		{
			lineWidthPx = 0f;
		}
		return Math.Max(lineWidthPx, 1.2f) * 0.5f;
	}

	private static float Clamp01(float v)
	{
		if (!(v < 0f))
		{
			if (!(v > 1f))
			{
				return v;
			}
			return 1f;
		}
		return 0f;
	}
}

using System;

namespace Haavk.Core;

public static class MarkerGlyph
{
	public struct Geometry
	{
		public float Inner;

		public float Outer;

		public float Width;

		public int Lines;

		public float Spacing;
	}

	public const float MinLinePx = 1.2f;

	public static Geometry GeometryOf(MarkerShape shape, bool lethal)
	{
		bool flag = shape == MarkerShape.Double;
		return new Geometry
		{
			Inner = ((!flag) ? (lethal ? 0.5f : 0.577f) : (lethal ? 0.4f : 0.47f)),
			Outer = (flag ? 0.87f : 1f),
			Width = 0.053f,
			Lines = ((!flag) ? 1 : 2),
			Spacing = (flag ? 0.1f : 0f)
		};
	}

	public static int SizeFor(float radiusPx)
	{
		if (float.IsNaN(radiusPx) || radiusPx < 2f)
		{
			radiusPx = 2f;
		}
		return ((int)Math.Ceiling(radiusPx) + 2) * 2;
	}

	public static byte[] Render(MarkerShape shape, bool lethal, float radiusPx, out int size)
	{
		if (shape == MarkerShape.None)
		{
			throw new ArgumentOutOfRangeException("shape");
		}
		size = SizeFor(radiusPx);
		if (float.IsNaN(radiusPx) || radiusPx < 2f)
		{
			radiusPx = 2f;
		}
		Geometry geometry = GeometryOf(shape, lethal);
		float inner = geometry.Inner * radiusPx;
		float outer = geometry.Outer * radiusPx;
		float halfW = Math.Max(geometry.Width * radiusPx, 1.2f) * 0.5f;
		float num = geometry.Spacing * radiusPx;
		byte[] array = new byte[size * size * 4];
		float num2 = (float)size * 0.5f;
		for (int i = 0; i < size; i++)
		{
			for (int j = 0; j < size; j++)
			{
				float value = (float)j + 0.5f - num2;
				float value2 = (float)i + 0.5f - num2;
				float num3 = Math.Abs(value);
				float num4 = Math.Abs(value2);
				float along = (num3 + num4) * 0.70710677f;
				float num5 = (num3 - num4) * 0.70710677f;
				float num6 = float.MaxValue;
				for (int k = 0; k < geometry.Lines; k++)
				{
					float num7 = ((float)k - (float)(geometry.Lines - 1) * 0.5f) * num;
					num6 = Math.Min(num6, BoxDistance(along, num5 - num7, inner, outer, halfW));
				}
				float num8 = Clamp01(0.5f - num6);
				int num9 = (i * size + j) * 4;
				array[num9] = byte.MaxValue;
				array[num9 + 1] = byte.MaxValue;
				array[num9 + 2] = byte.MaxValue;
				array[num9 + 3] = (byte)Math.Round(num8 * 255f);
			}
		}
		return array;
	}

	private static float BoxDistance(float along, float across, float inner, float outer, float halfW)
	{
		float num = (inner + outer) * 0.5f;
		float val = Math.Abs(along - num) - (outer - inner) * 0.5f;
		float num2 = Math.Abs(across) - halfW;
		float num3 = Math.Max(val, 0f);
		float num4 = Math.Max(num2, 0f);
		float num5 = (float)Math.Sqrt(num3 * num3 + num4 * num4);
		float num6 = Math.Min(Math.Max(val, num2), 0f);
		return num5 + num6;
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

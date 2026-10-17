using System;
using System.Text;

namespace Haavk.Core;

public static class MagnifierGlyph
{
	public const int DefaultSize = 96;

	private const int MinSize = 8;

	private const double LensCentreX = 0.4;

	private const double LensCentreY = 0.4;

	private const double LensRadius = 0.2575;

	private const double LensHalfWidth = 0.0425;

	private const double HandleAx = 0.582;

	private const double HandleAy = 0.582;

	private const double HandleBx = 0.86;

	private const double HandleBy = 0.86;

	private const double HandleHalfWidth = 0.055;

	private const double OutlineWidth = 0.03;

	private const double OutlineAlpha = 0.75;

	private const string PreviewRamp = " .:*#";

	public static byte[] Render(int size)
	{
		if (size < 8)
		{
			throw new ArgumentOutOfRangeException("size", size, $"放大镜至少要 {8}×{8} 才画得出层次");
		}
		byte[] array = new byte[size * size * 4];
		double num = 1.0 / (double)size;
		for (int i = 0; i < size; i++)
		{
			double v = ((double)i + 0.5) / (double)size;
			for (int j = 0; j < size; j++)
			{
				double num2 = Distance(((double)j + 0.5) / (double)size, v);
				double num3 = Coverage(0.5 - num2 / num);
				double num4 = Coverage(0.5 - (num2 - 0.03) / num) * 0.75;
				double num5 = num3 + num4 * (1.0 - num3);
				int num6 = (i * size + j) * 4;
				if (!(num5 <= 0.0))
				{
					array[num6 + 2] = (array[num6 + 1] = (array[num6] = ToByte(num3 / num5)));
					array[num6 + 3] = ToByte(num5);
				}
			}
		}
		return array;
	}

	public static string Preview(int size)
	{
		byte[] array = Render(size);
		StringBuilder stringBuilder = new StringBuilder(size * (size + 1));
		for (int i = 0; i < size; i++)
		{
			if (i > 0)
			{
				stringBuilder.Append('\n');
			}
			for (int j = 0; j < size; j++)
			{
				byte alpha = array[(i * size + j) * 4 + 3];
				stringBuilder.Append(" .:*#"[Level(alpha)]);
			}
		}
		return stringBuilder.ToString();
	}

	private static double Distance(double u, double v)
	{
		double num = u - 0.4;
		double num2 = v - 0.4;
		double num3 = Math.Abs(Math.Sqrt(num * num + num2 * num2) - 0.2575) - 0.0425;
		double num4 = SegmentDistance(u, v, 0.582, 0.582, 0.86, 0.86) - 0.055;
		if (!(num3 < num4))
		{
			return num4;
		}
		return num3;
	}

	private static double SegmentDistance(double px, double py, double ax, double ay, double bx, double by)
	{
		double num = bx - ax;
		double num2 = by - ay;
		double num3 = num * num + num2 * num2;
		double num4 = 0.0;
		if (num3 > 0.0)
		{
			num4 = ((px - ax) * num + (py - ay) * num2) / num3;
		}
		if (num4 < 0.0)
		{
			num4 = 0.0;
		}
		else if (num4 > 1.0)
		{
			num4 = 1.0;
		}
		double num5 = ax + num * num4;
		double num6 = ay + num2 * num4;
		return Math.Sqrt((px - num5) * (px - num5) + (py - num6) * (py - num6));
	}

	private static double Coverage(double raw)
	{
		if (raw <= 0.0)
		{
			return 0.0;
		}
		if (!(raw >= 1.0))
		{
			return raw;
		}
		return 1.0;
	}

	private static byte ToByte(double unit)
	{
		if (unit <= 0.0)
		{
			return 0;
		}
		if (unit >= 1.0)
		{
			return byte.MaxValue;
		}
		return (byte)Math.Round(unit * 255.0, MidpointRounding.AwayFromZero);
	}

	private static int Level(byte alpha)
	{
		if (alpha < 51)
		{
			return 0;
		}
		if (alpha < 102)
		{
			return 1;
		}
		if (alpha < 153)
		{
			return 2;
		}
		if (alpha < 204)
		{
			return 3;
		}
		return 4;
	}
}

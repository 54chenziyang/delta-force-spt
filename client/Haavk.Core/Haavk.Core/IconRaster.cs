using System;

namespace Haavk.Core;

public static class IconRaster
{
	public static bool AlphaBounds(RgbaImage img, out int x, out int y, out int w, out int h)
	{
		if (img == null)
		{
			throw new ArgumentNullException("img");
		}
		int num = int.MaxValue;
		int num2 = int.MaxValue;
		int num3 = -1;
		int num4 = -1;
		for (int i = 0; i < img.Height; i++)
		{
			for (int j = 0; j < img.Width; j++)
			{
				if (img.Pixels[(i * img.Width + j) * 4 + 3] != 0)
				{
					if (j < num)
					{
						num = j;
					}
					if (j > num3)
					{
						num3 = j;
					}
					if (i < num2)
					{
						num2 = i;
					}
					if (i > num4)
					{
						num4 = i;
					}
				}
			}
		}
		if (num3 < 0)
		{
			x = (y = (w = (h = 0)));
			return false;
		}
		x = num;
		y = num2;
		w = num3 - num + 1;
		h = num4 - num2 + 1;
		return true;
	}

	public static RgbaImage Fit(RgbaImage src, int boxW, int boxH, int pad)
	{
		if (src == null)
		{
			throw new ArgumentNullException("src");
		}
		if (boxW < 1)
		{
			boxW = 1;
		}
		if (boxH < 1)
		{
			boxH = 1;
		}
		if (pad < 0)
		{
			pad = 0;
		}
		int num = boxW + 2 * pad;
		int num2 = boxH + 2 * pad;
		byte[] array = new byte[num * num2 * 4];
		for (int i = 0; i < array.Length; i += 4)
		{
			int num3 = i;
			int num4 = i + 1;
			byte b;
			array[i + 2] = (b = byte.MaxValue);
			array[num3] = (array[num4] = b);
		}
		if (!AlphaBounds(src, out var x, out var y, out var w, out var h))
		{
			return new RgbaImage(num, num2, array);
		}
		double num5 = Math.Min((double)boxW / (double)w, (double)boxH / (double)h);
		int num6 = Math.Max(1, (int)Math.Round((double)w * num5));
		int num7 = Math.Max(1, (int)Math.Round((double)h * num5));
		int num8 = pad + (boxW - num6) / 2;
		int num9 = pad + (boxH - num7) / 2;
		double num10 = (double)w / (double)num6;
		double num11 = (double)h / (double)num7;
		for (int j = 0; j < num7; j++)
		{
			double num12 = (double)y + (double)j * num11;
			double num13 = num12 + num11;
			for (int k = 0; k < num6; k++)
			{
				double num14 = (double)x + (double)k * num10;
				double num15 = num14 + num10;
				double num16 = 0.0;
				double num17 = 0.0;
				double num18 = 0.0;
				double num19 = 0.0;
				double num20 = 0.0;
				for (int l = (int)Math.Floor(num12); l < Math.Min((int)Math.Ceiling(num13), src.Height); l++)
				{
					double num21 = Math.Min(num13, l + 1) - Math.Max(num12, l);
					if (num21 <= 0.0)
					{
						continue;
					}
					for (int m = (int)Math.Floor(num14); m < Math.Min((int)Math.Ceiling(num15), src.Width); m++)
					{
						double num22 = Math.Min(num15, m + 1) - Math.Max(num14, m);
						if (!(num22 <= 0.0))
						{
							double num23 = num22 * num21;
							int num24 = (l * src.Width + m) * 4;
							double num25 = (double)(int)src.Pixels[num24 + 3] / 255.0;
							num16 += (double)(int)src.Pixels[num24] * num25 * num23;
							num17 += (double)(int)src.Pixels[num24 + 1] * num25 * num23;
							num18 += (double)(int)src.Pixels[num24 + 2] * num25 * num23;
							num19 += num25 * num23;
							num20 += num23;
						}
					}
				}
				if (!(num19 <= 0.0) && !(num20 <= 0.0))
				{
					int num26 = ((num9 + j) * num + num8 + k) * 4;
					array[num26] = ToByte(num16 / num19);
					array[num26 + 1] = ToByte(num17 / num19);
					array[num26 + 2] = ToByte(num18 / num19);
					array[num26 + 3] = ToByte(num19 / num20 * 255.0);
				}
			}
		}
		return new RgbaImage(num, num2, array);
	}

	private static byte ToByte(double v)
	{
		return (byte)Math.Max(0.0, Math.Min(255.0, Math.Round(v)));
	}
}

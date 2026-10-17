using System;

namespace Haavk.Core;

public sealed class RgbaImage
{
	public readonly int Width;

	public readonly int Height;

	public readonly byte[] Pixels;

	public RgbaImage(int width, int height, byte[] pixels)
	{
		if (width <= 0)
		{
			throw new ArgumentOutOfRangeException("width");
		}
		if (height <= 0)
		{
			throw new ArgumentOutOfRangeException("height");
		}
		if (pixels == null)
		{
			throw new ArgumentNullException("pixels");
		}
		if (pixels.Length != width * height * 4)
		{
			throw new ArgumentException("长度应为 width*height*4", "pixels");
		}
		Width = width;
		Height = height;
		Pixels = pixels;
	}

	public byte[] BottomUp()
	{
		return FlipRows(Pixels, Width, Height);
	}

	public static byte[] FlipRows(byte[] rgba, int width, int height)
	{
		if (rgba == null)
		{
			throw new ArgumentNullException("rgba");
		}
		int num = width * 4;
		byte[] array = new byte[rgba.Length];
		for (int i = 0; i < height; i++)
		{
			Buffer.BlockCopy(rgba, i * num, array, (height - 1 - i) * num, num);
		}
		return array;
	}
}

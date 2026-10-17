using System;
using System.IO;
using System.IO.Compression;

namespace Haavk.Core;

public static class PngDecoder
{
	private static readonly byte[] Signature = new byte[8] { 137, 80, 78, 71, 13, 10, 26, 10 };

	public static RgbaImage Decode(byte[] png)
	{
		if (png == null)
		{
			throw new ArgumentNullException("png");
		}
		if (png.Length < 8)
		{
			throw new InvalidDataException("太短，不是 PNG");
		}
		for (int i = 0; i < 8; i++)
		{
			if (png[i] != Signature[i])
			{
				throw new InvalidDataException("PNG 签名不对");
			}
		}
		int num = 0;
		int num2 = 0;
		int num3 = 0;
		bool flag = false;
		MemoryStream memoryStream = new MemoryStream();
		int num6;
		int num5;
		for (int num4 = 8; num4 + 8 <= png.Length; num4 = num6 + num5 + 4)
		{
			num5 = ReadInt(png, num4);
			string text = new string(new char[4]
			{
				(char)png[num4 + 4],
				(char)png[num4 + 5],
				(char)png[num4 + 6],
				(char)png[num4 + 7]
			});
			num6 = num4 + 8;
			if (num5 < 0 || num6 + num5 + 4 > png.Length)
			{
				throw new InvalidDataException("块 " + text + " 越界");
			}
			switch (text)
			{
			case "IHDR":
			{
				if (num5 < 13)
				{
					throw new InvalidDataException("IHDR 太短");
				}
				num = ReadInt(png, num6);
				num2 = ReadInt(png, num6 + 4);
				int num7 = png[num6 + 8];
				int num8 = png[num6 + 9];
				byte b = png[num6 + 12];
				if (num <= 0 || num2 <= 0)
				{
					throw new InvalidDataException("尺寸非法");
				}
				if (num7 != 8)
				{
					throw new NotSupportedException("只支持 8 位，实际 " + num7);
				}
				if (b != 0)
				{
					throw new NotSupportedException("不支持隔行 PNG");
				}
				num3 = num8 switch
				{
					0 => 1, 
					2 => 3, 
					4 => 2, 
					6 => 4, 
					_ => throw new NotSupportedException("不支持颜色类型 " + num8), 
				};
				flag = true;
				continue;
			}
			case "IDAT":
				memoryStream.Write(png, num6, num5);
				continue;
			default:
				continue;
			case "IEND":
				break;
			}
			break;
		}
		if (!flag)
		{
			throw new InvalidDataException("没有 IHDR");
		}
		byte[] s = Unfilter(Inflate(memoryStream.ToArray(), num2 * (1 + num * num3)), num, num2, num3);
		return new RgbaImage(num, num2, ToRgba(s, num * num2, num3));
	}

	private static byte[] Inflate(byte[] zlib, int expected)
	{
		if (zlib.Length < 2)
		{
			throw new InvalidDataException("IDAT 为空");
		}
		int num = zlib[0];
		int num2 = zlib[1];
		if ((num & 0xF) != 8 || ((num << 8) | num2) % 31 != 0)
		{
			throw new InvalidDataException("zlib 头不对");
		}
		if ((num2 & 0x20) != 0)
		{
			throw new InvalidDataException("不支持预设字典");
		}
		byte[] array = new byte[expected];
		using MemoryStream stream = new MemoryStream(zlib, 2, zlib.Length - 2);
		using DeflateStream deflateStream = new DeflateStream(stream, CompressionMode.Decompress);
		int i;
		int num3;
		for (i = 0; i < expected; i += num3)
		{
			num3 = deflateStream.Read(array, i, expected - i);
			if (num3 <= 0)
			{
				break;
			}
		}
		if (i < expected)
		{
			throw new InvalidDataException("图像数据不够：要 " + expected + "，只有 " + i);
		}
		return array;
	}

	private static byte[] Unfilter(byte[] raw, int width, int height, int bpp)
	{
		int num = width * bpp;
		byte[] array = new byte[height * num];
		for (int i = 0; i < height; i++)
		{
			int num2 = raw[i * (num + 1)];
			int num3 = i * (num + 1) + 1;
			int num4 = i * num;
			int num5 = num4 - num;
			for (int j = 0; j < num; j++)
			{
				int num6 = ((j >= bpp) ? array[num4 + j - bpp] : 0);
				int num7 = ((i > 0) ? array[num5 + j] : 0);
				int c = ((j >= bpp && i > 0) ? array[num5 + j - bpp] : 0);
				int num8 = raw[num3 + j];
				switch (num2)
				{
				case 1:
					num8 += num6;
					break;
				case 2:
					num8 += num7;
					break;
				case 3:
					num8 += num6 + num7 >> 1;
					break;
				case 4:
					num8 += Paeth(num6, num7, c);
					break;
				default:
					throw new InvalidDataException("第 " + i + " 行滤波类型 " + num2 + " 不认识");
				case 0:
					break;
				}
				array[num4 + j] = (byte)num8;
			}
		}
		return array;
	}

	private static int Paeth(int a, int b, int c)
	{
		int num = a + b - c;
		int num2 = Math.Abs(num - a);
		int num3 = Math.Abs(num - b);
		int num4 = Math.Abs(num - c);
		if (num2 <= num3 && num2 <= num4)
		{
			return a;
		}
		if (num3 > num4)
		{
			return c;
		}
		return b;
	}

	private static byte[] ToRgba(byte[] s, int count, int channels)
	{
		if (channels == 4)
		{
			return s;
		}
		byte[] array = new byte[count * 4];
		for (int i = 0; i < count; i++)
		{
			int num = i * channels;
			int num2 = i * 4;
			switch (channels)
			{
			case 1:
				array[num2] = (array[num2 + 1] = (array[num2 + 2] = s[num]));
				array[num2 + 3] = byte.MaxValue;
				break;
			case 2:
				array[num2] = (array[num2 + 1] = (array[num2 + 2] = s[num]));
				array[num2 + 3] = s[num + 1];
				break;
			default:
				array[num2] = s[num];
				array[num2 + 1] = s[num + 1];
				array[num2 + 2] = s[num + 2];
				array[num2 + 3] = byte.MaxValue;
				break;
			}
		}
		return array;
	}

	private static int ReadInt(byte[] b, int p)
	{
		return (b[p] << 24) | (b[p + 1] << 16) | (b[p + 2] << 8) | b[p + 3];
	}
}

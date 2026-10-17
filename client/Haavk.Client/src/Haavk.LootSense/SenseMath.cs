using System;
using UnityEngine;

namespace Haavk.LootSense;

internal static class SenseMath
{
	internal static Vector3 Vec(float x, float y, float z)
	{
		return new Vector3
		{
			x = x,
			y = y,
			z = z
		};
	}

	internal static Vector3 Uniform(float s)
	{
		return Vec(s, s, s);
	}

	internal static float SqrDist(Vector3 a, Vector3 b)
	{
		float num = a.x - b.x;
		float num2 = a.y - b.y;
		float num3 = a.z - b.z;
		return num * num + num2 * num2 + num3 * num3;
	}

	internal static float Dist(Vector3 a, Vector3 b)
	{
		return (float)Math.Sqrt((double)SqrDist(a, b));
	}

	internal static bool Same(Vector3 a, Vector3 b)
	{
		if (a.x == b.x && a.y == b.y)
		{
			return a.z == b.z;
		}
		return false;
	}

	internal static Color Rgba(float r, float g, float b, float a)
	{
		return new Color
		{
			r = r,
			g = g,
			b = b,
			a = a
		};
	}

	internal static Color WithAlpha(Color c, float a)
	{
		return Rgba(c.r, c.g, c.b, a);
	}

	internal static bool Same(Color x, Color y)
	{
		if (x.r == y.r && x.g == y.g && x.b == y.b)
		{
			return x.a == y.a;
		}
		return false;
	}
}

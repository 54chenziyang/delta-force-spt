using System.Collections.Generic;
using Haavk.Core;

namespace Haavk.Combat;

internal static class FactQueue
{
	private const int Capacity = 256;

	private static readonly List<HitFact> Pending = new List<HitFact>();

	private static readonly object Gate = new object();

	internal static int Dropped;

	internal static void Push(HitFact fact)
	{
		if (fact == null)
		{
			return;
		}
		lock (Gate)
		{
			if (Pending.Count >= 256)
			{
				Dropped++;
			}
			else
			{
				Pending.Add(fact);
			}
		}
	}

	internal static void DrainTo(List<HitFact> into)
	{
		lock (Gate)
		{
			if (Pending.Count != 0)
			{
				into.AddRange((IEnumerable<HitFact>)Pending);
				Pending.Clear();
			}
		}
	}

	internal static void Clear()
	{
		lock (Gate)
		{
			Pending.Clear();
			Dropped = 0;
		}
	}
}

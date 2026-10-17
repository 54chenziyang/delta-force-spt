using System;

namespace Haavk.Core;

public sealed class SenseTimeline
{
	public float ShowFrom { get; private set; }

	public float ShowUntil { get; private set; }

	public float Intensity { get; private set; }

	public SenseTimeline(float showFrom, float showUntil)
	{
		ShowFrom = showFrom;
		ShowUntil = showUntil;
		Intensity = 0f;
	}

	public void Extend(float showFrom, float showUntil)
	{
		if (showFrom < ShowFrom)
		{
			ShowFrom = showFrom;
		}
		if (showUntil > ShowUntil)
		{
			ShowUntil = showUntil;
		}
	}

	public void ExpireAt(float now)
	{
		if (now < ShowUntil)
		{
			ShowUntil = now;
		}
	}

	public void Step(float now, float dt)
	{
		float num = ((now >= ShowFrom && now <= ShowUntil) ? 1f : 0f);
		float num2 = 2f * ((dt > 0f) ? dt : 0f);
		float num3 = ((Intensity < num) ? Math.Min(num, Intensity + num2) : Math.Max(num, Intensity - num2));
		SenseTimeline senseTimeline = this;
		float intensity;
		if (num3 < 0f)
		{
			intensity = 0f;
		}
		else
		{
			intensity = ((num3 > 1f) ? 1f : num3);
		}
		senseTimeline.Intensity = intensity;
	}

	public bool Finished(float now)
	{
		if (now > ShowUntil)
		{
			return Intensity <= 0f;
		}
		return false;
	}
}

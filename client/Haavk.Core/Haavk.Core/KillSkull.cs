namespace Haavk.Core;

public sealed class KillSkull
{
	internal float From;

	internal float To;

	internal double MoveStart;

	public bool Head { get; }

	public double Start { get; }

	internal KillSkull(bool head, double start)
	{
		Head = head;
		Start = start;
		MoveStart = start;
	}

	public float SlotAt(double now)
	{
		double num = (now - MoveStart) / 0.20000000298023224;
		if (!(num > 0.0))
		{
			return From;
		}
		if (num >= 1.0)
		{
			return To;
		}
		return (float)((double)From + (double)(To - From) * num);
	}
}

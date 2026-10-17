namespace Haavk.Core;

public sealed class KillText
{
	public bool Head { get; }

	public string Name { get; }

	public float Meters { get; }

	public double Start { get; }

	public double End { get; internal set; }

	internal KillText(bool head, string name, float meters, double start)
	{
		Head = head;
		Name = name;
		Meters = meters;
		Start = start;
		End = double.PositiveInfinity;
	}
}

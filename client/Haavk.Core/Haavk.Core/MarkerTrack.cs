namespace Haavk.Core;

public sealed class MarkerTrack
{
	private double _start = double.NegativeInfinity;

	public MarkerShape Shape { get; private set; }

	public bool Lethal { get; private set; }

	public bool AtCenter { get; private set; }

	public float X { get; private set; }

	public float Y { get; private set; }

	public void Start(MarkerShape shape, bool lethal, bool atCenter, float x, float y, double now)
	{
		if (shape != MarkerShape.None)
		{
			Shape = shape;
			Lethal = lethal;
			AtCenter = atCenter;
			X = x;
			Y = y;
			_start = now;
		}
	}

	public AnimFrame Sample(double now)
	{
		if (Shape == MarkerShape.None)
		{
			return AnimFrame.Hidden;
		}
		float num = (float)(now - _start);
		AnimFrame result = (Lethal ? CombatAnim.KillMarker(num) : CombatAnim.WhiteMarker(num));
		if (!result.Visible && num >= 0f)
		{
			Shape = MarkerShape.None;
		}
		return result;
	}

	public void Clear()
	{
		Shape = MarkerShape.None;
		_start = double.NegativeInfinity;
	}
}

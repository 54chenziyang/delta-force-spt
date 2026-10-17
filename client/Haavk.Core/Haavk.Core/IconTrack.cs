namespace Haavk.Core;

public sealed class IconTrack
{
	private double _start = double.NegativeInfinity;

	public ArmorIcon Icon { get; private set; }

	public void Start(ArmorIcon icon, double now)
	{
		if (icon != ArmorIcon.None)
		{
			Icon = icon;
			_start = now;
		}
	}

	public AnimFrame Sample(double now)
	{
		if (Icon == ArmorIcon.None)
		{
			return AnimFrame.Hidden;
		}
		float num = (float)(now - _start);
		AnimFrame result = CombatAnim.Armor(Icon, num);
		if (!result.Visible && num >= 0f)
		{
			Icon = ArmorIcon.None;
		}
		return result;
	}

	public void Clear()
	{
		Icon = ArmorIcon.None;
		_start = double.NegativeInfinity;
	}
}

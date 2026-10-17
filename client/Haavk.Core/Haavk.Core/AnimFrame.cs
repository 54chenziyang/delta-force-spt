namespace Haavk.Core;

public struct AnimFrame
{
	public bool Visible;

	public float Alpha;

	public float Scale;

	public float Shift;

	public static AnimFrame Hidden => default;

	public static AnimFrame Of(float alpha, float scale, float shift)
	{
		return new AnimFrame
		{
			Visible = true,
			Alpha = alpha,
			Scale = scale,
			Shift = shift
		};
	}
}

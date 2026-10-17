using System.Globalization;
using System.Text;

namespace Haavk.Core;

public sealed class HitFact
{
	public HitSource Source;

	public string ShotKey;

	public string VictimId;

	public string VictimName;

	public bool Head;

	public bool Blocked;

	public bool Lethal;

	public bool ArmorBroke;

	public bool HasHitPoint;

	public float HitX;

	public float HitY;

	public float HitZ;

	public float DistanceMeters;

	public string Note;

	public string Describe()
	{
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.Append(Source);
		stringBuilder.Append(" 目标=").Append(string.IsNullOrEmpty(VictimName) ? (VictimId ?? "?") : VictimName);
		stringBuilder.Append(" 部位=").Append(Head ? "头" : "身");
		stringBuilder.Append(" 挡=").Append(Blocked ? "是" : "否");
		stringBuilder.Append(" 致死=").Append(Lethal ? "是" : "否");
		stringBuilder.Append(" 碎甲=").Append(ArmorBroke ? "是" : "否");
		stringBuilder.Append(" 键=").Append(ShotKey ?? "死亡挂点");
		if (Lethal)
		{
			stringBuilder.Append(" 距离=").Append(DistanceMeters.ToString("0.0", CultureInfo.InvariantCulture)).Append('m');
		}
		if (!string.IsNullOrEmpty(Note))
		{
			stringBuilder.Append(' ').Append(Note);
		}
		return stringBuilder.ToString();
	}
}

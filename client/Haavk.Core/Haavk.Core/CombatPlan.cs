using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Haavk.Core;

public sealed class CombatPlan
{
	public MarkerShape Marker;

	public bool MarkerLethal;

	public bool MarkerAtCenter;

	public bool HasHitPoint;

	public float HitX;

	public float HitY;

	public float HitZ;

	public ArmorIcon Icon;

	public readonly List<KillNotice> Kills = new List<KillNotice>();

	public readonly List<CombatSound> Sounds = new List<CombatSound>();

	public bool IsEmpty
	{
		get
		{
			if (Marker == MarkerShape.None && Icon == ArmorIcon.None && Kills.Count == 0)
			{
				return Sounds.Count == 0;
			}
			return false;
		}
	}

	public string Describe()
	{
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.Append("X=");
		if (Marker == MarkerShape.None)
		{
			stringBuilder.Append('无');
		}
		else
		{
			stringBuilder.Append((Marker == MarkerShape.Double) ? "双线" : "单线");
			stringBuilder.Append(MarkerLethal ? "橙" : "白");
			StringBuilder stringBuilder2 = stringBuilder;
			string value;
			if (MarkerAtCenter)
			{
				value = "@中心";
			}
			else
			{
				value = (HasHitPoint ? "@命中点" : "@中心(无命中点)");
			}
			stringBuilder2.Append(value);
		}
		stringBuilder.Append(" 图标=").Append(ArmorIcons.Name(Icon));
		stringBuilder.Append(" 音=");
		if (Sounds.Count == 0)
		{
			stringBuilder.Append('无');
		}
		for (int i = 0; i < Sounds.Count; i++)
		{
			if (i > 0)
			{
				stringBuilder.Append(',');
			}
			stringBuilder.Append(Sounds[i]);
		}
		stringBuilder.Append(" 击败=");
		if (Kills.Count == 0)
		{
			stringBuilder.Append('无');
		}
		for (int j = 0; j < Kills.Count; j++)
		{
			KillNotice killNotice = Kills[j];
			if (j > 0)
			{
				stringBuilder.Append("; ");
			}
			stringBuilder.Append(string.IsNullOrEmpty(killNotice.Name) ? killNotice.VictimId : killNotice.Name);
			if (killNotice.Head)
			{
				stringBuilder.Append(" 头");
			}
			stringBuilder.Append(' ').Append(killNotice.DistanceMeters.ToString("0", CultureInfo.InvariantCulture)).Append('m');
		}
		return stringBuilder.ToString();
	}
}

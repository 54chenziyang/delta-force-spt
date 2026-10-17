using EFT;
using UnityEngine;

namespace Haavk.LootSense;

internal abstract class SenseTarget
{
	internal const float VerticalOffset = 0.22f;

	internal abstract bool Alive { get; }

	internal virtual bool Refreshable => false;

	internal abstract Vector3 Anchor();

	internal abstract SenseFacts Evaluate(Player player);

	internal static Vector3 TopOf(Collider collider)
	{
		Bounds bounds = collider.bounds;
		Vector3 center = bounds.center;
		return SenseMath.Vec(center.x, bounds.max.y + 0.22f, center.z);
	}
}

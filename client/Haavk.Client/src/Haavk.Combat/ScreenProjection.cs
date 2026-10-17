using EFT.CameraControl;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine;

namespace Haavk.Combat;

internal static class ScreenProjection
{
	internal static bool TryProject(Vector3 world, out float x, out float y)
	{
		x = 0f;
		y = 0f;
		CameraManager instance = CameraManager.Instance;
		Camera camera = ((instance == null) ? null : instance.Camera);
		if (camera == null)
		{
			return false;
		}
		Vector3 vector = camera.WorldToViewportPoint(world);
		if (vector.z <= 0f)
		{
			return false;
		}
		if (vector.x < 0f || vector.x > 1f || vector.y < 0f || vector.y > 1f)
		{
			return false;
		}
		x = vector.x * (float)Screen.width;
		y = (1f - vector.y) * (float)Screen.height;
		return true;
	}
}

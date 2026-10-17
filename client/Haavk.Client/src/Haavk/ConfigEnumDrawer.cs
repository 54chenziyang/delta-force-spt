using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace Haavk;

internal static class ConfigEnumDrawer
{
	private static string _open;

	private static readonly GUILayoutOption[] NoOptions = new GUILayoutOption[0];

	private static readonly HashSet<string> _warned = new HashSet<string>();

	internal static Action<ConfigEntryBase> For<T>() where T : struct, Enum
	{
		return (ConfigEntryBase entry) =>
		{
			Draw<T>(entry, null);
		};
	}

	internal static Action<ConfigEntryBase> For<T>(Func<T, Color> tint) where T : struct, Enum
	{
		return (ConfigEntryBase entry) =>
		{
			Draw(entry, tint);
		};
	}

	private static void Draw<T>(ConfigEntryBase entry, Func<T, Color> tint) where T : struct, Enum
	{
		T[] array;
		int num;
		string text;
		try
		{
			array = (T[])Enum.GetValues(typeof(T));
			if (array.Length == 0)
			{
				GUILayout.Label("（这个枚举没有可选值）", NoOptions);
				return;
			}
			num = Array.IndexOf(array, (T)entry.BoxedValue);
			if (num < 0)
			{
				num = 0;
			}
			text = entry.Definition.Section + "/" + entry.Definition.Key;
		}
		catch (Exception e)
		{
			Warn(entry, "读不出当前值", e);
			GUILayout.Label("（读不出当前值，见日志）", NoOptions);
			return;
		}
		bool flag = _open == text;
		bool flag2 = false;
		int num2 = -1;
		GUILayout.BeginVertical(NoOptions);
		try
		{
			if (Button(array[num].ToString() + (flag ? "  ▲" : "  ▼"), tint, array[num], entry))
			{
				flag2 = true;
			}
			if (flag)
			{
				for (int i = 0; i < array.Length; i++)
				{
					string text2;
					if (i != num)
					{
						text2 = array[i].ToString();
					}
					else
					{
						T val = array[i];
						text2 = "● " + val;
					}
					if (Button(text2, tint, array[i], entry))
					{
						num2 = i;
					}
				}
			}
		}
		catch (Exception e2)
		{
			Warn(entry, "画不出来", e2);
		}
		finally
		{
			GUILayout.EndVertical();
		}
		if (flag2)
		{
			_open = (flag ? null : text);
		}
		if (num2 < 0)
		{
			return;
		}
		_open = null;
		if (num2 == num)
		{
			return;
		}
		try
		{
			entry.BoxedValue = array[num2];
		}
		catch (Exception e3)
		{
			Warn(entry, "写不进去", e3);
		}
	}

	private static bool Button<T>(string text, Func<T, Color> tint, T value, ConfigEntryBase entry)
	{
		Color contentColor = GUI.contentColor;
		try
		{
			if (tint != null)
			{
				try
				{
					GUI.contentColor = tint(value);
				}
				catch (Exception e)
				{
					Warn(entry, "染不了色", e);
				}
			}
			return GUILayout.Button(text, NoOptions);
		}
		finally
		{
			GUI.contentColor = contentColor;
		}
	}

	private static void Warn(ConfigEntryBase entry, string what, Exception e)
	{
		string text = ((entry == null) ? "?" : (entry.Definition.Section + "/" + entry.Definition.Key));
		string data = "[Haavk] F12 枚举选择器（" + text + "）" + what + ": " + e;
		if (_warned.Add(text))
		{
			HaavkIl2CppPlugin.Instance.Log.LogError(data);
		}
		else
		{
			HaavkIl2CppPlugin.Instance.Log.LogDebug(data);
		}
	}
}

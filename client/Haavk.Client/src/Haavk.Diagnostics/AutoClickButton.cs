using System;
using System.Collections;
using System.Runtime.CompilerServices;
using EFT.UI;
using UnityEngine;

namespace Haavk.Diagnostics;

internal static class AutoClickButton
{
	private const int WaitFrames = 60;

	private const int Attempts = 10;

	internal static void Run(string nameFragment, Action<string> log)
	{
		if (!string.IsNullOrEmpty(nameFragment))
		{
			HaavkCoroutineHost.Run(ClickWhenReady(nameFragment, log));
		}
	}

	private static IEnumerator ClickWhenReady(string nameFragment, Action<string> log)
	{
		for (int attempt = 1; attempt <= 10; attempt++)
		{
			for (int i = 0; i < 60; i++)
			{
				yield return null;
			}
			DefaultUIButton defaultUIButton = null;
			string text = string.Empty;
			try
			{
				foreach (DefaultUIButton item in UnityEngine.Object.FindObjectsOfType<DefaultUIButton>())
				{
					if (!((UnityEngine.Object)(object)item == null) && ((Component)(object)item).gameObject.activeInHierarchy)
					{
						string text2 = ((Component)(object)item).gameObject.name ?? string.Empty;
						text = text + ((text.Length == 0) ? "" : ", ") + text2;
						if (item.Interactable && text2.IndexOf(nameFragment, StringComparison.OrdinalIgnoreCase) >= 0)
						{
							defaultUIButton = item;
						}
					}
				}
			}
			catch (Exception ex)
			{
				DefaultInterpolatedStringHandler val = new DefaultInterpolatedStringHandler(9, 2);
				val.AppendFormatted("[Haavk]");
				val.AppendLiteral(" 找按钮时抛了: ");
				val.AppendFormatted<Exception>(ex);
				log(val.ToStringAndClear());
				yield break;
			}
			if ((UnityEngine.Object)(object)defaultUIButton == null)
			{
				DefaultInterpolatedStringHandler val2 = new DefaultInterpolatedStringHandler(29, 5);
				val2.AppendFormatted("[Haavk]");
				val2.AppendLiteral(" 第 ");
				val2.AppendFormatted<int>(attempt);
				val2.AppendLiteral("/");
				val2.AppendFormatted<int>(10);
				val2.AppendLiteral(" 轮没找到含 \"");
				val2.AppendFormatted(nameFragment);
				val2.AppendLiteral("\" ");
				val2.AppendLiteral("的可交互按钮。当前可见: [");
				val2.AppendFormatted(text);
				val2.AppendLiteral("]");
				log(val2.ToStringAndClear());
				continue;
			}
			try
			{
				defaultUIButton.OnClick.Invoke();
				log("[Haavk] 已点击按钮 \"" + ((Component)(object)defaultUIButton).gameObject.name + "\"");
				yield break;
			}
			catch (Exception ex2)
			{
				DefaultInterpolatedStringHandler val3 = new DefaultInterpolatedStringHandler(9, 2);
				val3.AppendFormatted("[Haavk]");
				val3.AppendLiteral(" 点击按钮抛了: ");
				val3.AppendFormatted<Exception>(ex2);
				log(val3.ToStringAndClear());
				yield break;
			}
		}
		DefaultInterpolatedStringHandler val4 = new DefaultInterpolatedStringHandler(18, 3);
		val4.AppendFormatted("[Haavk]");
		val4.AppendLiteral(" ");
		val4.AppendFormatted<int>(10);
		val4.AppendLiteral(" 轮都没找到含 \"");
		val4.AppendFormatted(nameFragment);
		val4.AppendLiteral("\" 的按钮，放弃");
		log(val4.ToStringAndClear());
	}
}

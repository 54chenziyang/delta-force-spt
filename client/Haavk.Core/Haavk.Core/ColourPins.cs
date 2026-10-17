using System;
using System.Collections.Generic;

namespace Haavk.Core;

public static class ColourPins
{
	public const string FileName = "haavk-colours.json";

	public static readonly string[] TierNames = new string[6] { "Bad", "Poor", "Fair", "Good", "VeryGood", "Exceptional" };

	public static bool TryParseTier(string name, out int tier)
	{
		tier = -1;
		if (name == null)
		{
			return false;
		}
		string a = name.Trim();
		for (int i = 0; i < TierNames.Length; i++)
		{
			if (string.Equals(a, TierNames[i], StringComparison.OrdinalIgnoreCase))
			{
				tier = i;
				return true;
			}
		}
		return false;
	}

	public static ColourPinSet Merge(IEnumerable<ColourPinFile> files, Func<string, bool> templateExists)
	{
		ColourPinSet colourPinSet = new ColourPinSet();
		Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		foreach (ColourPinFile file in files)
		{
			if (file == null || file.Entries == null)
			{
				continue;
			}
			foreach (KeyValuePair<string, string> entry in file.Entries)
			{
				string text = ((entry.Key == null) ? null : entry.Key.Trim());
				if (string.IsNullOrEmpty(text))
				{
					colourPinSet.Warnings.Add(file.Source + "：有一条模板 ID 为空，跳过");
					continue;
				}
				if (!TryParseTier(entry.Value, out var tier))
				{
					colourPinSet.Warnings.Add(string.Format("{0}：{1} 的档位名 \"{2}\" 认不出，跳过（合法值：{3}）", file.Source, text, entry.Value, string.Join(" / ", TierNames)));
					continue;
				}
				if (!templateExists(text))
				{
					colourPinSet.Warnings.Add($"{file.Source}：模板 {text} 不在物品库里，跳过");
					continue;
				}
				if (colourPinSet.Tiers.TryGetValue(text, out var value) && value != tier)
				{
					colourPinSet.Warnings.Add($"{file.Source}：{text} 已被 {dictionary[text]} 指定为 {TierNames[value]}，这里改成 {TierNames[tier]}");
				}
				colourPinSet.Tiers[text] = tier;
				dictionary[text] = file.Source;
			}
		}
		return colourPinSet;
	}
}

namespace Haavk.Core;

public static class TooltipText
{
	public static string InsertAfterFirstLine(string text, string line)
	{
		if (string.IsNullOrEmpty(text))
		{
			return line;
		}
		int num = text.IndexOf('\n');
		if (num < 0)
		{
			return text + "\n" + line;
		}
		return text.Substring(0, num) + "\n" + line + text.Substring(num);
	}

	public static string ColourFirstLine(string text, string expectedFirstLine, string hexColour)
	{
		if (string.IsNullOrEmpty(text))
		{
			return text;
		}
		if (string.IsNullOrEmpty(expectedFirstLine))
		{
			return text;
		}
		if (string.IsNullOrEmpty(hexColour))
		{
			return text;
		}
		int num = text.IndexOf('\n');
		string text2 = ((num < 0) ? text : text.Substring(0, num));
		if (text2 != expectedFirstLine)
		{
			return text;
		}
		string text3 = ((num < 0) ? string.Empty : text.Substring(num));
		return "<color=" + hexColour + ">" + text2 + "</color>" + text3;
	}
}

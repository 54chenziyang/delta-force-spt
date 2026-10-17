using System;
using BepInEx.Configuration;

namespace Haavk;

internal sealed class ConfigurationManagerAttributes
{
	public string Category;

	public string DispName;

	public int? Order;

	public bool? IsAdvanced;

	public Action<ConfigEntryBase> CustomDrawer;

	public bool? Browsable;
}

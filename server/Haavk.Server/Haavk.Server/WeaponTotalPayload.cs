using System;
using System.Collections.Generic;

namespace Haavk.Server;

public class WeaponTotalPayload
{
	public Dictionary<string, double> Prices { get; set; } = new Dictionary<string, double>();

	public Dictionary<string, double> FleaMaxPrices { get; set; } = new Dictionary<string, double>();

	public Dictionary<string, double> FleaMinPrices { get; set; } = new Dictionary<string, double>();

	public string[] TierColours { get; set; } = new string[6];

	public string FleaBannedTextColour { get; set; } = "#c03a3a";

	public double[] ItemPerSlotTiers { get; set; } = new double[5];

	public string FleaBannedBackgroundColour { get; set; } = "#6e1f24";

	public bool ColourWeaponBackground { get; set; }

	public bool ColourFleaBannedWeaponBackground { get; set; }

	public double[] ArmourClassTiers { get; set; } = new double[5];

	public bool ColourArmourBackground { get; set; }

	public Dictionary<string, double> TraderPrices { get; set; } = new Dictionary<string, double>();

	public Dictionary<string, double> TraderAvgPrices { get; set; } = new Dictionary<string, double>();

	public Dictionary<string, double> TraderMinPrices { get; set; } = new Dictionary<string, double>();

	public double[] TraderItemPerSlotTiers { get; set; } = new double[5];

	public bool ColourItemBackground { get; set; }

	public bool ColourKeyBackground { get; set; }

	public string[] BackgroundColours { get; set; } = Array.Empty<string>();

	public int BackgroundColourOffset { get; set; }

	public Dictionary<string, int> ColourPins { get; set; } = new Dictionary<string, int>();
}

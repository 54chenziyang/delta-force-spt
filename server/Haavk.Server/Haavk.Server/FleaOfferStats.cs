using System.Collections.Generic;
using Haavk.Core;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Ragfair;

namespace Haavk.Server;

public static class FleaOfferStats
{
	public static Dictionary<MongoId, PriceSpread> Build(IEnumerable<RagfairOffer> offers)
	{
		Dictionary<MongoId, List<double>> dictionary = new Dictionary<MongoId, List<double>>();
		if (offers == null)
		{
			return new Dictionary<MongoId, PriceSpread>();
		}
		foreach (RagfairOffer offer in offers)
		{
			if (offer?.Items == null || offer.Items.Count != 1)
			{
				continue;
			}
			double valueOrDefault = offer.RequirementsCost.GetValueOrDefault();
			if (!(valueOrDefault <= 0.0))
			{
				MongoId template = offer.Items[0].Template;
				if (!dictionary.TryGetValue(template, out var value))
				{
					value = (dictionary[template] = new List<double>());
				}
				value.Add(valueOrDefault);
			}
		}
		Dictionary<MongoId, PriceSpread> dictionary2 = new Dictionary<MongoId, PriceSpread>(dictionary.Count);
		foreach (var (key, prices) in dictionary)
		{
			PriceSpread value2 = PriceStats.Summarise(prices);
			if (value2.Count > 0)
			{
				dictionary2[key] = value2;
			}
		}
		return dictionary2;
	}
}

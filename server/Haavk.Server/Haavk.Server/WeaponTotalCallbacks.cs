using System.Threading.Tasks;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Utils;

namespace Haavk.Server;

[Injectable(InjectionType.Transient, int.MaxValue)]
public class WeaponTotalCallbacks(HttpResponseUtil httpResponseUtil, WeaponTotalPriceStore store)
{
	public ValueTask<string> GetPrices(string url, EmptyRequestData _, MongoId sessionId)
	{
		return new ValueTask<string>(httpResponseUtil.NoBody(store.Payload));
	}
}

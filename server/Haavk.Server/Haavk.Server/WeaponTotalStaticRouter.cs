using System.Threading;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Utils;

namespace Haavk.Server;

[Injectable(InjectionType.Transient, int.MaxValue)]
public class WeaponTotalStaticRouter : StaticRouter
{
	public WeaponTotalStaticRouter(JsonUtil jsonUtil, WeaponTotalCallbacks callbacks)
		: base(jsonUtil, new _003C_003Ez__ReadOnlySingleElementList<RouteAction>(new RouteAction<EmptyRequestData>("/itemvaluation/weapontotal/prices", async (string url, EmptyRequestData info, MongoId sessionId, string? output, CancellationToken cancellationToken) => await callbacks.GetPrices(url, info, sessionId))))
	{
	}
}

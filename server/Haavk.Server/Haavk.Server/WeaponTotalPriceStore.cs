using SPTarkov.DI.Annotations;

namespace Haavk.Server;

[Injectable(InjectionType.Singleton, int.MaxValue)]
public class WeaponTotalPriceStore
{
	public WeaponTotalPayload Payload { get; set; } = new WeaponTotalPayload();
}

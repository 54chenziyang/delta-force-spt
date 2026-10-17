using System.Threading;
using System.Threading.Tasks;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers.Profile;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Utils;

namespace Haavk.TraderFill;

[Injectable(InjectionType.Transient, int.MaxValue, TypePriority = 1000102)]
public class TraderFillMod : IOnLoad
{
    private readonly ISptLogger<TraderFillMod> _logger;
    private readonly TradersTable _tradersTable;
    private readonly HandbookHelper _handbookHelper;
    private readonly HttpResponseUtil _httpResponseUtil;

    public TraderFillMod(ISptLogger<TraderFillMod> logger, TradersTable tradersTable,
        HandbookHelper handbookHelper, HttpResponseUtil httpResponseUtil)
    {
        _logger = logger;
        _tradersTable = tradersTable;
        _handbookHelper = handbookHelper;
        _httpResponseUtil = httpResponseUtil;
    }

    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        TraderFillPatch.Logger = _logger;
        TraderFillPatch.TradersTable = _tradersTable;
        TraderFillPatch.HandbookHelper = _handbookHelper;
        TraderFillPatch.HttpResponseUtil = _httpResponseUtil;
        TraderFillPatch.Enable();
        return Task.CompletedTask;
    }
}

using Microsoft.AspNetCore.SignalR;

namespace api;

public class SimulationHub(HistoryCacheService historyCacheService) : Hub
{
    public override async Task OnConnectedAsync()
    {
        var history = historyCacheService.GetHistory();

        if (history.Count != 0) await Clients.Caller.SendAsync("ReceiveHistory", history);

        await base.OnConnectedAsync();
    }
}
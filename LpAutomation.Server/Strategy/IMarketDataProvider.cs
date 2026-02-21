using LpAutomation.Core.Models;
using LpAutomation.Core.Strategy;

namespace LpAutomation.Server.Strategy;

public interface IMarketDataProvider
{
    Task<PoolSnapshot> GetSnapshotAsync(PoolKey key, CancellationToken ct);
}

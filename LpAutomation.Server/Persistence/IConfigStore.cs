using System.Collections.Generic;
using System.Threading.Tasks;
using LpAutomation.Core.Models;

namespace LpAutomation.Server.Persistence;

public interface IConfigStore
{
    Task<StrategyConfigDocument> GetCurrentAsync();
    Task<StrategyConfigDocument> SaveNewVersionAsync(StrategyConfigDocument doc, string actor);
    Task<List<ConfigVersionEntity>> ListVersionsAsync(int take);
    Task<StrategyConfigDocument?> GetVersionAsync(long id);
}
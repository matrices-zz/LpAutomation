using System.Collections.Concurrent;
using LpAutomation.Core.Models;
using LpAutomation.Core.Strategy;

namespace LpAutomation.Server.Strategy;

public interface IRecommendationStore
{
    void Add(Recommendation rec);
    IReadOnlyList<Recommendation> GetLatest(int take = 50);
}

public sealed class InMemoryRecommendationStore : IRecommendationStore
{
    private readonly ConcurrentQueue<Recommendation> _q = new();

    public void Add(Recommendation rec)
    {
        _q.Enqueue(rec);
        while (_q.Count > 500 && _q.TryDequeue(out _)) { }
    }

    public IReadOnlyList<Recommendation> GetLatest(int take = 50)
        => _q.Reverse().Take(take).ToList();
}

namespace LpAutomation.Server.Storage;

public sealed record PriceBar(
    int ChainId,
    string PoolAddress,
    DateTime TimestampUtc,
    double Open,
    double High,
    double Low,
    double Close,
    int Samples
);
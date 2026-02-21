using System;
using LpAutomation.Server.Storage;

namespace LpAutomation.Server.Strategy;

[Flags]
public enum SnapshotQualityFlag
{
    None = 0,
    NonPositivePrice = 1 << 0,
    TimestampDriftFuture = 1 << 1,
    StaleSample = 1 << 2,
    PriceJump = 1 << 3
}

public static class SnapshotQualityEvaluator
{
    private static readonly TimeSpan MaxFutureDrift = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan MaxSampleAge = TimeSpan.FromMinutes(3);
    private const double MaxSingleStepPriceJumpRatio = 0.30;

    public static SnapshotQualityFlag Evaluate(PoolSnapshot current, PoolSnapshot? previous, DateTime utcNow)
    {
        var flags = SnapshotQualityFlag.None;

        if (current.Price <= 0)
            flags |= SnapshotQualityFlag.NonPositivePrice;

        if (current.TimestampUtc > utcNow.Add(MaxFutureDrift))
            flags |= SnapshotQualityFlag.TimestampDriftFuture;

        if (utcNow - current.TimestampUtc > MaxSampleAge)
            flags |= SnapshotQualityFlag.StaleSample;

        if (previous is not null && previous.Price > 0 && current.Price > 0)
        {
            var jump = Math.Abs(current.Price - previous.Price) / previous.Price;
            if (jump > MaxSingleStepPriceJumpRatio)
                flags |= SnapshotQualityFlag.PriceJump;
        }

        return flags;
    }
}

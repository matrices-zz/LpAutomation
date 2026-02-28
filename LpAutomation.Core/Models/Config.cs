namespace LpAutomation.Core.Models;

public class Config
{
    public int ReinvestBaseScore { get; set; } = 70;
    public int ReinvestSidewaysBonus { get; set; } = 10;
    public int ReinvestTrendingPenalty { get; set; } = 15;
    public int ReinvestVolatilePenalty { get; set; } = 25;
    public double ReinvestVolNormFactor { get; set; } = 200;
    public double ReinvestVolNormMax { get; set; } = 0.5;

    public int ReallocateBaseScore { get; set; } = 30;
    public int ReallocateTrendingBonus { get; set; } = 25;
    public int ReallocateVolatileBonus { get; set; } = 35;
    public double ReallocateVolNormFactor { get; set; } = 150;
    public double ReallocateVolNormMax { get; set; } = 0.5;
        
}

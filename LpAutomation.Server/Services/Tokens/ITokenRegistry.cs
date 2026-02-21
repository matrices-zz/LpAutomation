namespace LpAutomation.Server.Services.Tokens;

public interface ITokenRegistry
{
    string NormalizeForV3(int chainId, string symbol);          // ETH -> WETH (etc.)
    string ResolveAddressOrThrow(int chainId, string symbol);   // WETH -> 0x...
}

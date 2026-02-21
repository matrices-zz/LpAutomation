namespace LpAutomation.Server.Services.Pools;

public sealed class RpcProviderOptions
{
    // RpcProviders: { DexName: { ChainId: Url } }
    public Dictionary<string, Dictionary<int, string>> RpcProviders { get; set; } = new();
}

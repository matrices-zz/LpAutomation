using Microsoft.OpenApi.Models;
using LpAutomation.Server.Persistence;
using LpAutomation.Server.Services.Pools;
using LpAutomation.Server.Strategy;
using LpAutomation.Server.Services.Tokens;
using LpAutomation.Server.Storage;
using LpAutomation.Server.PaperPositions;
using System.IO;

// 1. FORCE CONSOLE OUTPUT IMMEDIATELY
Console.WriteLine("***************************************************");
Console.WriteLine("SERVER STARTING...");
Console.WriteLine("***************************************************");

var builder = WebApplication.CreateBuilder(args);

// 2. SETUP PERSISTENT PATH
// We'll use a folder directly on C: or User profile to ensure it NEVER gets wiped by VS
string dataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LpAutomationData");

try
{
    if (!Directory.Exists(dataDir)) Directory.CreateDirectory(dataDir);
    Console.WriteLine($"[SUCCESS] Data Directory: {dataDir}");
}
catch (Exception ex)
{
    // If LocalAppData fails, fallback to a very safe temp folder
    dataDir = Path.Combine(Path.GetTempPath(), "LpAutomation_Fallback");
    Directory.CreateDirectory(dataDir);
    Console.WriteLine($"[FALLBACK] Using Temp Path: {dataDir}");
}

string dbPath = Path.Combine(dataDir, "lpautomation.db");
Console.WriteLine($"[DATABASE] Path: {dbPath}");

// 3. INITIALIZE DB
await SqliteDbInitializer.InitializeAsync(dbPath);

// 4. SERVICES
builder.Services.AddControllers();
builder.Services.AddSingleton<ITokenRegistry, InMemoryTokenRegistry>();
builder.Services.AddHttpClient<IOnChainPoolFactoryClient, JsonRpcUniswapV3FactoryClient>();
builder.Services.AddSingleton<IPoolAddressResolver, UniswapV3PoolAddressResolver>();
builder.Services.AddMemoryCache();
builder.Services.Configure<RpcProviderOptions>(builder.Configuration);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c => {
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "LpAutomation API", Version = "v1" });
});

// Register Repos with the PERSISTENT path
builder.Services.AddSingleton(new SnapshotRepository(dbPath));
builder.Services.AddSingleton(new ActivePoolRepository(dbPath));
builder.Services.AddSingleton<IConfigStore>(new SqliteConfigStore(dbPath));
builder.Services.AddSingleton<IPaperPositionStore>(new SqlitePaperPositionStore(dbPath));

builder.Services.AddSingleton<IRecommendationStore, InMemoryRecommendationStore>();
builder.Services.AddSingleton<IMarketDataProvider, StubMarketDataProvider>();
builder.Services.AddHostedService<StrategyEngineHostedService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseSwagger();
app.UseSwaggerUI(c => {
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "LpAutomation API v1");
    c.RoutePrefix = "swagger";
});

app.MapControllers();

Console.WriteLine("***************************************************");
Console.WriteLine("SERVER IS LIVE AND READY");
Console.WriteLine("***************************************************");

await app.RunAsync();
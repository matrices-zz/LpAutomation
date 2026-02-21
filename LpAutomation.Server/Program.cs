using LpAutomation.Server.Persistence;
using LpAutomation.Server.Services.Pools;
using LpAutomation.Server.Strategy;
using LpAutomation.Server.Services.Tokens;
using LpAutomation.Server.Storage;
using System.IO;

var builder = WebApplication.CreateBuilder(args);

// Controllers
builder.Services.AddControllers();
builder.Services.AddSingleton<ITokenRegistry, InMemoryTokenRegistry>();
builder.Services.AddHttpClient<IOnChainPoolFactoryClient, JsonRpcUniswapV3FactoryClient>();
builder.Services.AddSingleton<IPoolAddressResolver, UniswapV3PoolAddressResolver>();
builder.Services.AddMemoryCache();
builder.Services.Configure<RpcProviderOptions>(builder.Configuration);



// OpenAPI spec generation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// SQLite (Dapper))
var baseDir = builder.Environment.IsDevelopment()
    ? builder.Environment.ContentRootPath
    : AppContext.BaseDirectory;

var dbPath = Path.Combine(baseDir, "lpautomation.db");
await SqliteDbInitializer.InitializeAsync(dbPath);
Console.WriteLine($"SQLite path: {dbPath}");
Console.WriteLine($"[SqliteDbInitializer] Using dbPath: {dbPath}");
Console.WriteLine($"Environment: {builder.Environment.EnvironmentName}");
Console.WriteLine($"ContentRootPath: {builder.Environment.ContentRootPath}");
Console.WriteLine($"BaseDirectory: {AppContext.BaseDirectory}");
Console.WriteLine($"SQLite path (dbPath): {dbPath}");
Console.WriteLine($"SQLite exists before init: {File.Exists(dbPath)}");

await SqliteDbInitializer.InitializeAsync(dbPath);

Console.WriteLine($"SQLite exists after init: {File.Exists(dbPath)}");
if (File.Exists(dbPath))
{
    Console.WriteLine($"SQLite last write (UTC): {File.GetLastWriteTimeUtc(dbPath):O}");
}
builder.Services.AddSingleton(new SnapshotRepository(dbPath));
builder.Services.AddSingleton<IConfigStore>(new LpAutomation.Server.Persistence.SqliteConfigStore(dbPath));

builder.Services.AddSingleton<IRecommendationStore, InMemoryRecommendationStore>();
builder.Services.AddSingleton<IMarketDataProvider, StubMarketDataProvider>();
builder.Services.AddHostedService<StrategyEngineHostedService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

// OpenAPI JSON at /swagger/v1/swagger.json
app.UseSwagger();

// ReDoc at /redoc
app.UseReDoc(c =>
{
    c.RoutePrefix = "redoc";
    c.SpecUrl("/swagger/v1/swagger.json");
    c.DocumentTitle = "LpAutomation API Docs";
});

app.MapControllers();
await app.RunAsync();

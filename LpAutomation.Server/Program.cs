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

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var baseDir = builder.Environment.IsDevelopment()
    ? builder.Environment.ContentRootPath
    : AppContext.BaseDirectory;

var dbPath = Path.Combine(baseDir, "lpautomation.db");
await SqliteDbInitializer.InitializeAsync(dbPath);

builder.Services.AddSingleton(new SnapshotRepository(dbPath));
builder.Services.AddSingleton(new ActivePoolRepository(dbPath));
builder.Services.AddSingleton<IConfigStore>(new SqliteConfigStore(dbPath));

builder.Services.AddSingleton<IRecommendationStore, InMemoryRecommendationStore>();
builder.Services.AddSingleton<IMarketDataProvider, StubMarketDataProvider>();
builder.Services.AddHostedService<StrategyEngineHostedService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseSwagger();
app.UseReDoc(c =>
{
    c.RoutePrefix = "redoc";
    c.SpecUrl("/swagger/v1/swagger.json");
    c.DocumentTitle = "LpAutomation API Docs";
});

app.MapControllers();
await app.RunAsync();

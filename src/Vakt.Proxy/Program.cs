using Azure.Monitor.OpenTelemetry.AspNetCore;
using Vakt.Core.Extensions;
using Vakt.Core.Interfaces;
using Vakt.Core.Services;

using StackExchange.Redis;
using Vakt.Proxy.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Redis Client for Vector Search
builder.AddRedisClient("cache");
builder.AddRedisDistributedCache("cache");

// Observability (Azure Monitor)
if (!string.IsNullOrEmpty(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
{
    builder.Services.AddOpenTelemetry().UseAzureMonitor();
}

// Register HttpClient for Intelligence Service (Embeddings)
builder.Services.AddHttpClient("vakt-intelligence", client => 
{
    client.BaseAddress = new Uri("http://vakt-intelligence");
});

// 1. Register Vakt SDK
// This adds the Brain (IntelligenceService), the SovereignTransform, and YARP.
builder.Services.AddVaktProxy(builder.Configuration)
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

app.UseMiddleware<SemanticCacheMiddleware>();

app.MapDefaultEndpoints();
app.MapReverseProxy();

// Ensure Redis Index Exists (Semantic Cache)
using (var scope = app.Services.CreateScope())
{
    var options = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<Vakt.Core.Options.SemanticCacheOptions>>().Value;
    var indexName = options.IndexName;
    
    var muxer = scope.ServiceProvider.GetRequiredService<IConnectionMultiplexer>();
    var db = muxer.GetDatabase();
    try 
    {
        await db.ExecuteAsync("FT.INFO", indexName);
    }
    catch (Exception)
    {
        try 
        {
            await db.ExecuteAsync("FT.CREATE", indexName, "ON", "HASH", "PREFIX", "1", options.RedisPrefix, "SCHEMA", 
                "embedding", "VECTOR", "HNSW", "6", "TYPE", "FLOAT32", "DIM", options.EmbeddingDimension.ToString(), "DISTANCE_METRIC", "COSINE", 
                "response", "TEXT");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Failed to create Redis Vector Index: {ex.Message}");
        }
    }
}

// 🚀 Provision Model on Startup
// This ensures cross-platform compatibility without shell scripts.
using (var scope = app.Services.CreateScope())
{
    var provisioner = scope.ServiceProvider.GetRequiredService<IModelProvisioningService>();
    await provisioner.EnsureModelExistsAsync();
}

app.Run();

public partial class Program { }

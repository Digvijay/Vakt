using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Vakt.Core.Interfaces;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Yarp.ReverseProxy.Transforms;
using Yarp.ReverseProxy.Transforms.Builder;
using StackExchange.Redis;
using System.Net.Http.Json;

namespace Vakt.Core.Transforms;

/// <summary>
/// A YARP transform that intercepts request bodies and redacts PII using local intelligence.
/// </summary>
/// <summary>
/// A YARP transform that intercepts request bodies and redacts PII using local intelligence.
/// </summary>
public partial class SovereignTransform(
    IIntelligenceService brain, 
    IAuditLogger auditLogger,
    ILogger<SovereignTransform> logger,
    IHttpClientFactory httpFactory,
    IConnectionMultiplexer redis,
    Microsoft.Extensions.Options.IOptions<Vakt.Core.Options.SemanticCacheOptions> options) : ITransformProvider
{
    private readonly Vakt.Core.Options.SemanticCacheOptions _options = options.Value;
    private static readonly ActivitySource VaktActivitySource = new("Vakt.Proxy");
    private static readonly Meter VaktMeter = new("Vakt.Proxy");
    private static readonly Counter<double> RiskSavedCounter = VaktMeter.CreateCounter<double>("vakt.risk.saved_value", "USD", "Estimated value of GDPR fines avoided");

    public void ValidateRoute(TransformRouteValidationContext context)
    {
        // No validation needed for now
    }

    public void ValidateCluster(TransformClusterValidationContext context)
    {
        // No validation needed for now
    }

    /// <inheritdoc />
    public void Apply(TransformBuilderContext context)
    {
        // Register the per-request logic
        context.AddRequestTransform(async transformContext =>
        {
            var httpContext = transformContext.HttpContext;
            var proxyRequest = transformContext.ProxyRequest;
            var ct = httpContext.RequestAborted;

            // 🔍 OTel: Start "GenAI" span
            using var activity = VaktActivitySource.StartActivity("PII_Redaction_Check");

            if (httpContext.Request.Method == HttpMethods.Post && httpContext.Request.ContentLength > 0)
            {
                httpContext.Request.EnableBuffering();
                if (httpContext.Request.Body.CanSeek)
                    httpContext.Request.Body.Position = 0;

                try 
                {
                    // 1. Optimistic Parse (Avoid String Allocation of Body)
                    var jsonNode = await JsonNode.ParseAsync(httpContext.Request.Body, cancellationToken: ct);
                    
                    // Reset position after read (ParseAsync consumes it)
                    httpContext.Request.Body.Position = 0;

                    var messages = jsonNode?["messages"]?.AsArray();
                    
                    if (messages is not null)
                    {
                        // 2. SEMANTIC CACHE CHECK
                        // Extract last user message for embedding
                        var lastUserMessage = messages.Reverse().FirstOrDefault(m => m?["role"]?.GetValue<string>() == "user");
                        string? prompt = lastUserMessage?["content"]?.GetValue<string>();

                        if (!string.IsNullOrEmpty(prompt))
                        {
                            activity?.SetTag("gen_ai.prompt", prompt);
                            
                            // A. Generate Embedding
                            var client = httpFactory.CreateClient("vakt-intelligence");
                            float[]? embedding = null;
                            try 
                            {
                                var response = await client.PostAsJsonAsync("/embeddings", prompt, ct);
                                if (response.IsSuccessStatusCode)
                                {
                                    embedding = await response.Content.ReadFromJsonAsync<float[]>(cancellationToken: ct);
                                }
                            }
                            catch (Exception ex)
                            {
                                logger.LogWarning(ex, "Failed to get embedding. Skipping cache check.");
                            }

                            if (embedding != null && embedding.Length == _options.EmbeddingDimension)
                            {
                                // B. Query Redis Vector Index
                                var db = redis.GetDatabase();
                                var query = new object[] 
                                {
                                    _options.IndexName,
                                    "*=>[KNN 1 @embedding $BLOB AS score]",
                                    "PARAMS", "2", "BLOB", GetBytes(embedding),
                                    "RETURN", "1", "response",
                                    "SORTBY", "score", "ASC",
                                    "DIALECT", "2"
                                };

                                try 
                                {
                                    var result = await db.ExecuteAsync("FT.SEARCH", query);
                                    // Result format: [TotalResults, Key1, [Field1, Val1, ...]]
                                    // FT.SEARCH returns RedisResult[] usually if successful
                                    
                                    // Explicit cast needed for RedisResult
                                    var results = (RedisResult[])result!;
                                    
                                    if (results != null && results.Length > 0)
                                    {
                                        // results[0] is total count (long)
                                        var totalResults = (long)results[0];
                                        if (totalResults > 0 && results.Length > 2)
                                        {
                                            // Cast safely logic - try explicit cast
                                            RedisResult[]? fields = null;
                                            try 
                                            {
                                                fields = (RedisResult[]?)results[2];
                                            }
                                            catch 
                                            {
                                                // ignore cast fail
                                            }

                                            if (fields != null)
                                            {
                                                string? cachedResponse = null;
                                                string? scoreStr = null;
                                                
                                                for(int i=0; i<fields.Length; i+=2)
                                                {
                                                    // RedisResult can be cast to string/double/etc.
                                                    // Null check explicitly
                                                    var keyRes = fields[i];
                                                    var valRes = fields[i+1];
                                                    
                                                    string? outputKey = keyRes.IsNull ? null : (string?)keyRes;
                                                    
                                                    if (outputKey == "response") cachedResponse = valRes.IsNull ? null : (string?)valRes;
                                                    if (outputKey == "score") scoreStr = valRes.IsNull ? null : (string?)valRes; 
                                                }

                                                if (double.TryParse(scoreStr, out var score) && score < _options.SimilarityThreshold) // Tolerance
                                                {
                                                    // CACHE HIT!
                                                    // Short-circuit response
                                                    logger.LogInformation("💰 Semantic Cache HIT! (Score: {Score})", score);
                                                    RiskSavedCounter.Add(10); // Savings
                                                    
                                                    // Write Response
                                                    httpContext.Response.StatusCode = 200;
                                                    httpContext.Response.ContentType = "application/json";
                                                    if (!string.IsNullOrEmpty(cachedResponse))
                                                    {
                                                        await httpContext.Response.WriteAsync(cachedResponse, ct);
                                                    }
                                                    
                                                    return; 
                                                }
                                            }
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                     logger.LogWarning(ex, "Redis Vector Search failed");
                                }
                                
                                // Store embedding for Response capture
                                httpContext.Items["RequestEmbedding"] = embedding;
                            }
                        }

                        // ... PII Redaction Logic ...
                        bool modified = false;
                        foreach (var msg in messages)
                        {
                            var content = msg?["content"]?.GetValue<string>();
                            if (!string.IsNullOrEmpty(content))
                            {
                                // 🧠 CALL PHI-3
                                var redacted = brain.RedactPii(content);
                                
                                // 3. Check for Changes
                                bool wasModified = !string.Equals(content, redacted, StringComparison.Ordinal);
                                
                                // 📜 AUDIT LOG (Compliance)
                                await auditLogger.LogEventAsync(httpContext.TraceIdentifier, content, redacted, wasModified);

                                if (wasModified)
                                {
                                    // 🚨 PII DETECTED
                                    LogPiiDetected(logger);
                                    
                                    // OTel: Risk Tags
                                    activity?.SetTag("vakt.pii_detected", true);
                                    activity?.SetTag("vakt.risk.action", "REDACTED");
                                    activity?.AddEvent(new ActivityEvent("PII_Sanitized"));

                                    // 💰 Money Printer
                                    RiskSavedCounter.Add(5000); 

                                    // 4. Update JSON
                                    msg!["content"] = redacted;
                                    modified = true;
                                }
                            }
                        }

                        if (modified)
                        {
                            var newBody = jsonNode!.ToJsonString();
                            var bytes = Encoding.UTF8.GetBytes(newBody);
                            
                            proxyRequest.Content = new ByteArrayContent(bytes);
                            proxyRequest.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                            proxyRequest.Content.Headers.ContentLength = bytes.Length;
                        }
                    }
                }
                catch (JsonException)
                {
                   // Fallback
                   // ...
                }
            }
        });


    }

    private static byte[] GetBytes(float[] floatArray)
    {
        var byteArray = new byte[floatArray.Length * 4];
        Buffer.BlockCopy(floatArray, 0, byteArray, 0, byteArray.Length);
        return byteArray;
    }

    // Source Generated Logging
    [LoggerMessage(Level = LogLevel.Warning, Message = "🚨 PII DETECTED! Redacting...")]
    static partial void LogPiiDetected(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "🛡️ VAKT INTERCEPT (Non-JSON): {Body}")]
    static partial void LogNonJsonIntercepted(ILogger logger, string body);
}

using StackExchange.Redis;
using System.Text;

namespace Vakt.Proxy.Middleware;

public class SemanticCacheMiddleware(RequestDelegate next, IConnectionMultiplexer redis, ILogger<SemanticCacheMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        // 1. Wrap Response Stream to capture output
        var originalBodyStream = context.Response.Body;
        using var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        try
        {
            // 2. Proceed down pipeline (YARP will run here)
            await next(context);

            // 3. On Return: Check if we need to cache
            if (context.Response.StatusCode == 200 && context.Items.TryGetValue("RequestEmbedding", out var embObj))
            {
                var embedding = (float[])embObj!;
                
                // Read captured response
                responseBody.Position = 0;
                var responseText = await new StreamReader(responseBody).ReadToEndAsync();
                responseBody.Position = 0;

                // 4. Store in Redis Vector DB
                if (!string.IsNullOrEmpty(responseText))
                {
                    // Fire and forget (or async detach) to not block response
                    // Actually, we should allow response to flush.
                    // Async store
                    _ = StoreInCacheAsync(embedding, responseText);
                }
            }
            
            // 5. Copy buffer to original stream
            responseBody.Position = 0;
            await responseBody.CopyToAsync(originalBodyStream);
        }
        finally
        {
            context.Response.Body = originalBodyStream;
        }
    }

    private async Task StoreInCacheAsync(float[] embedding, string response)
    {
        try 
        {
            var db = redis.GetDatabase();
            var id = Guid.NewGuid().ToString();
            var key = $"vakt:{id}";
            
            // HSET key embedding <blob> response <text>
            await db.ExecuteAsync("HSET", key, 
                "embedding", GetBytes(embedding), 
                "response", response);
                
            logger.LogInformation("🧠 Semantic Cache Populated (Key: {Key})", key);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to populate semantic cache");
        }
    }

    private static byte[] GetBytes(float[] floatArray)
    {
        var byteArray = new byte[floatArray.Length * 4];
        Buffer.BlockCopy(floatArray, 0, byteArray, 0, byteArray.Length);
        return byteArray;
    }
}

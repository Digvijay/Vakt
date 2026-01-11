using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Net.Http;
using Vakt.Core.Interfaces;

namespace Vakt.Core.Services;

/// <summary>
/// Service that manages the downloading and verification of local AI models (Phi-3).
/// </summary>
public partial class ModelProvisioningService(
    IHttpClientFactory httpClientFactory, 
    Microsoft.Extensions.Options.IOptions<Vakt.Core.Options.ModelOptions> options,
    ILogger<ModelProvisioningService> logger) : IModelProvisioningService
{
    private readonly string _modelDir = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), options.Value.ModelPath));
    private readonly string _baseUrl = options.Value.ModelDownloadUrl;
    
    /// <inheritdoc />
    public async Task EnsureModelExistsAsync(CancellationToken cancellationToken = default)
    {
        // Map Local Name -> Remote Name
        // We MUST use the remote filename locally because genai_config.json references it.
        var variantName = _baseUrl.TrimEnd('/').Split('/').Last();
        var remoteModelName = $"phi3-mini-4k-instruct-{variantName}.onnx";
        var remoteDataName = $"{remoteModelName}.data";

        var fileMap = new Dictionary<string, string>
        {
            { remoteModelName, remoteModelName },
            { remoteDataName, remoteDataName },
            { "genai_config.json", "genai_config.json" },
            { "tokenizer.json", "tokenizer.json" },
            { "tokenizer_config.json", "tokenizer_config.json" },
            { "tokenizer.model", "tokenizer.model" },
            { "added_tokens.json", "added_tokens.json" }
        };

        // Check if all LOCAL files exist
        if (Directory.Exists(_modelDir) && fileMap.Keys.All(f => File.Exists(Path.Combine(_modelDir, f))))
        {
            LogModelFilesFound(logger, _modelDir);
            return;
        }

        LogModelMissing(logger, _modelDir);
        
        Directory.CreateDirectory(_modelDir);

        using var client = httpClientFactory.CreateClient();

        foreach (var kvp in fileMap)
        {
            var localName = kvp.Key;
            var remoteName = kvp.Value;
            var destination = Path.Combine(_modelDir, localName);
            
            if (File.Exists(destination)) continue;

            // ...

            var url = $"{_baseUrl}/{remoteName}?download=true";
            LogDownloadingFile(logger, remoteName);

            try 
            {
                using var stream = await client.GetStreamAsync(url, cancellationToken);
                using var fs = new FileStream(destination, FileMode.CreateNew);
                await stream.CopyToAsync(fs, cancellationToken);
            }
            catch (Exception ex)
            {
                LogDownloadFailed(logger, ex, remoteName);
                if (File.Exists(destination)) File.Delete(destination);
                throw;
            }
        }
        
        LogDownloadComplete(logger);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "✅ Model files found in {Path}.")]
    static partial void LogModelFilesFound(ILogger logger, string path);

    [LoggerMessage(Level = LogLevel.Information, Message = "🧠 Model missing or incomplete at {Path}. Starting Download...")]
    static partial void LogModelMissing(ILogger logger, string path);

    [LoggerMessage(Level = LogLevel.Information, Message = "⬇️ Downloading {File}...")]
    static partial void LogDownloadingFile(ILogger logger, string file);

    [LoggerMessage(Level = LogLevel.Error, Message = "❌ Failed to download {File}")]
    static partial void LogDownloadFailed(ILogger logger, Exception ex, string file);

    [LoggerMessage(Level = LogLevel.Information, Message = "✅ Model download complete.")]
    static partial void LogDownloadComplete(ILogger logger);
}

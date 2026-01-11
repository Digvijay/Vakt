using System.Text;
using Microsoft.ML.OnnxRuntimeGenAI;
using Vakt.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace Vakt.Core.Services;

/// <summary>
/// Implementation of intelligence service using local ONNX Runtime GenAI models.
/// </summary>
public partial class LocalIntelligenceService : IIntelligenceService, IDisposable
{
    private readonly Model? _model;
    private readonly Tokenizer? _tokenizer;
    private readonly ILogger<LocalIntelligenceService> _logger;
    private readonly string _modelPath;
    private bool _initialized;

    /// <summary>
    /// Initializes the local intelligence service and attempts to load the model.
    /// </summary>
    /// <param name="options">Configuration options containing the model path.</param>
    /// <param name="logger">Logger for diagnostic information.</param>
    public LocalIntelligenceService(Microsoft.Extensions.Options.IOptions<Vakt.Core.Options.ModelOptions> options, ILogger<LocalIntelligenceService> logger)
    {
        _logger = logger;
        _modelPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), options.Value.ModelPath));

        if (!Directory.Exists(_modelPath))
        {
             LogModelNotFound(_logger, _modelPath);
        }
        else 
        {
            LogLoadingModel(_logger);
            try
            {
                _model = new Model(_modelPath);
                _tokenizer = new Tokenizer(_model);
                LogModelReady(_logger);
                _initialized = true;
            }
            catch(Exception ex)
            {
                LogModelLoadFailed(_logger, ex);
            }
        }
    }

    /// <inheritdoc />
    public string RedactPii(string input)
    {
        if (!_initialized || _model == null || _tokenizer == null) return input;

        // 1. Construct the Prompt (Phi-3 Chat Format)
        // Use ReadOnlySpan or aggressive caching if prompt is static, but string const is fine.
        const string systemPrompt = "You are a strict PII redaction engine. Your ONLY job is to replace names, Swedish Personnummer (SSN), and phone numbers with [REDACTED]. Return ONLY the redacted text. Do not add explanations.";
        var fullPrompt = $"<|user|>\n{systemPrompt}\n\nINPUT: {input}<|end|>\n<|assistant|>";

        try 
        {
            // 2. Tokenize
            using var tokens = _tokenizer.Encode(fullPrompt);

            // 3. Configure Generation
            using var generatorParams = new GeneratorParams(_model);
            generatorParams.SetSearchOption("max_length", 2048);
            generatorParams.SetSearchOption("past_present_share_buffer", false);

            // 4. Generate
            using var generator = new Generator(_model, generatorParams);
            generator.AppendTokenSequences(tokens);

            // OPTIMIZATION: Use StringBuilder
            var output = new StringBuilder();

            while (!generator.IsDone())
            {
                generator.GenerateNextToken();
                
                var outputTokens = generator.GetSequence(0);
                var newToken = outputTokens.Slice(outputTokens.Length - 1, 1);
                output.Append(_tokenizer.Decode(newToken));
            }

            return output.ToString().Trim();
        }
        catch (Exception ex)
        {
            LogRedactionError(_logger, ex);
            return input; // Fallback to original
        }
    }

    public void Dispose()
    {
        _model?.Dispose();
        _tokenizer?.Dispose();
        GC.SuppressFinalize(this);
    }

    // Source Generated Logging
    [LoggerMessage(Level = LogLevel.Warning, Message = "Phi-3 Model not found at: {Path}. Redaction will be disabled.")]
    static partial void LogModelNotFound(ILogger logger, string path);

    [LoggerMessage(Level = LogLevel.Information, Message = "🧠 Loading Phi-3 Model... (This takes a few seconds)")]
    static partial void LogLoadingModel(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "✅ Phi-3 Loaded and Ready.")]
    static partial void LogModelReady(ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to load model.")]
    static partial void LogModelLoadFailed(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error during redaction generation.")]
    static partial void LogRedactionError(ILogger logger, Exception ex);
}

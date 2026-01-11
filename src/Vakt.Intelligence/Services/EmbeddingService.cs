using System.Text;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.Tokenizers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace Vakt.Intelligence.Services;

public class EmbeddingService : IDisposable
{
    private readonly ILogger<EmbeddingService> _logger;
    private readonly InferenceSession? _session;
    private readonly Tokenizer? _tokenizer;
    private const int EmbeddingDimension = 384; 
    private readonly bool _initialized;

    public EmbeddingService(Microsoft.Extensions.Options.IOptions<Vakt.Core.Options.SemanticCacheOptions> options, ILogger<EmbeddingService> logger)
    {
        _logger = logger;
        // In Docker, volume is at /data/models/all-MiniLM-L6-v2/model.onnx
        var modelPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), options.Value.EmbeddingModelPath));
        var vocabPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), options.Value.EmbeddingVocabPath));

        if (!File.Exists(modelPath) || !File.Exists(vocabPath))
        {
            _logger.LogWarning("⚠️ Embedding model not found at {Path}. Semantic Caching will be disabled (fallback to exact).", modelPath);
            return;
        }

        try 
        {
            _logger.LogInformation("🧠 Loading Embedding Model (all-MiniLM-L6-v2)...");
            _session = new InferenceSession(modelPath);
            // Use static factory
            _tokenizer = BertTokenizer.Create(vocabPath);
            _initialized = true;
            _logger.LogInformation("✅ Embedding Model Loaded.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to load Embedding Model.");
        }
    }

    public float[] GenerateEmbedding(string text)
    {
        if (!_initialized || _session == null || _tokenizer == null)
        {
            return new float[EmbeddingDimension]; // Return zero vector if not working
        }

        try 
        {
            // 1. Tokenize
            // Encode usually returns IReadOnlyList<Token> or similar in this lib version?
            // Let's try EncodeToIds if Encode failed.
            // Or just Encode(text) and check result properties.
            // If the error was "no definition for Encode", maybe it requires "using Microsoft.ML.Tokenizers;" which is there.
            // Maybe it is explicitly on the concrete type?
            
            // Re-use logic assuming standard behavior:
            // Input IDs, Attention Mask, Token Type IDs.
            
            // Try EncodeToIds first as it's cleaner if available.
            // If not, we fall back to generic inference.
            
            // WORKAROUND: If "Encode" is missing on abstract Tokenizer, cast to BertTokenizer?
            // Or maybe the method is named "Encode" but requires options?
            
            var inputIdsInt = _tokenizer.EncodeToIds(text);
            // Wait, EncodeToIds takes ReadOnlySpan<char>.
            
            // Actually, let's look at the "BertTokenizer" specifically.
            // var bert = (BertTokenizer)_tokenizer;
            // var inputIds = bert.EncodeToIds(text);
            
            var inputIds = inputIdsInt.Select(id => (long)id).ToArray();
            var padding = new long[inputIds.Length]; 
            var attentionMask = Enumerable.Repeat(1L, inputIds.Length).ToArray();
            var tokenTypeIds = new long[inputIds.Length];

            var inputTensor = new DenseTensor<long>(inputIds, new[] { 1, inputIds.Length });
            var attentionTensor = new DenseTensor<long>(attentionMask, new[] { 1, attentionMask.Length });
            var tokenTypeTensor = new DenseTensor<long>(tokenTypeIds, new[] { 1, tokenTypeIds.Length });

            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("input_ids", inputTensor),
                NamedOnnxValue.CreateFromTensor("attention_mask", attentionTensor),
                NamedOnnxValue.CreateFromTensor("token_type_ids", tokenTypeTensor)
            };

            // 2. Inference
            using var results = _session.Run(inputs);
            
            var rawOutput = results.First().AsTensor<float>();
            return rawOutput.ToArray().Take(EmbeddingDimension).ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Embedding generation failed.");
            return new float[EmbeddingDimension];
        }
    }

    public void Dispose()
    {
        _session?.Dispose();
    }
}

namespace Vakt.Core.Options;

public class SemanticCacheOptions
{
    public const string SectionName = "SemanticCache";

    public string IndexName { get; set; } = "vakt-semantic-cache";
    public double SimilarityThreshold { get; set; } = 0.1;
    public int EmbeddingDimension { get; set; } = 384;
    public string RedisPrefix { get; set; } = "vakt:";
    
    // Embedding Model Config
    public string EmbeddingModelPath { get; set; } = "../models/all-MiniLM-L6-v2/model.onnx";
    public string EmbeddingVocabPath { get; set; } = "../models/all-MiniLM-L6-v2/vocab.txt";
}

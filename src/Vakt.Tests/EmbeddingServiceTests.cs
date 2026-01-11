using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Vakt.Core.Options;
using Vakt.Intelligence.Services;
using Xunit;

namespace Vakt.Tests;

public class EmbeddingServiceTests
{
    private readonly Mock<IOptions<SemanticCacheOptions>> _mockOptions = new();
    private readonly Mock<ILogger<EmbeddingService>> _mockLogger = new();

    public EmbeddingServiceTests()
    {
        _mockOptions.Setup(p => p.Value).Returns(new SemanticCacheOptions 
        { 
            EmbeddingModelPath = "dummy-model-path",
            EmbeddingVocabPath = "dummy-vocab-path",
            EmbeddingDimension = 384
        });
    }

    [Fact]
    public void GenerateEmbedding_ShouldReturnZeroVector_WhenNotInitialized()
    {
        // Arrange
        var service = new EmbeddingService(_mockOptions.Object, _mockLogger.Object);
        var input = "Test input";

        // Act
        var result = service.GenerateEmbedding(input);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(384, result.Length);
        Assert.All(result, f => Assert.Equal(0f, f));
    }
}

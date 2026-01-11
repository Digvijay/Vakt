using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Vakt.Core.Options;
using Vakt.Core.Services;
using Xunit;

namespace Vakt.Tests;

public class LocalIntelligenceServiceTests
{
    private readonly Mock<IOptions<ModelOptions>> _mockOptions = new();
    private readonly Mock<ILogger<LocalIntelligenceService>> _mockLogger = new();

    public LocalIntelligenceServiceTests()
    {
        _mockOptions.Setup(p => p.Value).Returns(new ModelOptions 
        { 
            ModelPath = "dummy-model-path" 
        });
        _mockLogger.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
    }

    [Fact]
    public void Constructor_ShouldLogWarning_WhenModelMissing()
    {
        // Arrange
        // (Default mocks represent missing directory)

        // Act
        var service = new LocalIntelligenceService(_mockOptions.Object, _mockLogger.Object);

        // Assert
        // Verify LogModelNotFound
        // Since it is a source generated log method, we verify the underlying ILogger.Log
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void RedactPii_ShouldReturnInput_WhenNotInitialized()
    {
        // Arrange
        var service = new LocalIntelligenceService(_mockOptions.Object, _mockLogger.Object);
        var input = "My name is Alice";

        // Act
        var result = service.RedactPii(input);

        // Assert
        Assert.Equal(input, result);
    }
}

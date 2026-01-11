using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using RichardSzalay.MockHttp;
using Vakt.Core.Options;
using Vakt.Core.Services;
using Xunit;

namespace Vakt.Tests;

public class ModelProvisioningServiceTests
{
    private readonly Mock<IOptions<ModelOptions>> _mockOptions = new();
    private readonly Mock<ILogger<ModelProvisioningService>> _mockLogger = new();
    private readonly MockHttpMessageHandler _mockHttp = new();

    public ModelProvisioningServiceTests()
    {
        // Mock default config path
        _mockOptions.Setup(p => p.Value).Returns(new ModelOptions 
        { 
            ModelPath = "test-models",
            ModelDownloadUrl = "https://huggingface.co/mock"
        });
    }

    [Fact]
    public async Task EnsureModelExistsAsync_ShouldDownload_WhenModelMissing()
    {
        // Arrange
        // Ensure directory is clean
        var testDir = Path.GetFullPath("test-models");
        if (Directory.Exists(testDir)) Directory.Delete(testDir, true);

        // Mock HTTP responses for 5 files
        _mockHttp.When("https://huggingface.co/*").Respond("application/octet-stream", "DUMMY CONTENT");

        var httpClient = _mockHttp.ToHttpClient();
        var mockFactory = new Mock<IHttpClientFactory>();
        mockFactory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var service = new ModelProvisioningService(mockFactory.Object, _mockOptions.Object, _mockLogger.Object);

        // Act
        await service.EnsureModelExistsAsync();

        // Assert
        Assert.True(Directory.Exists(testDir));
        Assert.True(File.Exists(Path.Combine(testDir, "genai_config.json")));
        
        // Clean up
        if (Directory.Exists(testDir)) Directory.Delete(testDir, true);
    }

    [Fact]
    public async Task EnsureModelExistsAsync_ShouldSkip_WhenModelExists()
    {
        // Arrange
        var testDir = Path.GetFullPath("test-models-existing");
        if (Directory.Exists(testDir)) Directory.Delete(testDir, true);
        Directory.CreateDirectory(testDir);
        
        // Create dummy files (matching the "mock" variant from the URL)
        File.WriteAllText(Path.Combine(testDir, "genai_config.json"), "{}");
        File.WriteAllText(Path.Combine(testDir, "phi3-mini-4k-instruct-mock.onnx"), "blob");
        File.WriteAllText(Path.Combine(testDir, "phi3-mini-4k-instruct-mock.onnx.data"), "blob");
        File.WriteAllText(Path.Combine(testDir, "tokenizer.json"), "{}");
        File.WriteAllText(Path.Combine(testDir, "tokenizer_config.json"), "{}");
        File.WriteAllText(Path.Combine(testDir, "tokenizer.model"), "blob");
        File.WriteAllText(Path.Combine(testDir, "added_tokens.json"), "{}");

        // Update Mock for this test scenario
        _mockOptions.Setup(p => p.Value).Returns(new ModelOptions 
        { 
            ModelPath = "test-models-existing",
            ModelDownloadUrl = "https://huggingface.co/mock"
        });

        var mockFactory = new Mock<IHttpClientFactory>();
        // Should NOT create client if logic is correct
        
        var service = new ModelProvisioningService(mockFactory.Object, _mockOptions.Object, _mockLogger.Object);

        // Act
        await service.EnsureModelExistsAsync();

        // Assert
        mockFactory.Verify(x => x.CreateClient(It.IsAny<string>()), Times.Never);
        
        // Clean up
        if (Directory.Exists(testDir)) Directory.Delete(testDir, true);
    }
}

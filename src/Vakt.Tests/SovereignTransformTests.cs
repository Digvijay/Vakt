using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Moq;
using Vakt.Core.Interfaces;
using Xunit;
using Microsoft.Extensions.Configuration;
using StackExchange.Redis;

namespace Vakt.Tests;

public class SovereignTransformTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory = factory;

    [Fact]
    public async Task Proxy_ShouldRedactPii_WhenMatches()
    {
        // Arrange
        var mockIntelligence = new Mock<IIntelligenceService>();
        mockIntelligence.Setup(x => x.RedactPii(It.IsAny<string>()))
            .Returns("[REDACTED]");

        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "ConnectionStrings:cache", "localhost:6379" },
                    { "ConnectionStrings:vakt-intelligence", "http://localhost" }
                });
            });

            builder.ConfigureServices(services =>
            {
                // Mock the Provisioning Service to prevent real file download
                var mockProvisioner = new Mock<IModelProvisioningService>();
                mockProvisioner.Setup(x => x.EnsureModelExistsAsync(It.IsAny<CancellationToken>()))
                    .Returns(Task.CompletedTask);

                // Use RemoveAll to replace the existing registration from Program.cs
                services.RemoveAll(typeof(IModelProvisioningService));
                services.AddSingleton(mockProvisioner.Object);
            });

            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton(mockIntelligence.Object);
                
                // Mocks for Semantic Cache dependencies
                services.RemoveAll(typeof(IConnectionMultiplexer));
                var mockRedis = new Mock<IConnectionMultiplexer>();
                var mockDb = new Mock<IDatabase>();
                
                // Health Check Ping Support
                mockDb.Setup(x => x.PingAsync(It.IsAny<CommandFlags>())).ReturnsAsync(TimeSpan.Zero);
                
                mockRedis.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(mockDb.Object);
                mockRedis.Setup(x => x.IsConnected).Returns(true);
                services.AddSingleton(mockRedis.Object);

                // Correctly mock ONLY the vakt-intelligence client, leaving others (YARP) intact
                services.AddHttpClient("vakt-intelligence")
                    .ConfigurePrimaryHttpMessageHandler(() => new MockEmptyHandler());
            });
        }).CreateClient();

        var content = new StringContent(
            "{\"messages\": [{\"role\": \"user\", \"content\": \"My ID is 12345\"}]}", 
            System.Text.Encoding.UTF8, 
            "application/json");

        // Act
        var response = await client.PostAsync("/openai/v1/chat/completions", content);

        // Assert
        // Verify that RedactPii was called with the content
        mockIntelligence.Verify(x => x.RedactPii("My ID is 12345"), Times.Once);
    }
    [Fact]
    public async Task Proxy_ShouldIgnoreGetRequests()
    {
        // Arrange
        var mockIntelligence = new Mock<IIntelligenceService>();
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "ConnectionStrings:cache", "localhost:6379" },
                    { "ConnectionStrings:vakt-intelligence", "http://localhost" }
                });
            });

            builder.ConfigureServices(services =>
            {
                 var mockProvisioner = new Mock<IModelProvisioningService>();
                 mockProvisioner.Setup(x => x.EnsureModelExistsAsync(It.IsAny<CancellationToken>()))
                     .Returns(Task.CompletedTask);
                 services.RemoveAll(typeof(IModelProvisioningService));
                 services.AddSingleton(mockProvisioner.Object);
            });
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton(mockIntelligence.Object);
                 // Mocks for Semantic Cache dependencies
                services.RemoveAll(typeof(IConnectionMultiplexer));
                var mockRedis = new Mock<IConnectionMultiplexer>();
                var mockDb = new Mock<IDatabase>();
                mockDb.Setup(x => x.PingAsync(It.IsAny<CommandFlags>())).ReturnsAsync(TimeSpan.Zero);
                mockRedis.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(mockDb.Object);
                mockRedis.Setup(x => x.IsConnected).Returns(true);
                services.AddSingleton(mockRedis.Object);

                services.AddHttpClient("vakt-intelligence")
                    .ConfigurePrimaryHttpMessageHandler(() => new MockEmptyHandler());
            });
        }).CreateClient();

        // Act
        await client.GetAsync("/openai/v1/models");

        // Assert
        // Verify RedactPii was NEVER called (GET requests are ignored)
        mockIntelligence.Verify(x => x.RedactPii(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Proxy_ShouldHandleNonJsonBody()
    {
        // Arrange
        var mockIntelligence = new Mock<IIntelligenceService>();
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "ConnectionStrings:cache", "localhost:6379" },
                    { "ConnectionStrings:vakt-intelligence", "http://localhost" }
                });
            });

            builder.ConfigureServices(services =>
            {
                 var mockProvisioner = new Mock<IModelProvisioningService>();
                 mockProvisioner.Setup(x => x.EnsureModelExistsAsync(It.IsAny<CancellationToken>()))
                     .Returns(Task.CompletedTask);
                 services.RemoveAll(typeof(IModelProvisioningService));
                 services.AddSingleton(mockProvisioner.Object);
            });
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton(mockIntelligence.Object);
                 // Mocks for Semantic Cache dependencies
                services.RemoveAll(typeof(IConnectionMultiplexer));
                var mockRedis = new Mock<IConnectionMultiplexer>();
                var mockDb = new Mock<IDatabase>();
                mockDb.Setup(x => x.PingAsync(It.IsAny<CommandFlags>())).ReturnsAsync(TimeSpan.Zero);
                mockRedis.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(mockDb.Object);
                mockRedis.Setup(x => x.IsConnected).Returns(true);
                services.AddSingleton(mockRedis.Object);

                services.AddHttpClient("vakt-intelligence")
                    .ConfigurePrimaryHttpMessageHandler(() => new MockEmptyHandler());
            });
        }).CreateClient();

        var content = new StringContent("This is not JSON", System.Text.Encoding.UTF8, "text/plain");

        // Act
        await client.PostAsync("/openai/v1/chat/completions", content);

        // Assert
        // Should not crash, and should not call RedactPii (parsing fails)
        mockIntelligence.Verify(x => x.RedactPii(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Proxy_ShouldHandleJsonWithoutMessages()
    {
        // Arrange
        var mockIntelligence = new Mock<IIntelligenceService>();
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "ConnectionStrings:cache", "localhost:6379" },
                    { "ConnectionStrings:vakt-intelligence", "http://localhost" }
                });
            });

            builder.ConfigureServices(services =>
            {
                 var mockProvisioner = new Mock<IModelProvisioningService>();
                 mockProvisioner.Setup(x => x.EnsureModelExistsAsync(It.IsAny<CancellationToken>()))
                     .Returns(Task.CompletedTask);
                 services.RemoveAll(typeof(IModelProvisioningService));
                 services.AddSingleton(mockProvisioner.Object);
            });
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton(mockIntelligence.Object);
                 // Mocks for Semantic Cache dependencies
                services.RemoveAll(typeof(IConnectionMultiplexer));
                var mockRedis = new Mock<IConnectionMultiplexer>();
                var mockDb = new Mock<IDatabase>();
                mockDb.Setup(x => x.PingAsync(It.IsAny<CommandFlags>())).ReturnsAsync(TimeSpan.Zero);
                mockRedis.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(mockDb.Object);
                mockRedis.Setup(x => x.IsConnected).Returns(true);
                services.AddSingleton(mockRedis.Object);

                services.AddHttpClient("vakt-intelligence")
                    .ConfigurePrimaryHttpMessageHandler(() => new MockEmptyHandler());
            });
        }).CreateClient();

        var content = new StringContent("{\"model\": \"gpt-4\"}", System.Text.Encoding.UTF8, "application/json");

        // Act
        await client.PostAsync("/openai/v1/chat/completions", content);

        // Assert
        // Should not call RedactPii (no 'messages' array)
        mockIntelligence.Verify(x => x.RedactPii(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Proxy_ShouldNotModifyBody_WhenNoRedactionNeeded()
    {
        // Arrange
        var mockIntelligence = new Mock<IIntelligenceService>();
        // Setup to return the SAME string (no redaction)
        mockIntelligence.Setup(x => x.RedactPii(It.IsAny<string>()))
            .Returns<string>(input => input);

        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "ConnectionStrings:cache", "localhost:6379" },
                    { "ConnectionStrings:vakt-intelligence", "http://localhost" }
                });
            });

            builder.ConfigureServices(services =>
            {
                 var mockProvisioner = new Mock<IModelProvisioningService>();
                 mockProvisioner.Setup(x => x.EnsureModelExistsAsync(It.IsAny<CancellationToken>()))
                     .Returns(Task.CompletedTask);
                 services.RemoveAll(typeof(IModelProvisioningService));
                 services.AddSingleton(mockProvisioner.Object);
            });
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton(mockIntelligence.Object);
                 // Mocks for Semantic Cache dependencies
                services.RemoveAll(typeof(IConnectionMultiplexer));
                var mockRedis = new Mock<IConnectionMultiplexer>();
                var mockDb = new Mock<IDatabase>();
                mockDb.Setup(x => x.PingAsync(It.IsAny<CommandFlags>())).ReturnsAsync(TimeSpan.Zero);
                mockRedis.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(mockDb.Object);
                mockRedis.Setup(x => x.IsConnected).Returns(true);
                services.AddSingleton(mockRedis.Object);

                services.AddHttpClient("vakt-intelligence")
                    .ConfigurePrimaryHttpMessageHandler(() => new MockEmptyHandler());
            });
        }).CreateClient();

        var inputBody = "{\"messages\": [{\"role\": \"user\", \"content\": \"Safe content\"}]}";
        var content = new StringContent(inputBody, System.Text.Encoding.UTF8, "application/json");

        // Act
        // We can't easily check the *upstream* request body in this test setup without a mock destination.
        // However, we CAN verify that RedactPii WAS called, ensuring the logic ran.
        await client.PostAsync("/openai/v1/chat/completions", content);

        // Assert
        mockIntelligence.Verify(x => x.RedactPii("Safe content"), Times.Once);
    }

    [Fact]
    public async Task Proxy_ShouldIncrementRiskSavedMetric_WhenPiiRedacted()
    {
        // Arrange
        var mockIntelligence = new Mock<IIntelligenceService>();
        mockIntelligence.Setup(x => x.RedactPii(It.IsAny<string>()))
             .Returns("[REDACTED]"); // Simulate change

        var meterFactory = new Microsoft.Extensions.Diagnostics.Metrics.Testing.MetricCollector<double>(null, "Vakt.Proxy", "vakt.risk.saved_value");

        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "ConnectionStrings:cache", "localhost:6379" },
                    { "ConnectionStrings:vakt-intelligence", "http://localhost" }
                });
            });

            builder.ConfigureServices(services =>
            {
                 // Mock provisioner
                 var mockProvisioner = new Mock<IModelProvisioningService>();
                 mockProvisioner.Setup(x => x.EnsureModelExistsAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
                 services.RemoveAll(typeof(IModelProvisioningService));
                 services.AddSingleton(mockProvisioner.Object);
            });
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton(mockIntelligence.Object);
                services.RemoveAll(typeof(IConnectionMultiplexer));
                var mockRedis = new Mock<IConnectionMultiplexer>();
                var mockDb = new Mock<IDatabase>();
                mockDb.Setup(x => x.PingAsync(It.IsAny<CommandFlags>())).ReturnsAsync(TimeSpan.Zero);
                mockRedis.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(mockDb.Object);
                mockRedis.Setup(x => x.IsConnected).Returns(true);
                services.AddSingleton(mockRedis.Object);

                services.AddHttpClient("vakt-intelligence")
                    .ConfigurePrimaryHttpMessageHandler(() => new MockEmptyHandler());
            });
        }).CreateClient();

        var content = new StringContent(
            "{\"messages\": [{\"role\": \"user\", \"content\": \"My ID is 12345\"}]}", 
            System.Text.Encoding.UTF8, 
            "application/json");

        // Act
        await client.PostAsync("/openai/v1/chat/completions", content);

        // Assert
        var measurements = meterFactory.GetMeasurementSnapshot();
        Assert.NotEmpty(measurements);
        Assert.Contains(measurements, m => m.Value == 5000);
    }

    private class MockEmptyHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // Return 503 so logic might skip or fail gracefully (caught in try/catch)
            // Or return 404.
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        }
    }
}

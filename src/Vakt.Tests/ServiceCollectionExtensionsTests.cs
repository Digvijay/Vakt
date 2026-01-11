using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Vakt.Core.Extensions;
using Vakt.Core.Interfaces;
using Vakt.Core.Transforms;
using Xunit;
using Moq;
using Yarp.ReverseProxy.Transforms;

namespace Vakt.Tests;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddVaktProxy_ShouldRegisterCoreServices()
    {
        // Arrange
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder().Build();
        
        services.AddLogging();
        services.AddSingleton<IConfiguration>(config);
        
        // Mock dependencies for SovereignTransform
        var mockRedis = new Mock<StackExchange.Redis.IConnectionMultiplexer>();
        services.AddSingleton(mockRedis.Object);
        var mockHttpFactory = new Mock<System.Net.Http.IHttpClientFactory>();
        services.AddSingleton(mockHttpFactory.Object);

        // Act
        services.AddVaktProxy(config);
        var provider = services.BuildServiceProvider();

        // Assert
        // 1. Verify Intelligence Service
        var brain = provider.GetService<IIntelligenceService>();
        Assert.NotNull(brain);
        
        // 2. Verify Transform
        var transform = provider.GetService<SovereignTransform>();
        Assert.NotNull(transform);
        
        // 3. Verify YARP
        // YARP registers IProxyConfigProvider, IReverseProxyFeature, etc.
        // We can check for ITransformProvider which SovereignTransform implements, 
        // OR check if YARP services are present.
 
        // YARP's .AddTransforms<T>() usually registers T itself.
    }
}

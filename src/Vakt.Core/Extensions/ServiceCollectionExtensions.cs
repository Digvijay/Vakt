using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Vakt.Core.Interfaces;
using Vakt.Core.Services;
using Vakt.Core.Transforms;

namespace Vakt.Core.Extensions;

/// <summary>
/// Extension methods for registering Vakt services with the dependency injection container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Vakt proxy services, including intelligence, transforms, and YARP defaults.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Configuration for reading model paths and proxy settings.</param>
    /// <returns>A reverse proxy builder for further configuration.</returns>
    /// <summary>
    /// Registers the core Vakt services (Intelligence, Provisioning) without the Proxy layer.
    /// Useful for CLI tools or worker processes.
    /// </summary>
    public static IServiceCollection AddVaktCoreServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Bind Options
        services.Configure<Vakt.Core.Options.SemanticCacheOptions>(configuration.GetSection(Vakt.Core.Options.SemanticCacheOptions.SectionName));
        services.Configure<Vakt.Core.Options.ModelOptions>(configuration.GetSection(Vakt.Core.Options.ModelOptions.SectionName));
        services.Configure<Vakt.Core.Options.AuditOptions>(configuration.GetSection(Vakt.Core.Options.AuditOptions.SectionName));

        // Register Core Services
        services.AddHttpClient(); // Required for ModelProvisioningService
        services.AddSingleton<IModelProvisioningService, ModelProvisioningService>();
        services.AddSingleton<IIntelligenceService, LocalIntelligenceService>();
        services.AddSingleton<IAuditLogger, LocalFileAuditLogger>();

        return services;
    }

    /// <summary>
    /// Registers the Vakt proxy services, including intelligence, transforms, and YARP defaults.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Configuration for reading model paths and proxy settings.</param>
    /// <returns>A reverse proxy builder for further configuration.</returns>
    public static IReverseProxyBuilder AddVaktProxy(this IServiceCollection services, IConfiguration configuration)
    {
        // Add Core
        services.AddVaktCoreServices(configuration);
        
        // Register Transform
        services.AddSingleton<SovereignTransform>();
        
        // Return Yarp Builder so user can chain .LoadFromMemory() etc.
        return services.AddReverseProxy()
                       .AddTransforms<SovereignTransform>();
    }
}

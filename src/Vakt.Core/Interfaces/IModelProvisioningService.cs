namespace Vakt.Core.Interfaces;

/// <summary>
/// Responsible for provisioning and managing lifecycle of local AI models.
/// </summary>
public interface IModelProvisioningService
{
    /// <summary>
    /// Ensures that the required AI model files are present in the local cache.
    /// Downloads them from the configured source if missing.
    /// </summary>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    Task EnsureModelExistsAsync(CancellationToken cancellationToken = default);
}

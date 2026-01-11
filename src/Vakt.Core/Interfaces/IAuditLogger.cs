using System.Threading.Tasks;

namespace Vakt.Core.Interfaces;

/// <summary>
/// Interface for compliance audit logging.
/// </summary>
public interface IAuditLogger
{
    /// <summary>
    /// Logs an audit event for a prompt processing action.
    /// </summary>
    /// <param name="correlationId">The unique request ID.</param>
    /// <param name="originalPrompt">The original prompt before redaction.</param>
    /// <param name="redactedPrompt">The prompt after redaction logic applied.</param>
    /// <param name="wasModified">True if PII was redacted, false otherwise.</param>
    Task LogEventAsync(string correlationId, string originalPrompt, string redactedPrompt, bool wasModified);
}

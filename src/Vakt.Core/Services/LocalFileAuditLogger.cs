using System.Text.Json;
using Microsoft.Extensions.Logging;
using Vakt.Core.Interfaces;

namespace Vakt.Core.Services;

/// <summary>
/// A simple file-based audit logger that writes newline-delimited JSON.
/// </summary>
public class LocalFileAuditLogger(ILogger<LocalFileAuditLogger> logger, Microsoft.Extensions.Options.IOptions<Vakt.Core.Options.AuditOptions> options) : IAuditLogger
{
    private readonly Vakt.Core.Options.AuditOptions _options = options.Value;
    private readonly SemaphoreSlim _lock = new(1, 1);

    /// <inheritdoc />
    public async Task LogEventAsync(string correlationId, string originalPrompt, string redactedPrompt, bool wasModified)
    {
        if (!_options.Enabled) return;

        var logPath = Path.Combine(Directory.GetCurrentDirectory(), _options.LogPath);
        var logEntry = new
        {
            Timestamp = DateTime.UtcNow,
            CorrelationId = correlationId,
            WasModified = wasModified,
            OriginalSize = originalPrompt.Length,
            RedactedSize = redactedPrompt?.Length ?? 0,
            Original = originalPrompt,
            Redacted = redactedPrompt
        };

        var json = JsonSerializer.Serialize(logEntry);

        try
        {
            await _lock.WaitAsync();
            try
            {
                await File.AppendAllTextAsync(logPath, json + Environment.NewLine);
            }
            finally
            {
                _lock.Release();
            }
        }
        catch (Exception ex)
        {
            // Fallback to system logger if file write fails (Critical)
            logger.LogError(ex, "Failed to write to Audit Log! Compliance Risk!");
        }
    }
}

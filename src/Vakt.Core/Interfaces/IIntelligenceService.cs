namespace Vakt.Core.Interfaces;

/// <summary>
/// Provides intelligence capabilities for text processing, such as PII redaction.
/// </summary>
public interface IIntelligenceService
{
    /// <summary>
    /// Analyzes the input text and redacts personally identifiable information (PII) 
    /// such as names, SSNs, and phone numbers.
    /// </summary>
    /// <param name="input">The raw text input to sanitize.</param>
    /// <returns>The sanitized text with PII replaced by [REDACTED].</returns>
    string RedactPii(string input);
}

namespace Vakt.Core.Options;

public class AuditOptions
{
    public const string SectionName = "Audit";

    public bool Enabled { get; set; } = true;
    public string LogPath { get; set; } = "audit.log";
}

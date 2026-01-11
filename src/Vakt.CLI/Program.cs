using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Vakt.Core.Extensions;
using Vakt.Core.Interfaces;

// Build Host
var builder = Host.CreateApplicationBuilder(args);

// Add Configuration
builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
builder.Configuration.AddEnvironmentVariables();

// Add Vakt Core (No Proxy/YARP)
builder.Services.AddVaktCoreServices(builder.Configuration);

var host = builder.Build();

// Parse Args
if (args.Length == 0)
{
    Console.WriteLine("Usage: vakt [download|redact <text>]");
    return;
}

var command = args[0].ToLowerInvariant();

switch (command)
{
    case "download":
        Console.WriteLine("⬇️  Downloading Model...");
        var provisioner = host.Services.GetRequiredService<IModelProvisioningService>();
        await provisioner.EnsureModelExistsAsync();
        Console.WriteLine("✅ Done.");
        break;

    case "redact":
        if (args.Length < 2)
        {
            Console.WriteLine("Error: Please provide text to redact.");
            return;
        }
        var text = args[1];
        Console.WriteLine($"🔍 Redacting: \"{text}\"");
        
        var intelligence = host.Services.GetRequiredService<IIntelligenceService>();
        // Measure time
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var redacted = intelligence.RedactPii(text);
        sw.Stop();
        
        Console.WriteLine($"📝 Result:    \"{redacted}\"");
        Console.WriteLine($"⏱️  Time:      {sw.ElapsedMilliseconds}ms");
        break;

    default:
        Console.WriteLine($"Unknown command: {command}");
        break;
}

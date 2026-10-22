namespace Flowza.Gateway;

/// <summary>Warnings at start-up about settings a real installation must change (setup guide, T030).</summary>
internal static partial class GatewayStartupLog
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "AllowedHosts is '*'. Set it to the installation's host name(s), e.g. flowza.example.org (see the setup guide)")]
    public static partial void AllowedHostsOpen(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The web app is missing: index.html is not in the Gateway's wwwroot. Build it (npm run build in src/Web) before dotnet publish")]
    public static partial void WebAppMissing(ILogger logger);
}

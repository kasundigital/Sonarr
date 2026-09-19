using System;
using NzbDrone.Core.ThingiProvider.Status;

namespace NzbDrone.Core.HealthCheck.Checks
{
    public static class ProviderStatusHealthCheckMessage
    {
        public static string Format(string providerName, ProviderStatusBase status, ProviderFailureReason reason)
        {
            return reason switch
            {
                ProviderFailureReason.Authentication => $"{providerName} [authentication error]",
                ProviderFailureReason.Connection => $"{providerName} [connection error]",
                ProviderFailureReason.RateLimit when status.DisabledTill.HasValue =>
                    $"{providerName} [rate limited until {status.DisabledTill.Value.ToLocalTime():g}]",
                ProviderFailureReason.RateLimit => $"{providerName} [rate limited]",
                _ when status.DisabledTill.HasValue =>
                    $"{providerName} [retry after {status.DisabledTill.Value.ToLocalTime():g}]",
                _ => $"{providerName} [recent failures]"
            };
        }
    }
}

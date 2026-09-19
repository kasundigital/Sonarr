namespace NzbDrone.Core.ThingiProvider.Status
{
    public enum ProviderFailureReason
    {
        Unknown = 0,
        Failure = 1,
        Connection = 2,
        Authentication = 3,
        RateLimit = 4
    }
}

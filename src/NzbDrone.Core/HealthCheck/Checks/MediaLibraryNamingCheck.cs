using System.Collections.Generic;
using System.Linq;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Localization;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.Events;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.HealthCheck.Checks
{
    [CheckOn(typeof(SeriesScannedEvent))]
    [CheckOn(typeof(RenameCompletedEvent))]
    public class MediaLibraryNamingCheck : HealthCheckBase
    {
        private readonly ISeriesService _seriesService;
        private readonly IRenameEpisodeFileService _renameEpisodeFileService;

        public MediaLibraryNamingCheck(ISeriesService seriesService,
                                       IRenameEpisodeFileService renameEpisodeFileService,
                                       ILocalizationService localizationService)
            : base(localizationService)
        {
            _seriesService = seriesService;
            _renameEpisodeFileService = renameEpisodeFileService;
        }

        public override HealthCheck Check()
        {
            var series = _seriesService.GetAllSeries();

            if (series.Empty())
            {
                return new HealthCheck(GetType());
            }

            var seriesIds = series.Select(s => s.Id).ToList();
            var previews = _renameEpisodeFileService.GetRenamePreviews(seriesIds);

            if (previews.Empty())
            {
                return new HealthCheck(GetType());
            }

            var seriesLookup = series.ToDictionary(s => s.Id);
            var affectedSeries = previews
                .Select(p => seriesLookup.TryGetValue(p.SeriesId, out var item) ? item.Title : null)
                .Where(title => title.IsNotNullOrWhiteSpace())
                .Distinct()
                .Take(5)
                .ToList();

            var examples = affectedSeries.Join(", ");

            return new HealthCheck(
                GetType(),
                HealthCheckResult.Warning,
                HealthCheckReason.MediaLibraryNaming,
                _localizationService.GetLocalizedString("MediaLibraryNamingHealthCheckMessage", new Dictionary<string, object>
                {
                    { "count", previews.Count },
                    { "series", examples }
                }),
                "#media-library-naming");
        }
    }
}

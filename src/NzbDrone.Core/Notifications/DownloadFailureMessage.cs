using System.Collections.Generic;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Notifications
{
    public class DownloadFailureMessage
    {
        public Series Series { get; set; }
        public List<int> EpisodeIds { get; set; }
        public QualityModel Quality { get; set; }
        public string SourceTitle { get; set; }
        public string DownloadClient { get; set; }
        public string DownloadId { get; set; }
        public string FailureMessage { get; set; }
        public string FailureSource { get; set; }
        public Dictionary<string, string> Data { get; set; }
        public TrackedDownload TrackedDownload { get; set; }
        public List<Language> Languages { get; set; }
    }
}

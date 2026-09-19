using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Organizer;
using NzbDrone.Core.RootFolders;
using NzbDrone.Core.Tags;
using NzbDrone.Core.Tv.Commands;
using NzbDrone.Core.Tv.Events;

namespace NzbDrone.Core.Tv
{
    public class AutoMoveSeriesByTagService :
        IHandle<SeriesAddedEvent>,
        IHandle<SeriesEditedEvent>,
        IHandle<SeriesUpdatedEvent>,
        IHandle<SeriesBulkEditedEvent>
    {
        private const string TagPrefix = "rootfolder:";

        private readonly ISeriesService _seriesService;
        private readonly ITagService _tagService;
        private readonly IRootFolderService _rootFolderService;
        private readonly IBuildFileNames _fileNameBuilder;
        private readonly IManageCommandQueue _commandQueue;
        private readonly Logger _logger;

        public AutoMoveSeriesByTagService(ISeriesService seriesService,
                                          ITagService tagService,
                                          IRootFolderService rootFolderService,
                                          IBuildFileNames fileNameBuilder,
                                          IManageCommandQueue commandQueue,
                                          Logger logger)
        {
            _seriesService = seriesService;
            _tagService = tagService;
            _rootFolderService = rootFolderService;
            _fileNameBuilder = fileNameBuilder;
            _commandQueue = commandQueue;
            _logger = logger;
        }

        public void Handle(SeriesAddedEvent message)
        {
            ApplyRule(message.Series);
        }

        public void Handle(SeriesEditedEvent message)
        {
            ApplyRule(message.Series);
        }

        public void Handle(SeriesUpdatedEvent message)
        {
            ApplyRule(message.Series);
        }

        public void Handle(SeriesBulkEditedEvent message)
        {
            foreach (var series in message.Series)
            {
                ApplyRule(series);
            }
        }

        private void ApplyRule(Series series)
        {
            if (series.Tags.Empty())
            {
                return;
            }

            var rootTags = _tagService.GetTags(series.Tags)
                                      .Where(t => t.Label.StartsWith(TagPrefix, StringComparison.OrdinalIgnoreCase))
                                      .ToList();

            if (rootTags.Empty())
            {
                return;
            }

            if (rootTags.Count > 1)
            {
                _logger.Warn("Series '{0}' has multiple rootfolder tags. Auto move skipped.", series.Title);
                return;
            }

            var selector = rootTags[0].Label.Substring(TagPrefix.Length).Trim();
            var rootFolders = _rootFolderService.All();
            RootFolder target = null;

            if (int.TryParse(selector, out var rootId))
            {
                target = rootFolders.SingleOrDefault(r => r.Id == rootId);
            }

            target ??= rootFolders.SingleOrDefault(r =>
                string.Equals(
                    new DirectoryInfo(r.Path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)).Name,
                    selector,
                    StringComparison.OrdinalIgnoreCase));

            if (target == null)
            {
                _logger.Warn("Unable to resolve root folder tag '{0}' for series '{1}'. Use rootfolder:<id> or rootfolder:<folder-name>.", rootTags[0].Label, series.Title);
                return;
            }

            var sourcePath = series.Path;
            var destinationPath = Path.Combine(target.Path, _fileNameBuilder.GetSeriesFolder(series));

            if (sourcePath.PathEquals(destinationPath))
            {
                return;
            }

            _logger.Info("Auto-moving series '{0}' from '{1}' to '{2}' due to tag '{3}'", series.Title, sourcePath, destinationPath, rootTags[0].Label);

            series.Path = destinationPath;
            _seriesService.UpdateSeries(series, publishUpdatedEvent: false);

            _commandQueue.Push(new MoveSeriesCommand
            {
                SeriesId = series.Id,
                SourcePath = sourcePath,
                DestinationPath = destinationPath
            });
        }
    }
}

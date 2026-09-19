using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Instrumentation.Extensions;
using NzbDrone.Core.MediaFiles.Commands;
using NzbDrone.Core.MediaFiles.Events;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Organizer;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.MediaFiles
{
    public interface IRenameEpisodeFileService
    {
        List<RenameEpisodeFilePreview> GetRenamePreviews(int seriesId);
        List<RenameEpisodeFilePreview> GetRenamePreviews(int seriesId, int seasonNumber);
        List<RenameEpisodeFilePreview> GetRenamePreviews(List<int> seriesIds);
    }

    public class RenameEpisodeFileService : IRenameEpisodeFileService,
                                            IExecute<RenameFilesCommand>,
                                            IExecute<RenameSeriesCommand>
    {
        private readonly ISeriesService _seriesService;
        private readonly IMediaFileService _mediaFileService;
        private readonly IMoveEpisodeFiles _episodeFileMover;
        private readonly IEventAggregator _eventAggregator;
        private readonly IEpisodeService _episodeService;
        private readonly IBuildFileNames _filenameBuilder;
        private readonly INamingConfigService _namingConfigService;
        private readonly IDiskProvider _diskProvider;
        private readonly Logger _logger;

        public RenameEpisodeFileService(ISeriesService seriesService,
                                        IMediaFileService mediaFileService,
                                        IMoveEpisodeFiles episodeFileMover,
                                        IEventAggregator eventAggregator,
                                        IEpisodeService episodeService,
                                        IBuildFileNames filenameBuilder,
                                        INamingConfigService namingConfigService,
                                        IDiskProvider diskProvider,
                                        Logger logger)
        {
            _seriesService = seriesService;
            _mediaFileService = mediaFileService;
            _episodeFileMover = episodeFileMover;
            _eventAggregator = eventAggregator;
            _episodeService = episodeService;
            _filenameBuilder = filenameBuilder;
            _namingConfigService = namingConfigService;
            _diskProvider = diskProvider;
            _logger = logger;
        }

        public List<RenameEpisodeFilePreview> GetRenamePreviews(int seriesId)
        {
            var series = _seriesService.GetSeries(seriesId);
            var episodes = _episodeService.GetEpisodeBySeries(seriesId);
            var files = _mediaFileService.GetFilesBySeries(seriesId);

            return GetPreviews(series, episodes, files)
                .OrderByDescending(e => e.SeasonNumber)
                .ThenByDescending(e => e.EpisodeNumbers.First())
                .ToList();
        }

        public List<RenameEpisodeFilePreview> GetRenamePreviews(int seriesId, int seasonNumber)
        {
            var series = _seriesService.GetSeries(seriesId);
            var episodes = _episodeService.GetEpisodesBySeason(seriesId, seasonNumber);
            var files = _mediaFileService.GetFilesBySeason(seriesId, seasonNumber);

            return GetPreviews(series, episodes, files)
                .OrderByDescending(e => e.EpisodeNumbers.First()).ToList();
        }

        public List<RenameEpisodeFilePreview> GetRenamePreviews(List<int> seriesIds)
        {
            var seriesList = _seriesService.GetSeries(seriesIds);
            var episodesList = _episodeService.GetEpisodesBySeries(seriesIds).ToLookup(e => e.SeriesId);
            var filesList = _mediaFileService.GetFilesBySeriesIds(seriesIds).ToLookup(f => f.SeriesId);

            return seriesList.SelectMany(series =>
                {
                    var episodes = episodesList[series.Id].ToList();
                    var files = filesList[series.Id].ToList();

                    return GetPreviews(series, episodes, files);
                })
                .OrderByDescending(e => e.SeriesId)
                .ThenByDescending(e => e.SeasonNumber)
                .ThenByDescending(e => e.EpisodeNumbers.First())
                .ToList();
        }

        private NamingConfig GetManualNamingConfig()
        {
            var current = _namingConfigService.GetConfig();

            return new NamingConfig
            {
                Id = current.Id,
                RenameEpisodes = true,
                ReplaceIllegalCharacters = current.ReplaceIllegalCharacters,
                ColonReplacementFormat = current.ColonReplacementFormat,
                CustomColonReplacementFormat = current.CustomColonReplacementFormat,
                MultiEpisodeStyle = current.MultiEpisodeStyle,
                StandardEpisodeFormat = current.StandardEpisodeFormat,
                DailyEpisodeFormat = current.DailyEpisodeFormat,
                AnimeEpisodeFormat = current.AnimeEpisodeFormat,
                SeriesFolderFormat = current.SeriesFolderFormat,
                SeasonFolderFormat = current.SeasonFolderFormat,
                SpecialsFolderFormat = current.SpecialsFolderFormat
            };
        }

        private IEnumerable<RenameEpisodeFilePreview> GetPreviews(Series series, List<Episode> episodes, List<EpisodeFile> files)
        {
            var namingConfig = GetManualNamingConfig();

            foreach (var f in files)
            {
                var file = f;
                var episodesInFile = episodes.Where(e => e.EpisodeFileId == file.Id).ToList();
                var episodeFilePath = Path.Combine(series.Path, file.RelativePath);

                if (!episodesInFile.Any())
                {
                    _logger.Warn("File ({0}) is not linked to any episodes", episodeFilePath);
                    continue;
                }

                var seasonNumber = episodesInFile.First().SeasonNumber;
                var newPath = _filenameBuilder.BuildFilePath(episodesInFile, series, file, Path.GetExtension(episodeFilePath), namingConfig);

                if (!episodeFilePath.PathEquals(newPath, StringComparison.Ordinal))
                {
                    yield return new RenameEpisodeFilePreview
                    {
                        SeriesId = series.Id,
                        SeasonNumber = seasonNumber,
                        EpisodeNumbers = episodesInFile.Select(e => e.EpisodeNumber).ToList(),
                        EpisodeFileId = file.Id,
                        ExistingPath = file.RelativePath,
                        NewPath = series.Path.GetRelativePath(newPath)
                    };
                }
            }
        }

        private List<RenamedEpisodeFile> RenameFiles(List<EpisodeFile> episodeFiles, Series series)
        {
            var renamed = new List<RenamedEpisodeFile>();
            var namingConfig = GetManualNamingConfig();
            var previousRelativePaths = episodeFiles.ToDictionary(f => f.Id, f => f.RelativePath);
            var previousPaths = episodeFiles.ToDictionary(f => f.Id, f => f.Path ?? Path.Combine(series.Path, f.RelativePath));
            var destinationPaths = episodeFiles.ToDictionary(f => f.Id, f =>
            {
                var episodes = _episodeService.GetEpisodesByFileId(f.Id);

                if (episodes == null || episodes.Empty())
                {
                    return previousPaths[f.Id];
                }

                return _filenameBuilder.BuildFilePath(episodes, series, f, Path.GetExtension(previousPaths[f.Id]), namingConfig);
            });

            // When episode assignments are changed, one file can be renamed to a path that is
            // still occupied by another selected file. Stage those sources to unique temporary
            // names first so cyclic renames (E01 -> E02 -> E03 -> E01) cannot overwrite data.
            foreach (var episodeFile in episodeFiles)
            {
                var sourcePath = previousPaths[episodeFile.Id];
                var destinationPath = destinationPaths[episodeFile.Id];
                var destinationIsAnotherSource = previousPaths.Any(p =>
                    p.Key != episodeFile.Id &&
                    p.Value.PathEquals(destinationPath, StringComparison.Ordinal));
                var sourceIsAnotherDestination = destinationPaths.Any(p =>
                    p.Key != episodeFile.Id &&
                    p.Value.PathEquals(sourcePath, StringComparison.Ordinal));

                if ((!destinationIsAnotherSource && !sourceIsAnotherDestination) ||
                    sourcePath.PathEquals(destinationPath, StringComparison.Ordinal))
                {
                    continue;
                }

                var directory = Path.GetDirectoryName(sourcePath);
                var extension = Path.GetExtension(sourcePath);
                string temporaryPath;

                do
                {
                    temporaryPath = Path.Combine(directory, $".sonarr-rename-{episodeFile.Id}-{Guid.NewGuid():N}{extension}");
                }
                while (_diskProvider.FileExists(temporaryPath));

                _logger.Debug("Staging episode file before collision-safe rename: {0} to {1}", sourcePath, temporaryPath);
                _diskProvider.MoveFile(sourcePath, temporaryPath);

                episodeFile.RelativePath = series.Path.GetRelativePath(temporaryPath);
                episodeFile.Path = temporaryPath;
                _mediaFileService.Update(episodeFile);
            }

            foreach (var episodeFile in episodeFiles)
            {
                var previousRelativePath = previousRelativePaths[episodeFile.Id];
                var previousPath = previousPaths[episodeFile.Id];

                try
                {
                    _logger.Debug("Renaming episode file: {0}", episodeFile);
                    _episodeFileMover.MoveEpisodeFile(episodeFile, series, namingConfig);
                    episodeFile.Path = null;

                    _mediaFileService.Update(episodeFile);

                    renamed.Add(new RenamedEpisodeFile
                                {
                                    EpisodeFile = episodeFile,
                                    PreviousRelativePath = previousRelativePath,
                                    PreviousPath = previousPath
                                });

                    _logger.Debug("Renamed episode file: {0}", episodeFile);

                    _eventAggregator.PublishEvent(new EpisodeFileRenamedEvent(series, episodeFile, previousPath));
                }
                catch (FileAlreadyExistsException ex)
                {
                    _logger.Warn("File not renamed, there is already a file at the destination: {0}", ex.Filename);
                }
                catch (SameFilenameException ex)
                {
                    _logger.Debug("File not renamed, source and destination are the same: {0}", ex.Filename);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Failed to rename file {0}", previousPath);
                }
            }

            if (renamed.Any())
            {
                _diskProvider.RemoveEmptySubfolders(series.Path);

                _eventAggregator.PublishEvent(new SeriesRenamedEvent(series, renamed));
            }

            return renamed;
        }

        public void Execute(RenameFilesCommand message)
        {
            var series = _seriesService.GetSeries(message.SeriesId);
            var episodeFiles = _mediaFileService.Get(message.Files);

            _logger.ProgressInfo("Renaming {0} files for {1}", episodeFiles.Count, series.Title);
            var renamedFiles = RenameFiles(episodeFiles, series);
            _logger.ProgressInfo("{0} selected episode files renamed for {1}", renamedFiles.Count, series.Title);

            _eventAggregator.PublishEvent(new RenameCompletedEvent());
        }

        public void Execute(RenameSeriesCommand message)
        {
            _logger.Debug("Renaming all files for selected series");
            var seriesToRename = _seriesService.GetSeries(message.SeriesIds);

            foreach (var series in seriesToRename)
            {
                var episodeFiles = _mediaFileService.GetFilesBySeries(series.Id);
                _logger.ProgressInfo("Renaming all files in series: {0}", series.Title);
                var renamedFiles = RenameFiles(episodeFiles, series);
                _logger.ProgressInfo("{0} episode files renamed for {1}", renamedFiles.Count, series.Title);
            }

            _eventAggregator.PublishEvent(new RenameCompletedEvent());
        }
    }
}

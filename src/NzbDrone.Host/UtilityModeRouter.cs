using NLog;
using NzbDrone.Common;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Common.Processes;
using NzbDrone.Host.AccessControl;
using IServiceProvider = NzbDrone.Common.IServiceProvider;

namespace NzbDrone.Host
{
    public interface IUtilityModeRouter
    {
        void Route(ApplicationModes applicationModes);
    }

    public class UtilityModeRouter : IUtilityModeRouter
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IConsoleService _consoleService;
        private readonly IProcessProvider _processProvider;
        private readonly IRemoteAccessAdapter _remoteAccessAdapter;
        private readonly IAppFolderFactory _appFolderFactory;
        private readonly IStartupContext _startupContext;
        private readonly Logger _logger;

        public UtilityModeRouter(IServiceProvider serviceProvider,
                      IConsoleService consoleService,
                      IProcessProvider processProvider,
                      IRemoteAccessAdapter remoteAccessAdapter,
                      IAppFolderFactory appFolderFactory,
                      IStartupContext startupContext,
                      Logger logger)
        {
            _serviceProvider = serviceProvider;
            _consoleService = consoleService;
            _processProvider = processProvider;
            _remoteAccessAdapter = remoteAccessAdapter;
            _appFolderFactory = appFolderFactory;
            _startupContext = startupContext;
            _logger = logger;
        }

        public void Route(ApplicationModes applicationModes)
        {
            _logger.Info("Application mode: {0}", applicationModes);

            switch (applicationModes)
            {
                case ApplicationModes.InstallService:
                    {
                        var serviceName = _startupContext.ServiceName ?? ServiceProvider.SERVICE_NAME;
                        _logger.Debug("Install Service selected: {0}", serviceName);
                        if (_serviceProvider.ServiceExist(serviceName))
                        {
                            _consoleService.PrintServiceAlreadyExist();
                        }
                        else
                        {
                            _remoteAccessAdapter.MakeAccessible(true);
                            _serviceProvider.Install(serviceName);
                            _serviceProvider.SetPermissions(serviceName);

                            // Start the service and exit.
                            // Ensures that there isn't an instance of Sonarr already running that the service account cannot stop.
                            _processProvider.SpawnNewProcess("sc.exe", $"start \"{serviceName}\"", null, true);
                        }

                        break;
                    }

                case ApplicationModes.UninstallService:
                    {
                        var serviceName = _startupContext.ServiceName ?? ServiceProvider.SERVICE_NAME;
                        _logger.Debug("Uninstall Service selected: {0}", serviceName);
                        if (!_serviceProvider.ServiceExist(serviceName))
                        {
                            _consoleService.PrintServiceDoesNotExist();
                        }
                        else
                        {
                            _serviceProvider.Uninstall(serviceName);
                        }

                        break;
                    }

                case ApplicationModes.RegisterUrl:
                    {
                        _logger.Debug("Register URL selected");
                        _remoteAccessAdapter.MakeAccessible(false);
                        _appFolderFactory.SetPermissions();

                        break;
                    }

                default:
                    {
                        _consoleService.PrintHelp();
                        break;
                    }
            }
        }
    }
}

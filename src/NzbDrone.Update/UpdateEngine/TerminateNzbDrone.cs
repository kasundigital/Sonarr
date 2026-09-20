using System;
using NLog;
using NzbDrone.Common;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Common.Processes;
using IServiceProvider = NzbDrone.Common.IServiceProvider;

namespace NzbDrone.Update.UpdateEngine
{
    public interface ITerminateNzbDrone
    {
        void Terminate(int processId);
    }

    public class TerminateNzbDrone : ITerminateNzbDrone
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IProcessProvider _processProvider;
        private readonly IStartupContext _startupContext;
        private readonly Logger _logger;

        public TerminateNzbDrone(IServiceProvider serviceProvider, IProcessProvider processProvider, IStartupContext startupContext, Logger logger)
        {
            _serviceProvider = serviceProvider;
            _processProvider = processProvider;
            _startupContext = startupContext;
            _logger = logger;
        }

        public void Terminate(int processId)
        {
            if (OsInfo.IsWindows)
            {
                var serviceName = _startupContext.ServiceName;

                if (!string.IsNullOrWhiteSpace(serviceName) &&
                    _serviceProvider.ServiceExist(serviceName) &&
                    _serviceProvider.IsServiceRunning(serviceName))
                {
                    try
                    {
                        _logger.Info("Stopping Windows service '{0}' for process {1}", serviceName, processId);
                        _serviceProvider.Stop(serviceName);
                        return;
                    }
                    catch (Exception e)
                    {
                        _logger.Error(e, "Couldn't stop service '{0}', falling back to terminating process {1}", serviceName, processId);
                    }
                }

                _logger.Info("Terminating Sonarr process {0}", processId);
                _processProvider.Kill(processId);
            }
            else
            {
                _logger.Info("Killing all running processes");

                _processProvider.KillAll(ProcessProvider.SONARR_CONSOLE_PROCESS_NAME);
                _processProvider.KillAll(ProcessProvider.SONARR_PROCESS_NAME);

                _processProvider.Kill(processId);
            }
        }
    }
}

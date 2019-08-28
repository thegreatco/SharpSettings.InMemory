using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using KellermanSoftware.CompareNetObjects;
using KellermanSoftware.CompareNetObjects.TypeComparers;
using Microsoft.Extensions.Logging;

namespace SharpSettings.InMemory
{
    public class SharpSettingsInMemoryDataWatcher<TSettingsObject> : ISettingsWatcher<string, TSettingsObject>
        where TSettingsObject : WatchableSettings<string>
    {
        private CancellationTokenSource _cancellationTokenSource;
        private Task _watcherTask;
        private TSettingsObject _settings;
        private bool _startupComplete;

        private readonly string _settingsId;
        private readonly CompareLogic _compareLogic;
        private readonly SharpSettingsInMemoryDataStore<TSettingsObject> _store;
        private readonly Action<TSettingsObject> _settingsUpdatedCallback;
        private readonly ILogger _logger;

        public SharpSettingsInMemoryDataWatcher(ILogger logger, SharpSettingsInMemoryDataStore<TSettingsObject> settingsStore, WatchableSettings<string> settings,
            Action<TSettingsObject> settingsUpdatedCallback,
            IEnumerable<BaseTypeComparer> customComparers = null, CancellationTokenSource cts = default)
            : this(logger, settingsStore, settings.Id, settingsUpdatedCallback, customComparers, cts)
        {
        }

        public SharpSettingsInMemoryDataWatcher(ILogger logger, SharpSettingsInMemoryDataStore<TSettingsObject> settingsStore, string settingsId,
            Action<TSettingsObject> settingsUpdatedCallback,
            IEnumerable<BaseTypeComparer> customComparers = null, CancellationTokenSource cts = default)
        {
            _logger = logger;
            _cancellationTokenSource = cts;
            _compareLogic = new CompareLogic();
            if (customComparers != null)
                _compareLogic.Config.CustomComparers.AddRange(customComparers);
            _store = settingsStore;
            _settingsId = settingsId;
            _settingsUpdatedCallback = settingsUpdatedCallback;
            CreateWatcherTask();
        }

        private void CreateWatcherTask()
        {
            _logger.LogDebug("Calling start on a Polling task.");
            _watcherTask = Task.Factory.StartNew(PollAsync, _cancellationTokenSource.Token, TaskCreationOptions.LongRunning,
                TaskScheduler.Default).Unwrap();
            _logger.LogDebug("Finished calling start on a Polling task.");
        }

        private async Task PollAsync()
        {
            _logger.LogTrace("Starting a Polling task.");
            while (!_cancellationTokenSource.IsCancellationRequested)
            {
                try
                {
                    var tmpSettings = _store.Find(_settingsId);
                    _startupComplete = true;

                    if (_compareLogic.Compare(tmpSettings, _settings).AreEqual == false)
                    {
                        _logger.LogTrace("Settings updated.");

                        _settings = tmpSettings;
                        _settingsUpdatedCallback?.Invoke(_settings);

                        _logger.LogTrace("SettingsWatcher notified.");
                    }
                    if (_settings == null)
                    {
                        _logger.LogWarning("Settings not found.");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred in the polling task");
                }
                await Task.Delay(500).ConfigureAwait(false);
            }
            _logger.LogTrace("Ending a Polling task.");
        }

        /// <summary>
        /// Dispose of this <see cref="ISettingsWatcher{TId, TSettings}"/> object
        /// </summary>
        public void Dispose()
        {
            DisposeAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Asynchronously dispose of this <see cref="ISettingsWatcher{TId, TSettings}"/> object
        /// </summary>
        /// <returns>A <see cref="ValueTask"/> representing the dispose operation.</returns>
        public async ValueTask DisposeAsync()
        {
            _cancellationTokenSource.Cancel(false);
            await _watcherTask.ConfigureAwait(false);
        }

        /// <summary>
        /// Get the <see cref="TSettings"/> object
        /// </summary>
        /// <returns>The <see cref="TSettings"/> object if available, otherwise null</returns>
        public TSettingsObject GetSettings()
        {
            return _settings;
        }

        /// <summary>
        /// Wait for the <see cref="TSettings"/> object to become available and get it
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async ValueTask<TSettingsObject> GetSettingsAsync(CancellationToken token = default)
        {
            while (_settings == null && !token.IsCancellationRequested)
            {
                await Task.Delay(100, token).ConfigureAwait(false);
            }

            return _settings;
        }

        /// <summary>
        /// Restart the <see cref="ISettingsWatcher{TId, TSettings}"/>. Safe to use in both faulted and unfaulted states.
        /// Blocks until startup has completed.
        /// </summary>
        /// <param name="timeout">The timeout in milliseconds, set to -1 for no timeout</param>
        /// <returns>A <see cref="bool"/> value indicating if the restart was successful within the <paramref name="timeout"/>.</returns>
        public bool Restart(int timeout)
        {
            return RestartAsync(timeout).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Asynchronously restart the <see cref="ISettingsWatcher{TId, TSettings}"/>. Safe to use in both faulted and unfaulted states.
        /// Blocks until startup has completed.
        /// </summary>
        /// <param name="timeout">The timeout in milliseconds, set to -1 for no timeout</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> to cancel the operation</param>
        /// <returns>A <see cref="bool"/> value indicating if the restart was successful within the <paramref name="timeout"/>.</returns>
        public async ValueTask<bool> RestartAsync(int timeout, CancellationToken cancellationToken = default)
        {
            try
            {
                _cancellationTokenSource.Cancel();
                await _watcherTask.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Caught expected exception while restarting the watcher.");
            }
            _cancellationTokenSource = new CancellationTokenSource();
            CreateWatcherTask();
            return await WaitForStartupAsync(timeout, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Synchronously wait for the <see cref="ISettingsWatcher{TId, TSettings}"/> to startup.
        /// Since the <see cref="ISettingsWatcher{TId, TSettings}"/> may require network I/O and significant work
        /// to provide the settings, it may require some amount of time to startup. If applications must block
        /// until this infrastructure is setup, call this method.
        /// </summary>
        /// <param name="timeout">The timeout in milliseconds, set to 0 for no timeout</param>
        /// <returns>A <see cref="bool"/> value indicating if the startup was successful within the <paramref name="timeout"/>.</returns>
        public bool WaitForStartup(int timeout)
        {
            return WaitForStartupAsync(timeout).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Wait for the <see cref="ISettingsWatcher{TId, TSettings}"/> to startup.
        /// Since the <see cref="ISettingsWatcher{TId, TSettings}"/> may require network I/O and significant work
        /// to provide the settings, it may require some amount of time to startup. If applications must block
        /// until this infrastructure is setup, <see langword="await"/> this method.
        /// </summary>
        /// <param name="timeout">The timeout in milliseconds, set to 0 for no timeout</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> to cancel the operation</param>
        /// <returns>A <see cref="bool"/> value indicating if the startup was successful within the <paramref name="timeout"/>.</returns>
        public ValueTask<bool> WaitForStartupAsync(int timeout, CancellationToken cancellationToken = default)
        {
            var timespan = timeout == 0 ? TimeSpan.Zero : TimeSpan.FromMilliseconds(timeout);
            return WaitForStartupAsync(timespan, cancellationToken);
        }

        /// <summary>
        /// Synchronously wait for the <see cref="ISettingsWatcher{TId, TSettings}"/> to startup.
        /// Since the <see cref="ISettingsWatcher{TId, TSettings}"/> may require network I/O and significant work
        /// to provide the settings, it may require some amount of time to startup. If applications must block
        /// until this infrastructure is setup, call this method.
        /// </summary>
        /// <param name="timeout">The amount of time to wait for startup, set to <see cref="TimeSpan.Zero"/> for no timeout</param>
        /// <returns>A <see cref="bool"/> value indicating if the startup was successful within the <paramref name="timeout"/>.</returns>
        public bool WaitForStartup(TimeSpan timeout)
        {
            return WaitForStartupAsync(timeout).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Wait for the <see cref="ISettingsWatcher{TId, TSettings}"/> to startup.
        /// Since the <see cref="ISettingsWatcher{TId, TSettings}"/> may require network I/O and significant work
        /// to provide the settings, it may require some amount of time to startup. If applications must block
        /// until this infrastructure is setup, <see langword="await"/> this method.
        /// </summary>
        /// <param name="timeout">The amount of time to wait for startup, set to <see cref="TimeSpan.Zero"/> for no timeout</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> to cancel the operation</param>
        /// <returns>A <see cref="bool"/> value indicating if the startup was successful within the <paramref name="timeout"/>.</returns>
        public async ValueTask<bool> WaitForStartupAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            DateTime endTime;
            if (timeout == TimeSpan.Zero)
            {
                endTime = DateTime.MaxValue;
            }
            else 
                endTime = DateTime.UtcNow.Add(timeout);
            while(_startupComplete == false && endTime > DateTime.UtcNow)
            {
                await Task.Delay(10, cancellationToken).ConfigureAwait(false);
            }
            return _startupComplete;
        }

        /// <summary>
        /// A value indicating if an internal fault has stopped the <see cref="ISettingsWatcher{TId, TSettings}"/>
        /// </summary>
        /// <returns>A <see cref="bool"/> value indicating the fault state.</returns>
        public bool IsFaulted()
        {
            return _watcherTask == null 
                ? true 
                : !IsRunning() 
                    || _startupComplete == false;
        }

        /// <summary>
        /// A value indicating if the <see cref="ISettingsWatcher{TId, TSettings}"/> is is running
        /// </summary>
        /// <returns>A <see cref="bool"/> value indicating the internal run state.</returns>
        public bool IsRunning()
        {
            return _watcherTask == null 
                ? false 
                : (_watcherTask.Status == TaskStatus.WaitingForActivation 
                    || _watcherTask.Status == TaskStatus.Running 
                    || _watcherTask.Status == TaskStatus.WaitingToRun) 
                && _startupComplete;
        }
    }
}

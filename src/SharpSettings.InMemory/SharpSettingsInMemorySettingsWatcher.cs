using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using KellermanSoftware.CompareNetObjects;
using KellermanSoftware.CompareNetObjects.TypeComparers;

namespace SharpSettings.InMemory
{
    public class SharpSettingsInMemoryDataWatcher<TSettingsObject> : ISettingsWatcher<string, TSettingsObject>
        where TSettingsObject : WatchableSettings<string>
    {
        private readonly string _settingsId;
        private readonly CompareLogic _compareLogic;
        private readonly SharpSettingsInMemoryDataStore<TSettingsObject> _store;
        private Task _watcherTask;
        private readonly Action<TSettingsObject> _settingsUpdatedCallback;
        private readonly CancellationTokenSource _cancellationTokenSource;

        private TSettingsObject _settings;

        public SharpSettingsInMemoryDataWatcher(SharpSettingsInMemoryDataStore<TSettingsObject> settingsStore, WatchableSettings<string> settings,
            Action<TSettingsObject> settingsUpdatedCallback,
            IEnumerable<BaseTypeComparer> customComparers = null, CancellationTokenSource cts = default(CancellationTokenSource))
            : this(settingsStore, settings.Id, settingsUpdatedCallback, customComparers, cts)
        {
        }

        public SharpSettingsInMemoryDataWatcher(SharpSettingsInMemoryDataStore<TSettingsObject> settingsStore, string settingsId,
            Action<TSettingsObject> settingsUpdatedCallback,
            IEnumerable<BaseTypeComparer> customComparers = null, CancellationTokenSource cts = default(CancellationTokenSource))
        {
            _cancellationTokenSource = cts;
            _compareLogic = new CompareLogic();
            if (customComparers != null)
                _compareLogic.Config.CustomComparers.AddRange(customComparers);
            _store = settingsStore;
            _settingsId = settingsId;
            _settingsUpdatedCallback = settingsUpdatedCallback;
            _store.Logger?.Debug("Calling start on a Polling task.");
            _watcherTask = Task.Factory.StartNew(PollAsync, _cancellationTokenSource.Token, TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
            _store.Logger?.Debug("Finished calling start on a Polling task.");
        }

        private async Task PollAsync()
        {
            _store.Logger?.Trace("Starting a Polling task.");
            while (!_cancellationTokenSource.IsCancellationRequested)
            {
                try
                {
                    var tmpSettings = _store.Find(_settingsId);

                    if (_compareLogic.Compare(tmpSettings, _settings).AreEqual == false)
                    {
                        _store.Logger?.Trace("Settings updated.");

                        _settings = tmpSettings;
                        _settingsUpdatedCallback?.Invoke(_settings);

                        _store.Logger?.Trace("SettingsWatcher notified.");
                    }
                    if (_settings == null)
                    {
                        _store.Logger?.Warn("Settings not found.");
                    }
                }
                catch (Exception ex)
                {
                    _store.Logger?.Error(ex);
                }
                await Task.Delay(500);
            }
            _store.Logger?.Trace("Ending a Polling task.");
        }

        public async Task<TSettingsObject> GetSettingsAsync(CancellationToken token = default(CancellationToken))
        {
            while (_settings == null && !token.IsCancellationRequested)
            {
                await Task.Delay(100, token);
            }

            return _settings;
        }

        public void Dispose()
        {
            _cancellationTokenSource.Cancel(false);
            _watcherTask?.Wait(TimeSpan.FromSeconds(10));
        }
    }
}

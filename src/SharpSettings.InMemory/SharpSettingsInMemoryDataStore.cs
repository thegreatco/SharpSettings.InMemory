using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SharpSettings.InMemory
{
    public class SharpSettingsInMemoryDataStore <TSettingsObject> : ISharpSettingsDataStore<string, TSettingsObject>
        where TSettingsObject : WatchableSettings<string>
    {
        private readonly IEnumerable<TSettingsObject> Store;
        private readonly ILogger Logger;

        public SharpSettingsInMemoryDataStore(IEnumerable<TSettingsObject> store, ILogger logger = null)
        {
            Store = store;
            Logger = logger;
        }

        public SharpSettingsInMemoryDataStore(IEnumerable<TSettingsObject> store, ILoggerFactory loggerFactory = null)
            : this(store, loggerFactory?.CreateLogger<SharpSettingsInMemoryDataStore<TSettingsObject>>())
        {
        }

        public ValueTask<TSettingsObject> FindAsync(string settingsObjectId)
        {
            return new ValueTask<TSettingsObject>(Store.SingleOrDefault(x => x.Id == settingsObjectId));
        }

        public TSettingsObject Find(string settingsObjectId)
        {
            return Store.SingleOrDefault(x => x.Id == settingsObjectId);
        }
    }
}

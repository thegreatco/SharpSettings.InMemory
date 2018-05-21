using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SharpSettings.InMemory
{
    public class SharpSettingsInMemoryDataStore <TSettingsObject> : ISharpSettingsDataStore<string, TSettingsObject>
        where TSettingsObject : WatchableSettings<string>
    {
        internal readonly IEnumerable<TSettingsObject> Store;
        internal readonly ISharpSettingsLogger Logger;

        public SharpSettingsInMemoryDataStore(IEnumerable<TSettingsObject> store, ISharpSettingsLogger logger = null)
        {
            Store = store;
            Logger = logger;
        }

        public Task<TSettingsObject> FindAsync(string settingsObjectId)
        {
            return Task.FromResult(Store.SingleOrDefault(x => x.Id == settingsObjectId));
        }

        public TSettingsObject Find(string settingsObjectId)
        {
            return Store.SingleOrDefault(x => x.Id == settingsObjectId);
        }
    }
}

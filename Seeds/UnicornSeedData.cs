using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#if IL2CPP
using Il2CppScheduleOne.Product;
#elif MONO
using ScheduleOne.Product;
#endif

namespace UnicornsCustomSeeds.Seeds
{
    /// <summary>
    /// Legacy DTO for deserializing old DiscoveredCustomSeeds.json files.
    /// Only used during migration in PersistencePatches.
    /// </summary>
    [Serializable]
    internal class LegacySeedData
    {
        public string seedId { get; set; }
        public string weedId { get; set; }
        public string baseSeedId { get; set; }
        public float price { get; set; }
    }

    [Serializable]
    public class UnicornSeedData
    {
        public string seedId { get; set; }
        public string mixId { get; set; }
        public EDrugType drugType { get; set; }
        public float price { get; set; }
    }
}

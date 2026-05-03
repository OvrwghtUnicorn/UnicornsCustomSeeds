using System;
using System.Collections.Generic;

namespace UnicornsCustomSeeds.Managers
{
    /// <summary>
    /// Generic runtime registry mapping a station GUID (string) to the mix ID
    /// currently being cooked inside it.  Drug-agnostic: works for Cauldrons
    /// today and any future GridItem station tomorrow.
  ///
    /// Persisted to UnicornsActiveCooking.json alongside DiscoveredCustomSeeds.json.
    /// </summary>
    public static class ActiveCookingRegistry
    {
        // Key:   cauldron (or other station) GUID as string
        // Value: mixId of the custom product being cooked
        public static readonly Dictionary<string, string> GuidToMixId
            = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Record that a station has started a custom cook.</summary>
        public static void Register(string stationGuid, string mixId)
        {
            GuidToMixId[stationGuid] = mixId;
        }

        /// <summary>Remove a station's cook entry when the operation finishes.</summary>
    public static void Unregister(string stationGuid)
        {
            GuidToMixId.Remove(stationGuid);
}

     /// <summary>Look up the mixId for a station, returns null if not a custom cook.</summary>
 public static string GetMixId(string stationGuid)
     {
     GuidToMixId.TryGetValue(stationGuid, out string mixId);
  return mixId;
        }

        public static void Clear() => GuidToMixId.Clear();
    }

    /// <summary>DTO for JSON serialisation of UnicornsActiveCooking.json.</summary>
    [Serializable]
    public class ActiveCookingEntry
  {
        public string stationGuid { get; set; }
        public string mixId       { get; set; }
    }
}

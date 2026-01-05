using System;
using S1API.Quests;

namespace UnicornsCustomSeeds.SeedQuests
{
    /// <summary>
    /// Data class for CustomSeedQuest to store persistent data
    /// </summary>
    [Serializable]
    public class CustomSeedQuestData
    {
        /// <summary>
        /// Custom value for specific quest behavior
        /// Add your own data fields here as needed
        /// </summary>
        public float quantity { get; set; } = 0;
        public string seedId { get; set; } = String.Empty;
    }
}

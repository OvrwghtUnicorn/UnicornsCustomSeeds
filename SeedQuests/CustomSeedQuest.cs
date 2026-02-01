using S1API.Quests;
using S1API.Saveables;
using UnicornsCustomSeeds.Managers;

#if IL2CPP
using Il2CppScheduleOne.Product;
#elif MONO
using ScheduleOne.Product;
#endif

namespace UnicornsCustomSeeds.SeedQuests
{
    /// <summary>
    /// Custom Quest: Drop off the Mix
    /// </summary>
    public class CustomSeedQuest : Quest
    {
        public WeedDefinition WeedDefinition;

        /// <summary>
        /// The title of the quest
        /// </summary>
        protected override string Title => "Drop off the Mix";

        protected override void OnLoaded()
        {
            if (QuestEntries.Count == 0)
            {
                customSeedEntry = AddEntry($"Give Albert {StashManager.StashQtyEntry.Value}x of a Weed Mix and ${StashManager.StashCostEntry.Value}", poiPosition: StashManager.albertsStash.transform.position);
            }
        }

        /// <summary>
        /// The description of the quest
        /// </summary>
        protected override string Description => $"Take {StashManager.StashQtyEntry.Value}x of your weed mix and ${StashManager.StashCostEntry.Value} dollary doos to Albert Hoovers supply stash";

        /// <summary>
        /// Persistent data for this quest
        /// </summary>
        [SaveableField("QuestData")]
        private CustomSeedQuestData _data = new CustomSeedQuestData();

        /// <summary>
        /// Quest entry: Complete objective
        /// </summary>
        public QuestEntry customSeedEntry;

        /// <summary>
        /// Called when the quest is created
        /// </summary>
        protected override void OnCreated()
        {
            if (QuestEntries.Count == 0) {
                // Add quest entry: Complete objective
                customSeedEntry = AddEntry($"Give Albert {StashManager.StashQtyEntry.Value}x of a Weed Mix and ${StashManager.StashCostEntry.Value}", poiPosition: StashManager.albertsStash.transform.position);
            }
        }

    }
}

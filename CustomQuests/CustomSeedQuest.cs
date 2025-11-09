using S1API.Quests;
using S1API.Saveables;
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnicornsCustomSeeds.SupplierStashes;
using Il2CppScheduleOne.UI.Phone.Messages;
using Il2CppScheduleOne.Messaging;


#if IL2CPP
using Il2CppScheduleOne.Product;
#elif MONO
using ScheduleOne.Product;
#endif

namespace UnicornsCustomSeeds.CustomQuests
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

        /// <summary>
        /// The description of the quest
        /// </summary>
        protected override string Description => "Take 20x of your weed mix and $500 dollary doos to Albert Hoovers supply stash";

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
            if (StashManager.albertsStash != null)
            {
                Utility.Log("Stash exists");
            }
            
            // Add quest entry: Complete objective
            customSeedEntry = AddEntry("Give Albert 20x of a Weed Strain and $500", poiPosition: StashManager.albertsStash.transform.position);
        }

        public void OnComplete()
        {

        }

    }
}

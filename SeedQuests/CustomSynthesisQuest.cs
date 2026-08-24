using S1API.Quests;
using S1API.Saveables;
using UnicornsCustomSeeds.Managers;
using UnicornsCustomSeeds.TemplateUtils;


#if IL2CPP
using Il2CppScheduleOne.Economy;
using Il2CppScheduleOne.Product;
#elif MONO
using ScheduleOne.Economy;
using ScheduleOne.Product;
#endif

namespace UnicornsCustomSeeds.SeedQuests
{
    /// <summary>
    /// Generic synthesis quest used by all drug types (Weed, Shrooms, Coca, Pseudo).
    /// The EDrugType stored in _data.drugType drives the title, description, and
    /// which NPC / stash is referenced.
    /// </summary>
    public class CustomSynthesisQuest : Quest
    {
        [SaveableField("SynthesisQuestData")]
        private CustomSynthesisQuestData _data = new CustomSynthesisQuestData();

        public QuestEntry dropoffEntry;

        protected override string Title => GetTitle();
        protected override string Description => GetDescription();

        private string GetTitle()
        {
            return _data.drugType switch
            {
                EDrugType.Marijuana      => "Drop off the Mix",
                EDrugType.Shrooms        => "Drop off the Shroom Mix",
                EDrugType.Cocaine        => "Drop off the Cocaine Mix",
                EDrugType.Methamphetamine => "Drop off the Meth Mix",
                _                        => "Drop off the Mix",
            };
        }

        private string GetDescription()
        {
            string npc  = GetNPCName();
            string item = GetItemName();
            return $"Take {StashManager.StashQtyEntry.Value}x of your {item} and ${StashManager.StashCostEntry.Value} to {npc}'s supply stash";
        }

        private string GetNPCName() => _data.drugType switch
        {
            EDrugType.Marijuana       => "Albert",
            EDrugType.Shrooms         => "Phil",
            EDrugType.Cocaine         => "Salvador",
            EDrugType.Methamphetamine => "Shirley",
            _                         => "the supplier",
        };

        private string GetItemName() => _data.drugType switch
        {
            EDrugType.Marijuana       => "weed mix",
            EDrugType.Shrooms         => "shroom mix",
            EDrugType.Cocaine         => "cocaine mix",
            EDrugType.Methamphetamine => "meth mix",
            _                         => "mix",
        };

        private string GetStashDescription()
        {
            return _data.drugType switch
            {
                EDrugType.Marijuana       => StashManager.albertsStash?.transform.position.ToString() ?? "",
                EDrugType.Shrooms         => PhilStashManager.philsStash?.transform.position.ToString() ?? "",
                EDrugType.Cocaine         => SalvadorStashManager.salvadorsStash?.transform.position.ToString() ?? "",
                EDrugType.Methamphetamine => ShirleyStashManager.shirleysStash?.transform.position.ToString() ?? "",
                _                         => "",
            };
        }

        public void SetDrugType(EDrugType drugType)
        {
            _data.drugType = drugType;
        }

        protected override void OnCreated()
        {
            if (QuestEntries.Count == 0)
            {
                Utility.Log("[OnCreated]");
                AddDropoffEntry();
            }
        }

        protected override void OnLoaded()
        {
            if (QuestEntries.Count == 0)
            {
                Utility.Log("[OnLoaded]");
                AddDropoffEntry();
            }
        }

        private void AddDropoffEntry()
        {
            string npc  = GetNPCName();
            string item = GetItemName();

            // Temporary diagnostic bracketing — a client-side crash was landing
            // somewhere in this method with no managed exception (a native/IL2CPP
            // crash gives no stack), so every candidate call is isolated with a log
            // on both sides. Whichever "...OK" line is missing on the next repro
            // identifies the exact crashing call. Remove once found.
            Utility.Log($"[AddDropoffEntry] start, drugType={_data.drugType}");

            Utility.Log("[AddDropoffEntry] calling GetStash()...");
            var stash = GetStash();
            Utility.Log($"[AddDropoffEntry] GetStash() OK, stash={(stash != null ? "non-null" : "null")}");

            UnityEngine.Vector3 poi = UnityEngine.Vector3.zero;
            if (stash != null)
            {
                Utility.Log("[AddDropoffEntry] reading stash.transform.position...");
                poi = stash.transform.position;
                Utility.Log($"[AddDropoffEntry] stash.transform.position OK, poi={poi}");
            }

            Utility.Log("[AddDropoffEntry] calling AddEntry()...");
            dropoffEntry = AddEntry(
                $"Give {npc} {StashManager.StashQtyEntry.Value}x of a {item} and ${StashManager.StashCostEntry.Value}",
                poiPosition: poi);
            Utility.Log("[AddDropoffEntry] AddEntry() OK, done.");
        }

        private SupplierStash GetStash() => _data.drugType switch
        {
            EDrugType.Marijuana       => StashManager.GetSupplierStash(),
            EDrugType.Shrooms         => PhilStashManager.GetSupplierStash(),
            EDrugType.Cocaine         => SalvadorStashManager.GetSupplierStash(),
            EDrugType.Methamphetamine => ShirleyStashManager.GetSupplierStash(),
            _                         => null,
        };
    }
}

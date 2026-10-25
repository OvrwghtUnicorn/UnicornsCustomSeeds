using System;
using UnicornsCustomSeeds.TemplateUtils;
using UnityEngine;

#if IL2CPP
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Economy;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.Money;
using Il2CppScheduleOne.Product;
using Il2CppScheduleOne.UI;
using Il2CppTMPro;
#elif MONO
using ScheduleOne.DevUtilities;
using ScheduleOne.Economy;
using ScheduleOne.ItemFramework;
using ScheduleOne.Money;
using ScheduleOne.Product;
using ScheduleOne.UI;
using TMPro;
#endif

namespace UnicornsCustomSeeds.Managers
{
    /// <summary>
    /// Live status line inside the storage menu while one of the four supplier stashes is
    /// open: tells the player why closing the stash will (or won't) start a synthesis.
    ///
    /// StorageMenu is a single shared singleton for every container in the game, so there
    /// is one text object, not one per stash. Visibility is decided from
    /// StorageMenu.OpenedStorageEntity, NOT StorageEntity.onOpened/onClosed — those come
    /// from an ObserversRpc and fire on every client whenever anyone opens the stash.
    /// </summary>
    public static class StashInfoUI
    {
        /// <summary>
        /// The info text object, created by EnsureText. It sits inside StorageMenu.Container
        /// (via CloseButtonContainer), so the game's own open/close hides it with the menu.
        /// </summary>
        public static TextMeshProUGUI InfoText;

        private const string WarnColor = "#FF5A5A";
        private const string OkColor = "#54E717";

        /// <summary>
        /// Called by each supplier's Get*Stash() once its stash is found and subscribed.
        /// Hooks live refresh while items move in and out.
        /// </summary>
        public static void OnStashFound(SupplierStash stash, EDrugType drug)
        {
            if (stash == null) return;
            stash.Storage.onContentsChanged += (Action)Refresh;
        }

        /// <summary>
        /// Creates InfoText on first use, by cloning the menu's own subtitle so it inherits
        /// the game's font, size and material. Created lazily from Refresh rather than at
        /// stash discovery: StorageMenu lives in the Main scene and is destroyed with it,
        /// and a destroyed InfoText reads as null here, so it is simply rebuilt next time.
        ///
        /// Parented to CloseButtonContainer, which the game repositions below the slot grid
        /// on every Open — so the text follows the grid height for any row count.
        /// </summary>
        private static bool EnsureText(StorageMenu menu)
        {
            if (InfoText != null) return true;
            if (menu == null || menu.SubtitleLabel == null || menu.CloseButtonContainer == null) return false;

            InfoText = UnityEngine.Object.Instantiate(menu.SubtitleLabel, menu.CloseButtonContainer);
            InfoText.gameObject.name = "UnicornsStashInfoText";
            InfoText.text = string.Empty;
            InfoText.raycastTarget = false;
            InfoText.alignment = TextAlignmentOptions.Top;

            RectTransform rect = InfoText.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -10f);
            rect.sizeDelta = new Vector2(700f, 80f);

            InfoText.gameObject.SetActive(false);
            return true;
        }

        /// <summary>
        /// The supplier stash currently shown in the local player's storage menu, or null
        /// if the menu is closed or showing some other container. Compared by instance ID,
        /// which is safe across IL2CPP wrapper objects.
        /// </summary>
        public static SupplierStash GetOpenStash(out EDrugType drug)
        {
            drug = EDrugType.Marijuana;

            var menu = Singleton<StorageMenu>.Instance;
            if (menu == null || !menu.IsOpen || menu.OpenedStorageEntity == null) return null;

            int openedId = menu.OpenedStorageEntity.GetInstanceID();

            if (IsStorage(StashManager.albertsStash, openedId)) { drug = EDrugType.Marijuana; return StashManager.albertsStash; }
            if (IsStorage(PhilStashManager.philsStash, openedId)) { drug = EDrugType.Shrooms; return PhilStashManager.philsStash; }
            if (IsStorage(SalvadorStashManager.salvadorsStash, openedId)) { drug = EDrugType.Cocaine; return SalvadorStashManager.salvadorsStash; }
            if (IsStorage(ShirleyStashManager.shirleysStash, openedId)) { drug = EDrugType.Methamphetamine; return ShirleyStashManager.shirleysStash; }

            return null;
        }

        private static bool HasActiveQuest(string stashName)
        {
            if (stashName.ToLower().Contains("albert"))
            {
                return SeedQuestManager.HasActiveQuest;
            } 
            else if (stashName.ToLower().Contains("phil")) {
                return ShroomQuestManager.HasActiveQuest;
            }
            else if (stashName.ToLower().Contains("shirley"))
            {
                return PseudoQuestManager.HasActiveQuest;
            }
            else if (stashName.ToLower().Contains("salvador"))
            {
                return CocaQuestManager.HasActiveQuest;
            } else
            {
                return true;
            }
        }

        private static bool IsDrugType(ProductItemInstance item, EDrugType drug)
        {
            switch (drug)
            {
#if IL2CPP
                case EDrugType.Marijuana: return item.TryCast<WeedInstance>() != null;
                case EDrugType.Shrooms: return item.TryCast<ShroomInstance>() != null;
                case EDrugType.Cocaine: return item.TryCast<CocaineInstance>() != null;
                case EDrugType.Methamphetamine: return item.TryCast<MethInstance>() != null;
#elif MONO
                case EDrugType.Marijuana: return item is WeedInstance;
                case EDrugType.Shrooms: return item is ShroomInstance;
                case EDrugType.Cocaine: return item is CocaineInstance;
                case EDrugType.Methamphetamine: return item is MethInstance;
#endif
                default: return false;
            }
        }

        private static bool IsStorage(SupplierStash stash, int storageInstanceId)
        {
            return stash != null && stash.Storage != null && stash.Storage.GetInstanceID() == storageInstanceId;
        }

        public static void Hide()
        {
            if (InfoText != null) InfoText.gameObject.SetActive(false);
        }

        public static void Refresh()
        {
            try
            {
                SupplierStash stash = GetOpenStash(out EDrugType drug);
                if (stash == null || !HasActiveQuest(stash.gameObject.name))
                {
                    Hide();
                    return;
                }

                if (!EnsureText(Singleton<StorageMenu>.Instance)) return;

                InfoText.text = BuildStatus(stash, drug);
                InfoText.gameObject.SetActive(true);
            }
            catch (Exception e)
            {
                Utility.PrintException(e);
            }
        }

        /// <summary>
        /// Mirrors the close handlers (StashManager.AlbertsStashClosed and the Phil /
        /// Salvador / Shirley equivalents) exactly: the LAST cash slot's balance and the
        /// LAST matching product slot. The point is to report what closing will actually
        /// do, so if those handlers change, change this with them.
        /// </summary>
        private static string BuildStatus(SupplierStash stash, EDrugType drug)
        {
            int reqUnits = StashManager.StashQtyEntry.Value;
            int reqCash = StashManager.StashCostEntry.Value;
            bool wrongDrug = false;

            CashInstance cash = null;
            ProductItemInstance product = null;
            var distinctMixes = new HashSet<string>();

            foreach (var slot in stash.Storage.ItemSlots)
            {
                if (slot?.ItemInstance == null) continue;

#if IL2CPP
                if (slot.ItemInstance.TryCast<CashInstance>() is CashInstance cashVal)
#elif MONO
                if (slot.ItemInstance is CashInstance cashVal)
#endif
                {
                    cash = cashVal;
                    continue;
                }

                ProductItemInstance match = MatchProduct(slot.ItemInstance, drug);
                if (match == null)
                {
                    // Non-cash item that isn't this stash's drug. Cash already hit
                    // `continue` above, so a cash-only stash never sets this.
                    wrongDrug = true;
                    continue;
                }

                product = match;
                if (match.Definition != null) distinctMixes.Add(match.Definition.ID);
            }

            int units = product != null ? StashManager.GetTotalUnits(product) : 0;
            float balance = cash != null ? cash.Balance : 0f;

            string headline;
            bool ok = false;
            if (wrongDrug)
                headline = $"Provided product is not a {drug} mix";
            else if (product == null && cash == null)
                headline = $"Add {reqUnits} units of a mix and {MoneyManager.FormatAmount(reqCash)}";
            else if (product == null)
                headline = $"Add {reqUnits} units of a mix";
            else if (distinctMixes.Count > 1)
                headline = "Too many products - only one mix at a time";
            else if (IsDiscovered(product.Definition?.ID, drug))
                headline = $"{product.Definition.Name} has already been synthesized";
            else if (units < reqUnits)
                headline = "Not enough product";
            else if (balance < reqCash)
                headline = "Not enough cash";
            else if (product.ID == "ogkush" || product.ID == "granddaddypurple" || product.ID == "sourdiesel" || product.ID == "greencrack"
                  || product.ID == "shroom" || product.ID == "cocaine" || product.ID == "meth")
                headline = "Base strains can't be synthesized - add a mix";
            else
            {
                headline = "Ready - close the stash to begin synthesis";
                ok = true;
            }

            string unitsLine = Colorize($"Units: {units} / {reqUnits}", units >= reqUnits);
            string cashLine = Colorize($"Cash: {MoneyManager.FormatAmount(balance)} / {MoneyManager.FormatAmount(reqCash)}", balance >= reqCash);

            return $"{Colorize(headline, ok)}\n{unitsLine}    {cashLine}";
        }

        private static string Colorize(string text, bool ok)
        {
            return $"<color={(ok ? OkColor : WarnColor)}>{text}</color>";
        }

        private static ProductItemInstance MatchProduct(ItemInstance item, EDrugType drug)
        {
            switch (drug)
            {
#if IL2CPP
                case EDrugType.Marijuana: return item.TryCast<WeedInstance>();
                case EDrugType.Shrooms: return item.TryCast<ShroomInstance>();
                case EDrugType.Cocaine: return item.TryCast<CocaineInstance>();
                case EDrugType.Methamphetamine: return item.TryCast<MethInstance>();
#elif MONO
                case EDrugType.Marijuana: return item as WeedInstance;
                case EDrugType.Shrooms: return item as ShroomInstance;
                case EDrugType.Cocaine: return item as CocaineInstance;
                case EDrugType.Methamphetamine: return item as MethInstance;
#endif
                default: return null;
            }
        }

        private static bool IsDiscovered(string id, EDrugType drug)
        {
            if (string.IsNullOrEmpty(id)) return false;
            switch (drug)
            {
                case EDrugType.Marijuana: return CustomSeedsManager.DiscoveredSeeds.ContainsKey(id);
                case EDrugType.Shrooms: return CustomShroomsManager.DiscoveredShrooms.ContainsKey(id);
                case EDrugType.Cocaine: return CustomCocaSeedsManager.DiscoveredCocaSeeds.ContainsKey(id);
                case EDrugType.Methamphetamine: return CustomPseudoManager.DiscoveredPseudoSeeds.ContainsKey(id);
                default: return false;
            }
        }
    }
}

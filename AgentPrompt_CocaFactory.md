# Agent Task: Custom Coca Seed Factory (Phase 1 — Core Factory + Testing)

## Scope
This is **Phase 1 only**. Do NOT implement saving/loading, networking, or a quest system. The goal is:
1. A working `CocaFactory` that clones game prefabs to produce a custom `SeedDefinition` backed by a `CocaPlant`
2. Marker ScriptableObject subclasses (`CustomCocaLeafDefinition`, `CustomCocaineBaseDefinition`) for type-safe Harmony patch identification
3. Two Harmony patches: Cauldron ingredient filter bypass + Cauldron output redirect via cook-start provenance tracking
4. A minimal `CustomCocaSeedsManager` that wires Salvador's shop
5. A `"Synthesize Coca"` sendable message on Salvador's conversation that directly triggers coca seed creation for testing
6. A new developer console command `"finishcooking"` that instantly completes all active `Cauldron` and `LabOven` cook operations
7. Place the result seed in a `DeadDrop` for the player to retrieve

Read `Robots.md` for full project context before starting. Study `Seeds/SeedFactory.cs` and `Seeds/SyringeFactory.cs` thoroughly — `CocaFactory` mirrors their structure and patterns.

---

## Known IDs (Already Discovered)
```
Base coca seed ID:     "cocaseed"  ? template SeedDefinition to clone from
Base coca leaf ID: "cocaleaf"          ? QualityItemDefinition, input to Cauldron
Base cocaine base ID:  "cocainebase"       ? QualityItemDefinition, output of Cauldron / input to LabOven
Test cocaine mix ID:   "cocaine"        ? use this CocaineDefinition as the target mix for testing
Salvador NPC ID:       "salvador"     ? the supplier NPC (analogous to Albert/Phil)
```
Verify all IDs by logging `Registry.GetItem(id)` at startup before using them — the game may use different casing or name formats.

---

## Architecture Overview: Why Coca Seeds Are Different

Unlike weed seeds (where `WeedPlant.BranchPrefab.Product` directly encodes the final mix), the cocaine chain has **three hardcoded intermediate steps**:

```
CocaSeed (SeedDefinition, cloned per mix)
  ??? PlantPrefab ? CocaPlant clone
        ??? Harvestable.Product ? CustomCocaLeafDefinition (cloned per mix, marker subclass)
          ??? [Player carries leaf to Cauldron]
         ??? [PATCH 1] Cauldron filter bypass (accepts CustomCocaLeafDefinition)
       ??? [PATCH 2] Cauldron output redirect (outputs CustomCocaineBaseDefinition)
          ??? CustomCocaineBaseDefinition (cloned per mix, marker subclass)
        ??? StationItem.CookableModule.Product ? target CocaineDefinition (unmodified, already in Registry)
         ??? [Player puts base in LabOven]
     ??? LabOven reads CookableModule.Product ? outputs correct CocaineDefinition ?
```

The LabOven requires **zero patching** because `OvenCookOperation.ProductID` is set from `CookableModule.Product` on whatever ingredient is placed in the oven. As long as `CustomCocaineBaseDefinition.StationItem.GetModule<CookableModule>().Product` points to the right `CocaineDefinition`, the oven handles it automatically.

---

## New Files to Create

### 1. `Seeds/CocaFactory.cs`

Mirror the structure of `Seeds/SeedFactory.cs` exactly — inactive root `GameObject`, `DontDestroyOnLoad`, `DeleteChildren()`.

#### Fields to cache from `"cocaseed"`:
```csharp
private Transform rootGameObject;
private SeedDefinition baseSeedDefinition;
private CocaPlant baseCocaPlantPrefab;   // baseSeedDefinition.PlantPrefab cast to CocaPlant
private PlantHarvestable baseHarvestable;    // baseCocaPlantPrefab.Harvestable
private FunctionalSeed baseFunctionalSeedPrefab;
private Equippable_Seed baseEquippableSeedPrefab;
private AvatarEquippable baseAvatarEquippablePrefab;
private StoredItem baseStoredItem;

// Cached base definitions for cloning
private QualityItemDefinition baseCocaLeafDefinition;   // Registry.GetItem("cocaleaf")
private QualityItemDefinition baseCocaineBaseDefinition; // Registry.GetItem("cocainebase")
```

#### Constructor:
```csharp
public CocaFactory(SeedDefinition baseSeed, QualityItemDefinition baseCocaLeaf, QualityItemDefinition baseCocaineBase)
{
    baseSeedDefinition = baseSeed;
    baseCocaLeafDefinition = baseCocaLeaf;
    baseCocaineBaseDefinition = baseCocaineBase;

    // Cast PlantPrefab to CocaPlant
    // IL2CPP:  baseSeed.PlantPrefab.TryCast<CocaPlant>()
    // MONO:    (CocaPlant)baseSeed.PlantPrefab
    baseCocaPlantPrefab = /* cast */;
if (baseCocaPlantPrefab == null)
        Utility.Error("CocaFactory: Failed to cast PlantPrefab to CocaPlant.");

    baseHarvestable = baseCocaPlantPrefab?.Harvestable;
    baseFunctionalSeedPrefab = baseSeed.FunctionSeedPrefab;
    baseStoredItem = baseSeed.StoredItem;

    // Cast Equippable to Equippable_Seed — same pattern as SeedFactory
    // IL2CPP:  baseSeed.Equippable.TryCast<Equippable_Seed>()
    // MONO:    (Equippable_Seed)baseSeed.Equippable
    baseEquippableSeedPrefab = /* cast */;
    baseAvatarEquippablePrefab = baseEquippableSeedPrefab?.AvatarEquippable;

    GameObject go = new GameObject($"{baseSeedDefinition.ID}_CustomCocaSeeds");
    go.SetActive(false);
    GameObject.DontDestroyOnLoad(go);
    rootGameObject = go.transform;
}
```

#### `CreateCocaSeedDefinition(CocaineDefinition cocaineDef)`:
Main public method. Returns a fully configured `SeedDefinition`.

```
Steps:
1. CloneCustomCocaineBase(cocaineDef) ? customBase (CustomCocaineBaseDefinition)
2. CloneCustomCocaLeaf(customBase) ? customLeaf (CustomCocaLeafDefinition)
3. CloneCocaPlant(customLeaf) ? newPlant (CocaPlant)
4. Clone baseSeedDefinition ScriptableObject ? newSeed
5. Set newSeed.ID = "{cocaineDef.ID}_customcocaseed"
6. Set newSeed.Name = "{cocaineDef.name} Coca Seed"
7. Set newSeed.PlantPrefab = newPlant
8. Set newSeed.PlantPrefab.SeedDefinition = newSeed
9. Set newSeed.FunctionSeedPrefab = CloneFunctionalSeedPrefab(newSeed.ID)
10. Set newSeed.Equippable = CloneEquippableSeedPrefab(newSeed)
11. Set newSeed.StoredItem = CloneStoredItem(newSeed.ID)
12. Register customLeaf and customBase in Registry
13. Return newSeed
```

#### `CloneCustomCocaineBase(CocaineDefinition cocaineDef)`:
```csharp
private CustomCocaineBaseDefinition CloneCustomCocaineBase(CocaineDefinition cocaineDef)
{
    // ScriptableObject.CreateInstance instead of Instantiate — we use a marker subclass
  CustomCocaineBaseDefinition clone = ScriptableObject.CreateInstance<CustomCocaineBaseDefinition>();

    // Copy all fields from baseCocaineBaseDefinition:
    // Name, ID, Description, Icon, Category, StackLimit, legalStatus
    // StationItem (reference copy — same prefab, see note below)
    // BasePurchasePrice, StoredItem, etc.
    // Copy fields via direct assignment from baseCocaineBaseDefinition

    clone.ID = $"{cocaineDef.ID}_customcocainebase";
  clone.name = clone.ID;
    clone.Name = $"{cocaineDef.name} Base";
    clone.TargetCocaineMixId = cocaineDef.ID; // stored on the marker class

    // IMPORTANT: The StationItem prefab has a CookableModule whose Product field
    // points to the game's default cocaine output. We need a cloned StationItem
    // so we can change CookableModule.Product to cocaineDef without affecting
    // other cauldron outputs. Clone it parented under rootGameObject.
    // Then: clonedStationItem.GetModule<CookableModule>().Product = cocaineDef;
    clone.StationItem = CloneCocaineBaseStationItem(cocaineDef);

 return clone;
}
```

**Note on StationItem cloning:** `CookableModule.Product` is what the LabOven reads to determine its output. Cloning the `StationItem` prefab and setting `CookableModule.Product = cocaineDef` (the actual `CocaineDefinition` already in the Registry) is the key step that makes the LabOven output the right product with zero oven patching. Log the `StationItem` fields on `baseCocaineBaseDefinition` to understand all values that need copying.

#### `CloneCustomCocaLeaf(CustomCocaineBaseDefinition customBase)`:
```csharp
private CustomCocaLeafDefinition CloneCustomCocaLeaf(CustomCocaineBaseDefinition customBase)
{
    CustomCocaLeafDefinition clone = ScriptableObject.CreateInstance<CustomCocaLeafDefinition>();

    // Copy all fields from baseCocaLeafDefinition:
    // Name, ID, Description, Icon, Category, StackLimit, legalStatus
    // StationItem reference (coca leaf's station item — controls Cauldron cook visuals/time)
    // The StationItem here does NOT need cloning: CauldronOutput is overridden by our patch,
    // and we only need the CookableModule's CookTime and visual properties, which are shared.

    clone.ID = $"{customBase.TargetCocaineMixId}_customcocaleaf";
    clone.name = clone.ID;
    clone.Name = $"Coca Leaf ({customBase.Name})";
    clone.LinkedBaseId = customBase.ID; // stored on the marker class for patch lookup

    return clone;
}
```

#### `CloneCocaPlant(CustomCocaLeafDefinition customLeaf)`:
```csharp
private CocaPlant CloneCocaPlant(CustomCocaLeafDefinition customLeaf)
{
    CocaPlant newPlant = UnityEngine.Object.Instantiate(baseCocaPlantPrefab, rootGameObject);
    // Clone the harvestable and point its Product to our custom leaf
    PlantHarvestable newHarvestable = UnityEngine.Object.Instantiate(baseHarvestable, rootGameObject);
    newHarvestable.Product = customLeaf;
  newPlant.Harvestable = newHarvestable;
    // Update all existing PlantHarvestable children on the plant prefab too
    foreach (PlantHarvestable pH in newPlant.GetComponentsInChildren<PlantHarvestable>())
    {
   pH.Product = customLeaf;
    }
    return newPlant;
}
```

`CloneFunctionalSeedPrefab`, `CloneEquippableSeedPrefab`, `CloneAvatarEquippablePrefab`, `CloneStoredItem` — implement identically to `SeedFactory`. No label gradient needed (coca seeds have no mix-color appearance), but keep the same structure for consistency. Use `"cocaseed"` as the visual template.

#### `DeleteChildren()`:
```csharp
public void DeleteChildren()
{
    rootGameObject.DeleteChildren(true, false);
}
```

---

### 2. `Seeds/CustomCocaLeafDefinition.cs`

```csharp
// Marker subclass — enables type-safe identification in Harmony patches
// without string-matching IDs
[Serializable]
public class CustomCocaLeafDefinition : QualityItemDefinition
{
    // ID of the corresponding CustomCocaineBaseDefinition
    // Set after creation: clone.LinkedBaseId = customBase.ID
    public string LinkedBaseId;
}
```

### 3. `Seeds/CustomCocaineBaseDefinition.cs`

```csharp
[Serializable]
public class CustomCocaineBaseDefinition : QualityItemDefinition
{
    // ID of the target CocaineDefinition this base should produce in the LabOven
    public string TargetCocaineMixId;
}
```

Both classes must be under `#if IL2CPP / #elif MONO` guards for their base class namespace. Under IL2CPP, `QualityItemDefinition` is `Il2CppScheduleOne.ItemFramework.QualityItemDefinition`. Under IL2CPP, also add `[RegisterTypeInIl2Cpp]` attribute.

---

### 4. `Managers/CustomCocaSeedsManager.cs`

Minimal for Phase 1 — Salvador's shop wiring, factory, test creation coroutine.

```csharp
public static class CustomCocaSeedsManager
{
    public const string BASE_SEED_ID = "cocaseed";
    public const string BASE_LEAF_ID = "cocaleaf";
    public const string BASE_BASE_ID = "cocainebase";

    public static CocaFactory factory;
    public static Dictionary<string, UnicornSeedData> DiscoveredCocaSeeds = new();

    public static ShopInterface SalvadorShop = null;
    public static Salvador salvador = null;

    public static void Initialize()
    {
        salvador = GameObject.FindObjectOfType<Salvador>();
        if (salvador != null)
        {
SalvadorShop = salvador.Shop; // Supplier base exposes Shop

            if (SalvadorShop == null)
      Utility.Error("CustomCocaSeedsManager: Salvador's shop is null!");

            if (salvador.MSGConversation != null)
          ConversationManager.RegisterConversation("Salvador", salvador.MSGConversation);

            SetupSalvadorConversation();
        }
 else
 {
   Utility.Error("CustomCocaSeedsManager: Could not find Salvador.");
        }
    }

    private static void SetupSalvadorConversation()
    {
        MSGConversation convo = ConversationManager.GetConversation("Salvador");
        if (convo != null)
    {
            SendableMessage sendable = convo.CreateSendableMessage("Synthesize Coca");
        sendable.onSent += (Action)OnCocaSynthesisRequested;
     }
    }

    public static void OnCocaSynthesisRequested()
    {
     // TESTING: hardcoded to "cocaine" mix
        CocaineDefinition cocaineDef = Registry.GetItem<CocaineDefinition>("cocaine");
        if (cocaineDef == null)
        {
     Utility.Error("CustomCocaSeedsManager: Could not find 'cocaine' CocaineDefinition. Log all Registry IDs to find correct ID.");
          return;
        }
        MelonCoroutines.Start(CreateCocaSeed(cocaineDef));
    }

public static IEnumerator CreateCocaSeed(CocaineDefinition cocaineDef)
    {
     yield return new WaitForSeconds(5f);

        if (factory == null)
        {
      Utility.Error("CocaFactory is null!");
            yield break;
      }

        SeedDefinition newSeed = factory.CreateCocaSeedDefinition(cocaineDef);
 if (newSeed == null)
        {
        Utility.Error("Failed to create custom coca seed definition.");
      yield break;
        }

        Singleton<Registry>.Instance.AddToRegistry(newSeed);
        Utility.Log($"Created custom coca seed: {newSeed.ID}");

        UnicornSeedData newData = new UnicornSeedData
        {
   seedId = newSeed.ID,
 mixId = cocaineDef.ID,
 drugType = EDrugType.Cocaine, // verify this enum member exists — log EDrugType values at startup
   price = 100f,
        };
        DiscoveredCocaSeeds.Add(newData.mixId, newData);

        DeadDrop randomDrop = DeadDrop.GetRandomEmptyDrop(Player.Local.transform.position);
     if (randomDrop != null && InstanceFinder.IsServer)
 {
       ItemInstance defaultInstance = newSeed.GetDefaultInstance();
            defaultInstance.SetQuantity(3);
        randomDrop.Storage.InsertItem(defaultInstance, true);
          string guidString = GUIDManager.GenerateUniqueGUID().ToString();
  NetworkSingleton<QuestManager>.Instance.CreateDeaddropCollectionQuest(null, randomDrop.GUID.ToString(), guidString);
     ConversationManager.SendMessage("Salvador", $"{cocaineDef.name} coca seed synthesized and placed in a dead drop.");
        }
   else
        {
      Utility.Error("No available dead drop for coca seed placement.");
        }
    }

    public static void ClearAll()
    {
 DiscoveredCocaSeeds.Clear();
SalvadorShop = null;
        salvador = null;
   if (factory != null) factory.DeleteChildren();
    }
}
```

**Note on `EDrugType.Cocaine`:** At startup, log `Enum.GetNames(typeof(EDrugType))` to verify the cocaine member name. It may be `Cocaine`, `Coke`, `Meth` (if cocaine is grouped with meth in the enum), or another value.

**Note on `CocaineDefinition`:** Verify this type exists in the decompiled source. It may instead be a plain `ProductDefinition` subclass or share a type with other drugs. Log `Registry.GetItem("cocaine")?.GetType().Name` to confirm.

---

### 5. `Patches/CauldronPatches.cs`

Two patches, one file.

#### Patch 1 — Cauldron Ingredient Filter Bypass

The Cauldron's `Awake`/`dll()` method hard-filters all ingredient slots to ID `"cocaleaf"` via `ItemFilter_ID`. Our custom leaf IDs won't pass that filter.

Find the correct Harmony target by looking at where `AddFilter(new ItemFilter_ID(...))` is called on `IngredientSlots`. It is inside the private `dll()` method (decompiled Awake body). The patch target may need to be on a method called after `dll()` completes, or directly on the filter's `AllowsItem` / `ItemPassesFilter` method.

**Recommended approach — patch `ItemFilter_ID`'s filter check method directly** (more robust than patching `dll()`):

```csharp
// Find the actual method name by searching for "AllowsItem", "PassesFilter", or similar
// on ItemFilter_ID in the decompiled source. Log its name at startup if unsure.

[HarmonyPatch(typeof(ItemFilter_ID), "ACTUAL_METHOD_NAME")]
static class Patch_ItemFilter_ID_AllowCustomCocaLeaf
{
    static bool Prefix(ItemFilter_ID __instance, ItemInstance item, ref bool __result)
    {
        if (item?.Definition is CustomCocaLeafDefinition)
        {
 __result = true;
  return false; // skip original
        }
        return true;
    }
}
```

#### Patch 2 — Cauldron Output Redirect

The Cauldron always outputs its serialized `CocaineBaseDefinition` field regardless of what leaf was used. We need to intercept at two points:

**2a — Cook-start tracker** (Postfix on the `RpcLogic` for `SendCookOperation`):

```csharp
// Key: Cauldron instance ? CustomCocaineBaseDefinition to output when cooking finishes
static readonly Dictionary<Cauldron, CustomCocaineBaseDefinition> _pendingOutput
    = new Dictionary<Cauldron, CustomCocaineBaseDefinition>();

// Postfix on Cauldron.RpcLogic___SendCookOperation_3536682170
// (this fires on the server when the player clicks Start — ingredients are still in slots here)
[HarmonyPatch(typeof(Cauldron), "RpcLogic___SendCookOperation_3536682170")]
static class Patch_Cauldron_SendCookOperation
{
    static void Postfix(Cauldron __instance)
    {
        foreach (ItemSlot slot in __instance.IngredientSlots)
        {
            if (slot.ItemInstance?.Definition is CustomCocaLeafDefinition leaf)
            {
         // Resolve the linked base definition from the Registry
   CustomCocaineBaseDefinition customBase =
   Registry.GetItem(leaf.LinkedBaseId) as CustomCocaineBaseDefinition;
        if (customBase != null)
  {
             _pendingOutput[__instance] = customBase;
     }
      break;
}
     }
    }
}
```

**2b — Output redirect** (Prefix on `RpcLogic___FinishCookOperation_2166136261`):

```csharp
[HarmonyPatch(typeof(Cauldron), "RpcLogic___FinishCookOperation_2166136261")]
static class Patch_Cauldron_FinishCookOperation
{
    static bool Prefix(Cauldron __instance)
    {
        if (!_pendingOutput.TryGetValue(__instance, out CustomCocaineBaseDefinition customBase))
    return true; // vanilla path — no custom leaf was involved

        if (InstanceFinder.IsServer)
        {
            QualityItemInstance output = customBase.GetDefaultInstance(10) as QualityItemInstance;
            output.SetQuality(__instance.InputQuality);
 __instance.OutputSlot.InsertItem(output);
        }

        __instance.CauldronFillable.ResetContents();
        __instance.onCookEnd?.Invoke();
    _pendingOutput.Remove(__instance);
        return false; // skip original
    }
}
```

**Important:** Verify the exact method name `RpcLogic___FinishCookOperation_2166136261` against the decompiled `Cauldron.cs`. The hash suffix may differ. Search for `FinishCookOperation` in the decompiled source to confirm.

---

### 6. `Patches/FinishCookingCommand.cs` — New Console Command

Add a new `ConsoleCommand` subclass that instantly completes all active `Cauldron` and `LabOven` operations. The pattern is identical to `Console.GrowPlants` in `Console.cs`.

The command cannot be added via `Console.Awake` (game code). Instead, **Harmony Postfix `Console.Awake`** to inject it after the base commands are registered:

```csharp
[HarmonyPatch(typeof(Console), "Awake")]
static class Patch_Console_AddFinishCookingCommand
{
    static void Postfix(Console __instance)
    {
        // Use reflection to call the private AddCommand method, mirroring how GrowPlants is registered
        var addCmd = typeof(Console).GetMethod("AddCommand",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        addCmd?.Invoke(__instance, new object[] { new FinishCookingCommand() });
    }
}

public class FinishCookingCommand : Console.ConsoleCommand
{
    public override string CommandWord => "finishcooking";
    public override string CommandDescription => "Instantly completes all active Cauldron and LabOven cook operations.";
    public override string ExampleUsage => "finishcooking";

    public override void Execute(List<string> args)
    {
        int cauldronCount = 0;
        int ovenCount = 0;

        // Complete all Cauldrons
        foreach (Cauldron cauldron in GameObject.FindObjectsOfType<Cauldron>())
        {
      if (cauldron.RemainingCookTime > 0)
 {
     // Pass a large number to simulate many minutes passing — enough to finish any cook
       cauldron.RpcLogic___FinishCookOperation_2166136261();
             // OR: set RemainingCookTime to 0 and trigger finish manually
       // Prefer calling RpcLogic directly since it handles item output and events
    cauldronCount++;
            }
        }

        // Complete all LabOvens
     foreach (LabOven oven in GameObject.FindObjectsOfType<LabOven>())
      {
         if (oven.CurrentOperation != null && !oven.CurrentOperation.IsComplete())
    {
        // Advance cook progress to completion
                int remaining = oven.CurrentOperation.GetCookDuration() - oven.CurrentOperation.CookProgress;
       oven.CurrentOperation.UpdateCookProgress(remaining + 1);
                // Trigger the ding and appearance update that OnTimePass would normally handle
      oven.UpdateOvenAppearance(); // this may be private — use reflection or find alternate
       cauldronCount++;
            ovenCount++;
   }
        }

        Console.Log($"Finished {cauldronCount} cauldron(s) and {ovenCount} lab oven(s).", null);
    }
}
```

**Implementation notes for the command:**
- `Cauldron.RpcLogic___FinishCookOperation_2166136261` is `public` in the decompiled source — callable directly.
- `LabOven.UpdateOvenAppearance` and `LabOven.DingSound.Play()` may be private. Check access. If private, use `typeof(LabOven).GetMethod("UpdateOvenAppearance", BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(oven, null)`.
- Alternatively for `LabOven`, calling `oven.OnTimePass(remaining + 1)` via reflection is cleaner since it handles the ding, liquid update, and appearance in one call. Check if `OnTimePass` is private.
- Under IL2CPP, method name mangling may affect reflection. Prefer direct calls where the method is accessible.

---

## Files to Modify

### 7. `Core.cs`

**`OnSceneWasLoaded("Main")`** — initialize the factory when base definitions are available:
```csharp
var baseCocaSeed = Registry.GetItem<SeedDefinition>(CustomCocaSeedsManager.BASE_SEED_ID);
var baseCocaLeaf = Registry.GetItem<QualityItemDefinition>(CustomCocaSeedsManager.BASE_LEAF_ID);
var baseCocaBase = Registry.GetItem<QualityItemDefinition>(CustomCocaSeedsManager.BASE_BASE_ID);

if (CustomCocaSeedsManager.factory == null
&& baseCocaSeed != null && baseCocaLeaf != null && baseCocaBase != null)
{
  CustomCocaSeedsManager.factory = new CocaFactory(baseCocaSeed, baseCocaLeaf, baseCocaBase);
    Utility.Log("CocaFactory initialized.");
}
else if (CustomCocaSeedsManager.factory == null)
{
  Utility.Error($"CocaFactory init failed. cocaseed={baseCocaSeed != null}, cocaleaf={baseCocaLeaf != null}, cocainebase={baseCocaBase != null}");
}
```

**`InitMod()`** — add after existing seed/shroom initialization:
```csharp
CustomCocaSeedsManager.Initialize();
```

**`OnSceneWasLoaded()` non-main branch** — add alongside other `ClearAll()` calls:
```csharp
CustomCocaSeedsManager.ClearAll();
```

Add required `#if IL2CPP / #elif MONO` using directives for `Salvador`, `CocaPlant`, `CocaineDefinition`, `Cauldron`, `LabOven` to `Core.cs` and all new files.

---

## Dual-Compile Requirements

Every new file needs `#if IL2CPP / #elif MONO` blocks. Key type namespaces:

| Type | IL2CPP namespace | MONO namespace |
|---|---|---|
| `CocaPlant` | `Il2CppScheduleOne.Growing` | `ScheduleOne.Growing` |
| `PlantHarvestable` | `Il2CppScheduleOne.Growing` | `ScheduleOne.Growing` |
| `QualityItemDefinition` | `Il2CppScheduleOne.ItemFramework` | `ScheduleOne.ItemFramework` |
| `QualityItemInstance` | `Il2CppScheduleOne.ItemFramework` | `ScheduleOne.ItemFramework` |
| `SeedDefinition` | `Il2CppScheduleOne.ItemFramework` | `ScheduleOne.ItemFramework` |
| `Cauldron` | `Il2CppScheduleOne.ObjectScripts` | `ScheduleOne.ObjectScripts` |
| `LabOven` | `Il2CppScheduleOne.ObjectScripts` | `ScheduleOne.ObjectScripts` |
| `OvenCookOperation` | `Il2CppScheduleOne.ObjectScripts` | `ScheduleOne.ObjectScripts` |
| `Salvador` | `Il2CppScheduleOne.NPCs.CharacterClasses` | `ScheduleOne.NPCs.CharacterClasses` |
| `CocaineDefinition` | `Il2CppScheduleOne.Product` | `ScheduleOne.Product` |
| `ItemFilter_ID` | `Il2CppScheduleOne.ItemFramework` | `ScheduleOne.ItemFramework` |
| `Console` | `Il2CppScheduleOne` | `ScheduleOne` |

For `RegisterTypeInIl2Cpp` on marker classes, add the attribute only inside `#if IL2CPP` blocks:
```csharp
#if IL2CPP
[RegisterTypeInIl2Cpp]
#endif
[Serializable]
public class CustomCocaLeafDefinition : QualityItemDefinition { ... }
```

---

## Testing Instructions

1. Load a save where Salvador is unlocked.
2. Open Salvador's messages, send **"Synthesize Coca"**.
3. Wait ~5 seconds.
4. Check for a dead drop notification — collect 3 coca seeds.
5. Plant a coca seed in a `Pot` — verify `CocaPlant` grows.
6. Harvest the plant — verify you receive a `CustomCocaLeafDefinition` instance (log `item.Definition.GetType().Name` in a debug patch on `PlantHarvestable.Harvest`).
7. Put the custom coca leaf in a `Cauldron` — verify the ingredient filter accepts it (it should appear in the slot).
8. Start the Cauldron — use `finishcooking` console command to instantly complete.
9. Verify the Cauldron output slot contains a `CustomCocaineBaseDefinition` instance (not the base `cocainebase`).
10. Put the custom cocaine base in a `LabOven` — verify it is accepted (CookableModule should allow it since the ID differs from vanilla only by our Registry lookup).
11. Start the oven — use `finishcooking` to complete.
12. Verify the LabOven output is the correct `CocaineDefinition` matching the test mix ID.

**Debug logging to add during development:**
- Log `Registry.GetItem("cocaseed")?.GetType().Name` to confirm the base seed resolves.
- Log `Registry.GetItem("cocaine")?.GetType().Name` to confirm the target cocaine type name.
- Log `Enum.GetNames(typeof(EDrugType))` to confirm the cocaine `EDrugType` member name.
- Log `baseCocaineBaseDefinition.StationItem?.GetType().Name` and all `CookableModule` fields to understand what needs cloning.
- In `CauldronPatches`, log when `_pendingOutput` is set and when the prefix fires, to trace the provenance tracker.
- Log `ItemFilter_ID` method names via `typeof(ItemFilter_ID).GetMethods()` to find the correct filter method name.

---

## What NOT to Implement in Phase 1
- `DiscoveredCustomMixes.json` loading or saving for coca seeds
- `[NET-JSON-COCA]` or any network broadcasting
- Salvador stash closed handler / validation flow
- Shop or delivery listings for custom coca seeds
- Quest system (`CustomCocaQuest`)
- `SendableMessagePatch` IsValid override for Salvador's message
- `ProductManagerApp` seed indicator for coca seeds

These are all Phase 2.

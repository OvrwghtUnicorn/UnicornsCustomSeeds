# Creating Custom Seeds - Developer Documentation

This document explains the internal processes of the Unicorns Custom Seeds mod, covering how custom weed mixes are synthesized into seeds. The mod loading starts in `Core.cs`.

## Table of Contents
- [Mod Initialization](#mod-initialization)
- [Core Components](#core-components)
- [Custom Seed Creation Flow](#custom-seed-creation-flow)
- [Quest System](#quest-system)
- [Seed Factory Architecture](#seed-factory-architecture)
- [Persistence System](#persistence-system)
- [Configuration](#configuration)
- [Networking](#networking)

## Mod Initialization

The mod initialization begins in `Core.cs` when the game loads.

### OnLateInitializeMelon
```csharp
public override void OnLateInitializeMelon() {
    StashManager.InitializeConfig();
    LoadManager.Instance.onLoadComplete.AddListener((UnityAction)InitMod);
    SaveManager.Instance.onSaveComplete.AddListener((UnityAction)SaveData);
}
```

**Key Steps:**
1. Initialize configuration preferences via `StashManager.InitializeConfig()`
2. Register event listener for when a game loads (`InitMod`)
3. Register event listener for when a game saves (`SaveData`)

### InitMod
```csharp
public void InitMod()
{
    CustomSeedsManager.Initialize();
    StashManager.GetAlbertsStash();
}
```

**Actions Performed:**
- Initialize the CustomSeedsManager to set up shops, quests, and visuals
- Locate and register Albert's supply stash for the drop-off system

## Core Components

### 1. CustomSeedsManager
**Location:** `Managers/CustomSeedsManager.cs`

Manages the entire custom seed lifecycle including:
- Seed creation and registration
- Shop listing generation
- Dead drop placement
- Discovery tracking

### 2. SeedFactory
**Location:** `Seeds/SeedFactory.cs`

Factory pattern implementation for creating seed definitions and their components:
- Clones plant prefabs with custom appearances
- Creates seed vials with custom labels
- Generates equippable seed items

### 3. StashManager
**Location:** `Managers/StashManager.cs`

Handles Albert's supply stash interactions:
- Validates player's weed mix and payment
- Triggers seed synthesis
- Calculates ingredient costs

### 4. SeedQuestManager
**Location:** `Managers/SeedQuestManager.cs`

Manages the quest system for seed synthesis:
- Creates "Drop off the Mix" quests
- Sends phone messages to the player
- Completes quests upon successful synthesis

### 5. ConversationManager
**Location:** `Managers/ConversationManager.cs`

Manages phone conversations with Albert:
- Registers Albert's conversation interface
- Sends welcome messages when unlocked
- Handles message chains

## Custom Seed Creation Flow

### Step 1: Player Initiates Request
1. Player texts Albert "Synthesize Seeds"
2. Albert responds: "Drop the weed mix and cash in my drop box"
3. Quest "Drop off the Mix" is created

### Step 2: Drop-off Validation
When player closes Albert's stash (`StashManager.AlbertsStashClosed()`):

```csharp
if (cashInstance.Balance >= StashCostEntry.Value && total >= StashQtyEntry.Value)
{
    WeedDefinition definition = weedInstance.Definition;
    if (definition != null && !CustomSeedsManager.DiscoveredSeeds.ContainsKey(weedInstance.Definition.ID))
    {
        SeedQuestManager.CompleteQuest();
        SeedQuestManager.SendMessage("I will begin synthesizing the seed");
        CustomSeedsManager.StartSeedCreation(definition);
    }
}
```

**Requirements Checked:**
- Sufficient cash (default: $500)
- Sufficient weed quantity (default: 20 grams)
- Mix hasn't been synthesized before

### Step 3: Seed Synthesis
After a configurable wait time (default: 30 seconds), the seed is created:

```csharp
public static IEnumerator CreateSeed(WeedDefinition weedDef)
{
    yield return new WaitForSeconds(StashManager.SynthesizeTime.Value);
    
    var newSeed = factory.CreateSeedDefinition(weedDef);
    Singleton<Registry>.Instance.AddToRegistry(newSeed);
    
    float price = StashManager.GetIngredientCost(weedDef);
    UnicornSeedData newSeedData = new UnicornSeedData
    {
        seedId = newSeed.ID,
        weedId = weedDef.ID,
        baseSeedId = BASE_SEED_ID,
        price = price,
    };
    DiscoveredSeeds.Add(newSeed.ID, newSeedData);
}
```

### Step 4: Distribution
The newly created seed is:
1. Added to shop listings
2. Placed in a random dead drop (10 seeds)
3. Added to all pot configurations for growing
4. Broadcast to networked players (multiplayer support)

## Quest System

### CustomSeedQuest
A quest is created each time the player requests seed synthesis.

**Quest Properties:**
- **Title:** "Drop off the Mix"
- **Description:** Take {quantity}x of weed mix and ${cost} to Albert's stash
- **Entry:** "Give Albert {quantity}x of a Weed Mix and ${cost}"
- **POI:** Points to Albert's stash location

**Quest Flow:**
1. Player sends "Synthesize Seeds" text to Albert
2. Quest is created with POI marker
3. Player deposits items in Albert's stash
4. Quest completes when validation passes
5. Synthesis begins after quest completion

## Seed Factory Architecture

### CreateSeedDefinition Process

The `SeedFactory.CreateSeedDefinition()` method creates a complete seed definition:

```csharp
public SeedDefinition CreateSeedDefinition(WeedDefinition weedDef)
{
    SeedDefinition newSeedDef = UnityEngine.Object.Instantiate(baseSeedDefinition);
    
    newSeedDef.ID = $"{weedDef.ID}_customseeddefinition";
    newSeedDef.Name = $"{weedDef.name} Seed";
    
    // Clone all components
    newSeedDef.Equippable = CloneEquippableSeedPrefab(newSeedDef, weedAppearance);
    newSeedDef.PlantPrefab = ClonePlantPrefab(weedDef);
    newSeedDef.FunctionSeedPrefab = CloneFunctionalSeedPrefab(newSeedDef.ID);
    newSeedDef.StoredItem = CloneStoredItem(newSeedDef.ID);
    
    // Generate custom icon
    Sprite newIcon = SeedVisualsManager.GenerateSpriteWithGradient(
        weedAppearance.MainColor, 
        weedAppearance.SecondaryColor
    );
    newSeedDef.Icon = newIcon;
    
    return newSeedDef;
}
```

### Component Cloning

Each seed requires multiple Unity prefab clones:

1. **PlantPrefab:** The growing plant with custom colors
2. **Equippable:** The seed vial the player holds
3. **FunctionalSeed:** The seed object with physics
4. **StoredItem:** The inventory representation
5. **AvatarEquippable:** The seed appearance on player avatar

### Visual Customization

Colors are extracted from the weed definition's appearance settings:
- **Main Color:** Primary bud color
- **Secondary Color:** Secondary bud color
- **Leaf Color:** Leaf material color
- **Stem Color:** Stem material color

The `SeedVisualsManager` generates:
- Gradient seed icons based on bud colors
- Custom vial labels with animated materials
- Appearance mappings for plant growth stages

## Persistence System

### Saving Custom Seeds

When the player saves their game:

```csharp
public void SaveData()
{
    string saveFolder = Singleton<LoadManager>.Instance.LoadedGameFolderPath;
    string filePath = Path.Combine(saveFolder, "DiscoveredCustomSeeds.json");
    List<UnicornSeedData> seedsIl2cpp = CustomSeedsManager.DiscoveredSeeds.Values.ToList();
    
    string json = JsonConvert.SerializeObject(seedsIl2cpp, Formatting.Indented);
    File.WriteAllText(filePath, json);
}
```

**Saved Data (UnicornSeedData):**
```json
{
  "seedId": "purplehaze_customseeddefinition",
  "weedId": "purplehaze",
  "baseSeedId": "ogkushseed",
  "price": 125.5
}
```

### Loading Custom Seeds

On game load, `CustomSeedsManager.Initialize()`:
1. Reads `DiscoveredCustomSeeds.json` from save folder
2. Recreates SeedDefinitions via `SeedDefinitionLoader()`
3. Adds seeds to shop listings
4. Restores all visual customizations

## Configuration

Configurable via MelonLoader preferences or the [Mods App by k0Mods](https://thunderstore.io/c/schedule-i/p/k0Mods/ModsApp/):

```csharp
public static void InitializeConfig()
{
    ConfigCategory = MelonPreferences.CreateCategory("Unicorns Custom Seeds");
    StashCostEntry = ConfigCategory.CreateEntry("StashCostRequirement", 500, 
        "Stash Cost Requirement",
        "The price that Albert charges to synthesize seeds");
    StashQtyEntry = ConfigCategory.CreateEntry("StashQtyRequirement", 20, 
        "Stash Quantity Requirement", 
        "The quantity of weed that needs to be provided of a certain mix");
    SynthesizeTime = ConfigCategory.CreateEntry("SynthesizeTime", 30, 
        "Synthesize Time", 
        "Time in seconds that it will take for Albert to synthesize a seed");
}
```

**Default Values:**
- **Stash Cost:** $500
- **Stash Quantity:** 20 grams
- **Synthesize Time:** 30 seconds

## Networking

The mod includes multiplayer support via `BroadcastCustomSeed()`:

```csharp
public static void BroadcastCustomSeed(UnicornSeedData seed)
{
    ProductManager prodManager = NetworkSingleton<ProductManager>.Instance;
    string json = JsonConvert.SerializeObject(seed);
    string payload = "[NET-JSON]" + json;
    
    prodManager.CreateWeed_Server(payload, BASE_SEED_ID, 
        EDrugType.Marijuana, props, appearance);
}
```

This broadcasts the custom seed to all connected players, allowing them to:
- See the new seed in shops
- Find it in dead drops
- Grow plants from the custom seed

## Scene Management

### OnSceneWasLoaded
```csharp
public override void OnSceneWasLoaded(int buildIndex, string sceneName)
{
    var baseSeed = Registry.GetItem<SeedDefinition>("ogkushseed");
    if (CustomSeedsManager.factory == null && baseSeed != null)
    {
        CustomSeedsManager.factory = new SeedFactory(baseSeed);
    }
    
    if (sceneName.ToLower() != "main")
    {
        CustomSeedsManager.ClearAll();
    }
}
```

**Scene Loading Logic:**
- Ensures `SeedFactory` is initialized with base seed template
- Clears all custom seed data when returning to main menu
- Prevents data overlap between different save files

## Technical Notes

### IL2CPP vs MONO Compatibility

The mod supports both IL2CPP (main branch) and MONO (alternate branch) builds using preprocessor directives:

```csharp
#if IL2CPP
    if (listing.Item.TryCast<SeedDefinition>() is SeedDefinition seed)
#elif MONO
    if (listing.Item is SeedDefinition seed)
#endif
```

### Base Seed Template

All custom seeds are based on the "ogkushseed" definition:
```csharp
public const string BASE_SEED_ID = "ogkushseed";
```

This ensures compatibility with the base game's systems while allowing complete customization of:
- Weed properties (THC, flavor, effects)
- Visual appearance (colors, materials)
- Market pricing (based on ingredient costs)

## Developer Workflow

### Adding New Custom Seeds Programmatically

To create a custom seed programmatically:

```csharp
// 1. Get or create a WeedDefinition
WeedDefinition myWeed = Registry.GetItem<WeedDefinition>("my_weed_id");

// 2. Trigger seed creation
CustomSeedsManager.StartSeedCreation(myWeed);

// The system will:
// - Wait for synthesis time
// - Create all prefabs and definitions
// - Add to shop and registry
// - Place in dead drop
// - Save to persistence file
```

### Extending the System

To extend the custom seeds system:

1. **Add New Components:** Extend `SeedFactory` clone methods
2. **Custom Pricing:** Modify `StashManager.GetIngredientCost()`
3. **Alternative Templates:** Change `BASE_SEED_ID` or add multiple templates
4. **Additional Quests:** Create new quest types in `SeedQuests/`
5. **Enhanced Visuals:** Extend `SeedVisualsManager` shader/material system

## Troubleshooting

### Common Issues

**Seed Factory is Null:**
- Ensure "ogkushseed" exists in Registry
- Check that scene has loaded properly
- Verify `OnSceneWasLoaded` executed

**Seeds Not Persisting:**
- Check save folder path is valid
- Verify JSON serialization succeeds
- Ensure `DiscoveredSeeds` dictionary populated

**Albert's Stash Not Found:**
- Verify scene contains Albert's character
- Check GameObject name contains "albert"
- Ensure `StashManager.GetAlbertsStash()` called after scene load

## License & Credits

**Mod Name:** Unicorns Custom Seeds  
**Author:** OverweightUnicorn  
**Version:** 1.0.2  
**Company:** UnicornsCanMod  

For bug reports and support, contact the author on [Nexus](https://next.nexusmods.com/profile/OverweightUnicorn).

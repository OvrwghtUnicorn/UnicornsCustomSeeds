# Version 2.0.0
- Custom seeds are no longer weed only: synthesize custom shroom syringes, coca seeds and pseudo (meth) from your discovered mixes
- New supplier hooks for each drug: Albert (weed), Phil (shrooms), Salvador (coca) and Shirley (pseudo)
- Custom seed quests load and save for the new drug types
- Albert's chat option is now "Synthesize Weed" to match the other suppliers
- Supplier stashes show a status line while open: units and cash against the requirement, plus warnings for a wrong drug type, more than one mix, a base strain, or a mix that has already been synthesized
- Packaged product counts correctly in every stash (baggie 1, jar 5, brick 20) and the right number of packages is taken
- Fixed supplier stashes sometimes not reacting when closed after loading a save
- Botanists can use custom seeds when a pot's seed is set to "Any", and custom seeds no longer show twice in the pot menu
- Custom seed prices are calculated from the mix's additives plus the base strain's seed price
- Added a custom icon for coca seeds
- Custom icon generation
- Cooking in progress (cauldron and chemistry stations) now survives saving and loading
- Multiplayer: custom items, quests and cooking state now sync to joining clients for all drug types
- Multiplayer: fixed several bugs where items arrived before their definitions and were lost on clients
- Save file structure changed to store the drug type with each mix. Existing 1.x saves are migrated automatically
- Added DebugLogging as a MelonPreferences setting for verbose logging to assist with debugging errors and bugs

# Version 1.1.2
- Updated AssetBundle loader to cache the bundle to prevent bundles not loading on game boot
- [Lumahex](https://www.nexusmods.com/profile/Lumahex/mods) updated the mod to work with version 0.4.6f13
- Updated price calculation for custom seeds to be more fair and balanced
- Updated save file structure to support the addition of future drugs

# Version 1.1.1
- Fixed bug where createseed method crashes when trying to update delivery list

# Version 1.1.0
- Multiplayer!
- Seed icons appear in the product manager app to show what seeds have already been synthesized
- Seed quest can be saved and loaded in both singleplayer and multiplayer
- Albert not taking weed mixes should be fixed
- Can order seeds through the deaddrop menu
- Custom seeds can be delivered by albert

# Version 1.0.1
- Added mod dll's to the zip file. 

# Version 1.0.0
- Albert Hoover will message player about sythesizing seeds if they already have a good enough relationship with him
- When player unlocks shop, Albert Hoover will send a message about synthesizing seeds
- The syntheszie seed option exists in Albert's text chain that will start the synthesize seed quest
- Player can use Alberts Stash to complete the synthesize seed quest
- When quest is complete new seeds appear in Alberts Shop, the pot configuration menu, and are available in a deaddrop
- Botanist's are able to pickup and use custom seeds
- Weed plant color for custom seeds matches the color for the weed mix
- Custom seed vials have different color gradients based on the weed mix
- Custom seed vial objects appear correctly when planting seeds
- Custom seeds are saved with the current save game data and are unique to each save game
- Custom Seeds can be loaded when the players game loads no matter where they are stored(players inventory, storage rack, dropbox, weed pot)
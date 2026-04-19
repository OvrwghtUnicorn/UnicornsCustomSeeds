using UnityEngine;
using UnicornsCustomSeeds.Managers;
using UnicornsCustomSeeds.TemplateUtils;

#if IL2CPP
using Il2CppScheduleOne;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.Growing;
using Il2CppScheduleOne.Product;
using Il2CppScheduleOne.PlayerTasks;
using Il2CppFluffyUnderware.DevTools.Extensions;
#elif MONO
using ScheduleOne;
using ScheduleOne.DevUtilities;
using ScheduleOne.ItemFramework;
using ScheduleOne.Growing;
using ScheduleOne.Product;
using ScheduleOne.PlayerTasks;
using FluffyUnderware.DevTools.Extensions;
#endif

namespace UnicornsCustomSeeds.Seeds
{
    public class SyringeFactory
    {
        private Transform rootGameObject;
        private SporeSyringeDefinition baseSyringeDef;
        private ShroomSpawnDefinition baseSpawnDefinition;
        private ShroomColony baseColonyPrefab;
        private ShroomDefinition baseShroomDef;
        private SpawnChunk baseChunkPrefab;

        public SyringeFactory(SporeSyringeDefinition baseSyringe)
        {
            baseSyringeDef = baseSyringe;
            baseSpawnDefinition = baseSyringe.SpawnDefinition;
            baseColonyPrefab = baseSpawnDefinition.ColonyPrefab;
            baseShroomDef = baseSpawnDefinition.Shroom;
            baseChunkPrefab = baseSpawnDefinition.ChunkPrefab;

            GameObject go = new GameObject($"{baseSyringeDef.ID}_CustomSyringes");
            go.SetActive(false);
            GameObject.DontDestroyOnLoad(go);
            rootGameObject = go.transform;
        }

        public SporeSyringeDefinition CreateSyringeDefinition(ShroomDefinition shroomDef)
        {
            if (baseSyringeDef == null) throw new InvalidOperationException("Base syringe definition not initialized.");

            SporeSyringeDefinition newSyringe = UnityEngine.Object.Instantiate(baseSyringeDef);
            newSyringe.ID = $"{shroomDef.ID}_customsyringedefinition";
            newSyringe.name = newSyringe.ID;
            newSyringe.Name = $"{shroomDef.name} Spore Syringe";

            // Use the original ShroomDefinition directly — it's already registered and initialized
            ShroomSpawnDefinition clonedSpawnDef = CloneSpawnDefinition(shroomDef.ID, shroomDef);
            newSyringe.SpawnDefinition = clonedSpawnDef;

            // Register the cloned ShroomSpawnDefinition so MushroomBed.CreateAndAssignColony_Server
            // can look it up by ID when the player applies the spawn to a bed
            Singleton<Registry>.Instance.AddToRegistry(clonedSpawnDef);
            Utility.Log($"Registered ShroomSpawnDefinition: {clonedSpawnDef.ID}");

            return newSyringe;
        }

        private ShroomSpawnDefinition CloneSpawnDefinition(string shroomId, ShroomDefinition shroomDef)
        {
            ShroomSpawnDefinition cloned = UnityEngine.Object.Instantiate(baseSpawnDefinition);
            cloned.ID = $"{shroomId}_customspawndefinition";
            cloned.name = cloned.ID;
            cloned.Name = $"{shroomDef.name} Spawn";
            cloned.Shroom = shroomDef;

            // Clone the colony prefab so its serialized _spawnDefinition field points to our
            // cloned spawn def. ShroomColony.GetHarvestedShroom() reads _spawnDefinition.Shroom
            // to determine what product to yield. Without this, the colony uses the vanilla def.
            ShroomColony clonedColony = UnityEngine.Object.Instantiate(baseColonyPrefab, rootGameObject);
            clonedColony.gameObject.name = $"{shroomId}_Colony";
            clonedColony._spawnDefinition = cloned;
            cloned.ColonyPrefab = clonedColony;

            return cloned;
        }

        public void DeleteChildren()
        {
            rootGameObject.DeleteChildren(true, false);
        }
    }
}

using Il2Cpp;
using Il2CppScheduleOne.NPCs.CharacterClasses;
using MelonLoader;
using MelonLoader.Utils;
using UnityEngine;

namespace UnicornsCustomSeeds.TemplateUtils
{
    public static class SeedIconGenerator
    {
        public static Sprite GenerateSeedIcon(Transform model)
        {
            Utility.Log($"layer: {RuntimePreviewGenerator.PREVIEW_LAYER}");
            Utility.Log("[IconGen] LAME: " + RuntimePreviewGenerator.renderCamera.name + " | Layer=" + RuntimePreviewGenerator.renderCamera.gameObject.layer + " | CullingMask=" + RuntimePreviewGenerator.renderCamera.cullingMask);
            if (model == null) return null;

            // RuntimePreviewGenerator needs the bounds to frame the object
            RuntimePreviewGenerator.BackgroundColor = Color.red; // transparent bg
            RuntimePreviewGenerator.Padding = 0.1f;                       // optional, zoom out a bit
            RuntimePreviewGenerator.OrthographicMode = true;              // clean 2D-style icons

            // GetTextureFromModel will clone internally, isolate, and render it
            Texture2D tex = RuntimePreviewGenerator.GenerateModelPreview(model, 256, 256);

            if (tex == null)
            {
                MelonLogger.Error("Failed to generate preview texture!");
                return null;
            }

            // Save PNG to UserData/SeedIcons
            string userDataPath = Path.Combine(MelonEnvironment.UserDataDirectory, "SeedIcons");
            Directory.CreateDirectory(userDataPath);
            string filePath = Path.Combine(userDataPath, model.name + "_Icon.png");
            File.WriteAllBytes(filePath, tex.EncodeToPNG());
            MelonLogger.Msg($"Saved icon to: {filePath}");

            // Convert to Sprite so you can use it directly in UI
            return Sprite.Create(
                tex,
                new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f)
            );
        }
    }
}

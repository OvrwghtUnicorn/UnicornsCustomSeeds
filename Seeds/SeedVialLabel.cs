
using UnityEngine;
using MelonLoader;
using UnicornsCustomSeeds.Managers;
using UnicornsCustomSeeds.TemplateUtils;
#if IL2CPP
using Il2CppScheduleOne.Product;
#elif MONO
using ScheduleOne.Product;
#endif

namespace UnicornsCustomSeeds.Seeds
{
    [RegisterTypeInIl2Cpp]
    public class SeedVialLabel : MonoBehaviour
    {
        private Renderer rend;
        private MaterialPropertyBlock block;
        public Color colorA;
        public Color colorB;
        public bool useRandomColors = false;
        void Start()
        {
            // If the flag is true, generate new random colors.
            if (useRandomColors)
            {
                colorA = UnityEngine.Random.ColorHSV();
                colorB = UnityEngine.Random.ColorHSV();
            }

            rend = GetComponent<Renderer>();
            block = new MaterialPropertyBlock();

            SetupLabel();
        }

        public void SetupLabel()
        {
            if (rend == null) return;

            var meshFilter = GetComponent<MeshFilter>();
            if (meshFilter == null) return;

            // The factory renames this object to "<original>:<itemId>" on the line after
            // it adds this component, so the suffix is always present.
            string id = name.Split(':')[1];

            if (SeedVisualsManager.TryGetLabelColors(id, out Color main, out Color secondary))
            {
                colorA = main;
                colorB = secondary;
            }
            else if (!useRandomColors)
            {
                // Writing the block now would push the default Color — transparent black —
                // into the shader. Leaving the material's own colours is the better failure.
                Utility.Error($"SeedVialLabel: no appearance registered for '{id}' — leaving the label uncoloured.");
                return;
            }

            var localBounds = meshFilter.sharedMesh.bounds;

            rend.GetPropertyBlock(block);

            block.SetColor("_ColorA", colorA);
            block.SetColor("_ColorB", colorB);
            block.SetFloat("_MinZ", localBounds.min.z);
            block.SetFloat("_MaxZ", localBounds.max.z);

            rend.SetPropertyBlock(block);
        }
    }
}

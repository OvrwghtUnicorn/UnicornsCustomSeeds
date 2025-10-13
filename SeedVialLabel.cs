using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using MelonLoader;
using Il2CppScheduleOne.Product;

namespace UnicornsCustomSeeds
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

            // Get bounds from mesh filter
            var meshFilter = GetComponent<MeshFilter>();
            if (meshFilter != null)
            {
                string id = this.name.Split(':')[1];
                
                WeedAppearanceSettings appearance;
                CustomSeedsManager.appearanceMap.TryGetValue(id,out appearance);
                if (appearance != null) {
                    colorA = appearance.MainColor;
                    colorB = appearance.SecondaryColor;
                }
                var localBounds = meshFilter.sharedMesh.bounds;

                rend.GetPropertyBlock(block); // Get current properties

                block.SetColor("_ColorA", colorA);
                block.SetColor("_ColorB", colorB);
                block.SetFloat("_MinZ", localBounds.min.z);
                block.SetFloat("_MaxZ", localBounds.max.z);

                rend.SetPropertyBlock(block);
            }
        }
    }
}

using UnityEngine;

namespace LeiTing.Core
{
    public static class SpriteMaterialUtility
    {
        private const string UniversalSpriteUnlitShader = "Universal Render Pipeline/2D/Sprite-Unlit-Default";
        private const string UniversalSpriteLitShader = "Universal Render Pipeline/2D/Sprite-Lit-Default";
        private const string BuiltInSpriteShader = "Sprites/Default";
        private const string ErrorShader = "Hidden/InternalErrorShader";

        private static Material defaultSpriteMaterial;

        public static Material DefaultSpriteMaterial
        {
            get
            {
                if (defaultSpriteMaterial == null)
                {
                    defaultSpriteMaterial = CreateSpriteMaterial("LeiTing Default Sprite Material");
                }

                return defaultSpriteMaterial;
            }
        }

        public static Material CreateSpriteMaterial(string materialName, Texture mainTexture = null)
        {
            var shader = FindDefaultSpriteShader();
            if (shader == null)
            {
                Debug.LogError("No compatible sprite shader found. Check GraphicsSettings always-included shaders.");
                return null;
            }

            var material = new Material(shader)
            {
                name = string.IsNullOrEmpty(materialName) ? "LeiTing Sprite Material" : materialName,
                hideFlags = HideFlags.DontSave
            };

            if (mainTexture != null)
            {
                material.mainTexture = mainTexture;
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", Color.white);
            }

            return material;
        }

        public static void EnsureUsableSpriteMaterial(SpriteRenderer renderer)
        {
            if (renderer == null)
            {
                return;
            }

            var material = renderer.sharedMaterial;
            if (IsUsable(material))
            {
                return;
            }

            var fallbackMaterial = DefaultSpriteMaterial;
            if (fallbackMaterial != null)
            {
                renderer.sharedMaterial = fallbackMaterial;
            }
        }

        private static Shader FindDefaultSpriteShader()
        {
            return Shader.Find(UniversalSpriteUnlitShader)
                ?? Shader.Find(UniversalSpriteLitShader)
                ?? Shader.Find(BuiltInSpriteShader);
        }

        private static bool IsUsable(Material material)
        {
            return material != null
                && material.shader != null
                && material.shader.isSupported
                && material.shader.name != ErrorShader;
        }
    }
}

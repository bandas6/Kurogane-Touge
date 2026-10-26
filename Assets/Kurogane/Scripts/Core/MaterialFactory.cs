using UnityEngine;
using UnityEngine.Rendering;

namespace Kurogane.Core
{
    /// <summary>
    /// Materiales creados por código que funcionan con URP o, si el proyecto aún no tiene
    /// un URP Asset asignado, con el pipeline integrado.
    /// Nota: Shader.Find solo encuentra shaders incluidos en la build; antes de compilar
    /// para Android/PC hay que hornear materiales (Fase 3) o añadir estos shaders a
    /// Project Settings > Graphics > Always Included Shaders.
    /// </summary>
    public static class MaterialFactory
    {
        static bool Urp => GraphicsSettings.currentRenderPipeline != null;

        static Shader Find(string urp, string builtin)
        {
            var s = Urp ? Shader.Find(urp) : null;
            return s != null ? s : Shader.Find(builtin);
        }

        public static Material Lit(Color color, Texture2D tex = null, float smoothness = 0.1f)
        {
            var m = new Material(Find("Universal Render Pipeline/Lit", "Standard"));
            m.SetColor("_BaseColor", color);
            m.SetColor("_Color", color);
            if (tex != null)
            {
                m.SetTexture("_BaseMap", tex);
                m.SetTexture("_MainTex", tex);
            }
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Glossiness", smoothness);
            m.SetFloat("_Metallic", 0);
            return m;
        }

        public static Material Emissive(Color color, Color emission)
        {
            var m = Lit(color);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            return m;
        }

        public static Material Unlit(Color color, Texture2D tex = null)
        {
            var m = new Material(Find("Universal Render Pipeline/Unlit", tex != null ? "Unlit/Texture" : "Unlit/Color"));
            m.SetColor("_BaseColor", color);
            m.SetColor("_Color", color);
            if (tex != null)
            {
                m.SetTexture("_BaseMap", tex);
                m.SetTexture("_MainTex", tex);
            }
            return m;
        }

        /// <summary>Dibuja ambas caras (solo URP; en el pipeline integrado se ignora).</summary>
        public static Material DoubleSided(this Material m)
        {
            if (m.HasProperty("_Cull")) m.SetFloat("_Cull", (float)CullMode.Off);
            m.doubleSidedGI = true;
            return m;
        }
    }
}

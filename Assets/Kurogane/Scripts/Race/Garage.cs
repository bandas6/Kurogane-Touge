using Kurogane.Core;
using UnityEngine;

namespace Kurogane.Race
{
    /// <summary>
    /// Garaje de la escena Inicio, hecho por código: el taller de detrás de la tienda de tofu a medianoche, con
    /// fluorescentes, estanterías con cajas y la persiana abierta a la calle. El coche elegido se aparca en el centro
    /// y la cámara de garaje gira a su alrededor (radio 5,6 m: las paredes quedan más lejos).
    /// </summary>
    public class Garage : MonoBehaviour
    {
        [Tooltip("Rótulo del taller (inventado).")]
        public string sign = "豆腐店 黒鉄";

        /// <summary>Donde se aparca el coche, mirando a la persiana.</summary>
        public Vector3 Spot => transform.position;
        public float SpotHeading => transform.eulerAngles.y * Mathf.Deg2Rad;

        const float HalfX = 8, Back = -7.5f, Front = 8, Height = 4.2f;

        void Awake()
        {
            var floor = MaterialFactory.Lit(new Color(0.36f, 0.36f, 0.35f), ProceduralTextures.Gravel(), 0.25f);
            var wall = MaterialFactory.Lit(new Color(0.55f, 0.53f, 0.48f), null, 0.05f);
            var trim = MaterialFactory.Lit(new Color(0.18f, 0.2f, 0.24f), null, 0.3f);
            var wood = MaterialFactory.Lit(new Color(0.34f, 0.24f, 0.15f), null, 0.1f);
            var crate = MaterialFactory.Lit(new Color(0.62f, 0.5f, 0.3f), null, 0.05f);
            var tube = MaterialFactory.Emissive(new Color(0.9f, 0.95f, 1f), new Color(2.2f, 2.4f, 2.6f));
            var night = MaterialFactory.Unlit(new Color(0.02f, 0.03f, 0.07f));

            float w = HalfX * 2, d = Front - Back;
            Box("Suelo", new Vector3(0, -0.05f, (Front + Back) / 2), new Vector3(w, 0.1f, d), floor);
            Box("Techo", new Vector3(0, Height, (Front + Back) / 2), new Vector3(w, 0.1f, d), wall);
            Box("Pared del fondo", new Vector3(0, Height / 2, Back), new Vector3(w, Height, 0.2f), wall);
            Box("Pared izquierda", new Vector3(-HalfX, Height / 2, (Front + Back) / 2), new Vector3(0.2f, Height, d), wall);
            Box("Pared derecha", new Vector3(HalfX, Height / 2, (Front + Back) / 2), new Vector3(0.2f, Height, d), wall);
            Box("Zócalo", new Vector3(0, 0.25f, Back + 0.12f), new Vector3(w, 0.5f, 0.05f), trim);
            // Persiana subida y la calle de noche al otro lado
            Box("Persiana", new Vector3(0, Height - 0.35f, Front), new Vector3(w - 2.4f, 0.7f, 0.3f), trim);
            Box("Pilar", new Vector3(-HalfX + 0.6f, Height / 2, Front), new Vector3(1.2f, Height, 0.3f), wall);
            Box("Pilar", new Vector3(HalfX - 0.6f, Height / 2, Front), new Vector3(1.2f, Height, 0.3f), wall);
            Box("Noche", new Vector3(0, Height / 2, Front + 6), new Vector3(w * 2, Height * 2, 0.1f), night);

            // Fluorescentes
            for (int i = 0; i < 3; i++)
                Box("Fluorescente", new Vector3(0, Height - 0.08f, -4 + i * 4), new Vector3(1.4f, 0.06f, 0.12f), tube);

            // Estanterías con cajas de tofu y un banco de trabajo
            var rng = new System.Random(7);
            foreach (float x in new[] { -4.5f, 0, 4.5f })
            {
                for (int s = 0; s < 3; s++)
                {
                    Box("Estante", new Vector3(x, 0.5f + s * 0.9f, Back + 0.55f), new Vector3(3.6f, 0.06f, 0.8f), wood);
                    for (int c = 0; c < 4; c++)
                        if (rng.Next(4) > 0)
                            Box("Caja", new Vector3(x - 1.3f + c * 0.85f, 0.5f + s * 0.9f + 0.2f, Back + 0.55f), new Vector3(0.7f, 0.36f, 0.6f), crate);
                }
            }
            Box("Banco", new Vector3(HalfX - 0.6f, 0.45f, -2), new Vector3(1, 0.9f, 3.2f), wood);
            Box("Neumáticos", new Vector3(-HalfX + 0.7f, 0.35f, 2.5f), new Vector3(0.8f, 0.7f, 0.8f), trim);
            Box("Neumáticos", new Vector3(-HalfX + 0.7f, 1.05f, 2.5f), new Vector3(0.8f, 0.7f, 0.8f), trim);

            // Rótulo del taller sobre el fondo
            var board = Box("Rótulo", new Vector3(0, 3.35f, Back + 0.14f), new Vector3(4.2f, 0.7f, 0.06f),
                MaterialFactory.Emissive(new Color(0.55f, 0.08f, 0.1f), new Color(0.5f, 0.06f, 0.08f)));
            var text = new GameObject("Texto").AddComponent<TextMesh>();
            text.transform.SetParent(board.transform.parent, false);
            text.transform.localPosition = new Vector3(0, 3.35f, Back + 0.2f);
            text.transform.localRotation = Quaternion.Euler(0, 180, 0);
            text.text = sign;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 64;
            text.characterSize = 0.06f;
            text.color = new Color(1f, 0.93f, 0.8f);
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.GetComponent<MeshRenderer>().sharedMaterial = text.font.material;

            // Luz fría de los fluorescentes; la cálida sobre el coche la pone el menú (luz del garaje)
            var fill = new GameObject("Luz fluorescente").AddComponent<Light>();
            fill.transform.SetParent(transform, false);
            fill.transform.localPosition = new Vector3(0, Height - 0.4f, -1);
            fill.type = LightType.Point;
            fill.color = new Color(0.82f, 0.9f, 1f);
            fill.range = 16;
            fill.intensity = 1.6f;
            fill.shadows = LightShadows.None;
        }

        static Mesh cube;

        /// <summary>
        /// Caja con la malla de cubo integrada. Sin CreatePrimitive: añade un colisionador, y en la build (sin módulo de
        /// física) eso escribe un error en el log por cada caja.
        /// </summary>
        GameObject Box(string name, Vector3 localPos, Vector3 size, Material mat)
        {
            if (cube == null) cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.GetComponent<MeshFilter>().sharedMesh = cube;
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = size;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }
    }
}

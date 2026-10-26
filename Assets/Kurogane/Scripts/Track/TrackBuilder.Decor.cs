using System.Collections.Generic;
using Kurogane.Core;
using UnityEngine;

namespace Kurogane.Track
{
    /// <summary>
    /// Ambiente de la pista (Fase 3, por código): señales de tráfico y público. Todo se combina por celdas como
    /// el resto de objetos repetidos y se apaga por distancia.
    /// </summary>
    public partial class TrackBuilder
    {
        [Header("Ambiente")]
        [Range(0, 60)] public int spectatorsPerHairpin = 26;
        [Range(0, 40)] public int spectatorsAtStart = 18;
        [Tooltip("Flashes de cámara del público por horquilla.")]
        [Range(0, 12)] public int flashesPerHairpin = 5;

        static readonly Color[] JacketColors =
        {
            new Color(0.55f, 0.1f, 0.1f), new Color(0.12f, 0.2f, 0.42f), new Color(0.75f, 0.72f, 0.66f),
            new Color(0.16f, 0.16f, 0.18f), new Color(0.62f, 0.45f, 0.12f),
        };
        static readonly Color[] PantsColors = { new Color(0.12f, 0.15f, 0.25f), new Color(0.09f, 0.09f, 0.1f) };

        // ---------- Señales ----------

        void BuildSigns()
        {
            var d = Data;
            var quad = Primitive(PrimitiveType.Quad);
            var cyl = Primitive(PrimitiveType.Cylinder);
            var poles = new List<Matrix4x4>();
            var backs = new List<Matrix4x4>();
            var chevronLeft = new List<Matrix4x4>();
            var chevronRight = new List<Matrix4x4>();
            var warnLeft = new List<Matrix4x4>();
            var warnRight = new List<Matrix4x4>();
            var speed = new List<Matrix4x4>();

            foreach (var hp in d.Hairpins)
            {
                int turn = (int)Mathf.Sign(hp.angle), outer = -turn;
                bool left = turn < 0;

                // Chevrones a lo largo del exterior de la horquilla
                for (int i = hp.start; i <= hp.end; i += 5)
                {
                    var p = d.P[i];
                    var foot = d.At(i, outer, d.W + 0.95f, p.pos.y - 0.1f);
                    Sign(left ? chevronLeft : chevronRight, poles, backs, foot, p.forward, 1.1f, new Vector2(0.6f, 0.75f));
                }

                // Aviso de horquilla 70 m antes
                int w = hp.start - 35;
                if (w > 2)
                {
                    var p = d.P[w];
                    var foot = d.At(w, LowSide(w), d.W + 0.95f, p.pos.y - 0.1f);
                    Sign(left ? warnLeft : warnRight, poles, backs, foot, p.forward, 2.1f, new Vector2(0.95f, 0.95f));
                }
            }

            // Límite de velocidad en la salida y a la salida del túnel
            foreach (int i in new[] { 20, d.TunnelEnd + 15 })
            {
                if (i < 2 || i >= d.FinishIndex) continue;
                var p = d.P[i];
                Sign(speed, poles, backs, d.At(i, LowSide(i), d.W + 0.95f, p.pos.y - 0.1f), p.forward, 2.1f, new Vector2(0.75f, 0.75f));
            }

            AddCombined("Chevrons", quad, chevronLeft, MaterialFactory.Sign(ProceduralTextures.Chevron(true)), detailDistance);
            AddCombined("Chevrons", quad, chevronRight, MaterialFactory.Sign(ProceduralTextures.Chevron(false)), detailDistance);
            AddCombined("WarningSigns", quad, warnLeft, MaterialFactory.Sign(ProceduralTextures.HairpinWarning(true), true), detailDistance);
            AddCombined("WarningSigns", quad, warnRight, MaterialFactory.Sign(ProceduralTextures.HairpinWarning(false), true), detailDistance);
            AddCombined("SpeedSigns", quad, speed, MaterialFactory.Sign(ProceduralTextures.SpeedLimit(40), true), detailDistance);
            AddCombined("SignBacks", quad, backs, MaterialFactory.Lit(new Color(0.42f, 0.44f, 0.47f), null, 0.3f), detailDistance);
            AddCombined("SignPoles", cyl, poles, MaterialFactory.Lit(new Color(0.55f, 0.57f, 0.6f), null, 0.4f), detailDistance);

            BuildKmPosts(quad, cyl);
            BuildTunnelPlate(quad);
        }

        /// <summary>Lado con el muro más bajo en el punto i: ahí se planta la señal, en el borde del arcén.</summary>
        int LowSide(int i) => Data.WallHeight(i, 1) <= Data.WallHeight(i, -1) ? 1 : -1;

        /// <summary>
        /// Placa mirando al coche que llega (el Quad de Unity se ve desde -Z), su reverso gris y el poste.
        /// </summary>
        static void Sign(List<Matrix4x4> faces, List<Matrix4x4> poles, List<Matrix4x4> backs, Vector3 foot, Vector3 forward,
            float boardY, Vector2 size)
        {
            var center = foot + Vector3.up * boardY;
            faces.Add(Matrix4x4.TRS(center, Quaternion.LookRotation(forward), new Vector3(size.x, size.y, 1)));
            backs.Add(Matrix4x4.TRS(center + forward * 0.02f, Quaternion.LookRotation(-forward), new Vector3(size.x, size.y, 1)));
            float h = boardY - size.y / 2;
            poles.Add(Matrix4x4.TRS(foot + Vector3.up * h / 2 + forward * 0.04f, Quaternion.identity, new Vector3(0.07f, h / 2, 0.07f)));
        }

        /// <summary>Postes kilométricos cada 500 m. Cada número es una textura propia, así que van sueltos.</summary>
        void BuildKmPosts(Mesh quad, Mesh cyl)
        {
            var d = Data;
            var white = MaterialFactory.Lit(new Color(0.9f, 0.9f, 0.88f), null, 0.2f);
            int every = Mathf.RoundToInt(500 / d.Step);
            for (int i = every; i < d.FinishIndex; i += every)
            {
                if (d.InTunnel(i)) continue;
                var p = d.P[i];
                string km = (i * d.Step / 1000f).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
                var foot = d.At(i, LowSide(i), d.W + 0.95f, p.pos.y - 0.1f);
                var face = Add("KmPost", quad, MaterialFactory.Sign(ProceduralTextures.KmPost(km)));
                face.transform.SetPositionAndRotation(foot + Vector3.up * 0.75f, Quaternion.LookRotation(p.forward));
                face.transform.localScale = new Vector3(0.32f, 0.48f, 1);
                var post = Add("KmPost", cyl, white);
                post.transform.SetPositionAndRotation(foot + Vector3.up * 0.25f + p.forward * 0.03f, Quaternion.identity);
                post.transform.localScale = new Vector3(0.1f, 0.25f, 0.1f);
                culling.Add(face.GetComponent<MeshRenderer>(), detailDistance);
                culling.Add(post.GetComponent<MeshRenderer>(), detailDistance);
            }
        }

        /// <summary>Rótulo con el nombre del túnel sobre la boca de entrada.</summary>
        void BuildTunnelPlate(Mesh quad)
        {
            var d = Data;
            if (d.TunnelStart < 2) return;
            var p = d.P[d.TunnelStart - 2];
            var plate = Add("TunnelPlate", quad, MaterialFactory.Sign(ProceduralTextures.TunnelPlate("KUROGANE"), false, 0.5f));
            plate.transform.SetPositionAndRotation(p.pos + Vector3.up * 7.3f - p.forward * 0.72f, Quaternion.LookRotation(p.forward));
            plate.transform.localScale = new Vector3(3.6f, 0.95f, 1);
        }

        // ---------- Público ----------

        void BuildCrowd()
        {
            var d = Data;
            var cube = Primitive(PrimitiveType.Cube);
            var sphere = Primitive(PrimitiveType.Sphere);
            var quad = Primitive(PrimitiveType.Quad);
            var r = new System.Random(seed + 7);
            var jackets = new List<Matrix4x4>[JacketColors.Length];
            var pants = new List<Matrix4x4>[PantsColors.Length];
            for (int k = 0; k < jackets.Length; k++) jackets[k] = new List<Matrix4x4>();
            for (int k = 0; k < pants.Length; k++) pants[k] = new List<Matrix4x4>();
            var heads = new List<Matrix4x4>();
            var flashPoints = new List<(Vector3 pos, Vector3 facing)>();

            // En las explanadas por fuera de cada horquilla
            foreach (var hp in d.Hairpins)
            {
                int outer = -(int)Mathf.Sign(hp.angle), m = (hp.start + hp.end) / 2, placed = 0, flashes = 0;
                for (int tries = 0; placed < spectatorsPerHairpin && tries < spectatorsPerHairpin * 6; tries++)
                {
                    int i = m + r.Next(-12, 13);
                    if (i < 0 || i >= d.N || d.Platform(i, outer) < 0.6f) continue;
                    float lat = d.W + 2.2f + (float)r.NextDouble() * 4.6f;
                    var pos = d.At(i, outer, lat, 0);
                    pos.y = d.TerrainY(i, outer, lat);
                    var facing = Spectator(pos, d.P[i].pos, r, jackets, pants, heads);
                    if (flashes < flashesPerHairpin && r.Next(3) == 0)
                    {
                        flashPoints.Add((pos + Vector3.up * 1.62f + facing * 0.25f, facing));
                        flashes++;
                    }
                    placed++;
                }
            }

            // Junto a la salida, de pie en lo alto del muro o en la ladera
            var rows = new Vector2[4];
            for (int n = 0; n < spectatorsAtStart; n++)
            {
                int i = 4 + r.Next(0, 24), s = r.Next(2) == 0 ? -1 : 1;
                if (i >= d.N || d.InTunnel(i)) continue;
                d.Rows(i, s, rows);
                float lat = rows[2].x + 0.4f + (float)r.NextDouble() * 1.8f;
                var pos = d.At(i, s, lat, 0);
                pos.y = d.TerrainY(i, s, lat);
                Spectator(pos, d.P[i].pos, r, jackets, pants, heads);
            }

            float cull = detailDistance * 1.3f;
            for (int k = 0; k < jackets.Length; k++) AddCombined("Crowd", cube, jackets[k], MaterialFactory.Lit(JacketColors[k], null, 0.15f), cull);
            for (int k = 0; k < pants.Length; k++) AddCombined("Crowd", cube, pants[k], MaterialFactory.Lit(PantsColors[k], null, 0.1f), cull);
            AddCombined("Crowd", sphere, heads, MaterialFactory.Lit(new Color(0.14f, 0.1f, 0.08f), null, 0.1f), cull);

            // Flashes de cámara: quads blancos que CrowdFlashes enciende un instante al azar
            if (flashPoints.Count == 0) return;
            var white = MaterialFactory.Unlit(Color.white);
            var renderers = new List<Renderer>();
            foreach (var f in flashPoints)
            {
                var go = Add("Flash", quad, white);
                go.transform.SetPositionAndRotation(f.pos, Quaternion.LookRotation(-f.facing));
                go.transform.localScale = Vector3.one * 0.28f;
                var mr = go.GetComponent<MeshRenderer>();
                mr.enabled = false;
                renderers.Add(mr);
            }
            var fx = root.gameObject.AddComponent<CrowdFlashes>();
            fx.flashes = renderers.ToArray();
        }

        /// <summary>
        /// Un coche aparcado con los intermitentes de emergencia en la explanada de cada horquilla, junto al público
        /// (modelos del tráfico del bloque D).
        /// </summary>
        void BuildParkedCars()
        {
            var d = Data;
            var r = new System.Random(seed + 11);
            int n = 0;
            foreach (var hp in d.Hairpins)
            {
                int outer = -(int)Mathf.Sign(hp.angle), i = (hp.start + hp.end) / 2 + 10;
                if (i >= d.N || d.Platform(i, outer) < 0.6f) continue;
                float lat = d.W + 3.4f;
                var pos = d.At(i, outer, lat, 0);
                pos.y = d.TerrainY(i, outer, lat);
                var model = n++ % 2 == 0 ? Kurogane.Cars.TrafficModel.Kei : Kurogane.Cars.TrafficModel.Furgoneta;
                var paint = Kurogane.Cars.TrafficModels.Paints[r.Next(Kurogane.Cars.TrafficModels.Paints.Length)];
                var go = Kurogane.Cars.TrafficModels.Build(root, model, paint, out var hazards);
                go.name = "Aparcado";
                go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, d.P[i].heading * Mathf.Rad2Deg + 180, 0));
                var blink = go.AddComponent<Kurogane.City.Blinker>();
                blink.lamps = hazards;
                blink.period = 0.9f;
                // Los intermitentes los gobierna el Blinker; solo la carrocería se apaga por distancia
                foreach (var mr in go.GetComponentsInChildren<MeshRenderer>(true))
                    if (mr.gameObject.name != "Emergencia") culling.Add(mr, detailDistance * 1.3f);
            }
        }

        /// <summary>Figura de espectador mirando hacia <paramref name="look"/>; devuelve hacia dónde mira.</summary>
        static Vector3 Spectator(Vector3 pos, Vector3 look, System.Random r, List<Matrix4x4>[] jackets, List<Matrix4x4>[] pants,
            List<Matrix4x4> heads)
        {
            var dir = look - pos;
            dir.y = 0;
            if (dir.sqrMagnitude < 0.01f) dir = Vector3.forward;
            var rot = Quaternion.LookRotation(dir) * Quaternion.Euler(0, r.Next(-25, 26), 0);
            float sc = 0.9f + (float)r.NextDouble() * 0.18f;
            var m = Matrix4x4.TRS(pos, rot, Vector3.one * sc);
            var jacket = jackets[r.Next(jackets.Length)];
            pants[r.Next(pants.Length)].Add(m * Matrix4x4.TRS(new Vector3(0, 0.42f, 0), Quaternion.identity, new Vector3(0.34f, 0.84f, 0.22f)));
            jacket.Add(m * Matrix4x4.TRS(new Vector3(0, 1.13f, 0), Quaternion.identity, new Vector3(0.48f, 0.6f, 0.27f)));
            heads.Add(m * Matrix4x4.TRS(new Vector3(0, 1.6f, 0), Quaternion.identity, Vector3.one * 0.26f));
            // Uno de cada tres levanta el brazo animando
            if (r.Next(3) == 0)
                jacket.Add(m * Matrix4x4.TRS(new Vector3(0.3f, 1.58f, 0), Quaternion.Euler(0, 0, -15), new Vector3(0.13f, 0.55f, 0.13f)));
            return rot * Vector3.forward;
        }
    }
}

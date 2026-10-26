using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Kurogane.Atmosphere;
using UnityEngine;

namespace Kurogane.Race
{
    /// <summary>
    /// Benchmark para medir en el móvil sin mirar la pantalla. Se activa al arrancar con el argumento
    /// <c>-kuroganeBench</c> (en Android: <c>am start ... -e unity -kuroganeBench</c>) o si existe
    /// <c>benchmark.txt</c> en persistentDataPath (se borra al leerlo). Recorre siempre la misma ruta con el piloto
    /// automático en varias escenas y escribe en el log, con la etiqueta [KuroganeBench], fps, tiempo por cuadro y
    /// tiempos de CPU y GPU (FrameTimingManager, que sigue midiendo aunque el VSync limite a 60 fps).
    /// Las escenas "sin cielo" y "sin partículas" sirven para restar el coste del cielo y de la lluvia.
    /// </summary>
    public class Benchmark : MonoBehaviour
    {
        const string Tag = "[KuroganeBench] ";
        const float Seconds = 20, Warmup = 1.5f;

        RaceManager race;
        readonly FrameTiming[] timing = new FrameTiming[1];

        /// <summary>
        /// Si se pidió el benchmark. <paramref name="track"/> es la pista elegida (id de su TrackDefinition) con
        /// <c>-kuroganeBench=Usuba</c> o con el id escrito en benchmark.txt; vacío = la primera del catálogo.
        /// </summary>
        public static bool Requested(out string track)
        {
            bool arg = false;
            track = "";
            foreach (var a in Environment.GetCommandLineArgs())
                if (a == "-kuroganeBench" || a.StartsWith("-kuroganeBench="))
                {
                    arg = true;
                    if (a.Length > "-kuroganeBench=".Length) track = a.Substring("-kuroganeBench=".Length).Trim();
                }
            // El archivo se borra siempre, también si llegó el argumento: si no, el siguiente arranque normal volvía a medir
            var file = Path.Combine(Application.persistentDataPath, "benchmark.txt");
            if (!File.Exists(file)) return arg;
            try
            {
                var content = File.ReadAllText(file).Trim();
                if (content.Length > 1 && track.Length == 0) track = content;
                File.Delete(file);
            }
            catch (Exception) { }
            return true;
        }

        IEnumerator Start()
        {
            race = GetComponent<RaceManager>();
            race.saveRecords = false;
            yield return null;
            Debug.Log(Tag + $"INICIO {SystemInfo.deviceModel} · {SystemInfo.graphicsDeviceName} · {SystemInfo.graphicsDeviceType} · " +
                      $"{Screen.width}x{Screen.height} · calidad {QualitySettings.names[QualitySettings.GetQualityLevel()]} · " +
                      $"filtro PS2 {(race.retro != null && race.retro.enabled)}");

            var sky = FindAnyObjectByType<SkyDome>();
            var rain = race.chaseCamera.GetComponent<RainFx>();
            var d = race.track.Data;

            yield return Scenario("montaña", 0, 2, 40, null, null);
            yield return Scenario("montaña sin cielo", 0, 2, 40,
                () => { if (sky != null) sky.gameObject.SetActive(false); },
                () => { if (sky != null) sky.gameObject.SetActive(true); });
            bool filter = race.retroFilter;
            yield return Scenario("montaña sin filtro PS2", 0, 2, 40,
                () => { race.retroFilter = false; race.ApplyOptions(); },
                () => { race.retroFilter = filter; race.ApplyOptions(); });
            yield return Scenario("lluvia", 4, 2, 40, null, null);
            yield return Scenario("lluvia sin partículas", 4, 2, 40,
                () => { if (rain != null) rain.intensity = 0; if (race.retro != null) race.retro.rain = 0; }, null);
            yield return Scenario("niebla", 3, 2, 40, null, null);
            yield return Scenario("tráfico", 0, 1, 400, null, null);
            yield return Scenario("llegada al pueblo", 0, 2, d.FinishIndex - 60, null, null);
            Debug.Log(Tag + "FIN");
            race.player.Autopilot = null;
            race.OpenMenu();
            Destroy(this);
        }

        IEnumerator Scenario(string name, int weather, int traffic, int startIndex, Action setup, Action teardown)
        {
            race.weather = weather;
            race.traffic = traffic;
            race.mode = RaceMode.Contrarreloj;
            race.StartRace();
            setup?.Invoke();
            while (race.State == RaceState.Countdown) yield return null;
            race.player.ResetTo(startIndex, -2.4f);
            race.player.Autopilot = Drive;

            var dt = new List<float>();
            var cpu = new List<float>();
            var gpu = new List<float>();
            float t0 = Time.unscaledTime;
            while (Time.unscaledTime - t0 < Seconds + Warmup)
            {
                yield return null;
                if (Time.unscaledTime - t0 < Warmup) continue;
                dt.Add(Time.unscaledDeltaTime * 1000);
                FrameTimingManager.CaptureFrameTimings();
                if (FrameTimingManager.GetLatestTimings(1, timing) > 0)
                {
                    cpu.Add((float)timing[0].cpuFrameTime);
                    gpu.Add((float)timing[0].gpuFrameTime);
                }
            }
            race.player.Autopilot = null;
            teardown?.Invoke();

            dt.Sort();
            float avg = Average(dt), p95 = dt[Mathf.Min(dt.Count - 1, (int)(dt.Count * 0.95f))], worst = dt[dt.Count - 1];
            long mem = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / (1024 * 1024);
            Debug.Log(Tag + $"{name}: {1000 / avg:0.0} fps · cuadro {avg:0.0} ms (p95 {p95:0.0}, peor {worst:0.0}) · " +
                      $"CPU {Average(cpu):0.00} ms · GPU {Average(gpu):0.00} ms · {dt.Count} cuadros · memoria {mem} MB");
        }

        static float Average(List<float> v)
        {
            if (v.Count == 0) return 0;
            float s = 0;
            foreach (var x in v) s += x;
            return s / v.Count;
        }

        /// <summary>Piloto de prueba: carril izquierdo al 75 % de la velocidad límite de cada punto.</summary>
        (float gas, float brake, float steer) Drive()
        {
            var d = race.track.Data;
            var p = race.player;
            var q = d.P[Mathf.Min(d.N - 1, p.Index + 6)];
            var to = q.pos + q.right * -2.4f - p.transform.position;
            float err = Mathf.DeltaAngle(p.Heading * Mathf.Rad2Deg, Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg) / 25f;
            float vt = d.VMax[Mathf.Min(d.N - 1, p.Index + 4)] * 0.75f;
            return (p.Speed < vt ? 0.8f : 0f, p.Speed > vt + 1 ? 0.6f : 0f, Mathf.Clamp(err, -1, 1));
        }
    }
}

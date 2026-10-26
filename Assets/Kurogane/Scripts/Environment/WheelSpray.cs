using Kurogane.Cars;
using UnityEngine;
using UnityEngine.Rendering;

namespace Kurogane.Atmosphere
{
    /// <summary>
    /// Agua que levantan las ruedas traseras con el asfalto mojado: más cuanto más rápido va el coche (jugador o
    /// rival). En seco no emite nada.
    /// </summary>
    public class WheelSpray : MonoBehaviour
    {
        [Range(0, 1)] public float wetness;

        CarController player;
        RivalAI rival;
        ParticleSystem ps;

        void Awake()
        {
            player = GetComponent<CarController>();
            rival = GetComponent<RivalAI>();
        }

        void Build()
        {
            var go = new GameObject("Salpicaduras");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0, 0.25f, -2.1f);
            go.transform.localRotation = Quaternion.Euler(-12, 180, 0);
            ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
            main.startColor = new Color(0.75f, 0.78f, 0.85f, 0.22f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = Application.isMobilePlatform ? 120 : 250;
            main.gravityModifier = 0.25f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(1.5f, 0.1f, 0.2f);
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 0.6f, 1, 1.8f));
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) });
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = FxMaterials.Make(false);
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            ps.Play();
        }

        void Update()
        {
            if (wetness <= 0.001f && ps == null) return;
            if (ps == null) Build();
            float speed = player != null ? player.Speed : rival != null ? rival.Speed : 0;
            var emission = ps.emission;
            emission.rateOverTime = wetness * Mathf.Clamp(speed - 5, 0, 40) * 4;
        }
    }
}

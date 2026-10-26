using System.Collections.Generic;
using Kurogane.City;
using Kurogane.Track;
using UnityEngine;

namespace Kurogane.Cars
{
    /// <summary>
    /// Tráfico (bloque D):
    /// - Montaña (opción Tráfico): coches que suben en sentido contrario por su carril (se circula por la izquierda:
    ///   para el jugador que baja es la derecha, lateral +2,6 m). Aparecen fuera de la vista por delante y desaparecen
    ///   detrás del jugador; como mucho dos a la vez.
    /// - Pueblo (siempre): dos o tres coches lentos por la calle mayor que se paran en el paso a nivel cuando baja la
    ///   barrera.
    /// Todos se registran como obstáculos del jugador, con la misma respuesta que el rival.
    /// </summary>
    public class TrafficManager : MonoBehaviour
    {
        public bool mountainTraffic;
        [Range(0, 4)] public int maxMountainCars = 2;
        public float mountainLane = 2.6f;
        public float townLane = 2.4f;

        CarController player;
        TrackBuilder track;
        /// <summary>Pista de bajada: el tráfico va sobre ella sea cual sea el sentido de la carrera.</summary>
        TrackData data;
        CrossingController crossing;
        System.Func<bool> racing;
        readonly List<TrafficCar> mountain = new List<TrafficCar>(), town = new List<TrafficCar>();
        float spawnTimer = 4;
        float crossingDistance = -1;

        public IReadOnlyList<TrafficCar> MountainCars => mountain;

        public void Init(CarController playerCar, TrackBuilder trackBuilder, System.Func<bool> isRacing)
        {
            player = playerCar;
            track = trackBuilder;
            data = track.Geometry;
            var parent = track.Root;
            racing = isRacing;
            crossing = parent != null ? parent.GetComponentInChildren<CrossingController>(true) : null;
            if (crossing != null) crossingDistance = crossing.trackDistance;

            var holder = new GameObject("Trafico").transform;
            holder.SetParent(transform, false);
            var models = new[] { TrafficModel.Kei, TrafficModel.Furgoneta, TrafficModel.Taxi, TrafficModel.Camion };
            for (int k = 0; k < maxMountainCars; k++)
                mountain.Add(Make(holder, models[k % models.Length], TrafficModels.Paints[k % TrafficModels.Paints.Length]));

            // Pueblo: tres coches por la calle mayor, hacia la montaña, en el carril derecho del jugador
            if (data.TownStart < data.N)
            {
                var townModels = new[] { TrafficModel.Taxi, TrafficModel.Furgoneta, TrafficModel.Kei };
                for (int k = 0; k < townModels.Length; k++)
                {
                    var car = Make(holder, townModels[k], TrafficModels.Paints[(k + 2) % TrafficModels.Paints.Length]);
                    float start = (data.N - 2 - k * 40) * data.Step;
                    car.Init(data, start, -1, townLane, Random.Range(6f, 8.5f));
                    car.StopZone = AtCrossing;
                    car.gameObject.SetActive(true);
                    town.Add(car);
                    player.Obstacles.Add(car);
                }
            }
        }

        TrafficCar Make(Transform holder, TrafficModel model, Color paint)
        {
            var go = TrafficModels.Build(holder, model, paint, out _);
            var car = go.AddComponent<TrafficCar>();
            go.SetActive(false);
            return car;
        }

        /// <summary>La barrera está bajada y esa distancia queda justo antes de la vía.</summary>
        bool AtCrossing(float distance) =>
            crossing != null && crossing.Down && crossingDistance > 0 && distance > crossingDistance && distance < crossingDistance + 8;

        /// <summary>Quita los coches de la montaña (al empezar una bajada o volver al menú).</summary>
        public void ResetMountain()
        {
            foreach (var c in mountain) Despawn(c);
            spawnTimer = Random.Range(3f, 8f);
        }

        void Despawn(TrafficCar c)
        {
            if (!c.gameObject.activeSelf) return;
            c.gameObject.SetActive(false);
            player.Obstacles.Remove(c);
        }

        void Update()
        {
            if (player == null || data == null) return;

            // Montaña: en sentido contrario al jugador. Bajando, suben (-1) por su izquierda (+lateral); subiendo, bajan
            // (+1) por la suya (-lateral). Todo en índices de la pista de bajada
            var race = track.Data;
            int dir = race.Uphill ? 1 : -1;
            int at0 = race.ToGeometry(player.Index), goal = race.ToGeometry(race.FinishIndex);
            bool on = mountainTraffic && racing != null && racing();
            foreach (var c in mountain)
                if (c.gameObject.activeSelf && (!on || (c.Index - at0) * dir > 30 || c.Distance <= 1 || c.Distance >= data.Length - 2)) Despawn(c);
            if (on)
            {
                spawnTimer -= Time.deltaTime;
                if (spawnTimer <= 0)
                {
                    spawnTimer = Random.Range(7f, 16f);
                    int at = at0 - dir * Random.Range(150, 200); // unos 300-400 m por delante: fuera de la vista
                    if (race.Uphill ? at > goal : at < goal)
                        foreach (var c in mountain)
                            if (!c.gameObject.activeSelf)
                            {
                                c.Init(data, at * data.Step, dir, -dir * mountainLane, Random.Range(10f, 13f));
                                c.gameObject.SetActive(true);
                                player.Obstacles.Add(c);
                                break;
                            }
                }
            }

            // Pueblo: al llegar al principio de la calle vuelven al final, siempre en marcha
            foreach (var c in town)
                if (c.Index <= data.TownStart + 1)
                    c.Init(data, (data.N - 2) * data.Step, -1, townLane, c.CruiseSpeed);
        }
    }
}

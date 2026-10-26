using Kurogane.Cars;
using UnityEngine;

namespace Kurogane.CameraRig
{
    public enum ChaseMode { Persecucion, Lejana, Capo }

    /// <summary>Cámara de persecución que gira con el derrape, como en el prototipo web.</summary>
    [RequireComponent(typeof(Camera))]
    public class ChaseCamera : MonoBehaviour
    {
        public CarController target;
        public ChaseMode mode = ChaseMode.Persecucion;
        public float baseFov = 60;

        Camera cam;
        float camHeading;
        Vector3 camPos;
        float shake;

        void Awake() => cam = GetComponent<Camera>();

        public void Snap()
        {
            if (target == null) return;
            camHeading = target.Heading;
            var t = target.transform.position;
            camPos = t - new Vector3(Mathf.Sin(camHeading), 0, Mathf.Cos(camHeading)) * 7 + Vector3.up * 3;
            transform.position = camPos;
        }

        public void Shake(float amount) => shake = Mathf.Max(shake, Mathf.Clamp01(amount));

        public void Cycle() => mode = (ChaseMode)(((int)mode + 1) % 3);

        void LateUpdate()
        {
            if (target == null || target.Track == null) return;
            float dt = Time.deltaTime;
            var car = target.transform.position;
            float h = target.Heading, sp = target.Speed;
            var fwd = new Vector3(Mathf.Sin(h), 0, Mathf.Cos(h));
            Vector3 look;
            if (mode == ChaseMode.Capo)
            {
                camPos = car + fwd * 0.9f + Vector3.up * 1.12f;
                look = car + fwd * 20 + Vector3.up * 0.6f;
                camHeading = h;
            }
            else
            {
                var v = target.Velocity;
                float velH = sp > 3 ? Mathf.Atan2(v.x, v.z) : h;
                float wanted = h + Mathf.DeltaAngle(h * Mathf.Rad2Deg, velH * Mathf.Rad2Deg) * Mathf.Deg2Rad * 0.55f;
                camHeading += Mathf.DeltaAngle(camHeading * Mathf.Rad2Deg, wanted * Mathf.Rad2Deg) * Mathf.Deg2Rad * Mathf.Min(1, dt * 4.5f);
                bool far = mode == ChaseMode.Lejana;
                float dist = (far ? 9.5f : 6.2f) + sp * 0.03f, ht = (far ? 3.6f : 2.3f) + sp * 0.012f;
                var want = car - new Vector3(Mathf.Sin(camHeading), 0, Mathf.Cos(camHeading)) * dist + Vector3.up * ht;
                camPos = Vector3.Lerp(camPos, want, Mathf.Min(1, dt * 8));
                var track = target.Track;
                int ci = track.Nearest(camPos, target.Index);
                camPos.y = Mathf.Max(camPos.y, track.P[ci].pos.y + 1.2f);
                if (track.InTunnel(ci)) camPos.y = Mathf.Min(camPos.y, track.P[ci].pos.y + 5.2f);
                look = car + fwd * 4 + Vector3.up * 1.1f;
            }
            var pos = camPos;
            if (shake > 0)
            {
                pos += new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(-0.2f, 0.2f), 0) * shake;
                shake = Mathf.Max(0, shake - dt * 2.5f);
            }
            if (target.InGutter) pos.y += Random.Range(-0.03f, 0.03f);
            transform.SetPositionAndRotation(pos, Quaternion.LookRotation(look - pos));
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, (mode == ChaseMode.Capo ? 66 : baseFov) + sp * 0.32f, 0.08f);
        }
    }
}

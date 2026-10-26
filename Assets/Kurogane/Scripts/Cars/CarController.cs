using System;
using Kurogane.Controls;
using Kurogane.Track;
using UnityEngine;

namespace Kurogane.Cars
{
    /// <summary>
    /// Física arcade del coche, portada tal cual del prototipo web. No usa WheelCollider ni Rigidbody:
    /// integra posición y velocidad a 120 Hz y resuelve los límites de la calzada contra la línea central.
    /// Convención: rumbo 0 = +Z, positivo = giro a la derecha; lateral positivo = derecha.
    /// </summary>
    public class CarController : MonoBehaviour
    {
        public CarSpec spec;
        public bool manualGearbox;
        [Tooltip("Coche rival para rebufo y contacto (opcional).")]
        public RivalAI rival;

        const float SubStep = 1f / 120f;

        // ---- Estado público (lo leen la cámara, el HUD y el RaceManager) ----
        public TrackData Track { get; private set; }
        public bool ControlsEnabled { get; set; }
        public bool AutoBrake { get; set; }
        /// <summary>
        /// Piloto automático (llegada al pueblo): si no es nulo, da gas, freno y volante en lugar del jugador.
        /// Necesita ControlsEnabled para que la caja automática siga cambiando.
        /// </summary>
        public Func<(float gas, float brake, float steer)> Autopilot { get; set; }
        /// <summary>Multiplica spec.grip (clima: 0,8 con lluvia). Es el único cambio de física del bloque C.</summary>
        public float GripScale { get; set; } = 1;
        /// <summary>Multiplica spec.driftGrip (clima: 0,85 con lluvia).</summary>
        public float DriftGripScale { get; set; } = 1;
        public float Heading { get; private set; }
        public Vector3 Velocity => new Vector3(vx, 0, vz);
        public float Speed => Mathf.Sqrt(vx * vx + vz * vz);
        public int Index { get; private set; }
        public int Gear { get; private set; }
        public float Rpm { get; private set; } = 1000;
        public float Slip { get; private set; }
        public float Steer { get; private set; }
        public bool Drifting { get; private set; }
        public bool InGutter { get; private set; }
        public bool Slipstream { get; private set; }
        public bool RevLimiter { get; private set; }
        public bool Braking { get; private set; }
        public float GasInput { get; private set; }
        /// <summary>Aceleración lateral suavizada (m/s², positiva = derecha).</summary>
        public float LateralAccel { get; private set; }
        /// <summary>Aceleración longitudinal suavizada (m/s², positiva = hacia delante).</summary>
        public float LongitudinalAccel { get; private set; }

        public event Action<float, Vector3> WallHit;
        public event Action<float> RivalContact;
        public event Action Upshift;
        public event Action LiftOff;

        float vx, vz, shiftTimer, accX, accZ, accumulator;
        bool lastGas;
        int shiftRequest;

        public void Init(TrackData track, CarSpec carSpec, int startIndex, float startLateral)
        {
            Track = track;
            spec = carSpec;
            ResetTo(startIndex, startLateral);
        }

        public void ResetTo(int index, float lateral)
        {
            var p = Track.P[index];
            transform.position = p.pos + p.right * lateral;
            Heading = p.heading;
            transform.rotation = Quaternion.Euler(0, Heading * Mathf.Rad2Deg, 0);
            vx = vz = 0;
            Index = index;
            Gear = 0;
            Rpm = 1000;
            Steer = 0;
            Slip = 0;
            Drifting = false;
            InGutter = false;
            accX = accZ = 0;
            shiftTimer = 0;
        }

        /// <summary>Vuelve al centro de la calzada en el punto actual (tecla R).</summary>
        public void Respawn() => ResetTo(Index, 0);

        public void RequestShift(int dir) => shiftRequest = dir;

        void Update()
        {
            if (Track == null) return;
            if (manualGearbox && ControlsEnabled)
            {
                if (DriverInput.ShiftUp) shiftRequest = 1;
                if (DriverInput.ShiftDown) shiftRequest = -1;
            }
            accumulator += Mathf.Min(Time.deltaTime, 0.1f);
            while (accumulator >= SubStep)
            {
                Step(SubStep);
                accumulator -= SubStep;
            }
            transform.SetPositionAndRotation(transform.position, Quaternion.Euler(0, Heading * Mathf.Rad2Deg, 0));
        }

        void Step(float dt)
        {
            float gas = ControlsEnabled ? DriverInput.Gas : 0;
            float brake = ControlsEnabled ? DriverInput.Brake : (AutoBrake ? 0.6f : 0);
            bool hand = ControlsEnabled && DriverInput.Handbrake;
            float steerIn = DriverInput.Steer;
            if (Autopilot != null)
            {
                // Piloto automático: solo cambia de dónde salen los mandos; la física es la misma
                var a = Autopilot();
                gas = a.gas;
                brake = a.brake;
                steerIn = a.steer;
                hand = false;
            }
            GasInput = gas;
            Braking = brake > 0.1f;

            Steer += (steerIn - Steer) * Mathf.Min(1, dt * (Mathf.Abs(steerIn) > 0.01f ? 7 : 10));
            float h = Heading;
            float fx = Mathf.Sin(h), fz = Mathf.Cos(h), rx = Mathf.Cos(h), rz = -Mathf.Sin(h);
            float vF = vx * fx + vz * fz, vL = vx * rx + vz * rz;

            // Caja de cambios
            if (shiftTimer > 0) shiftTimer -= dt;
            Rpm = RpmAt(Gear, vF);
            if (ControlsEnabled)
            {
                if (!manualGearbox)
                {
                    if (Rpm > 7700 && Gear < spec.Gears - 1 && shiftTimer <= 0) DoUpshift();
                    else if (Gear > 0 && shiftTimer <= 0 && RpmAt(Gear - 1, vF) < 6600 && Rpm < 4200) { Gear--; shiftTimer = 0.15f; }
                }
                else if (shiftRequest != 0 && shiftTimer <= 0)
                {
                    if (shiftRequest > 0 && Gear < spec.Gears - 1) DoUpshift();
                    else if (shiftRequest < 0 && Gear > 0) { Gear--; shiftTimer = 0.1f; }
                    shiftRequest = 0;
                }
            }
            Rpm = RpmAt(Gear, vF);
            if (Gear == 0 && Mathf.Abs(vF) < 1)
                Rpm = !ControlsEnabled && DriverInput.Gas > 0 ? 6500 + UnityEngine.Random.value * 900 : gas > 0 ? 4500 : 950;
            float rn = (Rpm - 1000) / 7200f;
            float torque = 0.72f + 0.4f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(rn) * 0.95f);
            RevLimiter = gas > 0 && Rpm >= 8300;

            float acc = 0;
            if (gas > 0 && shiftTimer <= 0 && !RevLimiter) acc += spec.gearAccel[Gear] * torque * gas;
            if (Rpm > 8600) acc -= 4;
            if (brake > 0) { if (vF > 0.5f) acc -= 14 * brake; else if (vF > -6) acc -= 4 * brake; }
            if (gas <= 0 && brake <= 0) acc -= Mathf.Sign(vF) * (0.9f + Rpm / 8000f * 0.8f);
            acc -= 0.0011f * vF * Mathf.Abs(vF) * (Slipstream ? 0.55f : 1);
            if (Slipstream && gas > 0) acc += 1.4f;
            if (hand && vF > 0) acc -= 3.5f;
            vF += acc * dt;
            if (gas <= 0 && brake <= 0 && Mathf.Abs(vF) < 0.3f) vF = 0;
            float spA = Mathf.Sqrt(vF * vF + vL * vL), fsp = Mathf.Abs(vF);

            // Petardeo al soltar el gas con vueltas altas
            bool gasOn = gas > 0;
            if (ControlsEnabled && !gasOn && lastGas && Rpm > 5200) LiftOff?.Invoke();
            lastGas = gasOn;

            // Derrape: freno de mano, o frenada brusca con el volante girado a buena velocidad
            if (hand && fsp > 8) Drifting = true;
            if (brake > 0 && Mathf.Abs(Steer) > 0.6f && fsp > 19 && Mathf.Abs(Slip) < 0.1f) Drifting = true;
            if (Drifting && ((Mathf.Abs(Slip) < 0.09f && !hand) || fsp < 5)) Drifting = false;

            // Canaleta: las ruedas interiores enganchadas en el desagüe de la horquilla
            var pos = transform.position;
            float dlat = Track.Lateral(Index, pos);
            int gs = Track.GutterSide[Index];
            bool inGut = ControlsEnabled && gs != 0 && dlat * gs > Track.W - 1.75f && fsp > 7;

            // Giro
            float yaw = Steer * spec.yawRate * Mathf.Min(1, fsp / 6) / (1 + fsp / 30);
            if (vF < 0) yaw = -yaw * 0.6f;
            if (Drifting) yaw *= 1.45f + gas * 0.25f;
            if (inGut) yaw *= 1.3f;
            yaw += Slip * (Drifting ? 0.55f : 2.2f);
            Heading = h + yaw * dt;

            float grip = Drifting ? (hand ? 0.9f : spec.driftGrip * DriftGripScale + (gas > 0 ? 0 : 1.2f)) : spec.grip * GripScale;
            if (spec.allWheelDrive && Drifting && gas > 0) grip += 0.8f;
            if (inGut) grip = Mathf.Max(grip, 14);
            vL *= Mathf.Exp(-grip * dt);
            float nsp = Mathf.Sqrt(vF * vF + vL * vL);
            if (nsp > 0.01f)
            {
                float loss = Drifting ? 0.06f + 0.22f * Mathf.Abs(Slip) : 0.02f;
                float sc = spA * (1 - loss * dt) / nsp;
                if (sc < 1.5f) { vF *= sc; vL *= sc; }
            }

            float ovx = vx, ovz = vz;
            vx = fx * vF + rx * vL;
            vz = fz * vF + rz * vL;
            float nh = Heading;
            float nfx = Mathf.Sin(nh), nfz = Mathf.Cos(nh), nrx = Mathf.Cos(nh), nrz = -Mathf.Sin(nh);
            vF = vx * nfx + vz * nfz;
            vL = vx * nrx + vz * nrz;
            Slip = Mathf.Abs(vF) > 2 ? Mathf.Atan2(vL, Mathf.Abs(vF)) : 0;
            if (Mathf.Abs(Slip) > 1.25f) vL *= 0.9f;
            vx = nfx * vF + nrx * vL;
            vz = nfz * vF + nrz * vL;
            pos.x += vx * dt;
            pos.z += vz * dt;

            float ax = (vx - ovx) / dt, az = (vz - ovz) / dt, k = Mathf.Min(1, dt * 6);
            accX = Mathf.Lerp(accX, ax, k);
            accZ = Mathf.Lerp(accZ, az, k);
            LateralAccel = accX * nrx + accZ * nrz;
            LongitudinalAccel = accX * nfx + accZ * nfz;

            // Límites de la calzada (por el lado de la canaleta las ruedas pueden bajar al desagüe)
            Index = Track.Nearest(pos, Index);
            var p = Track.P[Index];
            float d = Track.Lateral(Index, pos);
            int sd = d >= 0 ? 1 : -1;
            float lim = Track.GutterSide[Index] == sd ? Track.W - 0.6f : Track.W - 0.95f;
            float along = (pos.x - p.pos.x) * p.forward.x + (pos.z - p.pos.z) * p.forward.z;
            var next = Track.P[Mathf.Min(Track.N - 1, Index + 1)];
            pos.y = Mathf.Lerp(p.pos.y, next.pos.y, Mathf.Clamp01(along / Track.Step)) - (inGut ? 0.06f : 0);
            InGutter = inGut;
            if (Mathf.Abs(d) > lim)
            {
                float over = Mathf.Abs(d) - lim;
                pos.x -= p.right.x * over * sd;
                pos.z -= p.right.z * over * sd;
                float vn = (vx * p.right.x + vz * p.right.z) * sd;
                if (vn > 0)
                {
                    vx -= p.right.x * sd * vn * 1.3f;
                    vz -= p.right.z * sd * vn * 1.3f;
                    vx *= 0.985f;
                    vz *= 0.985f;
                    if (vn > 2.2f && ControlsEnabled) WallHit?.Invoke(vn, pos + p.right * sd * 0.9f);
                }
            }

            // Rebufo y contacto con el rival
            Slipstream = false;
            if (rival != null && rival.isActiveAndEnabled && ControlsEnabled)
            {
                float ds = rival.Distance - Index * Track.Step;
                float rl = Track.Lane[Mathf.Clamp(rival.Index, 0, Track.N - 1)];
                if (ds > 3 && ds < 16 && Mathf.Abs(d - rl) < 1.9f && fsp > 18) Slipstream = true;
                var rp = rival.transform.position;
                float dx = pos.x - rp.x, dz = pos.z - rp.z, dd = Mathf.Sqrt(dx * dx + dz * dz);
                if (dd < 2.3f && dd > 0.01f)
                {
                    float nx = dx / dd, nz = dz / dd, push = 2.3f - dd;
                    pos.x += nx * push;
                    pos.z += nz * push;
                    var rv = rival.Velocity;
                    float rel = (vx - rv.x) * nx + (vz - rv.z) * nz;
                    if (rel < 0)
                    {
                        vx -= nx * rel * 1.2f;
                        vz -= nz * rel * 1.2f;
                        rival.Bump(rel * 0.3f);
                        if (rel < -2) RivalContact?.Invoke(-rel);
                    }
                }
            }
            transform.position = pos;
        }

        float RpmAt(int gear, float forwardSpeed) => Mathf.Clamp(1000 + 7200 * Mathf.Abs(forwardSpeed) / spec.gearTopSpeed[gear], 900, 9000);

        void DoUpshift()
        {
            Gear++;
            shiftTimer = manualGearbox ? 0.12f : 0.22f;
            Upshift?.Invoke();
        }
    }
}

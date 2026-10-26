using UnityEngine;

namespace Kurogane.Cars
{
    /// <summary>
    /// De dónde salen el suelo y las paredes para el coche (bloque G). La física del coche (fuerzas, agarre, derrape,
    /// caja de cambios) no cambia: solo el último paso, que pone la altura y frena el coche contra los bordes.
    /// - <see cref="TrackSurface"/>: contra la línea central de la pista, exacto y sin colisionadores (carrera).
    /// - <see cref="WorldSurface"/>: rayo contra el suelo y colisionadores (modo libre y pueblo abierto).
    /// </summary>
    public interface ICarSurface
    {
        /// <summary>Si las ruedas interiores están en la canaleta de una horquilla (sin mirar velocidad ni mandos).</summary>
        bool OverGutter(CarController car, Vector3 pos);

        /// <summary>
        /// Pone la altura del coche y lo separa de los bordes, con el mismo rebote para todas las superficies.
        /// <paramref name="impact"/> es la velocidad contra la pared (0 si no tocó) y <paramref name="point"/> dónde.
        /// </summary>
        void Constrain(CarController car, ref Vector3 pos, ref float vx, ref float vz, bool inGutter, out float impact, out Vector3 point);

        /// <summary>Normal del suelo bajo el coche, para inclinarlo (Vector3.up en la pista).</summary>
        Vector3 Normal { get; }
    }
}

using UnityEngine;

namespace Kurogane.Cars
{
    /// <summary>Coche con el que el jugador puede chocar: el rival o un coche del tráfico.</summary>
    public interface ICarObstacle
    {
        bool Active { get; }
        Vector3 Position { get; }
        Vector3 Velocity { get; }
        /// <summary>Cambio de velocidad por el golpe (negativo frena).</summary>
        void Bump(float deltaSpeed);
    }
}

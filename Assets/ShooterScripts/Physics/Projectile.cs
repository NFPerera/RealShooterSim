using UnityEngine;

namespace RealShooter.Ballistics
{
    /// Estado en tiempo real de una bala en vuelo. No es un ScriptableObject: es una instancia
    /// efimera creada por PhysicsManager.Fire() a partir de un BulletData/WeaponData reutilizables.
    public class Projectile
    {
        public BulletData Bullet { get; }
        public WeaponData Weapon { get; }

        public Vector3 Origin { get; }
        public Vector3 Position;
        public Vector3 Velocity;

        public float SpinRateRadPerSec;
        public float TimeOfFlight;
        public bool ShouldDespawn;

        /// Direccion horizontal inicial de disparo (fija durante todo el vuelo).
        /// El spin drift se aplica lateralmente respecto a esta direccion, no a la velocidad
        /// instantanea (que se inclina hacia abajo por la gravedad a medida que avanza el vuelo).
        public Vector3 InitialHorizontalDirection { get; }

        /// Deriva lateral acumulada por spin drift hasta el momento, en metros.
        public float AccumulatedSpinDriftM;

        public Projectile(BulletData bullet, WeaponData weapon, Vector3 position, Vector3 velocity)
        {
            Bullet = bullet;
            Weapon = weapon;
            Origin = position;
            Position = position;
            Velocity = velocity;
            TimeOfFlight = 0f;
            ShouldDespawn = false;
            AccumulatedSpinDriftM = 0f;

            Vector3 horizontal = new Vector3(velocity.x, 0f, velocity.z);
            InitialHorizontalDirection = horizontal.sqrMagnitude > 0.0001f ? horizontal.normalized : Vector3.forward;

            float velocityInPerSec = velocity.magnitude * 39.3700787f;
            float spinRevPerSec = weapon.twistRateInchesPerTurn > 0f ? velocityInPerSec / weapon.twistRateInchesPerTurn : 0f;
            SpinRateRadPerSec = 2f * Mathf.PI * spinRevPerSec;
        }

        public float DistanceFromOrigin => Vector3.Distance(Position, Origin);
    }
}

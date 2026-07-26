using UnityEngine;

namespace RealShooter.Ballistics.Visuals
{
    /// Puente entre la simulacion (un Projectile, que es una clase C# simple, no un MonoBehaviour)
    /// y su representacion en escena. Solo sincroniza posicion/orientacion; no calcula fisica.
    public class ProjectileVisual : MonoBehaviour
    {
        private Projectile projectile;

        public void Initialize(Projectile targetProjectile)
        {
            projectile = targetProjectile;
            SyncTransform();
        }

        private void LateUpdate()
        {
            if (projectile == null) return;
            SyncTransform();
        }

        private void SyncTransform()
        {
            transform.position = projectile.Position;
            if (projectile.Velocity.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(projectile.Velocity.normalized);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

namespace RealShooter.Ballistics
{
    /// Gestiona la simulacion en vuelo de los proyectiles: integracion numerica (RK4) de las
    /// fuerzas de gravedad, arrastre y Coriolis, mas la correccion empirica de spin drift.
    /// Es un sistema separado de WeatherManager: solo LEE condiciones atmosfericas de el,
    /// nunca las posee ni las modifica.
    public class PhysicsManager : MonoBehaviour
    {
        public static PhysicsManager Instance { get; private set; }

        [SerializeField] private WeatherManager weatherManager;

        [Tooltip("Subdivisiones de integracion por FixedUpdate. Mas subpasos = mas precision, especialmente cerca del regimen transonico.")]
        [SerializeField] private int integrationSubsteps = 4;

        [Tooltip("Distancia horizontal maxima desde el origen antes de despawnear un proyectil, en metros.")]
        [SerializeField] private float maxRangeMeters = 3000f;

        [Tooltip("Tiempo de vuelo maximo antes de despawnear un proyectil, en segundos.")]
        [SerializeField] private float maxTimeOfFlightSeconds = 30f;

        [Header("Deteccion de impactos")]
        [Tooltip("Whitelist de layers contra las que se detectan impactos. Solo colisiones en estas layers despawnean el proyectil y disparan ProjectileHit.")]
        [SerializeField] private LayerMask impactLayerMask = ~0;

        private readonly List<Projectile> activeProjectiles = new List<Projectile>();

        public IReadOnlyList<Projectile> ActiveProjectiles => activeProjectiles;

        /// Se dispara cuando un proyectil es creado (justo despues de Fire()). Sistemas de
        /// presentacion (visuales, sonido) se suscriben a esto en vez de acoplarse a la simulacion.
        public event Action<Projectile> ProjectileFired;

        /// Se dispara cuando un proyectil deja de simularse (alcance/tiempo maximo, impacto, etc).
        public event Action<Projectile> ProjectileDespawned;

        /// Se dispara especificamente cuando el proyectil impacta un collider en una layer de la whitelist.
        public event Action<Projectile, RaycastHit> ProjectileHit;

        private void Awake()
        {
            Instance = this;
            if (weatherManager == null)
            {
                weatherManager = WeatherManager.Instance != null ? WeatherManager.Instance : FindFirstObjectByType<WeatherManager>();
            }
        }

        /// Dispara un proyectil. bullet/weapon son datos reutilizables (ScriptableObjects);
        /// el Projectile resultante es una instancia de vuelo efimera y propia de esta llamada.
        public Projectile Fire(BulletData bullet, WeaponData weapon, Vector3 origin, Vector3 direction)
        {
            float muzzleVelocity = bullet.GetRandomMuzzleVelocity();
            var projectile = new Projectile(bullet, weapon, origin, direction.normalized * muzzleVelocity);

            float stability = BallisticsMath.GetMillerStabilityFactor(bullet, weapon, muzzleVelocity);
            if (stability < 1.0f)
            {
                Debug.LogWarning($"[PhysicsManager] {bullet.bulletName} con {weapon.weaponName}: factor de estabilidad giroscopica muy bajo ({stability:F2}). La bala probablemente sera inestable en vuelo (twist rate insuficiente).");
            }

            activeProjectiles.Add(projectile);
            ProjectileFired?.Invoke(projectile);
            return projectile;
        }

        private void FixedUpdate()
        {
            if (weatherManager == null || activeProjectiles.Count == 0) return;

            float dt = Time.fixedDeltaTime / integrationSubsteps;

            for (int i = activeProjectiles.Count - 1; i >= 0; i--)
            {
                Projectile projectile = activeProjectiles[i];

                for (int step = 0; step < integrationSubsteps; step++)
                {
                    IntegrateStep(projectile, dt);
                    if (projectile.ShouldDespawn) break;
                }

                if (!projectile.ShouldDespawn && (projectile.TimeOfFlight > maxTimeOfFlightSeconds || projectile.DistanceFromOrigin > maxRangeMeters))
                {
                    projectile.ShouldDespawn = true;
                }

                if (projectile.ShouldDespawn)
                {
                    activeProjectiles.RemoveAt(i);
                    ProjectileDespawned?.Invoke(projectile);
                }
            }
        }

        private void IntegrateStep(Projectile projectile, float dt)
        {
            Vector3 stepStartPosition = projectile.Position;

            // Integracion RK4 sobre el sistema (posicion, velocidad).
            Vector3 p0 = projectile.Position;
            Vector3 v0 = projectile.Velocity;

            Vector3 a1 = ComputeAcceleration(p0, v0, projectile);
            Vector3 v1 = v0 + a1 * (dt * 0.5f);
            Vector3 p1 = p0 + v0 * (dt * 0.5f);

            Vector3 a2 = ComputeAcceleration(p1, v1, projectile);
            Vector3 v2 = v0 + a2 * (dt * 0.5f);
            Vector3 p2 = p0 + v1 * (dt * 0.5f);

            Vector3 a3 = ComputeAcceleration(p2, v2, projectile);
            Vector3 v3 = v0 + a3 * dt;
            Vector3 p3 = p0 + v2 * dt;

            Vector3 a4 = ComputeAcceleration(p3, v3, projectile);

            projectile.Velocity = v0 + (dt / 6f) * (a1 + 2f * a2 + 2f * a3 + a4);
            projectile.Position = p0 + (dt / 6f) * (v0 + 2f * v1 + 2f * v2 + v3);
            projectile.TimeOfFlight += dt;

            ApplySpinDrift(projectile);

            CheckImpact(projectile, stepStartPosition, projectile.Position);
        }

        /// Barrido (raycast) entre la posicion previa y la nueva del proyectil en este paso de
        /// integracion, para no atravesar geometria fina en un solo frame (tunneling). Solo
        /// considera colliders en la whitelist de layers configurada.
        private void CheckImpact(Projectile projectile, Vector3 fromPosition, Vector3 toPosition)
        {
            Vector3 delta = toPosition - fromPosition;
            float distance = delta.magnitude;
            if (distance < 0.0001f) return;

            if (Physics.Raycast(fromPosition, delta.normalized, out RaycastHit hit, distance, impactLayerMask, QueryTriggerInteraction.Ignore))
            {
                projectile.Position = hit.point;
                projectile.ShouldDespawn = true;

                string layerName = LayerMask.LayerToName(hit.collider.gameObject.layer);
                Debug.Log($"[PhysicsManager] {projectile.Bullet.bulletName} impacto '{hit.collider.name}' en layer '{layerName}' a {projectile.DistanceFromOrigin:F1} m del origen.");

                ProjectileHit?.Invoke(projectile, hit);
            }
        }

        private void ApplySpinDrift(Projectile projectile)
        {
            float stability = BallisticsMath.GetMillerStabilityFactor(projectile.Bullet, projectile.Weapon, projectile.Velocity.magnitude);
            float totalDrift = BallisticsMath.GetSpinDriftMeters(stability, projectile.TimeOfFlight);
            float driftDelta = totalDrift - projectile.AccumulatedSpinDriftM;
            projectile.AccumulatedSpinDriftM = totalDrift;

            Vector3 lateralAxis = Vector3.Cross(Vector3.up, projectile.InitialHorizontalDirection);
            float driftSign = projectile.Weapon.twistDirection == TwistDirection.RightHand ? 1f : -1f;
            projectile.Position += lateralAxis * (driftDelta * driftSign);
        }

        private Vector3 ComputeAcceleration(Vector3 position, Vector3 velocity, Projectile projectile)
        {
            Vector3 wind = weatherManager.GetWindAtPosition(position);
            Vector3 velocityRelativeToAir = velocity - wind;

            float airDensity = weatherManager.GetAirDensityKgM3();
            float speedOfSound = weatherManager.GetSpeedOfSoundMps();

            Vector3 dragAccel = BallisticsMath.GetDragAcceleration(velocityRelativeToAir, projectile.Bullet, airDensity, speedOfSound);
            Vector3 gravityAccel = new Vector3(0f, -BallisticsMath.GetGravityMps2(weatherManager.LatitudeDegrees, weatherManager.AltitudeMeters), 0f);
            Vector3 coriolisAccel = BallisticsMath.GetCoriolisAcceleration(velocity, weatherManager.LatitudeDegrees);

            return dragAccel + gravityAccel + coriolisAccel;
        }
    }
}

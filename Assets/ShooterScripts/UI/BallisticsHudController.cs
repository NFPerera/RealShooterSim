using RealShooter.Ballistics;
using TMPro;
using UnityEngine;

namespace RealShooter.UI
{
    /// HUD de diagnostico: muestra en pantalla el estado del ultimo proyectil disparado
    /// (velocidad, distancia recorrida, tiempo de vuelo) para verificar que la simulacion
    /// se comporta como se espera al variar clima/municion/distancia. Es una herramienta de
    /// desarrollo/testeo, no esta pensada para quedar en el juego final.
    public class BallisticsHudController : MonoBehaviour
    {
        [SerializeField] private TMP_Text hudText;

        [Tooltip("Se busca automaticamente en la escena si se deja vacio.")]
        [SerializeField] private PhysicsManager physicsManager;

        [SerializeField] private GameObject scopeGameObject;
        private Projectile trackedProjectile;

        private void Awake()
        {
            if (physicsManager == null)
            {
                physicsManager = FindFirstObjectByType<PhysicsManager>();
            }
        }

        private void OnEnable()
        {
            if (physicsManager == null) return;
            physicsManager.ProjectileFired += HandleProjectileFired;
            physicsManager.ProjectileDespawned += HandleProjectileDespawned;
        }

        private void OnDisable()
        {
            if (physicsManager == null) return;
            physicsManager.ProjectileFired -= HandleProjectileFired;
            physicsManager.ProjectileDespawned -= HandleProjectileDespawned;
        }

        private void HandleProjectileFired(Projectile projectile)
        {
            trackedProjectile = projectile;
        }

        private void HandleProjectileDespawned(Projectile projectile)
        {
            if (trackedProjectile == projectile)
            {
                trackedProjectile = null;
            }
        }

        private void Update()
        {
            if (hudText == null) return;

            if (trackedProjectile == null)
            {
                hudText.text = "Sin proyectil en vuelo";
                return;
            }

            float speed = trackedProjectile.Velocity.magnitude;
            float distance = trackedProjectile.DistanceFromOrigin;
            float timeOfFlight = trackedProjectile.TimeOfFlight;

            hudText.text =
                $"Velocidad: {speed:F1} m/s\n" +
                $"Distancia: {distance:F1} m\n" +
                $"Tiempo de vuelo: {timeOfFlight:F2} s";
        }
        
        
        public void ToggleScope() => scopeGameObject.SetActive(!scopeGameObject.activeSelf);
    }
}

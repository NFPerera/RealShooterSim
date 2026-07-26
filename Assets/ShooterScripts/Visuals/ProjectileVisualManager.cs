using System.Collections.Generic;
using UnityEngine;

namespace RealShooter.Ballistics.Visuals
{
    /// Crea y sincroniza la representacion visual (con estela) de cada proyectil simulado por
    /// PhysicsManager. Separado a proposito de la simulacion fisica: solo escucha los eventos
    /// ProjectileFired/ProjectileDespawned y jamas participa en el calculo de la trayectoria.
    public class ProjectileVisualManager : MonoBehaviour
    {
        [Tooltip("Prefab a instanciar por cada bala disparada (debe incluir un TrailRenderer para la estela). Si se deja vacio, se crea una esfera simple como fallback.")]
        [SerializeField] private GameObject projectileVisualPrefab;

        [Tooltip("Se busca automaticamente en la escena si se deja vacio.")]
        [SerializeField] private PhysicsManager physicsManager;

        [Header("Fallback por defecto (solo si no hay prefab asignado)")]
        [Tooltip("Multiplicador sobre el diametro real de la bala, unicamente para que sea visible (las balas reales miden pocos mm).")]
        [SerializeField] private float defaultVisualScaleMultiplier = 4f;

        [SerializeField] private float trailTime = 3f;
        [SerializeField] private float trailStartWidth = 0.015f;
        [SerializeField] private float trailEndWidth = 0f;
        [SerializeField] private Color trailColor = new Color(1f, 0.65f, 0f);

        private readonly Dictionary<Projectile, GameObject> activeVisuals = new Dictionary<Projectile, GameObject>();

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
            GameObject instance = projectileVisualPrefab != null
                ? Instantiate(projectileVisualPrefab)
                : CreateDefaultVisual(projectile);

            ProjectileVisual visual = instance.GetComponent<ProjectileVisual>();
            if (visual == null) visual = instance.AddComponent<ProjectileVisual>();
            visual.Initialize(projectile);

            activeVisuals[projectile] = instance;
        }

        private void HandleProjectileDespawned(Projectile projectile)
        {
            if (!activeVisuals.TryGetValue(projectile, out GameObject instance))
            {
                return;
            }

            activeVisuals.Remove(projectile);
            if (instance == null) return;

            // Deja de seguir al proyectil (que ya no se simula) y oculta la "bala",
            // pero conserva la estela para que se desvanezca en el punto de impacto/alcance maximo.
            ProjectileVisual visual = instance.GetComponent<ProjectileVisual>();
            if (visual != null) Destroy(visual);

            Renderer bulletRenderer = instance.GetComponentInChildren<Renderer>();
            if (bulletRenderer != null) bulletRenderer.enabled = false;

            TrailRenderer trail = instance.GetComponentInChildren<TrailRenderer>();
            if (trail != null)
            {
                trail.autodestruct = true; // Unity destruye el GameObject solo cuando la estela termina de desvanecerse
            }
            else
            {
                Destroy(instance);
            }
        }

        private GameObject CreateDefaultVisual(Projectile projectile)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = $"ProjectileVisual_{projectile.Bullet.bulletName}";

            Collider col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);

            float diameter = Mathf.Max(projectile.Bullet.DiameterM, 0.005f) * defaultVisualScaleMultiplier;
            go.transform.localScale = Vector3.one * diameter;

            TrailRenderer trail = go.AddComponent<TrailRenderer>();
            trail.time = trailTime;
            trail.startWidth = trailStartWidth;
            trail.endWidth = trailEndWidth;
            trail.minVertexDistance = 0.02f;
            trail.material = CreateDefaultTrailMaterial();
            trail.startColor = trailColor;
            trail.endColor = new Color(trailColor.r, trailColor.g, trailColor.b, 0f);

            return go;
        }

        private static Material CreateDefaultTrailMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            return new Material(shader);
        }
    }
}

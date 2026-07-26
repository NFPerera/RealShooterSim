using RealShooter.Ballistics;
using RealShooter.Interaction;
using RealShooter.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RealShooter.Player
{
    /// El arma como objeto fijo de la escena (montada/atornillada, no hay pickup ni inventario).
    /// El jugador interactua con ella (IInteractable) para ceder temporalmente el control de su
    /// camara: PlayerShooterController reubica la camara en OperatorViewpoint y este script pasa
    /// a manejar apuntado, mira telescopica, disparo y camara de seguimiento de bala hasta que se
    /// presiona la tecla de salida, momento en el que la camara vuelve al jugador.
    public class GunController : MonoBehaviour, IInteractable
    {
        [Header("Interaccion")]
        [Tooltip("Requiere un Collider en este GameObject para que el raycast de PlayerInteractor lo detecte.")]
        [SerializeField] private float interactionRange = 4f;

        [Tooltip("Punto donde se reubica la camara del jugador mientras se opera esta arma. Su rotacion define la mira 'neutra' (yaw/pitch = 0).")]
        [SerializeField] private Transform operatorViewpoint;

        [Tooltip("Tecla para dejar de operar el arma y devolver el control de camara/movimiento al jugador.")]
        [SerializeField] private Key exitOperationKey = Key.E;

        [Header("Disparo")]
        [SerializeField] private BulletData bulletData;
        [SerializeField] private WeaponData weaponData;
        [SerializeField] private Transform muzzlePoint;

        [Tooltip("Se busca automaticamente en la escena si se deja vacio.")]
        [SerializeField] private PhysicsManager physicsManager;

        [Header("Apuntado")]
        [SerializeField] private float mouseSensitivity = 0.1f;
        [SerializeField] private float minPitch = -80f;
        [SerializeField] private float maxPitch = 80f;

        [Header("Mira telescopica")]
        [SerializeField] private float normalFieldOfView = 60f;
        [SerializeField] private float minZoomFactor = 3f;
        [SerializeField] private float maxZoomFactor = 20f;
        [SerializeField] private float scopeZoomScrollSensitivity = 0.01f;

        [Header("Camara de seguimiento de bala")]
        [SerializeField] private Key bulletCamToggleKey = Key.C;
        [SerializeField] private float bulletCamTrailDistance = 2f;
        [SerializeField] private float bulletCamSmoothing = 12f;

        [Header("Torretas de mira (windage / elevation)")]
        [SerializeField] private GameObject visual;
        [SerializeField] private TurretController elevationTurret;
        [SerializeField] private TurretController windageTurret;

        [Tooltip("Distancia maxima del raycast que detecta si se esta mirando una torreta.")]
        [SerializeField] private float turretLookDistance = 2f;

        [SerializeField] private LayerMask turretLayerMask = ~0;

        [SerializeField] private BallisticsHudController hudController;
        public float InteractionRange => interactionRange;

        // 1 mil = 1/1000 rad, exacto por definicion.
        private static readonly float MilToDegrees = 0.001f * Mathf.Rad2Deg;

        private PlayerShooterController controllingPlayer;
        private Transform cameraTransform;
        private Camera playerCamera;
        private bool isBeingOperated;

        private float yaw;
        private float pitch;

        private bool isScoped;
        private float currentZoomFactor;

        private bool bulletCamEnabled;
        private Projectile trackedProjectile;

        private void Awake()
        {
            if (physicsManager == null)
            {
                physicsManager = FindFirstObjectByType<PhysicsManager>();
            }

            currentZoomFactor = minZoomFactor;
        }

        private void OnEnable()
        {
            if (physicsManager != null)
            {
                physicsManager.ProjectileDespawned += HandleProjectileDespawned;
            }
        }

        private void OnDisable()
        {
            if (physicsManager != null)
            {
                physicsManager.ProjectileDespawned -= HandleProjectileDespawned;
            }
        }

        public void Interact(GameObject interactor)
        {
            if (isBeingOperated) return;

            PlayerShooterController player = interactor.GetComponent<PlayerShooterController>();
            if (player == null) return;

            player.EnterGunOperation(this);
        }

        /// Llamado por PlayerShooterController al ceder el control de su camara.
        public void BeginOperation(PlayerShooterController player, Transform camera)
        {
            controllingPlayer = player;
            cameraTransform = camera;
            playerCamera = camera.GetComponent<Camera>();

            cameraTransform.SetParent(operatorViewpoint, false);
            cameraTransform.localPosition = Vector3.zero;
            cameraTransform.localRotation = Quaternion.identity;

            yaw = 0f;
            pitch = 0f;
            isScoped = false;
            ApplyFieldOfView();

            isBeingOperated = true;
        }

        private void Update()
        {
            if (!isBeingOperated) return;

            if (Keyboard.current != null && Keyboard.current[exitOperationKey].wasPressedThisFrame)
            {
                EndOperation();
                return;
            }

            HandleBulletCamToggle();

            if (trackedProjectile != null)
            {
                // Mientras la camara sigue a la bala, se suspende el apuntado/disparo normal.
                HandleBulletCamFollow();
                return;
            }

            HandleAim();
            HandleScopeToggle();

            // Si se esta mirando una torreta, el scroll la ajusta a ella en vez de hacer zoom.
            bool scrollHandledByTurret = HandleTurretAdjustment();
            if (!scrollHandledByTurret)
            {
                HandleScopeZoom();
            }

            HandleFire();
        }

        private void EndOperation()
        {
            isBeingOperated = false;
            isScoped = false;
            ApplyFieldOfView();
            trackedProjectile = null;

            PlayerShooterController player = controllingPlayer;
            controllingPlayer = null;
            cameraTransform = null;
            playerCamera = null;

            player.ExitGunOperation();
        }

        private void HandleAim()
        {
            if (Mouse.current == null) return;

            // Al estar en la mira, reducir la sensibilidad en proporcion al zoom actual:
            // sin esto, apuntar con mucho aumento seria imposible de controlar.
            float sensitivityScale = isScoped && currentZoomFactor > 0f ? 1f / currentZoomFactor : 1f;

            Vector2 lookDelta = Mouse.current.delta.ReadValue();
            yaw += lookDelta.x * mouseSensitivity * sensitivityScale;
            pitch -= lookDelta.y * mouseSensitivity * sensitivityScale;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

            cameraTransform.localRotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        private void HandleScopeToggle()
        {
            if (Mouse.current == null || !Mouse.current.rightButton.wasPressedThisFrame) return;

            isScoped = !isScoped;
            
            hudController.ToggleScope();
            visual.SetActive(!isScoped);
            
            ApplyFieldOfView();
        }

        private void HandleScopeZoom()
        {
            if (!isScoped || Mouse.current == null) return;

            float scroll = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Approximately(scroll, 0f)) return;

            // Sumar: scrollear "hacia adelante" (positivo) aumenta el zoom (x3 -> x20).
            currentZoomFactor += scroll * scopeZoomScrollSensitivity;
            currentZoomFactor = Mathf.Clamp(currentZoomFactor, minZoomFactor, maxZoomFactor);
            ApplyFieldOfView();
        }

        private void ApplyFieldOfView()
        {
            if (playerCamera == null) return;
            // El FOV se relaciona con el zoom de forma inversa: FOV_normal / factor de zoom.
            playerCamera.fieldOfView = isScoped ? normalFieldOfView / currentZoomFactor : normalFieldOfView;
        }

        /// Detecta si la mira esta apuntando a una torreta (u otro IScrollInteractable) y le
        /// reenvia el scroll de este frame. Devuelve true si algo lo consumio, para que
        /// HandleScopeZoom no lo use tambien. Misma logica que PlayerInteractor usa mirando el
        /// arma desde afuera, via ScrollInteractionUtility.
        private bool HandleTurretAdjustment()
        {
            if (Mouse.current == null) return false;

            float scroll = Mouse.current.scroll.ReadValue().y;
            return ScrollInteractionUtility.TryHandleScroll(cameraTransform.position, cameraTransform.forward, turretLookDistance, turretLayerMask, scroll);
        }

        /// Direccion de disparo corregida por el zero shift de las torretas: elevacion inclina
        /// el impacto de punto de mira hacia arriba, windage hacia la derecha, ambos relativos
        /// al eje de apuntado actual (no a los ejes del mundo).
        private Vector3 GetCorrectedAimDirection()
        {
            float elevationDeg = elevationTurret != null ? -elevationTurret.CurrentMil * MilToDegrees : 0f;
            float windageDeg = windageTurret != null ? windageTurret.CurrentMil * MilToDegrees : 0f;

            if (Mathf.Approximately(elevationDeg, 0f) && Mathf.Approximately(windageDeg, 0f))
            {
                return cameraTransform.forward;
            }

            Quaternion correction = Quaternion.Euler(elevationDeg, windageDeg, 0f);
            return cameraTransform.rotation * correction * Vector3.forward;
        }

        private void HandleBulletCamToggle()
        {
            if (Keyboard.current == null || !Keyboard.current[bulletCamToggleKey].wasPressedThisFrame) return;
            bulletCamEnabled = !bulletCamEnabled;
        }

        private void HandleBulletCamFollow()
        {
            Vector3 velocity = trackedProjectile.Velocity;
            Vector3 targetPosition = trackedProjectile.Position - velocity.normalized * bulletCamTrailDistance;
            Quaternion targetRotation = velocity.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(velocity.normalized)
                : cameraTransform.rotation;

            float t = 1f - Mathf.Exp(-bulletCamSmoothing * Time.deltaTime);
            cameraTransform.position = Vector3.Lerp(cameraTransform.position, targetPosition, t);
            cameraTransform.rotation = Quaternion.Slerp(cameraTransform.rotation, targetRotation, t);
        }

        private void HandleProjectileDespawned(Projectile projectile)
        {
            if (trackedProjectile != projectile) return;

            trackedProjectile = null;
            if (cameraTransform == null) return;

            cameraTransform.localPosition = Vector3.zero;
            cameraTransform.localRotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        private void HandleFire()
        {
            if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return;
            Fire();
        }

        private void Fire()
        {
            if (physicsManager == null)
            {
                Debug.LogWarning("[GunController] No se encontro un PhysicsManager en la escena.");
                return;
            }

            if (bulletData == null || weaponData == null)
            {
                Debug.LogWarning("[GunController] Falta asignar BulletData o WeaponData.");
                return;
            }

            Vector3 origin = muzzlePoint != null ? muzzlePoint.position : cameraTransform.position;
            Vector3 direction = GetCorrectedAimDirection();

            Projectile projectile = physicsManager.Fire(bulletData, weaponData, origin, direction);

            if (bulletCamEnabled)
            {
                trackedProjectile = projectile;
            }
        }
    }
}

using RealShooter.Ballistics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RealShooter.Player
{
    /// Controlador del jugador: mira con el mouse (estilo shooter en primera persona) y dispara
    /// en la direccion a la que apunta la camara, delegando el vuelo real de la bala a PhysicsManager.
    /// Requiere el nuevo Input System (activo en este proyecto).
    [RequireComponent(typeof(CharacterController))]
    public class PlayerShooterController : MonoBehaviour
    {
        [Header("Camara")]
        [Tooltip("Camara del jugador, hija de este transform. El yaw se aplica a este objeto, el pitch a la camara.")]
        [SerializeField] private Transform cameraTransform;

        [Tooltip("Sensibilidad del mouse (grados por pixel de movimiento)")]
        [SerializeField] private float mouseSensitivity = 0.1f;

        [SerializeField] private float minPitch = -80f;
        [SerializeField] private float maxPitch = 80f;

        [Header("Disparo")]
        [SerializeField] private BulletData bulletData;
        [SerializeField] private WeaponData weaponData;

        [Tooltip("Punto de origen de la bala. Si se deja vacio, se usa la posicion de la camara.")]
        [SerializeField] private Transform muzzlePoint;

        [Tooltip("Se busca automaticamente en la escena si se deja vacio.")]
        [SerializeField] private PhysicsManager physicsManager;

        [Header("Movimiento")]
        [Tooltip("Velocidad de desplazamiento en m/s")]
        [SerializeField] private float moveSpeed = 4f;

        [Header("Mira telescopica")]
        [Tooltip("Zoom minimo de la mira (ej: 3 = x3 de aumento)")]
        [SerializeField] private float minZoomFactor = 3f;

        [Tooltip("Zoom maximo de la mira (ej: 20 = x20 de aumento)")]
        [SerializeField] private float maxZoomFactor = 20f;

        [Tooltip("Sensibilidad de la rueda del mouse para variar el zoom (en 'x' por unidad de scroll) mientras la mira esta activa.")]
        [SerializeField] private float scopeZoomScrollSensitivity = 0.01f;

        [Header("Camara de seguimiento de bala")]
        [Tooltip("Tecla para activar/desactivar que la camara siga a la bala mientras vuela.")]
        [SerializeField] private Key bulletCamToggleKey = Key.C;

        [Tooltip("Distancia detras de la bala a la que se posiciona la camara mientras la sigue.")]
        [SerializeField] private float bulletCamTrailDistance = 2f;

        [Tooltip("Suavizado de la camara seguidora (mas alto = camara mas 'pegada' a la bala).")]
        [SerializeField] private float bulletCamSmoothing = 12f;

        private CharacterController characterController;
        private Camera playerCamera;
        private float normalFieldOfView;
        private bool isScoped;
        private float currentZoomFactor;

        private bool bulletCamEnabled;
        private Projectile trackedProjectile;
        private Vector3 cameraRestLocalPosition;

        private float yaw;
        private float pitch;

        private void Awake()
        {
            if (cameraTransform == null && Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }

            if (cameraTransform == null)
            {
                Debug.LogError("[PlayerShooterController] No hay camara asignada ni Camera.main en la escena.");
                enabled = false;
                return;
            }

            if (physicsManager == null)
            {
                physicsManager = FindFirstObjectByType<PhysicsManager>();
            }

            characterController = GetComponent<CharacterController>();

            playerCamera = cameraTransform.GetComponent<Camera>();
            if (playerCamera == null)
            {
                Debug.LogWarning("[PlayerShooterController] La camara no tiene un componente Camera; la mira telescopica no funcionara.");
            }
            else
            {
                normalFieldOfView = playerCamera.fieldOfView;
            }

            currentZoomFactor = minZoomFactor;
            cameraRestLocalPosition = cameraTransform.localPosition;

            yaw = transform.eulerAngles.y;
            pitch = NormalizeAngle(cameraTransform.localEulerAngles.x);

            LockCursor();
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

        private void Update()
        {
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                {
                    LockCursor();
                }
                return;
            }

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                UnlockCursor();
                return;
            }

            HandleBulletCamToggle();

            if (trackedProjectile != null)
            {
                // Mientras la camara sigue a la bala, se suspende el control normal del jugador.
                HandleBulletCamFollow();
                return;
            }

            HandleLook();
            HandleMovement();
            HandleScopeToggle();
            HandleScopeZoom();
            HandleFire();
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
            cameraTransform.localPosition = cameraRestLocalPosition;
            cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        private void HandleLook()
        {
            if (Mouse.current == null) return;

            // Al estar en la mira, reducir la sensibilidad en proporcion al zoom actual:
            // sin esto, apuntar con mucho aumento seria imposible de controlar.
            float sensitivityScale = isScoped && currentZoomFactor > 0f ? 1f / currentZoomFactor : 1f;

            Vector2 lookDelta = Mouse.current.delta.ReadValue();
            yaw += lookDelta.x * mouseSensitivity * sensitivityScale;
            pitch -= lookDelta.y * mouseSensitivity * sensitivityScale;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

            transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        private void HandleMovement()
        {
            if (characterController == null || Keyboard.current == null) return;

            Vector2 input = Vector2.zero;
            if (Keyboard.current.wKey.isPressed) input.y += 1f;
            if (Keyboard.current.sKey.isPressed) input.y -= 1f;
            if (Keyboard.current.dKey.isPressed) input.x += 1f;
            if (Keyboard.current.aKey.isPressed) input.x -= 1f;
            input = Vector2.ClampMagnitude(input, 1f);

            Vector3 moveDirection = transform.right * input.x + transform.forward * input.y;
            characterController.SimpleMove(moveDirection * moveSpeed);
        }

        private void HandleScopeToggle()
        {
            if (Mouse.current == null || !Mouse.current.rightButton.wasPressedThisFrame) return;

            isScoped = !isScoped;
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

        private void HandleFire()
        {
            if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return;
            Fire();
        }

        private void Fire()
        {
            if (physicsManager == null)
            {
                Debug.LogWarning("[PlayerShooterController] No se encontro un PhysicsManager en la escena.");
                return;
            }

            if (bulletData == null || weaponData == null)
            {
                Debug.LogWarning("[PlayerShooterController] Falta asignar BulletData o WeaponData.");
                return;
            }

            Vector3 origin = muzzlePoint != null ? muzzlePoint.position : cameraTransform.position;
            Vector3 direction = cameraTransform.forward;

            Projectile projectile = physicsManager.Fire(bulletData, weaponData, origin, direction);

            if (bulletCamEnabled)
            {
                trackedProjectile = projectile;
            }
        }

        private void LockCursor()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void UnlockCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private static float NormalizeAngle(float angle)
        {
            return angle > 180f ? angle - 360f : angle;
        }
    }
}

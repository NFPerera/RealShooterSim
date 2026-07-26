using UnityEngine;
using UnityEngine.InputSystem;

namespace RealShooter.Player
{
    /// Controlador del jugador: mira con el mouse (estilo shooter en primera persona) y se mueve
    /// con WASD. Toda la logica de armas (disparo, mira telescopica, camara de seguimiento de bala)
    /// vive en GunController, un objeto fijo separado en la escena — el jugador solo cede
    /// temporalmente el control de su camara mientras opera un arma (ver EnterGunOperation /
    /// ExitGunOperation, invocados por GunController a traves de PlayerInteractor/IInteractable).
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

        [Header("Movimiento")]
        [Tooltip("Velocidad de desplazamiento en m/s")]
        [SerializeField] private float moveSpeed = 4f;

        private CharacterController characterController;
        private PlayerInteractor playerInteractor;

        private GunController activeGun;
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

            characterController = GetComponent<CharacterController>();
            playerInteractor = GetComponent<PlayerInteractor>();

            cameraRestLocalPosition = cameraTransform.localPosition;

            yaw = transform.eulerAngles.y;
            pitch = NormalizeAngle(cameraTransform.localEulerAngles.x);

            LockCursor();
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

            if (activeGun != null) return; // el GunController activo maneja su propio Update mientras dura la operacion

            HandleLook();
            HandleMovement();
        }

        /// Llamado por un GunController cuando el jugador interactua con el (ver PlayerInteractor/IInteractable).
        public void EnterGunOperation(GunController gun)
        {
            if (activeGun != null) return;

            activeGun = gun;
            if (playerInteractor != null) playerInteractor.enabled = false;

            gun.BeginOperation(this, cameraTransform);
        }

        /// Llamado por el GunController activo al terminar la operacion (tecla de salida).
        public void ExitGunOperation()
        {
            activeGun = null;
            if (playerInteractor != null) playerInteractor.enabled = true;

            cameraTransform.SetParent(transform, false);
            cameraTransform.localPosition = cameraRestLocalPosition;
            cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        private void HandleLook()
        {
            if (Mouse.current == null) return;

            Vector2 lookDelta = Mouse.current.delta.ReadValue();
            yaw += lookDelta.x * mouseSensitivity;
            pitch -= lookDelta.y * mouseSensitivity;
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

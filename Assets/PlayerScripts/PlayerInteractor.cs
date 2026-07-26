using RealShooter.Interaction;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RealShooter.Player
{
    /// Detecta objetos IInteractable mirados por la camara del jugador y dispara su interaccion
    /// al presionar la tecla configurada. Generico a proposito: no sabe nada de armas
    /// especificamente, cualquier IInteractable futuro (puertas, items, paneles, etc.) funciona igual.
    /// Se desactiva mientras el jugador esta operando un arma (ver PlayerShooterController).
    public class PlayerInteractor : MonoBehaviour
    {
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private Key interactKey = Key.E;

        [Tooltip("Techo de distancia del raycast. El rango real de cada IInteractable (InteractionRange) se compara aparte, este valor solo debe ser mayor o igual que el mas largo que uses.")]
        [SerializeField] private float maxInteractionDistance = 6f;

        [SerializeField] private LayerMask interactableLayerMask = ~0;

        private void Update()
        {
            if (cameraTransform == null || Keyboard.current == null) return;
            if (!Keyboard.current[interactKey].wasPressedThisFrame) return;

            if (!Physics.Raycast(cameraTransform.position, cameraTransform.forward, out RaycastHit hit, maxInteractionDistance, interactableLayerMask, QueryTriggerInteraction.Collide))
            {
                return;
            }

            if (hit.collider.TryGetComponent(out IInteractable interactable) && hit.distance <= interactable.InteractionRange)
            {
                interactable.Interact(gameObject);
            }
        }
    }
}

using RealShooter.Interaction;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RealShooter.Player
{
    /// Detecta objetos IInteractable mirados por la camara del jugador y dispara su interaccion
    /// al presionar la tecla configurada; ademas, reenvia el scroll del mouse a cualquier
    /// IScrollInteractable mirado (sin necesidad de tecla) — p.ej. las torretas de una mira,
    /// ajustables tanto mirandolas desde afuera del arma como operandola (ver GunController,
    /// que usa la misma ScrollInteractionUtility). Generico a proposito: no sabe nada de armas
    /// especificamente, cualquier interactuable futuro (puertas, items, paneles, etc.) funciona igual.
    /// Se desactiva mientras el jugador esta operando un arma (ver PlayerShooterController).
    public class PlayerInteractor : MonoBehaviour
    {
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private Key interactKey = Key.E;

        [Tooltip("Techo de distancia del raycast. El rango real de cada IInteractable (InteractionRange) se compara aparte, este valor solo debe ser mayor o igual que el mas largo que uses.")]
        [SerializeField] private float maxInteractionDistance = 6f;

        [SerializeField] private LayerMask interactableLayerMask = ~0;

        [Tooltip("Layers de objetos IScrollInteractable (p.ej. torretas) detectables sin presionar tecla, solo mirandolos y scrolleando.")]
        [SerializeField] private LayerMask scrollInteractableLayerMask = ~0;

        [SerializeField] private float maxScrollInteractionDistance = 3f;

        private void Update()
        {
            if (cameraTransform == null) return;

            HandleKeyInteraction();
            HandleScrollInteraction();
        }

        private void HandleKeyInteraction()
        {
            if (Keyboard.current == null || !Keyboard.current[interactKey].wasPressedThisFrame) return;

            if (!Physics.Raycast(cameraTransform.position, cameraTransform.forward, out RaycastHit hit, maxInteractionDistance, interactableLayerMask, QueryTriggerInteraction.Collide))
            {
                return;
            }

            if (hit.collider.TryGetComponent(out IInteractable interactable) && hit.distance <= interactable.InteractionRange)
            {
                interactable.Interact(gameObject);
            }
        }

        private void HandleScrollInteraction()
        {
            if (Mouse.current == null) return;

            float scroll = Mouse.current.scroll.ReadValue().y;
            ScrollInteractionUtility.TryHandleScroll(cameraTransform.position, cameraTransform.forward, maxScrollInteractionDistance, scrollInteractableLayerMask, scroll);
        }
    }
}

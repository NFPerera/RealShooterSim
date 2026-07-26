using UnityEngine;

namespace RealShooter.Interaction
{
    /// Deteccion compartida para IScrollInteractable: la usan tanto PlayerInteractor (mirando el
    /// objeto desde afuera, sin operar nada) como GunController (mirando a traves de la mira
    /// mientras se opera el arma), para no duplicar el mismo raycast en los dos lugares.
    public static class ScrollInteractionUtility
    {
        /// Intenta reenviar el scroll de este frame al IScrollInteractable mirado desde el origen
        /// dado. Devuelve true si algo lo consumio (por ejemplo, para que el llamador no haga
        /// zoom ese mismo frame si el scroll ya fue usado por una torreta).
        public static bool TryHandleScroll(Vector3 originPosition, Vector3 originForward, float maxDistance, LayerMask layerMask, float scrollDelta)
        {
            if (Mathf.Approximately(scrollDelta, 0f)) return false;
            if (!Physics.Raycast(originPosition, originForward, out RaycastHit hit, maxDistance, layerMask)) return false;
            if (!hit.collider.TryGetComponent(out IScrollInteractable interactable)) return false;
            if (hit.distance > interactable.InteractionRange) return false;

            interactable.OnScroll(scrollDelta);
            return true;
        }
    }
}

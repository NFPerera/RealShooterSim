using UnityEngine;

namespace RealShooter.Interaction
{
    /// Contrato generico para cualquier objeto de la escena con el que el jugador pueda
    /// interactuar mirandolo y presionando la tecla de interaccion (ver PlayerInteractor).
    /// No es especifico de armas: pensado para reutilizarse en futuros objetos interactuables.
    public interface IInteractable
    {
        /// Distancia maxima (metros) a la que este objeto puede interactuarse.
        float InteractionRange { get; }

        void Interact(GameObject interactor);
    }
}

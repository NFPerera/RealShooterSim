namespace RealShooter.Interaction
{
    /// Contrato para objetos que reaccionan a la rueda del mouse mientras el jugador los mira,
    /// sin necesidad de presionar una tecla (a diferencia de IInteractable). Pensado para
    /// controles continuos como las torretas de la mira, utilizables tanto mirandolas desde
    /// afuera del arma como a traves de la mira mientras se la opera.
    public interface IScrollInteractable
    {
        /// Distancia maxima (metros) a la que este objeto reacciona al scroll.
        float InteractionRange { get; }

        /// scrollDelta es el valor crudo del eje Y de la rueda del mouse de este frame
        /// (el signo importa, la magnitud no: quien la implementa decide el paso por "click").
        void OnScroll(float scrollDelta);
    }
}

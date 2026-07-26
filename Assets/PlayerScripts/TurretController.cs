using RealShooter.Interaction;
using UnityEngine;

namespace RealShooter.Player
{
    public enum TurretAxis
    {
        Windage,
        Elevation
    }

    /// Una torreta individual de la mira (windage o elevation). El jugador la mira y ajusta su
    /// valor con clicks discretos de la rueda del mouse, igual que una torreta mecanica real:
    /// cada muesca de scroll es exactamente un click, no hay ajuste analogico/proporcional a la
    /// velocidad de scroll. Implementa IScrollInteractable para que tanto PlayerInteractor
    /// (mirandola desde afuera del arma) como GunController (mirando a traves de la mira mientras
    /// se opera) puedan detectarla y ajustarla con la misma logica compartida.
    public class TurretController : MonoBehaviour, IScrollInteractable
    {
        [SerializeField] private TurretAxis axis = TurretAxis.Elevation;

        [Tooltip("Ajuste angular por click, en miliradianes (mil). 0.1 mil es el estandar habitual en torretas modernas.")]
        [SerializeField] private float milPerClick = 0.1f;

        [Tooltip("Recorrido maximo hacia cada lado desde el cero, en mil.")]
        [SerializeField] private float maxTravelMil = 10f;

        [Tooltip("Distancia maxima a la que se puede ajustar esta torreta mirandola (adentro o afuera del arma).")]
        [SerializeField] private float interactionRange = 3f;

        public TurretAxis Axis => axis;
        public float CurrentMil { get; private set; }
        public float InteractionRange => interactionRange;

        /// 1 mil = 1/1000 de radian, exacto por definicion.
        public float CurrentRadians => CurrentMil * 0.001f;

        /// Aplica un click en la direccion indicada (+1 o -1), clampeado al recorrido maximo.
        public void ApplyClick(int direction)
        {
            CurrentMil = Mathf.Clamp(CurrentMil + direction * milPerClick, -maxTravelMil, maxTravelMil);

            Debug.Log($"{axis} = {CurrentMil} mils");
        }

        /// Implementacion de IScrollInteractable: cada muesca de scroll es un click discreto,
        /// sin importar la magnitud cruda del valor (asi el "feel" es identico adentro y afuera).
        public void OnScroll(float scrollDelta)
        {
            ApplyClick(scrollDelta > 0f ? 1 : -1);
        }
    }
}

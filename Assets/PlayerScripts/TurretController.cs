using UnityEngine;

namespace RealShooter.Player
{
    public enum TurretAxis
    {
        Windage,
        Elevation
    }

    /// Una torreta individual de la mira (windage o elevation). El jugador la mira mientras
    /// opera el arma (GunController hace el raycast) y ajusta su valor con clicks discretos de
    /// la rueda del mouse, igual que una torreta mecanica real: cada muesca de scroll es
    /// exactamente un click, no hay ajuste analogico/proporcional a la velocidad de scroll.
    public class TurretController : MonoBehaviour
    {
        [SerializeField] private TurretAxis axis = TurretAxis.Elevation;

        [Tooltip("Ajuste angular por click, en miliradianes (mil). 0.1 mil es el estandar habitual en torretas modernas.")]
        [SerializeField] private float milPerClick = 0.1f;

        [Tooltip("Recorrido maximo hacia cada lado desde el cero, en mil.")]
        [SerializeField] private float maxTravelMil = 10f;

        public TurretAxis Axis => axis;
        public float CurrentMil { get; private set; }

        /// 1 mil = 1/1000 de radian, exacto por definicion.
        public float CurrentRadians => CurrentMil * 0.001f;

        /// Aplica un click en la direccion indicada (+1 o -1), clampeado al recorrido maximo.
        public void ApplyClick(int direction)
        {
            CurrentMil = Mathf.Clamp(CurrentMil + direction * milPerClick, -maxTravelMil, maxTravelMil);
            
            Debug.Log($"{axis} = {CurrentMil} mils");
        }
    }
}

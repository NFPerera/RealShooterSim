using UnityEngine;

namespace RealShooter.Ballistics
{
    public enum TwistDirection
    {
        RightHand,
        LeftHand
    }

    [CreateAssetMenu(fileName = "NewWeaponData", menuName = "RealShooter/Ballistics/Weapon Data")]
    public class WeaponData : ScriptableObject
    {
        [Header("Identificacion")]
        public string weaponName = "Rifle generico";

        [Header("Cañon")]
        [Tooltip("Longitud del cañon en mm (referencia para futuras correcciones de velocidad de salida)")]
        public float barrelLengthMm = 610f;

        [Tooltip("Paso de estriado en pulgadas por vuelta completa (ej: estriado 1:10\" -> 10)")]
        public float twistRateInchesPerTurn = 10f;

        [Tooltip("Sentido de giro del estriado. Determina la direccion de la deriva giroscopica (spin drift).")]
        public TwistDirection twistDirection = TwistDirection.RightHand;

        [Header("Municion de referencia")]
        [Tooltip("Bala usada por defecto para pruebas/validacion. En tiempo real el arma puede disparar cualquier BulletData compatible con su recamara.")]
        public BulletData defaultBullet;
    }
}

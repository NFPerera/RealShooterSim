using UnityEngine;

namespace RealShooter.Ballistics
{
    public enum DragModelType
    {
        G1,
        G7
    }

    [CreateAssetMenu(fileName = "NewBulletData", menuName = "RealShooter/Ballistics/Bullet Data")]
    public class BulletData : ScriptableObject
    {
        [Header("Identificacion")]
        public string bulletName = "Bala generica";

        [Header("Geometria y masa")]
        [Tooltip("Masa del proyectil en grains (1 grain = 0.0648 g)")]
        public float massGrains = 175f;

        [Tooltip("Diametro del proyectil en milimetros (calibre)")]
        public float diameterMm = 7.82f;

        [Tooltip("Longitud del proyectil en milimetros")]
        public float lengthMm = 33f;

        [Header("Velocidad inicial")]
        [Tooltip("Velocidad de salida (boca de cañon) en m/s")]
        public float muzzleVelocityMps = 800f;

        [Tooltip("Desviacion estandar de la velocidad, disparo a disparo (m/s). Simula variacion de lote de municion.")]
        public float muzzleVelocitySdMps = 3f;

        [Header("Coeficiente balistico")]
        [Tooltip("Modelo de arrastre estandar de referencia (G7 recomendado para balas boat-tail modernas de largo alcance)")]
        public DragModelType dragModel = DragModelType.G7;

        [Tooltip("Coeficiente balistico publicado por el fabricante, en unidades estandar de la industria (lb/in^2). Ej: 0.223")]
        public float ballisticCoefficient = 0.223f;

        public float MassKg => massGrains * 0.00006479891f;
        public float DiameterM => diameterMm * 0.001f;
        public float LengthM => lengthMm * 0.001f;
        public float CrossSectionalAreaM2 => Mathf.PI * (DiameterM * 0.5f) * (DiameterM * 0.5f);

        /// Densidad seccional en lb/in^2 (convencion imperial estandar usada para publicar BCs).
        public float SectionalDensityImperial
        {
            get
            {
                float massLb = massGrains / 7000f; // 7000 grains = 1 lb exacto
                float diameterIn = diameterMm / 25.4f;
                return massLb / (diameterIn * diameterIn);
            }
        }

        /// Factor de forma i = SD / BC. Escala la tabla de arrastre estandar (G1/G7) al Cd real de esta bala especifica.
        public float FormFactor => ballisticCoefficient > 0.0001f ? SectionalDensityImperial / ballisticCoefficient : 1f;

        /// Longitud del proyectil expresada en calibres (usado en el calculo de estabilidad giroscopica).
        public float LengthInCalibers => diameterMm > 0.0001f ? lengthMm / diameterMm : 0f;

        /// Devuelve una velocidad de salida con variacion gaussiana disparo a disparo (Box-Muller).
        public float GetRandomMuzzleVelocity()
        {
            float u1 = Random.value;
            float u2 = Random.value;
            float gaussian = Mathf.Sqrt(-2f * Mathf.Log(Mathf.Max(u1, 1e-6f))) * Mathf.Cos(2f * Mathf.PI * u2);
            return muzzleVelocityMps + gaussian * muzzleVelocitySdMps;
        }
    }
}

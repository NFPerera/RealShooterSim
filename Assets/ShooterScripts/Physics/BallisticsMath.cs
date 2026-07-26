using UnityEngine;

namespace RealShooter.Ballistics
{
    /// Funciones puras con las formulas fisicas/empiricas de balistica exterior.
    /// No depende de MonoBehaviour: recibe todo lo que necesita por parametro,
    /// para poder testearse y reutilizarse sin acoplarse a PhysicsManager ni WeatherManager.
    public static class BallisticsMath
    {
        private const float EarthAngularVelocityRadPerSec = 7.2921159e-5f;

        /// Gravedad efectiva segun latitud (formula internacional de gravedad, 1980) y altitud.
        public static float GetGravityMps2(float latitudeDegrees, float altitudeMeters)
        {
            float phi = latitudeDegrees * Mathf.Deg2Rad;
            float sinPhi2 = Mathf.Sin(phi) * Mathf.Sin(phi);
            float sin2Phi2 = Mathf.Sin(2f * phi) * Mathf.Sin(2f * phi);
            float gLatitude = 9.780327f * (1f + 0.0053024f * sinPhi2 - 0.0000058f * sin2Phi2);

            const float earthRadiusM = 6371000f;
            return gLatitude * (1f - 2f * altitudeMeters / earthRadiusM);
        }

        /// Vector de rotacion terrestre en el sistema de coordenadas del mundo (X=Este, Y=Arriba, Z=Norte).
        public static Vector3 GetEarthAngularVelocityVector(float latitudeDegrees)
        {
            float phi = latitudeDegrees * Mathf.Deg2Rad;
            return EarthAngularVelocityRadPerSec * new Vector3(0f, Mathf.Sin(phi), Mathf.Cos(phi));
        }

        /// Aceleracion de Coriolis: a = -2 * (Omega x v).
        public static Vector3 GetCoriolisAcceleration(Vector3 velocity, float latitudeDegrees)
        {
            Vector3 omega = GetEarthAngularVelocityVector(latitudeDegrees);
            return -2f * Vector3.Cross(omega, velocity);
        }

        /// Aceleracion de arrastre aerodinamico. Escala la tabla de arrastre estandar (G1/G7)
        /// por el factor de forma real de la bala (derivado de su BC publicado) y calcula
        /// la fuerza a partir de la masa y el area transversal reales del proyectil.
        public static Vector3 GetDragAcceleration(Vector3 velocityRelativeToAir, BulletData bullet, float airDensity, float speedOfSound)
        {
            float speed = velocityRelativeToAir.magnitude;
            if (speed < 0.01f) return Vector3.zero;

            float mach = speed / speedOfSound;
            float standardCd = DragTables.GetStandardCd(bullet.dragModel, mach);
            float actualCd = standardCd * bullet.FormFactor;

            float dragForce = 0.5f * airDensity * speed * speed * actualCd * bullet.CrossSectionalAreaM2;
            float dragDeceleration = dragForce / bullet.MassKg;

            return -velocityRelativeToAir.normalized * dragDeceleration;
        }

        /// Modelo simplificado del efecto Magnus (fuerza lateral por giro + angulo de ataque).
        /// No existe una tabla estandar universal de coeficiente Magnus como si existe para el arrastre;
        /// esto es una aproximacion empirica de orden de magnitud, expuesta para uso avanzado (p.ej. un
        /// futuro modelo 6-DOF). El modelo activo por defecto en PhysicsManager usa en cambio la
        /// correccion empirica de "spin drift" (ver GetSpinDriftMeters), que es la practica estandar
        /// en calculadoras balisticas no-6DOF y evita contar dos veces el mismo efecto fisico.
        public static Vector3 GetMagnusAcceleration(Vector3 velocityRelativeToAir, Vector3 spinAxis, float spinRateRadPerSec, BulletData bullet, float airDensity)
        {
            const float magnusCoefficient = 0.00002f;
            Vector3 spinVector = spinAxis.normalized * spinRateRadPerSec;
            Vector3 magnusForceDirection = Vector3.Cross(spinVector, velocityRelativeToAir);

            float magnitude = magnusCoefficient * airDensity * velocityRelativeToAir.magnitude * bullet.DiameterM * bullet.DiameterM;
            return magnusForceDirection * (magnitude / bullet.MassKg);
        }

        /// Factor de estabilidad giroscopica (formula simplificada de Miller, con correccion de velocidad).
        /// SG >= 1.4 se considera estable; por debajo, la bala puede desestabilizarse (tumbling) en vuelo.
        public static float GetMillerStabilityFactor(BulletData bullet, WeaponData weapon, float currentVelocityMps)
        {
            float diameterIn = bullet.diameterMm / 25.4f;
            if (diameterIn < 0.0001f || weapon.twistRateInchesPerTurn < 0.0001f) return 0f;

            float twistCalibers = weapon.twistRateInchesPerTurn / diameterIn;
            float lengthCalibers = bullet.LengthInCalibers;

            float sg = (30f * bullet.massGrains) /
                       (twistCalibers * twistCalibers * diameterIn * diameterIn * diameterIn * lengthCalibers * (1f + lengthCalibers * lengthCalibers));

            float velocityFps = currentVelocityMps * 3.28084f;
            sg *= Mathf.Pow(velocityFps / 2800f, 1f / 3f);

            return sg;
        }

        /// Deriva lateral acumulada por el acoplamiento giroscopico/Magnus (formula empirica de Litz), en metros.
        /// Se aplica como correccion directa sobre la posicion en funcion del tiempo de vuelo, en vez de
        /// simular el acoplamiento Magnus-giroscopico completo (que requeriria un modelo 6-DOF de actitud).
        public static float GetSpinDriftMeters(float stabilityFactor, float timeOfFlightSeconds)
        {
            if (timeOfFlightSeconds <= 0f) return 0f;
            float driftInches = 1.25f * (stabilityFactor + 1.2f) * Mathf.Pow(timeOfFlightSeconds, 1.83f);
            return driftInches * 0.0254f;
        }
    }
}

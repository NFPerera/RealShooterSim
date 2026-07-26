using UnityEngine;

namespace RealShooter.Ballistics
{
    /// Gestiona las condiciones atmosfericas y ambientales del escenario (temperatura, presion,
    /// humedad, viento, latitud). No sabe nada de proyectiles ni integracion de trayectorias:
    /// eso es responsabilidad de PhysicsManager, que solo LEE datos de aqui.
    public class WeatherManager : MonoBehaviour
    {
        public static WeatherManager Instance { get; private set; }

        [Header("Atmosfera")]
        [Tooltip("Temperatura del aire en grados Celsius")]
        [SerializeField] private float temperatureCelsius = 15f;

        [Tooltip("Presion barometrica local (en el punto de disparo), en hPa")]
        [SerializeField] private float pressureHpa = 1013.25f;

        [Tooltip("Humedad relativa, 0-100%")]
        [Range(0f, 100f)]
        [SerializeField] private float relativeHumidityPercent = 50f;

        [Tooltip("Altitud del punto de disparo sobre el nivel del mar, en metros. Usada para la correccion de gravedad.")]
        [SerializeField] private float altitudeMeters = 0f;

        [Header("Viento")]
        [Tooltip("Vector de viento en espacio de mundo, m/s. Convencion de ejes: X=Este, Y=Vertical, Z=Norte.")]
        [SerializeField] private Vector3 windVelocityMps = Vector3.zero;

        [Header("Geografia (para efecto Coriolis)")]
        [Tooltip("Latitud del punto de disparo en grados (positivo = hemisferio norte, negativo = sur)")]
        [Range(-90f, 90f)]
        [SerializeField] private float latitudeDegrees = 0f;

        public float TemperatureCelsius => temperatureCelsius;
        public float TemperatureKelvin => temperatureCelsius + 273.15f;
        public float PressureHpa => pressureHpa;
        public float RelativeHumidityPercent => relativeHumidityPercent;
        public float AltitudeMeters => altitudeMeters;
        public float LatitudeDegrees => latitudeDegrees;

        private const float DryAirGasConstant = 287.05f;      // J/(kg*K)
        private const float WaterVaporGasConstant = 461.495f; // J/(kg*K)
        private const float AdiabaticIndex = 1.4f;

        private void Awake()
        {
            Instance = this;
        }

        /// Presion de vapor de saturacion (hPa), aproximacion de Buck.
        private float GetSaturationVaporPressureHpa()
        {
            return 6.1121f * Mathf.Exp((18.678f - temperatureCelsius / 234.5f) * (temperatureCelsius / (257.14f + temperatureCelsius)));
        }

        /// Densidad del aire (kg/m^3), corregida por humedad (el aire humedo es MENOS denso que el seco).
        public float GetAirDensityKgM3()
        {
            float tempK = TemperatureKelvin;
            float vaporPressureHpa = GetSaturationVaporPressureHpa() * (relativeHumidityPercent / 100f);
            float dryPressureHpa = pressureHpa - vaporPressureHpa;

            float dryPressurePa = dryPressureHpa * 100f;
            float vaporPressurePa = vaporPressureHpa * 100f;

            return (dryPressurePa / (DryAirGasConstant * tempK)) + (vaporPressurePa / (WaterVaporGasConstant * tempK));
        }

        public float GetSpeedOfSoundMps()
        {
            return Mathf.Sqrt(AdiabaticIndex * DryAirGasConstant * TemperatureKelvin);
        }

        /// Viento en una posicion del mundo dada. Punto de extension futuro para variar
        /// el viento por altitud (capas) o agregar rafagas/turbulencia.
        public Vector3 GetWindAtPosition(Vector3 worldPosition)
        {
            return windVelocityMps;
        }

        public void SetWind(Vector3 windMps)
        {
            windVelocityMps = windMps;
        }
    }
}

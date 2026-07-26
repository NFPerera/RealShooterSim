using RealShooter.Ballistics;
using TMPro;
using UnityEngine;

namespace RealShooter.UI
{
    /// HUD de datos ambientales: expone en pantalla los valores que un tirador de precision
    /// necesitaria para calcular su disparo a mano (rumbo, viento, temperatura, presion,
    /// humedad, altitud, latitud). Es una herramienta de desarrollo/testeo, igual que
    /// BallisticsHudController, no esta pensada para quedar en el juego final tal cual.
    public class EnvironmentHudController : MonoBehaviour
    {
        [SerializeField] private TMP_Text hudText;

        [Tooltip("Transform cuyo rumbo (rotacion en Y) se muestra. Normalmente el jugador (no la camara).")]
        [SerializeField] private Transform headingTransform;

        [Tooltip("Se busca automaticamente en la escena si se deja vacio.")]
        [SerializeField] private WeatherManager weatherManager;

        // N/NE/E/SE/S/SO/O/NO, en pasos de 45 grados empezando en el Norte.
        private static readonly string[] CompassLabels = { "N", "NE", "E", "SE", "S", "SO", "O", "NO" };

        private void Awake()
        {
            if (weatherManager == null)
            {
                weatherManager = WeatherManager.Instance != null ? WeatherManager.Instance : FindFirstObjectByType<WeatherManager>();
            }
        }

        private void Update()
        {
            if (hudText == null || weatherManager == null) return;

            float headingDegrees = headingTransform != null ? NormalizeDegrees(headingTransform.eulerAngles.y) : 0f;

            Vector3 samplePosition = headingTransform != null ? headingTransform.position : Vector3.zero;
            Vector3 wind = weatherManager.GetWindAtPosition(samplePosition);
            float windSpeed = wind.magnitude;

            string windLine;
            if (windSpeed < 0.05f)
            {
                windLine = "Viento: calma";
            }
            else
            {
                // Convencion meteorologica: se informa de donde VIENE el viento, no hacia donde sopla.
                float windFromDegrees = VectorToCompassDegrees(-wind);
                windLine = $"Viento: {windSpeed:F1} m/s desde {windFromDegrees:F0}° {ToCompassLabel(windFromDegrees)}";
            }

            hudText.text =
                $"Rumbo: {headingDegrees:F0}° {ToCompassLabel(headingDegrees)}\n" +
                $"{windLine}\n" +
                $"Temperatura: {weatherManager.TemperatureCelsius:F1} °C\n" +
                $"Presion: {weatherManager.PressureHpa:F0} hPa\n" +
                $"Humedad: {weatherManager.RelativeHumidityPercent:F0}%\n" +
                $"Altitud: {weatherManager.AltitudeMeters:F0} m\n" +
                $"Latitud: {weatherManager.LatitudeDegrees:F1}°";
        }

        /// Convierte una direccion del mundo a rumbo en grados (0=Norte, 90=Este), asumiendo
        /// la convencion del proyecto: X=Este, Z=Norte (ver CLAUDE.md).
        private static float VectorToCompassDegrees(Vector3 worldDirection)
        {
            float degrees = Mathf.Atan2(worldDirection.x, worldDirection.z) * Mathf.Rad2Deg;
            return NormalizeDegrees(degrees);
        }

        private static float NormalizeDegrees(float degrees)
        {
            degrees %= 360f;
            if (degrees < 0f) degrees += 360f;
            return degrees;
        }

        private static string ToCompassLabel(float degrees)
        {
            int index = Mathf.RoundToInt(degrees / 45f) % CompassLabels.Length;
            return CompassLabels[index];
        }
    }
}

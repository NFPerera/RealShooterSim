namespace RealShooter.Ballistics
{
    /// Tablas de arrastre estandar G1 (bala clasica spitzer) y G7 (bala boat-tail de largo alcance),
    /// las mismas tablas de referencia (Mach -> Cd) usadas practicamente en toda la industria balistica
    /// (JBM Ballistics, Applied Ballistics, GNU Ballistics, etc). El Cd real de una bala especifica se
    /// obtiene escalando estos valores por su factor de forma (ver BulletData.FormFactor).
    public static class DragTables
    {
        private static readonly float[] G1Mach =
        {
            0f, 0.05f, 0.10f, 0.15f, 0.20f, 0.25f, 0.30f, 0.35f, 0.40f, 0.45f,
            0.50f, 0.55f, 0.60f, 0.65f, 0.70f, 0.725f, 0.75f, 0.775f, 0.80f, 0.825f,
            0.85f, 0.875f, 0.90f, 0.925f, 0.95f, 0.975f, 1.00f, 1.025f, 1.05f, 1.075f,
            1.10f, 1.15f, 1.20f, 1.25f, 1.30f, 1.35f, 1.40f, 1.50f, 1.60f, 1.70f,
            1.80f, 1.90f, 2.00f, 2.20f, 2.40f, 2.60f, 2.80f, 3.00f, 3.50f, 4.00f,
            4.50f, 5.00f
        };

        private static readonly float[] G1Cd =
        {
            0.2629f, 0.2558f, 0.2487f, 0.2413f, 0.2344f, 0.2278f, 0.2214f, 0.2155f, 0.2104f, 0.2061f,
            0.2032f, 0.2020f, 0.2034f, 0.2090f, 0.2165f, 0.2230f, 0.2313f, 0.2417f, 0.2546f, 0.2706f,
            0.2901f, 0.3136f, 0.3415f, 0.3734f, 0.4084f, 0.4448f, 0.4805f, 0.5136f, 0.5427f, 0.5677f,
            0.5883f, 0.6191f, 0.6393f, 0.6518f, 0.6589f, 0.6621f, 0.6625f, 0.6573f, 0.6474f, 0.6347f,
            0.6210f, 0.6072f, 0.5934f, 0.5685f, 0.5481f, 0.5325f, 0.5211f, 0.5133f, 0.5000f, 0.4923f,
            0.4880f, 0.4823f
        };

        private static readonly float[] G7Mach = G1Mach; // mismos puntos de muestreo de Mach

        private static readonly float[] G7Cd =
        {
            0.1198f, 0.1197f, 0.1196f, 0.1194f, 0.1193f, 0.1194f, 0.1194f, 0.1194f, 0.1193f, 0.1193f,
            0.1194f, 0.1194f, 0.1194f, 0.1197f, 0.1202f, 0.1207f, 0.1215f, 0.1226f, 0.1242f, 0.1266f,
            0.1306f, 0.1368f, 0.1464f, 0.1660f, 0.2054f, 0.2993f, 0.3803f, 0.4015f, 0.4043f, 0.4034f,
            0.4014f, 0.3955f, 0.3884f, 0.3810f, 0.3732f, 0.3657f, 0.3580f, 0.3440f, 0.3315f, 0.3209f,
            0.3117f, 0.3042f, 0.2980f, 0.2864f, 0.2752f, 0.2640f, 0.2547f, 0.2470f, 0.2338f, 0.2177f,
            0.2088f, 0.1974f
        };

        public static float GetStandardCd(DragModelType model, float mach)
        {
            float[] machTable = model == DragModelType.G7 ? G7Mach : G1Mach;
            float[] cdTable = model == DragModelType.G7 ? G7Cd : G1Cd;
            return Interpolate(machTable, cdTable, mach);
        }

        private static float Interpolate(float[] xs, float[] ys, float x)
        {
            int n = xs.Length;
            if (x <= xs[0]) return ys[0];
            if (x >= xs[n - 1]) return ys[n - 1];

            int lo = 0, hi = n - 1;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) / 2;
                if (xs[mid] <= x) lo = mid; else hi = mid;
            }

            float t = (x - xs[lo]) / (xs[hi] - xs[lo]);
            return ys[lo] + (ys[hi] - ys[lo]) * t;
        }
    }
}

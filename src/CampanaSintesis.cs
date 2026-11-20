// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using System;

namespace WitchHuntCoop
{
    /// Campana de la Luna de sangre, generada desde cero por el mod (sin sonidos ajenos; sin Unity,
    /// para escucharla también fuera del juego con herramientas/campana).
    /// Síntesis modal de una campana de bronce: cada golpe excita sus parciales reales, medidos respecto
    /// del tono principal ("nominal"): hum (×0,25), prima (×0,5), tercera menor (×0,6), quinta (×0,75),
    /// nominal (×1), y los agudos que le dan el brillo metálico. Cada parcial tiene un gemelo apenas
    /// desafinado (el "uaua" típico) y se apaga a su ritmo: los graves duran mucho, los agudos poco.
    /// Encima, un golpe metálico corto y un eco moderado de iglesia.
    public static class CampanaSintesis
    {
        public const int Frecuencia = 44100;

        public class Version
        {
            public string Nombre; public double Nominal; public double[] Golpes; public double[] Fuerza; public double Eco; public float Duracion;
        }

        /// Versiones para elegir (la primera es la que usa el juego).
        public static readonly Version[] Versiones = {
            new Version { Nombre = "A_toque_grave", Nominal = 196, Golpes = new[] { 0.0, 3.6, 7.2 }, Fuerza = new[] { 1.0, 0.95, 1.0 }, Eco = 0.30, Duracion = 14f },
            new Version { Nombre = "B_tanido_balanceo", Nominal = 165, Golpes = new[] { 0.0, 2.3, 4.6, 6.9, 9.2, 11.5 }, Fuerza = new[] { 1.0, 0.8, 0.95, 0.78, 0.9, 0.72 }, Eco = 0.35, Duracion = 18f },
            new Version { Nombre = "C_campana_lenta_profunda", Nominal = 131, Golpes = new[] { 0.0, 5.0 }, Fuerza = new[] { 1.0, 1.0 }, Eco = 0.40, Duracion = 14f },
        };
        public static int Elegida = 0;

        // proporción respecto del nominal, amplitud, segundos de caída
        static readonly double[,] Parciales = {
            { 0.250, 0.35, 14.0 },   // hum
            { 0.500, 0.45, 9.0 },    // prima
            { 0.600, 0.60, 7.0 },    // tercera menor: el carácter de campana
            { 0.750, 0.25, 4.5 },    // quinta
            { 1.000, 1.00, 5.5 },    // nominal: el tono que se oye
            { 1.250, 0.30, 3.0 },    // tercera mayor superior
            { 1.500, 0.35, 2.6 },    // superquinta
            { 2.000, 0.30, 2.0 },    // octava nominal
            { 2.500, 0.18, 1.4 },
            { 2.670, 0.15, 1.2 },
            { 3.000, 0.12, 0.9 },
            { 4.000, 0.08, 0.6 },
        };

        static float[] lista; static int listaDe = -1;
        static readonly object cerrojo = new object();

        public static void Preparar() { new System.Threading.Thread(() => { try { Generar(); } catch { } }) { IsBackground = true }.Start(); }

        public static float[] Generar() { return Generar(Elegida); }

        public static float[] Generar(int v)
        {
            lock (cerrojo)
            {
                if (lista == null || listaDe != v) { lista = Crear(Versiones[v]); listaDe = v; }
                return lista;
            }
        }

        public static float[] Crear(Version ver)
        {
            int n = (int)(Frecuencia * ver.Duracion);
            const double DosPi = 2 * Math.PI;
            var rng = new Random(7);
            // un golpe
            int nG = Math.Min(n, (int)(Frecuencia * 14.0));
            var golpe = new double[nG];
            for (int k = 0; k < Parciales.GetLength(0); k++)
            {
                double f = ver.Nominal * Parciales[k, 0], a = Parciales[k, 1], c = Parciales[k, 2];
                if (f > 9000) continue;
                double ph1 = rng.NextDouble() * DosPi, ph2 = rng.NextDouble() * DosPi;
                double d = 1.0 + 0.0012 + 0.0008 * k;   // gemelo desafinado → batido lento
                for (int i = 0; i < nG; i++)
                {
                    double t = i / (double)Frecuencia;
                    double env = Math.Exp(-t / c) * (1 - Math.Exp(-t * 2000));   // ataque de 0,5 ms
                    golpe[i] += a * env * (0.65 * Math.Sin(DosPi * f * t + ph1) + 0.35 * Math.Sin(DosPi * f * d * t + ph2));
                }
            }
            // golpe metálico del badajo: parciales altos inarmónicos que se apagan en décimas
            double[] metal = { 5.4, 6.8, 8.1, 9.9 };
            foreach (var m in metal)
            {
                double f = ver.Nominal * m; if (f > 12000) continue;
                for (int i = 0; i < Frecuencia / 2; i++)
                {
                    double t = i / (double)Frecuencia;
                    golpe[i] += 0.10 * Math.Exp(-t * (8 + m)) * Math.Sin(DosPi * f * t) * (1 - Math.Exp(-t * 3000));
                }
            }
            // los golpes se suman
            var x = new double[n];
            for (int g = 0; g < ver.Golpes.Length; g++)
            {
                int ini = (int)(ver.Golpes[g] * Frecuencia);
                for (int i = 0; i < nG && ini + i < n; i++) x[ini + i] += golpe[i] * ver.Fuerza[g];
            }
            // eco de iglesia moderado: reflexiones tempranas + cola suave (con los agudos más apagados)
            if (ver.Eco > 0)
            {
                var entrada = new double[n]; double prev = 0;
                for (int i = 0; i < n; i++) { prev += 0.35 * (x[i] - prev); entrada[i] = prev; }   // paso bajo suave
                var cola = new double[n];
                int[] dp = { 1559, 1693, 1811, 1931 }; double[] gp = { .80, .79, .78, .77 };
                for (int k = 0; k < 4; k++)
                {
                    var y = new double[n];
                    for (int i = 0; i < n; i++) y[i] = entrada[i] + (i >= dp[k] ? gp[k] * y[i - dp[k]] : 0);
                    for (int i = 0; i < n; i++) cola[i] += y[i] * 0.25;
                }
                cola = Pasatodo(Pasatodo(cola, 347, .6), 113, .6);
                for (int i = 0; i < n; i++) x[i] += ver.Eco * cola[i];
            }
            double max = 1e-9;
            for (int i = 0; i < n; i++) max = Math.Max(max, Math.Abs(x[i]));
            var r = new float[n];
            for (int i = 0; i < n; i++)
            {
                double t = i / (double)Frecuencia;
                double fin = Math.Max(0, Math.Min(1, (ver.Duracion - t) / 1.5));
                r[i] = (float)(x[i] / max * 0.95 * fin);
            }
            return r;
        }

        static double[] Pasatodo(double[] x, int d, double g)
        {
            var y = new double[x.Length];
            for (int i = 0; i < x.Length; i++)
            {
                double xd = i >= d ? x[i - d] : 0, yd = i >= d ? y[i - d] : 0;
                y[i] = -g * x[i] + xd + g * yd;
            }
            return y;
        }

        /// WAV de 16 bits mono.
        public static byte[] Wav(float[] datos)
        {
            using (var ms = new System.IO.MemoryStream())
            using (var w = new System.IO.BinaryWriter(ms))
            {
                int bytes = datos.Length * 2;
                w.Write(new[] { 'R', 'I', 'F', 'F' }); w.Write(36 + bytes); w.Write(new[] { 'W', 'A', 'V', 'E' });
                w.Write(new[] { 'f', 'm', 't', ' ' }); w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(Frecuencia); w.Write(Frecuencia * 2); w.Write((short)2); w.Write((short)16);
                w.Write(new[] { 'd', 'a', 't', 'a' }); w.Write(bytes);
                foreach (var v in datos) w.Write((short)Math.Max(-32768, Math.Min(32767, v * 32767f)));
                w.Flush();
                return ms.ToArray();
            }
        }
    }
}

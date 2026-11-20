// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace WitchHuntCoop
{
    /// Medición del costo del mod (cuánto tarda cada parte en cada cuadro) y cosas compartidas para
    /// no buscar en toda la escena a cada rato.
    public static class Rendimiento
    {
        static readonly Dictionary<string, long> ticks = new Dictionary<string, long>();
        static readonly Dictionary<string, int> veces = new Dictionary<string, int>();
        static readonly Stopwatch reloj = new Stopwatch();
        static int cuadros; static float desde = -1f, peorCuadro;

        public static void Medir(string parte, Action a)
        {
            long t0 = reloj.ElapsedTicks;
            if (!reloj.IsRunning) reloj.Start();
            try { a(); }
            catch (Exception e) { UnityEngine.Debug.LogError("[WitchHuntCoop] " + parte + ": " + e); }
            long d = reloj.ElapsedTicks - t0;
            long v; ticks.TryGetValue(parte, out v); ticks[parte] = v + d;
            int n; veces.TryGetValue(parte, out n); veces[parte] = n + 1;
        }

        public static void Cuadro()
        {
            if (desde < 0f) desde = Time.realtimeSinceStartup;
            cuadros++;
            peorCuadro = Mathf.Max(peorCuadro, Time.unscaledDeltaTime);
        }

        /// Informe y reinicio de los contadores.
        public static string Informe()
        {
            float seg = Mathf.Max(0.001f, Time.realtimeSinceStartup - desde);
            var l = new List<KeyValuePair<string, long>>(ticks);
            l.Sort((a, b) => b.Value.CompareTo(a.Value));
            double ms = 1000.0 / Stopwatch.Frequency;
            long total = 0; foreach (var kv in l) total += kv.Value;
            var sb = new System.Text.StringBuilder();
            sb.Append(cuadros / seg >= 0 ? (cuadros / seg).ToString("0") : "?").Append(" FPS de media, peor cuadro ").Append((peorCuadro * 1000f).ToString("0")).Append(" ms; el mod gasta ")
              .Append((total * ms / Math.Max(1, cuadros)).ToString("0.00")).Append(" ms por cuadro: ");
            foreach (var kv in l) sb.Append(kv.Key).Append(' ').Append((kv.Value * ms / Math.Max(1, cuadros)).ToString("0.000")).Append(" ms; ");
            ticks.Clear(); veces.Clear(); cuadros = 0; desde = Time.realtimeSinceStartup; peorCuadro = 0f;
            return sb.ToString();
        }

        // ---------------- Caché ----------------

        static PlayerWeapons armas; static float proximaArmas;

        /// Las armas del jugador (se busca en la escena solo si no se tiene, como mucho una vez por segundo).
        public static PlayerWeapons Armas
        {
            get
            {
                if (armas == null && Time.realtimeSinceStartup >= proximaArmas)
                {
                    proximaArmas = Time.realtimeSinceStartup + 1f;
                    armas = UnityEngine.Object.FindObjectOfType<PlayerWeapons>();
                }
                return armas;
            }
        }
    }
}

// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using UnityEngine;

namespace WitchHuntCoop
{
    /// Sonidos falsos de la noche oscura (pedido 2026-11-10): pasos que se acercan, susurros, ramas que crujen y
    /// gruñidos lejanos, desde sitios al azar alrededor del jugador y sin nada que los cause. Sintetizados aquí.
    public static class Ilusiones
    {
        const int Fs = 22050;
        static AudioClip pasos, susurro, rama, gruñido;
        static System.Random az = new System.Random();
        static float proximo;

        public static void Reiniciar() { proximo = Time.realtimeSinceStartup + 6f; }

        public const int CuantosTerror = 30;   // terror_01 … terror_30 (sonidos/CREDITOS.txt)
        static int ultimoTerror = -1;
        static bool cargadosTerror;

        public static void Actualizar(Transform jugador)
        {
            if (!cargadosTerror) { cargadosTerror = true; for (int i = 1; i <= CuantosTerror; i++) Sonidos.Preparar("terror_" + i.ToString("00")); }
            if (jugador == null || Time.realtimeSinceStartup < proximo) return;
            // un sonido de terror real (grabado, CC0) desde un sitio al azar alrededor del jugador y sin nada que lo cause
            int k, intentos = 0; AudioClip c = null;
            do { k = az.Next(1, CuantosTerror + 1); if (k == ultimoTerror) continue; c = Sonidos.Clip("terror_" + k.ToString("00")); intentos++; } while (c == null && intentos < 12);
            if (c == null) { proximo = Time.realtimeSinceStartup + 1f; return; }   // aún cargando
            ultimoTerror = k;
            proximo = Time.realtimeSinceStartup + 5f + (float)az.NextDouble() * 11f;
            float dist = 8f + (float)az.NextDouble() * 22f, ang = (float)az.NextDouble() * Mathf.PI * 2f;
            var pos = jugador.position + new Vector3(Mathf.Cos(ang) * dist, 0.5f, Mathf.Sin(ang) * dist);
            float vol = (0.45f + 0.4f * (float)az.NextDouble()) * Opciones.FactorVolumen;
            var go = new GameObject("coop_ilusion");
            go.transform.position = pos;
            var a = go.AddComponent<AudioSource>();
            a.clip = c; a.volume = vol; a.spatialBlend = 1f; a.rolloffMode = AudioRolloffMode.Linear; a.minDistance = 4f; a.maxDistance = 60f; a.pitch = 0.95f + (float)az.NextDouble() * 0.1f;
            a.Play();
            Object.Destroy(go, c.length + 0.5f);
            Debug.Log("[WitchHuntCoop] Ilusiones: terror_" + k.ToString("00") + " (" + c.length.ToString("0.0") + " s) a " + Mathf.RoundToInt(dist) + " m");
        }

        static AudioClip Hacer(string n, float seg, System.Func<float, float> f)
        {
            int N = (int)(seg * Fs); var d = new float[N];
            for (int i = 0; i < N; i++) d[i] = Mathf.Clamp(f(i / (float)Fs), -1f, 1f);
            var c = AudioClip.Create(n, N, 1, Fs, false); c.SetData(d, 0); return c;
        }
        static float Ruido() { return (float)(az.NextDouble() * 2.0 - 1.0); }

        /// Cinco pasos sobre hojas y tierra, cada vez más fuertes (se acerca algo).
        static AudioClip Pasos()
        {
            float lp = 0f;
            return Hacer("coop_ilu_pasos", 3.6f, t =>
            {
                float v = 0f; int k = Mathf.FloorToInt(t / 0.62f); float u = t - k * 0.62f;
                if (k < 5)
                {
                    float fuerza = 0.35f + 0.13f * k;
                    lp += (Ruido() - lp) * 0.22f;
                    v = (lp * 1.8f * Mathf.Exp(-u * 22f) + Mathf.Sin(2f * Mathf.PI * 70f * u) * Mathf.Exp(-u * 18f) * 0.8f) * fuerza;
                }
                return v * 0.9f;
            });
        }

        /// Murmullo ininteligible: ruido filtrado con la modulación lenta de unas sílabas.
        static AudioClip Susurro()
        {
            float lp = 0f, lp2 = 0f;
            return Hacer("coop_ilu_susurro", 2.8f, t =>
            {
                float silaba = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * 14f) * Mathf.Sin(t * 5.3f + 1f)), 1.5f);
                float sob = Mathf.Sin(Mathf.PI * t / 2.8f);
                lp += (Ruido() - lp) * 0.35f; lp2 += (lp - lp2) * 0.5f;
                float formante = (lp - lp2) * 3.2f;
                return formante * silaba * sob * 0.9f;
            });
        }

        /// Una rama seca que se parte, y otra más débil.
        static AudioClip Rama()
        {
            return Hacer("coop_ilu_rama", 1.0f, t =>
            {
                float a = t < 0.35f ? t : t - 0.35f; float g = t < 0.35f ? 1f : 0.5f;
                return (Ruido() * Mathf.Exp(-a * 90f) + Mathf.Sin(2f * Mathf.PI * 1100f * a) * Mathf.Exp(-a * 60f) * 0.5f) * g * 0.8f;
            });
        }

        /// Gruñido grave y lejano.
        static AudioClip Gruñido()
        {
            float lp = 0f;
            return Hacer("coop_ilu_gruñido", 2.4f, t =>
            {
                float env = Mathf.Sin(Mathf.PI * t / 2.4f); env *= env;
                float f0 = 58f + 12f * Mathf.Sin(t * 3f);
                float saw = ((t * f0) % 1f) * 2f - 1f;
                lp += (saw + Ruido() * 0.6f - lp) * 0.07f;
                return lp * env * 1.6f * (0.7f + 0.3f * Mathf.Sin(t * 22f));
            });
        }
    }
}

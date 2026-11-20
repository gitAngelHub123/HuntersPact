// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using UnityEngine;

namespace WitchHuntCoop
{
    /// Sonidos del perro compañero, sintetizados aquí (sin archivos): jadeo contento de perro cansado con la lengua afuera, y mordisco con gruñido.
    public static class SonidosPerro
    {
        const int Hz = 22050;
        static AudioClip jadeo, mordisco;
        static System.Random az = new System.Random(11);
        static float Ruido() { return (float)(az.NextDouble() * 2.0 - 1.0); }

        /// Jadeo en bucle: 3,4 inhalaciones y exhalaciones por segundo, de ruido suave filtrado, con un resoplido contento al final de cada exhalación.
        static AudioClip delJuego(string nombre)
        {
            foreach (var c in Resources.FindObjectsOfTypeAll<AudioClip>()) if (c.name == nombre) return c;
            return null;
        }
        static AudioClip jadeoJuego, mordiscoJuego; static bool buscado;
        static void Buscar() { if (buscado) return; buscado = true; jadeoJuego = delJuego("Bear Breathing"); mordiscoJuego = delJuego("DogBite"); }
        /// Grabaciones reales del propio juego: la respiración de oso (se sube de tono para que suene a jadeo de perro) y el mordisco de perro del juego.
        public static AudioClip JadeoReal() { Buscar(); return jadeoJuego; }
        public static AudioClip MordiscoReal() { Buscar(); return mordiscoJuego; }

        public static AudioClip Jadeo()
        {
            if (jadeo != null) return jadeo;
            float dur = 2.35f; int n = (int)(Hz * dur); var d = new float[n];
            float lp1 = 0f, lp2 = 0f, hp = 0f, prev = 0f;
            float ciclo = dur / 8f;   // 8 ciclos exhala-inhala en el bucle
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Hz, c = (t % ciclo) / ciclo;
                // exhalación (0–0,5): sube rápido y cae; inhalación (0,5–0,85): más débil y más aguda
                float env = c < 0.5f ? Mathf.Sin(c / 0.5f * Mathf.PI) * (1f - 0.35f * (c / 0.5f)) : (c < 0.85f ? 0.45f * Mathf.Sin((c - 0.5f) / 0.35f * Mathf.PI) : 0f);
                float cut = c < 0.5f ? 0.20f : 0.34f;
                float x = Ruido();
                lp1 += cut * (x - lp1); lp2 += cut * (lp1 - lp2);
                float y = lp2 * 3.2f; hp += 0.04f * (y - hp); y -= hp;   // sin graves de más
                // tono de resoplido (voz muy suave) en la exhalación
                float voz = c < 0.5f ? 0.05f * Mathf.Sin(2f * Mathf.PI * (330f - 60f * c) * t) * env : 0f;
                d[i] = (y * env + voz) * 0.9f;
                prev = d[i];
            }
            // empalme sin chasquido
            for (int i = 0; i < 400; i++) { float k = i / 400f; d[i] *= k; d[n - 1 - i] *= k; }
            jadeo = AudioClip.Create("coop_perro_jadeo", n, 1, Hz, false); jadeo.SetData(d, 0);
            return jadeo;
        }

        /// Mordisco: chasquido de mandíbulas, gruñido corto y otro chasquido.
        public static AudioClip Mordisco()
        {
            if (mordisco != null) return mordisco;
            float dur = 0.5f; int n = (int)(Hz * dur); var d = new float[n];
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Hz;
                // gruñido: diente de sierra grave modulado a ~28 Hz, con ruido, entre 0,02 y 0,40 s
                float g = 0f;
                if (t > 0.02f && t < 0.40f)
                {
                    float e = Mathf.Sin((t - 0.02f) / 0.38f * Mathf.PI);
                    float f0 = 118f - 26f * (t - 0.02f);
                    float ph = (t * f0) % 1f; float saw = ph * 2f - 1f;
                    float am = 0.55f + 0.45f * Mathf.Sin(2f * Mathf.PI * 28f * t);
                    g = (saw * 0.5f + Ruido() * 0.25f) * am * e * 0.7f;
                    lp += 0.25f * (g - lp); g = lp;
                }
                // chasquidos de dientes en 0,0 y 0,27 s
                float ch = 0f;
                foreach (float t0 in new[] { 0.0f, 0.27f })
                {
                    float dt = t - t0; if (dt >= 0f && dt < 0.05f) ch += Ruido() * Mathf.Exp(-dt * 90f) * 0.9f;
                }
                d[i] = Mathf.Clamp(g + ch, -1f, 1f);
            }
            mordisco = AudioClip.Create("coop_perro_mordisco", n, 1, Hz, false); mordisco.SetData(d, 0);
            return mordisco;
        }
    }
}

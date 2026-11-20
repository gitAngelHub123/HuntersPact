// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using System.Collections.Generic;
using UnityEngine;

namespace WitchHuntCoop
{
    /// Rendimiento y sonido (2026-11-19): los 125 cofres del mod ("inside_chest_movement") y las 60 plantas curativas ("bell_loop")
    /// tienen un bucle 3D que se oye a 10–15 m pero sonaba SIEMPRE, aunque estuvieran a 1 km: unos 190 sonidos a la vez. Las plantas
    /// (caída de volumen personalizada) hasta ocupaban voces reales (hay 42), así que las armas y el ambiente se cortaban.
    /// Aquí: los bucles 3D cortos (alcance ≤ 60 m) que estén fuera de su alcance se PAUSAN y se reanudan al acercarse; además bajan
    /// de prioridad para que armas, jefe y eventos ganen siempre. Pause/UnPause: si el juego los para o los vuelve a tocar, manda el juego.
    public static class SonidosLejanos
    {
        const float AlcanceMax = 60f, AlcanceMaxCustom = 200f, Margen = 8f, CadaBusqueda = 5f, CadaRevision = 0.5f;
        // (2026-11-20) Stop en vez de Pause: una fuente en pausa SEGUÍA ocupando su voz (y las de caída personalizada cuentan como audibles a
        // cualquier distancia): la prueba 'probartodos' mostró ~100 de 128 voces reales tomadas. Solo se apagan los bucles que arrancan solos
        // (playOnAwake), para no pelear con la lógica del juego, y se vuelven a encender al acercarse.
        const int PrioridadBaja = 200;   // 0 = la más alta, 256 = la más baja
        static readonly List<AudioSource> lista = new List<AudioSource>();
        static readonly HashSet<AudioSource> pausados = new HashSet<AudioSource>();
        static float proxBusqueda, proxRevision;
        static AudioListener oyente;

        public static int Gestionados { get { return lista.Count; } }
        public static int Pausados { get { return pausados.Count; } }

        public static void Actualizar()
        {
            float t = Time.unscaledTime;
            if (t > proxBusqueda)
            {
                proxBusqueda = t + CadaBusqueda;
                pausados.RemoveWhere(a => a == null);
                lista.Clear();
                foreach (var a in Object.FindObjectsOfType<AudioSource>())
                    if (Gestionable(a)) { lista.Add(a); if (a.priority < PrioridadBaja) a.priority = PrioridadBaja; }
                if (oyente == null || !oyente.isActiveAndEnabled) oyente = Object.FindObjectOfType<AudioListener>();
            }
            if (t < proxRevision || oyente == null) return;
            proxRevision = t + CadaRevision;
            Vector3 o = oyente.transform.position;
            foreach (var a in lista)
            {
                if (a == null) continue;
                float lejos = a.maxDistance + Margen;
                bool fuera = (a.transform.position - o).sqrMagnitude > lejos * lejos;
                if (fuera) { if (a.isPlaying) { a.Stop(); pausados.Add(a); } }
                else if (pausados.Remove(a) && a.isActiveAndEnabled && !a.isPlaying) a.Play();
            }
        }

        static bool Gestionable(AudioSource a)
        {
            if (a == null || !a.loop || !a.playOnAwake || a.spatialBlend < 0.9f) return false;
            if (a.maxDistance > (a.rolloffMode == AudioRolloffMode.Custom ? AlcanceMaxCustom : AlcanceMax)) return false;
            if (!a.isPlaying && !pausados.Contains(a)) return false;
            return a.GetComponentInParent<FPSPlayer>() == null;   // nunca lo del propio jugador
        }

        /// Al cambiar de escena: olvidar todo (los objetos ya no existen).
        public static void Reiniciar() { lista.Clear(); pausados.Clear(); oyente = null; proxBusqueda = 0f; }
    }
}

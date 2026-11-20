// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using System.Collections.Generic;
using UnityEngine;

namespace WitchHuntCoop
{
    /// (2026-11-20) Sonidos de recarga de las armas del mod, por pasos en el tiempo (s desde que empieza la recarga).
    /// - Escopeta: los mismos 6 sonidos que toca RecargaEscopeta en primera persona (el compañero oía el clip "silence" del juego).
    /// - Ballesta: antes recargaba en SILENCIO (se le quitó el sonido de la baqueta del mosquete y no se le puso otro). Ahora, con clips
    ///   del propio juego, siguiendo su animación de 6 s: crujido al inclinarla, la mano toma la cuerda (3,6 s), la tensa con la madera
    ///   crujiendo (3,95–4,85 s), la cuerda traba (5,1 s) y la flecha se asienta en el riel (5,75 s).
    /// Las usan RecargaBallesta (primera persona) y SonidosCompanero (el compañero, en 3D).
    public static class SecuenciaRecarga
    {
        public struct Paso
        {
            public float T; public System.Func<AudioClip> Clip; public float Vol;
            public Paso(float t, System.Func<AudioClip> clip, float vol) { T = t; Clip = clip; Vol = vol; }
        }

        public static readonly Paso[] Escopeta =
        {
            new Paso(0.55f, () => Sonidos.Clip("esc_abrir") ?? Sonidos2.Seguro, 1f),
            new Paso(0.95f, () => Sonidos2.Tintineo, 0.8f),
            new Paso(1.0f, () => Sonidos2.Tintineo, 0.6f),
            new Paso(1.35f, () => Sonidos.Clip("esc_meter1") ?? Sonidos2.Meter, 1f),
            new Paso(1.65f, () => Sonidos.Clip("esc_meter2") ?? Sonidos2.Meter, 1f),
            new Paso(2.05f, () => Sonidos.Clip("esc_cerrar") ?? Sonidos2.Cierre, 1f),
        };

        // Cada paso tiene un respaldo sintético: los clips del juego solo están en memoria a veces (rope_slow_* faltaba en una sesión).
        public static readonly Paso[] Ballesta =
        {
            new Paso(0.25f, () => DelJuego("CREAK_Wood_Bright_Short") ?? Sonidos2.Seguro, 0.5f),
            new Paso(3.6f, () => DelJuego("rope_slow_02") ?? Sonidos2.Cuerda, 0.8f),
            new Paso(3.95f, () => DelJuego("CREAK_Wood_Tree_Creak_04") ?? Sonidos2.Cuerda, 0.9f),
            new Paso(4.35f, () => DelJuego("rope_slow_04") ?? Sonidos2.Cuerda, 0.7f),
            new Paso(4.85f, () => DelJuego("CREAK_Wood_Bright_Short") ?? Sonidos2.Seguro, 0.7f),
            new Paso(5.1f, () => DelJuego("lock") ?? Sonidos2.Cierre, 1f),
            new Paso(5.75f, () => Sonidos2.Meter, 0.8f),
        };

        public static Paso[] De(string arma)
        {
            if (arma == WitchHuntCoop.Escopeta.Nombre) return Escopeta;
            if (arma == WitchHuntCoop.Ballesta.Nombre) return Ballesta;
            return null;
        }

        static readonly Dictionary<string, AudioClip> delJuego = new Dictionary<string, AudioClip>();

        /// Un clip del propio juego por nombre (exacto o que empiece así). Se busca una vez y se guarda.
        public static AudioClip DelJuego(string nombre)
        {
            AudioClip c;
            if (delJuego.TryGetValue(nombre, out c) && c != null) return c;
            c = null;
            foreach (var x in Resources.FindObjectsOfTypeAll<AudioClip>())
            {
                if (x == null) continue;
                if (x.name == nombre) { c = x; break; }
                if (c == null && x.name.StartsWith(nombre)) c = x;
            }
            if (c != null && c.loadState == AudioDataLoadState.Unloaded) c.LoadAudioData();
            delJuego[nombre] = c;
            return c;
        }
    }
}

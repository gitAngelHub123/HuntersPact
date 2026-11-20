// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using HarmonyLib;
using UnityEngine;

namespace WitchHuntCoop
{
    /// (2026-11-20) La música del pueblo dejó de sonar en una de las dos copias tras ir al bosque y volver: el SoundManager del juego
    /// (SoundManagerPro, objeto que sobrevive entre escenas) quedó con la música silenciada. Lo silencia y lo reactiva una acción de
    /// PlayMaker (MuteMusic). Este parche anota en el registro cada cambio y qué máquina de estados lo hizo, para saber qué paso del viaje
    /// lo deja apagado.
    [HarmonyPatch(typeof(SoundManager), "mutedMusic", MethodType.Setter)]
    static class RegistroMusica
    {
        public static string Ultimo = "-";

        /// El SoundManager que YA existe, o null. NUNCA usar SoundManager.Instance desde el mod: su Singleton (antilunchbox) CREA uno
        /// vacío si todavía no hay ninguno, y entonces el verdadero (el que trae las conexiones de música "Bellville", "forest"...) se
        /// destruye por duplicado al despertar y el juego queda sin música en todas las escenas (pasó el 2026-11-20 al consultarlo en
        /// cada cuadro desde Pantalla). Se busca como mucho una vez por segundo.
        static SoundManager visto; static float proxBusqueda;
        public static SoundManager Existente()
        {
            if (visto != null) return visto;
            if (Time.realtimeSinceStartup < proxBusqueda) return null;
            proxBusqueda = Time.realtimeSinceStartup + 1f;
            visto = Object.FindObjectOfType<SoundManager>();
            return visto;
        }

        static void Prefix(SoundManager __instance, bool value)
        {
            try
            {
                if (__instance == null || __instance.mutedMusic == value) return;
                var fsm = HutongGames.PlayMaker.FsmExecutionStack.ExecutingFsm;
                var st = HutongGames.PlayMaker.FsmExecutionStack.ExecutingState;
                string quien = fsm != null ? (fsm.GameObject != null ? fsm.GameObject.name : "?") + "/" + fsm.Name + " [" + (st != null ? st.Name : "?") + "]" : "código (sin FSM)";
                Ultimo = (value ? "silenciada" : "activada") + " por " + quien + " en escena " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name + " (t=" + Time.time.ToString("0") + ")";
                Debug.Log("[WitchHuntCoop] Música: " + Ultimo);
            }
            catch { }
        }

        /// Para la orden 'musica'.
        public static string Informe()
        {
            visto = null; proxBusqueda = 0f;
            var sm = Existente();
            if (sm == null) return "sin SoundManager";
            var t = Traverse.Create(sm);
            var sc = t.Field("currentSoundConnection").GetValue<SoundConnection>();
            string esc = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            var sb = new System.Text.StringBuilder("música silenciada=" + sm.mutedMusic + " pausada=" + sm.isPaused + " apagada=" + sm.offTheBGM
                + " escenaSilenciosa=" + t.Field("silentLevel").GetValue<bool>() + " nivel='" + t.Field("currentLevel").GetValue<string>() + "' escena='" + esc + "'"
                + " conexión=" + (sc != null ? sc.level + " " + sc.playMethod + " retraso " + sc.minDelay + "-" + sc.maxDelay + "/" + sc.delay + " pistas " + (sc.soundsToPlay != null ? sc.soundsToPlay.Count : 0) : "ninguna"));
            foreach (var c in sm.soundConnections) if (c != null) sb.Append(" [" + c.level + (c.isCustomLevel ? " (propia)" : "") + " " + c.playMethod + " " + c.minDelay + "-" + c.maxDelay + "]");
            sb.Append("; último cambio: " + Ultimo);
            foreach (var otro in Resources.FindObjectsOfTypeAll<SoundManager>())
            {
                if (otro == null) continue;
                sb.Append(" || instancia '" + otro.name + "' id " + otro.GetInstanceID() + (otro == sm ? " (en uso)" : "") + " escena=" + (otro.gameObject.scene.IsValid() ? otro.gameObject.scene.name : "-")
                    + " activa=" + otro.gameObject.activeInHierarchy + " conexiones=" + (otro.soundConnections != null ? otro.soundConnections.Count : -1));
                foreach (var a in otro.GetComponents<AudioSource>()) sb.Append(" " + (a.clip != null ? a.clip.name : "-") + (a.isPlaying ? "▶" : ""));
            }
            foreach (var a in sm.GetComponents<AudioSource>())
                sb.Append(" | " + (a.clip != null ? a.clip.name : "(sin clip)") + " suena=" + a.isPlaying + " mute=" + a.mute + " vol=" + a.volume.ToString("0.00"));
            return sb.ToString();
        }
    }
}

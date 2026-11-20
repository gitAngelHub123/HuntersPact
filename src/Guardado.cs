// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using System;
using System.IO;
using HarmonyLib;
using UnityEngine;

namespace WitchHuntCoop
{
    /// Partidas separadas: con el cooperativo activo (anfitrión escuchando o invitado unido),
    /// el juego lee y escribe "savefile_coop.sav" en vez de "savefile.sav". La partida en
    /// solitario nunca se toca. Una partida coop nueva empieza siempre desde cero ("Nueva partida",
    /// con el equipo inicial del cooperativo): ya no se copia la de solitario.
    /// Easy Save 2 arma todas sus rutas en ES2FilenameData.Init(path, ...), con path del tipo
    /// "savefile.sav?tag=Variable".
    [HarmonyPatch(typeof(ES2FilenameData), "Init")]
    static class Guardado
    {
        public const string Solo = "savefile.sav";
        public const string Coop = "savefile_coop.sav";

        /// Lo fija el plugin: true mientras la red cooperativa está activa.
        public static bool ModoCoop;

        static void Prefix(ref string path)
        {
            if (!ModoCoop || string.IsNullOrEmpty(path)) return;
            int i = path.IndexOf(Solo, StringComparison.OrdinalIgnoreCase);
            if (i < 0) return;
            path = path.Substring(0, i) + Coop + path.Substring(i + Solo.Length);
        }

        /// ¿Hay una partida cooperativa guardada en este PC?
        public static bool ExisteCoop()
        {
            try { return File.Exists(Path.Combine(Application.persistentDataPath, Coop)); } catch { return false; }
        }

        /// Antes de empezar una partida cooperativa nueva, la anterior se guarda con fecha (nunca se pierde).
        public static string RespaldarCoop()
        {
            try
            {
                string dir = Application.persistentDataPath, f = Path.Combine(dir, Coop);
                if (!File.Exists(f)) return null;
                string destino = Path.Combine(Path.Combine(dir, "coop_respaldos"), "savefile_coop_" + System.DateTime.Now.ToString("yyyy-MM-dd_HHmmss") + ".sav");
                Directory.CreateDirectory(Path.GetDirectoryName(destino));
                File.Copy(f, destino, true);
                return destino;
            }
            catch { return null; }
        }

        // ---------------- Partida terminada (2026-11-20, pedido: "marcarla como terminada sin borrarla") ----------------
        const string Marca = "coop_terminada.txt";

        /// Al terminar los créditos de una partida cooperativa: copia la partida a coop_respaldos/terminada_<fecha>.sav y deja una
        /// marca. La partida NO se borra (Continuar sigue cargándola; el menú avisa que ya terminó).
        public static string ArchivarTerminada()
        {
            try
            {
                string dir = Application.persistentDataPath, f = Path.Combine(dir, Coop);
                if (!File.Exists(f)) return null;
                string fecha = System.DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
                string destino = Path.Combine(Path.Combine(dir, "coop_respaldos"), "terminada_" + fecha + ".sav");
                Directory.CreateDirectory(Path.GetDirectoryName(destino));
                File.Copy(f, destino, true);
                File.WriteAllText(Path.Combine(dir, Marca), System.DateTime.Now.ToString("yyyy-MM-dd HH:mm") + "|" + new FileInfo(f).Length + "|" + File.GetLastWriteTimeUtc(f).Ticks + "|" + destino);
                return destino;
            }
            catch { return null; }
        }

        /// Fecha en que terminó la partida cooperativa guardada, o null si no está terminada (o se volvió a jugar y guardar después).
        public static string Terminada()
        {
            try
            {
                string dir = Application.persistentDataPath, f = Path.Combine(dir, Coop), m = Path.Combine(dir, Marca);
                if (!File.Exists(f) || !File.Exists(m)) return null;
                var p = File.ReadAllText(m).Split('|');
                if (p.Length < 3) return null;
                long ticks; if (!long.TryParse(p[2], out ticks)) return null;
                return File.GetLastWriteTimeUtc(f).Ticks <= ticks ? p[0] : null;   // guardada otra vez después del final: ya no cuenta como terminada
            }
            catch { return null; }
        }

        /// Partida nueva DE VERDAD (2026-11-18): el menú carga la partida anterior en memoria (variables de Lua) al abrirse y el
        /// botón "Nueva partida" del juego no las borra todas: la partida nueva heredaba la historia (lobo muerto, CurrentQuest 33,
        /// bruja jefa activa). Se devuelven todas las variables a sus valores iniciales y se vuelve a poner la dificultad elegida.
        public static void ReiniciarHistoria(WitchHuntCoop.Plugin plugin)
        {
            try
            {
                PixelCrushers.DialogueSystem.DialogueManager.ResetDatabase(PixelCrushers.DialogueSystem.DatabaseResetOptions.RevertToDefault);
                if (Opciones.DificultadElegida >= 0) Opciones.Dificultad = Opciones.DificultadElegida;
                if (plugin != null) plugin.Log("Inicio: variables de la historia devueltas al principio (partida nueva desde cero)");
            }
            catch (System.Exception e) { if (plugin != null) plugin.Log("Inicio: no pude reiniciar la historia: " + e.Message); }
        }

        public static string Describir()
        {
            string dir = Application.persistentDataPath;
            return "modo=" + (ModoCoop ? "coop" : "solo") +
                   " solo=" + (File.Exists(Path.Combine(dir, Solo)) ? "sí" : "no") +
                   " coop=" + (File.Exists(Path.Combine(dir, Coop)) ? "sí" : "no");
        }
    }
}

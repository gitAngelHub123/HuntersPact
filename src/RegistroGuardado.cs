// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using System;
using System.IO;
using HarmonyLib;
using UnityEngine;

namespace WitchHuntCoop
{
    /// Registro de cada guardado real en disco (Easy Save 2: ES2Writer.Save escribe el archivo).
    /// Sirve para averiguar por qué a veces "se guardó en la piedra" pero el .sav no cambió:
    /// cada escritura deja en LogOutput.log el archivo, la etiqueta, el tamaño y si falló.
    /// En modo cooperativo, al escribir la partida coop sale "Partida guardada" en pantalla
    /// (como mucho una vez cada 8 s, para no repetirlo en cada etiqueta).
    [HarmonyPatch(typeof(ES2Writer), "Save", new Type[] { typeof(bool) })]
    static class RegistroGuardado
    {
        static float ultimoAviso = -100f;
        static string ultimaLinea; static int repetidas;

        static string Ruta(ES2Writer w)
        {
            try { var f = w.settings.filenameData; return (f.filePath ?? f.filename ?? "?") + (string.IsNullOrEmpty(f.tag) ? "" : "?tag=" + f.tag); }
            catch { return "?"; }
        }

        static void Postfix(ES2Writer __instance)
        {
            try
            {
                string ruta = Ruta(__instance);
                long tam = -1;
                try { var f = __instance.settings.filenameData.filePath; if (!string.IsNullOrEmpty(f) && File.Exists(f)) tam = new FileInfo(f).Length; } catch { }
                string linea = "Guardado ES2: " + ruta + " ok" + (tam >= 0 ? " (" + tam + " bytes)" : "");
                // las mismas etiquetas se escriben muchas veces seguidas: no inundar el registro
                if (linea == ultimaLinea) { repetidas++; return; }
                if (repetidas > 0 && Viaje.plugin != null) Viaje.plugin.Log("Guardado ES2: (la línea anterior se repitió " + repetidas + " veces más)");
                ultimaLinea = linea; repetidas = 0;
                if (Viaje.plugin != null) Viaje.plugin.Log(linea + " modo=" + (Guardado.ModoCoop ? "coop" : "solo") + " escena=" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);

                if (Guardado.ModoCoop && ruta.IndexOf(Guardado.Coop, StringComparison.OrdinalIgnoreCase) >= 0
                    && Time.realtimeSinceStartup - ultimoAviso > 8f)
                {
                    ultimoAviso = Time.realtimeSinceStartup;
                    Viaje.Avisar("Partida guardada.", 3f);
                }
            }
            catch { }
        }

        static Exception Finalizer(ES2Writer __instance, Exception __exception)
        {
            if (__exception != null && Viaje.plugin != null)
            {
                Viaje.plugin.Log("Guardado ES2: FALLÓ " + Ruta(__instance) + " → " + __exception.GetType().Name + ": " + __exception.Message);
                if (Guardado.ModoCoop) Viaje.Avisar("ERROR: no se pudo guardar la partida.", 6f);
            }
            return __exception;   // el juego sigue viendo el error como antes
        }
    }
}

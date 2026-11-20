// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using HarmonyLib;
using UnityEngine;

namespace WitchHuntCoop
{
    /// (2026-11-20, pedido) Fin del juego en cooperativo: cuando corre el final del juego (música "final_ambient" o los créditos en
    /// pantalla: CreditsTitle/CreditsSpace; la conexión "credits" que pide Final_Cutscene NO existe en el juego) y después se vuelve al menú, la partida cooperativa se marca como
    /// TERMINADA y se copia a coop_respaldos/terminada_<fecha>.sav. No se borra: Continuar la sigue cargando y el menú avisa.
    public static class FinalCoop
    {
        static bool vioCreditos; static float prox;

        public static void Actualizar(Plugin plugin)
        {
            if (Time.realtimeSinceStartup < prox) return;
            prox = Time.realtimeSinceStartup + 1f;
            if (plugin.Escena == 3)   // menú
            {
                if (!vioCreditos) return;
                vioCreditos = false;
                var r = Guardado.ArchivarTerminada();
                plugin.Log(r != null ? "Final: partida cooperativa marcada como terminada; copia en " + r : "Final: no pude copiar la partida terminada");
                return;
            }
            if (vioCreditos || !Guardado.ModoCoop) return;
            var sm = RegistroMusica.Existente(); if (sm == null) return;   // nunca SoundManager.Instance (ver RegistroMusica)
            SoundConnection sc = null;
            try { sc = Traverse.Create(sm).Field("currentSoundConnection").GetValue<SoundConnection>(); } catch { }
            bool creditos = GameObject.Find("CreditsTitle(Clone)") != null || GameObject.Find("CreditsSpace(Clone)") != null;
            if (creditos || (sc != null && sc.level == "final_ambient"))
            {
                vioCreditos = true;
                plugin.Log("Final: " + (creditos ? "créditos en pantalla" : "música del final") + " en la partida cooperativa (al volver al menú se marca como terminada)");
            }
        }
    }
}

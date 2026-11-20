// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using HarmonyLib;

namespace WitchHuntCoop
{
    /// El juego lee el mouse con Rewired (entrada cruda), que sigue recibiendo movimiento
    /// aunque la ventana no esté activa. Con el mod el juego no se pausa al perder el foco,
    /// así que hay que anular la entrada a mano: si no, dos copias en un mismo PC se
    /// "pelean" por el cursor y la cámara se va al cielo. También se anula con el panel F8 abierto.
    [HarmonyPatch(typeof(InputControl), "Update")]
    static class Entrada
    {
        public static bool Enfocado = true;
        public static bool PanelAbierto;
        public static bool Espectando;
        public static bool PausaCoop;   // (2026-11-20) menú de pausa abierto en cooperativo: el tiempo sigue, pero sin controles   // caído en coop: no se controla al jugador

        public static int Disparar, Recargar, Usar, Apuntar;   // prueba: fotogramas con el gatillo apretado
        public static bool UsarPulso;   // prueba (2026-11-19): la orden usar también cuenta como un toque de E para lo del mod (minas, etc.)

        static void Postfix(InputControl __instance)
        {
            var i = __instance;
            if (Enfocado && !PanelAbierto && !PausaCoop) SelectorArmas.Procesar(i);
            if (Apuntar > 0) { Apuntar--; i.zoomHold = true; return; }
            if (Disparar > 0) { Disparar--; i.fireHold = true; return; }
            if (Recargar > 0) { Recargar--; i.reloadPress = true; return; }
            if (Usar > 0) { Usar--; i.useHold = true; return; }   // (2026-11-19) return: sin foco, lo de abajo borraba la E simulada
            if (Depuracion.Bot)
            {
                // Modo de prueba: camina hacia delante girando poco a poco (círculo de ~10 s).
                i.moveX = 0f; i.moveY = 1f; i.forwardHold = true; i.moveHold = true;
                i.lookX = Depuracion.BotRecto ? 0f : 0.15f; i.lookY = 0f;
                return;
            }
            if (Enfocado && !PanelAbierto && !Espectando && !PausaCoop) return;
            i.lookX = 0f; i.lookY = 0f;
            i.moveX = 0f; i.moveY = 0f;
            i.mouseWheel = 0f;
            i.fireHold = false; i.zoomHold = false; i.jumpHold = false; i.jumpPress = false;
            i.sprintHold = false; i.useHold = false; i.reloadPress = false; i.throwHold = false;
            i.leftHold = false; i.rightHold = false; i.forwardHold = false; i.backHold = false;
            i.moveHold = false; i.movePress = false;
        }
    }
}

// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using HutongGames.PlayMaker;
using PixelCrushers.DialogueSystem;
using UnityEngine;
using UnityEngine.UI;

namespace WitchHuntCoop
{
    /// Equipo inicial de una partida cooperativa NUEVA: 10 ganzúas y 20 balas para cada arma por
    /// jugador, y 200 de oro en total (la bolsa es compartida: la fija el anfitrión y el invitado la copia).
    /// Solo cuando se pulsó "Nueva partida" con la sala activa y la partida aún no lo recibió, para no
    /// tocar nunca una partida que ya existe.
    public class Kit
    {
        public const string VarKit = "coop_kit_inicial";
        public const int Ganzuas = 10, Balas = 20, Oro = 200;   // oro: 50 → 200 (pedido 2026-11-19)

        readonly Plugin plugin;
        bool pendiente;
        float guardarEn = -1f;   // guardado silencioso pendiente tras dar el kit (tiempo real)
        Button nuevaVista;

        public Kit(Plugin p) { plugin = p; }

        /// En el menú: escucha el botón "Nueva partida".
        public void VigilarMenu()
        {
            if (nuevaVista != null) return;
            foreach (var b in Object.FindObjectsOfType<Button>())
                if (b.name == "NewGame")
                {
                    nuevaVista = b;
                    b.onClick.AddListener(() =>
                    {
                        if (!plugin.RedActiva) return;
                        pendiente = true;
                        plugin.Log("Kit: nueva partida cooperativa, el equipo inicial se dará al empezar");
                    });
                    break;
                }
        }

        public const int MaxOro = 99999;

        /// Topes del cooperativo (el juego no tiene): 9999 ganzúas por jugador y 99 999 de oro en la
        /// bolsa del grupo. El oro lo recorta solo el anfitrión: la bolsa es compartida y el recorte
        /// le llega al invitado como cualquier cambio (si lo hicieran los dos, se restaría dos veces).
        void Topes()
        {
            if (!Guardado.ModoCoop) return;
            var gz = Mundo.Ganzuas();
            if (gz != null && gz.Value > Mundo.MaxGanzuas) gz.Value = Mundo.MaxGanzuas;
            if (plugin.EsAnfitrion || !plugin.Conectado)
            {
                var c = FsmVariables.GlobalVariables.FindFsmInt("Coins");
                if (c != null && c.Value > MaxOro) c.Value = MaxOro;
            }
        }

        public void Actualizar()
        {
            Topes();
            if (guardarEn > 0f && Time.realtimeSinceStartup >= guardarEn) { guardarEn = -1f; Guardar(); }
            if (plugin.Escena == 3) { VigilarMenu(); return; }
            nuevaVista = null;
            if (!pendiente || !Guardado.ModoCoop || plugin.Jugador == null || Time.timeSinceLevelLoad < 3f) return;
            bool yaTiene = false;
            try { yaTiene = DialogueLua.GetVariable(VarKit).AsBool; } catch { }
            pendiente = false;
            if (yaTiene) { plugin.Log("Kit: esta partida ya recibió su equipo inicial"); return; }
            Dar();
        }

        void Dar()
        {
            var g = FsmVariables.GlobalVariables;
            var gz = g.FindFsmInt("Lockpicks"); if (gz != null) gz.Value = Ganzuas;
            DialogueLua.SetVariable(Municion.VarPistola, Balas);
            DialogueLua.SetVariable(Municion.VarMosquete, Balas);
            DialogueLua.SetVariable(Municion.VarEscopeta, Balas);
            var bal = g.FindFsmInt("silver_bullets_v2"); if (bal != null) bal.Value = Balas;
            plugin.municion.Recargar();
            if (plugin.EsAnfitrion)
            {
                var c = g.FindFsmInt("Coins"); if (c != null) c.Value = Oro;
            }
            DialogueLua.SetVariable(VarKit, true);
            // la dificultad que eligió el anfitrión al crear la partida queda guardada en ella
            if (plugin.EsAnfitrion && Opciones.DificultadElegida >= 0)
            {
                Opciones.Dificultad = Opciones.DificultadElegida;
                plugin.Log("Kit: dificultad de la partida " + Opciones.NombresDificultad[Opciones.DificultadElegida]);
                Opciones.DificultadElegida = -1;
            }
            if (plugin.EsAnfitrion && Opciones.FuegoAmigoElegido >= 0)
            {
                DialogueLua.SetVariable(Opciones.VarFuegoAmigo, Opciones.FuegoAmigoElegido == 1);
                plugin.Log("Kit: fuego amigo " + (Opciones.FuegoAmigoElegido == 1 ? "activado" : "desactivado"));
                Opciones.FuegoAmigoElegido = -1;
            }
            plugin.Log("Kit: " + Ganzuas + " ganzúas, " + Balas + " balas por arma" + (plugin.EsAnfitrion ? ", " + Oro + " de oro" : " (el oro lo pone el anfitrión)"));
            guardarEn = Time.realtimeSinceStartup + 1.5f;
        }

        /// (2026-11-19) El juego guarda al cerrar la pantalla de carga ("Presiona cualquier botón para guardar y continuar") ANTES
        /// de que se dé el kit (el tiempo de escena no corre con esa pantalla en pausa). Si se cerraba el juego antes del siguiente
        /// guardado, al Continuar no había kit (50 de oro), ni la dificultad ni el fuego amigo elegidos. Se guarda en silencio como
        /// el propio juego: evento global SilentSave de la FSM SaveLoad (Dialogue Manager/!!!!!!!!!!!!saveload).
        void Guardar()
        {
            if (plugin.Jugador == null || !Guardado.ModoCoop) { plugin.Log("Kit: no guardé (sin jugador o fuera del cooperativo)"); return; }
            foreach (var f in Object.FindObjectsOfType<PlayMakerFSM>())
                if (f != null && f.FsmName == "SaveLoad")
                {
                    f.SendEvent("SilentSave");
                    plugin.Log("Kit: partida guardada con el equipo inicial (SilentSave, estado '" + f.ActiveStateName + "')");
                    return;
                }
            plugin.Log("Kit: no encontré la máquina de guardado del juego (SaveLoad)");
        }

        public void OrdenDar() { Dar(); }
        public string Describir() { bool k = false; try { k = DialogueLua.GetVariable(VarKit).AsBool; } catch { } return "pendiente=" + pendiente + " recibido=" + k; }
    }
}

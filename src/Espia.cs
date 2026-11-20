// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using HarmonyLib;
using HutongGames.PlayMaker;
using UnityEngine;

namespace WitchHuntCoop
{
    /// Depuración: avisa quién escribe la global LevelToLoad (qué FSM y en qué estado).
    [HarmonyPatch(typeof(FsmString), "set_Value")]
    static class EspiaNivel
    {
        /// Última FSM que escribió el destino (para devolver su botón a como estaba si el viaje se bloquea).
        public static Fsm Ultimo; public static float UltimoHora;
        static void Postfix(FsmString __instance, string value)
        {
            if (string.IsNullOrEmpty(value) || __instance.Name != "LevelToLoad") return;
            var f = FsmExecutionStack.ExecutingFsm;
            Ultimo = f; UltimoHora = Time.realtimeSinceStartup;
            Debug.Log("[WitchHuntCoop] LevelToLoad='" + value + "' por " + (f != null ? (f.GameObject != null ? f.GameObject.name : "?") + "/" + f.Name + " [" + f.ActiveStateName + "]" : "código"));
        }
    }
}

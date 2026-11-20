// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using HarmonyLib;
using PixelCrushers.DialogueSystem;
using UnityEngine;

namespace WitchHuntCoop
{
    /// El juego recuerda en la partida cada objeto recogido o destruido (PersistentDestructible guarda una variable
    /// "destruido" por objeto) y al volver a cargar el bosque los vuelve a quitar. En el cooperativo el botín se
    /// regenera en CADA entrada al bosque (pedido 2026-11-11): al aplicar los datos guardados, los recogibles y cofres
    /// del bosque se dan por "no destruidos".
    [HarmonyPatch(typeof(PersistentDestructible), "OnApplyPersistentData")]
    static class RegenerarBotin
    {
        public static int Reiniciados;
        static readonly string[] Extra = { "Chest_static", "ChestParrent", "Chest_locked", "Skill_book" };

        public static bool EsBotin(string nombre)
        {
            if (nombre.StartsWith("GoodDog")) return true;   // el perro de la celda vuelve a existir en cada entrada: el compañero se copia de su modelo animado
            string b = Catalogo.BaseDe(nombre);
            foreach (var t in Mundo.TiposBotin) if (b == t) return true;
            foreach (var t in Extra) if (b == t) return true;
            return false;
        }

        static void Prefix(PersistentDestructible __instance)
        {
            if (!Guardado.ModoCoop || __instance == null) return;
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex != Mundo.EscenaBosque) return;
            if (!EsBotin(__instance.gameObject.name)) return;
            try
            {
                string v = string.IsNullOrEmpty(__instance.variableName) ? OverrideActorName.GetInternalName(__instance.transform) : __instance.variableName;
                if (DialogueLua.GetVariable(v).AsBool) { DialogueLua.SetVariable(v, false); Reiniciados++; }
            }
            catch { }
        }
    }
}

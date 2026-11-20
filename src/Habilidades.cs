// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using System;
using System.Collections.Generic;
using HarmonyLib;
using HutongGames.PlayMaker;
using UnityEngine;

namespace WitchHuntCoop
{
    /// Efectos de los libros que no dependen de un arma:
    ///  - Robin Hood: al matar un enemigo o guardián, +1–5 de oro (siempre).
    ///  - Vampiro: al matar, 40 % de curar el 4 % de la vida máxima.
    ///  - Mercader soberano: +45 % de las monedas que dan los objetos valiosos al recogerlos.
    ///  (Cargamento pesado sube el máximo de balas: ver LibrosTienda.Actualizar.)
    public static class Habilidades
    {
        public static Plugin plugin;
        static readonly System.Random azar = new System.Random();

        // ---------------- Muertes hechas por mí ----------------

        /// Lo llama el parche de TakeDamage cuando un golpe MÍO (no del compañero) mata a un enemigo.
        public static void Mate(TakeDamage td)
        {
            if (plugin == null || !Guardado.ModoCoop) return;
            string quien = td != null ? Combate.NombreDe(td) : "?";
            if (Aliados.Instancia != null) try { Aliados.Instancia.Tragar(td); } catch (Exception e) { plugin.Log("Bola: error al tragar " + e); }
            // Enemigo de ojos rojos (potenciado): recompensas ×4 y el maná se llena entero.
            bool rojo = td != null && Potenciado.Es(td);
            int x = rojo ? 4 : 1;
            if (rojo) LlenarMana(quien);
            if (LibrosTienda.Tiene(LibrosTienda.RobinHood))
            {
                int oro = (1 + azar.Next(5)) * x;
                Oro.Gastar(-oro);
                Viaje.Avisar("+" + oro + " de oro (Robin Hood" + (rojo ? " ×4" : "") + ")");
                plugin.Log("Robin Hood: +" + oro + " de oro por " + quien);
            }
            if (LibrosTienda.Tiene(LibrosTienda.Vampiro) && azar.NextDouble() < 0.40)
            {
                var j = plugin.Jugador;
                if (j != null)
                {
                    var g = FsmVariables.GlobalVariables.FindFsmFloat("Player_Max_Health");
                    float max = g != null && g.Value > 0f ? g.Value : 100f;
                    float cura = max * 0.04f * x;
                    j.HealPlayer(cura);
                    plugin.Log("Vampiro: +" + cura.ToString("0.#") + " de vida por " + quien);
                }
            }
        }

        /// Maná al máximo. (2026-11-19) El maná actual es la variable de diálogo "ManaCurrent" (Muerte.VarMana), no una global de
        /// PlayMaker: antes se buscaba en las globales, no se encontraba y el maná nunca se llenaba. Máximo 100 (el inventario lo
        /// muestra como "x/100").
        public const int ManaMaxima = 100;
        static void LlenarMana(string quien)
        {
            float antes = Muerte.LeerMana();
            Muerte.PonerMana(ManaMaxima);
            plugin.Log("Maná: lleno (" + antes + " → " + ManaMaxima + ") por " + quien);
        }

        // ---------------- Mercader soberano ----------------

        static readonly string[] Valiosos = {
            "Bronzeplate_pickable", "Bronzestatuette_pickable", "Bronzevase_pickable", "Dinamicvase_pickable", "Gabled_pickable",
            "Gold_Nugget", "Goldenfigure_pickable", "Goldenplate_pickable", "Goldidol_pickable", "silverbars_pickable",
        };
        static readonly Dictionary<GameObject, bool> cerca = new Dictionary<GameObject, bool>();
        static float horaRecogido = -10f, proximaBusqueda;
        static int oroVisto = int.MinValue;

        public static void Actualizar()
        {
            var j = plugin != null ? plugin.Jugador : null;
            if (j == null || !Guardado.ModoCoop) { oroVisto = int.MinValue; cerca.Clear(); return; }
            int? oro = Oro.Leer();
            if (oro == null) return;
            // Un objeto valioso que estaba a mi lado desapareció: lo acabo de recoger.
            List<GameObject> fuera = null;
            foreach (var g in cerca.Keys) if (g == null || !g.activeInHierarchy) (fuera ?? (fuera = new List<GameObject>())).Add(g);
            if (fuera != null) { foreach (var g in fuera) cerca.Remove(g); horaRecogido = Time.realtimeSinceStartup; }
            if (Time.realtimeSinceStartup > proximaBusqueda && LibrosTienda.Tiene(LibrosTienda.MercaderSoberano))   // solo hace falta con el libro
            {
                proximaBusqueda = Time.realtimeSinceStartup + 0.5f;
                foreach (var c in UnityEngine.Object.FindObjectsOfType<PickupButton>())
                {
                    Transform t = c.transform; if (t.parent != null) t = t.parent;
                    if ((t.position - j.transform.position).sqrMagnitude > 25f) continue;
                    string b = Catalogo.BaseDe(t.name.Replace(" [coop]", ""));
                    if (Array.IndexOf(Valiosos, b) >= 0) cerca[t.gameObject] = true;
                }
            }
            if (oroVisto != int.MinValue && oro.Value > oroVisto && Time.realtimeSinceStartup - horaRecogido < 1.5f && LibrosTienda.Tiene(LibrosTienda.MercaderSoberano))
            {
                int extra = Mathf.Max(1, Mathf.RoundToInt((oro.Value - oroVisto) * 0.45f));
                Oro.Gastar(-extra);
                horaRecogido = -10f;
                Viaje.Avisar("+" + extra + " de oro");   // sin «(Mercader soberano)» (pedido 2026-11-17)
                plugin.Log("Mercader soberano: +" + extra + " sobre " + (oro.Value - oroVisto));
                oro = Oro.Leer();
            }
            oroVisto = oro.Value;
        }
    }

    /// Detecta mis muertes: el golpe que pone en verdadero la casilla "dead" de la vida del enemigo.
    [HarmonyPatch(typeof(TakeDamage), "Damage")]
    public static class ParcheMuerteEnemigo
    {
        public static int Golpes; public static float Dano;   // prueba: golpes míos desde la última consulta

        static void Prefix(TakeDamage __instance, float damage, out bool __state)
        {
            if (!Combate.AplicandoRemoto) { Golpes++; Dano += damage; }
            __state = Muerto(__instance);
        }

        static void Postfix(TakeDamage __instance, bool __state)
        {
            try
            {
                if (__state || Combate.AplicandoRemoto) return;   // ya estaba muerto, o lo golpeó el compañero
                if (Muerto(__instance)) Habilidades.Mate(__instance);
            }
            catch (Exception e) { Debug.LogError("[WitchHuntCoop] muerte: " + e); }
        }

        static bool Muerto(TakeDamage td)
        {
            if (td == null || string.IsNullOrEmpty(td.NameOfFsm)) return false;
            var f = PlayMakerFSM.FindFsmOnGameObject(td.gameObject, td.NameOfFsm);
            if (f == null) return false;
            // La vida es de cada enemigo ("Health"); la casilla "dead" de su FSM es una variable global.
            var h = f.FsmVariables.FindFsmFloat("Health");
            if (h != null) return h.Value <= 0f;
            var v = string.IsNullOrEmpty(td.NameOfVariableThatReceiveTheDamage) ? null : f.FsmVariables.FindFsmFloat("health");
            return v != null && v.Value <= 0f;
        }
    }
}

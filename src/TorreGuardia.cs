// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using System.Collections.Generic;
using UnityEngine;

namespace WitchHuntCoop
{
    /// Guardia de la Torre del Homenaje (pedido 2026-11-18): 4 enemigos fijos dentro de la torre. Los que mueren quedan muertos
    /// para siempre en la partida (variable de Lua), salvo en Pesadilla, donde no se cuentan las muertes (siempre vuelven los 4;
    /// además allí reviven al minuto con la regla general de Pesadilla). Solo el anfitrión los genera, como los guardianes.
    public partial class Mundo
    {
        public const string VarTorreMuertos = "coop_torre_muertos";
        const int GuardiasTorre = 4;
        static readonly string[] TiposTorre = { "Undead_naked", "Deadblind", "Elongata_ai", "BigSpider_ai" };
        GameObject torreDe; bool torreGenerada;
        readonly List<TakeDamage> guardiasTorre = new List<TakeDamage>();
        readonly HashSet<TakeDamage> guardiasContados = new HashSet<TakeDamage>();

        int MuertosTorre()
        {
            try { var v = PixelCrushers.DialogueSystem.DialogueLua.GetVariable(VarTorreMuertos); if (v.AsString != "nil" && v.AsString != "") return Mathf.Clamp(v.AsInt, 0, GuardiasTorre); } catch { }
            return 0;
        }

        void ActualizarTorre()
        {
            if (castilloRaiz == null) { torreDe = null; torreGenerada = false; guardiasTorre.Clear(); guardiasContados.Clear(); return; }
            if (torreDe != castilloRaiz) { torreDe = castilloRaiz; torreGenerada = false; guardiasTorre.Clear(); guardiasContados.Clear(); }
            if (!plugin.EsAnfitrion && plugin.Conectado) return;
            if (plugin.CombateActual == null) return;
            var pa = castilloRaiz.transform;
            Vector3 centro = pa.TransformPoint(new Vector3(TorreX, 0f, TorreZ));
            bool pesadilla = Opciones.Dificultad >= 2;
            // muertes (solo cuentan fuera de Pesadilla)
            foreach (var td in guardiasTorre)
            {
                if (td == null || guardiasContados.Contains(td)) continue;
                bool muerto; try { muerto = Eventos.Muerto(td); } catch { muerto = false; }
                if (!muerto) continue;
                guardiasContados.Add(td);
                if (!pesadilla) { int m = MuertosTorre() + 1; PixelCrushers.DialogueSystem.DialogueLua.SetVariable(VarTorreMuertos, m); plugin.Log("Torre: guardia caído (" + m + " de " + GuardiasTorre + ")"); }
            }
            if (torreGenerada) return;
            var j = plugin.Jugador; var c = plugin.CompaneroActual;
            bool cerca = (j != null && Vector3.Distance(j.transform.position, centro) < 90f) || (c != null && c.Visible && Vector3.Distance(c.PosicionMundo, centro) < 90f);
            if (!cerca) return;
            torreGenerada = true;
            int quedan = pesadilla ? GuardiasTorre : GuardiasTorre - MuertosTorre();
            var rng = new System.Random(SemillaPartida ^ Hash("coop_torre_guardia"));
            int hechos = 0;
            for (int k = 0; k < quedan; k++)
            {
                float a = (k + 0.5f) / GuardiasTorre * Mathf.PI * 2f;
                float r = (TorreR - TorreMuro) * 0.55f;
                Vector3 p = pa.TransformPoint(new Vector3(TorreX + Mathf.Cos(a) * r, 0f, TorreZ + Mathf.Sin(a) * r));
                // el piso de la torre: el primer suelo por debajo de la terraza
                RaycastHit h;
                if (Physics.Raycast(p + Vector3.up * 6f, Vector3.down, out h, 30f, ~0, QueryTriggerInteraction.Ignore)) p = h.point;
                var td = plugin.CombateActual.Generar(TiposTorre[rng.Next(TiposTorre.Length)], p + Vector3.up * 0.4f);
                if (td != null) { guardiasTorre.Add(td); hechos++; }
            }
            plugin.Log("Torre: " + hechos + " guardias en la Torre del Homenaje (" + (pesadilla ? "Pesadilla: siempre 4" : MuertosTorre() + " ya muertos en la partida") + ")");
        }
    }
}

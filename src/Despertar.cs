// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace WitchHuntCoop
{
    /// Enemigos "estatua" (pedido 2026-11-10): si un enemigo que simula este juego lleva varios segundos
    /// quieto con un jugador cerca, se le da al jugador como objetivo (variable "Target" de sus máquinas de
    /// estados) y se le manda hacia él. Los jefes y los personajes protegidos no se tocan.
    public class Despertar
    {
        readonly Plugin plugin;
        class Seguimiento { public float PrimerQuieto = -1f; public Vector3 Pos; public float Desde; public bool Despierto; public List<HutongGames.PlayMaker.FsmGameObject> Vars; }
        readonly Dictionary<Combate.Unidad, Seguimiento> vistos = new Dictionary<Combate.Unidad, Seguimiento>();
        float proxima;
        public const float TiempoQuieto = 2f, Radio = 45f, SegundosParaQuitar = 2.5f, CuerpoACuerpo = 3.5f;
        public int Despertados;

        public Despertar(Plugin p) { plugin = p; }

        readonly Dictionary<Combate.Unidad, List<HutongGames.PlayMaker.FsmGameObject>> varsLuna = new Dictionary<Combate.Unidad, List<HutongGames.PlayMaker.FsmGameObject>>();

        /// Pone al jugador como objetivo del enemigo y lo manda hacia él (con la Luna de sangre, sin importar la distancia).
        public void Acosar(Combate.Unidad u, FPSPlayer j)
        {
            List<HutongGames.PlayMaker.FsmGameObject> vs;
            if (!varsLuna.TryGetValue(u, out vs))
            {
                vs = new List<HutongGames.PlayMaker.FsmGameObject>();
                Transform raiz = u.Raiz.parent != null ? u.Raiz.parent : u.Raiz;
                foreach (var f in raiz.GetComponentsInChildren<PlayMakerFSM>(true))
                    foreach (var v in f.FsmVariables.GameObjectVariables) if (v.Name == "Target") vs.Add(v);
                varsLuna[u] = vs;
            }
            foreach (var v in vs) if (v.Value == null) v.Value = j.gameObject;   // solo si no tiene a nadie (no se le quita un aliado)
            if (u.Agente != null && u.Agente.isActiveAndEnabled && u.Agente.isOnNavMesh) { u.Agente.isStopped = false; u.Agente.SetDestination(j.transform.position); }
        }

        public void Actualizar()
        {
            if (Time.time < proxima) return;
            proxima = Time.time + (Eventos.Luna ? 1.5f : 0.5f);
            var c = plugin.CombateActual; var j = plugin.Jugador;
            if (c == null || j == null || plugin.Escena != Mundo.EscenaBosque) return;
            foreach (var u in c.Unidades)
            {
                if (!u.Viva || u.Remota || u.Raiz == null || Aliados.Protegido(u.Raiz) || (u.Td != null && Dificultad.EsJefe(u.Td))) continue;
                Vector3 m = (u.Mover != null ? u.Mover : u.Raiz).position;
                float d = Vector3.Distance(m, j.transform.position);
                if (Eventos.Luna || (u.Nombre != null && u.Nombre.Contains(Mundo.MarcaRefuerzo))) { Acosar(u, j); continue; }   // Luna de sangre y refuerzos: a por ti, estés donde estés   // Luna de sangre: todos a por ti, estés donde estés
                Seguimiento s;
                if (!vistos.TryGetValue(u, out s)) { s = new Seguimiento { Pos = m, Desde = Time.time }; vistos[u] = s; }
                if ((m - s.Pos).sqrMagnitude > 0.35f * 0.35f) { s.Pos = m; s.Desde = Time.time; s.Despierto = false; s.PrimerQuieto = -1f; continue; }   // se mueve: bien
                if (d > Radio || Time.time - s.Desde < TiempoQuieto) continue;
                // quieto con un jugador cerca: a por él
                if (s.Vars == null)
                {
                    s.Vars = new List<HutongGames.PlayMaker.FsmGameObject>();
                    Transform raiz = u.Raiz.parent != null ? u.Raiz.parent : u.Raiz;
                    foreach (var f in raiz.GetComponentsInChildren<PlayMakerFSM>(true))
                        foreach (var v in f.FsmVariables.GameObjectVariables) if (v.Name == "Target") s.Vars.Add(v);
                }
                foreach (var v in s.Vars) if (v.Value != j.gameObject) v.Value = j.gameObject;
                if (u.Agente != null && u.Agente.isActiveAndEnabled && u.Agente.isOnNavMesh) { u.Agente.isStopped = false; u.Agente.SetDestination(j.transform.position); }
                if (!s.Despierto) { s.Despierto = true; Despertados++; plugin.Log("Despertar: '" + u.Nombre + "' llevaba " + Mathf.RoundToInt(Time.time - s.Desde) + " s quieto a " + Mathf.RoundToInt(d) + " m; va a por el jugador"); }
                // Un enemigo que, ya mandado a por el jugador, sigue 2,5 s sin moverse (y no está pegado a ti) es un modelo decorativo atascado: se quita del mapa.
                if (d < CuerpoACuerpo) { s.PrimerQuieto = -1f; }   // pegado a ti está atacando: no cuenta como quieto
                else if (s.PrimerQuieto < 0f) s.PrimerQuieto = Time.time;
                else if (Time.time - s.PrimerQuieto > SegundosParaQuitar)
                {
                    Transform grupo = u.Raiz.parent != null ? u.Raiz.parent : u.Raiz;
                    plugin.Log("Despertar: '" + u.Nombre + "' no se movió en " + SegundosParaQuitar + " s ni mandado a por el jugador: se quita del mapa");
                    vistos.Remove(u);
                    Object.Destroy(grupo.gameObject);
                    continue;
                }
                s.Desde = Time.time - TiempoQuieto + 1.5f;   // se vuelve a comprobar en 1,5 s
            }
            // limpiar las que ya no existen
            if (vistos.Count > 400) { var rm = new List<Combate.Unidad>(); foreach (var kv in vistos) if (kv.Key == null || !kv.Key.Viva) rm.Add(kv.Key); foreach (var k in rm) vistos.Remove(k); }
        }
    }
}

// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using System.Collections.Generic;
using UnityEngine;

namespace WitchHuntCoop
{
    /// Quita del mapa a los enemigos que no sirven: los invisibles (todas sus mallas apagadas) y los que aparecen y se quedan quietos
    /// con un jugador cerca sin atacarlo. Jefes, aliados, el perro y las serpientes (que esperan enroscadas) no se tocan.
    /// Corre igual en los dos juegos (sin red): lo que uno quita, el otro también lo ve quieto o invisible y lo quita.
    public class Limpieza
    {
        const float QuietoSegundos = 8f, CercaJugador = 45f, PegadoAlJugador = 7f;
        readonly Plugin plugin;
        float prox;
        readonly Dictionary<int, Vector3> refPos = new Dictionary<int, Vector3>();
        readonly Dictionary<int, float> refT = new Dictionary<int, float>(), invisibleDesde = new Dictionary<int, float>();
        public Limpieza(Plugin p) { plugin = p; }

        /// Espíritus blancos del juego (WhiteShadowParrent y _v2): guiaban a los libros de su sitio original,
        /// pero el mod reparte el botín al azar en cada entrada y llevaban a lugares vacíos (pedido 2026-11-17).
        void QuitarEspiritus()
        {
            foreach (var n in new[] { "WhiteShadowParrent", "WhiteShadowParrent_v2" })
            {
                var g = GameObject.Find(n);
                if (g != null && g.transform.parent == null) { g.SetActive(false); plugin.Log("Limpieza: espíritu blanco " + n + " quitado"); }
            }
        }

        public void Actualizar()
        {
            float t = Time.realtimeSinceStartup;
            if (t < prox) return; prox = t + 1f;
            if (plugin.Escena != Mundo.EscenaBosque || plugin.Jugador == null) return;
            QuitarEspiritus();
            Vector3 yo = plugin.Jugador.transform.position;
            var comp = plugin.CompaneroActual; bool hayC = comp != null && comp.Visible; Vector3 pc = hayC ? comp.PosicionMundo : yo;
            int quitadosInv = 0, quitadosQuietos = 0;
            foreach (var td in Object.FindObjectsOfType<TakeDamage>())
            {
                if (td == null || td.GetComponentInParent<FPSPlayer>() != null) continue;
                if (Dificultad.EsJefe(td)) continue;
                Transform u = Combate.RaizDe(td);
                if (u == null || !u.gameObject.activeInHierarchy || Aliados.Protegido(u) || u.name.StartsWith("coop_") || u.name.Contains("Snake")) continue;
                if (u.GetComponentInChildren<CharacterDamagePlaymakerAI>() == null || Eventos.Muerto(td)) continue;
                int id = u.GetInstanceID(); Vector3 p = u.position;
                float dj = Vector3.Distance(p, yo), dc = Vector3.Distance(p, pc), d = Mathf.Min(dj, dc);
                // invisible: ninguna malla encendida (2 s seguidos, por si se está armando)
                bool vis = false;
                foreach (var r in u.GetComponentsInChildren<Renderer>(false)) if (r.enabled && r.gameObject.activeInHierarchy && !(r is ParticleSystemRenderer) && !(r is TrailRenderer)) { vis = true; break; }
                if (!vis)
                {
                    if (!invisibleDesde.ContainsKey(id)) invisibleDesde[id] = t;
                    if (t - invisibleDesde[id] > 2f && d < 150f) { Quitar(u, "invisible"); quitadosInv++; continue; }
                }
                else invisibleDesde.Remove(id);
                // quieto: sin moverse 8 s con un jugador entre 7 y 45 m (a menos de 7 m está peleando)
                if (!refPos.ContainsKey(id)) { refPos[id] = p; refT[id] = t; continue; }
                if ((p - refPos[id]).sqrMagnitude > 0.36f) { refPos[id] = p; refT[id] = t; continue; }
                if (d > PegadoAlJugador && d < CercaJugador && t - refT[id] > QuietoSegundos) { Quitar(u, "quieto"); quitadosQuietos++; }
            }
            if (quitadosInv + quitadosQuietos > 0) plugin.Log("Limpieza: quitados " + quitadosInv + " invisibles y " + quitadosQuietos + " quietos");
        }

        void Quitar(Transform u, string motivo)
        {
            int id = u.GetInstanceID(); refPos.Remove(id); refT.Remove(id); invisibleDesde.Remove(id);
            plugin.Log("Limpieza: se quita " + u.name + " (" + motivo + ") en " + (u.position - Anclaje.Origen()).ToString("F0"));
            Object.Destroy(u.gameObject);
        }
    }
}

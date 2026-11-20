// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace WitchHuntCoop
{
    /// Enemigos de las dos minas de la ballesta (pedido 2026-11-13): siempre 2 enemigos "de luna de sangre" (potenciados) dentro de cada mina,
    /// en cualquier cruce lejos de los jugadores; si uno muere, reaparece otro en un sitio al azar unos 25 s después.
    /// Solo los genera el anfitrión; existen mientras alguien esté dentro de la mina (al salir los dos se quitan).
    public partial class Mundo
    {
        class EstadoMinaEnemigos { public List<TakeDamage> Vivos = new List<TakeDamage>(), Muertos = new List<TakeDamage>(); public float Prox; public bool Nav; public NavMeshDataInstance NavDatos; }
        readonly Dictionary<int, EstadoMinaEnemigos> enemigosMinas = new Dictionary<int, EstadoMinaEnemigos>();
        public const int EnemigosPorMina = 1; public const float RetardoReaparicionMina = 90f;
        static readonly string[] TiposMina = { "BigSpider_ai", "Deadblind", "Elongata_ai", "Undead_naked" };

        void LimpiarEnemigosMinas()
        {
            foreach (var st in enemigosMinas.Values) { foreach (var td in st.Vivos) if (td != null) Object.Destroy(td.transform.root.gameObject); foreach (var td in st.Muertos) if (td != null) Object.Destroy(td.transform.root.gameObject); if (st.Nav && st.NavDatos.valid) NavMesh.RemoveNavMeshData(st.NavDatos); }
            enemigosMinas.Clear();
        }

        void ActualizarEnemigosMinas()
        {
            if (!plugin.EsAnfitrion || !plugin.Conectado || plugin.CombateActual == null || plugin.Escena != EscenaBosque) return;
            var j = plugin.Jugador; var comp = plugin.CompaneroActual;
            foreach (var m in minas)
            {
                if (m.Guarida || m.Red == null) continue;
                EstadoMinaEnemigos st;
                if (!enemigosMinas.TryGetValue(m.N, out st)) enemigosMinas[m.N] = st = new EstadoMinaEnemigos();
                if (st.Vivos.RemoveAll(x => x == null) > 0) { st.Prox = Time.time + 4f; plugin.Log("Mina " + (m.N + 1) + ": un enemigo desapareció solo (el juego lo quita si no llega al jugador); se reintenta"); }
                st.Muertos.RemoveAll(x => x == null);
                if (!m.Red.activeSelf)
                {
                    foreach (var td in st.Vivos) if (td != null) Object.Destroy(td.transform.root.gameObject);
                    foreach (var td in st.Muertos) if (td != null) Object.Destroy(td.transform.root.gameObject);
                    st.Vivos.Clear(); st.Muertos.Clear(); st.Prox = 0f; continue;
                }
                if (!st.Nav) { ConstruirNavMina(m, st); st.Prox = Time.time + 1.5f; continue; }   // el mapa de navegación necesita un momento antes de poder generar
                // muerto: su cadáver se queda y a los 90 s desaparece y aparece otro enemigo en un sitio al azar
                for (int k = st.Vivos.Count - 1; k >= 0; k--)
                    if (Eventos.Muerto(st.Vivos[k])) { st.Muertos.Add(st.Vivos[k]); st.Vivos.RemoveAt(k); st.Prox = Time.time + RetardoReaparicionMina; plugin.Log("Mina " + (m.N + 1) + ": murió el enemigo potenciado; otro aparece en " + (int)RetardoReaparicionMina + " s"); }
                if (Time.time < st.Prox) continue;
                foreach (var td in st.Muertos) if (td != null) Object.Destroy(td.transform.root.gameObject);   // se va el cadáver
                st.Muertos.Clear();
                Desatascar(m, st);
                if (st.Vivos.Count >= EnemigosPorMina) continue;
                // un cruce al azar a más de 22 m de los jugadores
                Vector3 o = m.Red.transform.position;
                st.Prox = Time.time + 1.5f;   // si no se logra, se reintenta en 1,5 s
                for (int intento = 0; intento < 30; intento++)
                {
                    int x = Random.Range(0, MinaAncho), z = Random.Range(0, MinaFondo);
                    if (x == 0 && z == 0) continue;
                    Vector3 pos = o + new Vector3(x * MinaPaso, 0.3f, z * MinaPaso);
                    if (j != null && Vector3.Distance(pos, j.transform.position) < 22f) continue;
                    if (comp != null && comp.Visible && Vector3.Distance(pos, comp.PosicionMundo) < 22f) continue;
                    // que el jugador pueda llegar a pie hasta ahí (si no, el propio juego quita al enemigo: "Destroy_if_!reachable")
                    NavMeshHit nh, nj; var cam = new NavMeshPath();
                    if (!NavMesh.SamplePosition(pos, out nh, 3f, NavMesh.AllAreas)) continue;
                    // justo sobre el pasillo y sin paredes alrededor (uno apareció metido en el muro, con los ojos asomando; 2026-11-17)
                    if (new Vector2(nh.position.x - pos.x, nh.position.z - pos.z).magnitude > 1.2f) continue;
                    if (Physics.CheckCapsule(nh.position + Vector3.up * 0.7f, nh.position + Vector3.up * 1.6f, 0.55f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    if (j != null && (!NavMesh.SamplePosition(j.transform.position, out nj, 4f, NavMesh.AllAreas) || !NavMesh.CalculatePath(nh.position, nj.position, NavMesh.AllAreas, cam) || cam.status != NavMeshPathStatus.PathComplete)) continue;
                    string tipo = TiposMina[Random.Range(0, TiposMina.Length)];
                    var td = plugin.CombateActual.Generar(tipo + Potenciado.Marca, pos);
                    if (td == null) break;
                    st.Vivos.Add(td);
                    plugin.Log("Mina " + (m.N + 1) + ": enemigo potenciado '" + tipo + "' generado en el cruce (" + x + ", " + z + ")");
                    break;
                }
            }
        }

        /// Enemigo de la mina fuera del pasillo (metido en una pared o fuera de la red): se lleva al punto navegable más cercano;
        /// si no hay ninguno cerca, se quita (y aparece otro en su lugar con el tiempo normal).
        void Desatascar(Mina m, EstadoMinaEnemigos st)
        {
            for (int k = st.Vivos.Count - 1; k >= 0; k--)
            {
                var td = st.Vivos[k]; if (td == null) { st.Vivos.RemoveAt(k); continue; }
                var ag = td.GetComponentInParent<NavMeshAgent>(); if (ag == null) continue;
                NavMeshHit h; bool cerca = NavMesh.SamplePosition(ag.transform.position, out h, 4f, NavMesh.AllAreas);
                float fuera = cerca ? new Vector2(h.position.x - ag.transform.position.x, h.position.z - ag.transform.position.z).magnitude : 99f;
                if (ag.isOnNavMesh && fuera < 0.8f) continue;
                if (cerca) { ag.Warp(h.position); plugin.Log("Mina " + (m.N + 1) + ": enemigo atascado (" + fuera.ToString("0.0") + " m fuera del pasillo) devuelto al pasillo"); }
                else { Object.Destroy(td.transform.root.gameObject); st.Vivos.RemoveAt(k); st.Prox = Time.time + 1.5f; plugin.Log("Mina " + (m.N + 1) + ": enemigo fuera de la red quitado"); }
            }
        }

        void ConstruirNavMina(Mina m, EstadoMinaEnemigos st)
        {
            st.Nav = true;
            try
            {
                var b = new Bounds(m.Red.transform.position + new Vector3(MinaAncho * MinaPaso / 2f, 1.7f, MinaFondo * MinaPaso / 2f), new Vector3(MinaAncho * MinaPaso + 12f, 8f, MinaFondo * MinaPaso + 12f));
                var fuentes = new List<NavMeshBuildSource>();
                NavMeshBuilder.CollectSources(b, ~0, NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), fuentes);
                var datos = NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByIndex(0), fuentes, b, Vector3.zero, Quaternion.identity);
                st.NavDatos = NavMesh.AddNavMeshData(datos);
                plugin.Log("Mina " + (m.N + 1) + ": mapa de navegación con " + fuentes.Count + " fuentes");
            }
            catch (System.Exception e) { plugin.Log("Mina " + (m.N + 1) + ": no pude armar la navegación: " + e.Message); }
        }
    }
}

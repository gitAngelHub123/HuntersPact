// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace WitchHuntCoop
{
    /// Población de enemigos (pedido 2026-11-17; reemplaza a los refuerzos por distancia recorrida, que se quitaron):
    ///  - Normal: hasta 8 enemigos por jugador, que aparecen entre 50 y 400 m de él.
    ///  - Luna de sangre: hasta 16 por jugador, entre 20 y 150 m.
    ///  - Más lejos no aparece nada (salvo el jefe opcional). Los enemigos de la población que quedan lejos de los
    ///    dos jugadores se quitan.
    /// Alrededor del jugador de cada juego manda el spawner del propio juego (Crux: máximo, radios y borrado se
    /// ajustan aquí). Alrededor del compañero, el anfitrión repone a mano (Crux solo mira a un jugador).
    public partial class Mundo
    {
        public const int MaxPorJugador = 8, MaxPorJugadorLuna = 16;
        public const float RadioPoblacion = 400f, RadioMinNormal = 50f, RadioLunaMin = 20f, RadioLunaMax = 150f;   // luna de sangre: 20–150 m (pedido 2026-11-17)
        public static int MaxActual { get { return Eventos.Luna ? MaxPorJugadorLuna : MaxPorJugador; } }
        public static float RadioMaxActual { get { return Eventos.Luna ? RadioLunaMax : RadioPoblacion; } }
        float RadioMinActual { get { return Eventos.Luna ? RadioLunaMin : RadioMinNormal; } }   // normal: 50–400 m (pedido 2026-11-17)
        public const float LejosRefuerzo = 450f;
        public int Refuerzos;
        float proxRefuerzo;
        readonly List<TakeDamage> refuerzosVivos = new List<TakeDamage>();
        float proxCuidado;
        public const string MarcaRefuerzo = " +R";
        Crux crux; int cruxVisto = -1; float radioMinCrux = 150f; bool lunaPuesta;

        /// Ajusta el spawner del juego (los dos juegos: inofensivo en el invitado).
        void AjustarCrux()
        {
            if (crux == null || cruxVisto != plugin.Escena)
            {
                crux = Object.FindObjectOfType<Crux>(); cruxVisto = plugin.Escena; lunaPuesta = !Eventos.Luna;
                if (crux != null) { radioMinCrux = Mathf.Clamp(crux._MinRadius, 30f, RadioPoblacion - 100f); plugin.Log("Población: Crux original máx " + crux._MaxObjectsToSpawn + ", radio " + crux._MinRadius + "–" + crux._MaxRadius + ", borra a " + crux._DespawnRadius); }
            }
            if (crux == null) return;
            bool luna = Eventos.Luna;
            int max = MaxActual; int rMin = Mathf.RoundToInt(RadioMinActual), rMax = Mathf.RoundToInt(RadioMaxActual);
            if (crux._MaxObjectsToSpawn != max || crux._MinRadius != rMin || crux._MaxRadius != rMax)
            {
                crux._MaxObjectsToSpawn = max; crux._MinRadius = rMin; crux._MaxRadius = rMax; crux._DespawnRadius = RadioPoblacion + 50f;
                if (luna != lunaPuesta) { lunaPuesta = luna; plugin.Log("Población: " + (luna ? "luna de sangre" : "normal") + " → máx " + max + " por jugador, " + rMin + "–" + rMax + " m"); }
            }
        }

        void ActualizarRefuerzos()
        {
            if (plugin.Escena != EscenaBosque) { crux = null; return; }
            AjustarCrux();
            if (!plugin.EsAnfitrion || !plugin.Conectado || plugin.CombateActual == null) return;
            float t = Time.time;
            if (t < proxRefuerzo) return;
            proxRefuerzo = t + 2f;
            var j = plugin.Jugador; var c = plugin.CompaneroActual;
            bool hayC = c != null && c.Visible && c.Escena == plugin.Escena;
            if (hayC)
            {
                Vector3 pc = c.PosicionMundo;
                if (VivosCerca(pc, RadioMaxActual) < MaxActual) Refuerzo(pc, j != null ? (Vector3?)j.transform.position : null);
            }
            Cuidar(t, j, hayC ? (Vector3?)c.PosicionMundo : null);
        }

        /// Enemigos vivos (no jefes, no aliados ni el perro/cabra) a menos de r de ese punto.
        int VivosCerca(Vector3 p, float r)
        {
            int n = 0; float r2 = r * r;
            foreach (var td in Object.FindObjectsOfType<TakeDamage>())
            {
                if (td == null || td.GetComponentInParent<FPSPlayer>() != null || Eventos.Muerto(td) || Dificultad.EsJefe(td) || Aliados.Protegido(td.transform)) continue;
                var rz = Combate.RaizDe(td); if (rz.name.StartsWith("coop_aliado_") || (rz.parent != null && rz.parent.name.StartsWith("coop_aliado_"))) continue;
                if ((rz.position - p).sqrMagnitude < r2) n++;
            }
            return n;
        }

        public void ForzarRefuerzo() { proxRefuerzo = 0f; }

        void Refuerzo(Vector3 centro, Vector3? otro)
        {
            refuerzosVivos.RemoveAll(x => x == null);
            float rMin = RadioMinActual, rMax = RadioMaxActual;
            if (refuerzosVivos.Count >= MaxActual) return;   // tope duro de los puestos por el mod
            NavMeshHit origen;
            bool hayOrigen = NavMesh.SamplePosition(centro, out origen, 10f, NavMesh.AllAreas);
            var camino = new NavMeshPath();
            for (int intento = 0; intento < 20; intento++)
            {
                float ang = Random.value * Mathf.PI * 2f, d = Random.Range(rMin, rMax);
                Vector3 cand = centro + new Vector3(Mathf.Cos(ang) * d, 0f, Mathf.Sin(ang) * d);
                cand.y = AlturaEn(cand);
                if (float.IsNaN(cand.y) || cand.y < nivelAgua + 0.8f) continue; if (EnFoso(cand)) continue;
                if (Vector3.Distance(cand, cabana) < RadioCabana) continue;   // la zona de la cabaña es segura
                NavMeshHit nh;
                if (!NavMesh.SamplePosition(cand, out nh, 8f, NavMesh.AllAreas)) continue;
                float real = Vector3.Distance(nh.position, centro);
                if (real < rMin - 5f || real > rMax + 5f) continue;
                if (otro.HasValue && Vector3.Distance(nh.position, otro.Value) < rMin) continue;   // tampoco encima del otro jugador
                if (hayOrigen && (!NavMesh.CalculatePath(origen.position, nh.position, NavMesh.AllAreas, camino) || camino.status != NavMeshPathStatus.PathComplete)) continue;
                string tipo = Guardianes[Random.Range(0, Guardianes.Length)];
                bool pot = Random.value < Potenciado.ProbabilidadActual;
                var td = plugin.CombateActual.Generar(tipo + (pot ? Potenciado.Marca : ""), nh.position);   // sin MarcaRefuerzo: esa marca los mandaba a por el jugador, se iban de la zona y se reponían sin fin
                if (td != null)
                {
                    Refuerzos++; refuerzosVivos.Add(td);
                    plugin.Log("Población: " + tipo + (pot ? " potenciado" : "") + " junto al compañero a " + Mathf.RoundToInt(real) + " m (puestos por el mod vivos " + refuerzosVivos.Count + ")");
                }
                return;
            }
        }

        /// Cada 2 s: quita los enemigos puestos por el mod que quedaron a más de 450 m de los dos jugadores.
        void Cuidar(float t, FPSPlayer j, Vector3? posC)
        {
            if (t < proxCuidado) return;
            proxCuidado = t + 2f;
            for (int i = refuerzosVivos.Count - 1; i >= 0; i--)
            {
                var td = refuerzosVivos[i];
                if (td == null) { refuerzosVivos.RemoveAt(i); continue; }
                Vector3 p = td.transform.position;
                float dj = j != null ? Vector3.Distance(p, j.transform.position) : float.MaxValue;
                float dc = posC.HasValue ? Vector3.Distance(p, posC.Value) : float.MaxValue;
                if (dj > LejosRefuerzo && dc > LejosRefuerzo)
                {
                    var raiz = td.transform.root.gameObject;
                    refuerzosVivos.RemoveAt(i);
                    Object.Destroy(raiz);
                    plugin.Log("Población: uno quedó a más de " + (int)LejosRefuerzo + " m de los dos y se quitó");
                    continue;
                }
            }
        }
    }
}

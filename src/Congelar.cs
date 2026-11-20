// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using System.Collections.Generic;
using HutongGames.PlayMaker;
using UnityEngine;
using UnityEngine.AI;

namespace WitchHuntCoop
{
    /// Rendimiento (pedido 2026-11-12): el juego tiene más de mil máquinas de PlayMaker activas y el hilo principal es el cuello de botella.
    /// Los monstruos que están a más de 170 m de los DOS jugadores no hacen nada útil: se les apagan sus máquinas, su animador y su agente de
    /// navegación, y se reactivan al bajar de 140 m. Nunca se tocan los jefes, el perro, la cabra negra, los aliados ni los que ya murieron.
    public partial class Mundo
    {
        const float RadioCongelar = 170f, RadioDescongelar = 140f;
        class Congelado { public Transform Raiz; public List<Behaviour> Apagados = new List<Behaviour>(); }
        readonly Dictionary<TakeDamage, Congelado> congelados = new Dictionary<TakeDamage, Congelado>();
        // Máquinas de objetos quietos del mundo (recogibles, plantas, pájaros de arbustos…): sin estado que se pierda si se reinician.
        static readonly HashSet<string> FsmCongelables = new HashSet<string> { "randomRotation", "Rain_destroy", "spawn", "random_anim_speed" };   // "Button", "Fsm Message Broker" y "MedkitMessage" (los recogibles y las plantas) ya NO se congelan: al reactivarlos podían no dejarse recoger (reporte 2026-11-13)
        readonly Dictionary<PlayMakerFSM, bool> fsmCongeladas = new Dictionary<PlayMakerFSM, bool>();
        // Máquinas dormidas SIN apagar el componente: Fsm.ManualUpdate = true hace que PlayMakerFSM.Update no la actualice cada cuadro,
        // pero no pasa por OnDisable/OnEnable (apagar y encender sacaba a los recogibles de su estado y dejaban de poder recogerse).
        // Los eventos siguen entrando (Fsm.Event se procesa al instante); solo se detienen las acciones de cada cuadro y los eventos con retardo.
        readonly HashSet<PlayMakerFSM> fsmDormidas = new HashSet<PlayMakerFSM>();
        public int FsmDormidasAhora { get { return fsmDormidas.Count; } }
        /// Nunca se duermen: lo que sea del jugador, de la interfaz, de monstruos (los maneja el congelado de arriba), aliados y el perro.
        // Las máquinas a las que el jugador avisa (FPSRigidBodyWalker.FSMs: caída "FallDamage" → piernas rotas, etc.) pueden vivir en otro
        // objeto lejos del jugador: si se dormían, su espera de ~15 s nunca terminaba y el efecto quedaba para siempre (pedido 2026-11-17).
        static PlayMakerFSM[] fsmDelJugador; static float proxFsmJugador;
        public static bool EsDelJugador(PlayMakerFSM f)
        {
            if (fsmDelJugador == null || Time.time > proxFsmJugador)
            {
                proxFsmJugador = Time.time + 5f;
                var w = Object.FindObjectOfType<FPSRigidBodyWalker>();
                fsmDelJugador = w != null && w.FSMs != null ? w.FSMs : new PlayMakerFSM[0];
            }
            foreach (var g in fsmDelJugador) if (g == f || (g != null && f != null && g.gameObject == f.gameObject)) return true;
            return false;
        }

        bool PuedeDormir(PlayMakerFSM f)
        {
            if (f.GetComponentInParent<FPSPlayer>() != null) return false;
            if (EsDelJugador(f)) return false;
            for (Transform t = f.transform; t != null; t = t.parent)
                if (t.name.StartsWith("Canvas_RFPS") || t.name.StartsWith("coop_") || t.name.StartsWith("WitchHuntCoop")) return false;
            if (f.GetComponentInParent<TakeDamage>() != null || f.GetComponentInChildren<TakeDamage>() != null) return false;
            if (f.GetComponentInParent<CharacterDamagePlaymakerAI>() != null) return false;
            if (Aliados.Protegido(f.transform)) return false;
            // Lo interactivo nunca se duerme (pedido 2026-11-17): una puerta cerrada quedó a medio abrir (el candado cayó
            // pero su Wait de 0,5 s no terminaba y la puerta no giraba). Puertas, cofres, rejas, cabañas, candados.
            if (f.FsmName == "Interact" || f.FsmName == "text") return false;
            for (Transform t = f.transform; t != null; t = t.parent)
            {
                string n = t.name.ToLowerInvariant();
                if (n.Contains("door") || n.Contains("chest") || n.Contains("gate") || n.Contains("lock") || n.Contains("cabin") || n.Contains("interactable")) return false;
            }
            // las que viajan de escena (botones/carteles con RunFSM) o cargan niveles se dejan vivas: el viaje conjunto depende de ellas
            if (f.FsmStates != null)
                foreach (var st in f.FsmStates)
                {
                    FsmStateAction[] acts = null;
                    try { acts = st.Actions; } catch { }
                    if (acts == null) continue;
                    foreach (var a in acts) if (a is HutongGames.PlayMaker.Actions.RunFSM || a is HutongGames.PlayMaker.Actions.LoadLevel) return false;
                }
            // solo objetos del mundo (con malla o colisión): las máquinas "globales" sin cuerpo (clima, hora, eventos) siguen vivas
            return f.GetComponentInChildren<Renderer>(true) != null || f.GetComponentInChildren<Collider>(true) != null;
        }
        float proxFsm;
        public int FsmCongeladasAhora { get { return fsmCongeladas.Count; } }
        float proxCongelar; public int ForzarCongelar = -1;   // -1 automático, 0 nunca, 1 siempre activo (orden "congelar")
        public int CongeladosAhora { get { return congelados.Count; } }

        void ActualizarFsmLejanas(Vector3 pj, bool hayC, Vector3 pc)
        {
            if (Time.time < proxFsm) return;
            proxFsm = Time.time + 2f;
            foreach (var f in Object.FindObjectsOfType<PlayMakerFSM>())
            {
                if (f == null || !f.enabled || f.Fsm == null || !f.Fsm.Active) continue;
                bool ok = FsmCongelables.Contains(f.FsmName) || (f.FsmName == "trigger" && f.gameObject.name.StartsWith("trigger_BushBirds"));
                if (f.GetComponentInParent<FPSPlayer>() != null || EsDelJugador(f)) continue;
                Vector3 p = f.transform.position;
                float d = Vector3.Distance(p, pj); if (hayC) d = Mathf.Min(d, Vector3.Distance(p, pc));
                if (d < RadioCongelar) continue;
                if (ok) { f.enabled = false; fsmCongeladas[f] = true; continue; }
                if (f.Fsm.ManualUpdate || fsmDormidas.Contains(f) || !PuedeDormir(f)) continue;
                f.Fsm.ManualUpdate = true; fsmDormidas.Add(f);
            }
            List<PlayMakerFSM> despertar = null;
            foreach (var kv in fsmCongeladas)
            {
                if (kv.Key == null) { (despertar ?? (despertar = new List<PlayMakerFSM>())).Add(kv.Key); continue; }
                Vector3 p = kv.Key.transform.position;
                float d = Vector3.Distance(p, pj); if (hayC) d = Mathf.Min(d, Vector3.Distance(p, pc));
                if (d < RadioDescongelar) (despertar ?? (despertar = new List<PlayMakerFSM>())).Add(kv.Key);
            }
            if (despertar != null) foreach (var f in despertar) { if (f != null) f.enabled = true; fsmCongeladas.Remove(f); }
            List<PlayMakerFSM> despiertas = null;
            foreach (var f in fsmDormidas)
            {
                if (f == null || f.Fsm == null) { (despiertas ?? (despiertas = new List<PlayMakerFSM>())).Add(f); continue; }
                Vector3 p = f.transform.position;
                float d = Vector3.Distance(p, pj); if (hayC) d = Mathf.Min(d, Vector3.Distance(p, pc));
                if (d < RadioDescongelar) (despiertas ?? (despiertas = new List<PlayMakerFSM>())).Add(f);
            }
            if (despiertas != null) foreach (var f in despiertas) { if (f != null && f.Fsm != null) f.Fsm.ManualUpdate = false; fsmDormidas.Remove(f); }
            // Red de seguridad: cualquier máquina dormida cerca de un jugador se despierta, esté o no en la lista.
            int sueltas = 0;
            foreach (var f in Object.FindObjectsOfType<PlayMakerFSM>())
            {
                if (f == null || f.Fsm == null || !f.Fsm.ManualUpdate) continue;
                Vector3 p = f.transform.position;
                float d = Vector3.Distance(p, pj); if (hayC) d = Mathf.Min(d, Vector3.Distance(p, pc));
                if (d >= RadioDescongelar) continue;
                f.Fsm.ManualUpdate = false; fsmDormidas.Remove(f); sueltas++;
            }
            if (sueltas > 0) plugin.Log("Congelar: " + sueltas + " máquinas dormidas cerca del jugador despertadas (fuera de la lista)");
        }

        void ActualizarCongelados()
        {
            if (Time.time < proxCongelar) return;
            proxCongelar = Time.time + 1f;
            var j = plugin.Jugador; var comp = plugin.CompaneroActual;
            if (j == null || plugin.Escena != EscenaBosque || ForzarCongelar == 0) { if (congelados.Count > 0 || fsmCongeladas.Count > 0 || fsmDormidas.Count > 0) DescongelarTodos(); return; }
            Vector3 pj = j.transform.position;
            bool hayC = comp != null && comp.Visible && comp.Escena == plugin.Escena; Vector3 pc = hayC ? comp.PosicionMundo : Vector3.zero;
            ActualizarFsmLejanas(pj, hayC, pc);
            foreach (var td in Object.FindObjectsOfType<TakeDamage>())
            {
                if (td == null || td.GetComponentInParent<FPSPlayer>() != null) continue;
                float d = Vector3.Distance(td.transform.position, pj);
                if (hayC) d = Mathf.Min(d, Vector3.Distance(td.transform.position, pc));
                Congelado c;
                bool esta = congelados.TryGetValue(td, out c);
                if (esta) { if (d < RadioDescongelar) Descongelar(td, c); continue; }
                if (d < RadioCongelar || !td.gameObject.activeInHierarchy) continue;
                if (Aliados.Protegido(td.transform) || Dificultad.EsJefe(td)) continue;
                try { if (Eventos.Muerto(td)) continue; } catch { continue; }
                var raiz = Combate.RaizDe(td); if (raiz == null) continue;
                var nuevo = new Congelado { Raiz = raiz };
                foreach (var f in raiz.GetComponentsInChildren<PlayMakerFSM>(false)) if (f.enabled) { f.enabled = false; nuevo.Apagados.Add(f); }
                foreach (var a in raiz.GetComponentsInChildren<Animator>(false)) if (a.enabled) { a.enabled = false; nuevo.Apagados.Add(a); }
                foreach (var n in raiz.GetComponentsInChildren<NavMeshAgent>(false)) if (n.enabled) { n.enabled = false; nuevo.Apagados.Add(n); }
                congelados[td] = nuevo;
            }
            // olvidar los que ya no existen
            List<TakeDamage> viejos = null;
            foreach (var kv in congelados) if (kv.Key == null) (viejos ?? (viejos = new List<TakeDamage>())).Add(kv.Key);
            if (viejos != null) foreach (var k in viejos) congelados.Remove(k);
        }

        void Descongelar(TakeDamage td, Congelado c)
        {
            foreach (var b in c.Apagados) if (b != null) b.enabled = true;
            congelados.Remove(td);
        }

        void DescongelarTodos()
        {
            foreach (var kv in new List<KeyValuePair<TakeDamage, Congelado>>(congelados)) { if (kv.Key == null) { congelados.Remove(kv.Key); continue; } Descongelar(kv.Key, kv.Value); }
            congelados.Clear();
            foreach (var kv in new List<KeyValuePair<PlayMakerFSM, bool>>(fsmCongeladas)) if (kv.Key != null) kv.Key.enabled = true;
            fsmCongeladas.Clear();
            foreach (var f in fsmDormidas) if (f != null && f.Fsm != null) f.Fsm.ManualUpdate = false;
            fsmDormidas.Clear();
        }
    }
}

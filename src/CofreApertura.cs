// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using System;
using System.Collections.Generic;
using UnityEngine;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;

namespace WitchHuntCoop
{
    /// Apertura de los cofres del mod con la MISMA animación que los del juego (pedido 2026-11-19).
    /// El juego no usa una animación grabada: la FSM del cofre (ChestParrent/Chest_locked) hace, al forzarlo,
    /// GetPosition("Expl") + SetIsKinematic(candado, false) + AddExplosionForce(candado) + DestroyObject(candado, retraso)
    /// + AudioPlay, espera (Wait) y luego, en el estado de abrir, AudioPlay + iTweenRotateAdd(tapa "chest_cap_save").
    /// Las copias del mod no tienen FSM (Piezas.Clonar quita los MonoBehaviour), así que se leen esos valores de un cofre
    /// original de la escena y se repiten igual sobre la copia (iTween del propio juego). Si no se encuentra ninguno,
    /// queda el giro a mano con los valores leídos del archivo del juego (85° en 0,7 s).
    public partial class Mundo
    {
        class SonidoJuego { public string Objeto; public AudioClip Clip; public float Volumen = 1f; }

        class AperturaJuego
        {
            public string Tapa; public Vector3 Giro; public float Tiempo = 0.7f, Retraso; public iTween.EaseType Ease = iTween.EaseType.linear;
            public iTween.LoopType Bucle = iTween.LoopType.none; public Space Espacio = Space.Self;
            public string Candado, Centro; public float Fuerza, Radio, Arriba, DestruirCandado = -1f; public ForceMode Modo;
            public float Espera;   // Wait entre soltar el candado y abrir la tapa
            public readonly List<SonidoJuego> SonidosCandado = new List<SonidoJuego>(), SonidosTapa = new List<SonidoJuego>();
        }
        AperturaJuego apertura;

        static string NombreDe(Fsm fsm, FsmOwnerDefault od)
        {
            try { var g = fsm.GetOwnerDefaultTarget(od); return g != null ? g.name : null; } catch { return null; }
        }

        /// Busca en los cofres originales la FSM que gira la tapa con iTween y copia sus valores.
        void LeerAperturaJuego(List<GameObject> originales)
        {
            apertura = null;
            foreach (var g in originales)
            {
                foreach (var f in g.GetComponentsInChildren<PlayMakerFSM>(true))
                {
                    if (f == null || f.Fsm == null || f.FsmStates == null) continue;
                    AperturaJuego a = null;
                    foreach (var st in f.FsmStates)
                    {
                        FsmStateAction[] acts = null;
                        try { acts = st.Actions; } catch { }
                        if (acts == null) continue;
                        foreach (var ac in acts)
                        {
                            var rot = ac as iTweenRotateAdd;
                            if (rot == null) continue;
                            a = new AperturaJuego
                            {
                                Tapa = NombreDe(f.Fsm, rot.gameObject),
                                Giro = rot.vector.Value,
                                Tiempo = rot.time.IsNone ? 1f : rot.time.Value,
                                Retraso = rot.delay.IsNone ? 0f : rot.delay.Value,
                                Ease = rot.easeType, Bucle = rot.loopType, Espacio = rot.space,
                            };
                            foreach (var ac2 in acts) AgregarSonido(f.Fsm, ac2, a.SonidosTapa);
                        }
                    }
                    if (a == null || a.Tapa == null) continue;
                    // estado del candado: el que lo lanza con una explosión
                    foreach (var st in f.FsmStates)
                    {
                        FsmStateAction[] acts = null;
                        try { acts = st.Actions; } catch { }
                        if (acts == null) continue;
                        AddExplosionForce ex = null;
                        foreach (var ac in acts) if (ac is AddExplosionForce) ex = (AddExplosionForce)ac;
                        if (ex == null) continue;
                        a.Candado = NombreDe(f.Fsm, ex.gameObject);
                        a.Fuerza = ex.force.Value; a.Radio = ex.radius.Value; a.Arriba = ex.upwardsModifier.IsNone ? 0f : ex.upwardsModifier.Value; a.Modo = ex.forceMode;
                        foreach (var ac in acts)
                        {
                            var gp = ac as GetPosition; if (gp != null) a.Centro = NombreDe(f.Fsm, gp.gameObject);
                            var de = ac as DestroyObject; if (de != null && !de.delay.IsNone) a.DestruirCandado = de.delay.Value;
                            var w = ac as Wait; if (w != null && !w.time.IsNone) a.Espera = w.time.Value;
                            AgregarSonido(f.Fsm, ac, a.SonidosCandado);
                        }
                        break;
                    }
                    apertura = a;
                    plugin.Log("Cofres: apertura del juego tomada de " + g.name + "/" + f.FsmName + ": tapa " + a.Tapa + " giro " + a.Giro + " en " + a.Tiempo + " s (" + a.Ease + ", " + a.Espacio + ", retraso " + a.Retraso + ")"
                        + " | candado " + (a.Candado ?? "-") + " fuerza " + a.Fuerza + " radio " + a.Radio + " arriba " + a.Arriba + " " + a.Modo + " centro " + (a.Centro ?? "-") + " se borra a los " + a.DestruirCandado + " s, espera " + a.Espera + " s"
                        + " | sonidos " + a.SonidosCandado.Count + "+" + a.SonidosTapa.Count);
                    return;
                }
            }
            plugin.Log("Cofres: no encontré la apertura del juego en los cofres originales; queda el giro a mano");
        }

        static void AgregarSonido(Fsm fsm, FsmStateAction ac, List<SonidoJuego> lista)
        {
            var ap = ac as AudioPlay;
            if (ap == null) return;
            var clip = ap.oneShotClip != null ? ap.oneShotClip.Value as AudioClip : null;
            lista.Add(new SonidoJuego { Objeto = NombreDe(fsm, ap.gameObject), Clip = clip, Volumen = ap.volume.IsNone ? 1f : ap.volume.Value });
        }

        static Transform Hijo(Transform raiz, string nombre)
        {
            if (string.IsNullOrEmpty(nombre)) return null;
            if (raiz.name == nombre) return raiz;
            foreach (var t in raiz.GetComponentsInChildren<Transform>(true)) if (t.name == nombre) return t;
            return null;
        }

        static void Sonar(Transform raiz, List<SonidoJuego> sonidos)
        {
            foreach (var s in sonidos)
            {
                var t = Hijo(raiz, s.Objeto) ?? raiz;
                var src = t.GetComponent<AudioSource>();
                if (src == null) { if (s.Clip != null) AudioSource.PlayClipAtPoint(s.Clip, t.position, s.Volumen); continue; }
                if (s.Clip != null) src.PlayOneShot(s.Clip, s.Volumen);
                else { src.volume = s.Volumen; src.Play(); }
            }
        }

        /// Abre la copia como el juego. Devuelve false si no hay datos del juego (se usa el giro a mano).
        bool AbrirComoJuego(Cofre c)
        {
            if (apertura == null || c.Go == null) return false;
            var tapa = Hijo(c.Go.transform, apertura.Tapa);
            if (tapa == null) return false;
            float espera = 0f;
            var candado = Hijo(c.Go.transform, apertura.Candado);
            if (candado != null)
            {
                var rb = candado.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = false;
                    var centro = Hijo(c.Go.transform, apertura.Centro);
                    rb.AddExplosionForce(apertura.Fuerza, centro != null ? centro.position : candado.position - c.Go.transform.forward * 0.2f, apertura.Radio, apertura.Arriba, apertura.Modo);
                }
                if (apertura.DestruirCandado >= 0f) UnityEngine.Object.Destroy(candado.gameObject, apertura.DestruirCandado);
                Sonar(c.Go.transform, apertura.SonidosCandado);
                espera = apertura.Espera;
            }
            var rbTapa = tapa.GetComponent<Rigidbody>();
            if (rbTapa != null) rbTapa.isKinematic = true;   // que la física no pelee con el giro
            iTween.RotateAdd(tapa.gameObject, iTween.Hash("amount", apertura.Giro, "time", apertura.Tiempo, "delay", espera + apertura.Retraso,
                "easetype", apertura.Ease, "looptype", apertura.Bucle, "space", apertura.Espacio));
            if (espera > 0f) plugin.StartCoroutine(SonarLuego(c.Go.transform, espera)); else Sonar(c.Go.transform, apertura.SonidosTapa);
            c.Angulo = GradosTapa;   // AnimarTapas no la toca
            return true;
        }

        System.Collections.IEnumerator SonarLuego(Transform raiz, float s)
        {
            yield return new WaitForSeconds(s);
            if (raiz != null && apertura != null) Sonar(raiz, apertura.SonidosTapa);
        }

        // ---------------- Orientación (pedido 2026-11-19) ----------------
        // El mod ponía los cofres con giro al azar y el aviso salía por cualquier lado: se veían mucho por detrás (bisagras).
        // Ahora la cerradura mira al lado más despejado y el aviso sale solo de frente.

        /// Centro del cuerpo (sin la tapa ni el candado), en el mundo.
        static Bounds CuerpoDe(GameObject go, Transform tapa, Transform candado)
        {
            Bounds b = new Bounds(go.transform.position, Vector3.zero); bool hay = false;
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (tapa != null && (r.transform == tapa || r.transform.IsChildOf(tapa))) continue;
                if (candado != null && (r.transform == candado || r.transform.IsChildOf(candado))) continue;
                if (!hay) { b = r.bounds; hay = true; } else b.Encapsulate(r.bounds);
            }
            return b;
        }

        /// Frente del cofre (lado de la cerradura) en coordenadas locales de la raíz.
        Vector3 CalcularFrenteLocal(GameObject go)
        {
            var candado = apertura != null ? Hijo(go.transform, apertura.Candado) : null;
            if (candado == null) foreach (var t in go.GetComponentsInChildren<Transform>(true)) if (t.name.StartsWith("lock")) { candado = t; break; }
            if (candado == null) return Vector3.forward;
            // (2026-11-19) con la POSICIÓN de la pieza, no con el centro de su malla: la malla del candado (pb_Mesh) tiene el
            // centro corrido a otro lado del cofre y el frente salía girado ~90° (el aviso aparecía por un costado).
            var cuerpoT = Hijo(go.transform, "chest");
            Vector3 d = candado.position - (cuerpoT != null ? cuerpoT.position : go.transform.position); d.y = 0f;
            if (d.sqrMagnitude < 1e-4f) return Vector3.forward;
            var l = go.transform.InverseTransformDirection(d.normalized); l.y = 0f;
            return l.normalized;
        }

        /// Gira el cofre sobre su eje para que la cerradura mire hacia donde hay más espacio libre (16 direcciones, hasta 8 m).
        void OrientarCofre(Cofre c)
        {
            if (c.Go == null) return;
            c.FrenteLocal = CalcularFrenteLocal(c.Go);
            var tapa = apertura != null ? Hijo(c.Go.transform, apertura.Tapa) : null;
            var cuerpo = CuerpoDe(c.Go, tapa, null);
            var propias = c.Go.GetComponentsInChildren<Collider>(false);
            var estaban = new bool[propias.Length];
            for (int i = 0; i < propias.Length; i++) { estaban[i] = propias[i].enabled; propias[i].enabled = false; }
            Vector3 mejorDir = Vector3.zero; float mejorLibre = -1f;
            try
            {
                Vector3 o = new Vector3(cuerpo.center.x, cuerpo.min.y + 0.6f, cuerpo.center.z);
                for (int k = 0; k < 16; k++)
                {
                    float a = k * 22.5f * Mathf.Deg2Rad;
                    var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                    RaycastHit h;
                    float libre = Physics.Raycast(o, dir, out h, 8f, ~0, QueryTriggerInteraction.Ignore) ? h.distance : 8f;
                    // también que se pueda estar de pie delante (sin pozo ni pared baja a 1,5 m)
                    if (libre > 1.5f && !Physics.Raycast(o + dir * 1.5f, Vector3.down, 2.5f, ~0, QueryTriggerInteraction.Ignore)) libre = 1.4f;
                    if (libre > mejorLibre + 0.01f) { mejorLibre = libre; mejorDir = dir; }
                }
            }
            finally { for (int i = 0; i < propias.Length; i++) propias[i].enabled = estaban[i]; }
            if (mejorDir == Vector3.zero) return;
            Vector3 frente = c.Go.transform.TransformDirection(c.FrenteLocal); frente.y = 0f; frente.Normalize();
            float giro = Mathf.Atan2(mejorDir.x, mejorDir.z) * Mathf.Rad2Deg - Mathf.Atan2(frente.x, frente.z) * Mathf.Rad2Deg;   // Unity 5.6 no tiene SignedAngle
            c.Go.transform.RotateAround(new Vector3(cuerpo.center.x, c.Go.transform.position.y, cuerpo.center.z), Vector3.up, giro);
        }

        /// ¿El jugador está del lado de la cerradura? (±75° del frente)
        bool DeFrente(Cofre c, Vector3 jugador)
        {
            Vector3 f = c.Go.transform.TransformDirection(c.FrenteLocal); f.y = 0f;
            Vector3 d = jugador - c.Go.transform.position; d.y = 0f;
            if (f.sqrMagnitude < 1e-4f || d.sqrMagnitude < 1e-4f) return true;
            return Vector3.Angle(f, d) <= 75f;
        }
    }
}

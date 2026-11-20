// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using System.Collections.Generic;
using UnityEngine;

namespace WitchHuntCoop
{
    /// Recarga propia de la ballesta (3 s): se sustituye el clip "Reload" del mosquete (baqueta, 7 s, el arma baja fuera de la vista) por uno
    /// donde el arma se queda a la vista y la mano izquierda, que sujeta el riel, suelta, agarra la cuerda floja, la jala hasta la nuez
    /// (junto a la culata) y vuelve al riel. La cuerda y los brazos los mueve Ballesta.Animar con el avance del clip.
    static class AnimBallesta
    {
        public const float Largo = 6.0f;               // duración del clip a velocidad 1 (s)
        static AnimationClip original; static int hecho = -1;
        public static Vector3 EulerBase = new Vector3(30f, 0f, 0f), PosBase = new Vector3(0f, -0.8f, 0f);   // inclinación del arma durante la recarga (ajustable con "animbal base x y z")
        public static Vector3 AgarreAjuste = Vector3.zero;   // ajuste (espacio del modelo) del punto donde la mano toma la cuerda
        public static float MaxDesplazamiento = 0.6f, Alcanza = 1f; public static float MaxGiroBrazo = 85f, Curva = 70f, GiroMano = 180f; public static int CurvaEje = 0;   // GiroMano: grados que se da vuelta la mano (palma hacia la cuerda)
        public static bool ManoActiva = true; public static float PunoManual = -1f;

        static string Ruta(Transform t, Transform raiz)
        {
            string r = t.name;
            while (t.parent != null && t.parent != raiz) { t = t.parent; r = t.name + "/" + r; }
            return r;
        }

        public static void Asegurar()
        {
            var arma = Ballesta.Copia.Arma; if (arma == null || Ballesta.Copia.Din == null || Ballesta.Copia.Din.Flecha == null) return;
            var wb = arma.GetComponent<WeaponBehavior>(); if (wb == null || wb.weaponMesh == null) return;
            var an = wb.weaponMesh.GetComponent<Animation>(); if (an == null) return;
            if (hecho == an.GetInstanceID()) return;
            Construir();
        }

        public static bool Construir()
        {
            var arma = Ballesta.Copia.Arma; if (arma == null) return false;
            var wb = arma.GetComponent<WeaponBehavior>(); var raiz = wb != null ? wb.weaponMesh : null;
            var an = raiz != null ? raiz.GetComponent<Animation>() : null; if (an == null) return false;
            var tBase = raiz.transform.Find("Base"); var tIzq = raiz.transform.Find("Base/L Arm"); var tDer = raiz.transform.Find("Base/R Arm");
            if (tBase == null || tIzq == null || tDer == null) { Ballesta.Registro("AnimBallesta: no encontré Base/L Arm/R Arm"); return false; }
            if (original == null) original = an["Reload"].clip;
            if (original == null) return false;
            // diagnóstico: pose del hueso Base en cada clip del arma (para que la recarga empiece y termine en la misma pose que el reposo)
            foreach (AnimationState st0 in an)
            {
                if (st0.clip == null || st0.name == "Reload" && st0.clip.name == "Reload" && st0.clip.length < 5.9f && original != null && st0.clip != original && st0.clip.legacy && st0.name == "Reload") continue;
                var cl = st0.clip; cl.SampleAnimation(raiz, 0f); var b0 = tBase.localPosition; var r0 = tBase.localEulerAngles; var i0 = tIzq.localEulerAngles; cl.SampleAnimation(raiz, Mathf.Max(0f, cl.length - 0.01f));
                Ballesta.Registro("AnimBallesta clip '" + st0.name + "' (" + cl.length.ToString("F2") + " s): Base inicio pos " + b0.ToString("F3") + " rot " + r0.ToString("F1") + " · fin pos " + tBase.localPosition.ToString("F3") + " rot " + tBase.localEulerAngles.ToString("F1") + " · L Arm inicio " + i0.ToString("F1"));
            }
            original.SampleAnimation(raiz, 0f);   // pose de partida = primer cuadro de la recarga del mosquete (las manos en su sitio)
            Quaternion rB = tBase.localRotation, rI = tIzq.localRotation, rD = tDer.localRotation; Vector3 pB = tBase.localPosition;
            var clip = new AnimationClip { legacy = true, name = "Reload", wrapMode = WrapMode.Once, frameRate = 30f };
            // parte A (0–3 s): el arma se agacha fuera de la vista (como la escopeta), se coloca la flecha y sube; parte B (3–6 s): el arma queda quieta y la mano tensa la cuerda
            float[] T = { 0f, 0.5f, 2.2f, 3.0f, 6.0f }; float[] inc = { 0f, 1f, 1f, 0f, 0f };
            var qB = new Quaternion[5]; var vB = new Vector3[5]; var qI = new Quaternion[5]; var qD = new Quaternion[5]; var vI = new Vector3[5]; var vD = new Vector3[5];
            Vector3 pI = tIzq.localPosition, pD = tDer.localPosition;
            for (int k = 0; k < 5; k++) { qB[k] = rB * Quaternion.Euler(EulerBase * inc[k]); vB[k] = pB + PosBase * inc[k]; qI[k] = rI; qD[k] = rD; vI[k] = pI; vD[k] = pD; }
            Pista(clip, Ruta(tBase, raiz.transform), T, qB, vB);
            Pista(clip, Ruta(tIzq, raiz.transform), T, qI, vI);   // también la posición: la mano desplaza el brazo para alcanzar la cuerda y debe volver
            Pista(clip, Ruta(tDer, raiz.transform), T, qD, vD);
            an.RemoveClip("Reload"); an.AddClip(clip, "Reload");
            hecho = an.GetInstanceID();
            var rc = raiz.GetComponent<RecargaBallesta>(); if (rc == null) rc = raiz.AddComponent<RecargaBallesta>();
            rc.Iniciar(an, tIzq, tIzq.Find("L Hand"));
            wb.reloadSnd = null;   // sin el sonido de la baqueta del mosquete
            Ballesta.Registro("AnimBallesta: clip de recarga propio (" + Largo + " s)");
            return true;
        }

        static void Pista(AnimationClip c, string ruta, float[] T, Quaternion[] q, Vector3[] v)
        {
            int n = T.Length;
            var cx = new Keyframe[n]; var cy = new Keyframe[n]; var cz = new Keyframe[n]; var cw = new Keyframe[n];
            for (int k = 0; k < n; k++) { cx[k] = new Keyframe(T[k], q[k].x); cy[k] = new Keyframe(T[k], q[k].y); cz[k] = new Keyframe(T[k], q[k].z); cw[k] = new Keyframe(T[k], q[k].w); }
            c.SetCurve(ruta, typeof(Transform), "localRotation.x", new AnimationCurve(cx)); c.SetCurve(ruta, typeof(Transform), "localRotation.y", new AnimationCurve(cy));
            c.SetCurve(ruta, typeof(Transform), "localRotation.z", new AnimationCurve(cz)); c.SetCurve(ruta, typeof(Transform), "localRotation.w", new AnimationCurve(cw));
            if (v == null) return;
            var px = new Keyframe[n]; var py = new Keyframe[n]; var pz = new Keyframe[n];
            for (int k = 0; k < n; k++) { px[k] = new Keyframe(T[k], v[k].x); py[k] = new Keyframe(T[k], v[k].y); pz[k] = new Keyframe(T[k], v[k].z); }
            c.SetCurve(ruta, typeof(Transform), "localPosition.x", new AnimationCurve(px)); c.SetCurve(ruta, typeof(Transform), "localPosition.y", new AnimationCurve(py)); c.SetCurve(ruta, typeof(Transform), "localPosition.z", new AnimationCurve(pz));
        }

        public static string Parametro(string arg)
        {
            var p = arg.Split(' ');
            float F(int i) { return i < p.Length ? float.Parse(p[i], System.Globalization.CultureInfo.InvariantCulture) : 0f; }
            switch (p[0])
            {
                case "base": EulerBase = new Vector3(F(1), F(2), F(3)); break;
                case "posbase": PosBase = new Vector3(F(1), F(2), F(3)); break;
                case "giro": GiroMano = F(1); break;
                case "alcance": MaxDesplazamiento = F(1); Alcanza = F(2); break;
                case "agarre": AgarreAjuste = new Vector3(F(1), F(2), F(3)); break;
                case "maxbrazo": MaxGiroBrazo = F(1); break;
                case "curva": Curva = F(1); CurvaEje = (int)F(2); break;
                case "mano": ManoActiva = F(1) > 0f; break;
                case "puno": PunoManual = F(1); if (p.Length > 2) CurvaEje = (int)F(2); return "puño manual " + PunoManual + " eje " + CurvaEje;
            }
            bool ok = Construir();
            return "base=" + EulerBase + " agarre=" + AgarreAjuste + " maxbrazo=" + MaxGiroBrazo + " curva=" + Curva + " eje=" + CurvaEje + " mano=" + ManoActiva + " construido=" + ok;
        }
    }

    /// Mueve la mano izquierda durante la recarga: va a la cuerda floja, la agarra, la jala hasta la nuez y vuelve al riel.
    public class RecargaBallesta : MonoBehaviour
    {
        Animation an; Transform brazo, mano, medio;
        readonly List<Transform> dedos = new List<Transform>(); readonly List<Quaternion> baseDedos = new List<Quaternion>();
        bool curvados;
        // (2026-11-20) sonidos de la recarga (antes era muda): SecuenciaRecarga.Ballesta, según el avance del clip "Reload"
        AudioSource fuente; bool recargaAntes; bool[] sonados;

        void SonidosRecarga()
        {
            var st = an["Reload"];
            bool ahora = st != null && an.IsPlaying("Reload");
            if (ahora && !recargaAntes) sonados = new bool[SecuenciaRecarga.Ballesta.Length];
            recargaAntes = ahora;
            if (!ahora || sonados == null) return;
            if (fuente == null) { fuente = gameObject.AddComponent<AudioSource>(); fuente.spatialBlend = 0f; fuente.playOnAwake = false; fuente.priority = 32; }
            float t = st.time;
            for (int i = 0; i < sonados.Length; i++)
            {
                var paso = SecuenciaRecarga.Ballesta[i];
                if (sonados[i] || t < paso.T) continue;
                sonados[i] = true;
                var c = paso.Clip(); if (c != null) fuente.PlayOneShot(c, Mathf.Clamp01(paso.Vol * Opciones.FactorVolumen));
                Ballesta.Registro("Recarga de la ballesta: paso " + (i + 1) + "/" + sonados.Length + " " + (c != null ? c.name : "SIN CLIP") + " a " + t.ToString("0.00") + " s");
            }
        }

        public void Iniciar(Animation a, Transform brazoIzq, Transform manoIzq)
        {
            an = a; brazo = brazoIzq; mano = manoIzq; dedos.Clear(); medio = null;
            if (mano != null)
                foreach (var t in mano.GetComponentsInChildren<Transform>(true))
                {
                    if (t == mano || !t.name.StartsWith("L Finger") && !t.name.StartsWith("L Thumb")) continue;
                    dedos.Add(t); if (t.name == "L Finger 2 1") medio = t;
                }
        }

        // instantes (s del clip) y pesos: la mano va a la cuerda (0,2–0,6 s), la jala (0,65–2,0 s), suelta y vuelve al riel (2,1–2,7 s)
        static readonly float[] KT = { 0f, 0.6f, 0.9f, 1.95f, 2.1f, 2.8f, 3.0f };
        static readonly float[] KW = { 0f, 1f, 1f, 1f, 1f, 0f, 0f };      // peso del brazo (sigue la cuerda): va en medio de los brazos, la jala hasta la flecha y vuelve al riel
        static readonly float[] KC = { 0f, 0f, 1f, 1f, 0f, 0f, 0f };      // cuánto se cierran los dedos (puño sobre la cuerda)
        /// Pose «garra» (la 09 de la hoja): dedos casi rectos que solo doblan las puntas. Factor por articulación (0 = base del dedo, 1, 2 = punta).
        static float Garra(string nombre)
        {
            if (nombre.StartsWith("L Thumb")) return 0.15f;
            var q = nombre.Split(' '); int j = 0; if (q.Length >= 4) int.TryParse(q[3], out j);
            return j == 0 ? 0.0f : (j == 1 ? 0.35f : 1.0f);
        }
        static float Valor(float t, float[] k, float[] v)
        {
            if (t <= k[0]) return v[0];
            for (int i = 1; i < k.Length; i++) if (t <= k[i]) return Mathf.Lerp(v[i - 1], v[i], Mathf.SmoothStep(0f, 1f, (t - k[i - 1]) / (k[i] - k[i - 1])));
            return v[v.Length - 1];
        }

        void LateUpdate()
        {
            if (an == null) return;
            SonidosRecarga();
            if (AnimBallesta.PunoManual >= 0f)
            {
                if (!curvados) { baseDedos.Clear(); foreach (var f in dedos) baseDedos.Add(f.localRotation); curvados = true; }
                var ej = AnimBallesta.CurvaEje == 0 ? Vector3.right : (AnimBallesta.CurvaEje == 1 ? Vector3.up : Vector3.forward);
                for (int i = 0; i < dedos.Count; i++) if (dedos[i] != null) dedos[i].localRotation = baseDedos[i] * Quaternion.AngleAxis(AnimBallesta.Curva * AnimBallesta.PunoManual * Garra(dedos[i].name), ej);
                return;
            }
            var st = an["Reload"];
            if (st == null || !an.IsPlaying("Reload"))
            {
                if (curvados) { for (int i = 0; i < dedos.Count; i++) if (dedos[i] != null) dedos[i].localRotation = baseDedos[i]; curvados = false; }
                return;
            }
            float t = st.time; if (st.speed > 0f) t = Mathf.Min(t, AnimBallesta.Largo);
            t -= 3.0f;   // la mano solo actúa en la parte B (3–6 s)
            float w = AnimBallesta.ManoActiva && t > 0f ? Valor(t, KT, KW) : 0f, cur = t > 0f ? Valor(t, KT, KC) : 0f;
            var c = Ballesta.Copia; var d = c.Din;
            if (w > 0.001f && brazo != null && mano != null && c.Modelo != null && d != null)
            {
                Vector3 objetivo = c.Modelo.transform.TransformPoint(Ballesta.PuntoCuerda + AnimBallesta.AgarreAjuste);
                Vector3 actual = (medio != null ? medio.position : mano.position) - brazo.position, deseado = objetivo - brazo.position;
                if (actual.sqrMagnitude > 1e-6f && deseado.sqrMagnitude > 1e-6f)
                {
                    var q = Quaternion.FromToRotation(actual, deseado);
                    float ang; Vector3 eje; q.ToAngleAxis(out ang, out eje); if (ang > 180f) ang -= 360f;
                    ang = Mathf.Clamp(ang, -AnimBallesta.MaxGiroBrazo, AnimBallesta.MaxGiroBrazo);
                    brazo.rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.AngleAxis(ang, eje), w) * brazo.rotation;
                }
                // el brazo no se estira: lo que falta para llegar a la cuerda se corrige moviendo todo el brazo hacia ella
                Vector3 falta = objetivo - (medio != null ? medio.position : mano.position);
                brazo.position += Vector3.ClampMagnitude(falta, AnimBallesta.MaxDesplazamiento) * (w * AnimBallesta.Alcanza);
                // se da vuelta la mano: palma hacia la cuerda (giro sobre el eje del antebrazo)
                Vector3 ejeB = (mano.position - brazo.position); if (ejeB.sqrMagnitude > 1e-8f) brazo.rotation = Quaternion.AngleAxis(AnimBallesta.GiroMano * w, ejeB.normalized) * brazo.rotation;
            }
            if (!curvados) { baseDedos.Clear(); foreach (var f in dedos) baseDedos.Add(f.localRotation); curvados = true; }
            var ejeD = AnimBallesta.CurvaEje == 0 ? Vector3.right : (AnimBallesta.CurvaEje == 1 ? Vector3.up : Vector3.forward);
            for (int i = 0; i < dedos.Count; i++) if (dedos[i] != null) dedos[i].localRotation = baseDedos[i] * Quaternion.AngleAxis(AnimBallesta.Curva * cur * Garra(dedos[i].name), ejeD);
        }
    }
}

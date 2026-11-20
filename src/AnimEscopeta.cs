// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using System.Collections.Generic;
using UnityEngine;

namespace WitchHuntCoop
{
    /// Recarga de la escopeta de quiebre: se sustituye el clip "Reload" del mosquete (baqueta, 9 s) por uno propio.
    /// El arma se inclina, los cañones se abren por la bisagra (entre la culata y los cañones), se mantienen
    /// abiertos mientras se "meten" las dos balas (no se ven) y se cierran de golpe. Los números se ajustan con
    /// la orden de prueba "animpar".
    static class AnimEscopeta
    {
        public const float Largo = 3.0f;                    // duración del clip a velocidad 1 (s)
        public static float Apertura = -45f;                // grados que abren los cañones
        public static Vector3 EulerBase = new Vector3(30f, 0f, 0f);   // inclinación de todo el arma
        public static Vector3 PosBase = new Vector3(0f, -0.8f, 0f);
        public static Vector3 EulerIzq = Vector3.zero, EulerDer = Vector3.zero;   // (la mano izquierda va sola a los cañones: RecargaEscopeta)   // ajuste de brazos al abrir

        static AnimationClip original;
        static int hecho = -1;                              // Animation para la que ya está construido

        // línea de tiempo (s a velocidad 1): inclina el arma, abre con un pequeño rebote, espera con los cañones
        // abiertos (se meten las balas), cierra de golpe con rebote y se asienta
        internal static readonly float[] T = { 0f, 0.3f, 0.55f, 0.95f, 1.1f, 1.9f, 2.05f, 2.12f, 2.4f, 3.0f };
        static readonly float[] Inclina = { 0f, 0.5f, 1f, 1f, 1f, 1f, 1f, 1f, 0.3f, 0f };
        // el arma baja fuera de la vista del jugador mientras se recarga (pedido 2026-11-10); la baja y sube suavemente
        static readonly float[] Baja = { 0f, 0.6f, 1f, 1f, 1f, 1f, 1f, 1f, 0.35f, 0f };
        internal static readonly float[] Abre = { 0f, 0f, 0.15f, 1.08f, 1f, 1f, 0f, -0.06f, 0f, 0f };
        public static bool ManoActiva = false;
        public static float MaxGiroBrazo = 85f;                // máximo giro (°) del brazo izquierdo hacia el agarre
        public static float Curva = 60f; public static int CurvaEje = 2;   // cierre de los dedos de la mano izquierda (grados, eje local 0/1/2)
        public static Vector3 AgarreAjuste = Vector3.zero;   // ajuste (espacio del pivote) del punto donde la mano izquierda sujeta los cañones

        /// Peso 0–1 de "cañones abiertos" en el instante t del clip (para llevar la mano izquierda).
        internal static float PesoAbre(float t)
        {
            int n = T.Length;
            if (t <= T[0]) return 0f;
            for (int k = 1; k < n; k++)
                if (t <= T[k]) { float u = Mathf.SmoothStep(0f, 1f, (t - T[k - 1]) / (T[k] - T[k - 1])); return Mathf.Clamp01(Mathf.Lerp(Abre[k - 1], Abre[k], u)); }
            return 0f;
        }

        static string Ruta(Transform t, Transform raiz)
        {
            string r = t.name;
            while (t.parent != null && t.parent != raiz) { t = t.parent; r = t.name + "/" + r; }
            return r;
        }

        public static void Asegurar()
        {
            var arma = Escopeta.Copia.Arma; if (arma == null || Escopeta.Copia.Pivote == null) return;
            var wb = arma.GetComponent<WeaponBehavior>(); if (wb == null || wb.weaponMesh == null) return;
            var an = wb.weaponMesh.GetComponent<Animation>(); if (an == null) return;
            if (hecho == an.GetInstanceID() && Escopeta.Copia.Pivote != null) return;
            Construir();
        }

        public static bool Construir()
        {
            var arma = Escopeta.Copia.Arma; var pivote = Escopeta.Copia.Pivote;
            if (arma == null || pivote == null) return false;
            var wb = arma.GetComponent<WeaponBehavior>(); var raiz = wb != null ? wb.weaponMesh : null;
            var an = raiz != null ? raiz.GetComponent<Animation>() : null;
            if (an == null) return false;
            var tBase = raiz.transform.Find("Base"); var tIzq = raiz.transform.Find("Base/L Arm"); var tDer = raiz.transform.Find("Base/R Arm");
            if (tBase == null || tIzq == null || tDer == null) { Escopeta.plugin.Log("AnimEscopeta: no encontré Base/L Arm/R Arm"); return false; }
            if (original == null) original = an["Reload"].clip;
            if (original == null) return false;
            // pose de partida = primer cuadro de la recarga del mosquete
            original.SampleAnimation(raiz, 0f);
            Quaternion rB = tBase.localRotation, rI = tIzq.localRotation, rD = tDer.localRotation;
            Vector3 pB = tBase.localPosition;
            var clip = new AnimationClip { legacy = true, name = "Reload", wrapMode = WrapMode.Once, frameRate = 30f };
            int n = T.Length;
            var qB = new Quaternion[n]; var vB = new Vector3[n]; var qI = new Quaternion[n]; var qD = new Quaternion[n]; var qP = new Quaternion[n];
            for (int k = 0; k < n; k++)
            {
                qB[k] = rB * Quaternion.Euler(EulerBase * Inclina[k]); vB[k] = pB + PosBase * Baja[k];
                qI[k] = rI * Quaternion.Euler(EulerIzq * Abre[k]); qD[k] = rD * Quaternion.Euler(EulerDer * Abre[k]);
                qP[k] = Quaternion.AngleAxis(Apertura * Abre[k], Escopeta.Copia.EjeBisagra);
            }
            Pista(clip, Ruta(tBase, raiz.transform), qB, vB);
            Pista(clip, Ruta(tIzq, raiz.transform), qI, null);
            Pista(clip, Ruta(tDer, raiz.transform), qD, null);
            Pista(clip, Ruta(pivote, raiz.transform), qP, null);
            an.RemoveClip("Reload");
            an.AddClip(clip, "Reload");
            hecho = an.GetInstanceID();
            var rc = raiz.GetComponent<RecargaEscopeta>(); if (rc == null) rc = raiz.AddComponent<RecargaEscopeta>();
            rc.Iniciar(an, tIzq, tIzq.Find("L Hand"), pivote);
            if (wb != null) wb.reloadSnd = null;   // sin el sonido de la recarga del rifle: los sonidos son los de RecargaEscopeta
            Escopeta.plugin.Log("AnimEscopeta: clip de recarga propio (" + Largo + " s) en " + Ruta(pivote, raiz.transform));
            return true;
        }

        static void Pista(AnimationClip c, string ruta, Quaternion[] q, Vector3[] v)
        {
            int n = T.Length;
            var cx = new Keyframe[n]; var cy = new Keyframe[n]; var cz = new Keyframe[n]; var cw = new Keyframe[n];
            for (int k = 0; k < n; k++) { cx[k] = new Keyframe(T[k], q[k].x); cy[k] = new Keyframe(T[k], q[k].y); cz[k] = new Keyframe(T[k], q[k].z); cw[k] = new Keyframe(T[k], q[k].w); }
            c.SetCurve(ruta, typeof(Transform), "localRotation.x", new AnimationCurve(cx));
            c.SetCurve(ruta, typeof(Transform), "localRotation.y", new AnimationCurve(cy));
            c.SetCurve(ruta, typeof(Transform), "localRotation.z", new AnimationCurve(cz));
            c.SetCurve(ruta, typeof(Transform), "localRotation.w", new AnimationCurve(cw));
            if (v == null) return;
            var px = new Keyframe[n]; var py = new Keyframe[n]; var pz = new Keyframe[n];
            for (int k = 0; k < n; k++) { px[k] = new Keyframe(T[k], v[k].x); py[k] = new Keyframe(T[k], v[k].y); pz[k] = new Keyframe(T[k], v[k].z); }
            c.SetCurve(ruta, typeof(Transform), "localPosition.x", new AnimationCurve(px));
            c.SetCurve(ruta, typeof(Transform), "localPosition.y", new AnimationCurve(py));
            c.SetCurve(ruta, typeof(Transform), "localPosition.z", new AnimationCurve(pz));
        }

        /// Orden de prueba: animpar apertura|base|izq|der|posbase|pivote valores…  (y se rehace el clip)
        public static string Parametro(string arg)
        {
            var p = arg.Split(' ');
            float F(int i) { return i < p.Length ? float.Parse(p[i], System.Globalization.CultureInfo.InvariantCulture) : 0f; }
            switch (p[0])
            {
                case "apertura": Apertura = F(1); break;
                case "base": EulerBase = new Vector3(F(1), F(2), F(3)); break;
                case "izq": EulerIzq = new Vector3(F(1), F(2), F(3)); break;
                case "der": EulerDer = new Vector3(F(1), F(2), F(3)); break;
                case "posbase": PosBase = new Vector3(F(1), F(2), F(3)); break;
                case "maxbrazo": MaxGiroBrazo = F(1); break;
                case "curva": Curva = F(1); CurvaEje = (int)F(2); break;
                case "agarre": AgarreAjuste = new Vector3(F(1), F(2), F(3)); break;
                case "pivote":
                {
                    var c = Escopeta.Copia; if (c.Pivote == null || c.MallaCanones == null) return "sin pivote";
                    var d = new Vector3(F(1), F(2), F(3));
                    c.Pivote.localPosition += d;
                    var vs = c.MallaCanones.vertices; for (int i = 0; i < vs.Length; i++) vs[i] -= d;
                    c.MallaCanones.vertices = vs; c.MallaCanones.RecalculateBounds();
                    break;
                }
            }
            bool ok = Construir();
            return "apertura=" + Apertura + " base=" + EulerBase + " izq=" + EulerIzq + " der=" + EulerDer + " posbase=" + PosBase + " pivote=" + (Escopeta.Copia.Pivote != null ? Escopeta.Copia.Pivote.localPosition.ToString("F3") : "-") + " clip=" + ok;
        }
    }
}

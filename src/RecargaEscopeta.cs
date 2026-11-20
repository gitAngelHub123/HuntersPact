// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using UnityEngine;

namespace WitchHuntCoop
{
    /// Complementos de la recarga de la escopeta de quiebre, sincronizados con el clip "Reload" propio:
    /// la mano izquierda sigue a los cañones mientras se abren, sonidos propios (sintetizados) y dos balas de
    /// plata que salen al abrirla.
    public class RecargaEscopeta : MonoBehaviour
    {
        Animation an; Transform brazo, mano, pivote, medio;
        readonly System.Collections.Generic.List<Transform> dedos = new System.Collections.Generic.List<Transform>();
        readonly System.Collections.Generic.List<Quaternion> baseDedos = new System.Collections.Generic.List<Quaternion>();
        bool curvados;
        float tPrev = -1f;
        bool[] hecho = new bool[6];
        AudioSource fuente;

        // instantes (s del clip) de los eventos: cierre de seguro, balas que salen, meter balas, cierre
        static readonly float[] Tiempos = { 0.55f, 0.95f, 1.0f, 1.35f, 1.65f, 2.05f };

        public void Iniciar(Animation a, Transform brazoIzq, Transform manoIzq, Transform pivoteCanones)
        {
            an = a; brazo = brazoIzq; mano = manoIzq; pivote = pivoteCanones;
            dedos.Clear(); medio = null;
            if (mano != null)
                foreach (var t in mano.GetComponentsInChildren<Transform>(true))
                {
                    if (t == mano || !t.name.StartsWith("L Finger") && !t.name.StartsWith("L Thumb")) continue;
                    dedos.Add(t);
                    if (t.name == "L Finger 2 1") medio = t;
                }
            Sonidos.Preparar("esc_abrir"); Sonidos.Preparar("esc_meter1"); Sonidos.Preparar("esc_meter2"); Sonidos.Preparar("esc_cerrar");
            if (fuente == null) { fuente = gameObject.AddComponent<AudioSource>(); fuente.spatialBlend = 0f; fuente.playOnAwake = false; }
        }

        // Banco de pruebas de la mano (orden "mano"): giros sueltos por hueso, para estudiar qué eje hace qué.
        public static readonly System.Collections.Generic.Dictionary<string, Vector3> PruebaHuesos = new System.Collections.Generic.Dictionary<string, Vector3>();
        readonly System.Collections.Generic.Dictionary<string, Quaternion> baseHuesos = new System.Collections.Generic.Dictionary<string, Quaternion>();
        public string ListaHuesos()
        {
            var sb = new System.Text.StringBuilder();
            if (mano != null) foreach (var t in mano.GetComponentsInChildren<Transform>(true)) sb.Append(t.name).Append(" | ");
            return sb.ToString();
        }
        void AplicarPrueba()
        {
            if (mano == null) return;
            foreach (var t in mano.GetComponentsInChildren<Transform>(true))
            {
                Vector3 e;
                if (!baseHuesos.ContainsKey(t.name)) baseHuesos[t.name] = t.localRotation;
                if (PruebaHuesos.TryGetValue(t.name, out e)) t.localRotation = baseHuesos[t.name] * Quaternion.Euler(e);
                else if (PruebaHuesos.Count > 0 && t != mano) t.localRotation = baseHuesos[t.name];
            }
        }
        public void ReiniciarPrueba()
        {
            PruebaHuesos.Clear();
            if (mano == null) return;
            foreach (var t in mano.GetComponentsInChildren<Transform>(true)) { Quaternion q; if (baseHuesos.TryGetValue(t.name, out q)) t.localRotation = q; }
        }

        void LateUpdate()
        {
            if (PruebaHuesos.Count > 0) AplicarPrueba();
            if (an == null || pivote == null) return;
            var st = an["Reload"];
            if (st == null || !an.IsPlaying("Reload"))
            {
                tPrev = -1f; for (int i = 0; i < hecho.Length; i++) hecho[i] = false;
                if (curvados) { for (int i = 0; i < dedos.Count; i++) if (dedos[i] != null) dedos[i].localRotation = baseDedos[i]; curvados = false; }   // dedos como estaban
                return;
            }
            float t = st.time;
            if (t < tPrev) for (int i = 0; i < hecho.Length; i++) hecho[i] = false;   // recarga nueva
            tPrev = t;
            // mano izquierda (coreografía): agarra los cañones por arriba, los tira hacia abajo, va a la recámara y mete
            // las dos balas, pasa por debajo de los cañones y los sube para cerrar. El brazo apunta con la mitad de los dedos.
            float w, ta, tb, tc;
            Llave(t, out w, out ta, out tb, out tc);
            if (!AnimEscopeta.ManoActiva) w = 0f;   // sin coreografía de la mano: el arma baja fuera de la vista
            var malla = Escopeta.Copia.MallaCanones;
            if (w > 0.001f && brazo != null && mano != null && malla != null)
            {
                var c = Escopeta.Copia; var ext = malla.bounds.extents;
                float eAlto = Vector3.Dot(new Vector3(Mathf.Abs(c.EjeAltoHueso.x), Mathf.Abs(c.EjeAltoHueso.y), Mathf.Abs(c.EjeAltoHueso.z)), ext);
                float eLargo = Vector3.Dot(new Vector3(Mathf.Abs(c.EjeLargoHueso.x), Mathf.Abs(c.EjeLargoHueso.y), Mathf.Abs(c.EjeLargoHueso.z)), ext);
                // referencia (video de una escopeta de quiebre): la mano no viaja, se queda junto a la recámara
                Vector3 centro = malla.bounds.center + AnimEscopeta.AgarreAjuste;
                Vector3 arriba = c.EjeAltoHueso, largo = c.EjeLargoHueso;
                Vector3 trasera = centro - largo * (eLargo * 0.75f);                 // cara trasera de los cañones (recámara)
                Vector3 pTope = trasera + arriba * (eAlto * 0.45f) + largo * (eLargo * 0.15f);
                Vector3 pCam = trasera - arriba * (eAlto * 0.25f);   // más abajo (pedido 2026-11-10)
                Vector3 pBajo = trasera - arriba * (eAlto * 1.2f) + largo * (eLargo * 0.1f);
                Vector3 objetivo = pivote.TransformPoint(pTope * ta + pCam * tb + pBajo * tc);
                Vector3 actual = (medio != null ? medio.position : mano.position) - brazo.position, deseado = objetivo - brazo.position;
                if (actual.sqrMagnitude > 1e-6f && deseado.sqrMagnitude > 1e-6f)
                {
                    var d = Quaternion.FromToRotation(actual, deseado);
                    float ang; Vector3 eje; d.ToAngleAxis(out ang, out eje);
                    if (ang > 180f) ang -= 360f;
                    ang = Mathf.Clamp(ang, -AnimEscopeta.MaxGiroBrazo, AnimEscopeta.MaxGiroBrazo);   // el brazo no cruza la pantalla
                    brazo.rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.AngleAxis(ang, eje), w) * brazo.rotation;
                }
            }
            // (pose de partida de los dedos: se guarda al empezar la recarga)
            if (!curvados) { baseDedos.Clear(); foreach (var d in dedos) baseDedos.Add(d.localRotation); curvados = true; }
            for (int i = 0; i < dedos.Count; i++)
            {
                var d = dedos[i]; if (d == null) continue;
                // cada dedo con su gesto: agarre (arriba), pinza al meter las balas, agarre por debajo; y cada articulación su parte
                int f = 0, j = 0;   // f: 0 pulgar, 1 índice … 4 meñique; j: articulación
                if (!d.name.StartsWith("L Thumb")) { var p = d.name.Split(' '); if (p.Length >= 4) { int.TryParse(p[2], out f); int.TryParse(p[3], out j); } }
                else { f = 0; j = d.name.EndsWith("2") ? 2 : (d.name.EndsWith("1") ? 1 : 0); }
                f = Mathf.Clamp(f, 0, 4);
                float cur = w * (ta * FTop[f] + tb * FCam[f] + tc * FBajo[f]) * (j == 0 ? 0.7f : (j == 1 ? 1f : 0.85f));
                var eje = AnimEscopeta.CurvaEje == 0 ? Vector3.right : (AnimEscopeta.CurvaEje == 1 ? Vector3.up : Vector3.forward);
                d.localRotation = baseDedos[i] * Quaternion.AngleAxis(AnimEscopeta.Curva * cur, eje);
            }
            for (int i = 0; i < Tiempos.Length; i++)
            {
                if (hecho[i] || t < Tiempos[i]) continue;
                hecho[i] = true;
                Evento(i);
            }
        }

        // cuánto se cierra cada dedo (pulgar, índice, medio, anular, meñique) en cada fase
        static readonly float[] FTop = { 0.9f, 0.8f, 0.6f, 0.5f, 0.45f };
        static readonly float[] FCam = { 0.9f, 0.8f, 0.6f, 0.5f, 0.45f };    // pinza con pulgar e índice al meter las balas
        static readonly float[] FBajo = { 0.9f, 0.8f, 0.6f, 0.5f, 0.45f };

        // llaves de la mano: tiempo (s del clip), peso, y mezcla de posiciones (arriba de los cañones / recámara / debajo)
        // Versión simple (pedido 2026-11-10): con los cañones ya abiertos la mano va a la recámara, mete dos balas
        // (invisibles) y vuelve a su sitio. Los cañones abren y cierran solos con el clip.
        static readonly float[] LT = { 0f, 0.85f, 1.05f, 1.35f, 1.47f, 1.60f, 1.72f, 1.85f, 2.05f };
        static readonly float[] LW = { 0f, 0f,    1f,    1f,    1f,    1f,    1f,    1f,    0f };
        static readonly float[] LA = { 0f, 0f,    0f,    0f,    0.5f,  0f,    0.5f,  0.5f,  0.5f };   // 1 = por encima de la recámara
        static readonly float[] LB = { 1f, 1f,    1f,    1f,    0.5f,  1f,    0.5f,  0.5f,  0.5f };   // 1 = sobre la recámara
        static readonly float[] LC = { 0f, 0f,    0f,    0f,    0f,    0f,    0f,    0f,    0f };

        static void Llave(float t, out float w, out float a, out float b, out float c)
        {
            int n = LT.Length;
            if (t <= LT[0]) { w = LW[0]; a = LA[0]; b = LB[0]; c = LC[0]; return; }
            for (int k = 1; k < n; k++)
                if (t <= LT[k])
                {
                    float u = Mathf.SmoothStep(0f, 1f, (t - LT[k - 1]) / (LT[k] - LT[k - 1]));
                    w = Mathf.Lerp(LW[k - 1], LW[k], u); a = Mathf.Lerp(LA[k - 1], LA[k], u); b = Mathf.Lerp(LB[k - 1], LB[k], u); c = Mathf.Lerp(LC[k - 1], LC[k], u);
                    return;
                }
            w = LW[n - 1]; a = LA[n - 1]; b = LB[n - 1]; c = LC[n - 1];
        }

        void Evento(int i)
        {
            float vol = Opciones.FactorVolumen;
            switch (i)
            {
                case 0: Sonar(Sonidos.Clip("esc_abrir") ?? Sonidos2.Seguro, 1f * vol); break;
                case 1: Sonar(Sonidos2.Tintineo, 0.8f * vol); Expulsar(-1f); break;
                case 2: Sonar(Sonidos2.Tintineo, 0.6f * vol); Expulsar(1f); break;
                case 3: Sonar(Sonidos.Clip("esc_meter1") ?? Sonidos2.Meter, 1f * vol); break;
                case 4: Sonar(Sonidos.Clip("esc_meter2") ?? Sonidos2.Meter, 1f * vol); break;
                case 5: Sonar(Sonidos.Clip("esc_cerrar") ?? Sonidos2.Cierre, 1f * vol); break;
            }
        }

        void Sonar(AudioClip c, float v) { if (fuente != null && c != null) fuente.PlayOneShot(c, Mathf.Clamp01(v)); }

        /// Una bala de plata sale hacia arriba desde la recámara y cae.
        void Expulsar(float lado)
        {
            var cam = Camera.main; if (cam == null || pivote == null) return;
            var g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            g.name = "coop_bala_plata";
            g.transform.localScale = Vector3.one * 0.05f;
            g.transform.position = pivote.position + cam.transform.up * 0.04f;
            var r = g.GetComponent<Renderer>();
            if (balaMat == null)
            {
                balaMat = new Material(Shader.Find("Standard")) { color = new Color(0.92f, 0.94f, 0.98f) };
                balaMat.SetFloat("_Metallic", 0.35f); balaMat.SetFloat("_Glossiness", 0.8f);
                balaMat.EnableKeyword("_EMISSION"); balaMat.SetColor("_EmissionColor", new Color(0.45f, 0.46f, 0.5f));   // se ve aunque el bosque esté oscuro
            }
            r.sharedMaterial = balaMat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var col = g.GetComponent<Collider>();
            var rb = g.AddComponent<Rigidbody>(); rb.mass = 0.02f;
            foreach (var c in cam.transform.root.GetComponentsInChildren<Collider>()) if (col != null && c != col) Physics.IgnoreCollision(col, c);
            rb.velocity = cam.transform.up * 2.6f + cam.transform.right * (0.5f * lado) - cam.transform.forward * 0.2f;
            rb.angularVelocity = Random.insideUnitSphere * 10f;
            Destroy(g, 120f);   // cada bala desaparece a los 2 minutos de salir
            Escopeta.plugin.Log("Recarga: bala de plata sale en " + g.transform.position.ToString("F2"));
        }
        static Material balaMat;
    }

    /// Sonidos de la recarga, sintetizados (sin grabaciones de nadie).
    static class Sonidos2
    {
        const int Fs = 44100;
        static System.Random r = new System.Random(5);
        static AudioClip seguro, tintineo, meter, cierre;
        public static AudioClip Seguro { get { return seguro ?? (seguro = Hacer("coop_esc_seguro", 0.18f, t => Ruido() * Exp(t, 90) * 0.5f + Sen(1800, t) * Exp(t, 70) * 0.4f + Sen(140, t) * Exp(t, 40) * 0.5f)); } }
        public static AudioClip Tintineo { get { return tintineo ?? (tintineo = Hacer("coop_esc_tintineo", 0.45f, t => Sen(3300, t) * Exp(t, 14) * 0.35f + Sen(5100, t) * Exp(t, 18) * 0.2f + Sen(2400, t) * Exp(t, 10) * 0.2f)); } }
        public static AudioClip Meter { get { return meter ?? (meter = Hacer("coop_esc_meter", 0.14f, t => Sen(900, t) * Exp(t, 60) * 0.4f + Ruido() * Exp(t, 120) * 0.25f + Sen(380, t) * Exp(t, 50) * 0.3f)); } }
        public static AudioClip Cierre { get { return cierre ?? (cierre = Hacer("coop_esc_cierre", 0.32f, t => Ruido() * Exp(t, 45) * 0.6f + Sen(220, t) * Exp(t, 25) * 0.7f + Sen(1400, t) * Exp(t, 60) * 0.3f)); } }

        static float Sen(float f, float t) { return Mathf.Sin(2f * Mathf.PI * f * t); }
        static float Exp(float t, float k) { return Mathf.Exp(-t * k); }
        static float Ruido() { return (float)(r.NextDouble() * 2.0 - 1.0); }
        static AudioClip Hacer(string nombre, float dur, System.Func<float, float> f)
        {
            int n = (int)(dur * Fs); var d = new float[n];
            for (int i = 0; i < n; i++) d[i] = Mathf.Clamp(f(i / (float)Fs), -1f, 1f);
            var c = AudioClip.Create(nombre, n, 1, Fs, false); c.SetData(d, 0); return c;
        }
    }
}

// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using System.Collections.Generic;
using UnityEngine;

namespace WitchHuntCoop
{
    /// Jefe de la guarida: «La Madre de la Guarida», una murciélaga gigante (~7 m de envergadura). Diseño en
    /// documentacion/JEFE-MURCIELAGO.md. ETAPA 1 (2026-11-18): solo el modelo hecho por el mod y su animación
    /// (colgada, planeo, aleteo, picada). Sin combate todavía.
    /// Modelo: cuerpo, cabeza, hocico, orejas, colmillos y ojos rojos que brillan; alas con huesos (húmero, antebrazo,
    /// 3 dedos) y una membrana que se rehace cada cuadro desde la posición de los huesos; patas con garras.
    public class Murcielago : MonoBehaviour
    {
        public enum Estado { Colgada, Planeo, Aleteo, Picada }
        public Estado estado = Estado.Aleteo;

        class Ala
        {
            public float lado; public Transform hombro, codo, muneca, cadera;
            public Transform[] dedos = new Transform[3], puntas = new Transform[3];
        }
        Ala izq, der; Transform cuerpo;
        Mesh membrana; Vector3[] vtx; Vector2[] uvs; int[] tri;
        float t, apertura = 1f, flapPrev; Vector3 basePos;
        static readonly float[] AngDedo = { 20f, -25f, -72f }, LargoDedo = { 1.25f, 1.45f, 1.05f };
        static Material matPiel, matMembrana, matHueso, matOjo, matColmillo, matNariz;
        static Mesh cono;

        // ---------------- Crear ----------------

        public static Murcielago Crear(Vector3 pos, Quaternion rot)
        {
            Materiales();
            var raiz = new GameObject("coop_jefe_murcielago");
            raiz.transform.position = pos; raiz.transform.rotation = rot; raiz.transform.localScale = Vector3.one * 0.85f;
            var m = raiz.AddComponent<Murcielago>();
            m.Armar();
            return m;
        }

        void Armar()
        {
            basePos = transform.position;
            cuerpo = new GameObject("cuerpo").transform; cuerpo.SetParent(transform, false);
            // tronco, pecho y cuello (pelaje)
            Esfera(cuerpo, "torso", new Vector3(0f, 0f, 0f), new Vector3(0.95f, 1.0f, 1.7f), matPiel);
            Esfera(cuerpo, "pecho", new Vector3(0f, 0.05f, 0.55f), new Vector3(0.95f, 0.95f, 0.95f), matPiel);
            Esfera(cuerpo, "cuello", new Vector3(0f, 0.2f, 0.9f), new Vector3(0.7f, 0.65f, 0.6f), matPiel);
            // cabeza
            var cab = new GameObject("cabeza").transform; cab.SetParent(cuerpo, false); cab.localPosition = new Vector3(0f, 0.28f, 1.15f);
            Esfera(cab, "craneo", Vector3.zero, new Vector3(0.78f, 0.68f, 0.75f), matPiel);
            Esfera(cab, "hocico", new Vector3(0f, -0.1f, 0.38f), new Vector3(0.42f, 0.34f, 0.48f), matPiel);
            Esfera(cab, "nariz", new Vector3(0f, -0.03f, 0.6f), new Vector3(0.2f, 0.13f, 0.1f), matNariz);
            Esfera(cab, "labio", new Vector3(0f, -0.24f, 0.45f), new Vector3(0.34f, 0.08f, 0.3f), matNariz);
            foreach (var s in new[] { -1f, 1f })
            {
                Cono(cab, "oreja", new Vector3(s * 0.26f, 0.28f, -0.05f), new Vector3(0.24f, 0.75f, 0.13f), Quaternion.Euler(-12f, 0f, -s * 20f), matPiel);
                Cono(cab, "oreja_int", new Vector3(s * 0.25f, 0.3f, -0.0f), new Vector3(0.15f, 0.6f, 0.06f), Quaternion.Euler(-12f, 0f, -s * 20f), matNariz);
                Esfera(cab, "ojo", new Vector3(s * 0.18f, 0.08f, 0.32f), new Vector3(0.12f, 0.1f, 0.08f), matOjo);
                Cono(cab, "colmillo", new Vector3(s * 0.09f, -0.24f, 0.55f), new Vector3(0.045f, 0.17f, 0.045f), Quaternion.Euler(180f, 0f, 0f), matColmillo);
                Cono(cab, "colmillo_inf", new Vector3(s * 0.12f, -0.29f, 0.45f), new Vector3(0.03f, 0.1f, 0.03f), Quaternion.identity, matColmillo);
            }
            var lz = new GameObject("luz_ojos"); lz.transform.SetParent(cab, false); lz.transform.localPosition = new Vector3(0f, 0.08f, 0.6f);
            var l = lz.AddComponent<Light>(); l.type = LightType.Point; l.color = new Color(1f, 0.1f, 0.05f); l.range = 3.5f; l.intensity = 1.6f; l.shadows = LightShadows.None;
            // patas con garras
            foreach (var s in new[] { -1f, 1f })
            {
                var cad = new GameObject("pata").transform; cad.SetParent(cuerpo, false); cad.localPosition = new Vector3(s * 0.25f, -0.3f, -0.85f);
                Hueso(cad, new Vector3(s * 0.1f, -0.35f, -0.4f), 0.11f, matPiel);
                for (int k = 0; k < 4; k++)
                    Cono(cad, "garra", new Vector3(s * 0.1f + (k - 1.5f) * 0.06f, -0.38f, -0.48f), new Vector3(0.025f, 0.16f, 0.025f), Quaternion.Euler(-110f, 0f, 0f), matColmillo);
            }
            izq = CrearAla(-1f); der = CrearAla(1f);
            // membrana (las dos alas, dos caras)
            var mg = new GameObject("membrana"); mg.transform.SetParent(cuerpo, false);
            membrana = new Mesh { name = "coop_membrana" }; membrana.MarkDynamic();
            mg.AddComponent<MeshFilter>().sharedMesh = membrana;
            var mr = mg.AddComponent<MeshRenderer>(); mr.sharedMaterial = matMembrana;
            vtx = new Vector3[2 * 2 * 10]; uvs = new Vector2[vtx.Length];
            var lt = new List<int>();
            int[,] fan = { { 2, 3, 4 }, { 2, 4, 5 }, { 2, 5, 6 }, { 2, 6, 7 }, { 2, 7, 8 }, { 2, 8, 9 }, { 2, 9, 0 }, { 0, 1, 2 } };
            for (int a = 0; a < 2; a++)
                for (int cara = 0; cara < 2; cara++)
                {
                    int o = (a * 2 + cara) * 10;
                    for (int k = 0; k < fan.GetLength(0); k++)
                    {
                        if (cara == 0) { lt.Add(o + fan[k, 0]); lt.Add(o + fan[k, 1]); lt.Add(o + fan[k, 2]); }
                        else { lt.Add(o + fan[k, 0]); lt.Add(o + fan[k, 2]); lt.Add(o + fan[k, 1]); }
                    }
                }
            tri = lt.ToArray();
            Animar(0f, true);
            Membrana(true);
        }

        Ala CrearAla(float lado)
        {
            var a = new Ala { lado = lado };
            Vector3 d = new Vector3(lado, 0f, 0f);
            a.hombro = Nodo(cuerpo, "hombro", new Vector3(lado * 0.42f, 0.22f, 0.4f));
            Esfera(a.hombro, "musculo", d * 0.25f, new Vector3(0.5f, 0.32f, 0.38f), matPiel);
            a.codo = Nodo(a.hombro, "codo", d * 0.95f + new Vector3(0f, 0f, -0.12f));
            Hueso(a.hombro, a.codo.localPosition, 0.13f, matHueso);
            a.muneca = Nodo(a.codo, "muneca", d * 1.35f + new Vector3(0f, 0f, 0.08f));
            Hueso(a.codo, a.muneca.localPosition, 0.09f, matHueso);
            Cono(a.muneca, "pulgar", new Vector3(0f, 0.04f, 0.08f), new Vector3(0.04f, 0.22f, 0.04f), Quaternion.Euler(70f, 0f, 0f), matColmillo);
            for (int k = 0; k < 3; k++)
            {
                a.dedos[k] = Nodo(a.muneca, "dedo" + k, Vector3.zero);
                a.puntas[k] = Nodo(a.dedos[k], "punta" + k, d * LargoDedo[k]);
                Hueso(a.dedos[k], a.puntas[k].localPosition, 0.05f - k * 0.008f, matHueso);
            }
            a.cadera = Nodo(cuerpo, "cadera", new Vector3(lado * 0.38f, -0.12f, -0.9f));
            return a;
        }

        // ---------------- Animación ----------------

        void Update()
        {
            t += Time.deltaTime;
            Animar(Time.deltaTime, false);
        }

        void LateUpdate() { Membrana(false); }

        void Animar(float dt, bool deGolpe)
        {
            float objetivo = estado == Estado.Colgada ? 0f : estado == Estado.Picada ? 0.45f : 1f;
            apertura = deGolpe ? objetivo : Mathf.MoveTowards(apertura, objetivo, dt * 1.5f);
            float flap = 0f, barrido = 0f, cabeceo = 0f;
            Vector3 pos = basePos;
            switch (estado)
            {
                case Estado.Aleteo:
                    flap = 42f * Mathf.Sin(t * 4.2f) + 8f; barrido = 6f * Mathf.Sin(t * 4.2f + 1.2f);
                    cabeceo = -6f; pos += Vector3.up * (0.35f * Mathf.Sin(t * 4.2f - 1.5f)); break;
                case Estado.Planeo:
                    flap = 6f + 4f * Mathf.Sin(t * 1.3f); cabeceo = 4f; pos += Vector3.up * 0.15f * Mathf.Sin(t * 0.9f); break;
                case Estado.Picada:
                    flap = 18f; barrido = 38f; cabeceo = 38f; break;
                case Estado.Colgada:
                    flap = -62f; barrido = 12f; pos += Vector3.up * 0.0f; break;
            }
            transform.position = pos;
            // colgada: cabeza abajo, balanceándose un poco
            cuerpo.localRotation = estado == Estado.Colgada
                ? Quaternion.Euler(90f + 4f * Mathf.Sin(t * 0.7f), 0f, 3f * Mathf.Sin(t * 0.5f))
                : Quaternion.Euler(cabeceo, 0f, 3f * Mathf.Sin(t * 1.1f));
            float lag = flap - flapPrev; flapPrev = flap;   // los dedos se quedan atrás al cambiar de dirección
            foreach (var a in new[] { izq, der })
            {
                a.hombro.localRotation = Quaternion.Euler(0f, a.lado * barrido, a.lado * flap);
                float codo = Mathf.Lerp(150f, 18f, apertura);
                a.codo.localRotation = Quaternion.Euler(0f, a.lado * codo, a.lado * (estado == Estado.Aleteo ? -0.35f * flap : 0f));
                a.muneca.localRotation = Quaternion.Euler(0f, a.lado * Mathf.Lerp(-155f, 0f, apertura), a.lado * Mathf.Clamp(-lag * 3f, -12f, 12f));
                for (int k = 0; k < 3; k++)
                {
                    float ang = Mathf.Lerp(-12f - k * 4f, AngDedo[k], apertura);
                    a.dedos[k].localRotation = Quaternion.Euler(0f, -a.lado * ang, a.lado * Mathf.Clamp(-lag * (2f + k), -15f, 15f));
                }
            }
        }

        void Membrana(bool primera)
        {
            int idx = 0;
            foreach (var a in new[] { izq, der })
            {
                Vector3 hombro = L(a.hombro), codo = L(a.codo), mun = L(a.muneca), cad = L(a.cadera);
                Vector3 p0 = L(a.puntas[0]), p1 = L(a.puntas[1]), p2 = L(a.puntas[2]);
                Vector3[] p = { hombro, codo, mun, p0, Festón(p0, p1, mun), p1, Festón(p1, p2, mun), p2, Festón(p2, cad, mun), cad };
                for (int cara = 0; cara < 2; cara++)
                    for (int k = 0; k < 10; k++)
                    {
                        vtx[idx + cara * 10 + k] = p[k];
                        if (primera) uvs[idx + cara * 10 + k] = new Vector2(p[k].x * 0.35f, p[k].z * 0.35f);
                    }
                idx += 20;
            }
            membrana.vertices = vtx;
            if (primera) { membrana.uv = uvs; membrana.triangles = tri; }
            membrana.RecalculateNormals(); membrana.RecalculateBounds();
        }

        static Vector3 Festón(Vector3 a, Vector3 b, Vector3 hacia) { return Vector3.Lerp(Vector3.Lerp(a, b, 0.5f), hacia, 0.22f); }
        Vector3 L(Transform x) { return cuerpo.InverseTransformPoint(x.position); }

        // ---------------- Piezas ----------------

        static Transform Nodo(Transform pa, string n, Vector3 p) { var g = new GameObject(n).transform; g.SetParent(pa, false); g.localPosition = p; return g; }

        static void Esfera(Transform pa, string n, Vector3 p, Vector3 s, Material m)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Sphere); Object.Destroy(g.GetComponent<Collider>());
            g.name = n; g.transform.SetParent(pa, false); g.transform.localPosition = p; g.transform.localScale = s;
            g.GetComponent<Renderer>().sharedMaterial = m;
        }

        static void Hueso(Transform pa, Vector3 hasta, float grosor, Material m)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder); Object.Destroy(g.GetComponent<Collider>());
            g.name = "hueso"; g.transform.SetParent(pa, false);
            g.transform.localPosition = hasta * 0.5f;
            g.transform.localRotation = Quaternion.FromToRotation(Vector3.up, hasta.normalized);
            g.transform.localScale = new Vector3(grosor, hasta.magnitude * 0.5f, grosor);
            g.GetComponent<Renderer>().sharedMaterial = m;
            Esfera(pa, "articulacion", hasta, Vector3.one * grosor * 1.3f, m);
        }

        static void Cono(Transform pa, string n, Vector3 p, Vector3 s, Quaternion r, Material m)
        {
            if (cono == null) cono = MallaCono(12);
            var g = new GameObject(n); g.transform.SetParent(pa, false); g.transform.localPosition = p; g.transform.localRotation = r; g.transform.localScale = s;
            g.AddComponent<MeshFilter>().sharedMesh = cono; g.AddComponent<MeshRenderer>().sharedMaterial = m;
        }

        static Mesh MallaCono(int n)
        {
            var v = new List<Vector3>(); var t = new List<int>();
            for (int k = 0; k < n; k++)
            {
                float a0 = k * Mathf.PI * 2f / n, a1 = (k + 1) * Mathf.PI * 2f / n;
                int i = v.Count;
                v.Add(new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0))); v.Add(Vector3.up); v.Add(new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)));
                t.Add(i); t.Add(i + 1); t.Add(i + 2);
                int j = v.Count; v.Add(Vector3.zero); v.Add(new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0))); v.Add(new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)));
                t.Add(j); t.Add(j + 1); t.Add(j + 2);
            }
            var m = new Mesh { name = "coop_cono" }; m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds(); return m;
        }

        // ---------------- Materiales (texturas hechas aquí) ----------------

        static void Materiales()
        {
            if (matPiel != null) return;
            matPiel = new Material(Shader.Find("Standard")) { name = "coop_murcielago_pelaje", mainTexture = TexPelaje(128) };
            matPiel.SetFloat("_Glossiness", 0.12f);
            matMembrana = new Material(Shader.Find("Standard")) { name = "coop_murcielago_membrana", mainTexture = TexMembrana(128), color = new Color(0.62f, 0.5f, 0.48f) };
            matMembrana.SetFloat("_Glossiness", 0.35f);
            matHueso = new Material(Shader.Find("Standard")) { name = "coop_murcielago_hueso", color = new Color(0.13f, 0.09f, 0.08f) }; matHueso.SetFloat("_Glossiness", 0.3f);
            matNariz = new Material(Shader.Find("Standard")) { name = "coop_murcielago_nariz", color = new Color(0.28f, 0.14f, 0.13f) }; matNariz.SetFloat("_Glossiness", 0.55f);
            matColmillo = new Material(Shader.Find("Standard")) { name = "coop_murcielago_colmillo", color = new Color(0.86f, 0.82f, 0.7f) }; matColmillo.SetFloat("_Glossiness", 0.6f);
            matOjo = new Material(Shader.Find("Standard")) { name = "coop_murcielago_ojo", color = new Color(1f, 0.08f, 0.04f) };
            matOjo.EnableKeyword("_EMISSION"); matOjo.SetColor("_EmissionColor", new Color(2.2f, 0.12f, 0.05f)); matOjo.SetFloat("_Glossiness", 0.9f);
        }

        static Texture2D TexPelaje(int n)
        {
            var tx = new Texture2D(n, n, TextureFormat.RGB24, true) { wrapMode = TextureWrapMode.Repeat };
            var rng = new System.Random(707);
            var c = new Color[n * n];
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                float vet = Mathf.PerlinNoise(x * 0.9f, y * 0.06f) * 0.6f + Mathf.PerlinNoise(x * 0.08f + 3f, y * 0.08f) * 0.4f;
                float g = 0.06f + vet * 0.16f + (float)rng.NextDouble() * 0.03f;
                c[y * n + x] = new Color(g * 1.25f, g * 0.95f, g * 0.8f);
            }
            tx.SetPixels(c); tx.Apply(); return tx;
        }

        static Texture2D TexMembrana(int n)
        {
            var tx = new Texture2D(n, n, TextureFormat.RGB24, true) { wrapMode = TextureWrapMode.Repeat };
            var c = new Color[n * n];
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                float arr = Mathf.PerlinNoise(x * 0.05f, y * 0.05f);
                float vena = Mathf.Abs(Mathf.Sin((x + Mathf.PerlinNoise(x * 0.07f, y * 0.07f) * 30f) * 0.18f));
                float g = 0.2f + arr * 0.1f - (vena < 0.06f ? 0.06f : 0f);
                c[y * n + x] = new Color(g * 1.2f, g * 0.8f, g * 0.75f);
            }
            tx.SetPixels(c); tx.Apply(); return tx;
        }

        // ---------------- Orden de prueba ----------------

        static Murcielago actual;

        /// jefe murcielago [estado] | jefe estado colgada|planeo|aleteo|picada | jefe quitar
        public static string Orden(string arg)
        {
            var pa = arg.Split(' ');
            var cam = Camera.main;
            if (pa[0] == "huesos") return MurcielagoJuego.Jerarquia();
            if (pa[0] == "juego")   // murcielago juego [escala] [distancia]: copia del modelo del juego delante de la cámara
            {
                if (cam == null) return "sin cámara";
                var ci = System.Globalization.CultureInfo.InvariantCulture;
                float esc = pa.Length > 1 ? float.Parse(pa[1], ci) : 1f, dist = pa.Length > 2 ? float.Parse(pa[2], ci) : 10f;
                return MurcielagoJuego.Poner(cam, esc, dist);
            }
            if (pa[0] == "quitar") { if (actual != null) Object.Destroy(actual.gameObject); actual = null; return "quitada"; }
            if (pa[0] == "estado")
            {
                if (actual == null) return "no hay murciélaga";
                actual.estado = Parse(pa.Length > 1 ? pa[1] : "aleteo"); return "estado " + actual.estado;
            }
            if (pa[0] == "crear")
            {
                if (cam == null) return "sin cámara";
                if (actual != null) Object.Destroy(actual.gameObject);
                Vector3 f = cam.transform.forward; f.y = 0f; f.Normalize();
                Vector3 p = cam.transform.position + f * 8f + Vector3.up * 1.5f;
                actual = Crear(p, Quaternion.LookRotation(-f, Vector3.up));
                actual.estado = Parse(pa.Length > 1 ? pa[1] : "aleteo");
                return "murciélaga en " + p.ToString("F1") + " (" + actual.estado + ")";
            }
            return "uso: murcielago crear [estado] | murcielago estado <colgada|planeo|aleteo|picada> | murcielago quitar";
        }

        static Estado Parse(string s)
        {
            switch (s) { case "colgada": return Estado.Colgada; case "planeo": return Estado.Planeo; case "picada": return Estado.Picada; default: return Estado.Aleteo; }
        }
    }
}

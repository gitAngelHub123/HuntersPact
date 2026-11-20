// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using System.Collections.Generic;
using UnityEngine;

namespace WitchHuntCoop
{
    /// Castillo medieval embrujado (pedido 2026-11-11) en los 500 m más al norte de la zona nueva: una explanada llana con un camino de
    /// piedra, escalinata con faroles, torres con techos puntiagudos, almenas, ventanas encendidas, un gran salón por dentro
    /// (columnas, antorchas, trono), niebla baja y luces fantasmales. Todo es del mod (formas simples con colisión).
    public partial class Mundo
    {
        public const float CastilloX = 1500f, CastilloZ = 832f;   // centro, en coordenadas del terreno norte
        public static readonly Vector3 CastilloPuertaRel = new Vector3(0f, 3f, 0f);
        GameObject castilloRaiz;
        Vector3 castilloSuelo;   // origen del castillo en el mundo (pie de la escalinata va a -14 en z)

        bool EnCastillo(Vector3 w)
        {
            if (nuevo == null) return false;
            Vector3 l = w - nuevo.transform.position;
            return EnZonaCastillo(l.x, l.z, 40f);   // montaña + laberinto de setos
        }

        public Vector3? PosCastillo(float z) { return castilloRaiz == null ? (Vector3?)null : castilloSuelo + new Vector3(0f, 0f, z * CastilloEscala) - Ancla(); }

        // ---------------- Materiales ----------------

        static Material matPiedra, matTejado, matVentana, matAdoquin, matMadera2, matTela, matHierroC, matVidrio;

        /// Mapa de relieve (normales) a partir de un mapa de alturas; guardado como lo lee el shader de Unity en escritorio (x en alfa, y en verde).
        static Texture2D Normales(float[,] alt, int n, float fuerza)
        {
            var t = new Texture2D(n, n, TextureFormat.ARGB32, true) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Repeat };
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                float dx = alt[(x + 1) % n, y] - alt[(x + n - 1) % n, y], dy = alt[x, (y + 1) % n] - alt[x, (y + n - 1) % n];
                var v = new Vector3(-dx * fuerza, -dy * fuerza, 1f).normalized;
                t.SetPixel(x, y, new Color(1f, v.y * 0.5f + 0.5f, 1f, v.x * 0.5f + 0.5f));
            }
            t.Apply(); return t;
        }

        static void TexLadrillo(out Texture2D albedo, out Texture2D normal, int n, int filas, Color a, Color b, Color mortero, float musgo, int semilla)
        {
            var rng = new System.Random(semilla);
            var col = new Color[n * n]; var alt = new float[n, n];
            int hF = n / filas, wL = hF * 2;
            for (int fila = 0; fila < filas; fila++)
            {
                int desfase = (fila % 2) * (wL / 2) + rng.Next(0, 3);
                for (int bl = -1; bl < n / wL + 1; bl++)
                {
                    float tono = (float)rng.NextDouble(); Color cb = Color.Lerp(a, b, tono); float aleatorio = 0.82f + (float)rng.NextDouble() * 0.3f; cb *= aleatorio; cb.a = 1f;
                    cb = Color.Lerp(cb, new Color(cb.b, cb.g * 0.95f, cb.r), (float)rng.NextDouble() * 0.15f);   // algún bloque más cálido o más frío
                    float so = (float)rng.NextDouble() * 100f;
                    for (int yy = 0; yy < hF; yy++) for (int xx = 0; xx < wL; xx++)
                    {
                        int x = (bl * wL + desfase + xx + n * 4) % n, y = fila * hF + yy;
                        float distBorde = Mathf.Min(Mathf.Min(xx, wL - 1 - xx), Mathf.Min(yy, hF - 1 - yy));
                        float ruido = Mathf.PerlinNoise(x * 0.05f + semilla, y * 0.05f) * 0.4f + Mathf.PerlinNoise(x * 0.17f + so, y * 0.17f) * 0.3f + Mathf.PerlinNoise(x * 0.6f, y * 0.6f + so) * 0.3f;
                        // desportillado: el borde del bloque se rompe de forma irregular
                        float roto = Mathf.PerlinNoise(x * 0.4f + 11f, y * 0.4f + so);
                        bool borde = distBorde < 1.2f + roto * 1.8f;
                        float bis = Mathf.Clamp01(distBorde / 4f);
                        Color c = borde ? mortero * (0.8f + ruido * 0.6f) : cb * (0.72f + ruido * 0.5f) * (0.82f + 0.18f * bis);
                        // grietas finas
                        float gr = Mathf.Abs(Mathf.PerlinNoise(x * 0.045f + so, y * 0.045f) - 0.5f);
                        if (!borde && gr < 0.012f) c *= 0.45f;
                        // mugre que chorrea desde arriba y musgo en manchas
                        float mug = Mathf.PerlinNoise(x * 0.25f + 5f, y * 0.012f + so);
                        if (!borde && mug > 0.62f) c = Color.Lerp(c, c * 0.5f, (mug - 0.62f) * 2.5f);
                        float m = Mathf.PerlinNoise(x * 0.03f + 40f, y * 0.03f + semilla) - 0.55f;
                        if (!borde && m > 0f) c = Color.Lerp(c, new Color(0.17f, 0.3f, 0.12f) * (0.7f + ruido * 0.6f), Mathf.Clamp01(m * 3f) * musgo);
                        col[y * n + x] = new Color(c.r, c.g, c.b, 1f); alt[x, y] = borde ? 0f : 0.45f + bis * 0.35f + ruido * 0.3f;
                    }
                }
            }
            albedo = new Texture2D(n, n, TextureFormat.RGB24, true) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Repeat };
            albedo.SetPixels(col); albedo.Apply();
            normal = Normales(alt, n, 3f);
        }

        /// Detalle fino en gris (se mezcla por encima de la textura principal, a mayor repetición, para que de cerca no se vea borrosa).
        static Texture2D TexDetalle(int n)
        {
            var t = new Texture2D(n, n, TextureFormat.RGB24, true) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Repeat };
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                float u = x / (float)n, v = y / (float)n;
                float g = 0.5f + (Ruido(x, y, n, 0.09f) - 0.5f) * 0.7f + (Ruido(x + 37, y + 11, n, 0.31f) - 0.5f) * 0.55f + (Ruido(x + 5, y + 90, n, 0.9f) - 0.5f) * 0.4f;
                g = Mathf.Clamp01(g); t.SetPixel(x, y, new Color(g, g, g));
            }
            t.Apply(); return t;
        }

        /// Ruido de Perlin que se repite cada n píxeles.
        static float Ruido(float x, float y, int n, float f)
        {
            float u = x / n, v = y / n;
            float a1 = Mathf.PerlinNoise(x * f, y * f), a2 = Mathf.PerlinNoise((x - n) * f, y * f), a3 = Mathf.PerlinNoise(x * f, (y - n) * f), a4 = Mathf.PerlinNoise((x - n) * f, (y - n) * f);
            return Mathf.Lerp(Mathf.Lerp(a1, a2, u), Mathf.Lerp(a3, a4, u), v);
        }

        static void TexTejas(out Texture2D albedo, out Texture2D normal, int n)
        {
            var rng = new System.Random(9); var col = new Color[n * n]; var alt = new float[n, n]; int filas = 8, hF = n / filas, wT = hF;
            for (int fila = 0; fila < filas; fila++)
            {
                int desfase = (fila % 2) * wT / 2;
                for (int t = -1; t <= n / wT; t++)
                {
                    float tono = (float)rng.NextDouble();
                    for (int yy = 0; yy < hF; yy++) for (int xx = 0; xx < wT; xx++)
                    {
                        int x = (t * wT + desfase + xx + n * 4) % n, y = fila * hF + yy;
                        float redond = 1f - Mathf.Abs((xx - wT / 2f) / (wT / 2f)) * 0.25f - (yy < 2 ? 0.5f : 0f);
                        Color c = Color.Lerp(new Color(0.06f, 0.08f, 0.13f), new Color(0.17f, 0.2f, 0.28f), tono * 0.8f + yy / (float)hF * 0.2f) * redond;
                        col[y * n + x] = new Color(c.r, c.g, c.b, 1f); alt[x, y] = redond * (yy / (float)hF);
                    }
                }
            }
            albedo = new Texture2D(n, n, TextureFormat.RGB24, true) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Repeat }; albedo.SetPixels(col); albedo.Apply();
            normal = Normales(alt, n, 2.5f);
        }

        /// Suelo de rocas y losas irregulares (celdas de Voronoi que se repiten): cada piedra con su tono, bordes gastados y juntas de tierra.
        static void TexAdoquin(out Texture2D albedo, out Texture2D normal, int n)
        {
            const int K = 6;   // piedras por lado (en cada repetición)
            var rng = new System.Random(5);
            var px = new Vector2[K * K]; var tono = new float[K * K]; var tinte = new float[K * K];
            for (int j = 0; j < K; j++) for (int i = 0; i < K; i++)
            {
                px[j * K + i] = new Vector2((i + 0.15f + (float)rng.NextDouble() * 0.7f) / K, (j + 0.15f + (float)rng.NextDouble() * 0.7f) / K);
                tono[j * K + i] = (float)rng.NextDouble(); tinte[j * K + i] = (float)rng.NextDouble();
            }
            var col = new Color[n * n]; var alt = new float[n, n];
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                // el contorno se deforma con ruido para que las rocas no sean polígonos rectos
                float u = x / (float)n + (Ruido(x, y, n, 0.07f) - 0.5f) * 0.03f, v = y / (float)n + (Ruido(x + 50, y + 20, n, 0.07f) - 0.5f) * 0.03f;
                float d1 = 9f, d2 = 9f; int id = 0;
                int fu = Mathf.FloorToInt(u * K), fv = Mathf.FloorToInt(v * K);
                for (int dj = -1; dj <= 1; dj++) for (int di = -1; di <= 1; di++)
                {
                    int gi = fu + di, gj = fv + dj;
                    int ci = ((gi % K) + K) % K, cj = ((gj % K) + K) % K;
                    float cx = px[cj * K + ci].x + Mathf.Floor(gi / (float)K), cy = px[cj * K + ci].y + Mathf.Floor(gj / (float)K);
                    float dd = Mathf.Sqrt((u - cx) * (u - cx) + (v - cy) * (v - cy));
                    if (dd < d1) { d2 = d1; d1 = dd; id = cj * K + ci; } else if (dd < d2) d2 = dd;
                }
                float borde = (d2 - d1) * K;   // 0 en la junta, crece hacia el centro de la piedra
                float grano = Ruido(x, y, n, 0.22f) * 0.5f + Ruido(x + 9, y + 3, n, 0.7f) * 0.5f;
                Color roca = Color.Lerp(new Color(0.2f, 0.19f, 0.19f), new Color(0.46f, 0.44f, 0.42f), tono[id]);
                roca = Color.Lerp(roca, new Color(roca.b, roca.g * 1.05f, roca.r * 0.9f), tinte[id] * 0.25f);   // algunas más frías
                roca *= 0.7f + grano * 0.55f;
                float bis = Mathf.Clamp01(borde / 0.22f);
                Color c2 = Color.Lerp(new Color(0.05f, 0.045f, 0.04f) * (0.6f + grano), roca * (0.8f + 0.2f * bis), Mathf.SmoothStep(0f, 1f, borde / 0.07f));   // junta de tierra oscura
                // musgo en las juntas
                float mu = Ruido(x + 70, y + 40, n, 0.05f);
                if (mu > 0.55f && borde < 0.2f) c2 = Color.Lerp(c2, new Color(0.1f, 0.2f, 0.07f), (mu - 0.55f) * 3f);
                col[y * n + x] = new Color(c2.r, c2.g, c2.b, 1f);
                alt[x, y] = Mathf.SmoothStep(0f, 1f, borde / 0.12f) * (0.65f + 0.2f * bis) + grano * 0.25f;
            }
            albedo = new Texture2D(n, n, TextureFormat.RGB24, true) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Repeat }; albedo.SetPixels(col); albedo.Apply();
            normal = Normales(alt, n, 5f);
        }

        /// Hierro forjado: gris oscuro picado con manchas de óxido.
        static void TexHierro(out Texture2D albedo, out Texture2D normal, int n)
        {
            var col = new Color[n * n]; var alt = new float[n, n];
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                float g = Ruido(x, y, n, 0.18f) * 0.5f + Ruido(x + 20, y + 5, n, 0.55f) * 0.5f;
                float ox = Ruido(x + 60, y + 31, n, 0.07f);
                Color c = Color.Lerp(new Color(0.05f, 0.05f, 0.06f), new Color(0.2f, 0.2f, 0.22f), g);
                if (ox > 0.52f) c = Color.Lerp(c, new Color(0.34f, 0.15f, 0.06f) * (0.7f + g * 0.6f), Mathf.Clamp01((ox - 0.52f) * 4f));
                col[y * n + x] = new Color(c.r, c.g, c.b, 1f); alt[x, y] = g;
            }
            albedo = new Texture2D(n, n, TextureFormat.RGB24, true) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Repeat }; albedo.SetPixels(col); albedo.Apply();
            normal = Normales(alt, n, 2.5f);
        }

        static readonly Dictionary<Material, Material> sinRelieve = new Dictionary<Material, Material>();
        /// Copia sin mapa de normales: las mallas hechas a mano no llevan tangentes y con relieve se ven negras.
        static Material SinRelieve(Material m)
        {
            Material c;
            if (sinRelieve.TryGetValue(m, out c) && c != null) return c;
            c = new Material(m) { name = m.name + "_plano" }; c.SetTexture("_BumpMap", null); c.DisableKeyword("_NORMALMAP");
            sinRelieve[m] = c; return c;
        }
        static Texture2D texDetalle;
        static Material Estandar(string nombre, Texture2D alb, Texture2D nor, Color tinte, float brillo, float normalFuerza = 1f, bool detalle = false)
        {
            var m = new Material(Shader.Find("Standard")) { name = nombre, color = tinte };
            if (detalle) { if (texDetalle == null) texDetalle = TexDetalle(128); m.SetTexture("_DetailAlbedoMap", texDetalle); m.EnableKeyword("_DETAIL_MULX2"); }
            if (alb != null) m.mainTexture = alb;
            if (nor != null) { m.SetTexture("_BumpMap", nor); m.SetFloat("_BumpScale", normalFuerza); m.EnableKeyword("_NORMALMAP"); }
            m.SetFloat("_Glossiness", brillo); return m;
        }

        void PrepararMaterialesCastillo()
        {
            if (matPiedra != null) return;
            Texture2D a, n;
            TexLadrillo(out a, out n, 512, 20, new Color(0.34f, 0.36f, 0.42f), new Color(0.52f, 0.53f, 0.57f), new Color(0.06f, 0.06f, 0.07f), 1f, 3);
            matPiedra = Estandar("coop_castillo_piedra", a, n, Color.white, 0.1f, 1.5f, true);
            Texture2D at, nt; TexTejas(out at, out nt, 128);
            matTejado = Estandar("coop_castillo_tejado", at, nt, Color.white, 0.35f, 1.4f);
            Texture2D aa, na; TexAdoquin(out aa, out na, 256);
            matAdoquin = Estandar("coop_castillo_adoquin", aa, na, Color.white, 0.18f, 1.6f, true);
            matVentana = new Material(Shader.Find("Unlit/Color")) { name = "coop_castillo_ventana", color = new Color(1f, 0.72f, 0.32f) };
            matVidrio = new Material(Shader.Find("Unlit/Color")) { name = "coop_castillo_rosetón", color = new Color(0.62f, 0.34f, 0.95f) };
            var mad = TexRuido(64, (x, y) => 0.35f + 0.3f * Mathf.Abs(Mathf.Sin(x * 0.9f + Mathf.PerlinNoise(x * 0.1f, y * 0.04f) * 3f)), new Color(0.07f, 0.04f, 0.025f), new Color(0.24f, 0.15f, 0.08f));
            matMadera2 = Estandar("coop_castillo_madera", mad, null, Color.white, 0.18f);
            matTela = new Material(Shader.Find("Standard")) { name = "coop_castillo_tela", color = new Color(0.45f, 0.04f, 0.06f) }; matTela.SetFloat("_Glossiness", 0.05f);
            Texture2D ah, nh; TexHierro(out ah, out nh, 128);
            matHierroC = Estandar("coop_castillo_hierro", ah, nh, Color.white, 0.4f, 1.3f); matHierroC.SetFloat("_Metallic", 0.7f);
            // grano de piedra real (2026-11-18): el granito del terreno como textura de detalle sobre los bloques y adoquines
            var gran = GranitoTerreno();
            if (gran != null)
                foreach (var m in new[] { matPiedra, matAdoquin })
                { m.SetTexture("_DetailAlbedoMap", gran); m.SetTextureScale("_DetailAlbedoMap", new Vector2(0.45f, 0.45f)); m.EnableKeyword("_DETAIL_MULX2"); m.SetFloat("_Glossiness", 0.06f); }
        }

        /// Textura de granito del terreno del juego (hecha para repetirse), o null.
        static Texture2D GranitoTerreno()
        {
            var t = Anclaje.Principal();
            if (t == null || t.terrainData == null) return null;
            foreach (var sp in t.terrainData.splatPrototypes) if (sp.texture != null && sp.texture.name.ToLowerInvariant().Contains("granite")) return sp.texture;
            return null;
        }

        static Texture2D TexRuido(int n, System.Func<int, int, float> f, Color a, Color b)
        {
            var t = new Texture2D(n, n, TextureFormat.RGB24, true) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Repeat };
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++) t.SetPixel(x, y, Color.Lerp(a, b, Mathf.Clamp01(f(x, y))));
            t.Apply(); return t;
        }

        // ---------------- Piezas ----------------

        static readonly Dictionary<string, Material> matsPz = new Dictionary<string, Material>();
        static float CuantizarEsc(float v) { return v < 2f ? Mathf.Max(1f, Mathf.Round(v * 2f) / 2f) : Mathf.Round(v); }

        /// Agrupa las piezas fijas del castillo por zonas (de ~30 m) para que se dibujen en pocas tandas en vez de miles de objetos sueltos.
        void CombinarCastillo(Transform raiz)
        {
            try
            {
                var grupos = new Dictionary<string, List<GameObject>>();
                int total = 0;
                foreach (var mf in raiz.GetComponentsInChildren<MeshFilter>(false))
                {
                    var go = mf.gameObject;
                    if (mf.sharedMesh == null || go.GetComponent<MeshRenderer>() == null || go.GetComponent<MonoBehaviour>() != null || go.name == "coop_boveda_puerta") continue;
                    Vector3 l = raiz.InverseTransformPoint(go.transform.position);
                    string k = Mathf.FloorToInt(l.x / 30f) + "," + Mathf.FloorToInt(l.z / 30f) + "," + Mathf.FloorToInt(l.y / 40f);
                    List<GameObject> lista; if (!grupos.TryGetValue(k, out lista)) grupos[k] = lista = new List<GameObject>();
                    lista.Add(go); total++;
                }
                int lotes = 0;
                foreach (var kv in grupos) if (kv.Value.Count > 1) { StaticBatchingUtility.Combine(kv.Value.ToArray(), raiz.gameObject); lotes++; }
                plugin.Log("Castillo: " + total + " piezas agrupadas en " + lotes + " zonas (dibujado por lotes)");
            }
            catch (System.Exception e) { plugin.Log("Castillo: no se pudo agrupar: " + e.Message); }
        }

        GameObject Pz(Transform pa, string nombre, PrimitiveType tipo, Vector3 c, Vector3 tam, float giro, Material m, bool col = true, float tile = 6f)
        {
            var g = GameObject.CreatePrimitive(tipo);
            if (!col) Object.Destroy(g.GetComponent<Collider>());
            else if (tipo == PrimitiveType.Cylinder)
            {
                // el cilindro de Unity trae una cápsula: en los cilindros anchos y planos (pilares, columna de la torre, torres) los bordes
                // redondeados dejaban partes que se atravesaban → colisión con la forma real del cilindro
                Object.DestroyImmediate(g.GetComponent<Collider>());
                g.AddComponent<MeshCollider>().sharedMesh = g.GetComponent<MeshFilter>().sharedMesh;
            }
            g.name = nombre; g.transform.SetParent(pa, false);
            g.transform.localPosition = c; g.transform.localScale = tam; g.transform.localRotation = Quaternion.Euler(0f, giro, 0f);
            Vector2 esc = Vector2.one;
            if (m.mainTexture != null)
            {
                if (tipo == PrimitiveType.Cylinder) esc = new Vector2(Mathf.Max(1f, tam.x * 3.14f / tile), Mathf.Max(1f, tam.y * 2f / tile));
                else if (tam.y < Mathf.Min(tam.x, tam.z) * 0.2f) esc = new Vector2(Mathf.Max(1f, tam.x / tile), Mathf.Max(1f, tam.z / tile));   // losa horizontal: la textura se reparte sobre el suelo, no en rayas
                else esc = new Vector2(Mathf.Max(1f, Mathf.Max(tam.x, tam.z) / tile), Mathf.Max(1f, tam.y / tile));
                // se redondea la repetición para compartir el mismo material entre muchas piezas (así se pueden agrupar y dibujar de una vez)
                esc = new Vector2(CuantizarEsc(esc.x), CuantizarEsc(esc.y));
            }
            string clave = m.GetInstanceID() + ":" + esc.x + ":" + esc.y;
            Material mm;
            if (!matsPz.TryGetValue(clave, out mm) || mm == null)
            {
                mm = new Material(m);
                if (m.mainTexture != null)
                {
                    mm.mainTextureScale = esc;
                    if (mm.GetTexture("_DetailAlbedoMap") != null) mm.SetTextureScale("_DetailAlbedoMap", esc * 5f);
                }
                matsPz[clave] = mm;
            }
            g.GetComponent<Renderer>().sharedMaterial = mm;
            return g;
        }

        /// Pieza con rotación completa (para arcos, rampas y gárgolas).
        GameObject PzR(Transform pa, string nombre, PrimitiveType tipo, Vector3 c, Vector3 tam, Quaternion rot, Material m, bool col = false)
        {
            var g = Pz(pa, nombre, tipo, c, tam, 0f, m, col, 6f); g.transform.localRotation = rot; return g;
        }

        void Cono(Transform pa, string nombre, Vector3 baseC, float radio, float alto, bool invertido = false)
        {
            const int N = 20;
            var v = new List<Vector3>(); var tr = new List<int>(); var uv = new List<Vector2>();
            v.Add(baseC + Vector3.up * (invertido ? -alto : alto)); uv.Add(new Vector2(0.5f, 1f));
            for (int i = 0; i < N; i++) { float a = i * Mathf.PI * 2f / N; v.Add(baseC + new Vector3(Mathf.Cos(a) * radio, 0f, Mathf.Sin(a) * radio)); uv.Add(new Vector2(i / (float)N * 6f, 0f)); }
            for (int i = 0; i < N; i++) { int a = 1 + i, b = 1 + (i + 1) % N; tr.Add(0); tr.Add(b); tr.Add(a); tr.Add(0); tr.Add(a); tr.Add(b); }
            var me = new Mesh { name = nombre }; me.SetVertices(v); me.SetUVs(0, uv); me.SetTriangles(tr, 0); me.RecalculateNormals(); me.RecalculateBounds();
            var g = new GameObject(nombre); g.transform.SetParent(pa, false);
            g.AddComponent<MeshFilter>().sharedMesh = me; g.AddComponent<MeshRenderer>().sharedMaterial = SinRelieve(matTejado);
            g.AddComponent<MeshCollider>().sharedMesh = me;
        }

        /// Torre con cornisa de ménsulas, almenas pequeñas bajo el techo, aros de piedra, saeteras, techo puntiagudo, cruz y bandera.
        void Torre(Transform pa, string nombre, float x, float z, float y0, float y1, float r, float alturaTecho)
        {
            Pz(pa, nombre, PrimitiveType.Cylinder, new Vector3(x, (y0 + y1) / 2f, z), new Vector3(r * 2f, (y1 - y0) / 2f, r * 2f), 0f, matPiedra);
            // zócalo ensanchado y aros de piedra
            // zócalo de y0 − 2 a y0 + 3,5: antes acababa justo a 3 m, la misma altura que la plataforma y la base de las alas, y las dos caras
            // superiores en el mismo plano parpadeaban (un disco claro "bugeado" al pie de la torre); también se hunde en el terreno inclinado
            Pz(pa, nombre + "_zocalo", PrimitiveType.Cylinder, new Vector3(x, y0 + 0.75f, z), new Vector3(r * 2f + 1.6f, 2.75f, r * 2f + 1.6f), 0f, matPiedra);
            Pz(pa, nombre + "_zocalo_moldura", PrimitiveType.Cylinder, new Vector3(x, y0 + 3.5f, z), new Vector3(r * 2f + 1.9f, 0.12f, r * 2f + 1.9f), 0f, matPiedra, false);
            for (float y = y0 + 9f; y < y1 - 3f; y += 9f) Pz(pa, nombre + "_aro", PrimitiveType.Cylinder, new Vector3(x, y, z), new Vector3(r * 2f + 0.5f, 0.28f, r * 2f + 0.5f), 0f, matPiedra);
            // cornisa con ménsulas
            Pz(pa, nombre + "_cornisa", PrimitiveType.Cylinder, new Vector3(x, y1 + 0.35f, z), new Vector3(r * 2f + 2f, 0.35f, r * 2f + 2f), 0f, matPiedra);
            int mens = Mathf.Max(8, (int)(r * 3.2f));
            for (int i = 0; i < mens; i++) { float a = i * Mathf.PI * 2f / mens; Pz(pa, nombre + "_mensula", PrimitiveType.Cube, new Vector3(x + Mathf.Cos(a) * (r + 0.5f), y1 - 0.4f, z + Mathf.Sin(a) * (r + 0.5f)), new Vector3(0.6f, 0.9f, 0.6f), -a * Mathf.Rad2Deg, matPiedra, false); }
            Cono(pa, nombre + "_techo", new Vector3(x, y1 + 0.7f, z), r + 1.8f, alturaTecho);
            Pz(pa, nombre + "_asta", PrimitiveType.Cylinder, new Vector3(x, y1 + 0.7f + alturaTecho + 2.2f, z), new Vector3(0.14f, 2.4f, 0.14f), 0f, matHierroC, false);
            var bd = Pz(pa, nombre + "_bandera", PrimitiveType.Cube, new Vector3(x + 1.5f, y1 + 0.7f + alturaTecho + 3.2f, z), new Vector3(2.8f, 1.5f, 0.05f), 0f, matTela, false);
            bd.AddComponent<Ondear>().Fase = x * 0.13f + z * 0.07f;
            // saeteras (ranuras estrechas encendidas) en el cuerpo, mirando a los cuatro lados
            for (float y = y0 + 6f; y < y1 - 4f; y += 7f) for (int k = 0; k < 4; k++)
            {
                float a = k * Mathf.PI / 2f + 0.6f; var n = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                VentanaOjival(pa, new Vector3(x, y, z) + n * (r + 0.02f), n, 0.7f, 2.2f, 0.45f, false);
            }
        }

        /// Ventana ojival (rectángulo con punta) encendida, con jambas, dintel en arco y alféizar de piedra.
        void VentanaOjival(Transform pa, Vector3 c, Vector3 normal, float ancho, float alto, float punta, bool marco = true)
        {
            var piv = new GameObject("coop_ventana"); piv.transform.SetParent(pa, false);
            piv.transform.localPosition = c; piv.transform.localRotation = Quaternion.LookRotation(normal, Vector3.up);
            float w = ancho / 2f;
            var vs = new[] { new Vector3(-w, 0f, 0f), new Vector3(w, 0f, 0f), new Vector3(w, alto, 0f), new Vector3(0f, alto + punta, 0f), new Vector3(-w, alto, 0f) };
            var tri = new[] { 0, 2, 1, 0, 4, 2, 4, 3, 2, 0, 1, 2, 0, 2, 4, 4, 2, 3 };   // las dos caras
            var me = new Mesh { name = "coop_ventana" }; me.vertices = vs; me.triangles = tri; me.RecalculateNormals();
            var uvs = new Vector2[vs.Length]; for (int i = 0; i < vs.Length; i++) uvs[i] = new Vector2(vs[i].x / 0.34f, vs[i].y / 0.34f); me.uv = uvs;   // vidrios emplomados de ~34 cm
            var m = new GameObject("vidrio"); m.transform.SetParent(piv.transform, false); m.transform.localPosition = new Vector3(0f, 0f, 0.05f);
            m.AddComponent<MeshFilter>().sharedMesh = me; var mr = m.AddComponent<MeshRenderer>(); mr.sharedMaterial = MaterialVentanaEmplomada(); mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (!marco) { PzP(piv.transform, new Vector3(0f, alto / 2f, -0.04f), new Vector3(ancho + 0.5f, alto + 0.4f, 0.3f)); return; }
            PzP(piv.transform, new Vector3(-w - 0.2f, alto / 2f, 0.12f), new Vector3(0.4f, alto + 0.3f, 0.5f));
            PzP(piv.transform, new Vector3(w + 0.2f, alto / 2f, 0.12f), new Vector3(0.4f, alto + 0.3f, 0.5f));
            PzP(piv.transform, new Vector3(0f, -0.15f, 0.2f), new Vector3(ancho + 1f, 0.3f, 0.7f));
            float ang = Mathf.Atan2(punta, w) * Mathf.Rad2Deg, largo = Mathf.Sqrt(punta * punta + w * w) + 0.3f;
            PzP(piv.transform, new Vector3(-w / 2f - 0.05f, alto + punta / 2f, 0.12f), new Vector3(largo, 0.4f, 0.5f), ang);    // los dos lados del arco se juntan en la punta
            PzP(piv.transform, new Vector3(w / 2f + 0.05f, alto + punta / 2f, 0.12f), new Vector3(largo, 0.4f, 0.5f), -ang);   // (antes estaban girados al revés y formaban una "V")
            PzP(piv.transform, new Vector3(0f, alto / 2f, 0.07f), new Vector3(0.1f, alto, 0.05f), 0f, matMadera2);   // parteluz de madera
        }

        static Material matVentanaEmplomada;
        /// Ventana encendida con vidrios en rombo emplomados (antes: un color amarillo liso).
        static Material MaterialVentanaEmplomada()
        {
            if (matVentanaEmplomada != null) return matVentanaEmplomada;
            const int N = 64;
            var t = new Texture2D(N, N, TextureFormat.RGB24, true) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Repeat };
            var rng = new System.Random(911);
            float[] tono = new float[4]; for (int i = 0; i < 4; i++) tono[i] = 0.8f + 0.35f * (float)rng.NextDouble();
            for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
            {
                float u = (x + 0.5f) / N, v = (y + 0.5f) / N;
                float a = u + v, b = u - v + 1f;                      // rombos: líneas diagonales
                float da = Mathf.Abs(a - Mathf.Round(a)), db = Mathf.Abs(b - Mathf.Round(b));
                int celda = ((int)Mathf.Floor(a) + 2 * (int)Mathf.Floor(b)) & 3;
                float centro = Mathf.Min(da, db) * 2f;                  // 0 en el plomo, 1 en el centro del vidrio
                Color c = new Color(1f, 0.7f, 0.3f) * tono[celda] * (0.75f + 0.35f * centro);
                c *= 0.92f + 0.16f * Mathf.PerlinNoise(x * 0.3f, y * 0.3f);
                if (da < 0.035f || db < 0.035f) c = new Color(0.12f, 0.08f, 0.05f);
                c.a = 1f; t.SetPixel(x, y, c);
            }
            t.Apply();
            matVentanaEmplomada = new Material(Shader.Find("Unlit/Texture")) { name = "coop_ventana_emplomada", mainTexture = t };
            return matVentanaEmplomada;
        }

        void PzP(Transform piv, Vector3 c, Vector3 tam, float roll = 0f, Material m = null)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube); Object.Destroy(g.GetComponent<Collider>());
            g.name = "coop_marco"; g.transform.SetParent(piv, false); g.transform.localPosition = c; g.transform.localScale = tam; g.transform.localRotation = Quaternion.Euler(0f, 0f, roll);
            var mm = new Material(m ?? matPiedra); if (mm.mainTexture != null) mm.mainTextureScale = new Vector2(Mathf.Max(1f, Mathf.Max(tam.x, tam.y) / 3f), 1f);
            g.GetComponent<Renderer>().sharedMaterial = mm;
        }

        Light LuzCastillo(Transform pa, Vector3 c, Color col, float rango, float inten)
        {
            var g = new GameObject("coop_luz_castillo"); g.transform.SetParent(pa, false); g.transform.localPosition = c;
            var l = g.AddComponent<Light>(); l.type = LightType.Point; l.color = col; l.range = rango; l.intensity = inten; l.shadows = LightShadows.None; return l;
        }

        void Farol2(Transform pa, Vector3 baseC)
        {
            Pz(pa, "coop_poste_base", PrimitiveType.Cube, baseC + Vector3.up * 0.4f, new Vector3(0.9f, 0.8f, 0.9f), 0f, matPiedra);
            Pz(pa, "coop_poste_moldura", PrimitiveType.Cube, baseC + Vector3.up * 0.86f, new Vector3(1.05f, 0.12f, 1.05f), 0f, matPiedra, false);
            Pz(pa, "coop_poste", PrimitiveType.Cylinder, baseC + Vector3.up * 2.1f, new Vector3(0.22f, 1.25f, 0.22f), 0f, matHierroC);
            Pz(pa, "coop_poste_anillo", PrimitiveType.Cylinder, baseC + Vector3.up * 1.2f, new Vector3(0.36f, 0.05f, 0.36f), 0f, matHierroC, false);
            Pz(pa, "coop_poste_anillo", PrimitiveType.Cylinder, baseC + Vector3.up * 3.25f, new Vector3(0.42f, 0.05f, 0.42f), 0f, matHierroC, false);
            // farol: llama dentro de una jaula de barrotes con cristales, tejadillo y remate
            Pz(pa, "coop_farol_mecha", PrimitiveType.Cylinder, baseC + Vector3.up * 3.33f, new Vector3(0.14f, 0.06f, 0.14f), 0f, matHierroC, false);   // platillo de aceite
            Llama.Crear(pa.TransformPoint(baseC + Vector3.up * 3.38f), pa, 0.78f * pa.lossyScale.x, false, false);   // llama de verdad dentro del farol (antes una bola amarilla)
            foreach (var cx in new[] { -0.3f, 0.3f }) foreach (var cz in new[] { -0.3f, 0.3f })
                Pz(pa, "coop_farol_barra", PrimitiveType.Cylinder, baseC + new Vector3(cx, 3.75f, cz), new Vector3(0.06f, 0.5f, 0.06f), 0f, matHierroC, false);
            Pz(pa, "coop_farol_fondo", PrimitiveType.Cube, baseC + Vector3.up * 3.22f, new Vector3(0.74f, 0.08f, 0.74f), 0f, matHierroC, false);
            Pz(pa, "coop_farol_marco", PrimitiveType.Cube, baseC + Vector3.up * 4.28f, new Vector3(0.74f, 0.08f, 0.74f), 0f, matHierroC, false);
            Cono(pa, "coop_farol_techo", baseC + Vector3.up * 4.32f, 0.62f, 0.5f);
            Pz(pa, "coop_farol_remate", PrimitiveType.Sphere, baseC + Vector3.up * 4.95f, new Vector3(0.14f, 0.14f, 0.14f), 0f, matHierroC, false);
            var l = LuzCastillo(pa, baseC + Vector3.up * 3.9f, new Color(1f, 0.6f, 0.25f), 17f, 2.5f);
            l.gameObject.AddComponent<Titilar>();
        }

        /// Estatua de caballero de piedra con espada.
        const bool SinEstatuas = true;   // pedido 2026-11-12: las estatuas quedaban mal
        void Estatua(Transform pa, Vector3 b, float giro)
        {
            if (SinEstatuas) return;
            var raiz = new GameObject("coop_estatua"); raiz.transform.SetParent(pa, false); raiz.transform.localPosition = b; raiz.transform.localRotation = Quaternion.Euler(0f, giro, 0f);
            var t = raiz.transform;
            Pz(t, "pedestal", PrimitiveType.Cube, new Vector3(0f, 0.8f, 0f), new Vector3(1.6f, 1.6f, 1.6f), 0f, matPiedra);
            Pz(t, "pedestal_tapa", PrimitiveType.Cube, new Vector3(0f, 1.7f, 0f), new Vector3(1.9f, 0.2f, 1.9f), 0f, matPiedra);
            Pz(t, "piernas", PrimitiveType.Cube, new Vector3(0f, 2.55f, 0f), new Vector3(0.75f, 1.5f, 0.45f), 0f, matPiedra);
            Pz(t, "torso", PrimitiveType.Cube, new Vector3(0f, 3.85f, 0f), new Vector3(1.1f, 1.4f, 0.6f), 0f, matPiedra);
            Pz(t, "cabeza", PrimitiveType.Sphere, new Vector3(0f, 4.85f, 0f), new Vector3(0.62f, 0.7f, 0.62f), 0f, matPiedra, false);
            Pz(t, "yelmo", PrimitiveType.Cube, new Vector3(0f, 5.1f, 0f), new Vector3(0.7f, 0.25f, 0.7f), 0f, matPiedra, false);
            Pz(t, "brazo_i", PrimitiveType.Cube, new Vector3(-0.75f, 3.85f, 0f), new Vector3(0.38f, 1.3f, 0.38f), 0f, matPiedra, false);
            Pz(t, "brazo_d", PrimitiveType.Cube, new Vector3(0.75f, 3.85f, 0.2f), new Vector3(0.38f, 1.3f, 0.38f), 0f, matPiedra, false);
            Pz(t, "espada", PrimitiveType.Cube, new Vector3(0.75f, 3.3f, 0.55f), new Vector3(0.12f, 3.2f, 0.12f), 0f, matHierroC, false);
            Pz(t, "guarda", PrimitiveType.Cube, new Vector3(0.75f, 4.15f, 0.55f), new Vector3(0.8f, 0.12f, 0.12f), 0f, matHierroC, false);
        }

        // ---------------- Construcción ----------------

        void ConstruirCastillo(GameObject terreno, TerrainData dn)
        {
            PrepararMaterialesCastillo();
            float h = dn.GetInterpolatedHeight(CastilloX / AnchoNuevo, CastilloZ / FondoNuevo);
            castilloSuelo = terreno.transform.position + new Vector3(CastilloX, h, CastilloZ);
            var raiz = new GameObject("coop_castillo"); raiz.transform.SetParent(terreno.transform, true); raiz.transform.position = castilloSuelo; raiz.transform.localScale = Vector3.one * CastilloEscala;
            castilloRaiz = raiz; var pa = raiz.transform;
            var rng = new System.Random(4242);

            // plataforma, escalinata con muros y faroles
            // Podio en piezas (pedido 2026-11-17): su parte del fondo (z 44–50) entraba en la Torre del Homenaje y tapaba la
            // escalera de bajada a la bóveda (hueco x −10,5…−2). Se deja sin podio solo ese rincón.
            // (2026-11-19) el hueco llegaba solo hasta x −2 y la escalera del sótano y su muro llegan hasta x −0,4: quedaba un trozo de
            // podio de 1,6 m metido en la escalera (se veía como un bloque de losas que le arruinaba la forma). Ahora el borde va en x 0.
            foreach (var t in new[] { new[] { -29f, 29f, -6f, 44f }, new[] { -29f, -10.5f, 44f, 50f }, new[] { 0f, 29f, 44f, 50f }, new[] { -10.5f, 0f, 44f, 45.6f } })
            {
                float ex = t[0] == -29f ? -0.7f : 0f, ex1 = t[1] == 29f ? 0.7f : 0f, ez0 = t[2] == -6f ? -0.7f : 0f, ez1 = t[3] == 50f ? 0.7f : 0f;
                Pz(pa, "coop_podio", PrimitiveType.Cube, new Vector3((t[0] + t[1]) / 2f, 1.5f, (t[2] + t[3]) / 2f), new Vector3(t[1] - t[0], 3f, t[3] - t[2]), 0f, matPiedra);
                Pz(pa, "coop_podio_moldura", PrimitiveType.Cube, new Vector3((t[0] + ex + t[1] + ex1) / 2f, 3.1f, (t[2] + ez0 + t[3] + ez1) / 2f), new Vector3(t[1] + ex1 - t[0] - ex, 0.4f, t[3] + ez1 - t[2] - ez0), 0f, matPiedra, false);
            }
            for (int i = 0; i < 8; i++)
            {
                float hi = 3f * (i + 1) / 8f, zi = -14f + i + 0.5f;
                Pz(pa, "coop_escalon", PrimitiveType.Cube, new Vector3(0f, hi / 2f, zi), new Vector3(9f, hi, 1f), 0f, matPiedra);
                foreach (var sx in new[] { -5.3f, 5.3f }) Pz(pa, "coop_muro_escalera", PrimitiveType.Cube, new Vector3(sx, (hi + 0.9f) / 2f, zi), new Vector3(1.2f, hi + 0.9f, 1f), 0f, matPiedra);
            }
            foreach (var sx in new[] { -6.4f, 6.4f }) Farol2(pa, new Vector3(sx, 0f, -15f));
            // leones/gárgolas en los pilares de la escalinata (cono hacia adelante)
            foreach (var sx in new[] { -5.3f, 5.3f }) { Pz(pa, "coop_pilar", PrimitiveType.Cube, new Vector3(sx, 4.2f, -14.5f), new Vector3(1.6f, 3f, 1.6f), 0f, matPiedra); PzR(pa, "coop_gargola_pilar", PrimitiveType.Cube, new Vector3(sx, 6f, -14.5f), new Vector3(0.9f, 0.9f, 1.4f), Quaternion.Euler(20f, 0f, 0f), matPiedra); }

            // salón (el torreón principal): paredes con la puerta de 6 × 8 m, techo y almenas con matacanes
            float tz = 2f;
            Pz(pa, "coop_muro_frente_izq", PrimitiveType.Cube, new Vector3(-11.5f, 14f, tz + 0.8f), new Vector3(17f, 22f, 1.6f), 0f, matPiedra);
            Pz(pa, "coop_muro_frente_der", PrimitiveType.Cube, new Vector3(11.5f, 14f, tz + 0.8f), new Vector3(17f, 22f, 1.6f), 0f, matPiedra);
            Pz(pa, "coop_dintel", PrimitiveType.Cube, new Vector3(0f, 18f, tz + 0.8f), new Vector3(6f, 14f, 1.6f), 0f, matPiedra);
            // (la pared "coop_muro_izq" ahora la hace ConstruirInterior, con sus puertas abiertas)
            // (la pared "coop_muro_der" ahora la hace ConstruirInterior, con sus puertas abiertas)
            // (la pared "coop_muro_fondo" ahora la hace ConstruirInterior, con sus puertas abiertas)
            Pz(pa, "coop_techo_salon", PrimitiveType.Cube, new Vector3(0f, 17.6f, 20f), new Vector3(38.4f, 1.2f, 35f), 0f, matPiedra);
            Pz(pa, "coop_azotea", PrimitiveType.Cube, new Vector3(0f, 25.1f, 20f), new Vector3(40f, 0.6f, 36f), 0f, matPiedra);
            // moldura que recorre la fachada a media altura
            Pz(pa, "coop_moldura", PrimitiveType.Cube, new Vector3(0f, 12f, tz - 0.15f), new Vector3(40.6f, 0.45f, 0.5f), 0f, matPiedra, false);
            Pz(pa, "coop_moldura", PrimitiveType.Cube, new Vector3(0f, 23.6f, tz - 0.2f), new Vector3(41f, 0.6f, 0.7f), 0f, matPiedra, false);
            // portal gótico monumental (pedido 2026-11-17; antes: dos vigas en X, rastrillo simple y un rosetón tapado por su propio aro)
            PortalCastillo(pa, tz);
            // matacanes (ménsulas) y almenas
            for (float x = -19f; x <= 19f; x += 2f) Pz(pa, "coop_mensula", PrimitiveType.Cube, new Vector3(x, 24.3f, tz - 0.55f), new Vector3(0.8f, 1.1f, 1.2f), 0f, matPiedra, false);
            for (float x = -19f; x <= 19f; x += 4f) { Pz(pa, "coop_almena", PrimitiveType.Cube, new Vector3(x, 26.4f, tz + 0.2f), new Vector3(2f, 2f, 1.3f), 0f, matPiedra); Pz(pa, "coop_almena", PrimitiveType.Cube, new Vector3(x, 26.4f, 37.7f), new Vector3(2f, 2f, 1.3f), 0f, matPiedra); }
            for (float z = 6f; z <= 34f; z += 4f) { Pz(pa, "coop_almena", PrimitiveType.Cube, new Vector3(-19.7f, 26.4f, z), new Vector3(1.3f, 2f, 2f), 0f, matPiedra); Pz(pa, "coop_almena", PrimitiveType.Cube, new Vector3(19.7f, 26.4f, z), new Vector3(1.3f, 2f, 2f), 0f, matPiedra); }
            // contrafuertes en los flancos
            foreach (var sx in new[] { -1f, 1f }) for (float z = 8f; z <= 34f; z += 8.5f)
            {
                if (Mathf.Abs(z - 25f) < 1f) continue;   // ahí pasa el corredor sur hacia el ala
                Pz(pa, "coop_contrafuerte", PrimitiveType.Cube, new Vector3(sx * 21.2f, 7.5f, z), new Vector3(2.4f, 9f, 2.2f), 0f, matPiedra);
                Pz(pa, "coop_contrafuerte", PrimitiveType.Cube, new Vector3(sx * 20.7f, 15f, z), new Vector3(1.4f, 8f, 1.6f), 0f, matPiedra);
                PzR(pa, "coop_contrafuerte_rampa", PrimitiveType.Cube, new Vector3(sx * 20.3f, 20.5f, z), new Vector3(0.9f, 4f, 1.2f), Quaternion.Euler(0f, 0f, sx * 12f), matPiedra);
            }
            // garitas (bartizanas) en las esquinas del torreón y gárgolas
            foreach (var sx in new[] { -1f, 1f }) foreach (var zz in new[] { 2f, 38f })
            {
                Pz(pa, "coop_garita", PrimitiveType.Cylinder, new Vector3(sx * 19.5f, 24f, zz), new Vector3(3.4f, 2.6f, 3.4f), 0f, matPiedra);
                Cono(pa, "coop_garita_apoyo", new Vector3(sx * 19.5f, 21.2f, zz), 1.8f, 2.2f, true);
                Cono(pa, "coop_garita_techo", new Vector3(sx * 19.5f, 26.7f, zz), 2.3f, 3.6f);
            }
            foreach (var sx in new[] { -1f, 1f }) { PzR(pa, "coop_gargola", PrimitiveType.Cube, new Vector3(sx * 12f, 22f, tz - 1.1f), new Vector3(0.7f, 0.7f, 2.4f), Quaternion.Euler(-24f, 0f, 0f), matPiedra); PzR(pa, "coop_gargola_ala", PrimitiveType.Cube, new Vector3(sx * 12.8f, 22.5f, tz - 0.6f), new Vector3(1.4f, 0.12f, 0.9f), Quaternion.Euler(0f, 0f, sx * 20f), matPiedra); }

            // torres (techo puntiagudo, cruz y banderas)
            Torre(pa, "coop_torre_frente_izq", -22f, 3f, 0f, 40f, 5.5f, 17f);
            Torre(pa, "coop_torre_frente_der", 22f, 3f, 0f, 40f, 5.5f, 17f);
            Torre(pa, "coop_torre_fondo_izq", -22f, 36f, 0f, 36f, 4.6f, 14f);
            Torre(pa, "coop_torre_fondo_der", 22f, 36f, 0f, 36f, 4.6f, 14f);
            Torre(pa, "coop_torre_media_izq", -23f, 20f, 0f, 31f, 3.6f, 12f);
            Torre(pa, "coop_torre_media_der", 23f, 20f, 0f, 31f, 3.6f, 12f);
            // alas laterales con almenas y torrecillas
            foreach (var sx in new[] { -1f, 1f })
            {
                Pz(pa, "coop_ala_moldura", PrimitiveType.Cube, new Vector3(sx * 40f, 18.2f, 18f), new Vector3(17f, 0.6f, 29f), 0f, matPiedra, false);
                for (float z = 5f; z <= 31f; z += 4f) { Pz(pa, "coop_almena", PrimitiveType.Cube, new Vector3(sx * 40f, 19.4f, z), new Vector3(2f, 2f, 1.6f), 0f, matPiedra); }
                Torre(pa, "coop_torrecilla", sx * 50.85f, 6f, 0f, 25f, 3f, 10f);   // por fuera del ala (antes se metía dentro de la sala y tapaba la escalera)
                Torre(pa, "coop_torrecilla_fondo", sx * 50.45f, 30f, 0f, 22f, 2.6f, 9f);
                foreach (var z in new[] { 9f, 18f, 27f }) foreach (var y in new[] { 7f, 13f }) if (!(z == 18f && y == 7f)) VentanaOjival(pa, new Vector3(sx * 48.05f, y, z), new Vector3(sx, 0f, 0f), 1.2f, 2.6f, 0.8f);   // en z 18 abajo está la puerta al patio (la ventana flotaba en el hueco)
                foreach (var x in new[] { sx * 34f, sx * 40f }) foreach (var y in new[] { 7f, 13f }) VentanaOjival(pa, new Vector3(x, y, 3.9f), Vector3.back, 1.2f, 2.6f, 0.8f);
            }

            // ventanas ojivales encendidas
            // en x = ±9 solo la ventana de abajo: más arriba cuelgan los estandartes del portal (antes se montaban encima de las ventanas)
            foreach (var x in new[] { -14.5f, -9f, 9f, 14.5f }) foreach (var y in new[] { 7.5f, 14f }) if (Mathf.Abs(x) > 10f || y < 10f) VentanaOjival(pa, new Vector3(x, y, tz - 0.0f), Vector3.back, 1.4f, 3.2f, 1.0f);
            foreach (var x in new[] { -14.5f, 14.5f }) VentanaOjival(pa, new Vector3(x, 19.5f, tz - 0.0f), Vector3.back, 1.2f, 2.4f, 0.8f);
            foreach (var sx in new[] { -1f, 1f }) foreach (var z in new[] { 10f, 18f, 26f, 33f }) foreach (var y in new[] { 8f, 15f }) if (!(y < 10f && (z == 10f || z == 26f))) VentanaOjival(pa, new Vector3(sx * 20.45f, y, z), new Vector3(sx, 0f, 0f), 1.4f, 3.2f, 1.0f);
            // (quitadas las 3 ventanas de la antigua torre central: la torre ya no existe y flotaban en el cielo)
            LuzCastillo(pa, new Vector3(-9f, 10f, -3f), new Color(1f, 0.6f, 0.3f), 15f, 1.3f);
            LuzCastillo(pa, new Vector3(9f, 10f, -3f), new Color(1f, 0.6f, 0.3f), 15f, 1.3f);
            LuzCastillo(pa, new Vector3(0f, 6f, -8f), new Color(0.9f, 0.55f, 0.3f), 18f, 1.2f);

            // interior del salón: columnas con capiteles, antorchas, estandartes, mesas, candelabros, chimenea y trono
            for (int i = 0; i < 4; i++) foreach (var sx in new[] { -8.5f, 8.5f })
            {
                float z = 9f + i * 8f;
                Pz(pa, "coop_columna", PrimitiveType.Cylinder, new Vector3(sx, 10f, z), new Vector3(1.8f, 7f, 1.8f), 0f, matPiedra);
                Pz(pa, "coop_columna_base", PrimitiveType.Cube, new Vector3(sx, 3.4f, z), new Vector3(2.6f, 0.7f, 2.6f), 0f, matPiedra, false);
                Pz(pa, "coop_capitel", PrimitiveType.Cube, new Vector3(sx, 16.6f, z), new Vector3(2.8f, 0.9f, 2.8f), 0f, matPiedra, false);
                Pz(pa, "coop_antorcha_soporte", PrimitiveType.Cube, new Vector3(sx * 0.8f, 6.1f, z), new Vector3(0.18f, 0.9f, 0.18f), 0f, matHierroC, false);
                Llama.Crear(pa.TransformPoint(new Vector3(sx * 0.8f, 6.55f, z)), pa, 0.45f * pa.lossyScale.x, true, true);
                LuzCastillo(pa, new Vector3(sx * 0.8f, 7f, z), new Color(1f, 0.55f, 0.22f), 12f, 1.7f).gameObject.AddComponent<Titilar>();
            }
            Pz(pa, "coop_suelo_salon", PrimitiveType.Cube, new Vector3(0f, 3.05f, 20f), new Vector3(37f, 0.1f, 34f), 0f, matAdoquin, true, 3f);
            Pz(pa, "coop_alfombra", PrimitiveType.Cube, new Vector3(0f, 3.12f, 17f), new Vector3(4f, 0.05f, 26f), 0f, matTela, false);
            Pz(pa, "coop_estrado", PrimitiveType.Cube, new Vector3(0f, 3.5f, 33f), new Vector3(12f, 1f, 6f), 0f, matPiedra);
            Pz(pa, "coop_estrado2", PrimitiveType.Cube, new Vector3(0f, 4.2f, 34.2f), new Vector3(6f, 0.8f, 3.4f), 0f, matPiedra);
            Pz(pa, "coop_trono_asiento", PrimitiveType.Cube, new Vector3(0f, 5.2f, 34.6f), new Vector3(2f, 1.1f, 1.8f), 0f, matMadera2);
            Pz(pa, "coop_trono_respaldo", PrimitiveType.Cube, new Vector3(0f, 7.9f, 35.7f), new Vector3(2.4f, 5.5f, 0.5f), 0f, matMadera2);
            Pz(pa, "coop_trono_pico", PrimitiveType.Cube, new Vector3(-0.9f, 10.9f, 35.7f), new Vector3(0.35f, 1.4f, 0.4f), 0f, matMadera2, false);
            Pz(pa, "coop_trono_pico", PrimitiveType.Cube, new Vector3(0.9f, 10.9f, 35.7f), new Vector3(0.35f, 1.4f, 0.4f), 0f, matMadera2, false);
            foreach (var sx in new[] { -1f, 1f }) foreach (var z in new[] { 10f, 20f, 30f }) Pz(pa, "coop_estandarte", PrimitiveType.Cube, new Vector3(sx * 18.3f, 10f, z), new Vector3(0.08f, 6.4f, 2.4f), 0f, matTela, false);
            foreach (var sx in new[] { -5.2f, 5.2f })
            {
                Pz(pa, "coop_mesa", PrimitiveType.Cube, new Vector3(sx, 4.35f, 20f), new Vector3(2f, 0.16f, 17f), 0f, matMadera2);
                foreach (var z in new[] { 12.5f, 20f, 27.5f }) { Pz(pa, "coop_mesa_pata", PrimitiveType.Cube, new Vector3(sx - 0.8f, 3.7f, z), new Vector3(0.18f, 1.2f, 0.18f), 0f, matMadera2); Pz(pa, "coop_mesa_pata", PrimitiveType.Cube, new Vector3(sx + 0.8f, 3.7f, z), new Vector3(0.18f, 1.2f, 0.18f), 0f, matMadera2); }
                Pz(pa, "coop_banco", PrimitiveType.Cube, new Vector3(sx * 1.55f, 3.7f, 20f), new Vector3(0.6f, 0.14f, 16f), 0f, matMadera2, false);
                for (float z = 13f; z < 28f; z += 3.2f) LuzVela(pa, new Vector3(sx, 4.43f, z));
            }
            foreach (var z in new[] { 12f, 22f, 31f }) Candelabro(pa, new Vector3(0f, 12.5f, z));
            // chimenea con fuego al fondo del salón
            Pz(pa, "coop_chimenea", PrimitiveType.Cube, new Vector3(-13.5f, 7f, 36.2f), new Vector3(6f, 7.5f, 1.4f), 0f, matPiedra);
            Pz(pa, "coop_chimenea_hueco", PrimitiveType.Cube, new Vector3(-13.5f, 4.6f, 35.4f), new Vector3(3.2f, 2.8f, 0.5f), 0f, matHierroC, false);
            Fogata.Crear(raiz.transform.TransformPoint(new Vector3(-13.5f, 3.15f, 35.2f)), pa);
            LuzCastillo(pa, new Vector3(0f, 12f, 12f), new Color(0.8f, 0.5f, 0.35f), 22f, 1.0f);
            LuzCastillo(pa, new Vector3(0f, 12f, 28f), new Color(0.7f, 0.4f, 0.55f), 22f, 1.0f);

            // camino de adoquines hasta la escalinata, muros derruidos y estatuas de caballeros
            Pz(pa, "coop_camino", PrimitiveType.Cube, new Vector3(0f, 0.05f, -22f), new Vector3(26f, 0.1f, 15f), 0f, matAdoquin, true, 3.5f);   // explanada de roca ante la escalinata
            for (int i = 0; i < 6; i++)
            {
                float z = -17f - i * 2.1f - (float)rng.NextDouble() * 1.2f, x = (rng.NextDouble() < 0.5 ? -1f : 1f) * (6.8f + (float)rng.NextDouble() * 1.2f);
                float al = 0.4f + (float)rng.NextDouble() * (i % 3 == 0 ? 1.4f : 0.6f);
                Pz(pa, "coop_muro_derruido", PrimitiveType.Cube, new Vector3(x, al / 2f, z), new Vector3(1f, al, 2.5f + (float)rng.NextDouble() * 3f), (float)rng.NextDouble() * 12f - 6f, matPiedra);
            }
            for (int i = 0; i < 1; i++) foreach (var sx in new[] { -5.6f, 5.6f }) if ((i + (sx > 0 ? 1 : 0)) % 2 == 0) Farol2(pa, new Vector3(sx, 0f, -26f - i * 28f));
            for (int i = 0; i < 1; i++) foreach (var sx in new[] { -1f, 1f }) Estatua(pa, new Vector3(sx * 5.9f, 0f, -27f - i * 12f), sx > 0 ? 90f : -90f);
            // luces azuladas bajas para que las torres se lean de noche
            foreach (var p in new[] { new Vector3(-30f, 2f, -4f), new Vector3(30f, 2f, -4f), new Vector3(-52f, 2f, 20f), new Vector3(52f, 2f, 20f), new Vector3(-30f, 2f, 48f), new Vector3(30f, 2f, 48f), new Vector3(0f, 2f, 56f) })
                LuzCastillo(pa, p, new Color(0.38f, 0.5f, 0.85f), 55f, 1.5f);

            // lo embrujado: niebla baja que se arrastra sobre el foso y luces fantasmales
            var gn = new GameObject("coop_niebla_castillo"); gn.transform.SetParent(pa, false); gn.transform.localPosition = new Vector3(0f, 1.2f, 22f);
            var ps = gn.AddComponent<ParticleSystem>();
            var main = ps.main; main.loop = true; main.startLifetime = 14f; main.startSpeed = 0.25f; main.startSize = new ParticleSystem.MinMaxCurve(14f, 26f);
            main.startColor = new Color(0.42f, 0.5f, 0.6f, 0.17f); main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = 260; main.prewarm = true;
            var em = ps.emission; em.rateOverTime = 16f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.box = new Vector3(120f, 2f, 120f);
            var cl = ps.colorOverLifetime; cl.enabled = true; var gr = new Gradient();
            gr.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            cl.color = gr;
            var rr = gn.GetComponent<ParticleSystemRenderer>(); rr.material = Potenciado.MaterialGas(); rr.renderMode = ParticleSystemRenderMode.Billboard; rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            for (int i = 0; i < 8; i++)
            {
                var e = new GameObject("coop_espectro"); e.transform.SetParent(pa, false);
                float a = i * Mathf.PI * 2f / 8f; e.transform.localPosition = new Vector3(Mathf.Cos(a) * (34f + 12f * (i % 3)), 4f, 18f + Mathf.Sin(a) * 50f);
                var l = e.AddComponent<Light>(); l.type = LightType.Point; l.color = i % 2 == 0 ? new Color(0.3f, 1f, 0.8f) : new Color(0.5f, 0.55f, 1f); l.range = 13f; l.intensity = 1.4f; l.shadows = LightShadows.None;
                e.AddComponent<EspectroFlotante>().Fase = i * 1.3f;
            }
            ConstruirMuralla(pa); ConstruirAzotea(pa);
            try { ConstruirInterior(pa); } catch (System.Exception e) { plugin.Log("Castillo: error en el interior " + e); }
            CombinarCastillo(pa);
            try { ConstruirLaberinto(terreno, dn); } catch (System.Exception e) { plugin.Log("Laberinto: error " + e); }
            plugin.Log("Castillo: construido en " + (castilloSuelo - Ancla()).ToString("F0") + " (torreón con portón ojival y rosetón, 7 torres, 2 alas, ventanas ojivales, salón con mesas, candelabros, chimenea y trono; estatuas, niebla) sobre la montaña");
        }

        void LuzVela(Transform pa, Vector3 pos)
        {
            Pz(pa, "coop_vela", PrimitiveType.Cylinder, pos + Vector3.up * 0.15f, new Vector3(0.1f, 0.15f, 0.1f), 0f, matPiedra, false);
            Llama.Crear(pa.TransformPoint(pos + Vector3.up * 0.31f), pa, 0.1f * pa.lossyScale.x, false, false);
            LuzCastillo(pa, pos + Vector3.up * 0.55f, new Color(1f, 0.62f, 0.3f), 4.5f, 0.7f);
        }

        void Candelabro(Transform pa, Vector3 c)
        {
            Pz(pa, "coop_cand_aro", PrimitiveType.Cylinder, c, new Vector3(3.2f, 0.07f, 3.2f), 0f, matHierroC, false);
            for (int i = 0; i < 4; i++) { float a = i * Mathf.PI / 2f + 0.4f; Pz(pa, "coop_cadena", PrimitiveType.Cylinder, c + new Vector3(Mathf.Cos(a) * 1.5f * 0.55f, 2.4f, Mathf.Sin(a) * 1.5f * 0.55f), new Vector3(0.06f, 2.4f, 0.06f), 0f, matHierroC, false); }
            for (int i = 0; i < 8; i++) { float a = i * Mathf.PI / 4f; var p = c + new Vector3(Mathf.Cos(a) * 1.5f, 0.12f, Mathf.Sin(a) * 1.5f); Pz(pa, "coop_cand_vela", PrimitiveType.Cylinder, p + Vector3.up * 0.2f, new Vector3(0.1f, 0.2f, 0.1f), 0f, matPiedra, false); Llama.Crear(pa.TransformPoint(p + Vector3.up * 0.42f), pa, 0.09f * pa.lossyScale.x, false, false); }
            LuzCastillo(pa, c + Vector3.up * 0.8f, new Color(1f, 0.6f, 0.3f), 15f, 1.4f).gameObject.AddComponent<Titilar>();
        }

        void QuitarCastillo() { castilloRaiz = null; QuitarInterior(); QuitarTesoro(); if (caminoZonaNueva != null) Object.Destroy(caminoZonaNueva); caminoZonaNueva = null; }
    }

    /// Luz fantasma que flota y se mueve despacio.
    public class EspectroFlotante : MonoBehaviour
    {
        public float Fase; Vector3 origen; Light l;
        void Start()
        {
            origen = transform.localPosition; l = GetComponent<Light>();
            // orbe visible: sin él la luz flotaba "de la nada" (pedido 2026-11-17)
            var ps = gameObject.AddComponent<ParticleSystem>();
            var main = ps.main; main.loop = true; main.startLifetime = 1.4f; main.startSpeed = 0.05f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
            Color c = l != null ? l.color : new Color(0.4f, 1f, 0.8f); c.a = 0.75f; main.startColor = c;
            main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = 40;
            var em = ps.emission; em.rateOverTime = 16f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.12f;
            var cl = ps.colorOverLifetime; cl.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(c, 0.4f), new GradientColorKey(c, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.85f, 0.15f), new GradientAlphaKey(0f, 1f) });
            cl.color = g;
            var rr = GetComponent<ParticleSystemRenderer>();
            rr.material = Potenciado.MaterialGas(); rr.renderMode = ParticleSystemRenderMode.Billboard; rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        void Update()
        {
            float t = Time.time * 0.35f + Fase;
            transform.localPosition = origen + new Vector3(Mathf.Sin(t) * 6f, Mathf.Sin(t * 1.7f) * 1.6f, Mathf.Cos(t * 0.8f) * 6f);
            if (l != null) l.intensity = 1.0f + Mathf.Sin(t * 3.1f) * 0.5f;
        }
    }

    /// Luz que titila como una llama.
    public class Titilar : MonoBehaviour
    {
        Light l; float baseI, f;
        void Start() { l = GetComponent<Light>(); baseI = l != null ? l.intensity : 1f; f = Random.value * 10f; }
        void Update() { if (l == null) return; f += Time.deltaTime * 8f; l.intensity = baseI * (0.88f + Mathf.Sin(f) * 0.08f + Mathf.Sin(f * 2.3f) * 0.05f); }
    }

    /// Bandera que ondea (balanceo suave).
    public class Ondear : MonoBehaviour
    {
        public float Fase; Quaternion q0;
        void Start() { q0 = transform.localRotation; }
        void Update() { float t = Time.time * 1.6f + Fase; transform.localRotation = q0 * Quaternion.Euler(0f, Mathf.Sin(t) * 14f, Mathf.Sin(t * 1.7f) * 4f); }
    }
}

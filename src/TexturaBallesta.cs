using System;
using UnityEngine;

namespace WitchHuntCoop
{
    /// Texturas propias de la ballesta (atlas 2048×2048): la madera y el hierro salen de la textura REAL del mosquete (sintetizadas para que no se repitan),
    /// más cuerda, canal y plumas hechos aquí. Además genera un mapa de relieve (normales) y uno de metal/brillo (brillo en el canal alfa).
    /// Disposición: madera a la izquierda (U 0–0,75; V 0–0,625; veta a lo largo de U), hierro, canal, plumas y cuerda a la derecha.
    public static class TexturaBallesta
    {
        public const int T = 2048;
        public const float MaderaU1 = 0.75f, MaderaV1 = 0.625f;
        public static readonly Vector2 MaderaLo = new Vector2(0.004f, 0.004f), MaderaHi = new Vector2(MaderaU1 - 0.004f, MaderaV1 - 0.004f);
        public static readonly Vector2 HierroLo = new Vector2(0.765f, 0.012f), HierroHi = new Vector2(0.985f, 0.50f);
        public static readonly Vector2 CanalLo = new Vector2(0.78f, 0.515f), CanalHi = new Vector2(0.97f, 0.545f);
        public static readonly Vector2 PlumaLo = new Vector2(0.78f, 0.555f), PlumaHi = new Vector2(0.97f, 0.585f);
        public static readonly Vector2 CinchaLo = new Vector2(0.78f, 0.960f), CinchaHi = new Vector2(0.97f, 0.990f);
        public static readonly Vector2 CuerdaLo = new Vector2(0.78f, 0.62f), CuerdaHi = new Vector2(0.97f, 0.95f);

        /// Parche de madera principal de la textura del mosquete (lo usa la transición culata→ballesta, que va con el material original).
        public static Parche ParcheA;
        public class Juego { public Texture2D Color, Normal, MetalBrillo; }

        // ---------------------------------------------------------------- lectura de la textura del mosquete
        static Texture2D cacheOrigen; static Texture cacheDe;
        /// Copia legible de la textura del mosquete (se guarda para no leerla dos veces).
        public static Texture2D LeerOrigen(Texture tex)
        {
            if (tex != null && cacheDe == tex && cacheOrigen != null) return cacheOrigen;
            var t = LeerCopia(tex); cacheOrigen = t; cacheDe = tex; return t;
        }
        static Texture2D LeerCopia(Texture tex)
        {
            if (tex == null) return null;
            int w = Mathf.Min(tex.width, 2048), h = Mathf.Min(tex.height, 2048);
            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var antes = RenderTexture.active;
            Graphics.Blit(tex, rt); RenderTexture.active = rt;
            var t2 = new Texture2D(w, h, TextureFormat.RGB24, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            t2.ReadPixels(new Rect(0, 0, w, h), 0, 0); t2.Apply();
            RenderTexture.active = antes; RenderTexture.ReleaseTemporary(rt);
            return t2;
        }
        static float Espejo(float t) { t = t - 2f * Mathf.Floor(t / 2f); return t <= 1f ? t : 2f - t; }

        public struct Parche { public Vector2 Lo, Hi; public bool VetaU; public bool Ok; }

        /// Color del parche repetido en espejo y agrandado "k" veces; gx = píxel a lo largo de la veta, cy = píxel a lo ancho.
        static Color Muestra(Texture2D src, Parche p, float gx, float cy, float k)
        {
            float gw = p.VetaU ? (p.Hi.x - p.Lo.x) * src.width : (p.Hi.y - p.Lo.y) * src.height, cw = p.VetaU ? (p.Hi.y - p.Lo.y) * src.height : (p.Hi.x - p.Lo.x) * src.width;
            gw = Mathf.Max(8f, gw); cw = Mathf.Max(8f, cw);
            float gt = Espejo(gx / (gw * k)), ct = Espejo(cy / (cw * k));
            return p.VetaU ? src.GetPixelBilinear(p.Lo.x + gt * (p.Hi.x - p.Lo.x), p.Lo.y + ct * (p.Hi.y - p.Lo.y)) : src.GetPixelBilinear(p.Lo.x + ct * (p.Hi.x - p.Lo.x), p.Lo.y + gt * (p.Hi.y - p.Lo.y));
        }

        public static bool EsMadera(Color c) { return c.r > c.b * 1.5f && c.r >= c.g * 1.08f && c.g >= c.b * 0.95f && c.r < 0.88f && (c.r - c.b) / Mathf.Max(0.01f, c.r) > 0.34f && c.g <= c.r * 0.76f; }
        static bool EsHierro(Color c)
        {
            float mx = Mathf.Max(c.r, Mathf.Max(c.g, c.b)), mn = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
            return (mx - mn) / Mathf.Max(0.01f, mx) < 0.16f && mx > 0.26f && mx < 0.66f;
        }

        /// Rectángulo más grande de la textura cuyo contenido es todo del material pedido (y de color parecido a "referencia", si hay).
        static Parche BuscarParche(Texture2D src, Func<Color, bool> es, Color? referencia, Parche? excluir, Action<string> log, string nombre)
        {
            const int N = 128;
            var ok = new bool[N, N]; var pr = new float[N + 1, N + 1, 3]; var col = new Color[N, N];
            for (int j = 0; j < N; j++) for (int i = 0; i < N; i++)
            {
                col[i, j] = src.GetPixelBilinear((i + 0.5f) / N, (j + 0.5f) / N); ok[i, j] = es(col[i, j]);
                if (excluir.HasValue && excluir.Value.Ok)
                {
                    float u = (i + 0.5f) / N, v = (j + 0.5f) / N, mx = 0.02f;
                    if (u > excluir.Value.Lo.x - mx && u < excluir.Value.Hi.x + mx && v > excluir.Value.Lo.y - mx && v < excluir.Value.Hi.y + mx) ok[i, j] = false;
                }
            }
            for (int j = 0; j < N; j++) for (int i = 0; i < N; i++)
            {
                var c = ok[i, j] ? col[i, j] : Color.black;
                pr[i + 1, j + 1, 0] = c.r + pr[i, j + 1, 0] + pr[i + 1, j, 0] - pr[i, j, 0];
                pr[i + 1, j + 1, 1] = c.g + pr[i, j + 1, 1] + pr[i + 1, j, 1] - pr[i, j, 1];
                pr[i + 1, j + 1, 2] = c.b + pr[i, j + 1, 2] + pr[i + 1, j, 2] - pr[i, j, 2];
            }
            var h = new int[N]; float mejor = 0f; int bx0 = 0, bx1 = 0, by0 = 0, by1 = 0; var pila = new int[N + 1];
            for (int j = 0; j < N; j++)
            {
                for (int i = 0; i < N; i++) h[i] = ok[i, j] ? h[i] + 1 : 0;
                int sp = 0;
                for (int i = 0; i <= N; i++)
                {
                    int hh = i < N ? h[i] : 0;
                    while (sp > 0 && h[pila[sp - 1]] >= hh)
                    {
                        int alto = h[pila[--sp]]; int izq = sp == 0 ? 0 : pila[sp - 1] + 1; int ancho = i - izq;
                        if (alto >= 5 && ancho >= 5)
                        {
                            int x0 = izq, x1 = i, y0 = j - alto + 1, y1 = j + 1; float area = (x1 - x0) * (y1 - y0), score = area;
                            if (referencia.HasValue)
                            {
                                float r = (pr[x1, y1, 0] - pr[x0, y1, 0] - pr[x1, y0, 0] + pr[x0, y0, 0]) / area, g = (pr[x1, y1, 1] - pr[x0, y1, 1] - pr[x1, y0, 1] + pr[x0, y0, 1]) / area, b = (pr[x1, y1, 2] - pr[x0, y1, 2] - pr[x1, y0, 2] + pr[x0, y0, 2]) / area;
                                var rf = referencia.Value; float dd = Mathf.Sqrt((r - rf.r) * (r - rf.r) + (g - rf.g) * (g - rf.g) + (b - rf.b) * (b - rf.b));
                                score = area * Mathf.Clamp01(1f - dd / 0.18f);
                            }
                            if (score > mejor) { mejor = score; bx0 = x0; bx1 = x1; by0 = y0; by1 = y1; }
                        }
                    }
                    if (i < N) pila[sp++] = i;
                }
            }
            var p = new Parche();
            if (mejor <= 0f) { if (log != null) log("parche de " + nombre + ": no encontrado"); return p; }
            float mgx = (bx1 - bx0) * 0.20f / 2f, mgy = (by1 - by0) * 0.20f / 2f;
            p.Lo = new Vector2((bx0 + mgx) / N, (by0 + mgy) / N); p.Hi = new Vector2((bx1 - mgx) / N, (by1 - mgy) / N); p.Ok = true;
            float gu = 0f, gv = 0f;
            for (int k = 0; k < 400; k++)
            {
                float u = Mathf.Lerp(p.Lo.x, p.Hi.x, (k * 0.618034f) % 1f), v = Mathf.Lerp(p.Lo.y, p.Hi.y, (k * 0.414214f) % 1f); float e = 1.5f / src.width;
                var c0 = src.GetPixelBilinear(u, v); var cu = src.GetPixelBilinear(u + e, v); var cv = src.GetPixelBilinear(u, v + e);
                gu += Mathf.Abs(cu.r - c0.r) + Mathf.Abs(cu.g - c0.g) + Mathf.Abs(cu.b - c0.b); gv += Mathf.Abs(cv.r - c0.r) + Mathf.Abs(cv.g - c0.g) + Mathf.Abs(cv.b - c0.b);
            }
            p.VetaU = gu < gv;
            if (log != null) log("parche de " + nombre + ": " + p.Lo.ToString("F3") + "–" + p.Hi.ToString("F3") + " (" + (bx1 - bx0) + "×" + (by1 - by0) + " celdas), veta en " + (p.VetaU ? "U" : "V"));
            return p;
        }

        static float Ruido(float x, float y) { return Mathf.PerlinNoise(x, y); }

        /// Perfil de color a lo ancho de la veta: la MEDIANA a lo largo de la veta en cada fila (así desaparecen tornillos, bordes y otros detalles sueltos).
        static Color[] Perfil(Texture2D src, Parche p)
        {
            int n = Mathf.Max(32, (int)(p.VetaU ? (p.Hi.y - p.Lo.y) * src.height : (p.Hi.x - p.Lo.x) * src.width));
            var res = new Color[n]; const int m = 33; var r = new float[m]; var g = new float[m]; var b = new float[m];
            for (int k = 0; k < n; k++)
            {
                float ct = k / (n - 1f);
                for (int j = 0; j < m; j++)
                {
                    float gt = (j + 0.5f) / m;
                    var c = p.VetaU ? src.GetPixelBilinear(p.Lo.x + gt * (p.Hi.x - p.Lo.x), p.Lo.y + ct * (p.Hi.y - p.Lo.y)) : src.GetPixelBilinear(p.Lo.x + ct * (p.Hi.x - p.Lo.x), p.Lo.y + gt * (p.Hi.y - p.Lo.y));
                    r[j] = c.r; g[j] = c.g; b[j] = c.b;
                }
                Array.Sort(r); Array.Sort(g); Array.Sort(b);
                res[k] = new Color(r[m / 2], g[m / 2], b[m / 2], 1f);
            }
            return res;
        }
        /// Color del perfil en la posición s (texeles; se refleja en los extremos): interpolado.
        static Color PerfilEn(Color[] pf, float s)
        {
            int m = pf.Length - 1; float t = s - 2f * m * Mathf.Floor(s / (2f * m)); if (t > m) t = 2f * m - t;
            int i0 = Mathf.Clamp((int)t, 0, m - 1); return Color.Lerp(pf[i0], pf[i0 + 1], t - i0);
        }
        static Color Limita(Color c, float a) { return new Color(Mathf.Clamp(c.r, -a, a), Mathf.Clamp(c.g, -a, a), Mathf.Clamp(c.b, -a, a), 0f); }

        // ---------------------------------------------------------------- generación
        /// Genera color, relieve y metal/brillo. "ref" = parche de madera que usa la culata (para el color de referencia).
        public static Juego Generar(Texture origen, Vector2 refLo, Vector2 refHi, Action<string> log)
        {
            Texture2D src = null;
            try { src = LeerOrigen(origen); } catch (Exception e) { if (log != null) log("textura del mosquete no legible: " + e.Message); }
            if (log != null) log("textura del mosquete " + (src != null ? src.width + "×" + src.height : "no disponible"));
            var t0 = DateTime.Now;
            Parche A = new Parche(), B = new Parche(), Fe = new Parche();
            Color medio = new Color(0.40f, 0.22f, 0.13f, 1f);
            if (src != null)
            {
                Color? refMadera = null; float rr = 0, rg = 0, rb = 0; int rn = 0;
                for (int i = 0; i < 24; i++) for (int j = 0; j < 24; j++)
                {
                    var c = src.GetPixelBilinear(Mathf.Lerp(refLo.x, refHi.x, i / 23f), Mathf.Lerp(refLo.y, refHi.y, j / 23f));
                    if (EsMadera(c)) { rr += c.r; rg += c.g; rb += c.b; rn++; }
                }
                if (rn >= 20) refMadera = new Color(rr / rn, rg / rn, rb / rn, 1f);
                A = BuscarParche(src, EsMadera, refMadera, null, log, "madera A"); ParcheA = A;
                B = BuscarParche(src, EsMadera, refMadera, A, log, "madera B");
                if (!B.Ok) B = A;
                Fe = BuscarParche(src, EsHierro, null, null, log, "hierro");
                if (A.Ok)
                {
                    float r = 0, g = 0, b = 0;
                    for (int i = 0; i < 16; i++) for (int j = 0; j < 16; j++) { var c = src.GetPixelBilinear(Mathf.Lerp(A.Lo.x, A.Hi.x, i / 15f), Mathf.Lerp(A.Lo.y, A.Hi.y, j / 15f)); r += c.r; g += c.g; b += c.b; }
                    medio = new Color(r / 256f, g / 256f, b / 256f, 1f);
                }
            }
            bool real = src != null && A.Ok;
            Color[] PA = null, PB = null, PF = null;
            if (real) { PA = Perfil(src, A); PB = B.Ok ? Perfil(src, B) : PA; if (Fe.Ok) PF = Perfil(src, Fe); }
            float cwA = real ? Mathf.Max(8f, A.VetaU ? (A.Hi.y - A.Lo.y) * src.height : (A.Hi.x - A.Lo.x) * src.width) : 1f;
            float cwF = real && Fe.Ok ? Mathf.Max(8f, Fe.VetaU ? (Fe.Hi.y - Fe.Lo.y) * src.height : (Fe.Hi.x - Fe.Lo.x) * src.width) : 1f;
            var col = new Color32[T * T]; var mg = new Color32[T * T]; var H = new float[T * T];
            int xm = (int)(MaderaU1 * T), ym = (int)(MaderaV1 * T);
            int xh0 = (int)(HierroLo.x * T), xh1 = (int)(HierroHi.x * T), yh0 = (int)(HierroLo.y * T), yh1 = (int)(HierroHi.y * T);
            var rnd = new System.Random(7);
            // rayones del hierro
            var ray = new float[90, 4];
            for (int i = 0; i < 90; i++) { ray[i, 0] = (float)rnd.NextDouble(); ray[i, 1] = (float)rnd.NextDouble(); ray[i, 2] = 0.04f + (float)rnd.NextDouble() * 0.22f; ray[i, 3] = ((float)rnd.NextDouble() - 0.5f) * 0.06f; }
            // brillo/tono del fondo (zonas sin usar)
            Color32 fondo = new Color32(70, 70, 74, 255);
            for (int y = 0; y < T; y++)
            {
                float v = y / (float)T;
                for (int x = 0; x < T; x++)
                {
                    int idx = y * T + x; float u = x / (float)T; Color c; float h; float metal = 0f, brillo = 0.4f;
                    if (x < xm && y < ym)
                    {
                        // ---- MADERA: mezcla de dos parches reales con máscara suave, tono largo a lo largo de la veta, vetas finas y micrograno
                        float m = Ruido(x / 1100f + 3.1f, y / 520f + 7.7f);
                        float w = Mathf.SmoothStep(0.30f, 0.70f, m);
                        if (real)
                        {
                            // perfil de la veta real (A y B) con ondulación a lo largo + detalle fino real (recortado, sin tornillos ni bordes)
                            float onda = (Ruido(x / 800f + 3f, y / 350f + 9f) - 0.5f) * 70f + (Ruido(x / 260f + 1f, y / 90f + 4f) - 0.5f) * 16f;
                            Color pa = PerfilEn(PA, (y + onda) / 1.35f), pb = PerfilEn(PB, ((y + 233f) * 1.07f + onda) / 1.55f);
                            Color baseC = Color.Lerp(pa, pb, w);
                            Color real1 = Muestra(src, A, x + 311f, y + 97f, 1.35f);
                            float ctA = Espejo((y + 97f) / (cwA * 1.35f));
                            Color det = Limita(real1 - PerfilEn(PA, ctA * (PA.Length - 1)), 0.055f);
                            c = new Color(baseC.r + det.r * 0.9f, baseC.g + det.g * 0.9f, baseC.b + det.b * 0.9f, 1f);
                        }
                        else c = medio;
                        float tono = 0.86f + 0.28f * Ruido(x / 1500f + 11f, y / 140f + 5f);       // bandas largas a lo largo de la veta
                        float vena = 0.90f + 0.20f * Ruido(x / 420f + 2f, y / 7f + 4f);           // vetas finas
                        float micro = 0.95f + 0.10f * Ruido(x / 90f + 9f, y / 1.6f + 1f);         // micrograno
                        float tinte = Ruido(x / 1800f + 21f, y / 900f + 2f);                       // variación de color suave
                        c.r *= tono * vena * micro * (0.96f + 0.10f * tinte); c.g *= tono * vena * micro * (0.97f + 0.07f * tinte); c.b *= tono * vena * micro;
                        float poro = Ruido(x / 40f + 31f, y / 3.2f + 17f);
                        if (poro > 0.80f) { float k = 1f - (poro - 0.80f) * 2.4f; c.r *= k; c.g *= k; c.b *= k; }
                        c = Color.Lerp(medio, c, 0.80f);
                        float gr = (c.r + c.g + c.b) / 3f; c = Color.Lerp(c, new Color(gr, gr, gr, 1f), 0.30f);
                        c = new Color(Mathf.Min(1f, c.r * 1.56f), Mathf.Min(1f, c.g * 1.56f), Mathf.Min(1f, c.b * 1.50f), 1f);
                        float lum = (c.r + c.g + c.b) / 3f;
                        h = lum * 0.9f + 0.10f * Ruido(x / 14f, y / 1.3f); brillo = 0.52f + 0.38f * Mathf.Clamp01((lum - 0.25f) * 2.0f); metal = 0f;
                    }
                    else if (x >= xh0 && x < xh1 && y >= yh0 && y < yh1)
                    {
                        // ---- HIERRO: metal real del mosquete + cepillado, rayones y óxido
                        if (real && Fe.Ok)
                        {
                            float ond = (Ruido(x / 600f + 8f, y / 200f + 2f) - 0.5f) * 30f;
                            Color bf = PerfilEn(PF, (y - yh0 + ond) / 1.3f), rf = Muestra(src, Fe, x - xh0, y - yh0, 1.3f);
                            Color df = Limita(rf - PerfilEn(PF, Espejo((y - yh0) / (cwF * 1.3f)) * (PF.Length - 1)), 0.06f);
                            c = new Color(bf.r + df.r, bf.g + df.g, bf.b + df.b, 1f);
                        }
                        else c = new Color(0.42f, 0.44f, 0.48f, 1f);
                        float cep = 0.88f + 0.24f * Ruido(x / 260f + 5f, y / 2.5f + 3f);
                        float nube = 0.85f + 0.30f * Ruido(x / 300f + 1f, y / 300f + 8f);
                        float ox = Ruido(x / 170f + 13f, y / 170f + 6f), oxm = Mathf.Clamp01((ox - 0.60f) * 5f);
                        c.r *= cep * nube; c.g *= cep * nube; c.b *= cep * nube;
                        c = Color.Lerp(c, new Color(0.34f, 0.17f, 0.08f, 1f), oxm * 0.06f);
                        float uu = (x - xh0) / (float)(xh1 - xh0), vv = (y - yh0) / (float)(yh1 - yh0);
                        for (int i = 0; i < 90; i++)
                        {
                            float dx = uu - ray[i, 0], dy = vv - (ray[i, 1] + ray[i, 3] * (uu - ray[i, 0]));
                            if (dx > 0f && dx < ray[i, 2] && Mathf.Abs(dy) < 0.0016f) c = Color.Lerp(c, new Color(0.62f, 0.64f, 0.68f, 1f), 0.5f);
                        }
                        c = new Color(c.r * 1.45f, c.g * 1.46f, c.b * 1.52f, 1f); float gm = (c.r + c.g + c.b) / 3f; c = Color.Lerp(c, new Color(gm, gm, gm * 1.04f, 1f), 0.55f);
                        float lum = (c.r + c.g + c.b) / 3f;
                        h = lum * 0.8f + 0.2f * ox; metal = 0.95f * (1f - oxm * 0.3f); brillo = 0.50f + 0.30f * Mathf.Clamp01(lum * 2f);
                    }
                    else if (u >= CanalLo.x && u < CanalHi.x && v >= CanalLo.y && v < CanalHi.y)
                    {
                        float r = Ruido(x / 50f, y / 10f); c = new Color(0.09f + 0.04f * r, 0.05f + 0.025f * r, 0.028f + 0.015f * r, 1f); h = 0.35f + 0.1f * r; brillo = 0.12f;
                    }
                    else if (u >= PlumaLo.x && u < PlumaHi.x && v >= PlumaLo.y && v < PlumaHi.y)
                    {
                        float r = Ruido(x / 20f, y / 70f); c = new Color(0.50f + 0.20f * r, 0.08f + 0.05f * r, 0.06f + 0.04f * r, 1f); h = 0.5f + 0.2f * Mathf.Sin(x * 0.5f); brillo = 0.15f;
                    }
                    else if (u >= CuerdaLo.x && u < CuerdaHi.x && v >= CuerdaLo.y && v < CuerdaHi.y)
                    {
                        float a = (x - (int)(CuerdaLo.x * T)) * 0.5f, b = (y - (int)(CuerdaLo.y * T)) * 0.5f;
                        float trenza = 0.5f + 0.5f * Mathf.Sin(a * 0.9f + b * 3.2f), hebra = Ruido(a / 9f, b / 2f);
                        c = Color.Lerp(new Color(0.34f, 0.24f, 0.14f, 1f), new Color(0.74f, 0.60f, 0.38f, 1f), Mathf.Clamp01(trenza * 0.7f + hebra * 0.5f));
                        h = trenza * 0.8f + hebra * 0.2f; brillo = 0.08f;
                    }
                    else if (u >= CinchaLo.x && u < CinchaHi.x && v >= CinchaLo.y && v < CinchaHi.y)
                    {
                        // CINCHA: banda de metal amarillo grisáceo, cepillada
                        float r = Ruido(x / 120f, y / 3f), q = Ruido(x / 40f + 5f, y / 40f + 2f);
                        c = new Color(0.66f + 0.10f * r, 0.62f + 0.09f * r, 0.44f + 0.07f * r, 1f) * (0.92f + 0.14f * q);
                        h = 0.5f + 0.3f * r; metal = 0.55f; brillo = 0.55f; c.a = 1f;
                    }
                    else { c = fondo; h = 0.5f; brillo = 0.1f; }
                    col[idx] = c; H[idx] = h;
                    mg[idx] = new Color32((byte)Mathf.Clamp(metal * 255f, 0, 255), 0, 0, (byte)Mathf.Clamp(brillo * 255f, 0, 255));
                }
            }
            // ---- mapa de normales (formato DXT5nm: x en A, y en G) a partir de la altura
            var nr = new Color32[T * T];
            for (int y = 0; y < T; y++)
            {
                int ya = Mathf.Max(0, y - 1), yb = Mathf.Min(T - 1, y + 1);
                for (int x = 0; x < T; x++)
                {
                    int xa = Mathf.Max(0, x - 1), xb = Mathf.Min(T - 1, x + 1);
                    float dx = H[y * T + xb] - H[y * T + xa], dy = H[yb * T + x] - H[ya * T + x];
                    float fuerza = x < xm && y < ym ? 5.0f : 3.0f;
                    float nx = -dx * fuerza, ny = -dy * fuerza, nz = 1f; float l = Mathf.Sqrt(nx * nx + ny * ny + nz * nz); nx /= l; ny /= l;
                    nr[y * T + x] = new Color32(255, (byte)((ny * 0.5f + 0.5f) * 255f), 128, (byte)((nx * 0.5f + 0.5f) * 255f));
                }
            }
            var j2 = new Juego();
            j2.Color = new Texture2D(T, T, TextureFormat.RGBA32, true, false) { name = "coop_ballesta_color", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 8 };
            j2.Color.SetPixels32(col); j2.Color.Apply(true);
            j2.Normal = new Texture2D(T, T, TextureFormat.RGBA32, true, true) { name = "coop_ballesta_normal", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 8 };
            j2.Normal.SetPixels32(nr); j2.Normal.Apply(true);
            j2.MetalBrillo = new Texture2D(T, T, TextureFormat.RGBA32, true, true) { name = "coop_ballesta_metal", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            j2.MetalBrillo.SetPixels32(mg); j2.MetalBrillo.Apply(true);
            try
            {
                string dir = System.IO.Path.Combine(BepInEx.Paths.BepInExRootPath, "capturas");
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, "ballesta_textura.png"), j2.Color.EncodeToPNG());
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, "ballesta_normal.png"), j2.Normal.EncodeToPNG());
                if (src != null) System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, "mosquete_textura.png"), src.EncodeToPNG());
            }
            catch (Exception) { }
            // compresión (DXT) para gastar menos memoria; si falla se queda sin comprimir
            try { j2.Color.Compress(true); j2.Color.Apply(false, true); j2.Normal.Compress(true); j2.Normal.Apply(false, true); j2.MetalBrillo.Compress(false); j2.MetalBrillo.Apply(false, true); }
            catch (Exception e) { if (log != null) log("compresión de texturas omitida: " + e.Message); }
            if (log != null) log("texturas generadas en " + (DateTime.Now - t0).TotalSeconds.ToString("F1") + " s");
            return j2;
        }
    }
}

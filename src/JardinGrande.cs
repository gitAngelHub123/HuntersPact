// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using System.Collections.Generic;
using UnityEngine;

namespace WitchHuntCoop
{
    /// Jardín barroco de parterres (pedido 2026-11-17, rehecho por completo a partir de una foto de referencia del estilo):
    /// ocupa la terraza llana al norte del castillo. Un gran paseo de grava clara en el eje lleva a una fuente redonda con
    /// surtidor; a cada lado, parterres de bordado: volutas y arabescos de boj bajo sobre grava blanca, medallones y
    /// riñones de césped, bordes de grava roja y una banda ancha de flores de colores rodeando cada parterre. Conos de tejo
    /// recortado en hileras, bancos de piedra, faroles y una balaustrada blanca en el borde de la terraza.
    /// Todo con texturas propias (grava, césped, boj, flores con transparencia) y en mallas combinadas.
    public partial class Mundo
    {
        class Lote
        {
            readonly Transform pa; readonly string nombre; readonly Material mat; readonly bool col;
            List<Vector3> v = new List<Vector3>(); List<Vector2> uv = new List<Vector2>(); List<int> tr = new List<int>();
            public int Piezas;
            public Lote(Transform pa, string nombre, Material mat, bool col) { this.pa = pa; this.nombre = nombre; this.mat = mat; this.col = col; }
            public void Caja(Vector3 c, Vector3 s, Quaternion q, float tile) { CajaRot(v, uv, tr, c, s, q, tile); Piezas++; Revisar(); }
            /// Cuadrilátero suelto (a, b, c, d en orden), con uv dados.
            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
            {
                int i = v.Count; v.Add(a); v.Add(b); v.Add(c); v.Add(d); uv.Add(ua); uv.Add(ub); uv.Add(uc); uv.Add(ud);
                tr.Add(i); tr.Add(i + 1); tr.Add(i + 2); tr.Add(i); tr.Add(i + 2); tr.Add(i + 3); Piezas++; Revisar();
            }
            /// Tejo recortado en cono: perfil un poco abombado y punta redondeada, con vértices compartidos (se ve liso, no facetado).
            public void Cono(Vector3 b, float r, float h, int lados, float tile)
            {
                const int Anillos = 9; int i0 = v.Count;
                for (int a = 0; a <= Anillos; a++)
                {
                    float t = a / (float)Anillos;
                    float rr = r * Mathf.Pow(1f - t, 0.82f) * (1f + 0.08f * Mathf.Sin(t * Mathf.PI));   // abombado
                    if (a == Anillos) rr = 0.02f;
                    for (int k = 0; k <= lados; k++)
                    {
                        float ang = k * Mathf.PI * 2f / lados;
                        v.Add(b + new Vector3(Mathf.Cos(ang) * rr, t * h, Mathf.Sin(ang) * rr));
                        uv.Add(new Vector2(k / (float)lados * tile * 2f, t * h / 1.2f));
                    }
                }
                int fila = lados + 1;
                for (int a = 0; a < Anillos; a++)
                    for (int k = 0; k < lados; k++)
                    {
                        int p0 = i0 + a * fila + k, p1 = p0 + 1, q0 = p0 + fila, q1 = q0 + 1;
                        tr.Add(p0); tr.Add(q0); tr.Add(p1); tr.Add(p1); tr.Add(q0); tr.Add(q1);
                    }
                Piezas++; Revisar();
            }
            /// Bola de boj recortada (esfera un poco achatada, vértices compartidos).
            public void Esfera(Vector3 c, float r, float achatar, float tile)
            {
                const int A = 10, L = 16; int i0 = v.Count;
                for (int a = 0; a <= A; a++)
                {
                    float t = a / (float)A, fi = t * Mathf.PI, y = -Mathf.Cos(fi) * r * achatar, rr = Mathf.Sin(fi) * r;
                    for (int k = 0; k <= L; k++) { float ang = k * Mathf.PI * 2f / L; v.Add(c + new Vector3(Mathf.Cos(ang) * rr, y, Mathf.Sin(ang) * rr)); uv.Add(new Vector2(k / (float)L * tile * 2f, t * tile)); }
                }
                for (int a = 0; a < A; a++) for (int k = 0; k < L; k++)
                {
                    int p0 = i0 + a * (L + 1) + k, p1 = p0 + 1, q0 = p0 + L + 1, q1 = q0 + 1;
                    tr.Add(p0); tr.Add(q0); tr.Add(p1); tr.Add(p1); tr.Add(q0); tr.Add(q1);
                }
                Piezas++; Revisar();
            }
            /// Seto recortado: cuerpo recto y copa más estrecha encima (se ve redondeado, no una caja).
            public void SetoTramo(Vector3 c, float ancho, float alto, float largo, Quaternion q)
            {
                CajaRot(v, uv, tr, c - Vector3.up * alto * 0.12f, new Vector3(ancho, alto * 0.76f, largo), q, 1.2f);
                CajaRot(v, uv, tr, c + Vector3.up * alto * 0.3f, new Vector3(ancho * 0.72f, alto * 0.24f, largo * 0.98f), q, 1.2f);
                Piezas++; Revisar();
            }
            void Revisar() { if (v.Count > 60000) Cerrar(); }
            public void Cerrar()
            {
                if (v.Count == 0) return;
                var g = MallaDe(pa, nombre, v, uv, tr, mat);
                g.GetComponent<MeshRenderer>().shadowCastingMode = col ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
                if (col) g.AddComponent<MeshCollider>().sharedMesh = g.GetComponent<MeshFilter>().sharedMesh;
                v = new List<Vector3>(); uv = new List<Vector2>(); tr = new List<int>();
            }
        }

        static Material matGravaClara, matGravaRoja, matCesped, matBojJ, matBalaustre, matAguaFuente, matTierraJ;
        static Material[] matFloresJ;

        // ---------------- Texturas ----------------

        static Material MatJardin(string nombre, int n, int semilla, System.Func<float, float, float, Color> color, float fuerza, float brillo)
        {
            var col = new Color[n * n]; var alt = new float[n, n]; var rng = new System.Random(semilla);
            float ox = (float)rng.NextDouble() * 50f, oy = (float)rng.NextDouble() * 50f;
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                float g1 = Ruido(x + ox, y + oy, n, 0.08f), g2 = Ruido(x + oy, y + ox, n, 0.35f), g3 = Ruido(x * 1.3f + 7f, y * 1.3f + 3f, n, 1.1f);
                Color c = color(g1, g2, g3); col[y * n + x] = new Color(c.r, c.g, c.b, 1f); alt[x, y] = c.a;
            }
            var a = new Texture2D(n, n, TextureFormat.RGB24, true) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Repeat }; a.SetPixels(col); a.Apply();
            return Estandar(nombre, a, Normales(alt, n, fuerza), Color.white, brillo, 1.2f);
        }

        static void MaterialesJardin()
        {
            if (matGravaClara != null) return;
            // grava clara: guijarros redondos de tonos crema y gris, con sombra entre ellos
            matGravaClara = MatJardin("coop_grava_clara", 256, 11, (g1, g2, g3) =>
            {
                float guijarro = Mathf.Clamp01((g3 - 0.42f) * 4f); float tono = 0.62f + 0.18f * g2 + 0.08f * g1;
                Color c = Color.Lerp(new Color(0.45f, 0.41f, 0.35f), new Color(tono, tono * 0.95f, tono * 0.86f), guijarro);
                c = Color.Lerp(c, new Color(0.6f, 0.57f, 0.5f), 0.45f);   // menos contraste
                c.a = guijarro * 0.6f + g2 * 0.4f; return c;
            }, 1.1f, 0.04f);
            matGravaRoja = MatJardin("coop_grava_roja", 256, 12, (g1, g2, g3) =>
            {
                float guijarro = Mathf.Clamp01((g3 - 0.4f) * 4f); float t = 0.5f + 0.3f * g2;
                Color c = Color.Lerp(new Color(0.3f, 0.14f, 0.1f), new Color(0.62f * t + 0.2f, 0.3f * t + 0.08f, 0.22f * t + 0.05f), guijarro);
                c.a = guijarro; return c;
            }, 1.2f, 0.05f);
            // césped corto: hojitas en vetas, verde con variaciones
            matCesped = MatJardin("coop_cesped_jardin", 512, 13, (g1, g2, g3) =>
            {
                float hoja = Mathf.Clamp01(g3 * 1.4f - 0.2f); float v = 0.55f + 0.35f * g1;
                Color c = Color.Lerp(new Color(0.08f, 0.17f, 0.05f), new Color(0.22f * v + 0.05f, 0.42f * v + 0.08f, 0.1f * v), hoja);
                c.a = hoja; return c;
            }, 1.4f, 0.08f);
            // boj recortado: hojita apretada, verde oscuro con brillos
            matBojJ = MatJardin("coop_boj_jardin", 512, 14, (g1, g2, g3) =>
            {
                float h = Mathf.Clamp01(g3 * 1.5f - 0.25f) * 0.7f + g2 * 0.3f; float v = 0.7f + 0.3f * g1;
                Color c = Color.Lerp(new Color(0.03f, 0.08f, 0.03f), new Color(0.14f * v, 0.3f * v, 0.1f * v), h);
                c.a = h; return c;
            }, 3f, 0.18f);
            // piedra blanca de la balaustrada (caliza, apenas veteada)
            matBalaustre = MatJardin("coop_balaustre", 256, 15, (g1, g2, g3) =>
            {
                float t = 0.82f + 0.1f * g1 - 0.06f * g3; return new Color(t, t * 0.97f, t * 0.9f, g1 * 0.5f + g3 * 0.5f);
            }, 0.8f, 0.2f);
            matAguaFuente = new Material(Shader.Find("Standard")) { name = "coop_agua_fuente2", color = new Color(0.16f, 0.24f, 0.28f, 1f) };
            matAguaFuente.SetFloat("_Glossiness", 0.97f); matAguaFuente.SetFloat("_Metallic", 0.25f);
            // flores: matas con hojas y muchas flores pequeñas de un color (transparencia recortada)
            Color[] colores = { new Color(0.85f, 0.08f, 0.1f), new Color(0.98f, 0.5f, 0.06f), new Color(0.98f, 0.82f, 0.12f), new Color(0.95f, 0.45f, 0.65f), new Color(0.95f, 0.94f, 0.9f), new Color(0.5f, 0.2f, 0.75f) };
            matFloresJ = new Material[colores.Length];
            for (int i = 0; i < colores.Length; i++)
            {
                var t = TexFlor(64, colores[i], 100 + i);
                var m = new Material(Shader.Find("Standard")) { name = "coop_flores_" + i, mainTexture = t };
                m.SetFloat("_Mode", 1f); m.SetOverrideTag("RenderType", "TransparentCutout"); m.EnableKeyword("_ALPHATEST_ON"); m.SetFloat("_Cutoff", 0.45f);
                m.renderQueue = 2450; m.SetFloat("_Glossiness", 0.1f);
                matFloresJ[i] = m;
            }
        }

        static Texture2D TexFlor(int n, Color flor, int semilla)
        {
            var t = new Texture2D(n, n, TextureFormat.RGBA32, true) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp };
            var c = new Color[n * n]; var rng = new System.Random(semilla);
            for (int i = 0; i < c.Length; i++) c[i] = new Color(0f, 0f, 0f, 0f);
            // hojas: trazos verdes desde abajo
            for (int k = 0; k < 40; k++)
            {
                float x0 = (float)rng.NextDouble() * n, ang = ((float)rng.NextDouble() - 0.5f) * 1.2f, largo = n * (0.3f + 0.4f * (float)rng.NextDouble());
                Color verde = Color.Lerp(new Color(0.06f, 0.16f, 0.05f), new Color(0.2f, 0.42f, 0.12f), (float)rng.NextDouble());
                for (float s = 0; s < largo; s += 0.5f)
                {
                    int x = Mathf.RoundToInt(x0 + Mathf.Sin(ang) * s), y = Mathf.RoundToInt(Mathf.Cos(ang) * s);
                    float ancho = 1.6f * (1f - s / largo) + 0.4f;
                    for (int dx = -2; dx <= 2; dx++) { int xx = x + dx; if (xx < 0 || xx >= n || y < 0 || y >= n || Mathf.Abs(dx) > ancho) continue; c[y * n + xx] = new Color(verde.r, verde.g, verde.b, 1f); }
                }
            }
            // flores: discos de pétalos en la mitad de arriba, con centro más oscuro
            for (int k = 0; k < 26; k++)
            {
                float cx = 4 + (float)rng.NextDouble() * (n - 8), cy = n * (0.35f + 0.6f * (float)rng.NextDouble()), r = 2.2f + (float)rng.NextDouble() * 2.6f;
                float tono = 0.8f + 0.25f * (float)rng.NextDouble();
                for (int y = (int)(cy - r - 1); y <= cy + r + 1; y++) for (int x = (int)(cx - r - 1); x <= cx + r + 1; x++)
                {
                    if (x < 0 || x >= n || y < 0 || y >= n) continue;
                    float d = new Vector2(x - cx, y - cy).magnitude; if (d > r) continue;
                    float petalo = 0.85f + 0.15f * Mathf.Cos(Mathf.Atan2(y - cy, x - cx) * 5f);
                    if (d > r * petalo) continue;
                    Color fc = d < r * 0.3f ? new Color(flor.r * 0.45f + 0.25f, flor.g * 0.45f + 0.2f, flor.b * 0.3f, 1f) : new Color(flor.r * tono, flor.g * tono, flor.b * tono, 1f);
                    c[y * n + x] = fc;
                }
            }
            t.SetPixels(c); t.Apply(); return t;
        }

        // ---------------- El jardín ----------------

        void JardinGrande(Transform pa, TerrainData dn)
        {
            Vector2 c = CentroMonte;
            const float RMax = 170f;
            float cx = CastilloX, zIni = 952f, zFin = c.y + 172f, zMid = (zIni + zFin) / 2f;
            MaterialesJardin();
            // suelo a juego con el castillo (pedido 2026-11-18): el adoquín del castillo (con grano de granito) en vez de grava clara;
            // césped con la hierba del terreno; balaustrada con grano de granito
            PrepararMaterialesCastillo();
            var gran = GranitoTerreno();
            if (gran != null) { matBalaustre.SetTexture("_DetailAlbedoMap", gran); matBalaustre.SetTextureScale("_DetailAlbedoMap", new Vector2(0.6f, 0.6f)); matBalaustre.EnableKeyword("_DETAIL_MULX2"); }
            var tt = Anclaje.Principal();
            if (tt != null && tt.terrainData != null)
                foreach (var sp in tt.terrainData.splatPrototypes)
                    if (sp.texture != null && sp.texture.name.ToLowerInvariant().Contains("grass"))
                    { matCesped.SetTexture("_DetailAlbedoMap", sp.texture); matCesped.SetTextureScale("_DetailAlbedoMap", new Vector2(0.5f, 0.5f)); matCesped.EnableKeyword("_DETAIL_MULX2"); break; }
            var grava = new Lote(pa, "coop_jardin_grava", matAdoquin, false);
            var gravaR = new Lote(pa, "coop_jardin_grava_roja", matGravaRoja, false);
            var cesped = new Lote(pa, "coop_jardin_cesped", matCesped, false);
            var tierra = new Lote(pa, "coop_jardin_tierra", MatColor(ref matTierraJ, "coop_tierra_jardin", new Color(0.18f, 0.12f, 0.08f)), false);
            var boj = new Lote(pa, "coop_jardin_boj", matBojJ, true);
            var conos = new Lote(pa, "coop_jardin_conos", matBojJ, true);
            var piedra = new Lote(pa, "coop_jardin_balaustrada", matBalaustre, true);
            var flores = new Lote[matFloresJ.Length];
            for (int i = 0; i < flores.Length; i++) flores[i] = new Lote(pa, "coop_jardin_flores_" + i, matFloresJ[i], false);
            var rng = new System.Random(91357);
            System.Func<float, float, float> suelo = (x, z) => SueloT(dn, x, z);
            System.Func<float, float, bool> dentro = (x, z) => (x - c.x) * (x - c.x) + (z - c.y) * (z - c.y) < RMax * RMax && z > zIni;

            // 1) Empedrado en toda la terraza y hasta la muralla (178 m) y 25 m más cerca del castillo: sin franjas de tierra vacías
            const float RPiso = 178f;
            System.Func<float, float, bool> dentroPiso = (x, z) => (x - c.x) * (x - c.x) + (z - c.y) * (z - c.y) < RPiso * RPiso && z > zIni - 25f;
            Plano(grava, suelo, dentroPiso, cx - RPiso, cx + RPiso, zIni - 25f, zFin + 8f, 2f, 0.02f, 2.6f);

            // 2) Parterres de bordado: dos delante de la fuente y dos detrás, a cada lado del paseo central (9 m).
            int parterres = 0;
            foreach (var sx in new[] { -1f, 1f })
            {
                Parterre(new Vector2(cx + sx * 34f, zIni + 28f), 27f, 21f, sx, grava, gravaR, cesped, tierra, boj, flores, rng, suelo, dentro, ref parterres);
                Parterre(new Vector2(cx + sx * 34f, zMid + 26f), 27f, 15f, sx, grava, gravaR, cesped, tierra, boj, flores, rng, suelo, dentro, ref parterres);
                // paños de césped con borde de flores más afuera
                Pano(new Vector2(cx + sx * 92f, zIni + 28f), 19f, 20f, cesped, tierra, boj, flores, rng, suelo, dentro);
            }

            // 3) Fuente redonda en el eje, con surtidor y faroles.
            Fuente(pa, new Vector3(cx, suelo(cx, zMid), zMid), piedra, gravaR, suelo);
            foreach (var sx in new[] { -1f, 1f }) foreach (var sz in new[] { -1f, 1f }) Farol2(pa, new Vector3(cx + sx * 14f, suelo(cx + sx * 14f, zMid + sz * 14f), zMid + sz * 14f));

            // 4) Conos de tejo: hilera a cada lado del paseo central y en las esquinas de los parterres.
            int nConos = 0;
            for (float z = zIni + 8f; z < zFin - 6f; z += 10f)
            {
                if (Mathf.Abs(z - zMid) < 15f) continue;
                foreach (var sx in new[] { -1f, 1f })
                {
                    float x = cx + sx * 6.2f; if (!dentro(x, z)) continue;
                    float h = 3.6f + (float)rng.NextDouble() * 0.5f;
                    conos.Cono(new Vector3(x, suelo(x, z) - 0.05f, z), 1.0f, h, 20, 2f); nConos++;
                }
            }
            foreach (var sx in new[] { -1f, 1f })
                foreach (var p in new[] { new Vector2(7f, zIni + 6f), new Vector2(61f, zIni + 6f), new Vector2(7f, zIni + 50f), new Vector2(61f, zIni + 50f), new Vector2(7f, zMid + 10f), new Vector2(61f, zMid + 10f) })
                {
                    float x = cx + sx * p.x, z = p.y; if (!dentro(x, z)) continue;
                    conos.Cono(new Vector3(x, suelo(x, z) - 0.05f, z), 1.15f, 4.4f, 20, 2f); nConos++;
                }

            // 4b) Cipreses (conos altos) por dentro de la balaustrada, cada 9 m
            for (float a = 0f; a < Mathf.PI * 2f; a += 9f / (RMax - 8f))
            {
                float x = c.x + Mathf.Cos(a) * (RMax - 8f), z = c.y + Mathf.Sin(a) * (RMax - 8f);
                if (!dentro(x, z) || z < zIni + 4f) continue;
                conos.Cono(new Vector3(x, suelo(x, z) - 0.05f, z), 1.3f, 5.5f + (float)rng.NextDouble() * 1.2f, 20, 2f); nConos++;
            }

            // 5) Bancos de piedra a lo largo del paseo central, mirando al eje.
            for (float z = zIni + 13f; z < zFin - 6f; z += 20f)
            {
                if (Mathf.Abs(z - zMid) < 15f) continue;
                foreach (var sx in new[] { -1f, 1f })
                {
                    float x = cx + sx * 4.4f; if (!dentro(x, z)) continue; float g = suelo(x, z);
                    piedra.Caja(new Vector3(x, g + 0.47f, z), new Vector3(0.55f, 0.1f, 2f), Quaternion.identity, 1f);
                    foreach (var dz in new[] { -0.75f, 0.75f }) piedra.Caja(new Vector3(x, g + 0.22f, z + dz), new Vector3(0.45f, 0.45f, 0.22f), Quaternion.identity, 1f);
                }
            }

            // 6) Balaustrada blanca por el borde curvo de la terraza (y el borde del fondo).
            int tramos = Balaustrada(piedra, suelo, c, RMax - 3f, zIni + 2f);

            // 7) Vida (pedido 2026-11-18): pérgolas de rosas, rosales, macetones con flores, bolas de boj, más faroles, luciérnagas y polillas
            var hierro = new Lote(pa, "coop_jardin_hierro", matHierroC, true);
            string vida = "";
            try { vida = VidaJardin(pa, c, cx, zIni, zFin, zMid, RMax, suelo, dentro, piedra, conos, hierro, flores, rng); } catch (System.Exception e) { plugin.Log("Castillo: vida del jardín " + e); }
            hierro.Cerrar();
            foreach (var l in new[] { grava, gravaR, cesped, tierra, boj, conos, piedra }) l.Cerrar();
            foreach (var l in flores) l.Cerrar();
            if (vida != "") plugin.Log("Castillo: jardín vivo — " + vida);
            int nFlores = 0; foreach (var l in flores) nFlores += l.Piezas;
            plugin.Log("Castillo: jardín barroco con " + parterres + " parterres, " + nConos + " conos, " + nFlores + " matas de flores, " + boj.Piezas + " piezas de boj, " + tramos + " tramos de balaustrada");
        }

        /// Superficie plana sobre el terreno (rejilla de celdas 'paso'), solo donde 'dentro' y 'zona' lo permiten.
        static void Plano(Lote l, System.Func<float, float, float> suelo, System.Func<float, float, bool> dentro, float x0, float x1, float z0, float z1, float paso, float alto, float tile, System.Func<float, float, bool> zona = null)
        {
            for (float z = z0; z < z1; z += paso)
                for (float x = x0; x < x1; x += paso)
                {
                    float mx = x + paso / 2f, mz = z + paso / 2f;
                    if (!dentro(mx, mz) || (zona != null && !zona(mx, mz))) continue;
                    Vector3 a = new Vector3(x, suelo(x, z) + alto, z), b = new Vector3(x, suelo(x, z + paso) + alto, z + paso), cc = new Vector3(x + paso, suelo(x + paso, z + paso) + alto, z + paso), d = new Vector3(x + paso, suelo(x + paso, z) + alto, z);
                    l.Quad(a, b, cc, d, new Vector2(x, z) * tile, new Vector2(x, z + paso) * tile, new Vector2(x + paso, z + paso) * tile, new Vector2(x + paso, z) * tile);
                }
        }

        /// Seto bajo de boj a lo largo de una curva (tramos cortos que se solapan: se ve continuo y redondeado).
        static void Seto(Lote boj, System.Func<float, float, float> suelo, List<Vector2> pts, float ancho, float alto)
        {
            for (int i = 0; i < pts.Count - 1; i++)
            {
                Vector2 a = pts[i], b = pts[i + 1], d = b - a; float L = d.magnitude; if (L < 0.01f) continue;
                Vector2 m = (a + b) / 2f; float g = suelo(m.x, m.y);
                boj.SetoTramo(new Vector3(m.x, g + alto / 2f - 0.03f, m.y), ancho, alto, L + ancho * 0.5f, Quaternion.Euler(0f, Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg, 0f));
            }
        }

        /// Puntos de un rectángulo de esquinas redondeadas (semiejes a, b, radio de esquina r), centrado en c.
        static List<Vector2> RectRedondo(Vector2 c, float a, float b, float r)
        {
            var p = new List<Vector2>();
            Vector2[] centros = { new Vector2(a - r, b - r), new Vector2(-(a - r), b - r), new Vector2(-(a - r), -(b - r)), new Vector2(a - r, -(b - r)) };
            for (int q = 0; q < 4; q++)
                for (int k = 0; k <= 6; k++) { float ang = (q * 90f + k * 15f) * Mathf.Deg2Rad; p.Add(c + centros[q] + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r); }
            p.Add(p[0]); return p;
        }

        static bool EnRectRedondo(Vector2 p, Vector2 c, float a, float b, float r)
        {
            float dx = Mathf.Abs(p.x - c.x) - (a - r), dz = Mathf.Abs(p.y - c.y) - (b - r);
            if (dx <= 0f && dz <= 0f) return true;
            return new Vector2(Mathf.Max(dx, 0f), Mathf.Max(dz, 0f)).magnitude <= r;
        }

        static List<Vector2> Elipse(Vector2 c, float a, float b, float giro, int n = 28)
        {
            var p = new List<Vector2>(); float gr = giro * Mathf.Deg2Rad, cs = Mathf.Cos(gr), sn = Mathf.Sin(gr);
            for (int k = 0; k <= n; k++) { float t = k * Mathf.PI * 2f / n; float x = Mathf.Cos(t) * a, z = Mathf.Sin(t) * b; p.Add(c + new Vector2(x * cs - z * sn, x * sn + z * cs)); }
            return p;
        }

        static bool EnElipse(Vector2 p, Vector2 c, float a, float b, float giro)
        {
            float gr = -giro * Mathf.Deg2Rad, cs = Mathf.Cos(gr), sn = Mathf.Sin(gr); Vector2 d = p - c;
            float x = d.x * cs - d.y * sn, z = d.x * sn + d.y * cs; return (x * x) / (a * a) + (z * z) / (b * b) <= 1f;
        }

        /// Banda de flores entre dos contornos (matas cruzadas de un color por tramo, con algo de desorden).
        static void BandaFlores(Lote[] flores, System.Random rng, System.Func<float, float, float> suelo, System.Func<float, float, bool> enBanda, float x0, float x1, float z0, float z1, float sep, int colorBase, bool porTramos)
        {
            for (float z = z0; z < z1; z += sep)
                for (float x = x0; x < x1; x += sep)
                {
                    float px = x + ((float)rng.NextDouble() - 0.5f) * sep * 0.6f, pz = z + ((float)rng.NextDouble() - 0.5f) * sep * 0.6f;
                    if (!enBanda(px, pz)) continue;
                    int ci = porTramos ? (colorBase + Mathf.FloorToInt((px + pz) / 9f)) : colorBase; ci = ((ci % flores.Length) + flores.Length) % flores.Length;
                    float g = suelo(px, pz), h = 0.32f + (float)rng.NextDouble() * 0.18f, w = 0.42f + (float)rng.NextDouble() * 0.12f, giro = (float)rng.NextDouble() * Mathf.PI;
                    for (int k = 0; k < 2; k++)
                    {
                        float a = giro + k * Mathf.PI / 2f; Vector3 dx = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * w / 2f, b = new Vector3(px, g - 0.02f, pz);
                        Vector3 p0 = b - dx, p1 = b - dx + Vector3.up * h, p2 = b + dx + Vector3.up * h, p3 = b + dx;
                        flores[ci].Quad(p0, p1, p2, p3, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f));
                        flores[ci].Quad(p3, p2, p1, p0, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0f));   // cara de atrás
                    }
                }
        }

        /// Parterre de bordado (semiejes a × b), simétrico en x y z: banda de flores, bordes de boj, grava roja en las
        /// esquinas, medallón de césped al centro y arabescos de boj que terminan en volutas.
        void Parterre(Vector2 c, float a, float b, float lado, Lote grava, Lote gravaR, Lote cesped, Lote tierra, Lote boj, Lote[] flores, System.Random rng,
                      System.Func<float, float, float> suelo, System.Func<float, float, bool> dentro, ref int cuenta)
        {
            if (!dentro(c.x - a, c.y - b) || !dentro(c.x + a, c.y - b) || !dentro(c.x - a, c.y + b) || !dentro(c.x + a, c.y + b)) return;
            const float Banda = 2.4f, R = 5f;
            // tierra y flores en la banda exterior
            System.Func<float, float, bool> enBanda = (x, z) => EnRectRedondo(new Vector2(x, z), c, a - 0.4f, b - 0.4f, R) && !EnRectRedondo(new Vector2(x, z), c, a - Banda, b - Banda, R - 1.5f);
            Plano(tierra, suelo, dentro, c.x - a, c.x + a, c.y - b, c.y + b, 0.8f, 0.035f, 0.5f, enBanda);
            BandaFlores(flores, rng, suelo, enBanda, c.x - a, c.x + a, c.y - b, c.y + b, 0.38f, rng.Next(6), true);
            Seto(boj, suelo, RectRedondo(c, a - 0.15f, b - 0.15f, R), 0.32f, 0.38f);
            Seto(boj, suelo, RectRedondo(c, a - Banda - 0.15f, b - Banda - 0.15f, R - 1.5f), 0.36f, 0.42f);
            float ai = a - Banda - 0.6f, bi = b - Banda - 0.6f;
            // medallón central de césped con borde de boj y una estrella de grava roja dentro
            float ma = ai * 0.38f, mb = bi * 0.45f;
            Plano(cesped, suelo, dentro, c.x - ma, c.x + ma, c.y - mb, c.y + mb, 0.6f, 0.05f, 0.3f, (x, z) => EnElipse(new Vector2(x, z), c, ma, mb, 0f) && !EnElipse(new Vector2(x, z), c, ma * 0.35f, mb * 0.35f, 0f));
            Plano(gravaR, suelo, dentro, c.x - ma, c.x + ma, c.y - mb, c.y + mb, 0.4f, 0.05f, 0.4f, (x, z) => EnElipse(new Vector2(x, z), c, ma * 0.35f, mb * 0.35f, 0f));
            Seto(boj, suelo, Elipse(c, ma, mb, 0f, 40), 0.3f, 0.36f);
            Seto(boj, suelo, Elipse(c, ma * 0.35f, mb * 0.35f, 0f, 24), 0.26f, 0.32f);
            // cuatro cuartos simétricos: arabesco en S que nace del medallón y termina en voluta; riñón de césped; grava roja en la esquina
            foreach (var sx in new[] { -1f, 1f }) foreach (var sz in new[] { -1f, 1f })
            {
                System.Func<float, float, Vector2> P = (u, w) => new Vector2(c.x + sx * u, c.y + sz * w);
                var arab = new List<Vector2>();
                for (int k = 0; k <= 24; k++)
                {   // curva en S: del borde del medallón hacia la esquina
                    float t = k / 24f; float u = Mathf.Lerp(ma + 0.6f, ai * 0.78f, t), w = mb * 0.3f + Mathf.Sin(t * Mathf.PI * 1.15f) * bi * 0.32f + t * bi * 0.25f;
                    arab.Add(P(u, w));
                }
                Vector2 fin = arab[arab.Count - 1]; Vector2 cen = fin + new Vector2(-sx * 1.6f, -sz * 0.4f);
                float ang0 = Mathf.Atan2(fin.y - cen.y, fin.x - cen.x);
                for (int k = 1; k <= 28; k++)
                {   // voluta: espiral que se cierra (1,4 vueltas)
                    float t = k / 28f, ang = ang0 + sx * sz * t * Mathf.PI * 2.8f, r = Mathf.Lerp(1.65f, 0.35f, t);
                    arab.Add(cen + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r);
                }
                Seto(boj, suelo, arab, 0.28f, 0.34f);
                // segundo arabesco más corto, hacia el borde largo
                var arab2 = new List<Vector2>();
                for (int k = 0; k <= 16; k++) { float t = k / 16f; arab2.Add(P(Mathf.Lerp(ma * 0.3f, ai * 0.45f, t), Mathf.Lerp(mb + 0.5f, bi * 0.85f, t) - Mathf.Sin(t * Mathf.PI) * 1.4f)); }
                Vector2 f2 = arab2[arab2.Count - 1], c2 = f2 + new Vector2(sx * 0.9f, -sz * 0.6f); float a2 = Mathf.Atan2(f2.y - c2.y, f2.x - c2.x);
                for (int k = 1; k <= 18; k++) { float t = k / 18f; float ang = a2 - sx * sz * t * Mathf.PI * 2.2f, r = Mathf.Lerp(1.05f, 0.25f, t); arab2.Add(c2 + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r); }
                Seto(boj, suelo, arab2, 0.26f, 0.32f);
                // riñón de césped entre los arabescos
                Vector2 rc = P(ai * 0.55f, bi * 0.18f); float rg = sx * sz * 18f;
                Plano(cesped, suelo, dentro, rc.x - 4f, rc.x + 4f, rc.y - 4f, rc.y + 4f, 0.5f, 0.05f, 0.3f, (x, z) => EnElipse(new Vector2(x, z), rc, 3.1f, 1.3f, rg));
                Seto(boj, suelo, Elipse(rc, 3.1f, 1.3f, rg, 26), 0.24f, 0.3f);
                // grava roja en la esquina interior, en cuarto de círculo
                Vector2 ec = P(ai, bi);
                Plano(gravaR, suelo, dentro, ec.x - 4.5f, ec.x + 4.5f, ec.y - 4.5f, ec.y + 4.5f, 0.45f, 0.045f, 0.4f, (x, z) => (new Vector2(x, z) - ec).magnitude < 3.6f && EnRectRedondo(new Vector2(x, z), c, ai, bi, R - 2f));
                var arco = new List<Vector2>(); for (int k = 0; k <= 10; k++) { float ang = k / 10f * Mathf.PI / 2f; arco.Add(ec + new Vector2(-sx * Mathf.Sin(ang), -sz * Mathf.Cos(ang)) * 3.8f); }
                Seto(boj, suelo, arco, 0.26f, 0.32f);
                // flores en un lunar entre las volutas
                Vector2 fl = P(ai * 0.82f, bi * 0.55f);
                BandaFlores(flores, rng, suelo, (x, z) => (new Vector2(x, z) - fl).magnitude < 1.4f, fl.x - 1.6f, fl.x + 1.6f, fl.y - 1.6f, fl.y + 1.6f, 0.34f, rng.Next(6), false);
                Seto(boj, suelo, Elipse(fl, 1.55f, 1.55f, 0f, 18), 0.22f, 0.28f);
            }
            cuenta++;
        }

        /// Paño de césped con banda de flores y borde de boj (zonas laterales, más sencillas).
        void Pano(Vector2 c, float a, float b, Lote cesped, Lote tierra, Lote boj, Lote[] flores, System.Random rng, System.Func<float, float, float> suelo, System.Func<float, float, bool> dentro)
        {
            if (!dentro(c.x - a, c.y - b) || !dentro(c.x + a, c.y - b) || !dentro(c.x - a, c.y + b) || !dentro(c.x + a, c.y + b)) return;
            System.Func<float, float, bool> enBanda = (x, z) => EnRectRedondo(new Vector2(x, z), c, a - 0.4f, b - 0.4f, 3f) && !EnRectRedondo(new Vector2(x, z), c, a - 1.8f, b - 1.8f, 2f);
            Plano(tierra, suelo, dentro, c.x - a, c.x + a, c.y - b, c.y + b, 0.8f, 0.035f, 0.5f, enBanda);
            BandaFlores(flores, rng, suelo, enBanda, c.x - a, c.x + a, c.y - b, c.y + b, 0.4f, rng.Next(6), true);
            Plano(cesped, suelo, dentro, c.x - a, c.x + a, c.y - b, c.y + b, 0.8f, 0.05f, 0.3f, (x, z) => EnRectRedondo(new Vector2(x, z), c, a - 2f, b - 2f, 2f));
            Seto(boj, suelo, RectRedondo(c, a - 0.15f, b - 0.15f, 3f), 0.32f, 0.38f);
            Seto(boj, suelo, RectRedondo(c, a - 1.95f, b - 1.95f, 2f), 0.3f, 0.36f);
        }

        /// Fuente redonda: pila baja de piedra blanca con borde moldurado, agua, un pedestal con estatua y surtidor.
        void Fuente(Transform pa, Vector3 b, Lote piedra, Lote gravaR, System.Func<float, float, float> suelo)
        {
            const float R = 9f; const int N = 48;
            for (int k = 0; k < N; k++)
            {
                float a0 = k * Mathf.PI * 2f / N, a1 = (k + 1) * Mathf.PI * 2f / N, am = (a0 + a1) / 2f;
                Vector3 m = b + new Vector3(Mathf.Cos(am) * R, 0f, Mathf.Sin(am) * R); float L = 2f * R * Mathf.Sin(Mathf.PI / N) + 0.05f;
                Quaternion q = Quaternion.Euler(0f, -am * Mathf.Rad2Deg, 0f);
                piedra.Caja(m + Vector3.up * 0.35f, new Vector3(0.7f, 0.7f, L), q, 1f);                       // pretil
                piedra.Caja(m + Vector3.up * 0.76f + new Vector3(Mathf.Cos(am), 0f, Mathf.Sin(am)) * 0.05f, new Vector3(0.9f, 0.12f, L * 1.02f), q, 1f);   // albardilla
            }
            // fondo y agua
            Pz(pa, "coop_jardin_fuente_fondo", PrimitiveType.Cylinder, b + Vector3.up * 0.05f, new Vector3(R * 2f - 0.6f, 0.05f, R * 2f - 0.6f), 0f, matBalaustre, false);
            Pz(pa, "coop_jardin_fuente_agua", PrimitiveType.Cylinder, b + Vector3.up * 0.55f, new Vector3(R * 2f - 0.6f, 0.01f, R * 2f - 0.6f), 0f, matAguaFuente, false);
            // pedestal escalonado con estatua
            piedra.Caja(b + Vector3.up * 0.7f, new Vector3(3.2f, 1.4f, 3.2f), Quaternion.Euler(0f, 45f, 0f), 1f);
            piedra.Caja(b + Vector3.up * 1.6f, new Vector3(2.2f, 0.5f, 2.2f), Quaternion.identity, 1f);
            piedra.Caja(b + Vector3.up * 2.4f, new Vector3(1.5f, 1.1f, 1.5f), Quaternion.identity, 1f);
            try { Estatua(pa, b + Vector3.up * 2.95f, 180f); } catch { }
            // surtidores: cuatro chorros desde el pedestal hacia la pila
            for (int k = 0; k < 4; k++)
            {
                var g = new GameObject("coop_jardin_surtidor"); g.transform.SetParent(pa, false);
                float ang = k * 90f + 45f; g.transform.position = b + Vector3.up * 1.9f + Quaternion.Euler(0f, ang, 0f) * Vector3.forward * 1.2f;
                g.transform.rotation = Quaternion.Euler(-55f, ang, 0f);
                var ps = g.AddComponent<ParticleSystem>(); ps.Stop();
                var main = ps.main; main.startSpeed = 6.2f; main.startLifetime = 1.2f; main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.14f); main.gravityModifier = 1f; main.maxParticles = 600;
                main.startColor = new Color(0.8f, 0.88f, 0.95f, 0.55f); main.simulationSpace = ParticleSystemSimulationSpace.World;
                var em = ps.emission; em.rateOverTime = 260f;
                var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 3f; sh.radius = 0.04f;
                var r = g.GetComponent<ParticleSystemRenderer>();
                var mat = new Material(Shader.Find("Particles/Alpha Blended") ?? Shader.Find("Sprites/Default")) { name = "coop_agua_chorro" };
                r.sharedMaterial = mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                g.AddComponent<SoloCerca>();   // desde lejos se veían como arcos blancos en el cielo atravesando la niebla (2026-11-18)
                ps.Play();
            }
            // corona de grava roja alrededor de la pila
            for (int k = 0; k < 40; k++)
            {
                float a = k * Mathf.PI * 2f / 40f; Vector3 p = b + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (R + 1.4f);
                gravaR.Caja(new Vector3(p.x, suelo(p.x, p.z) + 0.03f, p.z), new Vector3(1.6f, 0.04f, 2f * (R + 1.4f) * Mathf.Sin(Mathf.PI / 40f) + 0.1f), Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 0f), 0.6f);
            }
        }

        /// Balaustrada de piedra blanca sobre el arco del borde de la terraza (radio r alrededor de c, solo donde z > zMin):
        /// pilastras cada 3 m con jarrón cada cuatro, balaustres torneados (dos piezas cruzadas) y pasamanos.
        int Balaustrada(Lote piedra, System.Func<float, float, float> suelo, Vector2 c, float r, float zMin)
        {
            int tramos = 0; const float Paso = 3f; int n = Mathf.CeilToInt(2f * Mathf.PI * r / Paso);
            for (int k = 0; k < n; k++)
            {
                float a0 = k * Mathf.PI * 2f / n, a1 = (k + 1) * Mathf.PI * 2f / n;
                Vector2 p0 = c + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * r, p1 = c + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * r;
                if (p0.y < zMin || p1.y < zMin) continue;
                if (Mathf.Abs(p0.x - CastilloX) < 5f && p0.y > c.y) continue;   // paso libre en el eje (por si se sigue hacia el fondo)
                float g0 = suelo(p0.x, p0.y), g1 = suelo(p1.x, p1.y); Vector2 d = p1 - p0; float L = d.magnitude; Quaternion q = Quaternion.Euler(0f, Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg, 0f);
                piedra.Caja(new Vector3(p0.x, g0 + 0.6f, p0.y), new Vector3(0.55f, 1.2f, 0.55f), q, 1f);                       // pilastra
                piedra.Caja(new Vector3(p0.x, g0 + 1.25f, p0.y), new Vector3(0.68f, 0.12f, 0.68f), q, 1f);                     // remate
                if (k % 4 == 0) { piedra.Caja(new Vector3(p0.x, g0 + 1.55f, p0.y), new Vector3(0.38f, 0.5f, 0.38f), q * Quaternion.Euler(0f, 45f, 0f), 1f); piedra.Caja(new Vector3(p0.x, g0 + 1.85f, p0.y), new Vector3(0.22f, 0.14f, 0.22f), q, 1f); }   // jarrón
                Vector2 m = (p0 + p1) / 2f; float gm = (g0 + g1) / 2f;
                piedra.Caja(new Vector3(m.x, gm + 0.08f, m.y), new Vector3(0.42f, 0.16f, L), q, 1f);                            // zócalo
                piedra.Caja(new Vector3(m.x, gm + 1.02f, m.y), new Vector3(0.36f, 0.12f, L), q, 1f);                            // pasamanos
                int nb = Mathf.FloorToInt((L - 0.6f) / 0.32f);
                for (int i = 0; i < nb; i++)
                {
                    float t = (i + 0.5f) / nb; Vector2 bp = Vector2.Lerp(p0, p1, Mathf.Lerp(0.1f, 0.9f, t)); float gb = Mathf.Lerp(g0, g1, t);
                    piedra.Caja(new Vector3(bp.x, gb + 0.55f, bp.y), new Vector3(0.13f, 0.78f, 0.13f), q * Quaternion.Euler(0f, 45f, 0f), 1f);
                    piedra.Caja(new Vector3(bp.x, gb + 0.45f, bp.y), new Vector3(0.2f, 0.26f, 0.2f), q, 1f);                     // panza del balaustre
                }
                tramos++;
            }
            return tramos;
        }
    
        /// Mata de flores suelta (dos planos cruzados con la textura de flores).
        static void Mata(Lote l, Vector3 b, float h, float w, float giro)
        {
            for (int k = 0; k < 2; k++)
            {
                float a = giro + k * Mathf.PI / 2f; Vector3 dx = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * w / 2f;
                Vector3 p0 = b - dx, p1 = b - dx + Vector3.up * h, p2 = b + dx + Vector3.up * h, p3 = b + dx;
                l.Quad(p0, p1, p2, p3, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f));
                l.Quad(p3, p2, p1, p0, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0f));
            }
        }

        string VidaJardin(Transform pa, Vector2 c, float cx, float zIni, float zFin, float zMid, float RMax, System.Func<float, float, float> suelo, System.Func<float, float, bool> dentro,
                          Lote piedra, Lote boj, Lote hierro, Lote[] flores, System.Random rng)
        {
            int rosa = 0, blanca = 4, roja = 0, rosada = 3; rosa = rosada;
            // a) pérgolas de rosas sobre el paseo central: arcos de hierro con rosas trepando
            int pergolas = 0;
            for (float z = zIni + 3f; z < zFin - 6f; z += 20f)
            {
                if (Mathf.Abs(z - zMid) < 18f || !dentro(cx, z)) continue;
                float g = suelo(cx, z), R = 4.9f, hP = 2.6f;
                foreach (var sx in new[] { -1f, 1f }) foreach (var dz in new[] { -0.9f, 0.9f })
                {
                    hierro.Caja(new Vector3(cx + sx * R, g + hP / 2f, z + dz), new Vector3(0.1f, hP, 0.1f), Quaternion.identity, 1f);
                    for (float y = 0.3f; y < hP; y += 0.32f) Mata(flores[(int)(y * 7f) % 2 == 0 ? roja : rosa], new Vector3(cx + sx * (R - 0.08f), g + y - 0.15f, z + dz + 0.05f), 0.42f, 0.5f, (float)rng.NextDouble() * 3f);
                }
                const int S = 14;
                for (int i = 0; i < S; i++)
                {
                    float a0 = i * Mathf.PI / S, a1 = (i + 1) * Mathf.PI / S, am = (a0 + a1) / 2f;
                    // tramo recto entre dos puntos del arco (semielipse): centro, largo y giro sacados de sus extremos
                    Vector3 e0 = new Vector3(cx + Mathf.Cos(a0) * R, g + hP + Mathf.Sin(a0) * R * 0.45f, 0f), e1 = new Vector3(cx + Mathf.Cos(a1) * R, g + hP + Mathf.Sin(a1) * R * 0.45f, 0f);
                    Vector3 pm = (e0 + e1) / 2f; float largo = Vector3.Distance(e0, e1) * 1.08f;
                    float incl = Mathf.Atan2(e1.y - e0.y, e1.x - e0.x) * Mathf.Rad2Deg;
                    foreach (var dz in new[] { -0.9f, 0.9f }) hierro.Caja(new Vector3(pm.x, pm.y, z + dz), new Vector3(largo, 0.09f, 0.09f), Quaternion.Euler(0f, 0f, incl), 1f);
                    if (i % 2 == 0) hierro.Caja(new Vector3(pm.x, pm.y, z), new Vector3(0.06f, 0.06f, 1.9f), Quaternion.identity, 1f);   // travesaños
                    for (int k = 0; k < 2; k++) Mata(flores[(i + k) % 3 == 0 ? blanca : (k == 0 ? roja : rosa)], new Vector3(pm.x, pm.y - 0.2f, z + ((float)rng.NextDouble() - 0.5f) * 1.8f), 0.5f, 0.6f, (float)rng.NextDouble() * 3f);
                }
                pergolas++;
            }
            // b) rosales redondos por dentro de la balaustrada y alrededor de la fuente
            int rosales = 0;
            for (float a = 0.05f; a < Mathf.PI * 2f; a += 6.5f / (RMax - 14f))
            {
                float x = c.x + Mathf.Cos(a) * (RMax - 14f), z = c.y + Mathf.Sin(a) * (RMax - 14f);
                if (!dentro(x, z) || z < zIni + 6f) continue;
                float g = suelo(x, z), r = 0.7f + 0.25f * (float)rng.NextDouble();
                boj.Esfera(new Vector3(x, g + r * 0.55f, z), r, 0.8f, 1.2f);
                int col = rng.Next(3) == 0 ? blanca : (rng.Next(2) == 0 ? roja : rosa);
                for (int k = 0; k < 4; k++) Mata(flores[col], new Vector3(x + ((float)rng.NextDouble() - 0.5f) * r, g + r * 0.7f, z + ((float)rng.NextDouble() - 0.5f) * r), 0.5f, 0.55f, (float)rng.NextDouble() * 3f);
                rosales++;
            }
            for (int k = 0; k < 16; k++)
            {
                float a = k * Mathf.PI * 2f / 16f, x = cx + Mathf.Cos(a) * 11.5f, z = zMid + Mathf.Sin(a) * 11.5f;
                if (Mathf.Abs(Mathf.Cos(a)) > 0.9f || Mathf.Abs(Mathf.Sin(a)) > 0.9f) continue;   // los cuatro accesos libres
                float g = suelo(x, z); boj.Esfera(new Vector3(x, g + 0.45f, z), 0.6f, 0.8f, 1.2f);
                for (int j = 0; j < 3; j++) Mata(flores[k % 2 == 0 ? roja : blanca], new Vector3(x + ((float)rng.NextDouble() - 0.5f) * 0.6f, g + 0.5f, z + ((float)rng.NextDouble() - 0.5f) * 0.6f), 0.45f, 0.5f, (float)rng.NextDouble() * 3f);
                rosales++;
            }
            // c) macetones de piedra con flores a ambos lados del paseo, entre los bancos
            int macetas = 0;
            for (float z = zIni + 23f; z < zFin - 6f; z += 20f)
            {
                if (Mathf.Abs(z - zMid) < 18f) continue;
                foreach (var sx in new[] { -1f, 1f })
                {
                    float x = cx + sx * 4.6f; if (!dentro(x, z)) continue; float g = suelo(x, z);
                    piedra.Caja(new Vector3(x, g + 0.12f, z), new Vector3(0.75f, 0.24f, 0.75f), Quaternion.identity, 1f);             // pie
                    piedra.Caja(new Vector3(x, g + 0.36f, z), new Vector3(0.32f, 0.28f, 0.32f), Quaternion.identity, 1f);             // fuste
                    piedra.Caja(new Vector3(x, g + 0.72f, z), new Vector3(0.8f, 0.48f, 0.8f), Quaternion.identity, 1f);               // cuerpo
                    piedra.Caja(new Vector3(x, g + 0.72f, z), new Vector3(0.8f, 0.48f, 0.8f), Quaternion.Euler(0f, 45f, 0f), 1f);     // (octogonal)
                    piedra.Caja(new Vector3(x, g + 1.0f, z), new Vector3(0.95f, 0.08f, 0.95f), Quaternion.identity, 1f);              // borde
                    int col = rng.Next(flores.Length);
                    for (int k = 0; k < 4; k++) Mata(flores[col], new Vector3(x + ((float)rng.NextDouble() - 0.5f) * 0.5f, g + 0.98f, z + ((float)rng.NextDouble() - 0.5f) * 0.5f), 0.55f, 0.6f, (float)rng.NextDouble() * 3f);
                    macetas++;
                }
            }
            // d) bolas de boj recortado en las esquinas de los cruces y a lo largo de los paños laterales
            int bolas = 0;
            for (int i = 0; i < 70; i++)
            {
                float x = cx + ((float)rng.NextDouble() * 2f - 1f) * (RMax - 25f), z = zIni + 4f + (float)rng.NextDouble() * (zFin - zIni - 10f);
                if (!dentro(x, z) || Mathf.Abs(x - cx) < 12f) continue;
                if (new Vector2(x - cx, z - zMid).magnitude < 20f) continue;
                float g = suelo(x, z), r = 0.5f + 0.3f * (float)rng.NextDouble();
                piedra.Caja(new Vector3(x, g + 0.2f, z), new Vector3(0.7f, 0.4f, 0.7f), Quaternion.identity, 1f);
                boj.Esfera(new Vector3(x, g + 0.4f + r * 0.95f, z), r, 0.95f, 1.2f);
                bolas++;
            }
            // e) faroles a lo largo del paseo (luz cálida) y luciérnagas por todo el jardín; polillas alrededor de los faroles
            int faroles = 0;
            for (float z = zIni + 13f; z < zFin - 6f; z += 40f)
            {
                if (Mathf.Abs(z - zMid) < 18f) continue;
                foreach (var sx in new[] { -1f, 1f }) { float x = cx + sx * 7.6f; if (!dentro(x, z)) continue; Farol2(pa, new Vector3(x, suelo(x, z), z)); Polillas(pa, new Vector3(x, suelo(x, z) + 3.3f, z)); faroles++; }
            }
            Luciernagas(pa, new Vector3(cx, suelo(cx, zMid) + 1.4f, zMid), new Vector3((RMax - 10f) * 2f, 2.2f, zFin - zIni));
            Luciernagas(pa, new Vector3(cx, suelo(cx, zMid) + 0.8f, zMid), new Vector3((RMax - 10f) * 2f, 1.2f, zFin - zIni));
            string relleno = RellenoJardin(pa, c, cx, zIni, zFin, zMid, RMax, suelo, dentro, piedra, boj, flores, rng);
            return relleno + "; " + pergolas + " pérgolas de rosas, " + rosales + " rosales, " + macetas + " macetones con flores, " + bolas + " bolas de boj, " + faroles + " faroles nuevos, luciérnagas y polillas";
        }

        /// Relleno (pedido 2026-11-18: "que no quede casi ningún espacio en blanco"): cada hueco libre de 10 m del empedrado lleva
        /// algo: macizo redondo de flores con borde de boj, cuadro de flores, grupo de topiarios, obelisco o banco con macetones.
        /// Más una franja continua de flores junto a la balaustrada.
        static bool enElipse(float x, float z, float ex, float ez, float a, float b) { return ((x - ex) * (x - ex)) / (a * a) + ((z - ez) * (z - ez)) / (b * b) < 1f; }

        string RellenoJardin(Transform pa, Vector2 c, float cx, float zIni, float zFin, float zMid, float RMax, System.Func<float, float, float> suelo, System.Func<float, float, bool> dentro,
                             Lote piedra, Lote boj, Lote[] flores, System.Random rng)
        {
            System.Func<float, float, bool> libre = (x, z) =>
            {
                // 2026-11-18: el jardín crece 15 m hacia el castillo (sobre el empedrado que ya llegaba hasta ahí)
                bool enCirculo = (x - c.x) * (x - c.x) + (z - c.y) * (z - c.y) < RMax * RMax;
                if (!enCirculo || z < zIni - 15f) return false;
                if (Mathf.Abs(x - cx) < 11f) return false;                                     // paseo central, conos, bancos y pérgolas
                if (new Vector2(x - cx, z - zMid).magnitude < 21f) return false;                // fuente
                if ((x - c.x) * (x - c.x) + (z - c.y) * (z - c.y) > (RMax - 18f) * (RMax - 18f)) return false;   // cipreses y rosales del borde
                foreach (var sx in new[] { -1f, 1f })
                {
                    if (enElipse(x, z, cx + sx * 34f, zIni + 28f, 30f, 24f)) return false;
                    if (enElipse(x, z, cx + sx * 34f, zMid + 26f, 30f, 18f)) return false;
                }
                return true;
            };
            int macizos = 0, cuadros = 0, topiarios = 0, obeliscos = 0, bancos = 0;
            const float Paso = 8f;   // más apretado (antes 10 m)
            int bebederos = 0;
            for (float z = zIni - 12f; z < zFin; z += Paso)
                for (float x = cx - RMax; x < cx + RMax; x += Paso)
                {
                    float px = x + ((float)rng.NextDouble() - 0.5f) * 2f, pz = z + ((float)rng.NextDouble() - 0.5f) * 2f;
                    if (!libre(px, pz) || !libre(px - 4f, pz) || !libre(px + 4f, pz) || !libre(px, pz - 4f) || !libre(px, pz + 4f)) continue;
                    float g = suelo(px, pz);
                    int tipo = rng.Next(11);
                    if (tipo == 10)
                    {
                        // bebedero de pájaros: pie de piedra, taza con agua y flores alrededor
                        piedra.Caja(new Vector3(px, g + 0.1f, pz), new Vector3(1.1f, 0.2f, 1.1f), Quaternion.Euler(0f, 45f, 0f), 1f);
                        piedra.Caja(new Vector3(px, g + 0.6f, pz), new Vector3(0.3f, 0.85f, 0.3f), Quaternion.identity, 1f);
                        piedra.Caja(new Vector3(px, g + 1.08f, pz), new Vector3(1.3f, 0.16f, 1.3f), Quaternion.identity, 1f);
                        piedra.Caja(new Vector3(px, g + 1.08f, pz), new Vector3(1.3f, 0.16f, 1.3f), Quaternion.Euler(0f, 45f, 0f), 1f);
                        var agua = GameObject.CreatePrimitive(PrimitiveType.Cylinder); Object.Destroy(agua.GetComponent<Collider>());
                        agua.name = "coop_bebedero_agua"; agua.transform.SetParent(pa, false); agua.transform.localPosition = new Vector3(px, g + 1.17f, pz); agua.transform.localScale = new Vector3(1.15f, 0.01f, 1.15f);
                        agua.GetComponent<Renderer>().sharedMaterial = matAguaFuente;
                        BandaFlores(flores, rng, suelo, (fx, fz) => { float d = new Vector2(fx - px, fz - pz).magnitude; return d < 2.6f && d > 0.9f; }, px - 2.7f, px + 2.7f, pz - 2.7f, pz + 2.7f, 0.42f, rng.Next(flores.Length), true);
                        bebederos++;
                    }
                    else if (tipo < 4)
                    {
                        // macizo redondo: aro de boj, flores apretadas por dentro y un cono o bola al centro
                        float r = 3.2f + (float)rng.NextDouble() * 0.8f; var pts = new List<Vector2>();
                        for (int k = 0; k <= 28; k++) { float a = k / 28f * Mathf.PI * 2f; pts.Add(new Vector2(px + Mathf.Cos(a) * r, pz + Mathf.Sin(a) * r)); }
                        Seto(boj, suelo, pts, 0.45f, 0.5f);
                        int col = rng.Next(flores.Length); float rr = r - 0.5f;
                        BandaFlores(flores, rng, suelo, (fx, fz) => { float d = new Vector2(fx - px, fz - pz).magnitude; return d < rr && d > 1.1f; }, px - rr, px + rr, pz - rr, pz + rr, 0.42f, col, rng.Next(2) == 0);
                        if (rng.Next(2) == 0) boj.Cono(new Vector3(px, g - 0.05f, pz), 0.75f, 2.4f + (float)rng.NextDouble(), 16, 2f); else boj.Esfera(new Vector3(px, g + 0.8f, pz), 0.85f, 0.9f, 1.2f);
                        macizos++;
                    }
                    else if (tipo < 6)
                    {
                        // cuadro de flores de 7 × 7 m con borde de boj y cuatro bolas en las esquinas
                        float h = 3.5f; var pts = new List<Vector2> { new Vector2(px - h, pz - h), new Vector2(px + h, pz - h), new Vector2(px + h, pz + h), new Vector2(px - h, pz + h), new Vector2(px - h, pz - h) };
                        var densos = new List<Vector2>();
                        for (int k = 0; k < 4; k++) for (int j = 0; j <= 12; j++) densos.Add(Vector2.Lerp(pts[k], pts[k + 1], j / 12f));
                        Seto(boj, suelo, densos, 0.4f, 0.45f);
                        int c1 = rng.Next(flores.Length), c2 = rng.Next(flores.Length);
                        BandaFlores(flores, rng, suelo, (fx, fz) => Mathf.Abs(fx - px) < h - 0.4f && Mathf.Abs(fz - pz) < h - 0.4f, px - h, px + h, pz - h, pz + h, 0.42f, ((int)((px + pz) / 7f) % 2 == 0) ? c1 : c2, true);
                        foreach (var e in new[] { new Vector2(-h, -h), new Vector2(h, -h), new Vector2(h, h), new Vector2(-h, h) })
                            boj.Esfera(new Vector3(px + e.x, suelo(px + e.x, pz + e.y) + 0.55f, pz + e.y), 0.55f, 0.95f, 1.2f);
                        cuadros++;
                    }
                    else if (tipo < 8)
                    {
                        // grupo de topiarios: un cono alto, dos bolas y una espiral de bolas pequeñas, sobre un círculo de flores
                        boj.Cono(new Vector3(px, g - 0.05f, pz), 1.1f, 3.6f + (float)rng.NextDouble(), 18, 2f);
                        for (int k = 0; k < 2; k++) { float a = k * Mathf.PI + 0.6f; boj.Esfera(new Vector3(px + Mathf.Cos(a) * 2.2f, g + 0.7f, pz + Mathf.Sin(a) * 2.2f), 0.75f, 0.9f, 1.2f); }
                        for (int k = 0; k < 4; k++) { float a = k * 1.7f + 2f, h2 = 0.5f + k * 0.55f; boj.Esfera(new Vector3(px + Mathf.Cos(a) * 1.9f, g + h2, pz + Mathf.Sin(a) * 1.9f), 0.35f, 1f, 1.2f); }
                        BandaFlores(flores, rng, suelo, (fx, fz) => { float d = new Vector2(fx - px, fz - pz).magnitude; return d < 3.4f && d > 2.6f; }, px - 3.5f, px + 3.5f, pz - 3.5f, pz + 3.5f, 0.45f, rng.Next(flores.Length), false);
                        topiarios++;
                    }
                    else if (tipo < 9)
                    {
                        // obelisco de piedra sobre basa escalonada, con una bola en la punta y flores alrededor
                        piedra.Caja(new Vector3(px, g + 0.15f, pz), new Vector3(1.8f, 0.3f, 1.8f), Quaternion.identity, 1f);
                        piedra.Caja(new Vector3(px, g + 0.45f, pz), new Vector3(1.3f, 0.3f, 1.3f), Quaternion.identity, 1f);
                        for (int k = 0; k < 6; k++) { float w = 0.8f - k * 0.1f; piedra.Caja(new Vector3(px, g + 0.9f + k * 0.55f, pz), new Vector3(w, 0.56f, w), Quaternion.identity, 1f); }
                        piedra.Caja(new Vector3(px, g + 4.2f, pz), new Vector3(0.28f, 0.3f, 0.28f), Quaternion.Euler(45f, 45f, 0f), 1f);
                        BandaFlores(flores, rng, suelo, (fx, fz) => { float d = new Vector2(fx - px, fz - pz).magnitude; return d < 2.6f && d > 1.3f; }, px - 2.7f, px + 2.7f, pz - 2.7f, pz + 2.7f, 0.42f, rng.Next(flores.Length), false);
                        obeliscos++;
                    }
                    else
                    {
                        // banco de piedra con dos macetones floridos a los lados y un rosal detrás
                        float a = (float)rng.NextDouble() * 360f; var q = Quaternion.Euler(0f, a, 0f);
                        piedra.Caja(new Vector3(px, g + 0.47f, pz), new Vector3(2f, 0.1f, 0.55f), q, 1f);
                        foreach (var dx in new[] { -0.75f, 0.75f }) piedra.Caja(new Vector3(px, g + 0.22f, pz) + q * new Vector3(dx, 0f, 0f), new Vector3(0.22f, 0.45f, 0.45f), q, 1f);
                        foreach (var dx in new[] { -1.7f, 1.7f })
                        {
                            Vector3 m = new Vector3(px, g, pz) + q * new Vector3(dx, 0f, 0f);
                            piedra.Caja(m + Vector3.up * 0.35f, new Vector3(0.6f, 0.7f, 0.6f), q, 1f);
                            int col = rng.Next(flores.Length);
                            for (int k = 0; k < 3; k++) Mata(flores[col], m + new Vector3(((float)rng.NextDouble() - 0.5f) * 0.4f, 0.68f, ((float)rng.NextDouble() - 0.5f) * 0.4f), 0.5f, 0.55f, (float)rng.NextDouble() * 3f);
                        }
                        Vector3 tras = new Vector3(px, g, pz) + q * new Vector3(0f, 0f, 1.2f);
                        boj.Esfera(tras + Vector3.up * 0.6f, 0.8f, 0.8f, 1.2f);
                        for (int k = 0; k < 4; k++) Mata(flores[0], tras + new Vector3(((float)rng.NextDouble() - 0.5f) * 0.9f, 0.85f, ((float)rng.NextDouble() - 0.5f) * 0.9f), 0.5f, 0.55f, (float)rng.NextDouble() * 3f);
                        bancos++;
                    }
                }
            // franja de flores continua por dentro de la balaustrada (entre los cipreses y los rosales)
            float rIn = RMax - 12.5f, rOut = RMax - 10f;
            BandaFlores(flores, rng, suelo, (fx, fz) => { float d = new Vector2(fx - c.x, fz - c.y).magnitude; return d > rIn && d < rOut && fz > zIni + 4f && dentro(fx, fz); },
                        c.x - RMax, c.x + RMax, zIni, zFin + 4f, 0.5f, 0, true);
            return bebederos + " bebederos de pájaros, " + macizos + " macizos redondos, " + cuadros + " cuadros de flores, " + topiarios + " grupos de topiarios, " + obeliscos + " obeliscos, " + bancos + " bancos con macetones, franja de flores en el borde";
        }

        static Material matBichos;
        static Material MatBichos()
        {
            if (matBichos != null) return matBichos;
            const int N = 32; var t = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color[N * N];
            for (int y = 0; y < N; y++) for (int x = 0; x < N; x++) { float u = (x + 0.5f) / N * 2f - 1f, v = (y + 0.5f) / N * 2f - 1f; float a = Mathf.Clamp01(1f - Mathf.Sqrt(u * u + v * v)); a = a * a * a; px[y * N + x] = new Color(1f, 1f, 1f, a); }
            t.SetPixels(px); t.Apply();
            var sh = Shader.Find("Particles/Additive"); if (sh == null) sh = Shader.Find("Legacy Shaders/Particles/Additive");
            matBichos = new Material(sh) { mainTexture = t };
            return matBichos;
        }

        /// Luciérnagas: puntos verdes amarillentos que se encienden y apagan y vagan despacio a baja altura.
        void Luciernagas(Transform pa, Vector3 centro, Vector3 tam)
        {
            var g = new GameObject("coop_jardin_luciernagas"); g.transform.SetParent(pa, false); g.transform.localPosition = centro;
            var ps = g.AddComponent<ParticleSystem>();
            var main = ps.main; main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 9f); main.startSpeed = 0f; main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.2f);
            main.maxParticles = 900; main.simulationSpace = ParticleSystemSimulationSpace.World; main.startColor = new Color(0.75f, 1f, 0.35f, 1f); main.scalingMode = ParticleSystemScalingMode.Shape;
            var em = ps.emission; em.rateOverTime = 120f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.box = tam;
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.x = new ParticleSystem.MinMaxCurve(-0.35f, 0.35f); vel.y = new ParticleSystem.MinMaxCurve(-0.12f, 0.15f); vel.z = new ParticleSystem.MinMaxCurve(-0.35f, 0.35f);
            var col = ps.colorOverLifetime; col.enabled = true;
            var gr = new Gradient();
            gr.SetKeys(new[] { new GradientColorKey(new Color(0.7f, 1f, 0.3f), 0f), new GradientColorKey(new Color(1f, 0.95f, 0.4f), 1f) },
                       new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(0.1f, 0.3f), new GradientAlphaKey(1f, 0.45f), new GradientAlphaKey(0.05f, 0.62f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
            col.color = gr;
            var noise = ps.noise; noise.enabled = true; noise.strength = 0.6f; noise.frequency = 0.25f; noise.scrollSpeed = 0.2f;
            g.GetComponent<ParticleSystemRenderer>().sharedMaterial = MatBichos();
        }

        /// Polillas: motas pálidas que revolotean alrededor de la luz de un farol.
        void Polillas(Transform pa, Vector3 p)
        {
            var g = new GameObject("coop_jardin_polillas"); g.transform.SetParent(pa, false); g.transform.localPosition = p;
            var ps = g.AddComponent<ParticleSystem>();
            var main = ps.main; main.startLifetime = new ParticleSystem.MinMaxCurve(2f, 4f); main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.9f); main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.08f);
            main.maxParticles = 14; main.startColor = new Color(1f, 0.9f, 0.7f, 0.8f); main.simulationSpace = ParticleSystemSimulationSpace.Local;
            var em = ps.emission; em.rateOverTime = 4f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.5f;
            var noise = ps.noise; noise.enabled = true; noise.strength = 1.6f; noise.frequency = 1.4f;
            g.GetComponent<ParticleSystemRenderer>().sharedMaterial = MatBichos();
        }
    }
}

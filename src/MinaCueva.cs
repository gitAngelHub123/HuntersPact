// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using System.Collections.Generic;
using UnityEngine;

namespace WitchHuntCoop
{
    /// Guarida del jefe como CUEVA NATURAL (2026-11-18): reemplaza la sala de caja de 64 × 60 × 14 m.
    /// Bóveda de roca irregular de ~220 × 224 m y hasta 56 m de alto (2026-11-19: el doble; antes 110 × 112 × 28) (crece al este, oeste y norte; la entrada sur queda
    /// donde estaba), hecha con ruido en varias escalas y estratos; piso con desniveles y escombros que suben hacia
    /// las paredes; estalactitas, estalagmitas y columnas naturales; charcos, goteo, polvo flotando, un rayo de luz
    /// fría que entra por una grieta del techo, antorchas con fuego real y restos de andamios de los mineros.
    /// Todo es geometría propia del mod (sin modelos del juego salvo rocas, vagonetas y barriles ya usados en las minas).
    public partial class Mundo
    {
        // medidas locales (respecto al origen de la guarida, que es la boca sur del pasillo)
        // 2026-11-19: el usuario pidió la cueva el DOBLE de ancho, de largo y de alto → todo se escala con CuevaE (la entrada y el pasillo no cambian)
        public const float CuevaE = 2f;
        const float CuevaAX = 55f * CuevaE, CuevaAZ = 56f * CuevaE, CuevaZ0 = 18f, CuevaH = 28f * CuevaE, CuevaHPared = 10f * CuevaE, CuevaN = 3f;
        static float CuevaZC { get { return CuevaZ0 + CuevaAZ; } }
        Material matCueva, matTunel, matCuevaSuelo, matCharco, matRayo, matGota, matPolvo, matGrieta;
        float semillaCueva;
        MeshCollider colCueva;

        // ---------------- Materiales ----------------

        void MaterialesCueva()
        {
            if (matCueva != null) return;
            Texture2D roca = null, rocaN = null, tierra = null, tierraN = null, granito = null, granitoN = null;
            var t = Anclaje.Principal();
            if (t != null && t.terrainData != null)
                foreach (var sp in t.terrainData.splatPrototypes)
                {
                    if (sp.texture == null) continue;
                    string n = sp.texture.name.ToLowerInvariant();
                    if (roca == null && (n.Contains("rock") || n.Contains("cliff") || n.Contains("stone"))) { roca = sp.texture; rocaN = sp.normalMap; }
                    if (tierra == null && (n.Contains("dirt") || n.Contains("mud") || n.Contains("soil"))) { tierra = sp.texture; tierraN = sp.normalMap; }
                    // roca hecha para repetirse (granito de cañón; si no, la roca de Zion)
                    if (n.Contains("granite") || (granito == null && n.Contains("zionrock"))) { granito = sp.texture; granitoN = sp.normalMap; }
                }
            // roca: primero el material de las rocas reales del juego (fotográfico, con relieve); si no está, la textura del terreno
            Material rocaJuego = null; string nombreRoca = null;
            foreach (var r in Object.FindObjectsOfType<MeshRenderer>())
            {
                string bn = Catalogo.BaseDe(r.transform.root.name);
                if ((bn == "Rock13" || bn == "Rock10" || bn == "Rock1" || bn == "Rock5") && r.sharedMaterial != null && r.sharedMaterial.mainTexture != null)
                { rocaJuego = r.sharedMaterial; nombreRoca = bn + "/" + r.sharedMaterial.name; break; }
            }
            if (rocaJuego != null)
            {
                matCueva = new Material(rocaJuego) { name = "coop_cueva_roca" };
                matCueva.color = new Color(0.7f, 0.66f, 0.62f);
                matCueva.mainTextureScale = Vector2.one; matCueva.mainTextureOffset = Vector2.zero;
                matCueva.SetFloat("_Glossiness", 0.07f);
                roca = null;
            }
            else
            // roca: textura fotográfica del terreno del juego (hecha para repetirse) + mapa de relieve + detalle; húmeda (algo de brillo)
            if (matCueva != null) { } else if (roca != null) matCueva = Estandar("coop_cueva_roca", roca, rocaN, new Color(0.46f, 0.44f, 0.43f), 0.3f, 1.4f, true);
            else { matCueva = new Material(matRoca) { name = "coop_cueva_roca" }; matCueva.color = new Color(0.55f, 0.53f, 0.52f); matCueva.SetFloat("_Glossiness", 0.3f); }
            if (tierra != null) matCuevaSuelo = Estandar("coop_cueva_suelo", tierra, tierraN, new Color(0.5f, 0.44f, 0.37f), 0.04f, 1.5f, false);
            else { matCuevaSuelo = new Material(matSuelo) { name = "coop_cueva_suelo" }; matCuevaSuelo.SetFloat("_Glossiness", 0.18f); }
            // túneles (y bóveda): granito que se repite sin costuras; el atlas de Rock1 de cerca se veía como lunares de piedra
            if (granito != null) matTunel = Estandar("coop_tunel_roca", granito, granitoN, new Color(0.62f, 0.56f, 0.5f), 0.08f, 1.6f, true);
            else matTunel = matCueva;
            matCueva = matTunel;
            // bajo tierra no hay cielo que reflejar: sin reflejos del cielo (daban un brillo gris azulado, como nieve)
            foreach (var mm in new[] { matCueva, matTunel, matCuevaSuelo })
            { mm.SetFloat("_GlossyReflections", 0f); mm.EnableKeyword("_GLOSSYREFLECTIONS_OFF"); }
            plugin.Log("Minas: cueva y túneles con roca '" + (granito != null ? granito.name : nombreRoca ?? (roca != null ? roca.name : "propia")) + "' y suelo '" + (tierra != null ? tierra.name : "propio") + "'");

            // agua quieta: casi negra y muy lisa; sin reflejo del cielo (bajo tierra se vería el cielo), solo brillos de las luces
            matCharco = new Material(Shader.Find("Standard")) { name = "coop_cueva_charco", color = new Color(0.04f, 0.045f, 0.05f, 0.45f) };
            // modo transparente del Standard: se ve el fondo oscurecido debajo
            matCharco.SetFloat("_Mode", 3f); matCharco.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            matCharco.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha); matCharco.SetInt("_ZWrite", 0);
            matCharco.DisableKeyword("_ALPHATEST_ON"); matCharco.DisableKeyword("_ALPHABLEND_ON"); matCharco.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            matCharco.renderQueue = 3000;
            matCharco.SetFloat("_Glossiness", 0.97f); matCharco.SetFloat("_Metallic", 0f);
            matCharco.SetFloat("_GlossyReflections", 0f); matCharco.EnableKeyword("_GLOSSYREFLECTIONS_OFF");

            // rayo de luz: degradado vertical aditivo muy tenue
            var deg = new Texture2D(4, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 64; y++) { float a = Mathf.Pow(y / 63f, 0.7f) * (1f - Mathf.Pow(y / 63f, 8f)); for (int x = 0; x < 4; x++) deg.SetPixel(x, y, new Color(1f, 1f, 1f, a)); }
            deg.Apply();
            matRayo = new Material(Shader.Find("Particles/Additive")) { name = "coop_cueva_rayo", mainTexture = deg };
            matRayo.SetColor("_TintColor", new Color(0.1f, 0.12f, 0.16f, 0.07f));
            var punto = Punto(16);
            matGota = new Material(Shader.Find("Particles/Alpha Blended")) { name = "coop_cueva_gota", mainTexture = punto };
            matGota.SetColor("_TintColor", new Color(0.75f, 0.8f, 0.85f, 0.55f));
            matPolvo = new Material(Shader.Find("Particles/Alpha Blended")) { name = "coop_cueva_polvo", mainTexture = punto };
            matPolvo.SetColor("_TintColor", new Color(0.8f, 0.76f, 0.68f, 0.28f));
            matGrieta = new Material(Shader.Find("Unlit/Color")) { name = "coop_cueva_grieta", color = new Color(0.32f, 0.38f, 0.5f) };
        }

        static Texture2D Punto(int n)
        {
            var tx = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                float d = new Vector2(x - (n - 1) / 2f, y - (n - 1) / 2f).magnitude / (n / 2f);
                tx.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(1f - d * d)));
            }
            tx.Apply(); return tx;
        }

        // ---------------- Ruido y forma ----------------

        static float Ruido3(float x, float y, float z)
        {
            return (Mathf.PerlinNoise(x, y) + Mathf.PerlinNoise(y + 31.7f, z + 3.1f) + Mathf.PerlinNoise(z + 57.1f, x + 11.3f)) / 3f;
        }

        /// Ruido fractal 3D aproximado, más o menos entre −1 y 1.
        static float Fractal(Vector3 p, float s)
        {
            float suma = 0f, a = 1f, f = 1f, tot = 0f;
            for (int o = 0; o < 4; o++) { suma += a * (Ruido3(p.x * f + s, p.y * f + s * 0.7f, p.z * f + s * 1.3f) - 0.5f); tot += a; a *= 0.5f; f *= 2.07f; }
            return suma / tot * 3.2f;
        }

        static Vector2 Contorno(float th)
        {
            float c = Mathf.Cos(th), s = Mathf.Sin(th), e = 2f / CuevaN;
            return new Vector2(Mathf.Sign(c) * Mathf.Pow(Mathf.Abs(c), e) * CuevaAX, CuevaZC + Mathf.Sign(s) * Mathf.Pow(Mathf.Abs(s), e) * CuevaAZ);
        }

        /// 0 en el centro de la sala, 1 en la pared (sin contar el ruido).
        static float RadioCueva(float x, float z)
        {
            return Mathf.Pow(Mathf.Pow(Mathf.Abs(x) / CuevaAX, CuevaN) + Mathf.Pow(Mathf.Abs(z - CuevaZC) / CuevaAZ, CuevaN), 1f / CuevaN);
        }

        /// Altura del piso (local) en x, z: ondulación suave, llano en la entrada y en el centro, escombros que suben hacia las paredes.
        float AlturaSueloCueva(float x, float z)
        {
            var p = new Vector3(x, 0f, z);
            float h = 0.22f * Fractal(p / 9f, semillaCueva + 3f) + 0.07f * Fractal(p / 1.8f, semillaCueva + 8f);
            float r = RadioCueva(x, z);
            if (r > 0.84f) h += Mathf.Pow((r - 0.84f) / 0.22f, 1.6f) * 4.5f * (0.75f + 0.5f * Ruido3(x / 6f + semillaCueva, z / 6f, 1.7f));
            float dCentro = new Vector2(x, z - CuevaZC).magnitude;
            if (dCentro < 36f) h *= Mathf.Lerp(0.35f, 1f, dCentro / 36f);   // arena del jefe casi llana
            float dBoca = new Vector2(x, (z - CuevaZ0) * 0.8f).magnitude;
            h *= Mathf.SmoothStep(0f, 1f, (dBoca - 3f) / 10f);              // la entrada empalma a ras con el pasillo
            return Mathf.Max(0f, h + 0.04f);
        }

        Vector3 PuntoBoveda(float u, float v)
        {
            float th = u * Mathf.PI * 2f - Mathf.PI / 2f;   // u = 0: sur (la entrada)
            Vector2 c = Contorno(th);
            const float vp = 0.32f;
            float s, y;
            if (v <= vp) { float w = v / vp; s = 1f - 0.05f * w; y = -1.2f + (CuevaHPared + 1.2f) * w; }
            else { float w = (v - vp) / (1f - vp), phi = w * Mathf.PI / 2f; s = 0.95f * Mathf.Cos(phi); y = CuevaHPared + (CuevaH - CuevaHPared) * Mathf.Sin(phi); }
            var b = new Vector3(c.x * s, y, CuevaZC + (c.y - CuevaZC) * s);
            var d = new Vector3(b.x / CuevaAX, Mathf.Max(0f, y - 6f * CuevaE) / (CuevaH - 6f * CuevaE) * 1.4f, (b.z - CuevaZC) / CuevaAZ);
            if (d.sqrMagnitude < 1e-4f) d = Vector3.up;
            d.Normalize();
            float n = Fractal(b / (17f * CuevaE), semillaCueva) * 5.5f * CuevaE + Fractal(b / 5.5f, semillaCueva + 9f) * 1.6f + Fractal(b / 1.7f, semillaCueva + 21f) * 0.45f;
            // estratos: capas horizontales marcadas en las paredes (se pierden hacia el techo)
            float capa = Mathf.Sin(y * 1.9f + Fractal(b / 9f, semillaCueva + 4f) * 3f);
            n += 0.45f * Mathf.Sign(capa) * Mathf.Pow(Mathf.Abs(capa), 0.4f) * (1f - Mathf.Clamp01((y - 11f * CuevaE) / (8f * CuevaE)));
            // alrededor de la entrada la pared se vuelve plana para empalmar con el marco del pasillo
            float k = 1f;
            if (b.z < CuevaZ0 + 14f) k = Mathf.SmoothStep(0f, 1f, (new Vector2(b.x, (y - 1.7f) * 1.25f).magnitude - 5.5f) / 9f);
            b += d * n * k;
            if (k < 1f && b.z < CuevaZC) b.z = Mathf.Lerp(CuevaZ0, b.z, k);
            return b;
        }

        // ---------------- Construcción ----------------

        /// Construye la sala-cueva bajo «red» (origen = boca sur del pasillo). hP y alto = medidas del hueco del pasillo.
        void ConstruirCueva(Transform red, Vector3 origen, System.Random rng, float hP, float altoPasillo, GameObject piedra)
        {
            MaterialesCueva();
            semillaCueva = (float)rng.NextDouble() * 500f;

            // 1) bóveda: paredes y techo en una sola malla (vista desde dentro), con colisión
            const int NU = 400, NV = 90;   // más fina que antes: la bóveda es el doble de grande
            var mb = new Malla();
            var rejilla = new Vector3[NU + 1, NV + 1];
            float perim = 0f; { Vector2 a0 = Contorno(-Mathf.PI / 2f); for (int i = 1; i <= 200; i++) { var a1 = Contorno(i / 200f * Mathf.PI * 2f - Mathf.PI / 2f); perim += (a1 - a0).magnitude; a0 = a1; } }
            for (int i = 0; i <= NU; i++) for (int j = 0; j <= NV; j++)
                rejilla[i, j] = PuntoBoveda(i == NU ? 0f : i / (float)NU, j / (float)NV);
            // el hueco del pasillo: no se dibujan los cuadros que caen en él (con margen; lo tapa el collar)
            float largoV = CuevaHPared + 1.2f + (CuevaH - CuevaHPared) * 1.2f;
            // UV: paredes con la vuelta del contorno y la altura; techo proyectado desde abajo (x, z) para que la textura
            // no se comprima hacia el centro de la bóveda (salían rayas). La fila de unión se duplica.
            int jT = Mathf.RoundToInt(NV * 0.55f);
            int[,] idxP = new int[NU + 1, NV + 1], idxT = new int[NU + 1, NV + 1];
            for (int i = 0; i <= NU; i++) for (int j = 0; j <= NV; j++)
            {
                if (j <= jT) idxP[i, j] = mb.V(rejilla[i, j], new Vector2(i / (float)NU * perim / 7f, j / (float)NV * largoV / 7f));
                if (j >= jT) idxT[i, j] = mb.V(rejilla[i, j], new Vector2(rejilla[i, j].x, rejilla[i, j].z) / 7f);
            }
            // (2026-11-19) hueco por SOLAPE con la puerta, no por el centro del cuadro: con la cueva al doble los cuadros frente a la
            // entrada miden ~7 m de ancho (la superelipse avanza rápido cerca del eje) y su centro quedaba fuera → tapaban la puerta
            float huecoX = hP + 3.2f, huecoY = altoPasillo + 3.2f;
            for (int i = 0; i < NU; i++) for (int j = 0; j < NV; j++)
            {
                Vector3 cen = (rejilla[i, j] + rejilla[i + 1, j + 1]) * 0.5f;
                Vector3 q0 = rejilla[i, j], q1 = rejilla[i + 1, j], q2 = rejilla[i, j + 1], q3 = rejilla[i + 1, j + 1];
                float mnx = Mathf.Min(Mathf.Min(q0.x, q1.x), Mathf.Min(q2.x, q3.x)), mxx = Mathf.Max(Mathf.Max(q0.x, q1.x), Mathf.Max(q2.x, q3.x));
                float mny = Mathf.Min(Mathf.Min(q0.y, q1.y), Mathf.Min(q2.y, q3.y)), mxy = Mathf.Max(Mathf.Max(q0.y, q1.y), Mathf.Max(q2.y, q3.y));
                if (cen.z < CuevaZ0 + 3f && mnx < hP + 0.15f && mxx > -hP - 0.15f && mny < altoPasillo + 0.15f)
                {
                    huecoX = Mathf.Max(huecoX, Mathf.Max(Mathf.Abs(mnx), Mathf.Abs(mxx)) + 0.4f); huecoY = Mathf.Max(huecoY, mxy + 0.4f);
                    continue;
                }
                var idx = j < jT ? idxP : idxT;
                mb.Tri(idx[i, j], idx[i + 1, j], idx[i, j + 1]);
                mb.Tri(idx[i + 1, j], idx[i + 1, j + 1], idx[i, j + 1]);
            }
            // collar plano alrededor del hueco (mirando hacia dentro de la cueva)
            float zc = CuevaZ0 + 0.12f;   // el collar cubre todo lo recortado (huecoX, huecoY)
            mb.Quad(new Vector3(-huecoX, 0f, zc), new Vector3(-hP, 0f, zc), new Vector3(-hP, huecoY, zc), new Vector3(-huecoX, huecoY, zc), 7f);
            mb.Quad(new Vector3(hP, 0f, zc), new Vector3(huecoX, 0f, zc), new Vector3(huecoX, huecoY, zc), new Vector3(hP, huecoY, zc), 7f);
            mb.Quad(new Vector3(-hP, altoPasillo, zc), new Vector3(hP, altoPasillo, zc), new Vector3(hP, huecoY, zc), new Vector3(-hP, huecoY, zc), 7f);
            Debug.Log("[WitchHuntCoop] Cueva: hueco de la puerta ±" + huecoX.ToString("0.0") + " m × " + huecoY.ToString("0.0") + " m (collar)");
            var boveda = mb.Crear("coop_cueva_boveda", red, matCueva, true);
            colCueva = boveda.GetComponent<MeshCollider>();

            // 2) piso con desniveles (sin el tramo que ocupa el pasillo)
            var ms = new Malla();
            const float X0 = -CuevaAX - 6f, X1 = CuevaAX + 6f, Z0 = 12f, Z1 = CuevaZ0 + 2f * CuevaAZ + 6f, paso = 1.25f;   // < 65 535 vértices
            int nx = Mathf.RoundToInt((X1 - X0) / paso), nz = Mathf.RoundToInt((Z1 - Z0) / paso);
            var ids = new int[nx + 1, nz + 1];
            for (int i = 0; i <= nx; i++) for (int j = 0; j <= nz; j++)
            {
                float x = X0 + i * paso, z = Z0 + j * paso;
                ids[i, j] = ms.V(new Vector3(x, AlturaSueloCueva(x, z), z), new Vector2(x / 3.5f, z / 3.5f));
            }
            for (int i = 0; i < nx; i++) for (int j = 0; j < nz; j++)
            {
                float cx = X0 + (i + 0.5f) * paso, cz = Z0 + (j + 0.5f) * paso;
                if (cz < CuevaZ0 + 0.3f && Mathf.Abs(cx) < hP + 0.6f) continue;
                if (RadioCueva(cx, cz) > 1.16f) continue;
                // cara hacia arriba: cross(b-a, c-a) con +z luego +x
                ms.Tri(ids[i, j], ids[i, j + 1], ids[i + 1, j]);
                ms.Tri(ids[i + 1, j], ids[i, j + 1], ids[i + 1, j + 1]);
            }
            ms.Crear("coop_cueva_suelo", red, matCuevaSuelo, true);

            // 3) columnas naturales (estalactita + estalagmita unidas) que llegan al techo
            var mc = new Malla();
            var columnas = new[] { new Vector2(-60f, CuevaZC - 52f), new Vector2(62f, CuevaZC - 44f), new Vector2(-72f, CuevaZC + 18f), new Vector2(74f, CuevaZC + 26f), new Vector2(-38f, CuevaZC + 74f), new Vector2(44f, CuevaZC + 70f),
                                   new Vector2(-84f, CuevaZC - 16f), new Vector2(86f, CuevaZC - 8f), new Vector2(6f, CuevaZC + 88f), new Vector2(-30f, CuevaZC - 78f) };
            var huecos = new List<Vector3>();   // sitios ocupados (x, z, radio) para no amontonar estalagmitas
            foreach (var c in columnas)
            {
                float y0 = AlturaSueloCueva(c.x, c.y), y1 = AlturaTecho(red, c.x, c.y);
                Columna(mc, new Vector3(c.x, y0 - 0.6f, c.y), y1 + 1.2f - (y0 - 0.6f), (1.3f + (float)rng.NextDouble() * 0.9f) * 1.7f, (3.6f + (float)rng.NextDouble() * 1.6f) * 1.7f, rng);
                huecos.Add(new Vector3(c.x, c.y, 10f));
            }
            mc.Crear("coop_cueva_columnas", red, matCueva, true);

            // 4) estalactitas en racimos (sin colisión: están en lo alto)
            var mt = new Malla();
            var gotas = new List<Vector3>();
            int nEst = 0;
            int jMin = Mathf.RoundToInt(NV * 0.33f);
            for (int g = 0; g < 115; g++)
            {
                int ci = rng.Next(NU), cj = jMin + 1 + rng.Next(NV - jMin - 3);
                int cuantas = 1 + rng.Next(5);
                for (int k = 0; k < cuantas; k++)
                {
                    int i = Mathf.Clamp(ci + rng.Next(5) - 2, 0, NU - 1), j = Mathf.Clamp(cj + rng.Next(5) - 2, jMin, NV - 1);
                    Vector3 p = rejilla[i, j];
                    if (Mathf.Abs(p.x) < hP + 4f && p.z < CuevaZ0 + 6f) continue;
                    float maxL = Mathf.Max(0.8f, p.y - 6.5f);
                    float L = Mathf.Min(maxL, 0.8f + Mathf.Pow((float)rng.NextDouble(), 2.2f) * 7f * CuevaE);
                    var dir = (Vector3.down + new Vector3((float)rng.NextDouble() - 0.5f, 0f, (float)rng.NextDouble() - 0.5f) * 0.12f).normalized;
                    Cono(mt, p + Vector3.up * 0.7f, dir, L + 0.7f, (L + 0.7f) * (0.13f + 0.09f * (float)rng.NextDouble()) + 0.1f, 9, rng, 1.25f);
                    nEst++;
                    if (gotas.Count < 22 && L > 2f && rng.NextDouble() < 0.35) gotas.Add(p + dir * (L + 0.05f));
                }
            }
            mt.Crear("coop_cueva_estalactitas", red, matCueva, false);

            // 5) estalagmitas (con colisión), lejos de la entrada, de la arena y de los altares
            var mg = new Malla();
            int nMit = 0;
            for (int intento = 0; intento < 2600 && nMit < 240; intento++)
            {
                float x = ((float)rng.NextDouble() * 2f - 1f) * CuevaAX, z = CuevaZ0 + (float)rng.NextDouble() * CuevaAZ * 2f;
                float r = RadioCueva(x, z);
                if (r < 0.5f || r > 0.95f) continue;
                if (Mathf.Abs(x) < 10f && z < CuevaZ0 + 60f) continue;
                if (new Vector2(x, z - CuevaZC).magnitude < 36f) continue;
                bool ocupado = false;
                foreach (var h0 in huecos) if (new Vector2(x - h0.x, z - h0.y).magnitude < h0.z) { ocupado = true; break; }
                foreach (var a in SitiosAltaresCueva) if (new Vector2(x - a.x, z - a.y).magnitude < 5.5f) { ocupado = true; break; }
                if (ocupado) continue;
                int racimo = 1 + rng.Next(4);
                for (int k = 0; k < racimo; k++)
                {
                    float xx = x + ((float)rng.NextDouble() - 0.5f) * 3f, zz = z + ((float)rng.NextDouble() - 0.5f) * 3f;
                    float L = 0.5f + Mathf.Pow((float)rng.NextDouble(), 1.8f) * (r > 0.8f ? 5f : 3.2f);
                    var dir = (Vector3.up + new Vector3((float)rng.NextDouble() - 0.5f, 0f, (float)rng.NextDouble() - 0.5f) * 0.1f).normalized;
                    Cono(mg, new Vector3(xx, AlturaSueloCueva(xx, zz) - 0.35f, zz), dir, L + 0.35f, (L + 0.35f) * (0.32f + 0.16f * (float)rng.NextDouble()) + 0.15f, 10, rng, 0.75f);
                    nMit++;
                }
                huecos.Add(new Vector3(x, z, 3.5f));
            }
            mg.Crear("coop_cueva_estalagmitas", red, matCueva, true);

            // 6) charcos en sitios llanos
            var mw = new Malla();
            int nCh = 0;
            for (int intento = 0; intento < 500 && nCh < 22; intento++)
            {
                float x = ((float)rng.NextDouble() * 2f - 1f) * 44f * CuevaE, z = CuevaZ0 + 8f + (float)rng.NextDouble() * 100f * CuevaE;
                if (RadioCueva(x, z) > 0.78f || (Mathf.Abs(x) < 4f && z < CuevaZ0 + 40f)) continue;
                bool ocupado = false;
                foreach (var h0 in huecos) if (new Vector2(x - h0.x, z - h0.y).magnitude < h0.z + 2f) { ocupado = true; break; }
                if (ocupado) continue;
                Charco(mw, new Vector3(x, AlturaSueloCueva(x, z) + 0.025f, z), 1.2f + (float)rng.NextDouble() * 3.2f, rng);
                nCh++;
            }
            mw.Crear("coop_cueva_charcos", red, matCharco, false);

            // 7) grieta en el techo con un rayo de luz fría que cae sobre la arena (polvo flotando dentro)
            Vector2 gp = new Vector2(10f, CuevaZC - 4f);
            float techo = AlturaTecho(red, gp.x, gp.y);
            Grieta(red, new Vector3(gp.x, techo - 0.15f, gp.y), rng);
            var luzG = new GameObject("coop_cueva_luz_grieta"); luzG.transform.SetParent(red, false);
            luzG.transform.localPosition = new Vector3(gp.x, techo - 0.5f, gp.y); luzG.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var sl = luzG.AddComponent<Light>(); sl.type = LightType.Spot; sl.color = new Color(0.62f, 0.66f, 0.74f); sl.range = techo + 6f; sl.spotAngle = 32f; sl.intensity = 1.5f; sl.shadows = LightShadows.None;
            Rayo(red, new Vector3(gp.x, techo - 0.2f, gp.y), techo, rng);
            Polvo(red, new Vector3(0f, 7f * CuevaE, CuevaZC), new Vector3(96f * CuevaE, 13f * CuevaE, 98f * CuevaE), 1400, 0.25f);
            Polvo(red, new Vector3(gp.x, techo * 0.5f, gp.y), new Vector3(7f, techo * 0.9f, 7f), 220, 0.6f);
            // 7b) más grietas en el techo con su haz de luz (pedido 2026-11-19: darle más vida a la cueva)
            int nGrietas = 1;
            for (int k = 0; k < 8; k++)
            {
                float a = k / 8f * Mathf.PI * 2f + (float)rng.NextDouble() * 0.5f, r = 30f + (float)rng.NextDouble() * 45f;
                float gx = Mathf.Cos(a) * r, gz = CuevaZC + Mathf.Sin(a) * r, tg = AlturaTecho(red, gx, gz);
                if (tg < 8f) continue;
                Grieta(red, new Vector3(gx, tg - 0.15f, gz), rng);
                var lg = new GameObject("coop_cueva_luz_grieta"); lg.transform.SetParent(red, false);
                lg.transform.localPosition = new Vector3(gx, tg - 0.5f, gz); lg.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                var sg = lg.AddComponent<Light>(); sg.type = LightType.Spot; sg.color = new Color(0.6f, 0.65f, 0.75f); sg.range = tg + 6f; sg.spotAngle = 24f + (float)rng.NextDouble() * 10f; sg.intensity = 1.1f; sg.shadows = LightShadows.None;
                Rayo(red, new Vector3(gx, tg - 0.2f, gz), tg, rng);
                Polvo(red, new Vector3(gx, tg * 0.5f, gz), new Vector3(5f, tg * 0.9f, 5f), 90, 0.6f);
                nGrietas++;
            }
            plugin.Log("Cueva: " + nGrietas + " grietas con luz en el techo");

            // 8) goteo desde algunas estalactitas
            foreach (var gpos in gotas) Goteo(red, gpos, rng);

            // 9) antorchas en las paredes (fuego real del mod), con parpadeo
            int nAnt = 0;
            const int Antorchas = 64;   // (2026-11-19: 40 → 64, más luz en la cueva)
            for (int k = 0; k < Antorchas; k++)
            {
                float th = (k + 0.5f) / Antorchas * Mathf.PI * 2f - Mathf.PI / 2f;
                Vector3 dir = new Vector3(Mathf.Cos(th), 0f, Mathf.Sin(th));
                if (Mathf.Abs(Mathf.Sin(th) + 1f) < 0.05f) continue;   // no justo sobre la entrada
                Vector3 desde = new Vector3(0f, 3.2f, CuevaZC);
                Vector3 hit;
                if (!RayoCueva(red, desde, dir, 200f, out hit)) continue;
                AntorchaCueva(red, hit - dir * 0.35f, dir);
                nAnt++;
            }

            // 9b) braseros alrededor de la arena del jefe: iluminan el suelo donde se pelea
            for (int k = 0; k < 6; k++)
            {
                float a = k / 6f * Mathf.PI * 2f + 0.5f;
                float bx = Mathf.Cos(a) * 32f, bz = CuevaZC + Mathf.Sin(a) * 32f;
                Brasero(red, new Vector3(bx, AlturaSueloCueva(bx, bz), bz));
            }
            // segundo anillo de braseros más afuera (2026-11-19)
            for (int k = 0; k < 8; k++)
            {
                float a = k / 8f * Mathf.PI * 2f + 0.2f;
                float bx = Mathf.Cos(a) * 62f, bz = CuevaZC + Mathf.Sin(a) * 62f;
                Brasero(red, new Vector3(bx, AlturaSueloCueva(bx, bz), bz));
            }

            // 10) rocas caídas al pie de las paredes y alrededor de la entrada
            int f, nRoc = 0;
            if (piedra != null)
            {
                for (int k = 0; k < 96; k++)
                {
                    float th = (float)rng.NextDouble() * Mathf.PI * 2f;
                    var dir = new Vector3(Mathf.Cos(th), 0f, Mathf.Sin(th));
                    Vector3 hit;
                    if (!RayoCueva(red, new Vector3(0f, 1.5f, CuevaZC), dir, 200f, out hit)) continue;
                    if (Mathf.Abs(hit.x) < hP + 5f && hit.z < CuevaZ0 + 4f) continue;
                    Vector3 lp = hit - dir * (1.2f + (float)rng.NextDouble() * 3f);
                    lp.y = AlturaSueloCueva(lp.x, lp.z) - 0.3f;
                    var r = Coop.Piezas.Clonar(piedra, red.TransformPoint(lp), Quaternion.Euler(((float)rng.NextDouble() - 0.5f) * 30f, rng.Next(360), ((float)rng.NextDouble() - 0.5f) * 30f), out f);
                    r.transform.localScale *= 1.1f + 2.4f * (float)rng.NextDouble(); r.transform.SetParent(red, true); nRoc++;
                }
                foreach (var lp in new[] { new Vector3(-hP - 2.6f, 0f, CuevaZ0 + 1.2f), new Vector3(hP + 2.8f, 0f, CuevaZ0 + 1.4f), new Vector3(-hP - 4.5f, 0f, CuevaZ0 + 2.2f), new Vector3(hP + 4.2f, 0f, CuevaZ0 + 2.6f) })
                {
                    var r = Coop.Piezas.Clonar(piedra, red.TransformPoint(lp), Quaternion.Euler(0f, rng.Next(360), 0f), out f);
                    r.transform.localScale *= 1.2f + 0.8f * (float)rng.NextDouble(); r.transform.SetParent(red, true); nRoc++;
                }
            }

            // 11) restos de los mineros: marco de madera en la entrada y andamios contra las paredes oeste y este
            var bm = new Constructor();
            float zm = CuevaZ0 + 0.35f;
            bm.Viga(new Vector3(-hP - 0.2f, 0f, zm), new Vector3(-hP - 0.2f, altoPasillo + 0.3f, zm), 0.34f, 1);
            bm.Viga(new Vector3(hP + 0.2f, 0f, zm), new Vector3(hP + 0.2f, altoPasillo + 0.3f, zm), 0.34f, 1);
            bm.Viga(new Vector3(-hP - 0.6f, altoPasillo + 0.25f, zm), new Vector3(hP + 0.6f, altoPasillo + 0.25f, zm), 0.38f, 1);
            int nAnd = 0;
            foreach (var lado in new[] { -1f, 1f })
                foreach (var zz in new[] { CuevaZC - 16f, CuevaZC + 28f })
                {
                    Vector3 hit;
                    if (!RayoCueva(red, new Vector3(0f, 2f, zz), new Vector3(lado, 0f, 0f), 200f, out hit)) continue;
                    float xw = hit.x - lado * 3.2f;
                    Andamio(bm, xw, zz, lado, rng);
                    nAnd++;
                }
            var madera = bm.Crear("coop_cueva_madera", new[] { matRoca, matMadera, matSuelo, matHierro, matNegro }, true);
            madera.transform.SetParent(red, false);

            // 12) más calidad (2026-11-18 noche): faroles en la entrada, grava, montones de huesos y niebla baja
            Farol(red, new Vector3(hP + 0.7f, altoPasillo - 0.25f, CuevaZ0 + 0.7f));
            Farol(red, new Vector3(-hP - 0.7f, altoPasillo - 0.25f, CuevaZ0 + 0.7f));
            var lzE = new GameObject("coop_cueva_luz_entrada"); lzE.transform.SetParent(red, false); lzE.transform.localPosition = new Vector3(0f, 5f, CuevaZ0 + 9f);
            var lE = lzE.AddComponent<Light>(); lE.type = LightType.Point; lE.color = new Color(1f, 0.62f, 0.32f); lE.range = 20f; lE.intensity = 0.9f; lE.shadows = LightShadows.None;
            int nGrava = 0;
            if (piedra != null)
                for (int k = 0; k < 170; k++)
                {
                    float x = ((float)rng.NextDouble() * 2f - 1f) * 48f * CuevaE, z = CuevaZ0 + 4f + (float)rng.NextDouble() * 104f * CuevaE;
                    if (RadioCueva(x, z) > 0.9f || (Mathf.Abs(x) < 2.5f && z < CuevaZ0 + 120f)) continue;
                    var r = Coop.Piezas.Clonar(piedra, red.TransformPoint(new Vector3(x, AlturaSueloCueva(x, z) - 0.05f, z)), Quaternion.Euler((float)rng.NextDouble() * 360f, rng.Next(360), (float)rng.NextDouble() * 360f), out f);
                    r.transform.localScale *= 0.12f + 0.3f * (float)rng.NextDouble(); r.transform.SetParent(red, true);
                    foreach (var c in r.GetComponentsInChildren<Collider>()) Object.Destroy(c);   // grava: se pisa sin tropezar
                    nGrava++;
                }
            int nHuesos = 0;
            try { nHuesos = Huesos(red, rng); } catch (System.Exception e) { plugin.Log("Minas: huesos de la cueva: " + e.Message); }
            try { Niebla(red); } catch (System.Exception e) { plugin.Log("Minas: niebla de la cueva: " + e.Message); }

            plugin.Log("Minas: cueva de la guarida: " + nGrava + " piedras de grava, " + nHuesos + " huesos");
            plugin.Log("Minas: cueva de la guarida: bóveda " + (2 * CuevaAX) + " × " + (2 * CuevaAZ) + " m, techo hasta " + CuevaH + " m; " + columnas.Length + " columnas, " +
                       nEst + " estalactitas, " + nMit + " estalagmitas, " + nCh + " charcos, " + gotas.Count + " goteos, " + nAnt + " antorchas, " + nRoc + " rocas, " + nAnd + " andamios");
        }

        /// Sitios de los altares (local x, z), para no tapar con estalagmitas.
        /// Se escalan desde la entrada con CuevaE (2026-11-19).
        static readonly Vector2[] SitiosAltaresCueva = EscalarSitios(new[]
        {
            new Vector2(0f, 100f),
            new Vector2(-36f, 40f), new Vector2(36f, 40f), new Vector2(-40f, 62f), new Vector2(40f, 62f),
            new Vector2(-38f, 84f), new Vector2(38f, 84f), new Vector2(-30f, 106f), new Vector2(30f, 106f),
            new Vector2(-14f, 118f), new Vector2(14f, 118f),
        });

        /// Medidas pensadas para la cueva original (110 × 112 m) llevadas a la actual: x × CuevaE y z desde la entrada × CuevaE.
        static Vector2[] EscalarSitios(Vector2[] s) { for (int i = 0; i < s.Length; i++) s[i] = EscalarSitio(s[i]); return s; }
        static Vector2 EscalarSitio(Vector2 p) { return new Vector2(p.x * CuevaE, CuevaZ0 + (p.y - CuevaZ0) * CuevaE); }

        bool RayoCueva(Transform red, Vector3 desdeLocal, Vector3 dirLocal, float dist, out Vector3 puntoLocal)
        {
            puntoLocal = Vector3.zero;
            if (colCueva == null) return false;
            var ray = new Ray(red.TransformPoint(desdeLocal), red.TransformDirection(dirLocal));
            RaycastHit h;
            if (!colCueva.Raycast(ray, out h, dist)) return false;
            puntoLocal = red.InverseTransformPoint(h.point); return true;
        }

        float AlturaTecho(Transform red, float x, float z)
        {
            Vector3 hit;
            if (RayoCueva(red, new Vector3(x, 2f, z), Vector3.up, 140f, out hit)) return hit.y;
            return CuevaH * 0.7f;
        }

        // ---------------- Piezas ----------------

        /// Cono irregular (estalactita o estalagmita) desde base en la dirección dir.
        static void Cono(Malla m, Vector3 b, Vector3 dir, float largo, float radio, int lados, System.Random rng, float forma)
        {
            Vector3 a = Vector3.Cross(dir, Mathf.Abs(dir.y) > 0.9f ? Vector3.right : Vector3.up).normalized, c = Vector3.Cross(dir, a);
            const int anillos = 6;
            float s = (float)rng.NextDouble() * 100f;
            int inicio = -1;
            for (int k = 0; k < anillos; k++)
            {
                float t = k / (float)anillos;
                float rr = radio * Mathf.Pow(1f - t, forma) * (1f + 0.22f * (Mathf.PerlinNoise(s, t * 4f) - 0.5f) * 2f);
                Vector3 centro = b + dir * largo * t + (a * (Mathf.PerlinNoise(s + 7f, t * 3f) - 0.5f) + c * (Mathf.PerlinNoise(s + 13f, t * 3f) - 0.5f)) * radio * 0.5f * t;
                for (int i = 0; i <= lados; i++)
                {
                    float ang = i / (float)lados * Mathf.PI * 2f;
                    float bulto = 1f + 0.2f * (Mathf.PerlinNoise(s + ang * 1.3f, t * 5f) - 0.5f) * 2f;
                    int id = m.V(centro + (a * Mathf.Cos(ang) + c * Mathf.Sin(ang)) * rr * bulto, new Vector2(i / (float)lados * radio * 2f, t * largo / 2f));
                    if (inicio < 0) inicio = id;
                }
            }
            int punta = m.V(b + dir * largo * (forma < 1f ? 0.94f : 1f), new Vector2(0.5f, largo / 2f));
            int fila = lados + 1;
            for (int k = 0; k < anillos; k++)
                for (int i = 0; i < lados; i++)
                {
                    int p = inicio + k * fila + i, pk = p + 1;
                    if (k == anillos - 1) { m.Tri(p, pk, punta); continue; }
                    int pr = p + fila, pr1 = pr + 1;
                    m.Tri(p, pk, pr); m.Tri(pk, pr1, pr);
                }
        }

        /// Columna de piso a techo: ancha arriba y abajo (donde se unen estalactita y estalagmita), con estrías de colada.
        static void Columna(Malla m, Vector3 b, float alto, float rMedio, float rPunta, System.Random rng)
        {
            const int lados = 18;
            int anillos = Mathf.Max(8, Mathf.RoundToInt(alto / 0.8f));
            float s = (float)rng.NextDouble() * 100f;
            int inicio = -1;
            for (int k = 0; k <= anillos; k++)
            {
                float t = k / (float)anillos, y = alto * t;
                float rr = rMedio + (rPunta - rMedio) * Mathf.Pow(Mathf.Abs(2f * t - 1f), 2.6f);
                Vector3 centro = b + Vector3.up * y + new Vector3(Mathf.PerlinNoise(s, t * 3f) - 0.5f, 0f, Mathf.PerlinNoise(s + 5f, t * 3f) - 0.5f) * 1.4f;
                for (int i = 0; i <= lados; i++)
                {
                    float ang = i / (float)lados * Mathf.PI * 2f;
                    float bulto = 1f + 0.16f * Mathf.Sin(ang * 7f + Mathf.PerlinNoise(s + 2f, t * 6f) * 4f) + 0.22f * (Mathf.PerlinNoise(s + ang, t * 4f) - 0.5f) * 2f;
                    int id = m.V(centro + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * rr * bulto, new Vector2(i / (float)lados * rr * 6.28f / 7f, y / 7f));
                    if (inicio < 0) inicio = id;
                }
            }
            int fila = lados + 1;
            // eje = arriba; con a = x y c = cross(up, x) = −z el giro va de x hacia −z: aquí el ángulo va de x hacia +z, así que se invierte el orden
            for (int k = 0; k < anillos; k++)
                for (int i = 0; i < lados; i++)
                {
                    int p = inicio + k * fila + i, pk = p + 1, pr = p + fila, pr1 = pr + 1;
                    m.Tri(p, pr, pk); m.Tri(pk, pr, pr1);
                }
        }

        static void Charco(Malla m, Vector3 c, float radio, System.Random rng)
        {
            const int lados = 20;
            float s = (float)rng.NextDouble() * 100f;
            int centro = m.V(c, new Vector2(0.5f, 0.5f)), inicio = -1;
            for (int i = 0; i <= lados; i++)
            {
                float ang = i / (float)lados * Mathf.PI * 2f;
                float rr = radio * (0.6f + 0.7f * Mathf.PerlinNoise(s + Mathf.Cos(ang) * 1.5f, s + Mathf.Sin(ang) * 1.5f)) * (1f + 0.25f * Mathf.Cos(ang * 2f + s));
                int id = m.V(c + new Vector3(Mathf.Cos(ang) * rr, 0f, Mathf.Sin(ang) * rr), new Vector2(0.5f + Mathf.Cos(ang) * 0.5f, 0.5f + Mathf.Sin(ang) * 0.5f));
                if (inicio < 0) inicio = id;
            }
            // cara hacia arriba: el ángulo va de +x hacia +z, por eso (centro, siguiente, actual)
            for (int i = 0; i < lados; i++) m.Tri(centro, inicio + i + 1, inicio + i);
        }

        void Grieta(Transform red, Vector3 lp, System.Random rng)
        {
            var mg = new Malla();
            // franja irregular y alargada que se ve desde abajo (cara hacia abajo)
            const int n = 14; float s = (float)rng.NextDouble() * 50f;
            int[] arriba = new int[n + 1], abajo = new int[n + 1];
            for (int i = 0; i <= n; i++)
            {
                float t = i / (float)n, x = (t - 0.5f) * 7f, ancho = (0.25f + 1.1f * Mathf.Sin(t * Mathf.PI)) * (0.7f + 0.6f * Mathf.PerlinNoise(s, t * 4f));
                float zc = (Mathf.PerlinNoise(s + 3f, t * 2.5f) - 0.5f) * 2.5f;
                arriba[i] = mg.V(lp + new Vector3(x, 0f, zc + ancho / 2f), Vector2.zero);
                abajo[i] = mg.V(lp + new Vector3(x, 0f, zc - ancho / 2f), Vector2.zero);
            }
            for (int i = 0; i < n; i++) { mg.Tri(abajo[i], abajo[i + 1], arriba[i]); mg.Tri(abajo[i + 1], arriba[i + 1], arriba[i]); }   // cara hacia abajo
            mg.Crear("coop_cueva_grieta", red, matGrieta, false);
        }

        void Rayo(Transform red, Vector3 lp, float alto, System.Random rng)
        {
            var m = new Malla();
            const int lados = 16; int fila = lados + 1;
            for (int k = 0; k <= 1; k++)
            {
                float r = k == 0 ? 2.2f : 5.5f, y = k == 0 ? 0f : -alto;
                for (int i = 0; i <= lados; i++)
                {
                    float ang = i / (float)lados * Mathf.PI * 2f;
                    m.V(lp + new Vector3(Mathf.Cos(ang) * r * 1.6f, y, Mathf.Sin(ang) * r), new Vector2(i / (float)lados, k == 0 ? 1f : 0f));
                }
            }
            for (int i = 0; i < lados; i++) { m.Tri(i, i + fila, i + 1); m.Tri(i + 1, i + fila, i + fila + 1); }
            var g = m.Crear("coop_cueva_rayo", red, matRayo, false);
            var mr = g.GetComponent<MeshRenderer>(); mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
        }

        void Polvo(Transform red, Vector3 lp, Vector3 caja, int max, float alfa)
        {
            var g = new GameObject("coop_cueva_polvo"); g.transform.SetParent(red, false); g.transform.localPosition = lp;
            var ps = g.AddComponent<ParticleSystem>();
            var main = ps.main; main.loop = true; main.startLifetime = 18f; main.startSpeed = 0f; main.maxParticles = max;
            main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.06f); main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new Color(1f, 1f, 1f, alfa); main.prewarm = true;
            var em = ps.emission; em.rateOverTime = max / 18f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.box = caja;
            var no = ps.noise; no.enabled = true; no.strength = 0.12f; no.frequency = 0.25f;
            var col = ps.colorOverLifetime; col.enabled = true;
            var gr = new Gradient(); gr.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                                                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
            col.color = gr;
            var r = g.GetComponent<ParticleSystemRenderer>(); r.sharedMaterial = matPolvo; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        void Goteo(Transform red, Vector3 lp, System.Random rng)
        {
            var g = new GameObject("coop_cueva_goteo"); g.transform.SetParent(red, false); g.transform.localPosition = lp;
            var ps = g.AddComponent<ParticleSystem>();
            var main = ps.main; main.loop = true; main.startLifetime = 3f; main.startSpeed = 0f; main.gravityModifier = 1f; main.maxParticles = 8;
            main.startSize = 0.035f; main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.rateOverTime = 0.3f + (float)rng.NextDouble() * 0.6f;
            var sh = ps.shape; sh.enabled = false;
            var co = ps.collision; co.enabled = true; co.type = ParticleSystemCollisionType.World; co.lifetimeLoss = 1f; co.bounce = 0f;
            var r = g.GetComponent<ParticleSystemRenderer>(); r.sharedMaterial = matGota; r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.04f; r.lengthScale = 1.5f;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        void AntorchaCueva(Transform red, Vector3 lp, Vector3 haciaPared)
        {
            var palo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.Destroy(palo.GetComponent<Collider>());
            palo.name = "coop_cueva_antorcha"; palo.transform.SetParent(red, false);
            palo.transform.localPosition = lp + Vector3.up * 0.1f;
            palo.transform.localRotation = Quaternion.FromToRotation(Vector3.up, (Vector3.up - haciaPared * 0.55f).normalized);
            palo.transform.localScale = new Vector3(0.07f, 0.38f, 0.07f);
            palo.GetComponent<Renderer>().sharedMaterial = matMadera;
            Vector3 punta = lp + Vector3.up * 0.1f + palo.transform.localRotation * Vector3.up * 0.38f;
            Llama.Crear(red.TransformPoint(punta + Vector3.up * 0.04f), red, 0.3f, true, false);
            var l = new GameObject("coop_cueva_luz_antorcha"); l.transform.SetParent(red, false); l.transform.localPosition = punta + Vector3.up * 0.35f - haciaPared * 0.4f;
            var lz = l.AddComponent<Light>(); lz.type = LightType.Point; lz.color = new Color(1f, 0.55f, 0.24f); lz.range = 19f; lz.intensity = 1.9f; lz.shadows = LightShadows.None;
            l.AddComponent<Titilar>();
        }

        void Brasero(Transform red, Vector3 lp)
        {
            var cuenco = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cuenco.name = "coop_cueva_brasero"; cuenco.transform.SetParent(red, false);
            cuenco.transform.localPosition = lp + Vector3.up * 1.05f; cuenco.transform.localScale = new Vector3(0.95f, 0.14f, 0.95f);
            cuenco.GetComponent<Renderer>().sharedMaterial = matHierro;
            for (int i = 0; i < 3; i++)
            {
                float a = i / 3f * Mathf.PI * 2f;
                var pata = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Object.Destroy(pata.GetComponent<Collider>());
                pata.name = "coop_cueva_brasero_pata"; pata.transform.SetParent(red, false);
                pata.transform.localPosition = lp + new Vector3(Mathf.Cos(a) * 0.32f, 0.5f, Mathf.Sin(a) * 0.32f);
                pata.transform.localRotation = Quaternion.Euler(Mathf.Sin(a) * 12f, 0f, -Mathf.Cos(a) * 12f);
                pata.transform.localScale = new Vector3(0.07f, 0.52f, 0.07f);
                pata.GetComponent<Renderer>().sharedMaterial = matHierro;
            }
            Llama.Crear(red.TransformPoint(lp + Vector3.up * 1.2f), red, 0.8f, true, true);
            var l = new GameObject("coop_cueva_luz_brasero"); l.transform.SetParent(red, false); l.transform.localPosition = lp + Vector3.up * 2.1f;
            var lz = l.AddComponent<Light>(); lz.type = LightType.Point; lz.color = new Color(1f, 0.5f, 0.2f); lz.range = 17f; lz.intensity = 1.7f; lz.shadows = LightShadows.None;
            l.AddComponent<Titilar>();
        }

        static Material matHueso, matCuenca;

        /// Montones de huesos (geometría propia: cráneos con mandíbula y cuencas, huesos largos con cabezas, costillas) junto a la arena y los altares.
        int Huesos(Transform red, System.Random rng)
        {
            if (matHueso == null)
            {
                matHueso = new Material(Shader.Find("Standard")) { name = "coop_cueva_hueso", color = new Color(0.58f, 0.53f, 0.44f) };
                matHueso.SetFloat("_Glossiness", 0.12f); matHueso.SetFloat("_GlossyReflections", 0f); matHueso.EnableKeyword("_GLOSSYREFLECTIONS_OFF");
                matCuenca = new Material(Shader.Find("Standard")) { name = "coop_cueva_cuenca", color = new Color(0.03f, 0.025f, 0.02f) };
                matCuenca.SetFloat("_Glossiness", 0f);
            }
            var mh = new Malla(); var mc = new Malla(); int n = 0, bloque = 0;
            var sitios = new List<Vector2>();
            for (int k = 0; k < 9; k++) { float a = k / 9f * Mathf.PI * 2f + 0.3f; sitios.Add(new Vector2(Mathf.Cos(a) * 22f, CuevaZC + Mathf.Sin(a) * 22f)); }
            foreach (var al in SitiosAltaresCueva) sitios.Add(al + new Vector2(((float)rng.NextDouble() - 0.5f) * 5f, -2.6f));
            foreach (var st in sitios)
            {
                int cuantos = 5 + rng.Next(9);
                for (int i = 0; i < cuantos; i++)
                {
                    // Unity 5.6: máx. 65 535 vértices por malla → se reparte en bloques
                    if (mh.Cuenta > 50000) { mh.Crear("coop_cueva_huesos_" + (bloque++), red, matHueso, false); mh = new Malla(); }
                    float x = st.x + ((float)rng.NextDouble() - 0.5f) * 2.6f, z = st.y + ((float)rng.NextDouble() - 0.5f) * 2.6f;
                    Vector3 p = new Vector3(x, AlturaSueloCueva(x, z), z);
                    float giro = (float)rng.NextDouble() * 360f;
                    double tipo = rng.NextDouble();
                    if (tipo < 0.2) Craneo(mh, mc, p, giro, 0.9f + 0.3f * (float)rng.NextDouble());
                    else if (tipo < 0.4) Costillas(mh, p, giro, rng);
                    else HuesoLargo(mh, p + Vector3.up * 0.04f, giro, 0.35f + 0.25f * (float)rng.NextDouble(), (float)rng.NextDouble() * 20f - 10f);
                    n++;
                }
            }
            mh.Crear("coop_cueva_huesos", red, matHueso, false);
            mc.Crear("coop_cueva_cuencas", red, matCuenca, false);
            return n;
        }

        /// Esfera deformada (escala por eje) de 8 × 6 caras.
        static void Elipsoide(Malla m, Vector3 c, Vector3 r, Quaternion q)
        {
            bool chica = Mathf.Max(r.x, Mathf.Max(r.y, r.z)) < 0.05f;
            int NA = chica ? 6 : 8, NB = chica ? 4 : 6; int i0 = -1;
            for (int j = 0; j <= NB; j++)
                for (int i = 0; i <= NA; i++)
                {
                    float th = i / (float)NA * Mathf.PI * 2f, ph = j / (float)NB * Mathf.PI - Mathf.PI / 2f;
                    var d = new Vector3(Mathf.Cos(ph) * Mathf.Cos(th) * r.x, Mathf.Sin(ph) * r.y, Mathf.Cos(ph) * Mathf.Sin(th) * r.z);
                    int id = m.V(c + q * d, new Vector2(i / (float)NA, j / (float)NB)); if (i0 < 0) i0 = id;
                }
            int fila = NA + 1;
            for (int j = 0; j < NB; j++)
                for (int i = 0; i < NA; i++)
                {
                    int a = i0 + j * fila + i, b = a + 1, d2 = a + fila, c2 = d2 + 1;
                    m.Tri(a, d2, b); m.Tri(b, d2, c2);
                }
        }

        static void HuesoLargo(Malla m, Vector3 p, float giro, float largo, float inclin)
        {
            var q = Quaternion.Euler(0f, giro, inclin);
            Vector3 eje = q * Vector3.right;
            Vector3 a = p - eje * largo / 2f, b = p + eje * largo / 2f;
            Elipsoide(m, (a + b) / 2f, new Vector3(largo / 2f, 0.022f, 0.022f), q);                       // diáfisis
            foreach (var e in new[] { a, b })
            {
                Elipsoide(m, e + q * new Vector3(0f, 0f, 0.025f), new Vector3(0.04f, 0.035f, 0.035f), q);  // cabezas (dos lóbulos)
                Elipsoide(m, e - q * new Vector3(0f, 0f, 0.025f), new Vector3(0.04f, 0.035f, 0.035f), q);
            }
        }

        static void Costillas(Malla m, Vector3 p, float giro, System.Random rng)
        {
            var q = Quaternion.Euler(0f, giro, 0f);
            for (int k = 0; k < 4; k++)
            {
                Vector3 c = p + q * new Vector3(0f, 0.03f, k * 0.07f);
                for (int s = 0; s < 5; s++)   // arco hecho de 5 trozos
                {
                    float a = Mathf.Lerp(0.15f, 2.6f, s / 4f);
                    Vector3 pos = c + q * new Vector3(Mathf.Cos(a) * 0.22f, Mathf.Sin(a) * 0.06f, 0f);
                    Elipsoide(m, pos, new Vector3(0.06f, 0.012f, 0.016f), q * Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg - 90f));
                }
            }
        }

        static void Craneo(Malla mh, Malla mc, Vector3 p, float giro, float e)
        {
            var q = Quaternion.Euler(-8f, giro, 0f);
            Vector3 c = p + Vector3.up * 0.1f * e;
            Elipsoide(mh, c, new Vector3(0.085f, 0.09f, 0.11f) * e, q);                                        // bóveda craneal
            Elipsoide(mh, c + q * new Vector3(0f, -0.045f, 0.075f) * e, new Vector3(0.06f, 0.04f, 0.05f) * e, q); // cara
            Elipsoide(mh, c + q * new Vector3(0f, -0.09f, 0.06f) * e, new Vector3(0.05f, 0.018f, 0.045f) * e, q); // mandíbula
            foreach (var lado in new[] { -1f, 1f })
                Elipsoide(mc, c + q * new Vector3(0.03f * lado, -0.01f, 0.105f) * e, new Vector3(0.022f, 0.02f, 0.012f) * e, q);   // cuencas
            Elipsoide(mc, c + q * new Vector3(0f, -0.05f, 0.118f) * e, new Vector3(0.011f, 0.014f, 0.008f) * e, q);                // nariz
        }

        void Niebla(Transform red)
        {
            var g = new GameObject("coop_cueva_niebla_baja"); g.transform.SetParent(red, false); g.transform.localPosition = new Vector3(0f, 0.45f, CuevaZC);
            var ps = g.AddComponent<ParticleSystem>();
            var main = ps.main; main.loop = true; main.startLifetime = 30f; main.startSpeed = 0f; main.maxParticles = 300; main.prewarm = true;
            main.startSize = new ParticleSystem.MinMaxCurve(3f, 6f); main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new Color(0.2f, 0.18f, 0.17f, 0.035f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            var em = ps.emission; em.rateOverTime = 300f / 30f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.box = new Vector3(96f * CuevaE, 0.3f, 98f * CuevaE);
            var no = ps.noise; no.enabled = true; no.strength = 0.08f; no.frequency = 0.08f;
            var col = ps.colorOverLifetime; col.enabled = true;
            var gr = new Gradient(); gr.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                                                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
            col.color = gr;
            var mat = new Material(Shader.Find("Particles/Alpha Blended")) { name = "coop_cueva_niebla", mainTexture = Punto(64) };
            mat.SetColor("_TintColor", new Color(0.5f, 0.5f, 0.5f, 0.5f));
            var r = g.GetComponent<ParticleSystemRenderer>(); r.sharedMaterial = mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.sortingFudge = 10f;
        }

        /// Andamio de madera viejo pegado a la pared: postes, travesaños, una plataforma y una diagonal.
        static void Andamio(Constructor b, float xw, float zc, float lado, System.Random rng)
        {
            const float alto = 5.5f, ancho = 6f, fondo = 2.2f;
            float xi = xw - lado * fondo;
            foreach (var z in new[] { zc - ancho / 2f, zc + ancho / 2f })
            {
                b.Viga(new Vector3(xw, 0f, z), new Vector3(xw, alto, z), 0.22f, 1);
                b.Viga(new Vector3(xi, 0f, z), new Vector3(xi, alto * 0.62f, z), 0.22f, 1);
                b.Viga(new Vector3(xi, alto * 0.6f, z), new Vector3(xw, alto * 0.6f, z), 0.18f, 1);
            }
            b.Viga(new Vector3(xi, alto * 0.6f, zc - ancho / 2f), new Vector3(xi, alto * 0.6f, zc + ancho / 2f), 0.18f, 1);
            b.Viga(new Vector3(xw, alto - 0.2f, zc - ancho / 2f), new Vector3(xw, alto - 0.2f, zc + ancho / 2f), 0.18f, 1);
            b.Viga(new Vector3(xi, 0.2f, zc - ancho / 2f), new Vector3(xi, alto * 0.58f, zc + ancho / 2f), 0.14f, 1);   // diagonal
            // tablones de la plataforma (alguno falta)
            for (float z = zc - ancho / 2f + 0.3f; z < zc + ancho / 2f; z += 0.36f)
                if (rng.NextDouble() > 0.18) b.Viga(new Vector3(xi, alto * 0.6f + 0.13f, z), new Vector3(xw, alto * 0.6f + 0.13f, z), 0.08f, 1);
        }

        // ---------------- Malla sencilla con tangentes ----------------

        public class Malla
        {
            readonly List<Vector3> v = new List<Vector3>();
            readonly List<Vector2> uv = new List<Vector2>();
            readonly List<int> t = new List<int>();
            public int V(Vector3 p, Vector2 u) { v.Add(p); uv.Add(u); return v.Count - 1; }
            public int Cuenta { get { return v.Count; } }
            public void Tri(int a, int b, int c) { t.Add(a); t.Add(b); t.Add(c); }
            /// a abajo-izq, b abajo-der, c arriba-der, d arriba-izq (vista desde el frente).
            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float tile)
            {
                int ia = V(a, new Vector2(a.x, a.y) / tile), ib = V(b, new Vector2(b.x, b.y) / tile), ic = V(c, new Vector2(c.x, c.y) / tile), id = V(d, new Vector2(d.x, d.y) / tile);
                Tri(ia, ib, id); Tri(ib, ic, id);
            }

            /// Igual que Crear, pero los vértices están en coordenadas del MUNDO (el objeto queda colgado de padre sin moverse).
            public GameObject CrearMundo(string nombre, Transform padre, Material mat, bool colision)
            {
                var go = Crear(nombre, null, mat, colision);
                go.transform.SetParent(padre, true);
                return go;
            }

            public GameObject Crear(string nombre, Transform padre, Material mat, bool colision)
            {
                var go = new GameObject(nombre);
                go.transform.SetParent(padre, false);
                var m = new Mesh { name = nombre };
                m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(t, 0);
                m.RecalculateNormals(); m.RecalculateBounds();
                m.tangents = Tangentes(v, uv, t, m.normals);
                go.AddComponent<MeshFilter>().sharedMesh = m;
                go.AddComponent<MeshRenderer>().sharedMaterial = mat;
                if (colision) go.AddComponent<MeshCollider>().sharedMesh = m;
                else go.AddComponent<NoSolido>();   // que Solidez no la vuelva una pared (el rayo de luz atrapaba al jugador)
                return go;
            }

            internal static Vector4[] Tangentes(List<Vector3> v, List<Vector2> uv, List<int> t, Vector3[] n)
            {
                var tan = new Vector3[v.Count]; var bit = new Vector3[v.Count];
                for (int i = 0; i < t.Count; i += 3)
                {
                    int a = t[i], b = t[i + 1], c = t[i + 2];
                    Vector3 e1 = v[b] - v[a], e2 = v[c] - v[a];
                    Vector2 d1 = uv[b] - uv[a], d2 = uv[c] - uv[a];
                    float r = d1.x * d2.y - d2.x * d1.y;
                    if (Mathf.Abs(r) < 1e-8f) continue;
                    r = 1f / r;
                    Vector3 sd = (e1 * d2.y - e2 * d1.y) * r, td = (e2 * d1.x - e1 * d2.x) * r;
                    tan[a] += sd; tan[b] += sd; tan[c] += sd; bit[a] += td; bit[b] += td; bit[c] += td;
                }
                var res = new Vector4[v.Count];
                for (int i = 0; i < v.Count; i++)
                {
                    Vector3 tt = tan[i] - n[i] * Vector3.Dot(n[i], tan[i]);
                    if (tt.sqrMagnitude < 1e-10f) tt = Vector3.Cross(n[i], Mathf.Abs(n[i].y) > 0.9f ? Vector3.right : Vector3.up);
                    tt.Normalize();
                    res[i] = new Vector4(tt.x, tt.y, tt.z, Vector3.Dot(Vector3.Cross(n[i], tt), bit[i]) < 0f ? -1f : 1f);
                }
                return res;
            }
        }
    }
}

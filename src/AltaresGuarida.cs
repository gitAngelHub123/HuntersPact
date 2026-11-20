// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using System.Collections.Generic;
using UnityEngine;

namespace WitchHuntCoop
{
    /// Altares de la guarida, rehechos (pedido 2026-11-18: los de antes eran cajas negras con un paño rojo flotando).
    /// Piedra tallada con bordes biselados, zócalo y losa con vuelo, columnas en las esquinas, paneles rehundidos y argollas;
    /// encima un paño de terciopelo carmesí con borde dorado que cae por los lados con pliegues; velas de cera con chorreaduras
    /// y charcos de cera; cráneos, un cuenco de piedra con sangre y huesos en los escalones.
    public partial class Mundo
    {
        static Material matTerciopelo, matCeraAltar, matSangreAltar, matHierroAltar;

        /// Malla con transformación: todo lo que se agrega pasa por M (para armar cada altar en su propio marco y girarlo).
        class MallaT
        {
            public readonly Malla M = new Malla(); public Matrix4x4 T = Matrix4x4.identity; public float Escala = 0.8f;
            /// Triángulo plano con UV proyectada según su normal (la textura de piedra no se estira en ninguna cara).
            public void Tri(Vector3 a, Vector3 b, Vector3 c, Vector3 afuera)
            {
                a = T.MultiplyPoint3x4(a); b = T.MultiplyPoint3x4(b); c = T.MultiplyPoint3x4(c); afuera = T.MultiplyVector(afuera);
                Vector3 n = Vector3.Cross(b - a, c - a);
                if (Vector3.Dot(n, afuera) < 0f) { var x = b; b = c; c = x; n = -n; }
                n = n.normalized; float ax = Mathf.Abs(n.x), ay = Mathf.Abs(n.y), az = Mathf.Abs(n.z);
                System.Func<Vector3, Vector2> uv = p => ay >= ax && ay >= az ? new Vector2(p.x, p.z) / Escala : ax >= az ? new Vector2(p.z, p.y) / Escala : new Vector2(p.x, p.y) / Escala;
                int ia = M.V(a, uv(a)), ib = M.V(b, uv(b)), ic = M.V(c, uv(c)); M.Tri(ia, ib, ic);
            }
            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 afuera) { Tri(a, b, c, afuera); Tri(a, c, d, afuera); }

            /// Caja con aristas biseladas (caras, 12 chaflanes y 8 esquinas).
            public void Bisel(Vector3 c, Vector3 tam, float bis)
            {
                Vector3 h = tam / 2f; bis = Mathf.Min(bis, Mathf.Min(h.x, Mathf.Min(h.y, h.z)) * 0.9f);
                System.Func<int, int, int, int, Vector3> P = (sx, sy, sz, eje) =>
                {
                    var s = new Vector3(sx, sy, sz);
                    Vector3 p = c + Vector3.Scale(s, h - Vector3.one * bis);
                    p[eje] += s[eje] * bis; return p;
                };
                int[] S = { -1, 1 };
                for (int e = 0; e < 3; e++)
                    foreach (int se in S)
                    {
                        int a1 = (e + 1) % 3, a2 = (e + 2) % 3; var pts = new Vector3[4]; int k = 0;
                        foreach (var par in new[] { new[] { -1, -1 }, new[] { 1, -1 }, new[] { 1, 1 }, new[] { -1, 1 } })
                        { var s = new int[3]; s[e] = se; s[a1] = par[0]; s[a2] = par[1]; pts[k++] = P(s[0], s[1], s[2], e); }
                        var fuera = Vector3.zero; fuera[e] = se;
                        Quad(pts[0], pts[1], pts[2], pts[3], fuera);
                    }
                for (int e = 0; e < 3; e++)   // chaflanes: aristas paralelas al eje e
                {
                    int a1 = (e + 1) % 3, a2 = (e + 2) % 3;
                    foreach (int s1 in S) foreach (int s2 in S)
                    {
                        var lo = new int[3]; lo[e] = -1; lo[a1] = s1; lo[a2] = s2; var hi = new int[3]; hi[e] = 1; hi[a1] = s1; hi[a2] = s2;
                        var fuera = Vector3.zero; fuera[a1] = s1; fuera[a2] = s2;
                        Quad(P(lo[0], lo[1], lo[2], a1), P(hi[0], hi[1], hi[2], a1), P(hi[0], hi[1], hi[2], a2), P(lo[0], lo[1], lo[2], a2), fuera);
                    }
                }
                foreach (int sx in S) foreach (int sy in S) foreach (int sz in S)
                    Tri(P(sx, sy, sz, 0), P(sx, sy, sz, 1), P(sx, sy, sz, 2), new Vector3(sx, sy, sz));
            }

            /// Cilindro (o tronco de cono) de n lados, con tapa arriba.
            public void Cilindro(Vector3 b, float r0, float r1, float alto, int n, bool tapa = true)
            {
                for (int i = 0; i < n; i++)
                {
                    float a0 = i * Mathf.PI * 2f / n, a1 = (i + 1) * Mathf.PI * 2f / n;
                    Vector3 d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)), d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                    Quad(b + d0 * r0, b + d1 * r0, b + Vector3.up * alto + d1 * r1, b + Vector3.up * alto + d0 * r1, (d0 + d1));
                    if (tapa) Tri(b + Vector3.up * alto, b + Vector3.up * alto + d0 * r1, b + Vector3.up * alto + d1 * r1, Vector3.up);
                }
            }
        }

        static Texture2D TexturaTerciopelo()
        {
            const int N = 256; var t = new Texture2D(N, N, TextureFormat.RGB24, true) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float u = (x + 0.5f) / N, v = (y + 0.5f) / N;
                    float pelo = Mathf.PerlinNoise(x * 0.6f, y * 0.08f) * 0.5f + Mathf.PerlinNoise(x * 0.05f, y * 0.05f) * 0.5f;
                    Color c = Color.Lerp(new Color(0.22f, 0.01f, 0.02f), new Color(0.5f, 0.03f, 0.05f), pelo);
                    float bx = Mathf.Min(u, 1f - u);
                    if (bx < 0.11f && bx > 0.03f)
                    {
                        // borde bordado en oro: banda con un motivo de rombos
                        float rom = Mathf.Abs(((v * 40f) % 1f) - 0.5f) * 2f, dist = Mathf.Abs((bx - 0.07f) / 0.04f);
                        float hilo = (rom + dist < 1f ? 1f : 0f) * (0.7f + 0.3f * Mathf.PerlinNoise(x * 0.9f, y * 0.9f));
                        c = Color.Lerp(c, new Color(0.85f, 0.62f, 0.2f), hilo * 0.95f);
                        if (Mathf.Abs(bx - 0.035f) < 0.006f || Mathf.Abs(bx - 0.105f) < 0.006f) c = new Color(0.8f, 0.58f, 0.18f);
                    }
                    if (bx <= 0.03f) c *= 0.75f;   // ribete
                    px[y * N + x] = c;
                }
            t.SetPixels(px); t.Apply(); return t;
        }

        void MaterialesAltar()
        {
            if (matTerciopelo != null) return;
            matTerciopelo = new Material(Shader.Find("Standard")) { name = "coop_altar_terciopelo", mainTexture = TexturaTerciopelo() };
            matTerciopelo.SetFloat("_Glossiness", 0.28f);
            matCeraAltar = new Material(Shader.Find("Standard")) { name = "coop_altar_cera", color = new Color(0.86f, 0.8f, 0.68f) };
            matCeraAltar.SetFloat("_Glossiness", 0.55f); matCeraAltar.EnableKeyword("_EMISSION"); matCeraAltar.SetColor("_EmissionColor", new Color(0.12f, 0.07f, 0.03f));
            matSangreAltar = new Material(Shader.Find("Standard")) { name = "coop_altar_sangre", color = new Color(0.22f, 0.0f, 0.01f) };
            matSangreAltar.SetFloat("_Glossiness", 0.92f);
            matHierroAltar = new Material(Shader.Find("Standard")) { name = "coop_altar_hierro", color = new Color(0.16f, 0.15f, 0.14f) };
            matHierroAltar.SetFloat("_Metallic", 0.8f); matHierroAltar.SetFloat("_Glossiness", 0.35f);
            if (matHueso == null)
            {
                matHueso = new Material(Shader.Find("Standard")) { name = "coop_cueva_hueso", color = new Color(0.58f, 0.53f, 0.44f) };
                matHueso.SetFloat("_Glossiness", 0.12f);
                matCuenca = new Material(Shader.Find("Standard")) { name = "coop_cueva_cuenca", color = new Color(0.03f, 0.025f, 0.02f) };
                matCuenca.SetFloat("_Glossiness", 0f);
            }
        }

        /// Paño que cruza la losa a lo largo de z (ancho w) y cae por delante y por detrás con pliegues.
        static void Pano(MallaT m, float yTop, float largoLosa, float w, float caida, float semilla)
        {
            const int NX = 10, NZ = 26;
            float total = largoLosa + 2f * caida; var g = new Vector3[NX + 1, NZ + 1];
            for (int i = 0; i <= NX; i++)
                for (int j = 0; j <= NZ; j++)
                {
                    float x = -w / 2f + w * i / NX, s = -total / 2f + total * j / NZ;   // s: recorrido por el paño
                    float medio = largoLosa / 2f + 0.03f;
                    Vector3 p;
                    if (Mathf.Abs(s) <= medio) p = new Vector3(x, yTop + 0.012f, s);
                    else
                    {
                        float baja = Mathf.Abs(s) - medio, sg = Mathf.Sign(s);
                        float pliegue = Mathf.Sin(x * 9f + semilla + sg) * 0.035f * Mathf.Clamp01(baja / 0.25f);
                        p = new Vector3(x, yTop + 0.012f - baja, sg * (medio + 0.025f + pliegue + 0.02f * Mathf.Clamp01(baja * 3f)));
                    }
                    g[i, j] = p;
                }
            for (int i = 0; i < NX; i++)
                for (int j = 0; j < NZ; j++)
                {
                    Vector3 a = g[i, j], b = g[i + 1, j], c = g[i + 1, j + 1], d = g[i, j + 1];
                    Vector3 fuera = Vector3.Cross(b - a, d - a); if (fuera.y < -0.01f || (Mathf.Abs(fuera.y) < 0.01f && Vector3.Dot(fuera, new Vector3(0f, 0f, a.z)) < 0f)) fuera = -fuera;
                    // UV: u a lo ancho (el borde dorado en los costados), v a lo largo
                    var mm = m.M; Matrix4x4 T = m.T;
                    int ia = mm.V(T.MultiplyPoint3x4(a), new Vector2(i / (float)NX, j / 6f)), ib = mm.V(T.MultiplyPoint3x4(b), new Vector2((i + 1) / (float)NX, j / 6f));
                    int ic = mm.V(T.MultiplyPoint3x4(c), new Vector2((i + 1) / (float)NX, (j + 1) / 6f)), id = mm.V(T.MultiplyPoint3x4(d), new Vector2(i / (float)NX, (j + 1) / 6f));
                    // las dos caras (el paño se ve por dentro al caer)
                    mm.Tri(ia, ib, ic); mm.Tri(ia, ic, id); mm.Tri(ia, ic, ib); mm.Tri(ia, id, ic);
                }
        }

        /// Vela de cera con chorreaduras y un charco en la base. Devuelve la punta (para la llama), en coordenadas locales del altar.
        static Vector3 VelaCera(MallaT m, Vector3 b, float alto, float r, System.Random rng)
        {
            m.Cilindro(b, r * 1.9f, r * 1.6f, 0.012f, 12);                      // charco de cera
            m.Cilindro(b, r, r * 0.92f, alto, 10);                                 // cuerpo
            int chorros = 3 + rng.Next(3);
            for (int k = 0; k < chorros; k++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f, l = alto * (0.3f + 0.6f * (float)rng.NextDouble());
                Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                m.Cilindro(b + d * r * 0.93f + Vector3.up * (alto - l), r * 0.16f, r * 0.24f, l, 5);
            }
            m.Cilindro(b + Vector3.up * alto, r * 0.95f, r * 0.7f, 0.015f, 10);   // borde derretido
            return m.T.MultiplyPoint3x4(b + Vector3.up * (alto + 0.02f));   // en coordenadas de la red
        }

        void ArmarAltaresGuarida(Transform red, Vector3 cza, Vector2[] lados, System.Random rng)
        {
            MaterialesAltar();
            var piedraBase = matCueva != null ? matCueva : matRoca;
            var piedra = new Material(piedraBase) { name = "coop_altar_piedra" }; piedra.color = piedraBase.color * 0.62f; piedra.color = new Color(piedra.color.r, piedra.color.g, piedra.color.b, 1f);
            var panel = new Material(piedraBase) { name = "coop_altar_panel" }; panel.color = new Color(0.2f, 0.17f, 0.16f, 1f);
            var mp = new MallaT(); var mpan = new MallaT(); var mpano = new MallaT(); var mcera = new MallaT(); var mhierro = new MallaT(); var msangre = new MallaT();
            var mh = new Malla(); var mc = new Malla();
            var llamas = new List<Vector3>(); var luces = new List<Vector3>();

            // ---------------- gran altar central ----------------
            var T = Matrix4x4.TRS(cza, Quaternion.identity, Vector3.one);
            foreach (var x in new[] { mp, mpan, mpano, mcera, mhierro, msangre }) x.T = T;
            mp.Bisel(new Vector3(0f, 0.22f, 0f), new Vector3(10f, 0.48f, 10f), 0.1f);       // primer escalón
            mp.Bisel(new Vector3(0f, 0.68f, 0f), new Vector3(7.4f, 0.48f, 7.4f), 0.09f);     // segundo
            mp.Bisel(new Vector3(0f, 1.0f, 0f), new Vector3(4.8f, 0.18f, 3.6f), 0.05f);      // zócalo
            mp.Bisel(new Vector3(0f, 1.6f, 0f), new Vector3(4.2f, 1.05f, 3.0f), 0.06f);      // cuerpo
            mp.Bisel(new Vector3(0f, 2.22f, 0f), new Vector3(4.9f, 0.22f, 3.7f), 0.06f);     // losa con vuelo
            mp.Bisel(new Vector3(0f, 2.08f, 0f), new Vector3(4.5f, 0.08f, 3.3f), 0.03f);     // moldura bajo la losa
            foreach (float sx in new[] { -1f, 1f }) foreach (float sz in new[] { -1f, 1f })
            {
                Vector3 c = new Vector3(sx * 2.05f, 0f, sz * 1.45f);
                mp.Bisel(c + new Vector3(0f, 1.6f, 0f), new Vector3(0.42f, 1.05f, 0.42f), 0.05f);       // columna de esquina
                mp.Bisel(c + new Vector3(0f, 2.05f, 0f), new Vector3(0.55f, 0.12f, 0.55f), 0.03f);      // capitel
                mp.Bisel(c + new Vector3(0f, 1.13f, 0f), new Vector3(0.55f, 0.1f, 0.55f), 0.03f);       // basa
            }
            foreach (float sz in new[] { -1f, 1f })
            {
                mpan.Bisel(new Vector3(0f, 1.6f, sz * 1.505f), new Vector3(2.9f, 0.7f, 0.03f), 0.012f);   // panel rehundido (más oscuro)
                mp.Bisel(new Vector3(0f, 1.6f, sz * 1.52f), new Vector3(0.9f, 0.5f, 0.04f), 0.02f);      // medallón tallado
                foreach (float sx in new[] { -0.95f, 0.95f })
                {
                    Vector3 an = new Vector3(sx, 1.72f, sz * 1.56f);
                    mhierro.Cilindro(an + Vector3.down * 0.02f, 0.03f, 0.03f, 0.04f, 6);                   // clavo
                    for (int k = 0; k < 10; k++)                                                           // argolla colgando
                    {
                        float a0 = k * Mathf.PI * 2f / 10f; Vector3 p = an + new Vector3(Mathf.Sin(a0) * 0.1f, -0.12f - Mathf.Cos(a0) * 0.1f, sz * 0.012f);
                        mhierro.Bisel(p, new Vector3(0.045f, 0.045f, 0.022f), 0.01f);
                    }
                }
            }
            foreach (float sx in new[] { -1f, 1f }) mpan.Bisel(new Vector3(sx * 2.105f, 1.6f, 0f), new Vector3(0.03f, 0.7f, 2.0f), 0.012f);
            Pano(mpano, 2.33f, 3.7f, 1.5f, 0.75f, 1.3f);
            // encima: cráneo, cuenco con sangre y velas en las cuatro esquinas de la losa y en las esquinas del segundo escalón
            Craneo(mh, mc, cza + new Vector3(0f, 2.36f, 0.25f), 180f, 1.35f);
            mp.Cilindro(new Vector3(0.9f, 2.33f, -0.6f), 0.22f, 0.3f, 0.16f, 14, false);
            msangre.Cilindro(new Vector3(0.9f, 2.33f, -0.6f), 0.27f, 0.27f, 0.145f, 14);
            foreach (float sx in new[] { -1f, 1f }) foreach (float sz in new[] { -1f, 1f })
            {
                for (int k = 0; k < 3; k++)
                {
                    Vector3 b = new Vector3(sx * (2.05f - 0.18f * k), 2.33f, sz * (1.45f - 0.22f * (k % 2)));
                    llamas.Add(VelaCera(mcera, b, 0.2f + 0.3f * (float)rng.NextDouble(), 0.045f + 0.012f * (float)rng.NextDouble(), rng));
                }
                for (int k = 0; k < 2; k++)
                    llamas.Add(VelaCera(mcera, new Vector3(sx * (3.3f - 0.25f * k), 0.92f, sz * (3.3f - 0.2f * k)), 0.3f + 0.35f * (float)rng.NextDouble(), 0.06f, rng));
                luces.Add(cza + new Vector3(sx * 2.1f, 3.0f, sz * 1.5f));
            }
            // cráneos y huesos en los escalones
            for (int k = 0; k < 6; k++) { float a = k * Mathf.PI / 3f + 0.4f; Craneo(mh, mc, cza + new Vector3(Mathf.Cos(a) * 4.3f, 0.47f, Mathf.Sin(a) * 4.3f), a * Mathf.Rad2Deg + 90f, 0.9f + 0.3f * (float)rng.NextDouble()); }

            // ---------------- altares laterales ----------------
            foreach (var l in lados)
            {
                Vector3 c0 = new Vector3(l.x, AlturaSueloCueva(l.x, l.y) - 0.05f, l.y);
                float yaw = l.x < 0f ? 90f : -90f;   // el frente mira al centro de la sala
                var Tl = Matrix4x4.TRS(c0, Quaternion.Euler(0f, yaw, 0f), Vector3.one);
                foreach (var x in new[] { mp, mpan, mpano, mcera, mhierro, msangre }) x.T = Tl;
                mp.Bisel(new Vector3(0f, 0.12f, 0f), new Vector3(3.0f, 0.24f, 1.7f), 0.05f);    // zócalo
                mp.Bisel(new Vector3(0f, 0.68f, 0f), new Vector3(2.6f, 0.9f, 1.3f), 0.05f);     // cuerpo
                mp.Bisel(new Vector3(0f, 1.22f, 0f), new Vector3(3.0f, 0.18f, 1.7f), 0.05f);    // losa
                foreach (float sx in new[] { -1f, 1f }) mp.Bisel(new Vector3(sx * 1.25f, 0.68f, 0f), new Vector3(0.28f, 0.9f, 1.42f), 0.04f);   // pilastras
                mpan.Bisel(new Vector3(0f, 0.68f, 0.655f), new Vector3(1.9f, 0.55f, 0.03f), 0.01f);
                mpan.Bisel(new Vector3(0f, 0.68f, -0.655f), new Vector3(1.9f, 0.55f, 0.03f), 0.01f);
                // el paño cruza la losa a lo ancho y cae por delante y por detrás
                var Tp = Tl * Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0f, 0f, 0f), Vector3.one);
                mpano.T = Tp; Pano(mpano, 1.31f, 1.7f, 0.9f, 0.55f, l.y);
                Craneo(mh, mc, Tl.MultiplyPoint3x4(new Vector3(0.75f, 1.33f, 0.1f)), yaw + 180f, 1.05f);
                int nv = 3 + rng.Next(3);
                for (int k = 0; k < nv; k++)
                {
                    Vector3 b = new Vector3(-1.2f + 0.32f * k + 0.05f * (float)rng.NextDouble(), 1.31f, -0.45f + 0.25f * (float)rng.NextDouble());
                    if (Mathf.Abs(b.x) < 0.5f) b.z = -0.55f;   // no sobre el paño
                    llamas.Add(VelaCera(mcera, b, 0.18f + 0.3f * (float)rng.NextDouble(), 0.04f + 0.012f * (float)rng.NextDouble(), rng));
                }
                luces.Add(Tl.MultiplyPoint3x4(new Vector3(-0.6f, 2.0f, -0.2f)));
            }

            mp.M.Crear("coop_altar_piedra", red, piedra, true);
            mpan.M.Crear("coop_altar_paneles", red, panel, false);
            mpano.M.Crear("coop_altar_pano", red, matTerciopelo, false);
            mcera.M.Crear("coop_altar_velas", red, matCeraAltar, false);
            mhierro.M.Crear("coop_altar_hierro", red, matHierroAltar, false);
            msangre.M.Crear("coop_altar_sangre", red, matSangreAltar, false);
            mh.Crear("coop_altar_craneos", red, matHueso, false); mc.Crear("coop_altar_cuencas", red, matCuenca, false);
            // llamas: las del altar central en espacio del centro; las demás ya vienen en su marco: se recalculan a mundo
            int nl = 0;
            foreach (var p in llamas) { Llama.Crear(red.TransformPoint(p), red, 0.1f, false, false); nl++; }
            foreach (var p in luces)
            {
                var g = new GameObject("coop_luz_altar"); g.transform.SetParent(red, false); g.transform.localPosition = p;
                var lz = g.AddComponent<Light>(); lz.type = LightType.Point; lz.color = new Color(1f, 0.42f, 0.16f); lz.range = 7f; lz.intensity = 1.3f; lz.shadows = LightShadows.None;
                g.AddComponent<Titilar>();
            }
            plugin.Log("Minas: altares de la guarida rehechos (" + (lados.Length + 1) + " altares, " + nl + " velas)");
        }
    }
}

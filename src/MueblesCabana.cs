// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using System.Collections.Generic;
using UnityEngine;

namespace WitchHuntCoop
{
    /// Muebles de calidad para las cabañas (2026-11-18): geometría propia del mod en mallas combinadas, en vez de cajas de colores.
    /// Mesa de tablones con travesaños, taburetes de patas abiertas, cama con bastidor, colchón hundido y manta caída, cajas de
    /// tablas clavadas, barril de duelas con aros, estante con botellas, tarros y libros, chimenea de piedras irregulares con
    /// repisa de madera, telarañas en las esquinas, hierbas colgadas, alfombra y polvo en el aire.
    public partial class Mundo
    {
        static Material matCera, matVidrioMueble, matBarro, matCuero, matPeltre, matHierba, matTelarana, matHollin;

        void MaterialesMuebles()
        {
            if (matCera != null) return;
            MaterialesCueva();
            matCera = Liso("coop_mueble_cera", new Color(0.86f, 0.8f, 0.66f), 0.25f);
            matVidrioMueble = Liso("coop_mueble_vidrio", new Color(0.07f, 0.16f, 0.09f), 0.85f);
            matBarro = Liso("coop_mueble_barro", new Color(0.42f, 0.27f, 0.17f), 0.2f);
            matCuero = Liso("coop_mueble_cuero", new Color(0.3f, 0.13f, 0.08f), 0.15f);
            matPeltre = Liso("coop_mueble_peltre", new Color(0.42f, 0.42f, 0.4f), 0.55f); matPeltre.SetFloat("_Metallic", 0.7f);
            matHierba = Liso("coop_mueble_hierba", new Color(0.3f, 0.32f, 0.16f), 0.05f);
            matHollin = Liso("coop_mueble_hollin", new Color(0.06f, 0.05f, 0.05f), 0.02f);
            // telaraña: textura con radios y espiral, recortada por alfa
            const int N = 128;
            var tx = new Texture2D(N, N, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
            {
                float r = Mathf.Sqrt(x * x + y * y) / N, ang = Mathf.Atan2(y, x);
                float radio = Mathf.Abs(Mathf.Sin(ang * 9f)) < 0.035f / Mathf.Max(0.15f, r) ? 1f : 0f;     // radios desde la esquina
                float esp = Mathf.Abs(Mathf.Sin((r * 22f + ang * 0.6f) * Mathf.PI)) < 0.07f ? 1f : 0f;     // vueltas de la espiral
                float a = Mathf.Max(radio, esp) * Mathf.Clamp01(1.15f - r);
                tx.SetPixel(x, y, new Color(0.85f, 0.85f, 0.82f, a));
            }
            tx.Apply();
            matTelarana = new Material(Shader.Find("Legacy Shaders/Transparent/Cutout/Diffuse")) { name = "coop_telarana", mainTexture = tx };
            matTelarana.SetFloat("_Cutoff", 0.5f);
        }

        static Material Liso(string n, Color c, float brillo)
        {
            var m = new Material(Shader.Find("Standard")) { name = n, color = c };
            m.SetFloat("_Glossiness", brillo); return m;
        }

        static Material Tinte(Material baseM, Color c)
        {
            var m = baseM != null ? new Material(baseM) : new Material(Shader.Find("Standard"));
            m.color = c; m.mainTextureScale = Vector2.one; return m;
        }

        // ---------------- Piezas básicas (en coordenadas del mundo) ----------------

        /// Bloque con UV en metros (la veta va por el lado más largo).
        static void Bloque(Malla m, Vector3 c, Vector3 tam, Quaternion q, float tile = 0.9f)
        {
            Vector3 X = q * Vector3.right, Y = q * Vector3.up, Z = q * Vector3.forward, h = tam * 0.5f;
            var caras = new[] {
                new[] { X, Y, Z }, new[] { -X, Z, Y }, new[] { Y, Z, X }, new[] { -Y, X, Z }, new[] { Z, X, Y }, new[] { -Z, Y, X } };
            var medio = new[] { h.x, h.x, h.y, h.y, h.z, h.z };
            for (int f = 0; f < 6; f++)
            {
                Vector3 N = caras[f][0], A = caras[f][1], B = caras[f][2];
                float ha = Mathf.Abs(Vector3.Dot(A, X)) > 0.5f ? h.x : Mathf.Abs(Vector3.Dot(A, Y)) > 0.5f ? h.y : h.z;
                float hb = Mathf.Abs(Vector3.Dot(B, X)) > 0.5f ? h.x : Mathf.Abs(Vector3.Dot(B, Y)) > 0.5f ? h.y : h.z;
                Vector3 o = c + N * medio[f];
                bool largaA = ha >= hb;   // la u sigue al lado más largo de la cara
                System.Func<float, float, Vector2> uvf = (sa, sb) => largaA ? new Vector2(sa * ha * 2f / tile, sb * hb * 2f / tile) : new Vector2(sb * hb * 2f / tile, sa * ha * 2f / tile);
                int a0 = m.V(o - A * ha - B * hb, uvf(0f, 0f)), b0 = m.V(o + A * ha - B * hb, uvf(1f, 0f));
                int c0 = m.V(o + A * ha + B * hb, uvf(1f, 1f)), d0 = m.V(o - A * ha + B * hb, uvf(0f, 1f));
                m.Tri(a0, b0, d0); m.Tri(b0, c0, d0);
            }
        }

        /// Pieza torneada alrededor del eje "arriba" de q: alturas y radios por anillo; tapas opcionales.
        static void Torno(Malla m, Vector3 b, Quaternion q, float[] alt, float[] rad, int lados, bool tapaAbajo, bool tapaArriba)
        {
            int fila = lados + 1, i0 = -1;
            for (int k = 0; k < alt.Length; k++)
                for (int i = 0; i <= lados; i++)
                {
                    float ang = i / (float)lados * Mathf.PI * 2f;
                    int id = m.V(b + q * new Vector3(Mathf.Cos(ang) * rad[k], alt[k], Mathf.Sin(ang) * rad[k]), new Vector2(i / (float)lados * rad[k] * 6.28f / 0.4f, alt[k] / 0.4f));
                    if (i0 < 0) i0 = id;
                }
            for (int k = 0; k < alt.Length - 1; k++)
                for (int i = 0; i < lados; i++)
                {
                    int p = i0 + k * fila + i, pk = p + 1, pr = p + fila, pr1 = pr + 1;
                    m.Tri(p, pr, pk); m.Tri(pk, pr, pr1);
                }
            if (tapaArriba)
            {
                int ult = alt.Length - 1, c = m.V(b + q * new Vector3(0f, alt[ult], 0f), new Vector2(0.5f, 0.5f));
                for (int i = 0; i < lados; i++) m.Tri(c, i0 + ult * fila + i + 1, i0 + ult * fila + i);
            }
            if (tapaAbajo)
            {
                int c = m.V(b + q * new Vector3(0f, alt[0], 0f), new Vector2(0.5f, 0.5f));
                for (int i = 0; i < lados; i++) m.Tri(c, i0 + i, i0 + i + 1);
            }
        }

        /// Palo redondo de a a b.
        static void Palo(Malla m, Vector3 a, Vector3 b, float r, int lados = 6)
        {
            Vector3 d = b - a;
            Torno(m, a, Quaternion.FromToRotation(Vector3.up, d.normalized), new[] { 0f, d.magnitude }, new[] { r, r * 0.92f }, lados, true, true);
        }

        // ---------------- Muebles ----------------

        /// Mesa de 5 tablones con juntas, patas, faldón y travesaño; encima platos, jarra, libro y una vela con llama real.
        void MesaCabana(Transform pad, Vector3 c, Quaternion q, Material mad, System.Random rng)
        {
            var m = new Malla();
            const float L = 1.5f, A = 0.85f, H = 0.78f, gr = 0.045f;
            float tabla = A / 5f;
            for (int i = 0; i < 5; i++)
            {
                float z = -A / 2f + tabla * (i + 0.5f);
                var qi = q * Quaternion.Euler(((float)rng.NextDouble() - 0.5f) * 0.8f, ((float)rng.NextDouble() - 0.5f) * 0.6f, 0f);
                Bloque(m, c + q * new Vector3(((float)rng.NextDouble() - 0.5f) * 0.03f, H - gr / 2f + ((float)rng.NextDouble() - 0.5f) * 0.006f, z), new Vector3(L, gr, tabla - 0.009f), qi);
            }
            foreach (var sx in new[] { -1f, 1f }) foreach (var sz in new[] { -1f, 1f })
                Bloque(m, c + q * new Vector3(sx * (L / 2f - 0.1f), (H - gr) / 2f, sz * (A / 2f - 0.08f)), new Vector3(0.075f, H - gr, 0.075f), q);
            foreach (var sz in new[] { -1f, 1f }) Bloque(m, c + q * new Vector3(0f, H - gr - 0.06f, sz * (A / 2f - 0.08f)), new Vector3(L - 0.25f, 0.1f, 0.03f), q);
            foreach (var sx in new[] { -1f, 1f }) Bloque(m, c + q * new Vector3(sx * (L / 2f - 0.1f), H - gr - 0.06f, 0f), new Vector3(0.03f, 0.1f, A - 0.2f), q);
            Bloque(m, c + q * new Vector3(0f, 0.16f, 0f), new Vector3(L - 0.25f, 0.05f, 0.05f), q);
            foreach (var sx in new[] { -1f, 1f }) Bloque(m, c + q * new Vector3(sx * (L / 2f - 0.1f), 0.16f, 0f), new Vector3(0.05f, 0.05f, A - 0.2f), q);
            m.CrearMundo("coop_mueble_mesa", pad, mad, true);

            // taburetes a los lados largos
            foreach (var sz in new[] { -1f, 1f }) Taburete(pad, c + q * new Vector3(((float)rng.NextDouble() - 0.5f) * 0.4f, 0f, sz * (A / 2f + 0.32f)), mad, rng);

            // sobre la mesa
            Vector3 top = c + Vector3.up * H;
            var mp = new Malla(); var mb = new Malla(); var mcera = new Malla(); var mcu = new Malla();
            foreach (var px in new[] { -0.4f, 0.35f })
                Torno(mp, top + q * new Vector3(px, 0f, ((float)rng.NextDouble() - 0.5f) * 0.2f), q, new[] { 0f, 0.012f, 0.02f, 0.02f }, new[] { 0.07f, 0.1f, 0.125f, 0.115f }, 16, true, true);
            Vector3 jarra = top + q * new Vector3(0.05f, 0f, 0.2f);
            Torno(mb, jarra, q, new[] { 0f, 0.03f, 0.13f, 0.17f, 0.2f }, new[] { 0.05f, 0.065f, 0.06f, 0.045f, 0.05f }, 12, true, false);
            Bloque(mb, jarra + q * new Vector3(0.075f, 0.11f, 0f), new Vector3(0.02f, 0.09f, 0.025f), q);
            Bloque(mcu, top + q * new Vector3(-0.25f, 0.025f, -0.25f), new Vector3(0.22f, 0.05f, 0.3f), q * Quaternion.Euler(0f, 17f, 0f), 0.3f);
            // vela en un platillo de hierro
            Vector3 pv = top + q * new Vector3(0.15f, 0f, -0.2f);
            Torno(mp, pv, q, new[] { 0f, 0.012f }, new[] { 0.06f, 0.065f }, 12, true, true);
            Torno(mcera, pv + Vector3.up * 0.012f, q, new[] { 0f, 0.13f, 0.15f }, new[] { 0.028f, 0.027f, 0.022f }, 10, false, true);
            mp.CrearMundo("coop_mueble_platos", pad, matPeltre, false);
            mb.CrearMundo("coop_mueble_jarra", pad, matBarro, false);
            mcera.CrearMundo("coop_mueble_vela", pad, matCera, false);
            mcu.CrearMundo("coop_mueble_libro", pad, matCuero, false);
            Llama.Crear(pv + Vector3.up * 0.17f, pad, 0.1f, false, false);
            var l = new GameObject("coop_luz_vela"); l.transform.SetParent(pad, true); l.transform.position = pv + Vector3.up * 0.3f;
            var lz = l.AddComponent<Light>(); lz.type = LightType.Point; lz.color = new Color(1f, 0.6f, 0.3f); lz.range = 6f; lz.intensity = 1.2f; lz.shadows = LightShadows.None;
            l.AddComponent<Titilar>();
        }

        void Taburete(Transform pad, Vector3 c, Material mad, System.Random rng)
        {
            var m = new Malla();
            var q = Quaternion.Euler(((float)rng.NextDouble() - 0.5f) * 3f, (float)rng.NextDouble() * 360f, ((float)rng.NextDouble() - 0.5f) * 3f);
            Torno(m, c + Vector3.up * 0.43f, q, new[] { 0f, 0.04f, 0.045f }, new[] { 0.18f, 0.19f, 0.175f }, 14, true, true);
            for (int i = 0; i < 3; i++)
            {
                float a = i / 3f * Mathf.PI * 2f;
                Vector3 dir = q * new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Palo(m, c + dir * 0.23f, c + Vector3.up * 0.44f + dir * 0.1f, 0.022f);
            }
            m.CrearMundo("coop_mueble_taburete", pad, mad, true);
        }

        /// Cama: postes, largueros, cabecera de tablas, colchón hundido en el medio y manta que cae por un lado.
        void CamaCabana(Transform pad, Vector3 c, Quaternion q, Material mad, System.Random rng)
        {
            var m = new Malla();
            const float W = 0.95f, L = 2.0f;
            foreach (var sx in new[] { -1f, 1f }) foreach (var sz in new[] { -1f, 1f })
                Bloque(m, c + q * new Vector3(sx * W / 2f, sz > 0 ? 0.45f : 0.3f, sz * L / 2f), new Vector3(0.08f, sz > 0 ? 0.9f : 0.6f, 0.08f), q);
            foreach (var sx in new[] { -1f, 1f }) Bloque(m, c + q * new Vector3(sx * W / 2f, 0.3f, 0f), new Vector3(0.05f, 0.14f, L - 0.08f), q);
            foreach (var sz in new[] { -1f, 1f }) Bloque(m, c + q * new Vector3(0f, 0.3f, sz * L / 2f), new Vector3(W - 0.08f, 0.14f, 0.05f), q);
            for (int i = 0; i < 4; i++) Bloque(m, c + q * new Vector3(-W / 2f + 0.14f + i * (W - 0.28f) / 3f, 0.62f, L / 2f), new Vector3(0.17f, 0.42f, 0.03f), q);   // cabecera
            for (int i = 0; i < 9; i++) Bloque(m, c + q * new Vector3(0f, 0.3f, -L / 2f + 0.15f + i * (L - 0.3f) / 8f), new Vector3(W - 0.1f, 0.025f, 0.09f), q);   // listones
            m.CrearMundo("coop_mueble_cama", pad, mad, true);

            // colchón: superficie hundida en el medio, con bordes que bajan hasta los listones
            var mc = new Malla();
            const int NX = 8, NZ = 16; float y0 = 0.33f, alto = 0.14f;
            var ids = new int[NX + 1, NZ + 1];
            for (int i = 0; i <= NX; i++) for (int j = 0; j <= NZ; j++)
            {
                float u = i / (float)NX, v = j / (float)NZ;
                float x = (u - 0.5f) * (W - 0.12f), z = (v - 0.5f) * (L - 0.12f);
                float borde = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
                float y = y0 + alto * Mathf.SmoothStep(0f, 1f, borde * 6f) - 0.035f * Mathf.Sin(Mathf.PI * u) * Mathf.Sin(Mathf.PI * v) + 0.008f * Mathf.PerlinNoise(x * 9f, z * 9f);
                ids[i, j] = mc.V(c + q * new Vector3(x, y, z), new Vector2(x, z));
            }
            for (int i = 0; i < NX; i++) for (int j = 0; j < NZ; j++) { mc.Tri(ids[i, j], ids[i, j + 1], ids[i + 1, j]); mc.Tri(ids[i + 1, j], ids[i, j + 1], ids[i + 1, j + 1]); }
            var colchon = mc.CrearMundo("coop_mueble_colchon", pad, null, false);
            Telar(colchon, new Color(0.46f, 0.38f, 0.26f), new Color(0.36f, 0.29f, 0.2f), 1, 6, 1f, 1f);
            colchon.GetComponent<Renderer>().sharedMaterial.mainTextureScale = Vector2.one * 2f;

            // manta: tapa los dos tercios de los pies y cae por un costado
            var mm = new Malla();
            const int MX = 12, MZ = 10; float cae = rng.NextDouble() < 0.5 ? -1f : 1f;
            var im = new int[MX + 1, MZ + 1];
            for (int i = 0; i <= MX; i++) for (int j = 0; j <= MZ; j++)
            {
                float u = i / (float)MX, v = j / (float)MZ;
                float x = (u - 0.5f) * (W + 0.5f), z = -L / 2f + 0.05f + v * L * 0.62f;
                float yc = y0 + alto + 0.015f - 0.03f * Mathf.Sin(Mathf.PI * Mathf.Clamp01((x / (W - 0.12f)) + 0.5f));
                float fuera = Mathf.Abs(x) - (W / 2f - 0.02f);
                float y = yc; float xx = x;
                if (fuera > 0f)
                {
                    if (Mathf.Sign(x) == cae) { y = yc - fuera * 1.6f; xx = Mathf.Sign(x) * (W / 2f + 0.03f + fuera * 0.15f); }   // cae por el costado
                    else { y = yc - fuera * 0.4f; xx = Mathf.Sign(x) * (W / 2f + 0.02f); }                                         // el otro lado se mete bajo el colchón
                }
                y += 0.012f * Mathf.Sin(v * 17f + u * 5f);   // pliegues
                im[i, j] = mm.V(c + q * new Vector3(xx, y, z), new Vector2(u * 1.4f, v * 1.2f));
            }
            for (int i = 0; i < MX; i++) for (int j = 0; j < MZ; j++) { mm.Tri(im[i, j], im[i, j + 1], im[i + 1, j]); mm.Tri(im[i + 1, j], im[i, j + 1], im[i + 1, j + 1]); }
            // por debajo también (la manta se ve de los dos lados al caer)
            for (int i = 0; i < MX; i++) for (int j = 0; j < MZ; j++) { mm.Tri(im[i, j], im[i + 1, j], im[i, j + 1]); mm.Tri(im[i + 1, j], im[i + 1, j + 1], im[i, j + 1]); }
            var manta = mm.CrearMundo("coop_mueble_manta", pad, null, false);
            Telar(manta, new Color(0.45f, 0.12f, 0.1f), new Color(0.2f, 0.07f, 0.06f), 2, 6, 1f, 1f);
            manta.GetComponent<Renderer>().sharedMaterial.mainTextureScale = Vector2.one * 2f;
            // almohada
            var ma = new Malla();
            Bloque(ma, c + q * new Vector3(0f, y0 + alto + 0.05f, L / 2f - 0.28f), new Vector3(0.5f, 0.1f, 0.3f), q * Quaternion.Euler(4f, ((float)rng.NextDouble() - 0.5f) * 10f, 0f), 0.5f);
            var alm = ma.CrearMundo("coop_mueble_almohada", pad, null, false);
            Telar(alm, new Color(0.74f, 0.69f, 0.58f), new Color(0.62f, 0.57f, 0.46f), 0, 10, 1f, 1f);
        }

        /// Caja de tablas clavadas: esquineros, tres tablas por lado y tapa de tablas.
        static void CajonTablas(Malla m, Vector3 c, float s, Quaternion q)
        {
            float h = s / 2f, t = 0.022f;
            foreach (var sx in new[] { -1f, 1f }) foreach (var sz in new[] { -1f, 1f })
                Bloque(m, c + q * new Vector3(sx * (h - 0.03f), 0f, sz * (h - 0.03f)), new Vector3(0.06f, s, 0.06f), q);
            for (int k = 0; k < 3; k++)
            {
                float y = -h + s * (k + 0.5f) / 3f, alto = s / 3f - 0.012f;
                foreach (var sz in new[] { -1f, 1f }) Bloque(m, c + q * new Vector3(0f, y, sz * (h - t / 2f)), new Vector3(s - 0.12f, alto, t), q);
                foreach (var sx in new[] { -1f, 1f }) Bloque(m, c + q * new Vector3(sx * (h - t / 2f), y, 0f), new Vector3(t, alto, s - 0.12f), q);
                Bloque(m, c + q * new Vector3(0f, h - t / 2f, -h + s * (k + 0.5f) / 3f), new Vector3(s, t, s / 3f - 0.012f), q);
            }
            Bloque(m, c + q * new Vector3(0f, 0f, h + 0.002f), new Vector3(s * 1.25f, 0.05f, t), q * Quaternion.Euler(0f, 0f, 45f));   // tabla en diagonal del frente
        }

        void CajasCabana(Transform pad, Vector3 c, Quaternion q, Material mad, System.Random rng)
        {
            var m = new Malla();
            CajonTablas(m, c + Vector3.up * 0.3f, 0.6f, Quaternion.Euler(0f, 12f, 0f) * q);
            CajonTablas(m, c + q * new Vector3(0.66f, 0.22f, -0.1f), 0.44f, Quaternion.Euler(0f, -20f, 0f) * q);
            CajonTablas(m, c + Vector3.up * 0.79f, 0.38f, Quaternion.Euler(0f, 35f, 0f) * q);
            m.CrearMundo("coop_mueble_cajas", pad, mad, true);
        }

        /// Barril de duelas (abombado) con cuatro aros de hierro y tapa hundida.
        void BarrilCabana(Transform pad, Vector3 c, Material mad, Material hierro)
        {
            var m = new Malla(); var mh = new Malla();
            const int K = 9; float H = 0.9f, R = 0.32f;
            var alt = new float[K]; var rad = new float[K];
            for (int k = 0; k < K; k++) { float t = k / (float)(K - 1); alt[k] = t * H; rad[k] = R * (0.86f + 0.14f * Mathf.Sin(Mathf.PI * t)); }
            Torno(m, c, Quaternion.identity, alt, rad, 18, true, false);
            Torno(m, c + Vector3.up * (H - 0.03f), Quaternion.identity, new[] { 0f }, new[] { R * 0.83f }, 18, false, true);
            foreach (var t in new[] { 0.07f, 0.3f, 0.7f, 0.93f })
            {
                float r = R * (0.86f + 0.14f * Mathf.Sin(Mathf.PI * t)) + 0.008f;
                Torno(mh, c + Vector3.up * (t * H - 0.02f), Quaternion.identity, new[] { 0f, 0.04f }, new[] { r, r }, 18, false, false);
            }
            m.CrearMundo("coop_mueble_barril", pad, mad, true);
            mh.CrearMundo("coop_mueble_barril_aros", pad, hierro, false);
        }

        /// Estante de dos tablas con ménsulas; botellas, tarros y libros.
        void EstanteCabana(Transform pad, Vector3 c, Quaternion q, Material mad, System.Random rng)
        {
            var m = new Malla(); var mv = new Malla(); var mb = new Malla(); var ml = new Malla();
            foreach (var y in new[] { 1.2f, 1.7f })
            {
                Bloque(m, c + q * new Vector3(0f, y, 0f), new Vector3(1.4f, 0.04f, 0.26f), q);
                foreach (var sx in new[] { -0.55f, 0.55f }) Bloque(m, c + q * new Vector3(sx, y - 0.09f, 0.07f), new Vector3(0.04f, 0.15f, 0.12f), q);
                float x = -0.6f;
                while (x < 0.6f)
                {
                    double t = rng.NextDouble();
                    Vector3 p = c + q * new Vector3(x, y + 0.02f, ((float)rng.NextDouble() - 0.5f) * 0.08f);
                    if (t < 0.35) { Torno(mv, p, q, new[] { 0f, 0.17f, 0.21f, 0.29f, 0.3f }, new[] { 0.04f, 0.04f, 0.016f, 0.013f, 0.016f }, 10, true, true); x += 0.12f; }
                    else if (t < 0.55) { Torno(mb, p, q, new[] { 0f, 0.02f, 0.11f, 0.13f, 0.14f }, new[] { 0.05f, 0.06f, 0.055f, 0.04f, 0.045f }, 12, true, true); x += 0.15f; }
                    else
                    {
                        int n = 2 + rng.Next(4);
                        for (int k = 0; k < n && x < 0.6f; k++)
                        {
                            float alto = 0.18f + 0.06f * (float)rng.NextDouble(), gros = 0.035f + 0.025f * (float)rng.NextDouble();
                            Bloque(ml, p + q * new Vector3(0f, alto / 2f, 0f), new Vector3(gros, alto, 0.15f), q * Quaternion.Euler(0f, 0f, k == n - 1 ? -12f : 0f), 0.3f);
                            x += gros + 0.004f; p = c + q * new Vector3(x, y + 0.02f, 0f);
                        }
                        x += 0.05f;
                    }
                }
            }
            m.CrearMundo("coop_mueble_estante", pad, mad, false);
            mv.CrearMundo("coop_mueble_botellas", pad, matVidrioMueble, false);
            mb.CrearMundo("coop_mueble_tarros", pad, matBarro, false);
            ml.CrearMundo("coop_mueble_libros", pad, matCuero, false);
        }

        /// Chimenea de piedras irregulares (granito) con repisa de madera; la fogata va dentro.
        void ChimeneaPiedra(Transform pad, Vector3 C, Vector3 da, Vector3 db, Material mad, System.Random rng, float alturaTecho)
        {
            var q = Quaternion.LookRotation(-da, Vector3.up);   // z local = hacia el cuarto
            var mp = new Malla(); var mh = new Malla(); var mm = new Malla();
            // hogar: losa de piedra delante
            Bloque(mp, C + q * new Vector3(0f, 0.05f, 0.25f), new Vector3(1.9f, 0.1f, 1.0f), q, 1.2f);
            // jambas y pecho de la chimenea hechos de piedras sueltas
            MuroPiedras(mp, C, q, rng, -0.95f, -0.42f, 0.1f, 1.3f, 0.75f);
            MuroPiedras(mp, C, q, rng, 0.42f, 0.95f, 0.1f, 1.3f, 0.75f);
            MuroPiedras(mp, C, q, rng, -0.95f, 0.95f, 1.5f, Mathf.Max(2.2f, alturaTecho), 0.7f);     // pecho hasta el techo
            // repisa de madera
            Bloque(mm, C + q * new Vector3(0f, 1.4f, 0.06f), new Vector3(2.05f, 0.2f, 0.88f), q);
            // fondo y techo del hogar con hollín
            Bloque(mh, C + q * new Vector3(0f, 0.7f, -0.32f), new Vector3(0.84f, 1.2f, 0.06f), q);
            Bloque(mh, C + q * new Vector3(0f, 1.29f, 0f), new Vector3(0.84f, 0.02f, 0.66f), q);
            foreach (var sx in new[] { -1f, 1f }) Bloque(mh, C + q * new Vector3(sx * 0.41f, 0.7f, 0f), new Vector3(0.02f, 1.2f, 0.66f), q);
            mp.CrearMundo("coop_chimenea_piedra", pad, matTunel, true);
            mh.CrearMundo("coop_chimenea_hollin", pad, matHollin, false);
            mm.CrearMundo("coop_chimenea_repisa", pad, mad, true);
            // sobre la repisa: dos tarros y una vela
            var mb = new Malla(); var mc = new Malla();
            Torno(mb, C + q * new Vector3(-0.6f, 1.5f, 0.1f), q, new[] { 0f, 0.02f, 0.14f, 0.16f }, new[] { 0.05f, 0.065f, 0.06f, 0.045f }, 12, true, true);
            Torno(mb, C + q * new Vector3(0.65f, 1.5f, 0.05f), q, new[] { 0f, 0.02f, 0.1f, 0.12f }, new[] { 0.04f, 0.05f, 0.05f, 0.035f }, 12, true, true);
            Vector3 pv = C + q * new Vector3(0.3f, 1.5f, 0.15f);
            Torno(mc, pv, q, new[] { 0f, 0.1f, 0.11f }, new[] { 0.025f, 0.024f, 0.02f }, 10, true, true);
            mb.CrearMundo("coop_chimenea_tarros", pad, matBarro, false);
            mc.CrearMundo("coop_chimenea_vela", pad, matCera, false);
            Llama.Crear(pv + Vector3.up * 0.13f, pad, 0.08f, false, false);
            Fogata.Crear(C + Vector3.up * 0.12f, pad);
        }


        /// Muro de piedras sueltas (filas de alto variable, piedras de ancho variable, algo giradas y salientes).
        static void MuroPiedras(Malla mp, Vector3 C, Quaternion q, System.Random rng, float x0, float x1, float y0, float y1, float prof)
            {
                float y = y0;
                while (y < y1 - 0.02f)
                {
                    float hf = Mathf.Min(y1 - y, 0.16f + 0.12f * (float)rng.NextDouble());
                    float x = x0 + ((float)rng.NextDouble()) * 0.12f * (y > y0 ? 1f : 0f);
                    if (x > x0) Bloque(mp, C + q * new Vector3((x0 + x) / 2f, y + hf / 2f, 0f), new Vector3(x - x0, hf - 0.015f, prof), q, 0.6f);
                    while (x < x1 - 0.02f)
                    {
                        float w = Mathf.Min(x1 - x, 0.22f + 0.2f * (float)rng.NextDouble());
                        var qi = q * Quaternion.Euler(((float)rng.NextDouble() - 0.5f) * 4f, ((float)rng.NextDouble() - 0.5f) * 4f, ((float)rng.NextDouble() - 0.5f) * 4f);
                        float sale = 0.02f * (float)rng.NextDouble();
                        Bloque(mp, C + q * new Vector3(x + w / 2f, y + hf / 2f, sale), new Vector3(w - 0.018f, hf - 0.018f, prof), qi, 0.6f);
                        x += w;
                    }
                    y += hf;
                }
            }

        /// Telarañas en las esquinas altas, hierbas secas colgando, alfombra y polvo en el aire.
        void DetallesCabana(Transform pad, Vector3 c, Vector3 da, Vector3 db, float la, float lb, float y0, float alturaTecho, System.Random rng, bool alfombra)
        {
            var mt = new Malla();
            float ht = Mathf.Min(alturaTecho - 0.05f, 2.5f);
            foreach (var sa in new[] { -1f, 1f }) foreach (var sb in new[] { -1f, 1f })
            {
                if (rng.NextDouble() < 0.25) continue;
                Vector3 esq = new Vector3(c.x, y0, c.z) + da * sa * (la / 2f - 0.06f) + db * sb * (lb / 2f - 0.06f);
                float tam = 0.5f + 0.4f * (float)rng.NextDouble();
                Vector3 p0 = esq + Vector3.up * (ht - tam * 0.9f), p1 = esq + Vector3.up * ht - da * sa * tam, p2 = esq + Vector3.up * ht - db * sb * tam;
                int a = mt.V(p0, new Vector2(0f, 0f)), b = mt.V(p1, new Vector2(1f, 0f)), d = mt.V(p2, new Vector2(0f, 1f));
                mt.Tri(a, b, d); mt.Tri(a, d, b);   // de los dos lados
            }
            mt.CrearMundo("coop_telaranas", pad, matTelarana, false);
            // hierbas secas colgando de la pared contraria a la chimenea
            var mh = new Malla(); var mcu = new Malla();
            for (int k = 0; k < 4; k++)
            {
                Vector3 p = new Vector3(c.x, y0 + ht - 0.15f, c.z) - da * (la / 2f - 0.25f) + db * (-0.6f + k * 0.4f);
                Palo(mcu, p, p + Vector3.down * 0.25f, 0.006f, 4);
                for (int i = 0; i < 6; i++)
                {
                    var dir = (Vector3.down + new Vector3((float)rng.NextDouble() - 0.5f, 0f, (float)rng.NextDouble() - 0.5f) * 0.5f).normalized;
                    Cono(mh, p + Vector3.down * 0.25f, dir, 0.35f + 0.1f * (float)rng.NextDouble(), 0.035f, 5, rng, 1.1f);
                }
            }
            mh.CrearMundo("coop_hierbas", pad, matHierba, false);
            mcu.CrearMundo("coop_hierbas_cuerda", pad, matCuero, false);
            // alfombra tejida
            if (alfombra)
            {
                var ma = new Malla();
                Bloque(ma, new Vector3(c.x, y0 + 0.006f, c.z) + db * 0.2f, new Vector3(1.8f, 0.012f, 1.1f), Quaternion.LookRotation(db, Vector3.up) * Quaternion.Euler(0f, 4f, 0f), 1f);
                var al = ma.CrearMundo("coop_alfombra", pad, null, false);
                Telar(al, new Color(0.38f, 0.16f, 0.1f), new Color(0.2f, 0.14f, 0.08f), 2, 4, 1f, 1f);
            }
            // polvo flotando
            Polvo(pad, pad.InverseTransformPoint(new Vector3(c.x, y0 + 1.3f, c.z)), new Vector3(la * 0.8f, 2f, lb * 0.8f), 45, 0.12f);   // tenue: de cerca se veían como bolas blancas
        }

        /// Altura libre sobre el piso de la cabaña (rayo hacia arriba; si no toca nada, 2,6 m).
        static float AlturaLibre(Vector3 p)
        {
            RaycastHit h;
            if (Physics.Raycast(p + Vector3.up * 0.3f, Vector3.up, out h, 8f, ~0, QueryTriggerInteraction.Ignore)) return h.point.y - p.y;
            return 2.6f;
        }
    }
}

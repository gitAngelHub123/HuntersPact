// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using UnityEngine;

namespace WitchHuntCoop
{
    /// Losas de piedra pulida para los pisos de las alas del castillo (pedido 2026-11-18: la madera se veía lisa, gris o rosada).
    /// Textura propia de 1024 px: losas grandes en hileras desfasadas, cada una con su tono, vetas de mármol, bordes biselados,
    /// juntas oscuras con polvo, desgaste y pequeñas grietas; mapa de normales del mismo relieve y brillo de piedra encerada.
    public partial class Mundo
    {
        static Material matLosas;
        static Material MatLosas()
        {
            if (matLosas != null) return matLosas;
            const int N = 1024; const int FILAS = 3, COLS = 3;   // losas grandes (antes 5 × 5: se veían como baldosas chicas)
            var col = new Color[N * N]; var alt = new float[N, N]; var rng = new System.Random(4411);
            var tono = new float[FILAS, COLS + 1]; var veta = new float[FILAS, COLS + 1];
            for (int f = 0; f < FILAS; f++) for (int c = 0; c <= COLS; c++) { tono[f, c] = (float)rng.NextDouble(); veta[f, c] = (float)rng.NextDouble() * 6.28f; }
            float alto = N / (float)FILAS, ancho = N / (float)COLS, junta = 2.5f;
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    int fila = (int)(y / alto); float desf = (fila % 2) * ancho * 0.5f;
                    float xs = (x + desf) % N; int colI = (int)(xs / ancho);
                    float lx = xs - colI * ancho, ly = y - fila * alto;
                    float borde = Mathf.Min(Mathf.Min(lx, ancho - lx), Mathf.Min(ly, alto - ly));
                    float t = tono[fila % FILAS, colI % (COLS + 1)], a = veta[fila % FILAS, colI % (COLS + 1)];
                    // piedra: gris cálido con nubes y vetas finas de mármol
                    float nube = Ruido(x, y, N, 0.006f) * 0.55f + Ruido(x + 300, y + 100, N, 0.02f) * 0.3f + Ruido(x + 50, y + 600, N, 0.08f) * 0.15f;
                    float u = (x * Mathf.Cos(a) + y * Mathf.Sin(a)) * 0.012f;
                    float v1 = Mathf.Abs(Mathf.Sin(u + Ruido(x, y + 200, N, 0.01f) * 7f)); v1 = Mathf.Pow(1f - v1, 18f);
                    float v2 = Mathf.Abs(Mathf.Sin(u * 2.7f + Ruido(x + 400, y, N, 0.02f) * 9f)); v2 = Mathf.Pow(1f - v2, 30f) * 0.6f;
                    Color basec = Color.Lerp(new Color(0.5f, 0.47f, 0.43f), new Color(0.62f, 0.58f, 0.52f), t * 0.45f + nube * 0.45f);
                    if (t > 0.85f) basec = Color.Lerp(basec, new Color(0.42f, 0.38f, 0.35f), 0.4f);   // alguna losa algo más oscura
                    Color c = Color.Lerp(basec, new Color(0.84f, 0.82f, 0.78f), v1 * 0.55f + v2 * 0.35f);
                    c = Color.Lerp(c, c * 0.72f, Mathf.Clamp01(Ruido(x + 900, y + 900, N, 0.03f) - 0.45f) * 1.5f);   // desgaste y manchas
                    float h = 0.85f + nube * 0.1f;
                    // bisel y junta
                    if (borde < junta) { float k = borde / junta; c = Color.Lerp(new Color(0.27f, 0.25f, 0.22f), c * 0.85f, k * k); h = 0.55f + 0.3f * k; }   // junta de mortero gris, fina
                    else if (borde < junta + 5f) { float k = (borde - junta) / 5f; h = Mathf.Lerp(0.82f, h, k); c *= 0.95f + 0.05f * k; }
                    // grietas finas
                    float gr = Mathf.Abs(Ruido(x * 1.3f + 77, y * 1.3f + 33, N, 0.015f) - 0.5f);
                    if (gr < 0.006f && Ruido(x + 3, y + 7, N, 0.004f) > 0.6f) { c *= 0.55f; h -= 0.15f; }
                    col[y * N + x] = new Color(c.r, c.g, c.b, 1f); alt[x, y] = h;
                }
            var tex = new Texture2D(N, N, TextureFormat.RGB24, true) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Repeat, anisoLevel = 8 };
            tex.SetPixels(col); tex.Apply();
            matLosas = Estandar("coop_castillo_losas", tex, Normales(alt, N, 1.2f), Color.white, 0.38f, 0.8f);
            return matLosas;
        }
    }
}

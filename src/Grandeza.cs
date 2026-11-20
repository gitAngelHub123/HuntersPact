// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using System.Collections.Generic;
using UnityEngine;

namespace WitchHuntCoop
{
    /// Entradas monumentales (pedido 2026-11-17: "mejora excesivamente el laberinto y el castillo; la entrada del castillo que se vea
    /// increíble"): portal gótico del castillo con arquivoltas, columnillas, tímpano con escudo dorado, puertas claveteadas, rastrillo con
    /// púas, rosetón de vitral, estandartes y braseros; y un portón de piedra para el laberinto con arco ojival de dovelas, rejas de hierro,
    /// braseros, farol colgante y macetones con setos recortados.
    public partial class Mundo
    {
        static Material matOroG, matVitral, matEstandarte, matPiedraOscura;

        void MaterialesGrandeza()
        {
            if (matOroG == null) matOroG = MaterialOro();   // el oro con textura del tesoro del castillo
            if (matPiedraOscura == null) { matPiedraOscura = new Material(matPiedra) { name = "coop_piedra_oscura", color = new Color(0.55f, 0.55f, 0.6f) }; }
            if (matVitral == null) { matVitral = new Material(Shader.Find("Unlit/Texture")) { name = "coop_vitral", mainTexture = TexVitral(256) }; }
            if (matEstandarte == null)
            {
                matEstandarte = new Material(Shader.Find("Standard")) { name = "coop_estandarte", mainTexture = TexEstandarte(256, 512) };
                matEstandarte.SetFloat("_Glossiness", 0.08f);
            }
        }

        /// Vitral de rosetón: pétalos de colores en anillos, plomo oscuro entre vidrios y un medallón dorado en el centro.
        static Texture2D TexVitral(int N)
        {
            var t = new Texture2D(N, N, TextureFormat.RGB24, true) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp };
            Color[] vidrios = { new Color(0.75f, 0.08f, 0.12f), new Color(0.1f, 0.22f, 0.8f), new Color(0.95f, 0.7f, 0.15f), new Color(0.1f, 0.6f, 0.3f), new Color(0.55f, 0.15f, 0.75f), new Color(0.12f, 0.45f, 0.85f) };
            Color plomo = new Color(0.05f, 0.04f, 0.05f);
            for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
            {
                float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy), a = Mathf.Atan2(dy, dx) / (Mathf.PI * 2f) + 0.5f;
                Color c;
                if (r > 0.98f) c = plomo;
                else if (r < 0.16f) c = r > 0.14f ? plomo : new Color(1f, 0.82f, 0.3f);               // medallón
                else
                {
                    int anillo = r < 0.42f ? 0 : r < 0.72f ? 1 : 2;
                    int petalos = anillo == 0 ? 8 : anillo == 1 ? 12 : 24;
                    float pa = a * petalos, fr = pa - Mathf.Floor(pa);
                    int idx = ((int)Mathf.Floor(pa) + anillo * 2) % vidrios.Length;
                    c = vidrios[idx];
                    // pétalo redondeado: más claro en el centro del vidrio
                    float rIn = anillo == 0 ? 0.16f : anillo == 1 ? 0.42f : 0.72f, rOut = anillo == 0 ? 0.42f : anillo == 1 ? 0.72f : 0.98f;
                    float fr2 = (r - rIn) / (rOut - rIn);
                    float brillo = 0.75f + 0.35f * Mathf.Sin(fr * Mathf.PI) * Mathf.Sin(fr2 * Mathf.PI);
                    c *= brillo;
                    // plomo: radios entre pétalos y círculos entre anillos
                    if (fr < 0.04f || fr > 0.96f || Mathf.Abs(r - rIn) < 0.012f) c = plomo;
                    // ruido del vidrio soplado
                    c *= 0.9f + 0.2f * Mathf.PerlinNoise(x * 0.15f, y * 0.15f);
                }
                c.a = 1f; t.SetPixel(x, y, c);
            }
            t.Apply(); return t;
        }

        /// Silueta de una mano de bruja que sube desde abajo, delante de la luna: muñeca, palma, cuatro dedos largos y huesudos
        /// (algo abiertos, con nudillos), pulgar y uñas en punta. Coordenadas del emblema (centro de la luna = 0,0; radio 0,3).
        static float seg(float px, float py, float ax, float ay, float bx, float by)
        {
            float vx = bx - ax, vy = by - ay, l2 = vx * vx + vy * vy;
            float t = l2 < 1e-9f ? 0f : Mathf.Clamp01(((px - ax) * vx + (py - ay) * vy) / l2);
            float dx = px - (ax + vx * t), dy = py - (ay + vy * t); return Mathf.Sqrt(dx * dx + dy * dy);
        }

        static bool ManoBruja(float x, float y)
        {
            if (Mathf.Abs(x) < 0.045f + (y + 0.16f) * -0.08f && y < -0.13f && y > -0.42f) return true;                     // muñeca y antebrazo
            float ex = x / 0.085f, ey = (y + 0.11f) / 0.07f; if (ex * ex + ey * ey < 1f) return true;                          // palma
            float[] bx = { -0.058f, -0.02f, 0.02f, 0.058f }, ang = { -16f, -5f, 5f, 15f }, lar = { 0.19f, 0.24f, 0.25f, 0.2f };
            for (int i = 0; i < 4; i++)
            {
                float a = ang[i] * Mathf.Deg2Rad, ax = bx[i], ay = -0.07f;
                float mx = ax + Mathf.Sin(a) * lar[i] * 0.55f, my = ay + Mathf.Cos(a) * lar[i] * 0.55f;                         // nudillo
                float a2 = a + (i < 2 ? -0.12f : 0.12f) + 0.1f;                                                                  // la punta se curva un poco
                float tx = mx + Mathf.Sin(a2) * lar[i] * 0.45f, ty = my + Mathf.Cos(a2) * lar[i] * 0.45f;
                if (seg(x, y, ax, ay, mx, my) < 0.0135f || seg(x, y, mx, my, tx, ty) < 0.0105f) return true;
                if (seg(x, y, mx, my, mx, my) < 0.017f) return true;                                                             // nudillo marcado
                float nx = tx + Mathf.Sin(a2 + 0.25f) * 0.04f, ny = ty + Mathf.Cos(a2 + 0.25f) * 0.04f;                         // uña en punta
                float tseg = Mathf.Clamp01(((x - tx) * (nx - tx) + (y - ty) * (ny - ty)) / ((nx - tx) * (nx - tx) + (ny - ty) * (ny - ty)));
                if (seg(x, y, tx, ty, nx, ny) < 0.009f * (1f - tseg)) return true;
            }
            if (seg(x, y, -0.07f, -0.12f, -0.15f, -0.05f) < 0.015f || seg(x, y, -0.15f, -0.05f, -0.19f, 0.01f) < 0.011f) return true;   // pulgar
            return false;
        }

        /// Estandarte rojo oscuro con ribete dorado, cola en punta y el emblema de la luna llena con la mano de bruja.
        static Texture2D TexEstandarte(int W, int H)
        {
            var t = new Texture2D(W, H, TextureFormat.RGB24, true) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp };
            Color rojo = new Color(0.42f, 0.03f, 0.05f), oro = new Color(0.85f, 0.62f, 0.2f);
            for (int y = 0; y < H; y++) for (int x = 0; x < W; x++)
            {
                float u = (x + 0.5f) / W, v = (y + 0.5f) / H;
                Color c = rojo * (0.85f + 0.25f * Mathf.PerlinNoise(x * 0.2f, y * 0.05f));   // tela con pliegues
                if (u < 0.07f || u > 0.93f || v > 0.95f || (v < 0.2f && Mathf.Abs(u - 0.5f) > v * 2.5f - 0.04f)) c = oro;   // ribete
                if (v < 0.2f && Mathf.Abs(u - 0.5f) > v * 2.5f) c = new Color(0f, 0f, 0f);                                 // cola en V (se recorta con color negro: queda como sombra)
                // LUNA LLENA CON MANO DE BRUJA (pedido 2026-11-18; antes media luna con estrella, que recordaba a una bandera)
                float cx = u - 0.5f, cy = (v - 0.62f) * (H / (float)W) * 0.5f;
                float dl = Mathf.Sqrt(cx * cx + cy * cy);
                if (dl < 0.3f)
                {
                    float cr = Mathf.PerlinNoise(cx * 22f + 5f, cy * 22f + 9f), cr2 = Mathf.PerlinNoise(cx * 55f, cy * 55f);
                    c = Color.Lerp(new Color(0.93f, 0.87f, 0.68f), new Color(0.7f, 0.63f, 0.48f), Mathf.Clamp01((cr - 0.45f) * 2.2f) * 0.8f + cr2 * 0.15f);   // cráteres
                    c *= 1f - 0.25f * Mathf.Pow(dl / 0.3f, 4f);   // borde algo más oscuro
                }
                if (dl > 0.3f && dl < 0.318f) c = oro;   // aro dorado
                if (ManoBruja(cx, cy)) c = new Color(0.03f, 0.02f, 0.025f);
                c.a = 1f; t.SetPixel(x, y, c);
            }
            t.Apply(); return t;
        }

        /// Arco ojival de dovelas (dos arcos de radio = luz total, centrados en el arranque opuesto) en el plano z, con clave más grande.
        void ArcoOjival(Transform pa, string nombre, float cx, float yArranque, float z, float semiLuz, float grosor, float prof, int dovelas, Material m)
        {
            float R = semiLuz * 2f, Rm = R + grosor * 0.5f;
            for (int lado = -1; lado <= 1; lado += 2)
            {
                // centro del arco de este lado: en el arranque contrario
                Vector2 centro = new Vector2(cx - lado * semiLuz, yArranque);
                float a0 = lado < 0 ? 180f : 0f, a1 = lado < 0 ? 120f : 60f;
                for (int i = 0; i < dovelas; i++)
                {
                    float am = Mathf.Lerp(a0, a1, (i + 0.5f) / dovelas) * Mathf.Deg2Rad;
                    float largo = R * Mathf.Abs(a1 - a0) * Mathf.Deg2Rad / dovelas * 1.04f;
                    Vector3 p = new Vector3(centro.x + Mathf.Cos(am) * Rm, centro.y + Mathf.Sin(am) * Rm, z);
                    PzR(pa, nombre + "_dovela", PrimitiveType.Cube, p, new Vector3(largo, grosor, prof), Quaternion.Euler(0f, 0f, am * Mathf.Rad2Deg + 90f), m, false);
                }
            }
            float yClave = yArranque + Mathf.Sqrt(3f) * semiLuz;
            PzR(pa, nombre + "_clave", PrimitiveType.Cube, new Vector3(cx, yClave + grosor * 0.55f, z - 0.04f), new Vector3(grosor * 0.95f, grosor * 1.5f, prof + 0.12f), Quaternion.identity, m, false);
        }

        /// Tímpano: el hueco bajo un arco ojival relleno con una lámina de piedra (malla con la forma del arco).
        void Timpano(Transform pa, string nombre, float cx, float yArranque, float z, float semiLuz, Material m)
        {
            float R = semiLuz * 2f;
            var v = new List<Vector3>(); var tr = new List<int>(); var uv = new List<Vector2>();
            v.Add(new Vector3(cx, yArranque, z)); uv.Add(new Vector2(0.5f, 0f));
            const int N = 12;
            for (int i = 0; i <= N; i++)   // lado izquierdo: del arranque izquierdo a la clave
            {
                float a = Mathf.Lerp(180f, 120f, i / (float)N) * Mathf.Deg2Rad;
                v.Add(new Vector3(cx + semiLuz + Mathf.Cos(a) * R, yArranque + Mathf.Sin(a) * R, z));
            }
            for (int i = 1; i <= N; i++)   // lado derecho: de la clave al arranque derecho
            {
                float a = Mathf.Lerp(60f, 0f, i / (float)N) * Mathf.Deg2Rad;
                v.Add(new Vector3(cx - semiLuz + Mathf.Cos(a) * R, yArranque + Mathf.Sin(a) * R, z));
            }
            for (int i = 1; i < v.Count; i++) uv.Add(new Vector2((v[i].x - cx) / 2f + 0.5f, (v[i].y - yArranque) / 2f));
            for (int i = 1; i < v.Count - 1; i++) { tr.Add(0); tr.Add(i); tr.Add(i + 1); tr.Add(0); tr.Add(i + 1); tr.Add(i); }   // dos caras
            var me = new Mesh { name = nombre }; me.SetVertices(v); me.SetUVs(0, uv); me.SetTriangles(tr, 0); me.RecalculateNormals(); me.RecalculateBounds();
            var g = new GameObject(nombre); g.transform.SetParent(pa, false);
            g.AddComponent<MeshFilter>().sharedMesh = me; g.AddComponent<MeshRenderer>().sharedMaterial = m;
        }

        /// Brasero: pedestal de piedra, cuenco de hierro con aro dorado y fuego grande con su luz.
        static Material matBrasas;
        void Brasero(Transform pa, Vector3 b, float escala)
        {
            if (matBrasas == null)
            {
                matBrasas = new Material(Shader.Find("Standard")) { name = "coop_brasas", color = new Color(0.25f, 0.05f, 0.02f) };
                matBrasas.EnableKeyword("_EMISSION"); matBrasas.SetColor("_EmissionColor", new Color(1.4f, 0.35f, 0.05f));
            }
            Pz(pa, "coop_brasero_base", PrimitiveType.Cube, b + Vector3.up * 0.25f * escala, new Vector3(1.3f, 0.5f, 1.3f) * escala, 0f, matPiedra);
            Pz(pa, "coop_brasero_pie", PrimitiveType.Cylinder, b + Vector3.up * 0.95f * escala, new Vector3(0.55f, 0.45f, 0.55f) * escala, 0f, matPiedra);
            Pz(pa, "coop_brasero_cuenco", PrimitiveType.Cylinder, b + Vector3.up * 1.52f * escala, new Vector3(1.35f, 0.15f, 1.35f) * escala, 0f, matHierroC, false);
            // aro de hierro con patas (antes un disco dorado tapaba el fuego)
            for (int i = 0; i < 10; i++)
            {
                float a = i * Mathf.PI * 2f / 10f;
                PzR(pa, "coop_brasero_aro", PrimitiveType.Cube, b + new Vector3(Mathf.Cos(a) * 0.7f, 1.7f, Mathf.Sin(a) * 0.7f) * escala, new Vector3(0.46f, 0.09f, 0.08f) * escala, Quaternion.Euler(0f, -a * Mathf.Rad2Deg + 90f, 0f), matHierroC, false);
                if (i % 3 == 0) PzR(pa, "coop_brasero_garra", PrimitiveType.Cube, b + new Vector3(Mathf.Cos(a) * 0.72f, 1.83f, Mathf.Sin(a) * 0.72f) * escala, new Vector3(0.08f, 0.26f, 0.08f) * escala, Quaternion.Euler(0f, 0f, 0f), matHierroC, false);
            }
            // brasas encendidas sobre el cuenco
            var rr = new System.Random(Mathf.RoundToInt(b.x * 13f + b.z * 7f));
            for (int i = 0; i < 6; i++)
            {
                float a = (float)rr.NextDouble() * Mathf.PI * 2f, d = (float)rr.NextDouble() * 0.5f;
                Pz(pa, "coop_brasa", PrimitiveType.Sphere, b + new Vector3(Mathf.Cos(a) * d, 1.7f, Mathf.Sin(a) * d) * escala, Vector3.one * (0.16f + 0.12f * (float)rr.NextDouble()) * escala, 0f, matBrasas, false);
            }
            var f = Fogata.Crear(pa.TransformPoint(b + Vector3.up * 1.74f * escala), pa, 1.7f * escala * pa.lossyScale.x);
            foreach (var l in f.GetComponentsInChildren<Light>(true)) { l.range = 13f * escala; l.intensity = 2.6f; if (escala < 1f) l.renderMode = LightRenderMode.ForceVertex; }   // los pequeños (escalinata): luz por vértice, mucho más barata
            foreach (var r in f.GetComponentsInChildren<MeshRenderer>(true)) r.enabled = false;   // los troncos quedan entre las brasas
        }

        /// Farol de hierro colgado de una cadena (bajo la clave de un arco).
        void FarolColgante(Transform pa, Vector3 cuelgue, float largoCadena)
        {
            Pz(pa, "coop_cadena", PrimitiveType.Cylinder, cuelgue + Vector3.down * (largoCadena / 2f), new Vector3(0.06f, largoCadena / 2f, 0.06f), 0f, matHierroC, false);
            Vector3 c = cuelgue + Vector3.down * (largoCadena + 0.45f);
            Llama.Crear(pa.TransformPoint(c + Vector3.down * 0.32f), pa, 0.5f * pa.lossyScale.x, false, false);
            foreach (var px in new[] { -0.26f, 0.26f }) foreach (var pz in new[] { -0.26f, 0.26f })
                Pz(pa, "coop_farol_c_barra", PrimitiveType.Cylinder, c + new Vector3(px, 0f, pz), new Vector3(0.05f, 0.42f, 0.05f), 0f, matHierroC, false);
            Pz(pa, "coop_farol_c_tapa", PrimitiveType.Cube, c + Vector3.up * 0.45f, new Vector3(0.66f, 0.08f, 0.66f), 0f, matHierroC, false);
            Pz(pa, "coop_farol_c_fondo", PrimitiveType.Cube, c + Vector3.down * 0.45f, new Vector3(0.66f, 0.08f, 0.66f), 0f, matHierroC, false);
            var l = LuzCastillo(pa, c, new Color(1f, 0.62f, 0.28f), 14f, 2.2f); l.gameObject.AddComponent<Titilar>();
        }

        /// Hoja de puerta de madera con flejes de hierro, clavos y aldaba (gira alrededor de su centro).
        void HojaClaveteada(Transform pa, string nombre, Vector3 centro, float yaw, float ancho, float alto, float caraFuera)
        {
            var piv = new GameObject(nombre); piv.transform.SetParent(pa, false); piv.transform.localPosition = centro; piv.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var t = piv.transform;
            Pz(t, "coop_hoja_tabla", PrimitiveType.Cube, Vector3.zero, new Vector3(0.35f, alto, ancho), 0f, matMadera2);
            float xf = caraFuera * 0.2f;
            for (int k = 0; k < 4; k++)
            {
                float y = -alto / 2f + alto * (k + 0.5f) / 4f;
                Pz(t, "coop_fleje", PrimitiveType.Cube, new Vector3(xf, y, 0f), new Vector3(0.05f, 0.2f, ancho * 0.98f), 0f, matHierroC, false);
                for (float z = -ancho / 2f + 0.3f; z < ancho / 2f - 0.2f; z += 0.42f)
                    Pz(t, "coop_clavo", PrimitiveType.Sphere, new Vector3(xf + caraFuera * 0.04f, y, z), new Vector3(0.11f, 0.11f, 0.11f), 0f, matHierroC, false);
            }
            var aldaba = Pz(t, "coop_aldaba", PrimitiveType.Cylinder, new Vector3(xf + caraFuera * 0.08f, 0f, -ancho * 0.3f), new Vector3(0.42f, 0.03f, 0.42f), 0f, matOroG, false);
            aldaba.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        }

        /// Portal gótico del castillo (en los ejes del castillo: fachada en z = tz, puerta de 6,2 × 8 m sobre el podio a 3 m).
        void PortalCastillo(Transform pa, float tz)
        {
            MaterialesGrandeza();
            // jambas
            foreach (var sx in new[] { -3.8f, 3.8f }) Pz(pa, "coop_jamba", PrimitiveType.Cube, new Vector3(sx, 7f, tz - 0.35f), new Vector3(1.4f, 8f, 0.9f), 0f, matPiedra, false);
            // tímpano con escudo dorado y cruz
            Timpano(pa, "coop_timpano", 0f, 11f, tz - 0.32f, 3.1f, matPiedraOscura);
            PzR(pa, "coop_escudo", PrimitiveType.Cube, new Vector3(0f, 13.3f, tz - 0.42f), new Vector3(1.45f, 1.45f, 0.16f), Quaternion.Euler(0f, 0f, 45f), matOroG, false);
            Pz(pa, "coop_escudo_cruz", PrimitiveType.Cube, new Vector3(0f, 13.3f, tz - 0.52f), new Vector3(0.2f, 1.5f, 0.08f), 0f, matPiedraOscura, false);
            Pz(pa, "coop_escudo_cruz", PrimitiveType.Cube, new Vector3(0f, 13.55f, tz - 0.52f), new Vector3(1.0f, 0.2f, 0.08f), 0f, matPiedraOscura, false);
            // dintel bajo el tímpano
            Pz(pa, "coop_dintel_portal", PrimitiveType.Cube, new Vector3(0f, 11.1f, tz - 0.4f), new Vector3(7.4f, 0.55f, 1.0f), 0f, matPiedra, false);
            // tres arquivoltas escalonadas hacia afuera, cada una con su par de columnillas
            for (int k = 0; k < 3; k++)
            {
                float semi = 3.1f + 0.62f * k + 0.3f, z = tz - 0.45f - 0.5f * k;
                ArcoOjival(pa, "coop_arquivolta" + k, 0f, 11f, z, semi, 0.6f, 0.55f, 7 + k, k == 1 ? matPiedraOscura : matPiedra);
                foreach (var sx in new[] { -1f, 1f })
                {
                    float x = sx * (semi + 0.3f);
                    Pz(pa, "coop_columnilla", PrimitiveType.Cylinder, new Vector3(x, 7f, z), new Vector3(0.44f, 3.6f, 0.44f), 0f, k == 1 ? matPiedraOscura : matPiedra, false);
                    Pz(pa, "coop_columnilla_basa", PrimitiveType.Cube, new Vector3(x, 3.3f, z), new Vector3(0.78f, 0.5f, 0.78f), 0f, matPiedra, false);
                    Pz(pa, "coop_columnilla_capitel", PrimitiveType.Cube, new Vector3(x, 10.75f, z), new Vector3(0.82f, 0.5f, 0.82f), 0f, matPiedra, false);
                    Pz(pa, "coop_columnilla_anillo", PrimitiveType.Cylinder, new Vector3(x, 10.4f, z), new Vector3(0.6f, 0.06f, 0.6f), 0f, matOroG, false);
                }
            }
            // pináculos a los lados de las arquivoltas (sin gablete: tapaba el rosetón)
            float semiE = 3.1f + 0.62f * 2f + 0.3f + 0.6f;
            foreach (var sx in new[] { -1f, 1f })
            {
                float x = sx * (semiE + 0.9f);
                Pz(pa, "coop_pinaculo", PrimitiveType.Cube, new Vector3(x, 8f, tz - 1.4f), new Vector3(0.9f, 10f, 0.9f), 0f, matPiedra);
                ConoMat(pa, "coop_pinaculo_punta", new Vector3(x, 13f, tz - 1.4f), 0.62f, 2.6f, matPiedra);
                Pz(pa, "coop_pinaculo_flor", PrimitiveType.Sphere, new Vector3(x, 15.7f, tz - 1.4f), new Vector3(0.36f, 0.36f, 0.36f), 0f, matOroG, false);
            }
            // rastrillo de hierro levantado: barrotes con púas asomando bajo el dintel
            for (int i = 0; i < 9; i++)
            {
                float x = -3.0f + i * 0.75f;
                Pz(pa, "coop_rastrillo", PrimitiveType.Cylinder, new Vector3(x, 10.2f, tz + 0.15f), new Vector3(0.13f, 0.75f, 0.13f), 0f, matHierroC, false);
                ConoMat(pa, "coop_rastrillo_pua", new Vector3(x, 9.45f, tz + 0.15f), 0.11f, 0.38f, matHierroC, true);
            }
            foreach (var y in new[] { 9.8f, 10.6f }) Pz(pa, "coop_rastrillo_h", PrimitiveType.Cube, new Vector3(0f, y, tz + 0.15f), new Vector3(6.4f, 0.14f, 0.16f), 0f, matHierroC, false);
            // puertas claveteadas entreabiertas
            HojaClaveteada(pa, "coop_hoja_puerta_i", new Vector3(-3.4f, 7f, tz + 2.2f), 35f, 3f, 8f, -1f);
            HojaClaveteada(pa, "coop_hoja_puerta_d", new Vector3(3.4f, 7f, tz + 2.2f), -35f, 3f, 8f, 1f);
            // rosetón de vitral con tracería de piedra
            var ros = Pz(pa, "coop_roseton", PrimitiveType.Cylinder, new Vector3(0f, 21.0f, tz - 0.1f), new Vector3(3.95f, 0.04f, 3.95f), 0f, matVitral, false);
            ros.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            ros.GetComponent<Renderer>().sharedMaterial = matVitral;   // sin la repetición ×2 de Pz: con el borde fijo solo se veía media rueda y el resto oscuro
            for (int i = 0; i < 28; i++)
            {
                float a = i * Mathf.PI * 2f / 28f;
                PzR(pa, "coop_roseton_marco", PrimitiveType.Cube, new Vector3(Mathf.Cos(a) * 2.12f, 21.0f + Mathf.Sin(a) * 2.12f, tz - 0.32f), new Vector3(0.52f, 0.36f, 0.5f), Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg + 90f), matPiedra, false);
            }
            for (int i = 0; i < 6; i++) PzR(pa, "coop_roseton_radio", PrimitiveType.Cube, new Vector3(0f, 21.0f, tz - 0.22f), new Vector3(3.9f, 0.09f, 0.1f), Quaternion.Euler(0f, 0f, i * 30f + 15f), matPiedra, false);
            var boss = Pz(pa, "coop_roseton_centro", PrimitiveType.Cylinder, new Vector3(0f, 21.0f, tz - 0.3f), new Vector3(0.7f, 0.1f, 0.7f), 0f, matPiedra, false);
            boss.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var luzRos = LuzCastillo(pa, new Vector3(0f, 21.0f, tz - 2.5f), new Color(0.7f, 0.45f, 1f), 9f, 1.1f);   // el vitral tiñe un poco la fachada
            // estandartes colgados a los lados del portal
            foreach (var sx in new[] { -1f, 1f })
            {
                float x = sx * 8.6f;
                Pz(pa, "coop_estandarte", PrimitiveType.Cube, new Vector3(x, 16.2f, tz - 0.22f), new Vector3(2.3f, 8.6f, 0.05f), 0f, matEstandarte, false);
                var vara = Pz(pa, "coop_estandarte_vara", PrimitiveType.Cylinder, new Vector3(x, 20.6f, tz - 0.3f), new Vector3(0.12f, 1.5f, 0.12f), 0f, matHierroC, false);
                vara.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                foreach (var px in new[] { -1.55f, 1.55f }) Pz(pa, "coop_estandarte_pomo", PrimitiveType.Sphere, new Vector3(x + px, 20.6f, tz - 0.3f), new Vector3(0.24f, 0.24f, 0.24f), 0f, matOroG, false);
            }
            // braseros: arriba de la escalinata y al pie
            foreach (var sx in new[] { -1f, 1f })
            {
                Brasero(pa, new Vector3(sx * 6.6f, 3.0f, -4.6f), 1.1f);
                Brasero(pa, new Vector3(sx * 9.2f, 0f, -15.2f), 1.25f);
            }
            plugin.Log("Castillo: portal gótico (3 arquivoltas, tímpano con escudo, rastrillo con púas, pináculos, puertas claveteadas, rosetón de vitral, estandartes y 4 braseros)");
        }

        /// Caja para mallas combinadas (normales hacia afuera; textura por metros con "tile").
        static void Caja6(List<Vector3> v, List<Vector2> uv, List<int> tr, Vector3 c, Vector3 s, float tile)
        {
            Vector3 h = s / 2f;
            Vector3[] ns = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            foreach (var n in ns)
            {
                Vector3 a = n.x != 0f ? Vector3.forward : Vector3.right, b = n.y != 0f ? Vector3.forward : Vector3.up;
                if (n.y != 0f) a = Vector3.right;
                float la = Vector3.Scale(a, s).magnitude, lb = Vector3.Scale(b, s).magnitude;
                Vector3 o = c + Vector3.Scale(n, h), ha = a * (la / 2f), hb = b * (lb / 2f);
                int k = v.Count;
                v.Add(o - ha - hb); v.Add(o - ha + hb); v.Add(o + ha + hb); v.Add(o + ha - hb);
                uv.Add(new Vector2(0f, 0f)); uv.Add(new Vector2(0f, lb / tile)); uv.Add(new Vector2(la / tile, lb / tile)); uv.Add(new Vector2(la / tile, 0f));
                // cara frontal en Unity = cross(v1-v0, v2-v0) = cross(b, a): si no apunta a n, se invierte el orden
                if (Vector3.Dot(Vector3.Cross(b, a), n) > 0f) { tr.Add(k); tr.Add(k + 1); tr.Add(k + 2); tr.Add(k); tr.Add(k + 2); tr.Add(k + 3); }
                else { tr.Add(k); tr.Add(k + 2); tr.Add(k + 1); tr.Add(k); tr.Add(k + 3); tr.Add(k + 2); }
            }
        }

        static GameObject MallaDe(Transform pa, string nombre, List<Vector3> v, List<Vector2> uv, List<int> tr, Material m)
        {
            var me = new Mesh { name = nombre }; me.SetVertices(v); me.SetUVs(0, uv); me.SetTriangles(tr, 0); me.RecalculateNormals(); me.RecalculateBounds();
            var g = new GameObject(nombre); g.transform.SetParent(pa, false);
            g.AddComponent<MeshFilter>().sharedMesh = me; g.AddComponent<MeshRenderer>().sharedMaterial = m;
            return g;
        }

        /// Salida del laberinto: el mismo portón monumental de la entrada, girado hacia la montaña y más ancho (la salida son dos celdas).
        void SalidaLaberinto(Transform pa, TerrainData dn)
        {
            float z = LabZ0 + LabNz * LabPaso, y = SueloT(dn, EscX, z);
            var g = new GameObject("coop_lab_salida"); g.transform.SetParent(pa, false);
            g.transform.localPosition = new Vector3(EscX, 0f, z); g.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            PortonLaberinto(g.transform, 0f, y, 0f, dn, 5.9f, true);
        }

        /// Escalinata recta de la salida del laberinto a la plaza del castillo: peldaños de piedra oscura, barandas con postes, pilares con
        /// braseros, tres arcos góticos con faroles colgantes sobre la subida, una puerta monumental al llegar a la cima y una avenida con
        /// faroles hasta la plaza. El terreno ya trae el pasillo recortado (TerrenoEscalinata).
        void EscalinataDracula(Transform pa, TerrainData dn)
        {
            MaterialesGrandeza();
            float z0 = EscZ0, z1 = EscZ1, z2 = EscZ2, x0 = EscX, w = EscAncho;
            float y0 = SueloT(dn, x0, z0), y1 = SueloT(dn, x0, z1), subida = y1 - y0, run = z1 - z0;
            if (subida < 2f || run < 10f) { plugin.Log("Laberinto: la escalinata no tiene subida (" + subida.ToString("0.0") + " m)"); return; }
            int N = Mathf.CeilToInt(subida / 0.19f); float alza = subida / N, huella = run / N;
            // peldaños (una sola malla)
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var tr = new List<int>();
            for (int i = 0; i < N; i++)
            {
                float top = y0 + alza * (i + 1), bot = y0 + alza * i - 0.8f;
                Caja6(v, uv, tr, new Vector3(x0, (top + bot) / 2f, z0 + huella * (i + 0.5f)), new Vector3(w, top - bot, huella + 0.005f), 1.6f);
            }
            var pel = MallaDe(pa, "coop_esc_peldanos", v, uv, tr, matPiedraOscura);
            // rampa invisible por encima de las narices de los peldaños: se sube caminando sin tropezar
            float ang = Mathf.Atan2(subida, run) * Mathf.Rad2Deg, largo = Mathf.Sqrt(subida * subida + run * run);
            var rampa = new GameObject("coop_esc_rampa"); rampa.transform.SetParent(pa, false);
            rampa.transform.localPosition = new Vector3(x0, (y0 + y1) / 2f + alza * 0.5f, (z0 + z1) / 2f) - Quaternion.Euler(-ang, 0f, 0f) * Vector3.up * 0.25f;
            rampa.transform.localRotation = Quaternion.Euler(-ang, 0f, 0f);
            rampa.AddComponent<BoxCollider>().size = new Vector3(w, 0.5f, largo);
            // barandas inclinadas, postes cada ~12 m y pilares con brasero cada ~36 m
            foreach (var sx in new[] { -1f, 1f })
            {
                float bx = x0 + sx * (w / 2f + 0.35f);
                PzR(pa, "coop_esc_baranda", PrimitiveType.Cube, new Vector3(bx, (y0 + y1) / 2f + 0.55f, (z0 + z1) / 2f), new Vector3(0.7f, 1.6f, largo), Quaternion.Euler(-ang, 0f, 0f), matPiedra, true);
                PzR(pa, "coop_esc_baranda_tapa", PrimitiveType.Cube, new Vector3(bx, (y0 + y1) / 2f + 1.38f, (z0 + z1) / 2f), new Vector3(0.9f, 0.14f, largo), Quaternion.Euler(-ang, 0f, 0f), matPiedraOscura, false);
                int k = 0;
                for (float d = 0f; d <= run + 0.1f; d += run / Mathf.Max(1f, Mathf.Round(run / 12f)), k++)
                {
                    float z = z0 + d, y = y0 + subida * (d / run);
                    bool grande = k % 3 == 0;
                    if (grande)
                    {
                        Pz(pa, "coop_esc_pilar", PrimitiveType.Cube, new Vector3(bx, y + 1.6f, z), new Vector3(1.5f, 3.6f, 1.5f), 0f, matPiedra);
                        Pz(pa, "coop_esc_pilar_tapa", PrimitiveType.Cube, new Vector3(bx, y + 3.5f, z), new Vector3(1.8f, 0.25f, 1.8f), 0f, matPiedraOscura, false);
                        Brasero(pa, new Vector3(bx, y + 3.62f, z), 0.75f);
                    }
                    else
                    {
                        Pz(pa, "coop_esc_poste", PrimitiveType.Cube, new Vector3(bx, y + 1.0f, z), new Vector3(0.95f, 2.2f, 0.95f), 0f, matPiedra);
                        ConoMat(pa, "coop_esc_poste_punta", new Vector3(bx, y + 2.1f, z), 0.55f, 0.9f, matPiedraOscura);
                    }
                }
            }
            // tres arcos góticos sobre la subida, con farol colgante
            foreach (var f in new[] { 0.22f, 0.5f, 0.78f })
            {
                float z = z0 + run * f, y = y0 + subida * f, semi = w / 2f + 1.9f;
                foreach (var sx in new[] { -1f, 1f })
                {
                    Pz(pa, "coop_esc_arco_pilar", PrimitiveType.Cube, new Vector3(x0 + sx * semi, y + 4.2f, z), new Vector3(1.6f, 9.6f, 1.8f), 0f, matPiedra);
                    Pz(pa, "coop_esc_arco_capitel", PrimitiveType.Cube, new Vector3(x0 + sx * semi, y + 8.9f, z), new Vector3(2.0f, 0.4f, 2.2f), 0f, matPiedraOscura, false);
                }
                ArcoOjival(pa, "coop_esc_arco", x0, y + 9.1f, z, semi, 1.0f, 1.8f, 10, matPiedra);
                FarolColgante(pa, new Vector3(x0, y + 9.1f + Mathf.Sqrt(3f) * semi - 0.3f, z), 4.2f);
            }
            // puerta monumental en la cima: dos torreones con aguja y un gran arco
            {
                float semi = w / 2f + 2.6f;
                foreach (var sx in new[] { -1f, 1f })
                {
                    float x = x0 + sx * semi;
                    Pz(pa, "coop_esc_torreon", PrimitiveType.Cube, new Vector3(x, y1 + 7f, z1), new Vector3(2.8f, 14f, 2.8f), 0f, matPiedra);
                    Pz(pa, "coop_esc_torreon_cornisa", PrimitiveType.Cube, new Vector3(x, y1 + 14.2f, z1), new Vector3(3.4f, 0.5f, 3.4f), 0f, matPiedraOscura, false);
                    ConoMat(pa, "coop_esc_torreon_aguja", new Vector3(x, y1 + 14.4f, z1), 1.9f, 5.5f, matPiedra);
                    Pz(pa, "coop_esc_torreon_bola", PrimitiveType.Sphere, new Vector3(x, y1 + 20.1f, z1), new Vector3(0.6f, 0.6f, 0.6f), 0f, matOroG, false);
                    for (float yy = 4f; yy < 13f; yy += 4.2f) VentanaOjival(pa, new Vector3(x, y1 + yy, z1 - 1.41f), Vector3.back, 0.55f, 1.5f, 0.35f, false);
                    Brasero(pa, new Vector3(x0 + sx * (semi + 3.4f), y1, z1 - 3f), 1.2f);
                }
                ArcoOjival(pa, "coop_esc_puerta_arco", x0, y1 + 11f, z1, semi - 1.4f, 1.2f, 2.6f, 11, matPiedra);
                Timpano(pa, "coop_esc_puerta_timpano", x0, y1 + 11f, z1 + 1.1f, semi - 1.4f, matPiedraOscura);
                Pz(pa, "coop_esc_puerta_dintel", PrimitiveType.Cube, new Vector3(x0, y1 + 10.8f, z1), new Vector3(semi * 2f, 0.7f, 2.6f), 0f, matPiedra, false);
                FarolColgante(pa, new Vector3(x0, y1 + 10.4f, z1 - 0.4f), 2f);
            }
            // avenida de adoquines con faroles hasta la plaza del castillo
            if (z2 > z1 + 4f)
            {
                Pz(pa, "coop_esc_avenida", PrimitiveType.Cube, new Vector3(x0, y1 + 0.05f, (z1 + z2) / 2f), new Vector3(w + 1f, 0.1f, z2 - z1), 0f, matAdoquin, true, 3.5f);
                int k = 0;
                for (float z = z1 + 8f; z < z2 - 3f; z += 16f, k++)
                    foreach (var sx in new[] { -1f, 1f }) Farol2(pa, new Vector3(x0 + sx * (w / 2f + 1.6f), SueloT(dn, x0 + sx * (w / 2f + 1.6f), z), z));
            }
            plugin.Log("Laberinto: escalinata de " + N + " peldaños (sube " + subida.ToString("0") + " m en " + run.ToString("0") + " m), 3 arcos, puerta en la cima y avenida hasta el castillo");
        }

        /// Antorcha de pared: soporte de hierro, mango de madera, llama y su luz (en ejes del padre; n = hacia afuera de la pared).
        void Antorcha(Transform pa, Vector3 p, Vector3 n)
        {
            Quaternion q = Quaternion.LookRotation(n, Vector3.up);
            PzR(pa, "coop_antorcha_soporte", PrimitiveType.Cube, p + n * 0.25f, new Vector3(0.12f, 0.12f, 0.5f), q, matHierroC, false);
            PzR(pa, "coop_antorcha_mango", PrimitiveType.Cylinder, p + n * 0.5f + Vector3.up * 0.25f, new Vector3(0.14f, 0.38f, 0.14f), q * Quaternion.Euler(20f, 0f, 0f), matMadera2, false);
            var f = Fogata.Crear(pa.TransformPoint(p + n * 0.6f + Vector3.up * 0.62f), pa, 1.7f);
            foreach (var l in f.GetComponentsInChildren<Light>(true)) { l.range = 11f; l.intensity = 1.9f; }
            foreach (var r in f.GetComponentsInChildren<MeshRenderer>(true)) r.enabled = false;
        }

        /// Rendimiento: las piezas chicas (clavos, cadenas, púas, brasas, aros, barrotes…) no proyectan sombra: cada una costaba otra pasada
        /// de dibujo en el mapa de sombras de la luna y casi no se nota.
        static void SinSombraLoPequeno(GameObject raiz)
        {
            int n = 0;
            foreach (var r in raiz.GetComponentsInChildren<MeshRenderer>(true))
            {
                var b = r.bounds;
                if (Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z)) > 1.6f) continue;
                if (r.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; n++; }
            }
            Debug.Log("[WitchHuntCoop] " + raiz.name + ": " + n + " piezas chicas sin sombra");
        }

        static void QuadOrientado(List<Vector3> v, List<Vector2> uv, List<int> tr, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 n, Vector2 u0, Vector2 u1, Vector2 u2, Vector2 u3)
        {
            int k = v.Count; v.Add(p0); v.Add(p1); v.Add(p2); v.Add(p3); uv.Add(u0); uv.Add(u1); uv.Add(u2); uv.Add(u3);
            if (Vector3.Dot(Vector3.Cross(p1 - p0, p2 - p0), n) >= 0f) { tr.Add(k); tr.Add(k + 1); tr.Add(k + 2); tr.Add(k); tr.Add(k + 2); tr.Add(k + 3); }
            else { tr.Add(k); tr.Add(k + 2); tr.Add(k + 1); tr.Add(k); tr.Add(k + 3); tr.Add(k + 2); }
        }

        /// Losa en forma de anillo (o sector de anillo) en ejes del padre: sin tablones superpuestos (no parpadea) y con colisión exacta.
        void Anillo(Transform pa, string nombre, Vector3 centro, float y0, float y1, float r0, float r1, float angDesde, float angHasta, Material m)
        {
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var tr = new List<int>();
            int n = Mathf.Max(2, Mathf.CeilToInt((angHasta - angDesde) / 4f));
            for (int i = 0; i < n; i++)
            {
                float a0 = Mathf.Lerp(angDesde, angHasta, i / (float)n) * Mathf.Deg2Rad, a1 = Mathf.Lerp(angDesde, angHasta, (i + 1) / (float)n) * Mathf.Deg2Rad;
                Vector3 d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)), d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                Vector3 i0 = centro + d0 * r0, i1 = centro + d1 * r0, o0 = centro + d0 * r1, o1 = centro + d1 * r1;
                Vector3 up0 = Vector3.up * y0, up1 = Vector3.up * y1;
                System.Func<Vector3, Vector2> uvp = q => new Vector2(q.x / 2.5f, q.z / 2.5f);
                QuadOrientado(v, uv, tr, i0 + up1, o0 + up1, o1 + up1, i1 + up1, Vector3.up, uvp(i0), uvp(o0), uvp(o1), uvp(i1));        // arriba
                QuadOrientado(v, uv, tr, i0 + up0, i1 + up0, o1 + up0, o0 + up0, Vector3.down, uvp(i0), uvp(i1), uvp(o1), uvp(o0));      // abajo
                float l0 = r1 * a0 / 2.5f, l1 = r1 * a1 / 2.5f, h0 = y0 / 2.5f, h1 = y1 / 2.5f;
                QuadOrientado(v, uv, tr, o0 + up0, o1 + up0, o1 + up1, o0 + up1, (d0 + d1).normalized, new Vector2(l0, h0), new Vector2(l1, h0), new Vector2(l1, h1), new Vector2(l0, h1));   // borde exterior
                QuadOrientado(v, uv, tr, i0 + up0, i1 + up0, i1 + up1, i0 + up1, -(d0 + d1).normalized, new Vector2(l0, h0), new Vector2(l1, h0), new Vector2(l1, h1), new Vector2(l0, h1));  // borde interior
            }
            // tapas de los extremos del sector
            foreach (var ex in new[] { angDesde, angHasta })
            {
                float a = ex * Mathf.Deg2Rad; Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)), t = new Vector3(-d.z, 0f, d.x) * (ex == angDesde ? -1f : 1f);
                Vector3 i0 = centro + d * r0, o0 = centro + d * r1;
                QuadOrientado(v, uv, tr, i0 + Vector3.up * y0, o0 + Vector3.up * y0, o0 + Vector3.up * y1, i0 + Vector3.up * y1, t, new Vector2(0f, 0f), new Vector2((r1 - r0) / 2.5f, 0f), new Vector2((r1 - r0) / 2.5f, (y1 - y0) / 2.5f), new Vector2(0f, (y1 - y0) / 2.5f));
            }
            var g = MallaDe(pa, nombre, v, uv, tr, m);
            g.AddComponent<MeshCollider>().sharedMesh = g.GetComponent<MeshFilter>().sharedMesh;
        }

        /// Montaña de oro (para los montones de la bóveda): un cerro de monedas con relieve, monedas sueltas encima, lingotes y destellos.
        static void MontanaOro(Transform raiz, Material oro, float R, float H, int semilla)
        {
            var rng = new System.Random(semilla);
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var tr = new List<int>();
            const int anillos = 10, gajos = 28;
            float fase = (float)rng.NextDouble() * 10f;
            v.Add(new Vector3(0f, H, 0f)); uv.Add(new Vector2(0.5f, 0.5f));
            for (int a = 1; a <= anillos; a++)
            {
                float r = R * a / anillos;
                for (int g = 0; g < gajos; g++)
                {
                    float ang = g * Mathf.PI * 2f / gajos;
                    float rr = r * (1f + 0.12f * (Mathf.PerlinNoise(fase + Mathf.Cos(ang) * 1.7f, fase + Mathf.Sin(ang) * 1.7f) - 0.5f));
                    float t = rr / R;
                    float y = H * Mathf.Pow(Mathf.Clamp01(1f - t * t), 1.25f) + 0.06f * H * (Mathf.PerlinNoise(fase + Mathf.Cos(ang) * r * 2f, fase + Mathf.Sin(ang) * r * 2f) - 0.5f) * (1f - t);
                    v.Add(new Vector3(Mathf.Cos(ang) * rr, Mathf.Max(0f, y), Mathf.Sin(ang) * rr)); uv.Add(new Vector2(Mathf.Cos(ang) * rr * 1.6f, Mathf.Sin(ang) * rr * 1.6f));
                }
            }
            for (int g = 0; g < gajos; g++) { int b0 = 1 + g, b1 = 1 + (g + 1) % gajos; tr.Add(0); tr.Add(b1); tr.Add(b0); }
            for (int a = 1; a < anillos; a++)
                for (int g = 0; g < gajos; g++)
                {
                    int i0 = 1 + (a - 1) * gajos + g, i1 = 1 + (a - 1) * gajos + (g + 1) % gajos, o0 = 1 + a * gajos + g, o1 = 1 + a * gajos + (g + 1) % gajos;
                    tr.Add(i0); tr.Add(i1); tr.Add(o0); tr.Add(i1); tr.Add(o1); tr.Add(o0);
                }
            var cerro = MallaDe(raiz, "coop_cerro_oro", v, uv, tr, oro);
            // monedas sueltas sobre la superficie, lingotes y un cáliz
            for (int i = 0; i < 26; i++)
            {
                float ang = (float)rng.NextDouble() * 6.283f, t = Mathf.Sqrt((float)rng.NextDouble()) * 1.05f;
                float y = t < 1f ? H * Mathf.Pow(Mathf.Clamp01(1f - t * t), 1.25f) : 0f;
                var mo = GameObject.CreatePrimitive(PrimitiveType.Cylinder); Object.Destroy(mo.GetComponent<Collider>());
                mo.name = "coop_moneda"; mo.transform.SetParent(raiz, false);
                mo.transform.localPosition = new Vector3(Mathf.Cos(ang) * R * t, y + 0.01f, Mathf.Sin(ang) * R * t);
                mo.transform.localScale = new Vector3(0.14f, 0.012f, 0.14f);
                mo.transform.localRotation = Quaternion.Euler(((float)rng.NextDouble() - 0.5f) * 50f, (float)rng.NextDouble() * 360f, ((float)rng.NextDouble() - 0.5f) * 50f);
                mo.GetComponent<Renderer>().sharedMaterial = oro;
            }
            for (int i = 0; i < 3; i++)
            {
                float ang = (float)rng.NextDouble() * 6.283f, t = 0.5f + 0.4f * (float)rng.NextDouble();
                var li = GameObject.CreatePrimitive(PrimitiveType.Cube); Object.Destroy(li.GetComponent<Collider>());
                li.name = "coop_lingote"; li.transform.SetParent(raiz, false);
                li.transform.localPosition = new Vector3(Mathf.Cos(ang) * R * t, H * Mathf.Pow(Mathf.Clamp01(1f - t * t), 1.25f) + 0.05f, Mathf.Sin(ang) * R * t);
                li.transform.localScale = new Vector3(0.36f, 0.1f, 0.16f); li.transform.localRotation = Quaternion.Euler(((float)rng.NextDouble() - 0.5f) * 30f, (float)rng.NextDouble() * 360f, ((float)rng.NextDouble() - 0.5f) * 30f);
                li.GetComponent<Renderer>().sharedMaterial = oro;
            }
            // destellos que aparecen y se apagan sobre el oro
            var ds = new GameObject("coop_destellos"); ds.transform.SetParent(raiz, false); ds.transform.localPosition = Vector3.up * H * 0.4f;
            var ps = ds.AddComponent<ParticleSystem>();
            var main = ps.main; main.loop = true; main.startLifetime = 0.6f; main.startSpeed = 0f; main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.16f); main.startColor = new Color(1f, 0.95f, 0.7f, 1f); main.maxParticles = 20;
            var em = ps.emission; em.rateOverTime = 9f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Hemisphere; sh.radius = R * 0.85f;
            var col = ps.colorOverLifetime; col.enabled = true;
            var gr = new Gradient(); gr.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.85f, 0.4f), 1f) }, new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(0f, 1f) });
            col.color = gr;
            var rd = ds.GetComponent<ParticleSystemRenderer>(); rd.material = Potenciado.MaterialGas(); rd.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// Suelo del laberinto (pedido 2026-11-17: "sigue mejorando el suelo y las paredes"): sendero de tierra con piedrecitas en todos los
        /// pasillos (la textura del propio terreno del juego) y un bordillo de piedra al pie de cada seto, como un jardín cuidado.
        void PisoLaberinto(Transform pa, TerrainData dn, List<Bounds> setos)
        {
            MaterialesGrandeza();
            // material del sendero: la capa de tierra con piedras del terreno (si existe), a escala de 2,2 m
            Material sendero = matAdoquin;
            var protos = dn.splatPrototypes;
            int idx = protos.Length >= 7 ? 4 : 0;   // las 2 últimas capas son del mod (tierra quemada y negra)
            if (protos.Length > idx && protos[idx].texture != null)
            {
                sendero = new Material(Shader.Find("Standard")) { name = "coop_sendero_laberinto", mainTexture = protos[idx].texture, color = new Color(0.85f, 0.82f, 0.78f) };
                sendero.SetFloat("_Glossiness", 0.08f);
                if (protos[idx].normalMap != null) { sendero.EnableKeyword("_NORMALMAP"); sendero.SetTexture("_BumpMap", protos[idx].normalMap); }
            }
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var tr = new List<int>();
            float P = LabPaso, h = (LabPaso - LabGrosor) / 2f;
            System.Action<float, float, float, float> losa = (x0, z0, x1, z1) =>
            {
                Vector3 a0 = new Vector3(x0, SueloT(dn, x0, z0) + 0.05f, z0), a1 = new Vector3(x1, SueloT(dn, x1, z0) + 0.05f, z0), a2 = new Vector3(x1, SueloT(dn, x1, z1) + 0.05f, z1), a3 = new Vector3(x0, SueloT(dn, x0, z1) + 0.05f, z1);
                QuadOrientado(v, uv, tr, a0, a3, a2, a1, Vector3.up, new Vector2(x0 / 2.2f, z0 / 2.2f), new Vector2(x0 / 2.2f, z1 / 2.2f), new Vector2(x1 / 2.2f, z1 / 2.2f), new Vector2(x1 / 2.2f, z0 / 2.2f));
            };
            for (int i = 0; i < LabNx; i++)
                for (int j = 0; j < LabNz; j++)
                {
                    float cx = LabX0 + (i + 0.5f) * P, cz = LabZ0 + (j + 0.5f) * P;
                    losa(cx - h, cz - h, cx + h, cz + h);
                    if (i < LabNx - 1 && labAbDer[i, j]) losa(cx + h, cz - h, cx + h + LabGrosor, cz + h);
                    if (j < LabNz - 1 && labAbArr[i, j]) losa(cx - h, cz + h, cx + h, cz + h + LabGrosor);
                    if (j == 0 && i == labEi) losa(cx - h, cz - h - LabGrosor, cx + h, cz - h);                       // entrada
                    if (j == LabNz - 1 && (i == labSi || i == labSi - 1)) losa(cx - h, cz + h, cx + h, cz + h + LabGrosor);   // salida
                    if (j == LabNz - 1 && i == labSi - 1) losa(cx + h, cz - h, cx + h + LabGrosor, cz + h + LabGrosor);   // entre las dos celdas de la salida
                }
            var piso = MallaDe(pa, "coop_lab_sendero", v, uv, tr, sendero);
            piso.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // bordillos al pie de los setos
            var bv = new List<Vector3>(); var buv = new List<Vector2>(); var btr = new List<int>(); int nb = 0;
            foreach (var b in setos)
            {
                bool largoX = b.size.x >= b.size.z;
                float y = SueloT(dn, b.center.x, b.center.z);
                foreach (var lado in new[] { -1f, 1f })
                {
                    Vector3 cc = largoX ? new Vector3(b.center.x, y + 0.1f, b.center.z + lado * (b.size.z / 2f + 0.09f)) : new Vector3(b.center.x + lado * (b.size.x / 2f + 0.09f), y + 0.1f, b.center.z);
                    Vector3 tam = largoX ? new Vector3(b.size.x + 0.36f, 0.32f, 0.2f) : new Vector3(0.2f, 0.32f, b.size.z + 0.36f);
                    Caja6(bv, buv, btr, cc, tam, 1.2f); nb++;
                    if (bv.Count > 60000) { MallaDe(pa, "coop_lab_bordillo", bv, buv, btr, matPiedraOscura); bv = new List<Vector3>(); buv = new List<Vector2>(); btr = new List<int>(); }
                }
            }
            if (bv.Count > 0) MallaDe(pa, "coop_lab_bordillo", bv, buv, btr, matPiedraOscura);
            plugin.Log("Laberinto: sendero en los pasillos (" + v.Count / 4 + " losas, textura " + (sendero != null ? sendero.name : "-") + ") y " + nb + " bordillos de piedra");
        }

        void ConoMat(Transform pa, string nombre, Vector3 baseC, float radio, float alto, Material m, bool invertido = false)
        {
            const int N = 12;
            var v = new List<Vector3>(); var tr = new List<int>(); var uv = new List<Vector2>();
            v.Add(baseC + Vector3.up * (invertido ? -alto : alto)); uv.Add(new Vector2(0.5f, 1f));
            for (int i = 0; i < N; i++) { float a = i * Mathf.PI * 2f / N; v.Add(baseC + new Vector3(Mathf.Cos(a) * radio, 0f, Mathf.Sin(a) * radio)); uv.Add(new Vector2(i / (float)N * 2f, 0f)); }
            for (int i = 0; i < N; i++) { int a = 1 + i, b = 1 + (i + 1) % N; tr.Add(0); tr.Add(b); tr.Add(a); tr.Add(0); tr.Add(a); tr.Add(b); }
            var me = new Mesh { name = nombre }; me.SetVertices(v); me.SetUVs(0, uv); me.SetTriangles(tr, 0); me.RecalculateNormals(); me.RecalculateBounds();
            var g = new GameObject(nombre); g.transform.SetParent(pa, false);
            g.AddComponent<MeshFilter>().sharedMesh = me; g.AddComponent<MeshRenderer>().sharedMaterial = m;
        }

        /// Portón monumental del laberinto (al sur, mirando hacia -z) en las coordenadas del terreno norte.
        void PortonLaberinto(Transform pa, float ex, float ey, float ez, TerrainData dn, float sep = 4.3f, bool sueloFijo = false)
        {
            MaterialesGrandeza();
            foreach (var sx in new[] { -1f, 1f })
            {
                float x = ex + sx * sep;
                Pz(pa, "coop_lab_plinto", PrimitiveType.Cube, new Vector3(x, ey + 0.5f, ez), new Vector3(3.0f, 1.0f, 3.2f), 0f, matPiedra);
                Pz(pa, "coop_lab_pilar", PrimitiveType.Cube, new Vector3(x, ey + 4.75f, ez), new Vector3(2.1f, 7.5f, 2.5f), 0f, matPiedra);
                Pz(pa, "coop_lab_banda", PrimitiveType.Cube, new Vector3(x, ey + 3.2f, ez), new Vector3(2.3f, 0.3f, 2.7f), 0f, matPiedraOscura, false);
                Pz(pa, "coop_lab_capitel", PrimitiveType.Cube, new Vector3(x, ey + 8.75f, ez), new Vector3(2.7f, 0.5f, 3.0f), 0f, matPiedra, false);
                Pz(pa, "coop_lab_capitel2", PrimitiveType.Cube, new Vector3(x, ey + 9.2f, ez), new Vector3(2.3f, 0.4f, 2.6f), 0f, matPiedraOscura, false);
                ConoMat(pa, "coop_lab_aguja", new Vector3(x, ey + 9.4f, ez), 1.05f, 3.0f, matPiedra);
                Pz(pa, "coop_lab_bola", PrimitiveType.Sphere, new Vector3(x, ey + 12.65f, ez), new Vector3(0.55f, 0.55f, 0.55f), 0f, matOroG, false);
                // reja de hierro abierta hacia afuera (barrotes con lanzas)
                var piv = new GameObject("coop_lab_reja"); piv.transform.SetParent(pa, false);
                piv.transform.localPosition = new Vector3(ex + sx * (sep - 1.2f), ey, ez - 0.3f);
                piv.transform.localRotation = Quaternion.Euler(0f, sx * -70f, 0f);
                var t = piv.transform;
                for (int i = 0; i < 9; i++)
                {
                    float xx = -sx * (0.2f + i * 0.34f);
                    Pz(t, "coop_lab_reja_barra", PrimitiveType.Cylinder, new Vector3(xx, 2.9f, 0f), new Vector3(0.07f, 2.8f, 0.07f), 0f, matHierroC, false);
                    ConoMat(t, "coop_lab_reja_lanza", new Vector3(xx, 5.7f, 0f), 0.09f, 0.35f, matHierroC);
                }
                foreach (var y in new[] { 0.4f, 2.6f, 5.2f })
                {
                    var hb = Pz(t, "coop_lab_reja_trav", PrimitiveType.Cube, new Vector3(-sx * 1.55f, y, 0f), new Vector3(3.0f, 0.12f, 0.08f), 0f, matHierroC, false);
                }
                // macetón con un seto recortado en bola a cada lado del portón
                float mx = ex + sx * (sep + 4.3f), my = sueloFijo ? ey : SueloT(dn, mx, ez - 1.5f);
                Pz(pa, "coop_lab_maceton", PrimitiveType.Cube, new Vector3(mx, my + 0.55f, ez - 1.5f), new Vector3(2.2f, 1.1f, 2.2f), 0f, matPiedra);
                Pz(pa, "coop_lab_maceton_borde", PrimitiveType.Cube, new Vector3(mx, my + 1.15f, ez - 1.5f), new Vector3(2.45f, 0.18f, 2.45f), 0f, matPiedraOscura, false);
                var bola = Pz(pa, "coop_lab_topiaria", PrimitiveType.Sphere, new Vector3(mx, my + 2.35f, ez - 1.5f), new Vector3(2.3f, 2.3f, 2.3f), 0f, matSeto, false);
                var punta = Pz(pa, "coop_lab_topiaria", PrimitiveType.Sphere, new Vector3(mx, my + 3.8f, ez - 1.5f), new Vector3(1.3f, 1.3f, 1.3f), 0f, matSeto, false);
                // braseros delante del portón
                float bx = ex + sx * (sep + 2.1f);
                Brasero(pa, new Vector3(bx, sueloFijo ? ey : SueloT(dn, bx, ez - 4.2f), ez - 4.2f), 1.15f);
            }
            // arco ojival de dovelas entre los pilares, con farol colgante bajo la clave
            ArcoOjival(pa, "coop_lab_arco", ex, ey + 8.5f, ez, sep - 1.1f, 0.9f, 2.3f, 8, matPiedra);
            Timpano(pa, "coop_lab_timpano", ex, ey + 8.5f, ez + 0.9f, sep - 1.1f, matPiedraOscura);
            Pz(pa, "coop_lab_dintel", PrimitiveType.Cube, new Vector3(ex, ey + 8.4f, ez), new Vector3(sep * 2f, 0.6f, 2.4f), 0f, matPiedra, false);
            FarolColgante(pa, new Vector3(ex, ey + 8.1f, ez - 0.4f), 1.4f);
            // explanada de adoquines delante del portón
            Pz(pa, "coop_lab_explanada", PrimitiveType.Cube, new Vector3(ex, ey + 0.04f, ez - 6.5f), new Vector3(sep * 2f + 7.4f, 0.1f, 11f), 0f, matAdoquin, true, 3.5f);
            plugin.Log("Laberinto: portón monumental (pilares con agujas, arco ojival de dovelas, rejas de hierro, farol colgante, braseros y setos en macetones)");
        }
    }
}

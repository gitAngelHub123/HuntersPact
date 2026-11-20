using System.Collections.Generic;
using UnityEngine;

namespace WitchHuntCoop
{
    /// Bóveda gigante bajo el castillo (pedido 2026-11-17: "usa toda la parte sobrante debajo del castillo para una bóveda gigante con
    /// montones y montones de oro, y solo unas cuantas pilas que se puedan tomar", con cristales como en la imagen de referencia).
    /// Acceso: tras la puerta de hierro del sótano de la Torre del Homenaje la escalera de caracol sigue bajando por un pozo (mismo giro),
    /// y desde su fondo una escalera recta entra por el norte a una sala de 50 × 46 (× 1,6 = 80 × 74 m) y 8,7 (14 m) de alto bajo la
    /// plataforma. El terreno se rebaja solo dentro de la huella de la plataforma y de la torre (lo tapan por arriba).
    /// Coordenadas del castillo (se agranda ×1,6 con la raíz).
    public partial class Mundo
    {
        const float BovX = 25f, BovZ0 = -3f, BovZ1 = 43f, BovPiso = -11f, BovTecho = -2.3f, PozoPiso = -7.4f;
        static Material matCristal, matGemaR, matGemaA, matGemaV;
        const float AltoManto = 0.35f;

        /// Manto de oro que cubre todo el piso de la bóveda: malla ondulada (monedas amontonadas) de 0,15 a 0,55 m,
        /// más alta junto a las paredes, con colisión. Se aplana al pie de la escalera para no tapar el último peldaño.
        void MantoOro(Transform pa, Material oro, System.Func<float, float, float, bool> libre)
        {
            const float paso = 0.7f;
            int nx = Mathf.CeilToInt(BovX * 2f / paso) + 1, nz = Mathf.CeilToInt((BovZ1 - BovZ0) / paso) + 1;
            var v = new List<Vector3>(nx * nz); var uv = new List<Vector2>(nx * nz); var tr = new List<int>();
            var rng = new System.Random(77121);
            float f1 = 0.6f + (float)rng.NextDouble() * 0.2f, f2 = 1.3f + (float)rng.NextDouble() * 0.3f;
            for (int iz = 0; iz < nz; iz++)
                for (int ix = 0; ix < nx; ix++)
                {
                    float x = -BovX + Mathf.Min(ix * paso, BovX * 2f), z = BovZ0 + Mathf.Min(iz * paso, BovZ1 - BovZ0);
                    float pared = Mathf.Min(BovX - Mathf.Abs(x), Mathf.Min(z - BovZ0, BovZ1 - z));
                    float h = 0.25f + 0.12f * Mathf.Sin(x * f1 + z * 0.4f) * Mathf.Cos(z * f2 - x * 0.3f) + 0.06f * Mathf.Sin(x * 3.1f + z * 2.7f);
                    h += Mathf.Clamp01((3f - pared) / 3f) * 0.3f;                       // amontonado contra los muros
                    if (Mathf.Abs(x) < 3.2f && z > BovZ1 - 6f) h = Mathf.Lerp(0.04f, h, Mathf.Clamp01((BovZ1 - 1f - z) / 5f));   // pie de la escalera casi plano
                    v.Add(new Vector3(x, BovPiso + Mathf.Max(0.04f, h), z)); uv.Add(new Vector2(x * 0.35f, z * 0.35f));
                }
            for (int iz = 0; iz < nz - 1; iz++)
                for (int ix = 0; ix < nx - 1; ix++)
                {
                    int a = iz * nx + ix, b = a + 1, c = a + nx, d = c + 1;
                    tr.Add(a); tr.Add(c); tr.Add(b); tr.Add(b); tr.Add(c); tr.Add(d);
                }
            var m = new Mesh { name = "coop_boveda_manto_oro" };
            m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(tr, 0); m.RecalculateNormals(); m.RecalculateBounds();
            var g = new GameObject("coop_boveda_manto_oro"); g.transform.SetParent(pa, false);
            g.AddComponent<MeshFilter>().sharedMesh = m; g.AddComponent<MeshRenderer>().sharedMaterial = oro;
            g.AddComponent<MeshCollider>().sharedMesh = m;
            plugin.Log("Castillo: manto de oro en la bóveda (" + v.Count + " vértices)");
        }

        void ConstruirBovedaGigante(Transform pa, float rEsc, float largoEsc)
        {
            Vector3 c = new Vector3(TorreX, 0f, TorreZ);
            float rIn = 4.5f, rOut = TorreR - TorreMuro;
            // ---- 1. Pozo: el caracol sigue bajando tras la puerta (135,5° → −89,5°) hasta el piso del pozo ----
            const int Pasos = 26;
            for (int k = 0; k < Pasos; k++)
            {
                float ang = SotanoPuertaAng - 4.5f - k * 360f / TorrePasosVuelta, rad = ang * Mathf.Deg2Rad, top = 0.2f - TorreSubida * (k + 1);
                Pz(pa, "coop_pozo_peldano", PrimitiveType.Cube, new Vector3(c.x + Mathf.Cos(rad) * rEsc, top - 0.35f, c.z + Mathf.Sin(rad) * rEsc), new Vector3(largoEsc, 0.7f, 1.8f), -ang, matPiedra);
                if (k % 7 == 3) { Vector3 nIn = -new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)); Antorcha(pa, c - nIn * (rOut + 0.02f) + Vector3.up * (top + 1.9f), nIn); }
            }
            Anillo(pa, "coop_pozo_piso", c, PozoPiso - 0.4f, PozoPiso, rIn - 0.05f, rOut + 0.05f, 0f, 360f, matAdoquin);
            // acera de losas alrededor de la base de la torre: tapa el desnivel del terreno rebajado bajo el muro
            Anillo(pa, "coop_torre_acera", c, -0.6f, 0.08f, TorreR - 0.2f, TorreR + 3.6f, 0f, 360f, matAdoquin);
            Pz(pa, "coop_pozo_eje", PrimitiveType.Cylinder, new Vector3(c.x, (PozoPiso - 0.4f) / 2f, c.z), new Vector3(9f, (0.4f - PozoPiso) / 2f + 0.05f, 9f), 0f, matPiedra);
            // muro del pozo (bajo tierra) con la salida al sur (260°–280°)
            for (int k = 0; k < 36; k++)
            {
                float ang = k * 10f, rad = ang * Mathf.Deg2Rad;
                Vector3 m = new Vector3(c.x + Mathf.Cos(rad) * (TorreR - TorreMuro / 2f), 0f, c.z + Mathf.Sin(rad) * (TorreR - TorreMuro / 2f));
                bool salida = k >= 26 && k <= 28;
                float y0 = PozoPiso - 0.4f, y1 = 0.05f;
                if (salida) y0 = PozoPiso + 3.6f;
                Pz(pa, "coop_pozo_muro", PrimitiveType.Cube, new Vector3(m.x, (y0 + y1) / 2f, m.z), new Vector3(TorreMuro, y1 - y0, 2.05f), -ang, matPiedra);
            }
            // ---- 2. Escalera recta del pozo a la bóveda (hacia el sur, x = 0) ----
            float zIni = c.z - rOut, alza = (PozoPiso - BovPiso) / 13f;
            for (int i = 0; i < 13; i++)
            {
                float top = PozoPiso - alza * (i + 1), z = zIni - 0.6f * (i + 0.5f);
                Pz(pa, "coop_boveda_escalon", PrimitiveType.Cube, new Vector3(0f, (top + BovPiso - 0.6f) / 2f, z), new Vector3(4.2f, top - BovPiso + 0.6f, 0.62f), 0f, matPiedra);
            }
            foreach (var sx in new[] { -1f, 1f })   // paredes del paso entre la torre y la sala
                Pz(pa, "coop_boveda_paso", PrimitiveType.Cube, new Vector3(sx * 2.5f, (BovPiso + PozoPiso + 3.6f) / 2f, (zIni + BovZ1) / 2f), new Vector3(0.6f, PozoPiso + 3.6f - BovPiso, zIni - BovZ1 + 1.6f), 0f, matPiedra);
            Pz(pa, "coop_boveda_paso_techo", PrimitiveType.Cube, new Vector3(0f, PozoPiso + 3.8f, (zIni + BovZ1) / 2f), new Vector3(5.6f, 0.4f, zIni - BovZ1 + 1.6f), 0f, matPiedra);
            // ---- 3. La sala ----
            Pz(pa, "coop_boveda_piso", PrimitiveType.Cube, new Vector3(0f, BovPiso - 0.3f, (BovZ0 + BovZ1) / 2f), new Vector3(BovX * 2f + 1.6f, 0.6f, BovZ1 - BovZ0 + 1.6f), 0f, matAdoquin);
            Pz(pa, "coop_boveda_techo", PrimitiveType.Cube, new Vector3(0f, BovTecho + 0.3f, (BovZ0 + BovZ1) / 2f), new Vector3(BovX * 2f + 1.6f, 0.6f, BovZ1 - BovZ0 + 1.6f), 0f, matPiedra);
            float hS = BovTecho - BovPiso, yM = (BovTecho + BovPiso) / 2f;
            foreach (var sx in new[] { -1f, 1f }) Pz(pa, "coop_boveda_pared", PrimitiveType.Cube, new Vector3(sx * (BovX + 0.4f), yM, (BovZ0 + BovZ1) / 2f), new Vector3(0.8f, hS, BovZ1 - BovZ0 + 1.6f), 0f, matPiedra);
            Pz(pa, "coop_boveda_pared", PrimitiveType.Cube, new Vector3(0f, yM, BovZ0 - 0.4f), new Vector3(BovX * 2f + 1.6f, hS, 0.8f), 0f, matPiedra);
            foreach (var sx in new[] { -1f, 1f }) Pz(pa, "coop_boveda_pared", PrimitiveType.Cube, new Vector3(sx * (BovX + 2.2f) / 2f, yM, BovZ1 + 0.4f), new Vector3(BovX - 2.2f + 0.8f, hS, 0.8f), 0f, matPiedra);
            Pz(pa, "coop_boveda_dintel", PrimitiveType.Cube, new Vector3(0f, (PozoPiso + 3.6f + BovTecho) / 2f, BovZ1 + 0.4f), new Vector3(4.4f, BovTecho - PozoPiso - 3.6f, 0.8f), 0f, matPiedra);
            // columnas con capitel
            var pilares = new List<Vector2>();
            foreach (var x in new[] { -15f, -5f, 5f, 15f }) foreach (var z in new[] { 6f, 19f, 32f })
            {
                pilares.Add(new Vector2(x, z));
                Pz(pa, "coop_boveda_columna", PrimitiveType.Cylinder, new Vector3(x, yM, z), new Vector3(1.6f, hS / 2f, 1.6f), 0f, matPiedra);
                Pz(pa, "coop_boveda_capitel", PrimitiveType.Cube, new Vector3(x, BovTecho - 0.3f, z), new Vector3(2.4f, 0.6f, 2.4f), 0f, matPiedraOscura, false);
                Pz(pa, "coop_boveda_basa", PrimitiveType.Cube, new Vector3(x, BovPiso + 0.25f, z), new Vector3(2.2f, 0.5f, 2.2f), 0f, matPiedraOscura);
            }
            // ---- 4. Montones y montones de oro (decorado, con colisión) ----
            var rng = new System.Random(80451); var oro = MaterialOro(); int cerros = 0;
            System.Func<float, float, float, bool> libre = (x, z, r) =>
            {
                if (Mathf.Abs(x) < 3.2f + r && z > BovZ1 - 10f) return false;                     // pie de la escalera
                foreach (var p in pilares) if (Vector2.Distance(p, new Vector2(x, z)) < r + 1.6f) return false;
                return Mathf.Abs(x) < BovX - r - 0.5f && z > BovZ0 + r + 0.5f && z < BovZ1 - r - 0.5f;
            };
            System.Action<float, float, float, float> cerro = (x, z, R, H) =>
            {
                var g = new GameObject("coop_boveda_cerro"); g.transform.SetParent(pa, false); g.transform.localPosition = new Vector3(x, BovPiso, z);
                g.transform.localRotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                MontanaOro(g.transform, oro, R, H, rng.Next());
                foreach (var mf in g.GetComponentsInChildren<MeshFilter>()) if (mf.name == "coop_cerro_oro") mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
                cerros++;
            };
            // 25 MONTAÑAS DE ORO RECOGIBLES (pedido 2026-11-18): de 10 000 (la del centro) a 100, y 15 más de 3000 a 100; aquí solo se eligen los sitios,
            // las crea Tesoro.RepartirMontanas como montones que se toman
            montanasBoveda.Clear();
            montanasBoveda.Add(new Vector4(0f, 19f, RadioMontana(MontoMontana(0)), 0f));
            for (int k = 1; k < MontanasBoveda; k++)
            {
                float R = RadioMontana(MontoMontana(k));
                for (int i = 0; i < 3000; i++)
                {
                    if (i > 0 && i % 600 == 0) R *= 0.82f;   // no cupo: se encoge un poco y sigue buscando (deben estar todas)
                    float x = ((float)rng.NextDouble() * 2f - 1f) * BovX, z = BovZ0 + (float)rng.NextDouble() * (BovZ1 - BovZ0);
                    if (!libre(x, z, R)) continue;
                    bool choca = false;
                    foreach (var o in montanasBoveda) if (Vector2.Distance(new Vector2(x, z), new Vector2(o.x, o.y)) < R + o.z + 0.4f) { choca = true; break; }
                    if (choca) continue;
                    montanasBoveda.Add(new Vector4(x, z, R, k)); break;
                }
            }
            cerros = montanasBoveda.Count;
            // ---- 5. Sin cristales ni gemas (pedido 2026-11-17): el suelo entero es un manto de oro, con copas encima ----
            MantoOro(pa, oro, libre);
            // (pedido 2026-11-18) sin copas: cofres de tesoro, lingotes, gemas, diamantes y collares
            try { JoyasBoveda(pa, oro, libre, BovPiso + AltoManto, BovX, BovZ0, BovZ1); } catch (System.Exception e) { plugin.Log("Castillo: joyas de la bóveda " + e); }
            int racimos = 0;
            // luz cálida del oro y antorchas en los muros
            foreach (var p in new[] { new Vector3(0f, -5f, 19f), new Vector3(-14f, -6f, 8f), new Vector3(14f, -6f, 8f), new Vector3(-14f, -6f, 32f), new Vector3(14f, -6f, 32f) })
                LuzCastillo(pa, p, new Color(1f, 0.72f, 0.3f), 22f, 1.5f);
            foreach (var z in new[] { 8f, 20f, 32f }) foreach (var sx in new[] { -1f, 1f })
                Antorcha(pa, new Vector3(sx * (BovX - 0.02f), BovPiso + 3.2f, z), new Vector3(-sx, 0f, 0f));
            // ---- 6. Pilas que SÍ se pueden tomar (pocas) y oro suelto ----
            sitiosBoveda.Clear();
            foreach (var p in new[] { new Vector2(-9f, 12f), new Vector2(9f, 26f), new Vector2(-10f, 37f), new Vector2(10f, 2f) }) sitiosBoveda.Add(new Vector3(p.x, BovPiso, p.y));
            sotanoOroSuelto.Clear();
            foreach (var p in new[] { new Vector2(-3f, 36f), new Vector2(3f, 30f), new Vector2(-6f, 25f), new Vector2(7f, 14f), new Vector2(-12f, 20f), new Vector2(12f, 38f) }) sotanoOroSuelto.Add(new Vector3(p.x, BovPiso, p.y));
            // ---- 7. Terreno: se rebaja bajo la plataforma y la torre para que nada atraviese la sala ----
            int muestras = RebajarTerrenoBoveda(pa);
            plugin.Log("Castillo: bóveda gigante de " + (BovX * 2f * CastilloEscala).ToString("0") + " × " + ((BovZ1 - BovZ0) * CastilloEscala).ToString("0") + " m con " + cerros + " montañas de oro, " + racimos + " racimos de cristales, pozo de " + Pasos + " peldaños (" + muestras + " muestras de terreno rebajadas)");
        }

        /// Prisma hexagonal con punta (cristal) en una malla combinada.
        static void Cristal(List<Vector3> v, List<Vector2> uv, List<int> tr, Vector3 b, Vector3 d, float L, float r)
        {
            Vector3 a = Vector3.Cross(d, Mathf.Abs(d.y) < 0.9f ? Vector3.up : Vector3.right).normalized, c2 = Vector3.Cross(d, a);
            Vector3 cuerpo = b + d * (L * 0.78f), punta = b + d * L;
            for (int k = 0; k < 6; k++)
            {
                float a0 = k * 60f * Mathf.Deg2Rad, a1 = (k + 1) * 60f * Mathf.Deg2Rad;
                Vector3 o0 = (a * Mathf.Cos(a0) + c2 * Mathf.Sin(a0)) * r, o1 = (a * Mathf.Cos(a1) + c2 * Mathf.Sin(a1)) * r;
                Vector3 n = (o0 + o1).normalized;
                QuadOrientado(v, uv, tr, b + o0, b + o1, cuerpo + o1, cuerpo + o0, n, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f));
                int i0 = v.Count; v.Add(cuerpo + o0); v.Add(cuerpo + o1); v.Add(punta); uv.Add(Vector2.zero); uv.Add(Vector2.right); uv.Add(Vector2.up);
                Vector3 nt = Vector3.Cross(v[i0 + 1] - v[i0], v[i0 + 2] - v[i0]);
                if (Vector3.Dot(nt, n + d) >= 0f) { tr.Add(i0); tr.Add(i0 + 1); tr.Add(i0 + 2); } else { tr.Add(i0); tr.Add(i0 + 2); tr.Add(i0 + 1); }
            }
        }

        /// Rebaja el terreno bajo la plataforma del castillo (con 1,8 m de margen hacia adentro, para que la transición quede tapada) y bajo la
        /// torre (radio 10), hasta 1,5 m por debajo del piso de la bóveda.
        int RebajarTerrenoBoveda(Transform pa)
        {
            var t = pa.GetComponentInParent<Terrain>(); if (t == null) return 0;
            var td = t.terrainData; Vector3 o = t.transform.position; int R = td.heightmapResolution;
            float px = td.size.x / (R - 1), pz = td.size.z / (R - 1);
            float objetivo = (pa.TransformPoint(new Vector3(0f, BovPiso - 1.5f, 0f)).y - o.y) / td.size.y;
            Vector3 a = pa.TransformPoint(new Vector3(-29f, 0f, -6f)), b = pa.TransformPoint(new Vector3(29f, 0f, TorreZ + TorreR));
            int x0 = Mathf.Clamp(Mathf.FloorToInt((Mathf.Min(a.x, b.x) - o.x) / px), 0, R - 1), x1 = Mathf.Clamp(Mathf.CeilToInt((Mathf.Max(a.x, b.x) - o.x) / px), 0, R - 1);
            int z0 = Mathf.Clamp(Mathf.FloorToInt((Mathf.Min(a.z, b.z) - o.z) / pz), 0, R - 1), z1 = Mathf.Clamp(Mathf.CeilToInt((Mathf.Max(a.z, b.z) - o.z) / pz), 0, R - 1);
            int w = x1 - x0 + 1, h = z1 - z0 + 1; if (w < 2 || h < 2) return 0;
            var hs = td.GetHeights(x0, z0, w, h); int n = 0;
            for (int zi = 0; zi < h; zi++) for (int xi = 0; xi < w; xi++)
            {
                Vector3 l = pa.InverseTransformPoint(new Vector3(o.x + (x0 + xi) * px, 0f, o.z + (z0 + zi) * pz));
                bool enPlataforma = Mathf.Abs(l.x) < 29f - 1.9f && l.z > -6f + 1.9f && l.z < 50f - 1.9f;
                float dt = new Vector2(l.x - TorreX, l.z - TorreZ).magnitude;
                if (!enPlataforma && dt > TorreR + 0.4f) continue;   // hasta fuera del muro: con 10 m el relieve subía en diagonal dentro del pasillo del pozo ("picos de roca", 2026-11-17)
                if (hs[zi, xi] > objetivo) { hs[zi, xi] = objetivo; n++; }
            }
            td.SetHeights(x0, z0, hs);
            return n;
        }
    }
}

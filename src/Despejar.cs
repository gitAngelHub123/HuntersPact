// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using System.Collections.Generic;
using UnityEngine;

namespace WitchHuntCoop
{
    /// Ningún árbol encima de una construcción del mod (pedido 2026-11-17: cabañas metidas dentro de troncos gigantes).
    /// Va al final del armado del mundo (después de mover y agregar árboles): quita de todos los terrenos los árboles
    /// que caen sobre la huella de una construcción, con un margen que crece con el tamaño del árbol (los gigantes
    /// de las minas tienen troncos de varios metros).
    public partial class Mundo
    {
        static readonly string[] Construccion = { "coop_casa_escopeta_", "coop_lugar_", "coop_pueblo_destruido_", "coop_puesto_", "coop_establo", "coop_granja", "coop_mina_boca", "coop_boca_" };

        void DespejarArbolesEstructuras()
        {
            // huellas: centro y radio en el plano de cada construcción
            var huellas = new List<Vector4>();   // x, z, radio
            var raices = new List<GameObject>();
            foreach (var g in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (!g.activeInHierarchy) continue;
                foreach (var p in Construccion) if (g.name.StartsWith(p)) { raices.Add(g); break; }
            }
            foreach (var g in lugaresCopias) if (g != null && g.activeInHierarchy && !raices.Contains(g) && !g.name.Contains("cadaver")) raices.Add(g);
            foreach (var c in cabanas) if (c.Raiz != null && !raices.Contains(c.Raiz)) raices.Add(c.Raiz);
            foreach (var m in minas) if (m.Boca != null && !raices.Contains(m.Boca)) raices.Add(m.Boca);
            foreach (var g in raices)
            {
                Bounds b; if (!CajaDe(new List<GameObject> { g }, out b)) continue;
                float r = Mathf.Max(b.extents.x, b.extents.z);
                if (r > 60f) r = 60f;   // (algo enorme mal medido no debe vaciar el bosque)
                bool esCasa = g.name.StartsWith("coop_cabana") || g.name.StartsWith("coop_casa_escopeta");
                // cabañas y casas: 6 m más, para que ni las ramas de un árbol vecino se metan dentro (pedido 2026-11-18)
                huellas.Add(new Vector4(b.center.x, b.center.z, r + 2f + (esCasa ? 6f : 0f), esCasa ? 1f : 0f));
            }
            if (huellas.Count == 0) return;
            int total = 0;
            foreach (var t in Terrain.activeTerrains)
            {
                if (t == null || t.terrainData == null) continue;
                var td = t.terrainData; var arr = td.treeInstances; if (arr == null || arr.Length == 0) continue;
                Vector3 o = t.transform.position, s = td.size;
                var quedan = new List<TreeInstance>(arr.Length); int fuera = 0;
                foreach (var ti in arr)
                {
                    float x = o.x + ti.position.x * s.x, z = o.z + ti.position.z * s.z;
                    float margen = 1.2f * Mathf.Max(1f, ti.widthScale);   // tronco más grueso cuanto más grande el árbol
                    bool choca = false;
                    foreach (var h in huellas)
                    {
                        float dx = x - h.x, dz = z - h.y, rr = h.z + margen;
                        if (dx * dx + dz * dz < rr * rr) { choca = true; break; }
                    }
                    if (choca) fuera++; else quedan.Add(ti);
                }
                if (fuera == 0) continue;
                td.treeInstances = quedan.ToArray(); t.Flush();
                var col = t.GetComponent<TerrainCollider>(); if (col != null) { col.enabled = false; col.enabled = true; }
                total += fuera;
            }
            plugin.Log("Despejar: " + total + " árboles quitados de encima de " + huellas.Count + " construcciones");
            try { DespejarCasas(huellas); } catch (System.Exception e) { plugin.Log("Despejar: error en las casas " + e.Message); }
        }

        /// Alrededor de cabañas y casas (huellas con w = 1): quita la vegetación del terreno (detalles) y los árboles/arbustos
        /// sueltos de la escena, para que no atraviesen las paredes ni se metan dentro.
        void DespejarCasas(List<Vector4> huellas)
        {
            int detalles = 0, objetos = 0;
            foreach (var t in Terrain.activeTerrains)
            {
                if (t == null || t.terrainData == null) continue;
                var td = t.terrainData; int res = td.detailResolution; if (res <= 0 || td.detailPrototypes.Length == 0) continue;
                Vector3 o = t.transform.position, sz = td.size;
                for (int capa = 0; capa < td.detailPrototypes.Length; capa++)
                {
                    bool cambio = false; int[,] mapa = null;
                    foreach (var h in huellas)
                    {
                        if (h.w < 0.5f) continue;
                        float rr = h.z;
                        int x0 = Mathf.Clamp(Mathf.FloorToInt((h.x - rr - o.x) / sz.x * res), 0, res - 1), x1 = Mathf.Clamp(Mathf.CeilToInt((h.x + rr - o.x) / sz.x * res), 0, res - 1);
                        int z0 = Mathf.Clamp(Mathf.FloorToInt((h.y - rr - o.z) / sz.z * res), 0, res - 1), z1 = Mathf.Clamp(Mathf.CeilToInt((h.y + rr - o.z) / sz.z * res), 0, res - 1);
                        if (x1 <= x0 || z1 <= z0) continue;
                        if (mapa == null) mapa = td.GetDetailLayer(0, 0, res, res, capa);
                        for (int z = z0; z <= z1; z++) for (int x = x0; x <= x1; x++)
                        {
                            float wx = o.x + (x + 0.5f) / res * sz.x, wz = o.z + (z + 0.5f) / res * sz.z;
                            if ((wx - h.x) * (wx - h.x) + (wz - h.y) * (wz - h.y) > rr * rr) continue;
                            if (mapa[z, x] != 0) { mapa[z, x] = 0; cambio = true; detalles++; }
                        }
                    }
                    if (cambio) td.SetDetailLayer(0, 0, capa, mapa);
                }
            }
            foreach (var g in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (!g.activeInHierarchy || g.name.StartsWith("coop_")) continue;
                string n = g.name.ToLowerInvariant();
                if (!(n.Contains("tree") || n.Contains("bush") || n.Contains("plant") || n.Contains("branch") || n.Contains("root") || n.Contains("shrub") || n.Contains("fern"))) continue;
                Vector3 p = g.transform.position;
                foreach (var h in huellas)
                {
                    if (h.w < 0.5f) continue;
                    if ((p.x - h.x) * (p.x - h.x) + (p.z - h.y) * (p.z - h.y) < h.z * h.z) { g.SetActive(false); objetos++; break; }
                }
            }
            plugin.Log("Despejar: alrededor de cabañas y casas se quitaron " + detalles + " matas del terreno y " + objetos + " árboles o arbustos sueltos");
        }

        /// LIMPIEZA FINAL (pedido 2026-11-18), después de colocar todo: (1) ningún árbol de ningún terreno sobre un camino ni a
        /// menos de ~2 m (como en el mapa original: los caminos son para orientarse); (2) nada de rocas, troncos ni tocones sobre
        /// un camino; (3) alrededor de cabañas y casas: ningún árbol a 10 m y ninguna roca/árbol suelto tocándolas.
        void LimpiezaFinal()
        {
            // cabañas y casas
            var casasB = new List<Bounds>();
            foreach (var g in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                if (g.activeInHierarchy && (g.name.StartsWith("coop_cabana") || g.name.StartsWith("coop_casa_escopeta")))
                { Bounds b; if (CajaDe(new List<GameObject> { g }, out b) && b.extents.x < 40f && b.extents.z < 40f) casasB.Add(b); }
            int enCamino = 0, enCasa = 0;
            foreach (var t in Terrain.activeTerrains)
            {
                if (t == null || t.terrainData == null) continue;
                var td = t.terrainData; var arr = td.treeInstances; if (arr == null || arr.Length == 0) continue;
                Vector3 o = t.transform.position, sz = td.size;
                // alphamaps de este terreno una sola vez (caminos = capa 0 > 0,5 o capa 4 > 0,45, la regla de EsCamino)
                float[,,] am = null; int aw = td.alphamapWidth, ah = td.alphamapHeight;
                if (td.alphamapLayers >= 5) am = td.GetAlphamaps(0, 0, aw, ah);
                System.Func<float, float, bool> camino = (x, z) =>
                {
                    if (am == null) return false;
                    int ax = Mathf.Clamp((int)((x - o.x) / sz.x * aw), 0, aw - 1), az = Mathf.Clamp((int)((z - o.z) / sz.z * ah), 0, ah - 1);
                    return am[az, ax, 0] > 0.5f || am[az, ax, 4] > 0.45f;
                };
                var quedan = new List<TreeInstance>(arr.Length); int fuera = 0;
                foreach (var ti in arr)
                {
                    float x = o.x + ti.position.x * sz.x, z = o.z + ti.position.z * sz.z;
                    float m = 1.6f + 0.8f * Mathf.Max(1f, ti.widthScale);
                    bool quitar = false;
                    if (am != null)
                        for (int k = 0; k < 9 && !quitar; k++)
                        {
                            float a = k * Mathf.PI * 2f / 8f, d = k == 8 ? 0f : m;
                            if (camino(x + Mathf.Cos(a) * d, z + Mathf.Sin(a) * d)) { quitar = true; enCamino++; }
                        }
                    if (!quitar)
                        foreach (var b in casasB)
                        {
                            float dx = Mathf.Max(0f, Mathf.Abs(x - b.center.x) - b.extents.x), dz = Mathf.Max(0f, Mathf.Abs(z - b.center.z) - b.extents.z);
                            if (dx * dx + dz * dz < 100f) { quitar = true; enCasa++; break; }
                        }
                    if (quitar) fuera++; else quedan.Add(ti);
                }
                if (fuera == 0) continue;
                td.treeInstances = quedan.ToArray(); t.Flush();
                var col = t.GetComponent<TerrainCollider>(); if (col != null) { col.enabled = false; col.enabled = true; }
            }
            // objetos sueltos: rocas, troncos, tocones y árboles sobre caminos o tocando una cabaña
            int objetos = 0;
            foreach (var g in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (!g.activeInHierarchy) continue;
                string n = g.name.ToLowerInvariant();
                if (!(n.Contains("rock") || n.Contains("stone") && !n.Contains("save") || n.Contains("log") || n.Contains("stump") || n.Contains("tree") || n.Contains("bush") || n.Contains("roca"))) continue;
                if (n.Contains("cabana") || n.Contains("casa_escopeta") || n.Contains("save_stone")) continue;
                Bounds b; if (!CajaDe(new List<GameObject> { g }, out b) || b.extents.x > 25f || b.extents.z > 25f) continue;
                bool quitar = EsCamino(new Vector3(b.center.x, 0f, b.center.z));
                if (!quitar) foreach (var cb in casasB) { var e = cb; e.Expand(3f); if (e.Intersects(b)) { quitar = true; break; } }
                if (quitar) { g.SetActive(false); objetos++; }
            }
            plugin.Log("Limpieza final: " + enCamino + " árboles quitados de los caminos, " + enCasa + " junto a cabañas/casas, " + objetos + " rocas/troncos/árboles sueltos quitados");
        }
    }
}
using System.Collections.Generic;
using UnityEngine;

namespace WitchHuntCoop
{
    /// Llano junto al borde este, entre el bosque original y la zona nueva (pedido 2026-11-17, captura terreno.xcf): ahí el río dejaba un
    /// cauce de agua gris entre dos lomas y una meseta cortada antes de la zona nueva. Se deja todo plano a 42 m (tapa el río en ese tramo),
    /// con orillas suaves de 45 m, y con pocos árboles. Afecta a los dos terrenos (el original se restaura al quitar la expansión).
    public partial class Mundo
    {
        static readonly Rect LlanoEsteZona = new Rect(1630f, 1800f, 370f, 235f);   // x 1630–2000, z 1800–2035 (coordenadas del mundo)
        const float LlanoEsteAltura = 42f, LlanoEsteOrilla = 45f;

        void AplanarLlanoEste()
        {
            var rng = new System.Random(4407); int muestras = 0, quitados = 0, puestos = 0;
            foreach (var t in Terrain.activeTerrains)
            {
                if (t == null || t.name.StartsWith("coop_bosque")) continue;
                var td = t.terrainData; Vector3 o = t.transform.position; int R = td.heightmapResolution;
                float px = td.size.x / (R - 1), pz = td.size.z / (R - 1);
                Rect r = LlanoEsteZona; float m = LlanoEsteOrilla;
                int x0 = Mathf.Clamp(Mathf.FloorToInt((r.xMin - m - o.x) / px), 0, R - 1), x1 = Mathf.Clamp(Mathf.CeilToInt((r.xMax + m - o.x) / px), 0, R - 1);
                int z0 = Mathf.Clamp(Mathf.FloorToInt((r.yMin - m - o.z) / pz), 0, R - 1), z1 = Mathf.Clamp(Mathf.CeilToInt((r.yMax + m - o.z) / pz), 0, R - 1);
                if (x1 <= x0 || z1 <= z0) continue;
                int w = x1 - x0 + 1, h = z1 - z0 + 1; var hs = td.GetHeights(x0, z0, w, h);
                float obj = (LlanoEsteAltura - o.y) / td.size.y; bool toco = false;
                for (int zi = 0; zi < h; zi++) for (int xi = 0; xi < w; xi++)
                {
                    float x = o.x + (x0 + xi) * px, z = o.z + (z0 + zi) * pz;
                    float dx = Mathf.Max(0f, Mathf.Max(r.xMin - x, x - r.xMax)), dz = Mathf.Max(0f, Mathf.Max(r.yMin - z, z - r.yMax));
                    float d = Mathf.Sqrt(dx * dx + dz * dz); if (d >= m) continue;
                    float wgt = d <= 0f ? 1f : Mathf.SmoothStep(1f, 0f, d / m);
                    float onda = (Mathf.PerlinNoise(x * 0.02f, z * 0.02f) - 0.5f) * 1.2f / td.size.y;   // apenas ondulado, no una mesa
                    hs[zi, xi] = Mathf.Lerp(hs[zi, xi], obj + onda, wgt); muestras++; toco = true;
                }
                if (!toco) continue;
                td.SetHeights(x0, z0, hs);
                // árboles: fuera los que había en el llano (quedaban enterrados o en el aire) y unos pocos nuevos
                var lista = new List<TreeInstance>(td.treeInstances);
                quitados += lista.RemoveAll(ti => { float x = o.x + ti.position.x * td.size.x, z = o.z + ti.position.z * td.size.z; return r.Contains(new Vector2(x, z)); });
                for (float gz = r.yMin + 8f; gz < r.yMax - 4f; gz += 26f)
                    for (float gx = r.xMin + 8f; gx < r.xMax - 4f; gx += 26f)
                    {
                        if (rng.NextDouble() < 0.45) continue;
                        float x = gx + ((float)rng.NextDouble() - 0.5f) * 18f, z = gz + ((float)rng.NextDouble() - 0.5f) * 18f;
                        float nx = (x - o.x) / td.size.x, nz = (z - o.z) / td.size.z; if (nx < 0f || nx > 1f || nz < 0f || nz > 1f) continue;
                        if (x > 1936f && x < 1944f) continue;   // no pegado a la pared invisible
                        float ev = t == nuevo ? 1.4f + (float)rng.NextDouble() * 1.2f : 0.8f + (float)rng.NextDouble() * 0.5f;
                        lista.Add(new TreeInstance { prototypeIndex = rng.Next(td.treePrototypes.Length), position = new Vector3(nx, td.GetInterpolatedHeight(nx, nz) / td.size.y, nz), widthScale = ev, heightScale = ev, color = Color.white, lightmapColor = Color.white });
                        puestos++;
                    }
                // todos los árboles vuelven a apoyarse en el suelo (los de las orillas quedaban colgando con la altura vieja)
                for (int i = 0; i < lista.Count; i++) { var ti = lista[i]; float tx = o.x + ti.position.x * td.size.x, tz = o.z + ti.position.z * td.size.z;
                    if (tx < r.xMin - m || tx > r.xMax + m || tz < r.yMin - m || tz > r.yMax + m) continue;
                    ti.position.y = td.GetInterpolatedHeight(ti.position.x, ti.position.z) / td.size.y; lista[i] = ti; }
                td.treeInstances = lista.ToArray(); t.Flush();
                if (t != nuevo) { RefrescarHierbaATG(t); RefrescarPasto(td, r.xMin - m - o.x, r.yMin - m - o.z, r.xMax + m - o.x, r.yMax + m - o.z); }
            }
            // objetos del juego que quedaron en el aire sobre el llano: abajo (los enterrados no se ven)
            int bajados = 0;
            foreach (var mr in Object.FindObjectsOfType<MeshRenderer>())
            {
                if (mr == null || !mr.enabled || mr.name.StartsWith("coop_")) continue;
                var b = mr.bounds; if (!LlanoEsteZona.Contains(new Vector2(b.center.x, b.center.z)) || Mathf.Max(b.size.x, b.size.z) > 20f) continue;
                if (b.min.y > LlanoEsteAltura + 0.4f) { mr.enabled = false; foreach (var c in mr.GetComponentsInChildren<Collider>()) c.enabled = false; bajados++; }
            }
            plugin.Log("Llano este: " + muestras + " muestras a " + LlanoEsteAltura + " m, " + quitados + " árboles quitados, " + puestos + " puestos, " + bajados + " objetos en el aire ocultos");
        }
    }
}

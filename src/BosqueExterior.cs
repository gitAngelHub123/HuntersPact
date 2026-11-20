using System.Collections.Generic;
using UnityEngine;

namespace WitchHuntCoop
{
    /// Bosque exterior (pedido 2026-11-17): más allá de las paredes invisibles de la zona nueva se veía el vacío ("el mapa roto").
    /// Se ponen franjas de terreno de 700 m alrededor (oeste, este, norte y la esquina sureste donde no llega el bosque original):
    /// arrancan a la altura del borde de la zona nueva y suben en lomas cubiertas de bosque denso, como si el bosque no terminara nunca.
    /// Sin colisión (nadie puede llegar: están detrás de las paredes) y se borran al quitar la expansión.
    public partial class Mundo
    {
        const float AnchoFranja = 700f;   // 2026-11-18: se probó 200 m y se veía el borde vacío; vuelve a 700 (pedido del usuario)
        readonly List<GameObject> bosqueExterior = new List<GameObject>();
        readonly List<TerrainData> datosExterior = new List<TerrainData>();

        void CrearBosqueExterior(Terrain orig, Terrain nuevoT, System.Random rng)
        {
            Vector3 b = nuevoT.transform.position; var dn = nuevoT.terrainData;
            float W = dn.size.x, F = dn.size.z, B = AnchoFranja;
            float bordeEsteOrig = orig.transform.position.x + orig.terrainData.size.x;
            var franjas = new List<Rect>
            {
                new Rect(b.x - B, b.z - 300f, B, F + B + 300f),            // oeste
                new Rect(b.x + W, b.z - 300f, B, F + B + 300f),            // este
                new Rect(b.x - B, b.z + F, W + 2f * B, B),                 // norte
            };
            if (b.x + W > bordeEsteOrig + 1f) franjas.Add(new Rect(bordeEsteOrig, b.z - B, b.x + W - bordeEsteOrig, B));   // sureste (sin bosque original)
            // alrededor del MAPA ORIGINAL (pedido 2026-11-18): oeste, sur y este, igual que en la zona nueva
            Vector3 o = orig.transform.position; float OW = orig.terrainData.size.x, OF = orig.terrainData.size.z;
            float zTope = b.z - 300f;   // hasta donde empiezan las franjas oeste/este de la zona nueva
            if (zTope > o.z) franjas.Add(new Rect(o.x - B, o.z, B, zTope - o.z));                     // oeste del original
            franjas.Add(new Rect(o.x - B, o.z - B, OW + 2f * B, B));                                  // sur del original (con esquinas)
            float zTopeE = b.x + W > bordeEsteOrig + 1f ? b.z - B : zTope;
            if (zTopeE > o.z) franjas.Add(new Rect(o.x + OW, o.z, B, zTopeE - o.z));                 // este del original
            int arbolesTot = 0;
            foreach (var r in franjas) arbolesTot += Franja(orig, nuevoT, r, rng);
            plugin.Log("Bosque exterior: " + franjas.Count + " franjas de " + B + " m con " + arbolesTot + " árboles");
        }

        int Franja(Terrain orig, Terrain nuevoT, Rect r, System.Random rng)
        {
            var dn = nuevoT.terrainData; Vector3 b = nuevoT.transform.position;
            const float AltoT = 320f;
            int res = Mathf.Max(r.width, r.height) > 2000f ? 513 : 257;
            var td = new TerrainData { heightmapResolution = res };
            td.size = new Vector3(r.width, AltoT, r.height);
            float sem = (float)rng.NextDouble() * 1000f;
            var h = new float[res, res];
            for (int zi = 0; zi < res; zi++)
                for (int xi = 0; xi < res; xi++)
                {
                    float x = r.x + xi * r.width / (res - 1), z = r.y + zi * r.height / (res - 1);
                    float lomas = (Mathf.PerlinNoise(sem + x * 0.004f, sem + z * 0.004f) - 0.3f) * 70f + (Mathf.PerlinNoise(sem * 2f + x * 0.015f, z * 0.015f) - 0.5f) * 14f + Mathf.Pow(Mathf.PerlinNoise(sem * 5f + x * 0.03f, sem + z * 0.03f), 3f) * 18f;   // + crestas rocosas
                    // en BAJADA desde la pared (pedido 2026-11-17), partiendo de la altura del borde de CADA terreno jugable (original y zona nueva)
                    // y mezclando las dos según la distancia. Antes se tomaba solo el más cercano: donde se cambiaba de uno a otro quedaba
                    // un escalón de ~55 m (la "grieta" en la esquina sureste, 2026-11-18).
                    float y = AlturaExterior(orig, nuevoT, x, z, lomas);
                    // las minas se arman aquí afuera (al oeste): el terreno se hunde bajo ellas para no atravesar los túneles (2026-11-18)
                    for (int nm = 0; nm < 3; nm++)
                    {
                        Rect zm = ZonaMina(orig.transform.position, nm); float piso = OrigenMina(orig.transform.position, nm).y;
                        float dx = Mathf.Max(0f, Mathf.Max(zm.xMin - x, x - zm.xMax)), dz = Mathf.Max(0f, Mathf.Max(zm.yMin - z, z - zm.yMax));
                        float dR = Mathf.Sqrt(dx * dx + dz * dz);
                        y = Mathf.Min(y, piso - 15f + dR * 0.7f);
                    }
                    h[zi, xi] = Mathf.Clamp01((y - b.y) / AltoT);
                }
            td.SetHeights(0, 0, h);
            // suelo: las mismas capas de la zona nueva; tierra del bosque con manchas
            td.splatPrototypes = dn.splatPrototypes;
            const int ar = 128; td.alphamapResolution = ar; td.baseMapResolution = 128;
            int capas = dn.splatPrototypes.Length;
            var am = new float[ar, ar, capas];
            for (int zi = 0; zi < ar; zi++)
                for (int xi = 0; xi < ar; xi++)
                {
                    // suelo de bosque con manchas de tierra y roca en las laderas empinadas (antes casi de un solo color)
                    float m = Mathf.PerlinNoise(sem + xi * 0.09f, sem + zi * 0.09f), m2 = Mathf.PerlinNoise(sem * 3f + xi * 0.21f, zi * 0.21f);
                    float pend = td.GetSteepness(xi / (float)(ar - 1), zi / (float)(ar - 1)), roca = Mathf.SmoothStep(0f, 1f, (pend - 18f) / 20f);
                    int cRoca = Mathf.Min(2, capas - 1);
                    am[zi, xi, 0] = (1f - m * 0.55f) * (1f - roca) * (0.7f + 0.3f * m2); if (capas > 1) am[zi, xi, 1] = m * 0.55f * (1f - roca) + (1f - (0.7f + 0.3f * m2)) * (1f - roca);
                    am[zi, xi, cRoca] += roca;
                }
            td.SetAlphamaps(0, 0, am);
            // bosque denso: cuadrícula de ~16 m con desvío; los de más lejos, más grandes (tapan el horizonte)
            td.treePrototypes = dn.treePrototypes;
            var arboles = new List<TreeInstance>();
            for (float gz = 6f; gz < r.height - 4f; gz += 22.6f)   // densidad de antes (2026-11-18: la mitad quedó peor, se volvió atrás)
                for (float gx = 6f; gx < r.width - 4f; gx += 22.6f)
                {
                    float x = Mathf.Clamp(gx + ((float)rng.NextDouble() - 0.5f) * 13f, 2f, r.width - 2f), z = Mathf.Clamp(gz + ((float)rng.NextDouble() - 0.5f) * 13f, 2f, r.height - 2f);
                    float wx = r.x + x, wz = r.y + z;
                    float bordeA, dist; BordeCercano(orig, nuevoT, wx, wz, out bordeA, out dist);
                    if (dist < 8f) continue;   // pegado a la pared no (se vería cortado)
                    bool enMina = false;
                    for (int nm = 0; nm < 3; nm++) { Rect zm = ZonaMina(orig.transform.position, nm); if (wx > zm.xMin - 45f && wx < zm.xMax + 45f && wz > zm.yMin - 45f && wz < zm.yMax + 45f) enMina = true; }
                    if (enMina) continue;   // ni árboles ni ramas dentro de los túneles
                    float ev = 1.8f + (float)rng.NextDouble() * 1.6f + Mathf.Clamp01(dist / AnchoFranja) * 1.6f;
                    arboles.Add(new TreeInstance { prototypeIndex = rng.Next(td.treePrototypes.Length), position = new Vector3(x / r.width, 0f, z / r.height), widthScale = ev, heightScale = ev, color = Color.white, lightmapColor = Color.white });
                }
            td.treeInstances = arboles.ToArray();
            td.RefreshPrototypes();
            var ta = td.treeInstances;
            for (int i = 0; i < ta.Length; i++) ta[i].position.y = td.GetInterpolatedHeight(ta[i].position.x, ta[i].position.z) / AltoT;
            td.treeInstances = ta;

            var go = Terrain.CreateTerrainGameObject(td);
            { var tc = go.GetComponent<TerrainCollider>(); if (tc != null) Object.Destroy(tc); }
            go.name = "coop_bosque_exterior";
            go.transform.position = new Vector3(r.x, b.y, r.y);
            var te = go.GetComponent<Terrain>();
            te.materialType = nuevoT.materialType; te.materialTemplate = nuevoT.materialTemplate;
            te.basemapDistance = nuevoT.basemapDistance; te.heightmapPixelError = 8f;
            te.treeDistance = nuevoT.treeDistance; te.treeBillboardDistance = 250f; te.treeCrossFadeLength = nuevoT.treeCrossFadeLength;
            te.treeMaximumFullLODCount = 60; te.detailObjectDistance = 0f;
            te.castShadows = false; te.reflectionProbeUsage = nuevoT.reflectionProbeUsage;
            go.layer = nuevoT.gameObject.layer;
            bosqueExterior.Add(go); datosExterior.Add(td);
            return arboles.Count;
        }

        static float AlturaExterior(Terrain orig, Terrain nuevoT, float x, float z, float lomas)
        {
            float suma = 0f, pesos = 0f;
            foreach (var t in new[] { orig, nuevoT })
            {
                if (t == null) continue;
                Vector3 p = t.transform.position, sz = t.terrainData.size;
                float cx = Mathf.Clamp(x, p.x, p.x + sz.x), cz = Mathf.Clamp(z, p.z, p.z + sz.z);
                float d = Mathf.Sqrt((x - cx) * (x - cx) + (z - cz) * (z - cz));
                float borde = t.SampleHeight(new Vector3(cx, 0f, cz)) + p.y;
                float tt = Mathf.Clamp01(d / AnchoFranja);
                float yi = borde + Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(d / 60f)) * (lomas * 0.35f * Mathf.SmoothStep(0f, 1f, tt * 1.5f) - 70f * Mathf.SmoothStep(0f, 1f, tt));
                if (d < 0.01f) return yi;   // pegado al borde: exactamente su altura
                float w = 1f / Mathf.Pow(d + 5f, 2.5f);
                suma += yi * w; pesos += w;
            }
            return pesos > 0f ? suma / pesos : 0f;
        }

        /// Altura del borde y distancia al terreno jugable más cercano (original o zona nueva).
        static void BordeCercano(Terrain orig, Terrain nuevoT, float x, float z, out float borde, out float dist)
        {
            borde = 0f; dist = float.MaxValue;
            foreach (var t in new[] { orig, nuevoT })
            {
                if (t == null) continue;
                Vector3 p = t.transform.position, sz = t.terrainData.size;
                float cx = Mathf.Clamp(x, p.x, p.x + sz.x), cz = Mathf.Clamp(z, p.z, p.z + sz.z);
                float d = Mathf.Sqrt((x - cx) * (x - cx) + (z - cz) * (z - cz));
                if (d < dist) { dist = d; borde = t.SampleHeight(new Vector3(cx, 0f, cz)) + p.y; }
            }
        }

        void QuitarBosqueExterior()
        {
            foreach (var g in bosqueExterior) if (g != null) Object.Destroy(g);
            foreach (var d in datosExterior) if (d != null) Object.Destroy(d);
            bosqueExterior.Clear(); datosExterior.Clear();
        }
    }
}

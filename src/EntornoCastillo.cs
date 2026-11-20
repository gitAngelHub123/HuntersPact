using System.Collections.Generic;
using UnityEngine;

namespace WitchHuntCoop
{
    /// Entorno del castillo (pedido 2026-11-17: "el castillo allí está solo"): una muralla exterior con torres que rodea la colina a media
    /// altura (radio 180 m, pasa justo por la puerta con torreones de la cima de la escalinata) y, en la ladera sureste dentro de la muralla,
    /// un cementerio vallado con lápidas, cruces del juego, huesos, faroles y una capilla en ruinas sobre una terraza de piedra.
    /// Muralla y lápidas son mallas combinadas (pocas piezas para dibujar). Coordenadas del terreno norte (las de "pa", el laberinto).
    public partial class Mundo
    {
        const float MurallaR = 180f, MurallaGrosor = 2.4f, MurallaAlto = 8f;

        /// Caja girada (yaw y una inclinación opcional) para mallas combinadas.
        static void CajaRot(List<Vector3> v, List<Vector2> uv, List<int> tr, Vector3 c, Vector3 s, Quaternion q, float tile)
        {
            int k = v.Count;
            Caja6(v, uv, tr, Vector3.zero, s, tile);
            for (int i = k; i < v.Count; i++) v[i] = c + q * v[i];
        }

        void EntornoCastillo(Transform pa, TerrainData dn)
        {
            MaterialesGrandeza();
            MurallaExterior(pa, dn);
            Cementerio(pa, dn);
            try { JardinGrande(pa, dn); } catch (System.Exception e) { plugin.Log("Castillo: error en el jardín " + e); }
        }

        // ---------------- Jardín (pedido 2026-11-17: "añade un jardín enorme" detrás del castillo) ----------------

        static Material matFlorRoja, matFlorBlanca, matFlorMorada, matAguaJ, matGrava;
        static Material MatColor(ref Material m, string n, Color c, float brillo = 0.1f) { if (m == null) { m = new Material(Shader.Find("Standard")) { name = n, color = c }; m.SetFloat("_Glossiness", brillo); } return m; }

        /// Jardín formal en la cima detrás del castillo (z del terreno 955–1018, entre la Torre del Homenaje y el borde de la meseta):
        /// paseos de grava en cruz con una fuente de tres tazas en el centro, cuatro parterres de setos bajos con flores, topiarios en el
        /// paseo principal, estatuas, bancos, faroles y un murete con pilares alrededor. Setos, flores y paseos son mallas combinadas.
        void Jardin(Transform pa, TerrainData dn)
        {
            MaterialSeto();
            Vector2 c = CentroMonte;
            float cx = CastilloX, z0 = 952f, z1 = 1018f, zc = (z0 + z1) / 2f, semiX = 92f;
            System.Func<float, float, bool> dentro = (x, z) => (x - c.x) * (x - c.x) + (z - c.y) * (z - c.y) < 113f * 113f && z > z0 && z < z1 && Mathf.Abs(x - cx) < semiX;
            var vs = new List<Vector3>(); var us = new List<Vector2>(); var ts = new List<int>();     // setos
            var vg = new List<Vector3>(); var ug = new List<Vector2>(); var tg = new List<int>();     // grava
            var vp = new List<Vector3>(); var up = new List<Vector2>(); var tp = new List<int>();     // piedra (murete, bordes, bancos)
            var vfr = new List<Vector3>(); var ufr = new List<Vector2>(); var tfr = new List<int>();
            var vfb = new List<Vector3>(); var ufb = new List<Vector2>(); var tfb = new List<int>();
            var vfm = new List<Vector3>(); var ufm = new List<Vector2>(); var tfm = new List<int>();
            var rng = new System.Random(7717);
            System.Func<float, float, float> suelo = (x, z) => SueloT(dn, x, z);
            // paseos: eje principal (norte-sur), eje cruzado y anillo de la fuente
            for (float z = z0 + 1f; z < z1 - 1f; z += 2f) if (dentro(cx, z)) CajaRot(vg, ug, tg, new Vector3(cx, suelo(cx, z) + 0.03f, z), new Vector3(5f, 0.08f, 2.05f), Quaternion.identity, 2f);
            for (float x = cx - semiX + 2f; x < cx + semiX - 2f; x += 2f) if (dentro(x, zc)) CajaRot(vg, ug, tg, new Vector3(x, suelo(x, zc) + 0.03f, zc), new Vector3(2.05f, 0.08f, 4f), Quaternion.identity, 2f);
            for (int i = 0; i < 36; i++)
            {
                float a = i * 10f * Mathf.Deg2Rad; float x = cx + Mathf.Cos(a) * 9.5f, z = zc + Mathf.Sin(a) * 9.5f;
                CajaRot(vg, ug, tg, new Vector3(x, suelo(x, z) + 0.035f, z), new Vector3(3.6f, 0.08f, 1.9f), Quaternion.Euler(0f, -i * 10f + 90f, 0f), 2f);
            }
            // parterres: en cada cuadrante, rectángulos de seto bajo (0,9 m) con un rombo interior y flores en hileras
            int setos = 0, flores = 0;
            foreach (var sx in new[] { -1f, 1f }) foreach (var sz in new[] { -1f, 1f })
                for (int k = 0; k < 3; k++)
                {
                    float ax = cx + sx * (14f + k * 24f), az = zc + sz * 15f, w = 18f, d = 22f;
                    if (!dentro(ax - w / 2f, az) || !dentro(ax + w / 2f, az) || !dentro(ax, az - d / 2f * sz) ) continue;
                    float g = suelo(ax, az);
                    foreach (var e in new[] { -1f, 1f })
                    {
                        CajaRot(vs, us, ts, new Vector3(ax, g + 0.45f, az + e * d / 2f), new Vector3(w, 0.9f, 0.7f), Quaternion.identity, 2f);
                        CajaRot(vs, us, ts, new Vector3(ax + e * w / 2f, g + 0.45f, az), new Vector3(0.7f, 0.9f, d), Quaternion.identity, 2f);
                    }
                    for (int r = 0; r < 4; r++)   // rombo interior
                    {
                        float ang = 45f + r * 90f; Vector3 m = new Vector3(ax + Mathf.Cos(ang * Mathf.Deg2Rad) * 3.4f, g + 0.3f, az + Mathf.Sin(ang * Mathf.Deg2Rad) * 4.2f);
                        CajaRot(vs, us, ts, m, new Vector3(6.8f, 0.6f, 0.5f), Quaternion.Euler(0f, -ang + 90f + 45f, 0f), 2f);
                    }
                    setos++;
                    // flores en hileras dentro del rectángulo (fuera del rombo)
                    for (float fx = -w / 2f + 1.2f; fx < w / 2f - 1f; fx += 0.9f)
                        for (float fz = -d / 2f + 1.2f; fz < d / 2f - 1f; fz += 0.9f)
                        {
                            if (Mathf.Abs(fx) / 4.2f + Mathf.Abs(fz) / 5.2f < 1.1f) continue;   // dentro del rombo: césped
                            float px = ax + fx + ((float)rng.NextDouble() - 0.5f) * 0.3f, pz = az + fz + ((float)rng.NextDouble() - 0.5f) * 0.3f, gy = suelo(px, pz);
                            int tipo = (Mathf.Abs(fz) > d / 2f - 3f || Mathf.Abs(fx) > w / 2f - 3f) ? 0 : (int)((fx + fz) * 0.5f + 100f) % 2 + 1;
                            var lv = tipo == 0 ? vfr : tipo == 1 ? vfb : vfm; var lu = tipo == 0 ? ufr : tipo == 1 ? ufb : ufm; var lt = tipo == 0 ? tfr : tipo == 1 ? tfb : tfm;
                            float hf = 0.25f + (float)rng.NextDouble() * 0.2f;
                            CajaRot(lv, lu, lt, new Vector3(px, gy + hf, pz), new Vector3(0.22f, 0.16f, 0.22f), Quaternion.Euler(0f, (float)rng.NextDouble() * 90f, 0f), 1f);
                            CajaRot(vs, us, ts, new Vector3(px, gy + hf / 2f, pz), new Vector3(0.05f, hf, 0.05f), Quaternion.identity, 1f);   // tallo
                            flores++;
                        }
                }
            // topiarios (bolas de seto sobre tronco) a los dos lados del paseo principal
            for (float z = z0 + 6f; z < z1 - 4f; z += 8f)
            {
                if (Mathf.Abs(z - zc) < 13f) continue;
                foreach (var sx in new[] { -1f, 1f })
                {
                    float x = cx + sx * 4.2f; if (!dentro(x, z)) continue; float g = suelo(x, z);
                    CajaRot(vs, us, ts, new Vector3(x, g + 0.6f, z), new Vector3(0.9f, 1.2f, 0.9f), Quaternion.identity, 2f);   // maceta de seto
                    Pz(pa, "coop_jardin_topiario", PrimitiveType.Sphere, new Vector3(x, g + 2.3f, z), new Vector3(1.9f, 1.9f, 1.9f), 0f, matSeto, false);
                    Pz(pa, "coop_jardin_topiario", PrimitiveType.Sphere, new Vector3(x, g + 3.6f, z), new Vector3(1.1f, 1.1f, 1.1f), 0f, matSeto, false);
                }
            }
            // fuente de tres tazas con agua
            {
                float g = suelo(cx, zc);
                Pz(pa, "coop_fuente_pila", PrimitiveType.Cylinder, new Vector3(cx, g + 0.4f, zc), new Vector3(11f, 0.4f, 11f), 0f, matPiedra);
                Pz(pa, "coop_fuente_agua", PrimitiveType.Cylinder, new Vector3(cx, g + 0.72f, zc), new Vector3(10.2f, 0.02f, 10.2f), 0f, MatColor(ref matAguaJ, "coop_agua_fuente", new Color(0.12f, 0.18f, 0.24f), 0.95f), false);
                Pz(pa, "coop_fuente_fuste", PrimitiveType.Cylinder, new Vector3(cx, g + 1.9f, zc), new Vector3(1.1f, 1.6f, 1.1f), 0f, matPiedra);
                Pz(pa, "coop_fuente_taza1", PrimitiveType.Cylinder, new Vector3(cx, g + 2.4f, zc), new Vector3(4.6f, 0.18f, 4.6f), 0f, matPiedra, false);
                Pz(pa, "coop_fuente_agua1", PrimitiveType.Cylinder, new Vector3(cx, g + 2.56f, zc), new Vector3(4.2f, 0.02f, 4.2f), 0f, matAguaJ, false);
                Pz(pa, "coop_fuente_fuste2", PrimitiveType.Cylinder, new Vector3(cx, g + 3.3f, zc), new Vector3(0.6f, 0.8f, 0.6f), 0f, matPiedra, false);
                Pz(pa, "coop_fuente_taza2", PrimitiveType.Cylinder, new Vector3(cx, g + 3.9f, zc), new Vector3(2.4f, 0.14f, 2.4f), 0f, matPiedra, false);
                Pz(pa, "coop_fuente_remate", PrimitiveType.Sphere, new Vector3(cx, g + 4.4f, zc), new Vector3(0.8f, 0.8f, 0.8f), 0f, matPiedraOscura, false);
            }
            // estatuas en los extremos del eje cruzado y bancos junto a la fuente
            foreach (var sx in new[] { -1f, 1f })
            {
                float x = cx + sx * (semiX - 6f); if (dentro(x, zc)) Estatua(pa, new Vector3(x, suelo(x, zc), zc), sx > 0 ? -90f : 90f);
                foreach (var sz in new[] { -1f, 1f })
                {
                    float bx = cx + sx * 7f, bz = zc + sz * 7f, g = suelo(bx, bz), yaw = Mathf.Atan2(cx - bx, zc - bz) * Mathf.Rad2Deg;
                    var q = Quaternion.Euler(0f, yaw, 0f);
                    CajaRot(vp, up, tp, new Vector3(bx, g + 0.45f, bz), new Vector3(2.2f, 0.12f, 0.6f), q, 1.5f);
                    foreach (var e in new[] { -0.85f, 0.85f }) CajaRot(vp, up, tp, new Vector3(bx, g + 0.2f, bz) + q * new Vector3(e, 0f, 0f), new Vector3(0.25f, 0.45f, 0.5f), q, 1.5f);
                }
            }
            // murete con pilares alrededor del jardín (abierto en el lado del castillo y en el eje)
            for (float x = cx - semiX; x < cx + semiX; x += 4f)
            {
                if (Mathf.Abs(x + 2f - cx) < 4f) continue;
                if (!dentro(x + 2f, z1 - 1.5f)) continue;
                float g = suelo(x + 2f, z1 - 0.5f);
                CajaRot(vp, up, tp, new Vector3(x + 2f, g + 0.5f, z1 - 0.5f), new Vector3(4.05f, 1.4f, 0.6f), Quaternion.identity, 2f);
                CajaRot(vp, up, tp, new Vector3(x, g + 0.8f, z1 - 0.5f), new Vector3(0.8f, 2f, 0.8f), Quaternion.identity, 2f);
            }
            var mSet = MallaDe(pa, "coop_jardin_setos", vs, us, ts, matSeto); mSet.AddComponent<MeshCollider>().sharedMesh = mSet.GetComponent<MeshFilter>().sharedMesh;
            MallaDe(pa, "coop_jardin_grava", vg, ug, tg, matAdoquin);   // (antes un color liso: de noche se veía como una línea blanca)
            var mPie = MallaDe(pa, "coop_jardin_piedra", vp, up, tp, matPiedra); mPie.AddComponent<MeshCollider>().sharedMesh = mPie.GetComponent<MeshFilter>().sharedMesh;
            MallaDe(pa, "coop_jardin_flores_rojas", vfr, ufr, tfr, MatColor(ref matFlorRoja, "coop_flor_roja", new Color(0.6f, 0.05f, 0.08f)));
            MallaDe(pa, "coop_jardin_flores_blancas", vfb, ufb, tfb, MatColor(ref matFlorBlanca, "coop_flor_blanca", new Color(0.85f, 0.85f, 0.8f)));
            MallaDe(pa, "coop_jardin_flores_moradas", vfm, ufm, tfm, MatColor(ref matFlorMorada, "coop_flor_morada", new Color(0.35f, 0.1f, 0.5f)));
            foreach (var sx in new[] { -1f, 1f }) foreach (var z in new[] { z0 + 8f, zc + 12f, z1 - 8f }) { float x = cx + sx * 7f; if (dentro(x, z)) Farol2(pa, new Vector3(x, suelo(x, z), z)); }
            plugin.Log("Castillo: jardín con " + setos + " parterres, " + flores + " flores, fuente, topiarios, estatuas y bancos");
        }

        // ---------------- Muralla exterior ----------------

        void MurallaExterior(Transform pa, TerrainData dn)
        {
            Vector2 c = CentroMonte;
            float semiPuerta = EscAncho / 2f + 2.6f + 1.3f;                    // hasta el borde exterior de los torreones de la escalinata
            float hueco = Mathf.Asin(semiPuerta / MurallaR) * Mathf.Rad2Deg;   // medio ángulo de la puerta (al sur, 270°)
            float desde = 270f + hueco, hasta = 270f + 360f - hueco;
            const float paso = 1.5f;                                           // tramos de ~4,7 m
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var tr = new List<int>();
            int n = Mathf.CeilToInt((hasta - desde) / paso), tramos = 0;
            for (int i = 0; i < n; i++)
            {
                float a0 = desde + (hasta - desde) * i / n, a1 = desde + (hasta - desde) * (i + 1) / n, am = (a0 + a1) / 2f;
                Vector2 p0 = c + Dir2(a0) * MurallaR, p1 = c + Dir2(a1) * MurallaR, pm = c + Dir2(am) * MurallaR;
                float g0 = SueloT(dn, p0.x, p0.y), g1 = SueloT(dn, p1.x, p1.y), gm = SueloT(dn, pm.x, pm.y);
                float bajo = Mathf.Min(g0, Mathf.Min(g1, gm)) - 3f, alto = Mathf.Max(g0, Mathf.Max(g1, gm)) + MurallaAlto;
                float largo = Vector2.Distance(p0, p1) + 0.35f;
                var q = Quaternion.Euler(0f, -am + 90f, 0f);   // eje x local a lo largo del arco
                CajaRot(v, uv, tr, new Vector3(pm.x, (bajo + alto) / 2f, pm.y), new Vector3(largo, alto - bajo, MurallaGrosor), q, 3f);
                // almenas sobre el lado de afuera, una sí y una no
                if (i % 2 == 0)
                {
                    Vector2 fuera = Dir2(am) * (MurallaGrosor / 2f - 0.4f);
                    CajaRot(v, uv, tr, new Vector3(pm.x + fuera.x, alto + 0.75f, pm.y + fuera.y), new Vector3(largo * 0.55f, 1.5f, 0.8f), q, 3f);
                }
                tramos++;
            }
            var muro = MallaDe(pa, "coop_muralla_exterior", v, uv, tr, matPiedra);
            muro.AddComponent<MeshCollider>().sharedMesh = muro.GetComponent<MeshFilter>().sharedMesh;
            // torres cada 30° (sin pisar la puerta: ahí están los torreones de la escalinata)
            int torres = 0;
            for (float a = 300f; a < 600f; a += 30f)
            {
                if (Mathf.Abs(Mathf.DeltaAngle(a, 270f)) < 12f) continue;
                Vector2 p = c + Dir2(a) * MurallaR;
                float g = SueloT(dn, p.x, p.y), top = g + MurallaAlto + 6f;
                Pz(pa, "coop_muralla_torre", PrimitiveType.Cylinder, new Vector3(p.x, (g - 3f + top) / 2f, p.y), new Vector3(7.2f, (top - g + 3f) / 2f, 7.2f), 0f, matPiedra);
                Pz(pa, "coop_muralla_torre_cornisa", PrimitiveType.Cylinder, new Vector3(p.x, top + 0.3f, p.y), new Vector3(8.4f, 0.3f, 8.4f), 0f, matPiedraOscura, false);
                for (int k = 0; k < 8; k++)
                {
                    float ak = k * 45f * Mathf.Deg2Rad;
                    Pz(pa, "coop_muralla_torre_almena", PrimitiveType.Cube, new Vector3(p.x + Mathf.Cos(ak) * 3.7f, top + 1.3f, p.y + Mathf.Sin(ak) * 3.7f), new Vector3(1.2f, 1.6f, 1.2f), -k * 45f, matPiedra, false);
                }
                ConoMat(pa, "coop_muralla_torre_techo", new Vector3(p.x, top + 2.2f, p.y), 4.6f, 7f, matPiedraOscura);
                torres++;
            }
            plugin.Log("Castillo: muralla exterior de " + tramos + " tramos (radio " + MurallaR + " m) con " + torres + " torres");
        }

        static Vector2 Dir2(float grados) { float r = grados * Mathf.Deg2Rad; return new Vector2(Mathf.Cos(r), Mathf.Sin(r)); }

        // ---------------- Cementerio ----------------

        /// Objeto del juego en coordenadas del terreno norte (se encola como los muebles del castillo, a tamaño natural).
        void PropTerreno(Transform pa, string tipo, Vector3 p, float yaw)
        {
            if (castilloRaiz == null) return;
            var l = castilloRaiz.transform.InverseTransformPoint(pa.TransformPoint(p));
            Prop(castilloRaiz.transform, tipo, l.x, l.y, l.z, yaw, 1f / CastilloEscala);
        }

        void Cementerio(Transform pa, TerrainData dn)
        {
            Vector2 c = CentroMonte;
            const float A0 = 290f, A1 = 330f, R0 = 130f, R1 = 172f;      // sector de la ladera sureste, entre la cima y la muralla
            var rng = new System.Random(6131);
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var tr = new List<int>();
            var vt = new List<Vector3>(); var uvt = new List<Vector2>(); var trt = new List<int>();   // montículos de tierra
            var vm = new List<Vector3>(); var uvm = new List<Vector2>(); var trm = new List<int>();   // cruces de madera
            Vector2 capilla = c + Dir2(310f) * 151f;
            int lapidas = 0, cruces = 0;
            for (float r = R0 + 5f; r < R1 - 4f; r += 7f)
            {
                float arco = (A1 - A0) * Mathf.Deg2Rad * r, pasoA = 4.2f / r * Mathf.Rad2Deg;
                for (float a = A0 + 2.5f; a < A1 - 2f; a += pasoA)
                {
                    Vector2 p = c + Dir2(a) * r;
                    if (Vector2.Distance(p, capilla) < 13f) continue;                       // la capilla y su terraza
                    if (rng.NextDouble() < 0.18) continue;                                  // huecos: tumbas perdidas
                    Vector2 fuera = Dir2(a);                                                // ladera abajo
                    float g = SueloT(dn, p.x, p.y);
                    float yaw = Mathf.Atan2(fuera.x, fuera.y) * Mathf.Rad2Deg + ((float)rng.NextDouble() - 0.5f) * 14f;
                    var q = Quaternion.Euler(((float)rng.NextDouble() - 0.5f) * 12f, yaw, ((float)rng.NextDouble() - 0.5f) * 10f);   // algunas torcidas por los años
                    // montículo delante de la lápida (ladera abajo)
                    Vector2 pm = p + fuera * 1.2f;
                    CajaRot(vt, uvt, trt, new Vector3(pm.x, SueloT(dn, pm.x, pm.y) - 0.05f, pm.y), new Vector3(1.0f, 0.35f, 2.1f), Quaternion.Euler(0f, yaw, 0f), 1.5f);
                    if (rng.NextDouble() < 0.3)
                    {
                        // cruz de madera (antes: cruces copiadas del juego, que no siempre tienen original del que copiar)
                        float hc = 1.7f + (float)rng.NextDouble() * 0.5f;
                        Vector3 pie = new Vector3(p.x, g - 0.4f, p.y);
                        CajaRot(vm, uvm, trm, pie + q * new Vector3(0f, (hc + 0.4f) / 2f, 0f), new Vector3(0.14f, hc + 0.4f, 0.12f), q, 0.8f);
                        CajaRot(vm, uvm, trm, pie + q * new Vector3(0f, hc * 0.78f + 0.4f, 0f), new Vector3(0.8f, 0.13f, 0.12f), q, 0.8f);
                        cruces++;
                        continue;
                    }
                    float alto = 0.9f + (float)rng.NextDouble() * 0.5f, ancho = 0.7f + (float)rng.NextDouble() * 0.25f;
                    CajaRot(v, uv, tr, new Vector3(p.x, g + alto / 2f - 0.35f, p.y), new Vector3(ancho, alto + 0.7f, 0.18f), q, 1.2f);
                    CajaRot(v, uv, tr, new Vector3(p.x, g - 0.3f, p.y), new Vector3(ancho + 0.25f, 0.3f, 0.4f), Quaternion.Euler(0f, yaw, 0f), 1.2f);   // basa
                    if (rng.NextDouble() < 0.5)   // remate: una cruz chica de piedra encima
                    {
                        Vector3 cima = new Vector3(p.x, g + alto + 0.05f, p.y);
                        CajaRot(v, uv, tr, cima + q * new Vector3(0f, 0.25f, 0f), new Vector3(0.1f, 0.6f, 0.1f), q, 1f);
                        CajaRot(v, uv, tr, cima + q * new Vector3(0f, 0.35f, 0f), new Vector3(0.4f, 0.1f, 0.1f), q, 1f);
                    }
                    lapidas++;
                }
            }
            var lap = MallaDe(pa, "coop_cementerio_lapidas", v, uv, tr, matPiedraOscura);
            lap.AddComponent<MeshCollider>().sharedMesh = lap.GetComponent<MeshFilter>().sharedMesh;
            MallaDe(pa, "coop_cementerio_tumbas", vt, uvt, trt, matTierraCementerio());
            var cm = MallaDe(pa, "coop_cementerio_cruces", vm, uvm, trm, matMadera2);
            cm.AddComponent<MeshCollider>().sharedMesh = cm.GetComponent<MeshFilter>().sharedMesh;
            VallaCementerio(pa, dn, c, A0, A1, R0, R1);
            CapillaRuinas(pa, dn, capilla, c);
            // huesos y calaveras entre las tumbas, y faroles en el camino de la puerta
            foreach (var a in new[] { 298f, 315f, 324f })
            {
                Vector2 p = c + Dir2(a) * (R0 + 18f + (float)rng.NextDouble() * 15f);
                // fosa abierta con una lápida caída al lado
                float g = SueloT(dn, p.x, p.y); float yw = (float)rng.NextDouble() * 360f;
                Pz(pa, "coop_cementerio_fosa", PrimitiveType.Cube, new Vector3(p.x, g + 0.01f, p.y), new Vector3(1.1f, 0.04f, 2.2f), yw, matNegroFosa(), false);
                Pz(pa, "coop_cementerio_tierra", PrimitiveType.Cube, new Vector3(p.x + 1.4f, g + 0.15f, p.y), new Vector3(0.9f, 0.5f, 1.8f), yw + 10f, matTierraCementerio(), false);
            }
            foreach (var r in new[] { 138f, 152f, 166f })
            {
                Vector2 p = c + Dir2(A0 + 1.2f) * r;
                Farol2(pa, new Vector3(p.x, SueloT(dn, p.x, p.y), p.y));
            }
            plugin.Log("Castillo: cementerio con " + lapidas + " lápidas, " + cruces + " cruces del juego, valla y capilla en ruinas");
        }

        static Material matNegroCem, matVelaC;
        Material matNegroFosa() { if (matNegroCem == null) matNegroCem = new Material(Shader.Find("Unlit/Color")) { name = "coop_fosa", color = new Color(0.02f, 0.015f, 0.01f) }; return matNegroCem; }
        Material matVelaCem() { if (matVelaC == null) matVelaC = new Material(Shader.Find("Standard")) { name = "coop_vela_cera", color = new Color(0.9f, 0.86f, 0.72f) }; return matVelaC; }
        static Material matTierraCem;
        Material matTierraCementerio()
        {
            if (matTierraCem != null) return matTierraCem;
            matTierraCem = new Material(matPiedraOscura) { name = "coop_tierra_tumba", color = new Color(0.32f, 0.26f, 0.2f) };
            return matTierraCem;
        }

        /// Valla de hierro: barrotes cada 0,35 m con punta, dos travesaños y pilares de piedra; puerta abierta del lado de la escalinata.
        void VallaCementerio(Transform pa, TerrainData dn, Vector2 c, float a0, float a1, float r0, float r1)
        {
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var tr = new List<int>();
            var vp = new List<Vector3>(); var uvp = new List<Vector2>(); var trp = new List<int>();
            System.Action<Vector2, Vector2, bool> tramo = (p0, p1, conPuerta) =>
            {
                float L = Vector2.Distance(p0, p1); Vector2 d = (p1 - p0) / L;
                float yaw = Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg - 90f;
                for (float s = 0f; s <= L; s += 0.35f)
                {
                    if (conPuerta && Mathf.Abs(s - L / 2f) < 1.8f) continue;
                    Vector2 p = p0 + d * s; float g = SueloT(dn, p.x, p.y);
                    CajaRot(v, uv, tr, new Vector3(p.x, g + 0.75f, p.y), new Vector3(0.05f, 1.9f, 0.05f), Quaternion.identity, 1f);
                    CajaRot(v, uv, tr, new Vector3(p.x, g + 1.75f, p.y), new Vector3(0.09f, 0.14f, 0.09f), Quaternion.Euler(0f, 45f, 45f), 1f);   // punta
                }
                for (float s = 0f; s < L; s += 2.4f)
                {
                    float s1 = Mathf.Min(L, s + 2.4f);
                    if (conPuerta && s1 > L / 2f - 1.8f && s < L / 2f + 1.8f) continue;
                    Vector2 pa0 = p0 + d * s, pa1 = p0 + d * s1, pm = (pa0 + pa1) / 2f;
                    float g0 = SueloT(dn, pa0.x, pa0.y), g1 = SueloT(dn, pa1.x, pa1.y);
                    float incl = Mathf.Atan2(g1 - g0, s1 - s) * Mathf.Rad2Deg;
                    foreach (var h in new[] { 0.35f, 1.45f })
                        CajaRot(v, uv, tr, new Vector3(pm.x, (g0 + g1) / 2f + h, pm.y), new Vector3(s1 - s + 0.05f, 0.06f, 0.06f), Quaternion.Euler(0f, yaw, incl), 1f);
                    float gp = SueloT(dn, pa0.x, pa0.y);
                    CajaRot(vp, uvp, trp, new Vector3(pa0.x, gp + 0.6f, pa0.y), new Vector3(0.45f, 2.6f, 0.45f), Quaternion.Euler(0f, yaw, 0f), 1.2f);   // pilar
                }
            };
            int N = 10;
            for (int i = 0; i < N; i++)
            {
                float aa = a0 + (a1 - a0) * i / N, ab = a0 + (a1 - a0) * (i + 1) / N;
                tramo(c + Dir2(aa) * r0, c + Dir2(ab) * r0, false);
                tramo(c + Dir2(aa) * r1, c + Dir2(ab) * r1, false);
            }
            tramo(c + Dir2(a0) * r0, c + Dir2(a0) * r1, true);    // lado de la escalinata, con la puerta
            tramo(c + Dir2(a1) * r0, c + Dir2(a1) * r1, false);
            var valla = MallaDe(pa, "coop_cementerio_valla", v, uv, tr, matHierroC);
            valla.AddComponent<MeshCollider>().sharedMesh = valla.GetComponent<MeshFilter>().sharedMesh;
            var pil = MallaDe(pa, "coop_cementerio_pilares", vp, uvp, trp, matPiedra);
            pil.AddComponent<MeshCollider>().sharedMesh = pil.GetComponent<MeshFilter>().sharedMesh;
        }

        /// Capilla en ruinas sobre una terraza de piedra nivelada en la ladera: muros rotos, puerta ojival mirando a la escalinata,
        /// ventanal vacío al fondo, altar con candelabros y columnas caídas.
        void CapillaRuinas(Transform pa, TerrainData dn, Vector2 p, Vector2 c)
        {
            // la puerta mira hacia la escalinata (x = EscX): el eje largo de la capilla va en esa dirección
            Vector2 haciaEsc = new Vector2(EscX - p.x, 0f).normalized; if (haciaEsc.sqrMagnitude < 0.5f) haciaEsc = Vector2.left;
            float yaw = Mathf.Atan2(-haciaEsc.x, -haciaEsc.y) * Mathf.Rad2Deg;   // +z local = lejos de la escalinata
            var rot = Quaternion.Euler(0f, yaw, 0f);
            const float AnchoC = 10f, LargoC = 15f;
            float gMax = float.MinValue, gMin = float.MaxValue;
            for (int i = -1; i <= 1; i++) for (int j = -1; j <= 1; j++)
            {
                Vector3 q = rot * new Vector3(i * (AnchoC / 2f + 1f), 0f, j * (LargoC / 2f + 1f));
                float g = SueloT(dn, p.x + q.x, p.y + q.z); gMax = Mathf.Max(gMax, g); gMin = Mathf.Min(gMin, g);
            }
            float piso = gMax + 0.3f;
            var raiz = new GameObject("coop_capilla"); raiz.transform.SetParent(pa, false);
            raiz.transform.localPosition = new Vector3(p.x, piso, p.y); raiz.transform.localRotation = rot;
            var t = raiz.transform;
            // terraza con muro de contención
            Pz(t, "coop_capilla_terraza", PrimitiveType.Cube, new Vector3(0f, -(piso - gMin + 1.5f) / 2f, 0f), new Vector3(AnchoC + 2f, piso - gMin + 1.5f, LargoC + 2f), 0f, matPiedra);
            Pz(t, "coop_capilla_suelo", PrimitiveType.Cube, new Vector3(0f, 0.02f, 0f), new Vector3(AnchoC - 1f, 0.06f, LargoC - 1f), 0f, matAdoquin, false);
            // muros laterales rotos (alturas desparejas)
            float[] alt = { 6.5f, 5.2f, 6.8f, 3.4f, 6.1f, 2.6f, 5.9f };
            foreach (var sx in new[] { -1f, 1f })
                for (int i = 0; i < alt.Length; i++)
                {
                    float h = sx > 0 ? alt[i] : alt[alt.Length - 1 - i] * 0.92f;
                    float z = -LargoC / 2f + (i + 0.5f) * LargoC / alt.Length;
                    Pz(t, "coop_capilla_muro", PrimitiveType.Cube, new Vector3(sx * AnchoC / 2f, h / 2f, z), new Vector3(0.9f, h, LargoC / alt.Length + 0.02f), 0f, matPiedra);
                }
            // fondo con ventanal vacío (arco ojival sin vidrio)
            Pz(t, "coop_capilla_fondo", PrimitiveType.Cube, new Vector3(-3.1f, 3.8f, LargoC / 2f), new Vector3(3.8f, 7.6f, 0.9f), 0f, matPiedra);
            Pz(t, "coop_capilla_fondo", PrimitiveType.Cube, new Vector3(3.1f, 3.8f, LargoC / 2f), new Vector3(3.8f, 7.6f, 0.9f), 0f, matPiedra);
            Pz(t, "coop_capilla_fondo", PrimitiveType.Cube, new Vector3(0f, 1.2f, LargoC / 2f), new Vector3(2.4f, 2.4f, 0.9f), 0f, matPiedra);
            ArcoOjival(t, "coop_capilla_ventanal", 0f, 4.6f, LargoC / 2f, 1.25f, 0.45f, 0.95f, 7, matPiedraOscura);
            // frente con la puerta ojival
            Pz(t, "coop_capilla_frente", PrimitiveType.Cube, new Vector3(-3.2f, 3f, -LargoC / 2f), new Vector3(3.6f, 6f, 0.9f), 0f, matPiedra);
            Pz(t, "coop_capilla_frente", PrimitiveType.Cube, new Vector3(3.2f, 2.2f, -LargoC / 2f), new Vector3(3.6f, 4.4f, 0.9f), 0f, matPiedra);
            ArcoOjival(t, "coop_capilla_puerta", 0f, 3.2f, -LargoC / 2f, 1.4f, 0.5f, 1.0f, 7, matPiedraOscura);
            foreach (var sx in new[] { -1.65f, 1.65f }) Pz(t, "coop_capilla_jamba", PrimitiveType.Cube, new Vector3(sx, 1.6f, -LargoC / 2f), new Vector3(0.5f, 3.2f, 1.0f), 0f, matPiedraOscura, false);
            // escalones de la terraza a la puerta
            for (int i = 0; i < 4; i++) Pz(t, "coop_capilla_escalon", PrimitiveType.Cube, new Vector3(0f, -0.3f - i * 0.3f, -LargoC / 2f - 1.3f - i * 0.45f), new Vector3(3.4f, 0.3f, 0.5f), 0f, matPiedra);
            // altar, candelabros y columnas caídas
            Pz(t, "coop_capilla_altar", PrimitiveType.Cube, new Vector3(0f, 0.55f, LargoC / 2f - 2f), new Vector3(2.6f, 1.1f, 1.2f), 0f, matPiedraOscura);
            Pz(t, "coop_capilla_altar_tapa", PrimitiveType.Cube, new Vector3(0f, 1.15f, LargoC / 2f - 2f), new Vector3(2.9f, 0.12f, 1.4f), 0f, matPiedra, false);
            var col1 = Pz(t, "coop_capilla_columna_caida", PrimitiveType.Cylinder, new Vector3(-2.2f, 0.45f, 0.5f), new Vector3(0.9f, 2.6f, 0.9f), 0f, matPiedra);
            col1.transform.localRotation = Quaternion.Euler(90f, 25f, 0f);
            Pz(t, "coop_capilla_columna", PrimitiveType.Cylinder, new Vector3(2.4f, 1.4f, 2.5f), new Vector3(0.9f, 1.4f, 0.9f), 0f, matPiedra);
            // velas encendidas sobre el altar (antes candelabros copiados del juego, que a veces no tienen original)
            foreach (var sx in new[] { -1.1f, -0.6f, 0.7f, 1.15f })
            {
                float hv = 0.25f + Mathf.Abs(sx) * 0.12f;
                Pz(t, "coop_capilla_vela", PrimitiveType.Cylinder, new Vector3(sx, 1.21f + hv / 2f, LargoC / 2f - 2f), new Vector3(0.09f, hv / 2f, 0.09f), 0f, matVelaCem(), false);
                Llama.Crear(t.TransformPoint(new Vector3(sx, 1.21f + hv + 0.05f, LargoC / 2f - 2f)), t, 0.18f, false, false);
            }
            var luzAltar = LuzCastillo(t, new Vector3(0f, 2.2f, LargoC / 2f - 3f), new Color(1f, 0.62f, 0.3f), 9f, 1.4f); luzAltar.gameObject.AddComponent<Titilar>();
            plugin.Log("Castillo: capilla en ruinas a " + Mathf.RoundToInt(Vector2.Distance(p, c)) + " m del centro de la colina, terraza de " + (piso - gMin).ToString("0.0") + " m");
        }
    }
}

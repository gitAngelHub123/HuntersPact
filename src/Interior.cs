// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using System.Collections.Generic;
using UnityEngine;

namespace WitchHuntCoop
{
    /// Interior del castillo (pedido 2026-11-12): todo conectado con pasillos y salas. El salón del trono tiene puertas a dos corredores que cruzan
    /// hasta las alas (cada ala con dos plantas unidas por una escalera: biblioteca/dormitorio a la izquierda, cocina/armería a la derecha) y una
    /// galería trasera que lleva a la Torre del Homenaje: una torre hueca de ~50 m con escalera de caracol, ventanas y una terraza con vistas.
    /// Los muebles son copias de los objetos del propio juego (estantes, camas, barriles, candelabros…); si falta alguno, se omite.
    public partial class Mundo
    {
        const float PisoC = 3.1f;                          // suelo del salón (y de todo el interior de planta baja)
        const float TorreX = 0f, TorreZ = 56f, TorreR = 12f, TorreMuro = 1.8f;
        const int TorrePasos = 168, TorrePasosVuelta = 40;
        const float TorreSubida = 0.28f;
        const float TorreAngIni = -75f;
        const float TorreBajadaIni = 150f, TorreBajadaFin = 257f;  // hueco del suelo sobre la bajada al sótano (grados): empieza en la esquina oeste del portón y sigue el caracol hacia abajo
        const float SotanoPuertaAng = 140f, SotanoFinAng = 52f;     // muro con la puerta de la bóveda y fondo de la bóveda (la bóveda va de 140° a 52°)

        Dictionary<string, GameObject> propOrig = new Dictionary<string, GameObject>();
        int propsPuestos, propsFaltan, propsHechos;
        GameObject bovedaPuerta;                                   // puerta de hierro de la bóveda (se abre con la llave del jefe de la mina)
        readonly List<Vector3> sitiosCastillo = new List<Vector3>(), sitiosBoveda = new List<Vector3>();   // dónde van los montones de oro (coordenadas del castillo)

        void CajaC(Transform pa, string n, float x0, float y0, float z0, float x1, float y1, float z1, Material m, bool col = true)
        {
            if (x1 - x0 < 0.001f || y1 - y0 < 0.001f || z1 - z0 < 0.001f) return;
            Pz(pa, n, PrimitiveType.Cube, new Vector3((x0 + x1) / 2f, (y0 + y1) / 2f, (z0 + z1) / 2f), new Vector3(x1 - x0, y1 - y0, z1 - z0), 0f, m, col);
        }

        /// Pared recta con huecos de puerta: cada hueco = {centro, ancho, alto desde y0}. alongZ: la pared corre a lo largo de z (x fija).
        void MuroLin(Transform pa, bool alongZ, float fix, float a0, float a1, float y0, float y1, float t, Material m, float[][] ops)
        {
            System.Array.Sort(ops, (p, q) => p[0].CompareTo(q[0]));
            float cur = a0;
            foreach (var o in ops)
            {
                float lo = o[0] - o[1] / 2f, hi = o[0] + o[1] / 2f;
                if (lo > cur) TramoMuro(pa, alongZ, fix, cur, lo, y0, y1, t, m);
                if (y0 + o[2] < y1) TramoMuro(pa, alongZ, fix, lo, hi, y0 + o[2], y1, t, m);
                cur = hi;
            }
            if (a1 > cur) TramoMuro(pa, alongZ, fix, cur, a1, y0, y1, t, m);
        }

        void TramoMuro(Transform pa, bool alongZ, float fix, float p, float q, float ya, float yb, float t, Material m)
        {
            if (alongZ) CajaC(pa, "coop_muro_int", fix - t / 2f, ya, p, fix + t / 2f, yb, q, m);
            else CajaC(pa, "coop_muro_int", p, ya, fix - t / 2f, q, yb, fix + t / 2f, m);
        }

        struct PropPendiente { public string Tipo; public float X, Y, Z, Yaw, Esc; }
        readonly List<PropPendiente> propPend = new List<PropPendiente>();

        /// Pide un objeto del propio juego en (x, y, z) del castillo. Se coloca más tarde (ColocarDecoracionCastillo), cuando ya están registrados los objetos del bosque.
        GameObject Prop(Transform pa, string tipo, float x, float y, float z, float yaw, float esc = 1f)
        {
            propPend.Add(new PropPendiente { Tipo = tipo, X = x, Y = y, Z = z, Yaw = yaw, Esc = esc });
            return null;
        }

        /// Si un tipo no existe con ese nombre en el bosque, se prueba con estos (mismo uso).
        static readonly Dictionary<string, string[]> PropAlternativas = new Dictionary<string, string[]>
        {
            { "barrel01_pile", new[] { "barrel01", "Barrel", "barrel" } },
            { "terracotta_plate01", new[] { "metal_bowl01", "metal_plate01" } },
            { "CandleStand1", new[] { "CandleStand1_nolight", "CandleStand2_nolight" } },
            { "CandleStand2_nolight", new[] { "CandleStand1_nolight" } },
            { "hammer", new[] { "Hammer_small" } },
            { "smallFrame_a", new[] { "smallFrame_a 1", "smallFrame_b", "smallFrame_b 1" } },
        };

        /// Cuántas piezas visibles de g no se podrían rearmar al copiarlo (horneadas sin malla suelta). 999 = no queda nada visible.
        static int PiezasPerdidas(GameObject g)
        {
            var s = Coop.Piezas.Sueltas();
            int buenas = 0, malas = 0;
            foreach (var r in g.GetComponentsInChildren<MeshRenderer>(false))
            {
                if (!r.enabled) continue;
                if (!r.isPartOfStaticBatch || Coop.Piezas.MallaDe(r, s) != null) buenas++; else malas++;
            }
            return buenas == 0 ? 999 : malas;
        }

        /// Entre todas las copias del tipo en la escena, la que se puede rearmar entera (o con menos piezas perdidas).
        GameObject OriginalProp(string tipo)
        {
            GameObject o;
            if (propOrig.TryGetValue(tipo, out o)) return o;
            o = MejorCopia(tipo);
            string[] alt;
            if (o == null && PropAlternativas.TryGetValue(tipo, out alt))
                foreach (var a in alt) { o = MejorCopia(a); if (o != null) { plugin.Log("Castillo: '" + tipo + "' se reemplaza por '" + a + "'"); break; } }
            propOrig[tipo] = o;
            return o;
        }

        /// Índice de objetos de la escena por nombre base: antes cada tipo recorría TODOS los objetos (≈6 s de carga, 2026-11-18).
        Dictionary<string, List<GameObject>> indiceProps; int indiceFrame = -1;
        GameObject MejorCopia(string tipo)
        {
            if (indiceProps == null || indiceFrame != Time.frameCount)
            {
                indiceProps = new Dictionary<string, List<GameObject>>(); indiceFrame = Time.frameCount;
                foreach (var g0 in Resources.FindObjectsOfTypeAll<GameObject>())
                {
                    if (g0 == null || !g0.scene.IsValid()) continue;
                    string bn = Catalogo.BaseDe(g0.name); List<GameObject> l;
                    if (!indiceProps.TryGetValue(bn, out l)) indiceProps[bn] = l = new List<GameObject>();
                    l.Add(g0);
                }
            }
            GameObject mejor = null; int perdidasMin = int.MaxValue;
            List<GameObject> cands; if (!indiceProps.TryGetValue(tipo, out cands)) return null;
            foreach (var g in cands)
            {
                if (g == null || !g.scene.IsValid() || !g.activeInHierarchy || g.name.EndsWith(" [coop]") || g.name.StartsWith("coop_") || g.name.StartsWith("clon")) continue;
                if (Catalogo.BaseDe(g.name) != tipo) continue;
                if (g.transform.parent != null && Catalogo.BaseDe(g.transform.parent.name) == tipo) continue;   // la raíz, no sus hijos del mismo nombre
                int perdidas = PiezasPerdidas(g);
                if (perdidas >= 999) continue;
                if (perdidas < perdidasMin || (perdidas == perdidasMin && string.CompareOrdinal(g.name, mejor.name) < 0)) { mejor = g; perdidasMin = perdidas; }
            }
            if (mejor != null && perdidasMin > 0) plugin.Log("Castillo: '" + tipo + "' sin copia completa; se usa " + mejor.name + " (pierde " + perdidasMin + " piezas)");
            return mejor;
        }

        /// Orden de prueba: qué copia se usaría para cada tipo de mueble y cuántas piezas pierde.
        public void ProbarProps(string lista)
        {
            propOrig.Clear(); Coop.Piezas.Olvidar();
            foreach (var t in lista.Split(','))
            {
                var o = OriginalProp(t.Trim());
                plugin.Log("  " + t.Trim() + " -> " + (o == null ? "NINGUNA" : o.name + " en " + (o.transform.position - Anclaje.Origen()).ToString("F0") + " pierde " + PiezasPerdidas(o)));
            }
            propOrig.Clear();
        }

        /// Coloca los muebles pedidos (copias del propio juego, apoyadas en el suelo).
        void ColocarDecoracionCastillo()
        {
            propsPuestos = 0; propsFaltan = 0; propsHechos = 0; propOrig.Clear();
            if (castilloRaiz == null) { propPend.Clear(); return; }
            var pa = castilloRaiz.transform;
            var faltantes = new HashSet<string>();
            foreach (var p in propPend)
            {
                var o = OriginalProp(p.Tipo);
                Vector3 objetivo = pa.TransformPoint(new Vector3(p.X, p.Y, p.Z));
                GameObject c;
                if (o != null)
                {
                    int faltan;
                    var rot = pa.rotation * Quaternion.Euler(0f, p.Yaw, 0f) * Quaternion.Euler(o.transform.eulerAngles.x, 0f, o.transform.eulerAngles.z);
                    c = Coop.Piezas.Clonar(o, objetivo, rot, out faltan);
                }
                else
                {
                    // la malla del juego ya no existe: mueble hecho a mano con el material original
                    c = PropHecho(p.Tipo);
                    if (c != null) { c.transform.position = objetivo; c.transform.rotation = pa.rotation * Quaternion.Euler(0f, p.Yaw, 0f); propsHechos++; }
                }
                if (c == null) { propsFaltan++; faltantes.Add(p.Tipo); continue; }
                c.name = "coop_prop_" + p.Tipo; c.transform.SetParent(pa, true);
                c.transform.localScale *= p.Esc * CastilloEscala;
                foreach (var rb in c.GetComponentsInChildren<Rigidbody>(true)) rb.isKinematic = true;
                Bounds b = new Bounds(); bool hay = false;
                foreach (var r in c.GetComponentsInChildren<Renderer>(false))
                {
                    if (!r.enabled || r is ParticleSystemRenderer) continue;
                    if (!hay) { b = r.bounds; hay = true; } else b.Encapsulate(r.bounds);
                }
                if (!hay) { Object.Destroy(c); propsFaltan++; faltantes.Add(p.Tipo); continue; }
                c.transform.position += new Vector3(objetivo.x - b.center.x, objetivo.y - b.min.y, objetivo.z - b.center.z);
                Asentar.EnSuelo(c, 6f);   // si el sitio pedido quedó en el aire, baja hasta el piso o la mesa de abajo
                propsPuestos++;
            }
            plugin.Log("Castillo: decoración del juego: " + propsPuestos + " puestos (" + propsHechos + " hechos a mano), " + propsFaltan + " sin modelo" + (faltantes.Count > 0 ? " (" + string.Join(", ", new List<string>(faltantes).ToArray()) + ")" : ""));
            propPend.Clear();
        }

        /// Misma colocación en los dos lados (sx = ±1): el yaw se refleja.
        GameObject PropE(Transform pa, float sx, string tipo, float x, float y, float z, float yaw, float esc = 1f)
        {
            return Prop(pa, tipo, sx * x, y, z, sx > 0f ? yaw : 180f - yaw, esc);
        }

        void Alfombra(Transform pa, float x, float y, float z, float ax, float az, Color c)
        {
            var g = Pz(pa, "coop_alfombra", PrimitiveType.Cube, new Vector3(x, y + 0.03f, z), new Vector3(ax, 0.05f, az), 0f, matTela, false);
            g.GetComponent<Renderer>().sharedMaterial = new Material(matTela) { color = c };
        }

        void Estandarte(Transform pa, float x, float y, float z, bool alongZ)
        {
            Pz(pa, "coop_estandarte", PrimitiveType.Cube, new Vector3(x, y, z), alongZ ? new Vector3(0.08f, 3.6f, 1.6f) : new Vector3(1.6f, 3.6f, 0.08f), 0f, matTela, false);
        }

        void ConstruirInterior(Transform pa)
        {
            propsPuestos = 0; propsFaltan = 0; propOrig.Clear(); propPend.Clear(); bovedaPuerta = null; sitiosCastillo.Clear(); sitiosBoveda.Clear();
            float Y = PisoC;
            // ---- 1. Puertas del salón: a los dos corredores laterales y a la galería trasera ----
            foreach (var sx in new[] { -1f, 1f })
            {
                MuroLin(pa, true, sx * 19.2f, 2f, 38f, 3f, 25f, 1.6f, matPiedra, new[] { new[] { 12.5f, 4f, 7.2f }, new[] { 27.5f, 4f, 7.2f } });
            }
            MuroLin(pa, false, 37.2f, -20f, 20f, 3f, 25f, 1.6f, matPiedra, new[] { new[] { 12f, 4.5f, 8f } });
            // ---- 2. Corredores del salón a las alas (dos por lado) ----
            foreach (var sx in new[] { -1f, 1f })
                foreach (var c in new[] { 12.5f, 27.5f })
                {
                    float xa = Mathf.Min(sx * 19.6f, sx * 32.4f), xb = Mathf.Max(sx * 19.6f, sx * 32.4f);
                    CajaC(pa, "coop_corredor_base", xa, 0f, c - 2.5f, xb, 3f, c + 2.5f, matPiedra);
                    CajaC(pa, "coop_corredor_suelo", xa, 3f, c - 2f, xb, 3.1f, c + 2f, matAdoquin);
                    CajaC(pa, "coop_corredor_techo", xa, 10.2f, c - 2.7f, xb, 10.8f, c + 2.7f, matPiedra);
                    MuroLin(pa, false, c - 2.4f, Mathf.Min(sx * 19.6f, sx * 32.4f), Mathf.Max(sx * 19.6f, sx * 32.4f), 3.1f, 10.2f, 0.8f, matPiedra, new float[0][]);
                    MuroLin(pa, false, c + 2.4f, Mathf.Min(sx * 19.6f, sx * 32.4f), Mathf.Max(sx * 19.6f, sx * 32.4f), 3.1f, 10.2f, 0.8f, matPiedra, new float[0][]);
                    LuzCastillo(pa, new Vector3(sx * 25.8f, 8f, c), new Color(1f, 0.6f, 0.3f), 14f, 1.5f).gameObject.AddComponent<Titilar>();
                    Estatua(pa, new Vector3(sx * 22.4f, Y, c - 1.2f), sx > 0f ? -90f : 90f);
                    Estandarte(pa, sx * 26.5f, 7f, c - 2f, false); Estandarte(pa, sx * 30f, 7f, c + 2f, false);
                    PropE(pa, sx, "CandleStand2_nolight", 29.5f, Y, c - 1.4f, 0f);
                    PropE(pa, sx, "OilLamp01_gua", 24.5f, Y, c + 1.4f, 0f);
                }

            // ---- 3. Alas: dos plantas cada una, unidas por una escalera ----
            foreach (var sx in new[] { -1f, 1f }) ConstruirAla(pa, sx);

            // ---- 4. Galería trasera y Torre del Homenaje ----
            ConstruirGaleria(pa);
            ConstruirTorreHomenaje(pa);

            // sitios del oro repartido (300–900 en total por partida)
            sitiosCastillo.AddRange(new[] { new Vector3(-15.5f, Y, 5.5f), new Vector3(15.5f, Y, 28.5f), new Vector3(-4f, Y, 39.2f), new Vector3(-27.5f, Y, 13.5f), new Vector3(27.5f, Y, 28.5f),
                new Vector3(-44.5f, Y, 8f), new Vector3(44.5f, 11.05f, 28.5f), new Vector3(-44.5f, 11.05f, 8.5f), new Vector3(-3.2f, Y, TorreZ + 6.8f) });   // (antes junto a la columna, sobre el hueco de la bajada: flotaba)
            // ---- 5. Salón del trono: decoración del propio juego ----
            PropE(pa, 1f, "barrel01_pile", 16.8f, Y, 4.8f, 20f); PropE(pa, -1f, "barrel01_pile", 16.8f, Y, 4.8f, 70f);
            PropE(pa, 1f, "Barrel_low", 14.8f, Y, 4.4f, 0f); PropE(pa, -1f, "Basket_gua", 14.8f, Y, 4.6f, 0f);
            PropE(pa, 1f, "CandleStand2_nolight", 3.8f, Y, 33f, 0f); PropE(pa, -1f, "CandleStand2_nolight", 3.8f, Y, 33f, 0f);
            PropE(pa, 1f, "skull_father", 17.2f, Y, 21f, 90f, 0.6f); PropE(pa, -1f, "skull_father", 17.2f, Y, 21f, 90f, 0.6f);
            PropE(pa, 1f, "Deer_Troffy", 18.2f, 9f, 6f, 90f); PropE(pa, -1f, "Deer_Troffy", 18.2f, 9f, 34f, 90f);
            PropE(pa, 1f, "Sack", 16.5f, Y, 36f, 0f); PropE(pa, 1f, "Sack", 15.8f, Y, 35.4f, 40f);
            for (int i = 0; i < 8; i++) { float z = 13.5f + i * 2f; foreach (var sx in new[] { -1f, 1f }) if (i % 2 == 0) PropE(pa, sx, "terracotta_pitcher01", 5.2f, 4.45f, z, 0f); }
            plugin.Log("Castillo: interior (salón con 3 puertas, 4 corredores, 2 alas de dos plantas, galería y Torre del Homenaje); " + propPend.Count + " muebles pedidos");
        }

        // ---------------- Patios laterales y azotea ----------------

        /// El castillo se extiende al este y al oeste: patios amurallados junto a cada ala, con torres, almenas, pozo y brasas (se entra por la puerta del ala).
        void ConstruirMuralla(Transform pa)
        {
            foreach (var sx in new[] { -1f, 1f })
            {
                float xa = sx * 48.2f, xb = sx * 64f, lo = Mathf.Min(xa, xb), hi = Mathf.Max(xa, xb);
                float z0 = 6f, z1 = 34f, ymur = 15f;
                // suelo: base maciza y losa delgada de adoquín (antes una sola caja de 6 m de alto: la textura salía estirada en vetas, como tablones)
                CajaC(pa, "coop_patio_base", lo, -3f, z0, hi, 3.0f, z1, matPiedra);
                CajaC(pa, "coop_patio_suelo", lo, 3.0f, z0, hi, 3.1f, z1, matAdoquin);
                // bordillo de piedra junto a los muros y anillo de losas alrededor del pozo
                CajaC(pa, "coop_patio_bordillo", lo, 3.1f, z0 + 0.4f, hi, 3.22f, z0 + 1.2f, matPiedra, false);
                CajaC(pa, "coop_patio_bordillo", lo, 3.1f, z1 - 1.2f, hi, 3.22f, z1 - 0.4f, matPiedra, false);
                Anillo(pa, "coop_patio_losas_pozo", new Vector3(sx * 56f, 0f, 20f), 3.1f, 3.16f, 1.6f, 3.4f, 0f, 360f, matPiedra);
                // uniones con el ala: antes quedaba un hueco por detrás (entre el ala, que acaba en z 32, y la muralla del fondo, en z 33,6) y una ranura delante
                CajaC(pa, "coop_muralla_union", Mathf.Min(sx * 46.8f, sx * 48.4f), -3f, 31.4f, Mathf.Max(sx * 46.8f, sx * 48.4f), ymur, z1 + 1f, matPiedra);
                CajaC(pa, "coop_muralla_union", Mathf.Min(sx * 46.8f, sx * 48.4f), -3f, 4f, Mathf.Max(sx * 46.8f, sx * 48.4f), ymur, z0 + 0.4f, matPiedra);
                CajaC(pa, "coop_muralla_frente", lo, -3f, z0 - 1f, hi + (sx > 0 ? 1f : 0f), ymur, z0 + 0.4f, matPiedra);
                CajaC(pa, "coop_muralla_fondo", lo, -3f, z1 - 0.4f, hi + (sx > 0 ? 1f : 0f), ymur, z1 + 1f, matPiedra);
                float xf = sx * 64.5f;
                CajaC(pa, "coop_muralla_fin", Mathf.Min(xf - 0.9f, xf + 0.9f), -3f, z0 - 1f, Mathf.Max(xf - 0.9f, xf + 0.9f), ymur, z1 + 1f, matPiedra);
                // adarve (camino de ronda) y almenas
                for (float x = lo; x <= hi; x += 3.2f)
                {
                    Pz(pa, "coop_almena", PrimitiveType.Cube, new Vector3(x + 1f, ymur + 0.9f, z0 - 0.7f), new Vector3(1.8f, 1.8f, 1.1f), 0f, matPiedra);
                    Pz(pa, "coop_almena", PrimitiveType.Cube, new Vector3(x + 1f, ymur + 0.9f, z1 + 0.7f), new Vector3(1.8f, 1.8f, 1.1f), 0f, matPiedra);
                }
                for (float z = z0; z <= z1; z += 3.2f) Pz(pa, "coop_almena", PrimitiveType.Cube, new Vector3(xf + sx * 0.5f, ymur + 0.9f, z + 1f), new Vector3(1.1f, 1.8f, 1.8f), 0f, matPiedra);
                // torres de esquina y una en medio
                Torre(pa, "coop_torre_patio_a", xf, z0 - 0.5f, -3f, 31f, 5f, 12f);
                Torre(pa, "coop_torre_patio_b", xf, z1 + 0.5f, -3f, 28f, 4.6f, 11f);
                Torre(pa, "coop_torre_patio_c", xf, 20f, -3f, 36f, 4.2f, 13f);
                // pozo, farolas y estandartes dentro del patio
                Pz(pa, "coop_pozo", PrimitiveType.Cylinder, new Vector3(sx * 56f, 3.7f, 20f), new Vector3(3.2f, 0.6f, 3.2f), 0f, matPiedra);
                Pz(pa, "coop_pozo_agua", PrimitiveType.Cylinder, new Vector3(sx * 56f, 4.15f, 20f), new Vector3(2.4f, 0.04f, 2.4f), 0f, matVidrio, false);
                foreach (var dx in new[] { -1.3f, 1.3f }) Pz(pa, "coop_pozo_poste", PrimitiveType.Cube, new Vector3(sx * 56f + dx, 6.2f, 20f), new Vector3(0.3f, 4f, 0.3f), 0f, matMadera2);
                Pz(pa, "coop_pozo_viga", PrimitiveType.Cube, new Vector3(sx * 56f, 8.2f, 20f), new Vector3(3.2f, 0.3f, 0.3f), 0f, matMadera2);
                Farol2(pa, new Vector3(sx * 54f, 3.1f, 11f)); Farol2(pa, new Vector3(sx * 55f, 3.1f, 28.5f));   // (apartados de las torrecillas, que ahora están por fuera del ala) Farol2(pa, new Vector3(sx * 60f, 3.1f, 20f));
                LuzCastillo(pa, new Vector3(sx * 56f, 8f, 20f), new Color(0.38f, 0.5f, 0.85f), 40f, 1.4f);
            }
        }

        /// La azotea del torreón principal: chimeneas, torretas, brasero y un asta con bandera.
        void ConstruirAzotea(Transform pa)
        {
            float Ya = 25.4f;
            foreach (var sx in new[] { -1f, 1f })
            {
                Torre(pa, "coop_azotea_torreta", sx * 12f, 31f, Ya, 36f, 2.8f, 7f);
                foreach (var z in new[] { 10f, 21f })
                {
                    Pz(pa, "coop_chimenea_azotea", PrimitiveType.Cube, new Vector3(sx * 14f, Ya + 3.5f, z), new Vector3(2.4f, 7f, 2.4f), 0f, matPiedra);
                    Pz(pa, "coop_chimenea_tapa", PrimitiveType.Cube, new Vector3(sx * 14f, Ya + 7.2f, z), new Vector3(3.1f, 0.5f, 3.1f), 0f, matPiedra, false);
                    Pz(pa, "coop_chimenea_boca", PrimitiveType.Cube, new Vector3(sx * 14f, Ya + 7.5f, z), new Vector3(1.6f, 0.2f, 1.6f), 0f, matHierroC, false);
                }
                Fogata.Crear(pa.TransformPoint(new Vector3(sx * 16.5f, Ya + 0.6f, 6f)), pa);
                Pz(pa, "coop_brasero", PrimitiveType.Cylinder, new Vector3(sx * 16.5f, Ya + 0.5f, 6f), new Vector3(1.6f, 0.5f, 1.6f), 0f, matHierroC, false);
            }
            Pz(pa, "coop_azotea_asta", PrimitiveType.Cylinder, new Vector3(0f, Ya + 6f, 24f), new Vector3(0.3f, 6f, 0.3f), 0f, matHierroC, false);
            var bd = Pz(pa, "coop_azotea_bandera", PrimitiveType.Cube, new Vector3(2.6f, Ya + 10f, 24f), new Vector3(5f, 2.6f, 0.06f), 0f, matTela, false);
            bd.AddComponent<Ondear>().Fase = 1.7f;
            Pz(pa, "coop_azotea_plinto", PrimitiveType.Cube, new Vector3(0f, Ya + 0.5f, 24f), new Vector3(4f, 1f, 4f), 0f, matPiedra);
        }

        // ---------------- Alas ----------------

        void ConstruirAla(Transform pa, float sx)
        {
            float Y = PisoC, Y2 = 11f;
            bool izq = sx < 0f;
            float xa = sx > 0 ? 32f : -48f, xb = sx > 0 ? 48f : -32f;
            // zócalo (hasta el suelo), suelo, paredes y techos
            CajaC(pa, "coop_ala_base", xa, 0f, 4f, xb, 3f, 32f, matPiedra);
            CajaC(pa, "coop_ala_suelo", xa + 0.6f, 3f, 4.6f, xb - 0.6f, 3.1f, 31.4f, MatLosas());   // losas pulidas (antes madera lisa)
            // pared oeste/este: la que da al salón (x=±32.6) lleva las dos puertas de los corredores
            float xi = sx * 32.6f, xe = sx * 47.4f;
            MuroLin(pa, true, xi, 4f, 32f, 3f, 18f, 1.2f, matPiedra, new[] { new[] { 12.5f, 4f, 7.2f }, new[] { 27.5f, 4f, 7.2f } });
            MuroLin(pa, true, xe, 4f, 32f, 3f, 18f, 1.2f, matPiedra, new[] { new[] { 18f, 4.4f, 7f } });   // puerta al patio exterior
            TramoMuro(pa, false, 4.6f, Mathf.Min(sx * 32f, sx * 48f), Mathf.Max(sx * 32f, sx * 48f), 3f, 18f, 1.2f, matPiedra);
            TramoMuro(pa, false, 31.4f, Mathf.Min(sx * 32f, sx * 48f), Mathf.Max(sx * 32f, sx * 48f), 3f, 18f, 1.2f, matPiedra);
            // techo de la planta 2
            CajaC(pa, "coop_ala_techo", xa, 17.4f, 4f, xb, 18.2f, 32f, matPiedra);
            // escalera de caracol en la esquina del fondo (pedido 2026-11-17): la recta de antes tapaba la puerta al patio y no dejaba volver.
            // Centro (|x| 44, z 28), radio 2,8; 28 peldaños de 15° y 0,282 m; arranca mirando a la sala (225°) y acaba en 270° junto al borde del hueco.
            float cu = 44f, cz = 28f, rIn = 0.5f, rOut = 2.8f, u0 = cu - rOut, u1 = cu + rOut, z0h = cz - rOut;   // hueco: |x| 41,2–46,8, z 25,2–31,4
            CajaC(pa, "coop_planta2", Mathf.Min(sx * 32.6f, sx * u0), 10.6f, 4.6f, Mathf.Max(sx * 32.6f, sx * u0), 11f, 31.4f, MatLosas());
            CajaC(pa, "coop_planta2", Mathf.Min(sx * u0, sx * 47.4f), 10.6f, 4.6f, Mathf.Max(sx * u0, sx * 47.4f), 11f, z0h, MatLosas());
            const int PasosAla = 28;
            float subida = (Y2 - Y) / PasosAla;
            for (int i = 0; i < PasosAla; i++)
            {
                float fi = (270f - (PasosAla - 1 - i) * 15f) * Mathf.Deg2Rad;   // en ejes (|x|, z)
                Vector3 d = new Vector3(sx * Mathf.Cos(fi), 0f, Mathf.Sin(fi));
                float top = Y + (i + 1) * subida, rc = (rIn + rOut) / 2f;
                float giro = -Mathf.Atan2(d.z, d.x) * Mathf.Rad2Deg;
                Pz(pa, "coop_caracol_peldano", PrimitiveType.Cube, new Vector3(sx * cu, top - 0.25f, cz) + d * rc, new Vector3(rOut - rIn + 0.05f, 0.5f, 0.8f), giro, matPiedra);
            }
            Pz(pa, "coop_caracol_eje", PrimitiveType.Cylinder, new Vector3(sx * cu, (Y + Y2 + 1.2f) / 2f, cz), new Vector3(rIn * 2f, (Y2 + 1.2f - Y) / 2f, rIn * 2f), 0f, matPiedra);
            Pz(pa, "coop_caracol_remate", PrimitiveType.Sphere, new Vector3(sx * cu, Y2 + 1.35f, cz), new Vector3(0.7f, 0.7f, 0.7f), 0f, matPiedra, false);
            // barandilla alrededor del hueco arriba, con paso libre donde termina la escalera (|x| 43,1–44,9 en el borde delantero)
            CajaC(pa, "coop_baranda", Mathf.Min(sx * (u0 - 0.15f), sx * (u0 + 0.05f)), 11f, z0h, Mathf.Max(sx * (u0 - 0.15f), sx * (u0 + 0.05f)), 12.1f, 31.4f, matMadera2, true);
            CajaC(pa, "coop_baranda", Mathf.Min(sx * u0, sx * 43.1f), 11f, z0h - 0.15f, Mathf.Max(sx * u0, sx * 43.1f), 12.1f, z0h + 0.05f, matMadera2, true);
            CajaC(pa, "coop_baranda", Mathf.Min(sx * 44.9f, sx * 46.8f), 11f, z0h - 0.15f, Mathf.Max(sx * 44.9f, sx * 46.8f), 12.1f, z0h + 0.05f, matMadera2, true);
            // luces
            foreach (var z in new[] { 10f, 26f }) foreach (var y in new[] { 7.5f, 14.5f })
                LuzCastillo(pa, new Vector3(sx * 38f, y, z), new Color(1f, 0.6f, 0.3f), 13f, 1.4f).gameObject.AddComponent<Titilar>();

            if (izq)
            {   // ---- Ala izquierda: Biblioteca (abajo) y Dormitorio (arriba)
                for (int i = 0; i < 3; i++) PropE(pa, sx, "bookshelf_witch", 35f + i * 2.9f, Y, 5.5f, 90f);
                PropE(pa, sx, "bookshelf_witch", 33.5f, Y, 20f, 0f); PropE(pa, sx, "bookshelf_witch", 33.5f, Y, 23f, 0f);
                PropE(pa, sx, "Shelf_wooden", 40f, Y, 30.2f, 180f); PropE(pa, sx, "Shelf_wooden", 36.5f, Y, 30.2f, 180f);
                PropE(pa, sx, "wooden_table", 38.5f, Y, 16f, 0f); PropE(pa, sx, "chair01", 37f, Y, 16f, 90f); PropE(pa, sx, "chair01", 40f, Y, 16f, -90f);
                PropE(pa, sx, "table01", 38.5f, Y, 24f, 0f); PropE(pa, sx, "Bench", 36f, Y, 24f, 0f);
                PropE(pa, sx, "CandleStand1", 38.5f, Y + 0.95f, 15.4f, 0f); PropE(pa, sx, "Book_parrent", 38.5f, Y + 0.95f, 17f, 20f);
                PropE(pa, sx, "MortarAndPestle01", 38.5f, Y + 0.95f, 24.6f, 0f);
                Alfombra(pa, sx * 38.5f, Y, 19.5f, 7f, 12f, new Color(0.25f, 0.06f, 0.12f));
                // dormitorio
                for (int i = 0; i < 3; i++) PropE(pa, sx, "bed_parrent", 36f, Y2 + 0.05f, 7.5f + i * 8.2f, 0f);
                PropE(pa, sx, "cupboard_cabinet", 34f, Y2 + 0.05f, 30f, 90f); PropE(pa, sx, "cupboard_drawer", 37f, Y2 + 0.05f, 30.4f, 180f);
                PropE(pa, sx, "cupboard_short", 44.5f, Y2 + 0.05f, 16f, 180f);
                PropE(pa, sx, "Chest_static", 40.5f, Y2 + 0.05f, 6.5f, 0f); PropE(pa, sx, "SK_BackPack", 39.5f, Y2 + 0.05f, 22f, 30f);
                PropE(pa, sx, "smallFrame_a", 33f, Y2 + 4f, 15f, 90f);
                Alfombra(pa, sx * 39f, Y2 + 0.05f, 17f, 5f, 8f, new Color(0.12f, 0.1f, 0.3f));
            }
            else
            {   // ---- Ala derecha: Cocina (abajo) y Armería/trofeos (arriba)
                PropE(pa, sx, "wooden_table", 38.5f, Y, 16f, 0f); PropE(pa, sx, "Bench", 36.6f, Y, 16f, 0f); PropE(pa, sx, "Bench", 40.4f, Y, 16f, 0f);
                PropE(pa, sx, "Balia_witch", 37f, Y, 7.5f, 0f); Fogata.Crear(pa.TransformPoint(new Vector3(sx * 37f, Y + 0.2f, 7.5f)), pa);
                for (int i = 0; i < 3; i++) PropE(pa, sx, "barrel01_pile", 34f + i * 2.4f, Y, 30.2f, i * 40f);
                PropE(pa, sx, "Barrel_low", 35.5f, Y, 27.8f, 0f); PropE(pa, sx, "Basket_gua", 36.9f, Y, 27.9f, 0f);
                PropE(pa, sx, "Sack", 33.8f, Y, 26f, 20f); PropE(pa, sx, "Sack", 34.4f, Y, 26.8f, 70f); PropE(pa, sx, "Sack", 33.9f, Y, 24.6f, 0f);
                PropE(pa, sx, "cupboard_cabinet", 33.6f, Y, 19.5f, 0f); PropE(pa, sx, "Shelf_wooden", 36f, Y, 5.3f, 90f); PropE(pa, sx, "Shelf_wooden", 39f, Y, 5.3f, 90f);
                PropE(pa, sx, "metal_pitcher01", 38.5f, Y + 0.95f, 15.4f, 0f); PropE(pa, sx, "metal_bowl01", 38.5f, Y + 0.95f, 16.6f, 0f); PropE(pa, sx, "terracotta_pitcher02", 38.5f, Y + 0.95f, 17.4f, 0f);
                PropE(pa, sx, "table01", 38.5f, Y, 24f, 0f); PropE(pa, sx, "terracotta_plate01", 38.5f, Y + 0.95f, 24f, 0f);
                // armería
                for (int i = 0; i < 4; i++) PropE(pa, sx, "SandBagPrefab", 41f, Y2 + 0.05f, 6.5f + i * 0.9f, 0f);
                PropE(pa, sx, "Deer_Troffy", 33.2f, Y2 + 4f, 18f, 90f); PropE(pa, sx, "Deer_Troffy", 33.2f, Y2 + 4f, 28f, 90f);
                PropE(pa, sx, "hammer", 36f, Y2 + 1.05f, 30.5f, 20f);   // (quitada la pala: quedaba un palo parado en medio de la sala)
                PropE(pa, sx, "wooden_table", 38.5f, Y2 + 0.05f, 17f, 0f); PropE(pa, sx, "Witch_WooDoo", 38.5f, Y2 + 1f, 17f, 0f);
                PropE(pa, sx, "skull_father", 40f, Y2 + 0.05f, 28f, 180f, 0.7f); PropE(pa, sx, "magic_totem", 35f, Y2 + 0.05f, 8f, 0f);
                PropE(pa, sx, "Chest_static", 44.5f, Y2 + 0.05f, 20f, 90f);
                Alfombra(pa, sx * 38f, Y2 + 0.05f, 20f, 6f, 10f, new Color(0.3f, 0.05f, 0.05f));
                for (int i = 0; i < 3; i++) Estandarte(pa, sx * 33.1f, Y2 + 3.4f, 8f + i * 8f, true);
            }
        }

        // ---------------- Galería trasera ----------------

        void ConstruirGaleria(Transform pa)
        {
            float Y = PisoC;
            CajaC(pa, "coop_galeria_suelo", -17.2f, Y - 0.1f, 38f, 17.2f, Y, 44.4f, matAdoquin);
            CajaC(pa, "coop_galeria_techo", -17.8f, 10.4f, 37.6f, 17.8f, 11f, 45.2f, matPiedra);
            MuroLin(pa, false, 44.4f, -17.8f, 17.8f, Y, 10.4f, 1f, matPiedra, new[] { new[] { 0f, 5.5f, 6.8f } });
            TramoMuro(pa, true, -17.4f, 38f, 44.9f, Y, 10.4f, 0.8f, matPiedra);
            TramoMuro(pa, true, 17.4f, 38f, 44.9f, Y, 10.4f, 0.8f, matPiedra);
            foreach (var x in new[] { -11f, -5f, 5f, 11f }) VentanaOjival(pa, new Vector3(x, 6f, 43.85f), Vector3.back, 1.4f, 3f, 0.9f);
            for (int i = 0; i < 4; i++) { float x = -13.5f + i * 9f; if (Mathf.Abs(x) < 4f || x < -9f) continue; Estatua(pa, new Vector3(x, Y, 41.6f), 180f); }
            foreach (var x in new[] { -14f, -7.5f, 7.5f, 14f }) LuzCastillo(pa, new Vector3(x, 8f, 41f), new Color(0.9f, 0.55f, 0.3f), 13f, 1.4f).gameObject.AddComponent<Titilar>();
            Alfombra(pa, 4.2f, Y, 40.8f, 22.4f, 2.4f, new Color(0.35f, 0.04f, 0.08f));
            PropE(pa, 1f, "CandleStand1", 8f, Y, 39.2f, 0f);
            PropE(pa, 1f, "cupboard_cabinet", 16.4f, Y, 41.2f, -90f);
            PropE(pa, 1f, "Deer_Troffy", 8.5f, 8f, 43.6f, 180f);
            Estandarte(pa, -2.6f, 6.5f, 43.8f, false); Estandarte(pa, 2.6f, 6.5f, 43.8f, false);

            // ---- Sala de la antigua bóveda (x −17,2 … −8,4): vacía y sin puerta (pedido 2026-11-17). El tesoro está ahora bajo la Torre del Homenaje. ----
            MuroLin(pa, true, -8f, 38f, 44.4f, Y, 10.4f, 0.8f, matPiedra, new[] { new[] { 41.2f, 3.8f, 5f } });
        }

        // ---------------- Torre del Homenaje (~50 m, escalera de caracol y terraza) ----------------

        void ConstruirTorreHomenaje(Transform pa)
        {
            float Y = PisoC;
            float yTope = Y + TorrePasos * TorreSubida;   // suelo de la terraza (50,1 m)
            float rMed = TorreR - TorreMuro / 2f;
            const int N = 36;
            // zócalo y suelo
            // zócalo: anillo por fuera del muro (antes un cilindro macizo; ahora debajo del suelo está el sótano con la bóveda)
            for (int k = 0; k < N; k++)
            {
                if (k >= 8 && k <= 10) continue;   // la salida al jardín cruza el zócalo
                float ang = k * 10f, rad = ang * Mathf.Deg2Rad;
                Pz(pa, "coop_torre_zocalo", PrimitiveType.Cube, new Vector3(TorreX + Mathf.Cos(rad) * (TorreR + 0.4f), 1.5f, TorreZ + Mathf.Sin(rad) * (TorreR + 0.4f)), new Vector3(0.8f, 3f, 2.3f), -ang, matPiedra);
            }
            // suelo de la planta baja: anillo con el hueco de la bajada al sótano (de 35° a 104°)
            Anillo(pa, "coop_torre_suelo", new Vector3(TorreX, 0f, TorreZ), Y - 0.3f, Y, 4.5f, TorreR - TorreMuro + 0.05f, TorreBajadaFin, TorreBajadaIni + 360f, matAdoquin);
            // huecos: puerta al sur (ángulo -90°) y ventanas junto a la escalera cada media vuelta
            var huecos = new List<float[]>[N];
            for (int k = 0; k < N; k++) huecos[k] = new List<float[]>();
            foreach (int k in new[] { 26, 27, 28 }) huecos[k].Add(new[] { Y, Y + 6.4f });   // portón al sur (270°): segmentos de 260°, 270° y 280°
            foreach (int k in new[] { 8, 9, 10 }) huecos[k].Add(new[] { Y, Y + 4.4f });   // salida al jardín, al norte (90°) (pedido 2026-11-17)
            for (int i = 12; i <= 150; i += 20)
            {
                float ang = (TorreAngIni + i * 360f / TorrePasosVuelta) % 360f; if (ang < 0f) ang += 360f;
                int k = Mathf.RoundToInt(ang / 10f) % N;
                float yv = Y + (i + 1) * TorreSubida + 1.3f;
                huecos[k].Add(new[] { yv, yv + 2.6f }); huecos[(k + 1) % N].Add(new[] { yv, yv + 2.6f });
                // luz que entra por la ventana
                float rad = ang * Mathf.Deg2Rad;
                LuzCastillo(pa, new Vector3(TorreX + Mathf.Cos(rad) * 8.5f, yv + 1.5f, TorreZ + Mathf.Sin(rad) * 8.5f), new Color(0.55f, 0.5f, 1f), 12f, 1.1f);
            }
            // Salida al jardín: rellano frente a la puerta norte y escalinata recta hasta la terraza del jardín, con pretiles y dos faroles.
            {
                float z0 = TorreZ + TorreR + 0.2f; const int Pasos = 12; float alza = Y / Pasos, huella = 0.48f, ancho = 5.2f;
                Pz(pa, "coop_salida_rellano", PrimitiveType.Cube, new Vector3(TorreX, Y / 2f - 0.15f, z0 + 1.2f), new Vector3(ancho + 0.8f, Y + 0.3f, 2.8f), 0f, matPiedra);
                float zp = z0 + 2.6f;
                for (int i = 0; i < Pasos; i++)
                {
                    float top = Y - alza * (i + 1); float z = zp + huella * (i + 0.5f);
                    Pz(pa, "coop_salida_escalon", PrimitiveType.Cube, new Vector3(TorreX, (top - 1.2f) / 2f, z), new Vector3(ancho, top + 1.2f, huella + 0.02f), 0f, matAdoquin);
                }
                float zf = zp + huella * Pasos;
                foreach (var sx in new[] { -1f, 1f })
                {
                    // pretil inclinado a cada lado
                    Vector3 a = new Vector3(TorreX + sx * (ancho / 2f + 0.25f), Y + 0.5f, z0), b = new Vector3(TorreX + sx * (ancho / 2f + 0.25f), 0.5f, zf);
                    var pr = Pz(pa, "coop_salida_pretil", PrimitiveType.Cube, (a + b) / 2f, new Vector3(0.5f, 1f, (b - a).magnitude + 0.4f), 0f, matPiedra);
                    pr.transform.localRotation = Quaternion.LookRotation(b - a, Vector3.up);
                    Pz(pa, "coop_salida_pilar", PrimitiveType.Cube, new Vector3(TorreX + sx * (ancho / 2f + 0.25f), 0.9f, zf + 0.2f), new Vector3(0.8f, 1.8f, 0.8f), 0f, matPiedraOscura);
                }
                foreach (var sx in new[] { -1f, 1f }) Antorcha(pa, new Vector3(TorreX + sx * 3.4f, Y + 3.2f, TorreZ + TorreR + 0.05f), new Vector3(0f, 0f, 1f));
            }
            // muro cilíndrico (36 tramos) con sus huecos
            for (int k = 0; k < N; k++)
            {
                float ang = k * 10f, rad = ang * Mathf.Deg2Rad;
                Vector3 c = new Vector3(TorreX + Mathf.Cos(rad) * rMed, 0f, TorreZ + Mathf.Sin(rad) * rMed);
                huecos[k].Sort((p, q) => p[0].CompareTo(q[0]));
                float cur = 0f;
                foreach (var h in huecos[k]) { if (h[0] > cur) PiezaTorre(pa, c, ang, cur, h[0]); cur = h[1]; }
                PiezaTorre(pa, c, ang, cur, yTope + 0.3f);
            }
            // columna central (macizo de la escalera)
            Pz(pa, "coop_torre_eje", PrimitiveType.Cylinder, new Vector3(TorreX, yTope / 2f, TorreZ), new Vector3(9f, yTope / 2f, 9f), 0f, matPiedra);   // hasta el sótano
            // escalera de caracol
            float rEsc = (4.5f + TorreR - TorreMuro) / 2f, largoEsc = (TorreR - TorreMuro) - 4.5f + 0.3f;
            for (int i = 0; i < TorrePasos - 1; i++)   // el último peldaño quedaba en el mismo plano que la terraza (se veía "bugeado"): la terraza lo reemplaza
            {
                float ang = TorreAngIni + i * 360f / TorrePasosVuelta, rad = ang * Mathf.Deg2Rad;
                float top = Y + (i + 1) * TorreSubida;
                var g = Pz(pa, "coop_torre_peldano", PrimitiveType.Cube, new Vector3(TorreX + Mathf.Cos(rad) * rEsc, top - 0.35f, TorreZ + Mathf.Sin(rad) * rEsc), new Vector3(largoEsc, 0.7f, 1.8f), -ang, matPiedra);
                if (i % 10 == 5)
                {
                    // antorcha en la cara interior del muro (antes una luz sin nada), salvo delante de una ventana
                    float an = (ang % 360f + 360f) % 360f; int kk = Mathf.RoundToInt(an / 10f) % N; bool libre = true;
                    foreach (int q in new[] { kk, (kk + 1) % N, (kk + N - 1) % N }) foreach (var h in huecos[q]) if (top + 3.2f > h[0] - 0.4f && top + 1.4f < h[1] + 0.4f) libre = false;
                    if (libre) { Vector3 nIn = -new Vector3(Mathf.Cos(kk * 10f * Mathf.Deg2Rad), 0f, Mathf.Sin(kk * 10f * Mathf.Deg2Rad)); Antorcha(pa, new Vector3(TorreX, top + 1.9f, TorreZ) - nIn * (TorreR - TorreMuro + 0.02f), nIn); }
                }
            }
            // terraza: tramos de suelo salvo el hueco por donde sale la escalera
            float angUlt = (TorreAngIni + (TorrePasos - 1) * 360f / TorrePasosVuelta) % 360f; if (angUlt < 0f) angUlt += 360f;
            Anillo(pa, "coop_torre_terraza", new Vector3(TorreX, 0f, TorreZ), yTope - 0.6f, yTope, 4.5f, TorreR + 0.8f, angUlt - 4f, angUlt + 274f, matPiedra);   // sin tablones encimados (el piso parpadeaba)
            // parapeto con almenas
            for (int k = 0; k < N; k++)
            {
                float ang = k * 10f, rad = ang * Mathf.Deg2Rad, rp = TorreR - 0.2f;
                Vector3 c = new Vector3(TorreX + Mathf.Cos(rad) * rp, yTope + 0.65f, TorreZ + Mathf.Sin(rad) * rp);
                Pz(pa, "coop_torre_parapeto", PrimitiveType.Cube, c, new Vector3(0.9f, 1.3f, 2.3f), -ang, matPiedra);
                if (k % 2 == 0) Pz(pa, "coop_torre_almena", PrimitiveType.Cube, c + Vector3.up * 1.15f, new Vector3(1.0f, 1.0f, 1.2f), -ang, matPiedra);
                // cornisa saliente (sin colisión)
                Pz(pa, "coop_torre_cornisa", PrimitiveType.Cube, new Vector3(TorreX + Mathf.Cos(rad) * (TorreR + 0.4f), yTope - 0.9f, TorreZ + Mathf.Sin(rad) * (TorreR + 0.4f)), new Vector3(0.9f, 0.5f, 2.4f), -ang, matPiedra, false);
                if (k % 2 == 1) foreach (var yb in new[] { 17f, 31f, 43f }) Pz(pa, "coop_torre_faja", PrimitiveType.Cube, new Vector3(TorreX + Mathf.Cos(rad) * (TorreR + 0.25f), yb, TorreZ + Mathf.Sin(rad) * (TorreR + 0.25f)), new Vector3(0.6f, 0.4f, 4.4f), -ang, matPiedra, false);
            }
            // techo cónico sobre ocho columnas (abierto a los lados para ver el paisaje) y bandera
            for (int k = 0; k < 8; k++)
            {
                float rad = (k * 45f + 22.5f) * Mathf.Deg2Rad;
                Pz(pa, "coop_torre_columna", PrimitiveType.Cylinder, new Vector3(TorreX + Mathf.Cos(rad) * (TorreR - 1.2f), yTope + 4.5f, TorreZ + Mathf.Sin(rad) * (TorreR - 1.2f)), new Vector3(1.1f, 4f, 1.1f), 0f, matPiedra);
            }
            Pz(pa, "coop_torre_arquitrabe", PrimitiveType.Cylinder, new Vector3(TorreX, yTope + 8.6f, TorreZ), new Vector3((TorreR + 1f) * 2f, 0.4f, (TorreR + 1f) * 2f), 0f, matPiedra, false);
            Cono(pa, "coop_torre_techo", new Vector3(TorreX, yTope + 9f, TorreZ), TorreR + 2.4f, 15f);
            // en vez de la bandera: el Ojo (pedido 2026-11-18)
            try { CrearOjo(pa, new Vector3(TorreX, yTope + 9f + 15f - 1.5f, TorreZ)); } catch (System.Exception e) { plugin.Log("Castillo: el Ojo " + e); }
            try { CrearPalancaOjo(pa, new Vector3(TorreX, yTope, TorreZ)); } catch (System.Exception e) { plugin.Log("Castillo: palanca del Ojo " + e); }
            // (quitada la luz violeta bajo el techo: teñía la terraza y no salía de ningún sitio; ahora alumbran los braseros)
            foreach (var k in new[] { 0, 2, 4, 6 }) { float rad = (k * 45f + 22.5f) * Mathf.Deg2Rad; float rr = TorreR - 3f; float rel = Mathf.DeltaAngle(angUlt, k * 45f + 22.5f); if (rel > -90f && rel < 0f) continue; Brasero(pa, new Vector3(TorreX + Mathf.Cos(rad) * rr, yTope, TorreZ + Mathf.Sin(rad) * rr), 0.8f); }   // braseros (antes luces sin nada)
            // luz y objetos del piso de la torre
            foreach (var deg in new[] { 240f, 300f }) { float r0 = deg * Mathf.Deg2Rad; Vector3 nIn = -new Vector3(Mathf.Cos(r0), 0f, Mathf.Sin(r0)); Antorcha(pa, new Vector3(TorreX, Y + 2.4f, TorreZ) - nIn * (TorreR - TorreMuro + 0.02f), nIn); }
            PropE(pa, 1f, "CandleStand2_nolight", 7.2f, Y, TorreZ - 6f, 0f);
            Estatua(pa, new Vector3(7.2f, Y, TorreZ - 4f), -90f);
            // el candelabro y la estatua del lado oeste quedaban sobre el hueco de la bajada: van al fondo de la torre (125°–140°)
            { float a1 = 140f * Mathf.Deg2Rad, a2 = 124f * Mathf.Deg2Rad;
              Prop(pa, "CandleStand2_nolight", TorreX + Mathf.Cos(a1) * 9.3f, Y, TorreZ + Mathf.Sin(a1) * 9.3f, 0f);
              Estatua(pa, new Vector3(TorreX + Mathf.Cos(a2) * 8.2f, Y, TorreZ + Mathf.Sin(a2) * 8.2f), Mathf.Atan2(-Mathf.Cos(a2), -Mathf.Sin(a2)) * Mathf.Rad2Deg); }
            sitiosCastillo.Add(new Vector3(TorreX + 5.2f, yTope, TorreZ + 4.4f));   // premio por subir (cuenta doble)
            // antorchas en la torre (por fuera): pegadas a la cara del muro y nunca delante de un hueco (antes una quedó flotando en una ventana)
            foreach (var deg in new[] { -90f, -35f, -145f })
                foreach (var yy in new[] { Y + 12f, Y + 30f })
                {
                    float an = (deg % 360f + 360f) % 360f; int k = Mathf.RoundToInt(an / 10f) % N;
                    bool libre = true;
                    foreach (var h in huecos[k]) if (yy + 1.6f > h[0] - 0.5f && yy - 1.2f < h[1] + 0.5f) libre = false;
                    foreach (var h in huecos[(k + 1) % N]) if (yy + 1.6f > h[0] - 0.5f && yy - 1.2f < h[1] + 0.5f) libre = false;
                    foreach (var h in huecos[(k + N - 1) % N]) if (yy + 1.6f > h[0] - 0.5f && yy - 1.2f < h[1] + 0.5f) libre = false;
                    if (!libre) continue;
                    float a = k * 10f * Mathf.Deg2Rad; Vector3 n = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    Antorcha(pa, new Vector3(TorreX, yy, TorreZ) + n * (TorreR - 0.05f), n);
                }
            ConstruirSotanoTorre(pa, Y, rEsc, largoEsc);
            plugin.Log("Castillo: Torre del Homenaje con " + TorrePasos + " peldaños, terraza a " + yTope.ToString("0.0") + " m sobre el suelo del castillo");
        }

        void PiezaTorre(Transform pa, Vector3 c, float ang, float y0, float y1)
        {
            if (y1 - y0 < 0.05f) return;
            Pz(pa, "coop_torre_muro", PrimitiveType.Cube, new Vector3(c.x, (y0 + y1) / 2f, c.z), new Vector3(TorreMuro, y1 - y0, 2.05f), -ang, matPiedra);
        }

        /// Sótano bajo la Torre del Homenaje (pedido 2026-11-17): la bajada es la continuación del mismo caracol de la subida. La subida
        /// arranca en la esquina este del portón (285°) y sube girando hacia ángulos mayores; la bajada arranca en la esquina oeste (255°)
        /// y baja girando hacia ángulos menores, como si la escalera atravesara el suelo. Abajo: pasillo corto, puerta de hierro de la
        /// bóveda (se abre con la llave del jefe de la mina) y, detrás, tres montañas de oro y oro suelto para recoger.
        void ConstruirSotanoTorre(Transform pa, float Y, float rEsc, float largoEsc)
        {
            Vector3 c = new Vector3(TorreX, 0f, TorreZ);
            float rIn = 4.5f, rOut = TorreR - TorreMuro, ySuelo = 0.2f, yTecho = Y - 0.3f;
            const int Bajada = 10;
            float angCierre = TorreBajadaFin + 4f;   // muro detrás del primer peldaño (bajo el rellano del portón)
            // suelo del sótano: desde el fondo de la bóveda hasta detrás del primer peldaño
            Anillo(pa, "coop_sotano_suelo", c, 0f, ySuelo, rIn - 0.05f, rOut + 0.05f, SotanoPuertaAng - 0.5f, angCierre + 1f, matAdoquin);   // tras la puerta empieza el pozo hacia la bóveda gigante
            // bajada: diez peldaños con el mismo paso que la subida (9° y 0,28 m), girando en el mismo sentido
            for (int k = 0; k < Bajada; k++)
            {
                float ang = TorreBajadaFin - 4.5f - k * 360f / TorrePasosVuelta, rad = ang * Mathf.Deg2Rad, top = Y - TorreSubida * (k + 1);
                Pz(pa, "coop_sotano_peldano", PrimitiveType.Cube, new Vector3(c.x + Mathf.Cos(rad) * rEsc, top - 0.35f, c.z + Mathf.Sin(rad) * rEsc), new Vector3(largoEsc, 0.7f, 1.8f), -ang, matPiedra);
                if (k == 5) { Vector3 nIn = -new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)); Antorcha(pa, c - nIn * (rOut + 0.02f) + Vector3.up * (top + 1.9f), nIn); }
            }
            // muros radiales: cierre detrás del primer peldaño, muro con la puerta y fondo de la bóveda
            MuroRadial(pa, c, "coop_sotano_muro", angCierre, rIn, rOut, ySuelo, yTecho);
            float rm = (rIn + rOut) / 2f, anchoP = 2.4f, altoP = 2.3f;
            MuroRadial(pa, c, "coop_sotano_muro", SotanoPuertaAng, rIn, rm - anchoP / 2f, ySuelo, yTecho);
            MuroRadial(pa, c, "coop_sotano_muro", SotanoPuertaAng, rm + anchoP / 2f, rOut, ySuelo, yTecho);
            MuroRadial(pa, c, "coop_sotano_dintel", SotanoPuertaAng, rm - anchoP / 2f, rm + anchoP / 2f, ySuelo + altoP, yTecho);
            float rp = SotanoPuertaAng * Mathf.Deg2Rad;
            Vector3 pp = new Vector3(c.x + Mathf.Cos(rp) * rm, ySuelo + altoP / 2f, c.z + Mathf.Sin(rp) * rm);
            bovedaPuerta = Pz(pa, "coop_boveda_puerta", PrimitiveType.Cube, pp, new Vector3(anchoP, altoP, 0.35f), -SotanoPuertaAng, matHierroC, true);
            Vector3 tg = new Vector3(-Mathf.Sin(rp), 0f, Mathf.Cos(rp));   // hacia ángulos mayores: el lado del pasillo, por donde llega el jugador
            for (int i = 0; i < 3; i++) foreach (var dr in new[] { -0.8f, 0f, 0.8f })
                Pz(pa, "coop_boveda_remache", PrimitiveType.Sphere, pp + new Vector3(Mathf.Cos(rp), 0f, Mathf.Sin(rp)) * dr + Vector3.up * (-0.7f + i * 0.7f) + tg * 0.2f, new Vector3(0.14f, 0.14f, 0.14f), 0f, matHierroC, false);
            var cerr = Pz(pa, "coop_boveda_cerradura", PrimitiveType.Cylinder, pp + tg * 0.22f, new Vector3(0.5f, 0.05f, 0.5f), 0f, MaterialOro(), false);
            cerr.transform.localRotation = Quaternion.LookRotation(tg, Vector3.up) * Quaternion.Euler(90f, 0f, 0f);
            // antorchas en el muro exterior: pasillo y bóveda
            foreach (var ang in new[] { 154f })
            {
                float rad = ang * Mathf.Deg2Rad; Vector3 n = -new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad));
                Antorcha(pa, c - n * (rOut + 0.02f) + Vector3.up * 1.6f, n);
            }
            ConstruirBovedaGigante(pa, rEsc, largoEsc);
            plugin.Log("Castillo: sótano bajo la torre (bajada de " + Bajada + " peldaños desde la esquina oeste del portón, puerta de la bóveda y 3 montañas de oro)");
        }
        readonly List<Vector3> sotanoOroSuelto = new List<Vector3>();

        void MuroRadial(Transform pa, Vector3 c, string nom, float ang, float r0, float r1, float y0, float y1)
        {
            float rad = ang * Mathf.Deg2Rad;
            Pz(pa, nom, PrimitiveType.Cube, new Vector3(c.x + Mathf.Cos(rad) * (r0 + r1) / 2f, (y0 + y1) / 2f, c.z + Mathf.Sin(rad) * (r0 + r1) / 2f), new Vector3(r1 - r0, y1 - y0, 0.7f), -ang, matPiedra);
        }

        void QuitarInterior() { propOrig.Clear(); propPend.Clear(); }

        // ---------------- Solo existe cerca (rendimiento) ----------------

        List<Light> lucesCastillo; GameObject lucesCastilloDe; bool lucesCastilloOn = true;
        float proxVisCastillo; public int ForzarCastillo = -1;   // -1 automático, 0 apagado, 1 encendido (orden de prueba "castillovis")
        const float RadioVisCastillo = 1500f, RadioLucesCastillo = 450f;   // la estructura se ve desde lejos (si no, desde la entrada de la zona nueva "no existe"); las luces solo cerca

        /// Montaña, castillo y laberinto suman miles de piezas y más de cien luces: se apagan cuando los dos jugadores están a más de 650 m.
        void ActualizarVisibilidadCastillo()
        {
            if (Time.time < proxVisCastillo) return;
            proxVisCastillo = Time.time + 0.5f;
            if (castilloRaiz == null || nuevo == null) return;
            Vector3 c = nuevo.transform.position + new Vector3(CastilloX, 0f, CentroMonte.y);
            var j = plugin.Jugador; var comp = plugin.CompaneroActual;
            bool cerca = j == null;
            if (j != null) { Vector3 d = j.transform.position - c; d.y = 0f; if (d.magnitude < RadioVisCastillo) cerca = true; }
            if (comp != null && comp.Visible) { Vector3 d = comp.PosicionMundo - c; d.y = 0f; if (d.magnitude < RadioVisCastillo) cerca = true; }
            if (ForzarCastillo >= 0) cerca = ForzarCastillo == 1;
            if (castilloRaiz.activeSelf != cerca) { castilloRaiz.SetActive(cerca); plugin.Log("Castillo: " + (cerca ? "encendido" : "apagado (lejos)")); }
            if (laberintoRaiz != null && laberintoRaiz.activeSelf != cerca) laberintoRaiz.SetActive(cerca);
            // luces del castillo y del laberinto (más de cien): solo importan a menos de ~450 m (cada una alumbra 15-17 m)
            if (cerca)
            {
                float dm = 1e9f;
                if (j != null) { Vector3 d = j.transform.position - c; d.y = 0f; dm = Mathf.Min(dm, d.magnitude); }
                if (comp != null && comp.Visible) { Vector3 d = comp.PosicionMundo - c; d.y = 0f; dm = Mathf.Min(dm, d.magnitude); }
                bool lucesOn = dm < RadioLucesCastillo;
                if (lucesCastillo == null || lucesCastilloDe != castilloRaiz)
                {
                    lucesCastillo = new List<Light>(castilloRaiz.GetComponentsInChildren<Light>(true));
                    if (laberintoRaiz != null) lucesCastillo.AddRange(laberintoRaiz.GetComponentsInChildren<Light>(true));
                    lucesCastilloDe = castilloRaiz; lucesCastilloOn = !lucesOn;
                }
                if (lucesOn != lucesCastilloOn)
                {
                    lucesCastilloOn = lucesOn;
                    foreach (var l in lucesCastillo) if (l != null) l.enabled = lucesOn;
                    plugin.Log("Castillo: luces " + (lucesOn ? "encendidas" : "apagadas (lejos)"));
                }
            }
        }
    }
}

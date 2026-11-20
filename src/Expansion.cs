// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace WitchHuntCoop
{
    /// Expansión del bosque hacia el norte (solo en cooperativo):
    ///  - Terreno nuevo de 1000 × 1000 m pegado al borde norte, con la misma densidad que el original.
    ///    Relieve, texturas, árboles y pasto se copian de un bloque del interior elegido por la semilla
    ///    (la zona nueva cambia en cada cacería). Los bordes exteriores suben a montaña.
    ///  - Se abre un paso en la loma del borde norte (x 550–950) que baja al llano, y la costura con el
    ///    terreno nuevo coincide en altura. Al salir del bosque el terreno original se restaura.
    ///  - Mapa de caminos (NavMesh) del terreno nuevo horneado en segundo plano y unido al original con
    ///    enlaces, para que enemigos y reparto de botín lleguen.
    public partial class Mundo
    {
        public const float AnchoNuevo = 3000f, FondoNuevo = 1500f;   // 2026-11-11: el mapa entero mide 3000 × 3000 m (original 2000 × 2000 + tira norte de 3000 × 1000)
        const float _viejo = 0f;  //   // 2026-11-10: de 1300 a 1500 (el máximo que cabe en el bloque de origen de 2000 m)
        public const float NuevoX0 = 0f;              // el terreno nuevo cubre x 0–3000
        public const float AnchoPaso = 260f, PasoBorde = 80f;   // ancho del valle (y transición a los lados)
        static float PasoX0 = 550f, PasoX1 = 950f;               // se eligen en ElegirPaso
        float pasoZ0, pasoAltura;                                // dónde empieza el valle y a qué altura
        readonly List<Vector3> alcanzables = new List<Vector3>();
        const float Pared = 110f;                      // ancho de la subida a montaña en los bordes nuevos

        Terrain nuevo; TerrainData datosNuevos;
        NavMeshData navNuevo; NavMeshDataInstance navInstancia; AsyncOperation navOp;
        readonly List<NavMeshLinkInstance> enlaces = new List<NavMeshLinkInstance>();
        int semillaExpansion;
        bool expansionLista;
        static readonly Dictionary<TerrainData, float[,]> respaldo = new Dictionary<TerrainData, float[,]>();
        static TerrainData datosOriginal;

        /// ¿Está ese punto (relativo al terreno original) dentro del valle que lleva a la zona nueva? Ahí no hay árboles.
        public static bool EnPasoNorte(float x, float z) { return x > PasoX0 - 25f && x < PasoX1 + 25f && z > ZPasoSinArboles; }
        static float ZPasoSinArboles = 1700f;

        /// Distancia (coordenadas del terreno norte) al camino que va del paso a la entrada del laberinto: sin árboles a menos de 14 m.
        float DistCaminoZonaNueva(float x, float z)
        {
            float cx = (PasoX0 + PasoX1) / 2f; Vector2 p = new Vector2(x, z), e = new Vector2(labEntradaPos.x, labEntradaPos.y - 8f);
            Vector2[] a = { new Vector2(cx, 0f), new Vector2(cx, 60f) }, b = { new Vector2(cx, 60f), e };
            float mejor = 1e9f;
            foreach (var sg in new[] { a, b })
            {
                Vector2 ab = sg[1] - sg[0]; float t = Mathf.Clamp01(Vector2.Dot(p - sg[0], ab) / Mathf.Max(0.01f, ab.sqrMagnitude));
                mejor = Mathf.Min(mejor, Vector2.Distance(p, sg[0] + ab * t));
            }
            return mejor;
        }

        /// Altura interpolada de la rejilla (fila z, columna x en índices fraccionarios).
        static float Muestra(float[,] h, int R, float zf, float xf)
        {
            zf = Mathf.Clamp(zf, 0f, R - 1.001f); xf = Mathf.Clamp(xf, 0f, R - 1.001f);
            int z0 = (int)zf, x0 = (int)xf; float fz = zf - z0, fx = xf - x0;
            float a = Mathf.Lerp(h[z0, x0], h[z0, x0 + 1], fx), b = Mathf.Lerp(h[z0 + 1, x0], h[z0 + 1, x0 + 1], fx);
            return Mathf.Lerp(a, b, fz);
        }

        /// El jefe Crux tiene paredes invisibles propias ("Trigger_Cruxblocker"); la del norte cae dentro
        /// de la zona nueva: se corre más allá para que los jefes puedan entrar.
        void CorrerBloqueadorJefe(Vector3 o, float bordeNorte)
        {
            foreach (var g in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (!g.scene.IsValid() || !g.name.StartsWith("Trigger_Cruxblocker")) continue;
                if (g.transform.position.z - o.z < bordeNorte) continue;
                var p = g.transform.position;
                g.transform.position = new Vector3(p.x, p.y, o.z + bordeNorte + FondoNuevo + 300f);
                plugin.Log("Expansión: bloqueador del jefe " + g.name + " corrido a z " + Mathf.RoundToInt(g.transform.position.z - o.z));
            }
        }

        /// Devuelve true cuando la expansión de esta semilla está lista (navmesh incluido).
        bool PrepararExpansion(int semilla)
        {
            if (semillaExpansion == semilla) return expansionLista;
            semillaExpansion = semilla; expansionLista = false;
            QuitarExpansion();
            try { CrearTerreno(semilla); }
            catch (System.Exception e) { plugin.Log("Expansión: error al crear el terreno: " + e); expansionLista = true; return true; }
            return false;
        }

        void ActualizarExpansion()
        {
            if (navOp == null || expansionLista) return;
            if (!navOp.isDone) return;
            navOp = null;
            Unir();
            expansionLista = true;
            plugin.Log("Expansión: mapa de caminos listo");
        }

        void CrearTerreno(int semilla)
        {
            var orig = Anclaje.Principal();
            if (orig == null) return;
            var d = orig.terrainData;
            datosOriginal = d;
            float t0 = Time.realtimeSinceStartup;

            // ---- 0. Elegir el paso: donde el bosque es transitable más cerca del río ----
            if (respaldo.ContainsKey(d)) d.SetHeights(0, 0, respaldo[d]);   // medir sobre el terreno limpio
            ElegirPaso(orig.transform.position, d.size.z);
            ZPasoSinArboles = pasoZ0 - 60f;

            // ---- 1. Paso en la loma (sobre una copia limpia del original) ----
            int R = d.heightmapResolution;
            float[,] h;
            if (!respaldo.TryGetValue(d, out h)) { h = d.GetHeights(0, 0, R, R); respaldo[d] = (float[,])h.Clone(); }
            h = (float[,])respaldo[d].Clone();
            float paso = d.size.x / (R - 1);
            // El borde norte tiene un río (bajo el agua global, que mata) y una loma. En el paso se
            // arma un terraplén a la altura del llano interior: rellena el río y rebaja la loma.
            // Valle de altura constante desde el último punto transitable hasta el borde, con
            // laderas suaves: corta colinas y rellena el río.
            float valle = Mathf.Max(NivelAgua() + 5f, pasoAltura) - orig.transform.position.y;
            valle /= d.size.y;
            // Lugares únicos del juego dentro del paso (la casa del árbol): sus mallas están horneadas y no se pueden mover, así que el
            // suelo bajo ellos NO se rebaja (queda un montículo con su altura original que se funde con el valle en 25 m).
            var protegidos = new List<Vector3>();
            foreach (var g in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (g == null || !g.activeInHierarchy || System.Array.IndexOf(Unicos, Catalogo.BaseDe(g.name)) < 0) continue;
                Bounds bu; if (!CajaDe(new List<GameObject> { g }, out bu)) continue;
                float ux = bu.center.x - orig.transform.position.x, uz = bu.center.z - orig.transform.position.z, ur = Mathf.Max(bu.extents.x, bu.extents.z);
                if (ux < PasoX0 - PasoBorde - ur - 30f || ux > PasoX1 + PasoBorde + ur + 30f || uz < pasoZ0 - 110f - ur) continue;
                protegidos.Add(new Vector3(ux, uz, ur));
                plugin.Log("Expansión: el paso no rebaja el suelo bajo " + g.name + " (radio " + ur.ToString("0") + " m)");
            }
            for (int xi = 0; xi < R; xi++)
            {
                float x = xi * paso;
                float m = Mascara(x);
                if (m <= 0f) continue;
                for (int zi = 0; zi < R; zi++)
                {
                    float z = zi * paso;
                    if (z < pasoZ0 - 80f) continue;
                    float f = m * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(pasoZ0 - 80f, pasoZ0, z));
                    foreach (var pr in protegidos)
                    {
                        float dd = Mathf.Sqrt((x - pr.x) * (x - pr.x) + (z - pr.y) * (z - pr.y));
                        f *= Mathf.SmoothStep(0f, 1f, (dd - pr.z) / 25f);
                    }
                    h[zi, xi] = Mathf.Lerp(h[zi, xi], valle, f);
                }
            }
            d.SetHeights(0, 0, h);
            AjustarObjetosAlPaso(orig, respaldo[d], h, R, paso);
            // a distancia Unity simplifica la malla del terreno y en las lomas queda MÁS BAJA que la altura real, mientras la hierba se pone
            // a la altura real: se veían franjas de hierba flotando (pedido 2026-11-17). Con error 1 la malla sigue el relieve como el terreno nuevo.
            if (pixelErrorOriginal < 0f) pixelErrorOriginal = orig.heightmapPixelError;
            orig.heightmapPixelError = 1f;
            plugin.Log("Expansión: terreno original con detalle máximo (error de píxel " + pixelErrorOriginal + " → 1)");

            AbrirBloqueadorRio(orig.transform.position);
            CorrerBloqueadorJefe(orig.transform.position, d.size.z);
            AbrirJaula(orig.transform.position, d.size.z);
            OcultarTelones(orig.transform.position, d.size.z);
            QuitarCubosNorte(orig.transform.position, d.size.z);

            // ---- 2. Terreno nuevo (2026-11-11): llano de tierra quemada con UNA montaña, árboles gigantes cada ~100 m y rocas carbonizadas ----
            var rng = new System.Random(semilla ^ Hash("coop_expansion"));
            const int rn = 1025;
            float pasoN = AnchoNuevo / (rn - 1), pasoNZ = FondoNuevo / (rn - 1);   // el terreno es de 3000 × 1000: la rejilla es cuadrada pero el paso en z es más corto
            float[,] hn = new float[rn, rn];
            float baseH = Mathf.Clamp01((NivelAgua() + 9f - orig.transform.position.y) / d.size.y); sueloLlanoY = orig.transform.position.y + baseH * d.size.y;
            float pisoFoso = Mathf.Clamp01((NivelAgua() + 1.6f - orig.transform.position.y) / d.size.y);   // el fondo del foso queda sobre el agua del juego (si no, se ve agua dentro del foso)   // llano a ~9 m sobre el agua (no a la altura del paso: ahí podía quedar en lo alto de una loma)
            // la montaña va en la parte sur de la zona (z 250–600): los 400 m del norte son del castillo
            float mcx = AnchoNuevo * 0.45f + (float)rng.NextDouble() * AnchoNuevo * 0.3f, mcz = 40f + (float)rng.NextDouble() * 50f, mr = 260f;
            GenerarLaberinto(new System.Random(semilla ^ Hash("coop_laberinto")));
            float offX = (float)rng.NextDouble() * 1000f, offZ = (float)rng.NextDouble() * 1000f;
            for (int zi = 0; zi < rn; zi++)
                for (int xi = 0; xi < rn; xi++)
                {
                    float x = xi * pasoN, z = zi * pasoNZ;
                    float v = baseH + Mathf.PerlinNoise(offX + x * 0.004f, offZ + z * 0.004f) * 0.02f;
                    {   // lomas y hondonadas en el llano (pedido 2026-11-17: el llano plano y vacío se veía como un hueco enorme)
                        float dcx0 = x - CentroMonte.x, dcz0 = z - CentroMonte.y, dc0 = Mathf.Sqrt(dcx0 * dcx0 + dcz0 * dcz0);
                        float wl = Mathf.SmoothStep(0f, 1f, (dc0 - PitOut - 60f) / 140f) * Mathf.SmoothStep(0f, 1f, (DistCaminoZonaNueva(x, z) - 18f) / 60f) * Mathf.SmoothStep(0f, 1f, (z - 160f) / 120f);
                        if (wl > 0f)
                        {
                            float l = Mathf.PerlinNoise(offX * 1.7f + x * 0.006f, offZ * 1.3f + z * 0.006f), l2 = Mathf.PerlinNoise(offX + x * 0.018f, offZ + z * 0.018f);
                            v += wl * (Mathf.Max(0f, l - 0.35f) * 60f + (l2 - 0.5f) * 6f) / d.size.y;
                        }
                    }
                    float dm = Mathf.Sqrt((x - mcx) * (x - mcx) + (z - mcz) * (z - mcz));
                    if (dm < mr) v += 0.55f * Mathf.SmoothStep(0f, 1f, 1f - dm / mr) * (0.75f + 0.25f * Mathf.PerlinNoise(offX + x * 0.012f, offZ + z * 0.012f));
                    // llano alrededor del foso, montaña del castillo (cima llana de 120 m de radio y 120 m de alto) y foso negro con el laberinto como único puente
                    {
                        float dcx = x - CentroMonte.x, dcz = z - CentroMonte.y; float dc = Mathf.Sqrt(dcx * dcx + dcz * dcz);
                        v = Mathf.Lerp(v, baseH, 1f - Mathf.SmoothStep(0f, 1f, (dc - PitOut) / 80f));
                        if (dc < MonteBase)
                        {
                            float ru = Mathf.PerlinNoise(offX + x * 0.02f, offZ + z * 0.02f) - 0.5f;
                            v = baseH + AlturaMonte(dc, ru) / d.size.y;
                            // terraza llana al norte del castillo hasta la muralla (pedido 2026-11-17: jardín en toda esa zona)
                            float dzN = z - CentroMonte.y;
                            if (dzN > 0f)
                            {
                                float wt = Mathf.SmoothStep(0f, 1f, (dzN - 25f) / 30f) * (1f - Mathf.SmoothStep(0f, 1f, (dc - 172f) / 26f));
                                v = Mathf.Max(v, Mathf.Lerp(v, baseH + MonteAlto / d.size.y, wt));
                            }
                        }
                        v = Mathf.Lerp(v, pisoFoso, PesoFoso(x, z));
                        v = TerrenoEscalinata(x, z, v, baseH, Mathf.Min(1f, baseH + MonteAlto / d.size.y));   // pasillo recto de la escalinata
                    }
                    // Costura: los primeros metros se funden con la fila norte del original.
                    float borde = Muestra(h, R, R - 1, Mathf.Min(x, d.size.x - 1f) / paso);
                    float fc = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, Mascara(NuevoX0 + x) > 0f ? 150f : 90f, z));
                    v = Mathf.Lerp(borde, v, fc);
                    // Montañas en los bordes (y al sur, donde no hay terreno original debajo).
                    float dist = Mathf.Min(x, AnchoNuevo - x, FondoNuevo - z);
                    // Borde sur donde no hay terreno original debajo (más allá del este del mapa viejo): la montaña sube de forma gradual desde 250 m
                    // antes del límite (antes empezaba a 50 m y la fila sur del original, baja, dejaba un acantilado liso de 100 m con el vacío detrás)
                    float pesoSur = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(d.size.x - 250f, d.size.x, NuevoX0 + x));
                    if (dist < Pared)
                    {
                        float fm = Mathf.SmoothStep(0f, 1f, 1f - dist / Pared);
                        v = Mathf.Max(v, Mathf.Lerp(v, 0.98f, fm));
                    }
                    if (pesoSur > 0f && z < Pared)
                    {
                        float fs = Mathf.SmoothStep(0f, 1f, 1f - z / Pared) * pesoSur;
                        v = Mathf.Max(v, Mathf.Lerp(v, 0.98f, fs));
                    }
                    hn[zi, xi] = Mathf.Clamp01(v);
                }
            for (int xi = 0; xi < rn; xi++)
            {
                // la fila de la costura es idéntica a la del original... salvo donde ya no hay original debajo: ahí es la montaña del borde sur
                float xw = xi * pasoN;
                float pesoSur = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(d.size.x - 250f, d.size.x, NuevoX0 + xw));
                float orig0 = Muestra(h, R, R - 1, Mathf.Min(xw, d.size.x - 1f) / paso);
                hn[0, xi] = Mathf.Lerp(orig0, hn[0, xi], pesoSur);
            }

            var dn = new TerrainData();
            dn.heightmapResolution = rn;
            dn.size = new Vector3(AnchoNuevo, d.size.y, FondoNuevo);
            dn.SetHeights(0, 0, hn);

            // Suelo: capa propia de tierra quemada (el borde sur conserva el suelo del original para que la costura no se note)
            var protos = new List<SplatPrototype>(d.splatPrototypes);
            protos.Add(new SplatPrototype { texture = TexCarbon(), tileSize = new Vector2(14f, 14f) });
            protos.Add(new SplatPrototype { texture = TexNegra(), tileSize = new Vector2(20f, 20f) });
            dn.splatPrototypes = protos.ToArray();
            const int ar = 1024;
            dn.alphamapResolution = ar;
            dn.baseMapResolution = d.baseMapResolution;
            int capas = d.splatPrototypes.Length;
            var filaNorte = d.GetAlphamaps(0, d.alphamapHeight - 1, d.alphamapWidth, 1);   // suelo de la última fila del original
            var amN = new float[ar, ar, capas + 2];
            for (int zi = 0; zi < ar; zi++)
                for (int xi = 0; xi < ar; xi++)
                {
                    float x = xi * AnchoNuevo / (ar - 1), z = zi * FondoNuevo / (ar - 1);
                    float w = NuevoX0 + x <= d.size.x ? 1f - Mathf.SmoothStep(0f, 1f, z / 140f) : 0f;
                    int ox = Mathf.Clamp(Mathf.RoundToInt((NuevoX0 + x) / d.size.x * (d.alphamapWidth - 1)), 0, d.alphamapWidth - 1);
                    float suma = 0f;
                    for (int c = 0; c < capas; c++) { float a0 = filaNorte[0, ox, c] * w; amN[zi, xi, c] = a0; suma += a0; }
                    float fo = PesoFoso(x, z);
                    for (int c = 0; c < capas; c++) amN[zi, xi, c] *= (1f - fo);
                    amN[zi, xi, capas] = Mathf.Max(0f, 1f - suma) * (1f - fo);
                    amN[zi, xi, capas + 1] = fo;
                }
            dn.SetAlphamaps(0, 0, amN);

            // Pasto: ninguno (todo quemado)
            dn.detailPrototypes = d.detailPrototypes;
            dn.SetDetailResolution(256, 16);

            // Árboles sumamente gigantes, uno cada ~100 m (cuadrícula con desvío)
            dn.treePrototypes = d.treePrototypes;
            var arboles = new List<TreeInstance>();
            var nombres = new System.Text.StringBuilder();
            for (int i = 0; i < d.treePrototypes.Length; i++) nombres.Append(i).Append('=').Append(d.treePrototypes[i].prefab != null ? d.treePrototypes[i].prefab.name : "?").Append(' ');
            for (float gz = 90f; gz < FondoNuevo - Pared - 20f; gz += 52f)   // más árboles grandes: una cuadrícula de 52 m (antes 100) para que el mapa no se vea pelado
                for (float gx = Pared + 20f; gx < AnchoNuevo - Pared - 20f; gx += 52f)
                {
                    float x = gx + ((float)rng.NextDouble() - 0.5f) * 40f, z = gz + ((float)rng.NextDouble() - 0.5f) * 40f;
                    if (EnZonaCastillo(x, z, 40f) || DistCaminoZonaNueva(x, z) < 14f) continue;   // sin árboles alrededor de la montaña y el laberinto (pedido 2026-11-12)
                    float ev = 4f + (float)rng.NextDouble() * 2f;   // 4–6 veces el tamaño normal
                    arboles.Add(new TreeInstance { prototypeIndex = rng.Next(d.treePrototypes.Length), position = new Vector3(x / AnchoNuevo, 0f, z / FondoNuevo), widthScale = ev, heightScale = ev, color = Color.white, lightmapColor = Color.white });
                }
            // pinos normales en la falda de la montaña (no sobre el camino de adoquines)
            for (int k = 0; k < 0; k++)   // sin pinos en la falda (pedido 2026-11-12)
            {
                float ang = (float)rng.NextDouble() * Mathf.PI * 2f, rd = MonteMeseta + 12f + (float)rng.NextDouble() * (MonteBase - MonteMeseta - 24f);
                float x = CastilloX + Mathf.Cos(ang) * rd, z = CentroMonte.y + Mathf.Sin(ang) * rd;
                bool cerca = false;
                if (labRuta != null) for (int s2 = 0; s2 < labRuta.Length - 1 && !cerca; s2++)
                {
                    Vector2 a2 = labRuta[s2], b2 = labRuta[s2 + 1], p2 = new Vector2(x, z), ab2 = b2 - a2;
                    float tt = Mathf.Clamp01(Vector2.Dot(p2 - a2, ab2) / Mathf.Max(0.01f, ab2.sqrMagnitude));
                    if (Vector2.Distance(p2, a2 + ab2 * tt) < 9f) cerca = true;
                }
                if (cerca) continue;
                float ev = 1.2f + (float)rng.NextDouble() * 1.2f;
                arboles.Add(new TreeInstance { prototypeIndex = rng.Next(d.treePrototypes.Length), position = new Vector3(x / AnchoNuevo, 0f, z / FondoNuevo), widthScale = ev, heightScale = ev, color = Color.white, lightmapColor = Color.white });
            }
            // anillo de árboles gigantes alrededor de la montaña, el foso y el laberinto (pedido 2026-11-13): cada ~26 m en una banda de 230 m
            for (float ang = 0f; ang < 360f; ang += 1.2f)
                for (float rd = PitOut + 30f; rd < PitOut + 260f; rd += 26f)
                {
                    float aa = (ang + rng.Next(0, 10)) * Mathf.Deg2Rad, rr = rd + ((float)rng.NextDouble() - 0.5f) * 20f;
                    float x = CastilloX + Mathf.Cos(aa) * rr, z = CentroMonte.y + Mathf.Sin(aa) * rr;
                    if (x < Pared + 20f || x > AnchoNuevo - Pared - 20f || z < 100f || z > FondoNuevo - Pared - 20f || DistCaminoZonaNueva(x, z) < 14f) continue;
                    if ((ang * 7 + (int)rd) % 3 != 0) continue;   // un tercio de los puntos: denso pero con huecos
                    if (Mathf.Abs(x - labEntradaPos.x) < 16f && z < labEntradaPos.y + 5f) continue;   // el camino a la entrada del laberinto queda libre
                    float ev = 4f + (float)rng.NextDouble() * 2f;
                    arboles.Add(new TreeInstance { prototypeIndex = rng.Next(d.treePrototypes.Length), position = new Vector3(x / AnchoNuevo, 0f, z / FondoNuevo), widthScale = ev, heightScale = ev, color = Color.white, lightmapColor = Color.white });
                }
            // bosque en las laderas de los bordes (pedido 2026-11-17): la subida de 110 m quedaba pelada y la esquina se veía como un valle en V
            // de paredes lisas; con árboles hasta el borde se ve como el bosque natural del resto del mapa
            int enLaderas = 0;
            for (float gz = 104f; gz < FondoNuevo - 4f; gz += 38f)
                for (float gx = 6f; gx < AnchoNuevo - 4f; gx += 38f)
                {
                    bool enBorde = gx < Pared + 20f || gx > AnchoNuevo - Pared - 20f || gz > FondoNuevo - Pared - 20f;
                    if (!enBorde) continue;
                    float x = Mathf.Clamp(gx + ((float)rng.NextDouble() - 0.5f) * 30f, 4f, AnchoNuevo - 4f), z = Mathf.Clamp(gz + ((float)rng.NextDouble() - 0.5f) * 30f, 104f, FondoNuevo - 4f);
                    if (dn.GetSteepness(x / AnchoNuevo, z / FondoNuevo) > 46f) continue;
                    if (EnZonaCastillo(x, z, 40f) || DistCaminoZonaNueva(x, z) < 14f) continue;
                    float ev = 2.6f + (float)rng.NextDouble() * 2.2f;
                    arboles.Add(new TreeInstance { prototypeIndex = rng.Next(d.treePrototypes.Length), position = new Vector3(x / AnchoNuevo, 0f, z / FondoNuevo), widthScale = ev, heightScale = ev, color = Color.white, lightmapColor = Color.white });
                    enLaderas++;
                }
            plugin.Log("Expansión: " + enLaderas + " árboles en las laderas de los bordes");
            // bosquecillos de árboles normales en el llano (pedido 2026-11-17): manchas de bosque denso separadas por claros
            int enLlano = 0;
            for (float gz = 140f; gz < FondoNuevo - Pared; gz += 15f)
                for (float gx = Pared; gx < AnchoNuevo - Pared; gx += 15f)
                {
                    if (Mathf.PerlinNoise(offX * 0.7f + gx * 0.008f, offZ * 0.9f + gz * 0.008f) < 0.52f) continue;
                    float x = gx + ((float)rng.NextDouble() - 0.5f) * 12f, z = gz + ((float)rng.NextDouble() - 0.5f) * 12f;
                    if (EnZonaCastillo(x, z, 50f) || DistCaminoZonaNueva(x, z) < 14f) continue;
                    if (dn.GetSteepness(x / AnchoNuevo, z / FondoNuevo) > 40f) continue;
                    float ev = 1.3f + (float)rng.NextDouble() * 1.3f;
                    arboles.Add(new TreeInstance { prototypeIndex = rng.Next(d.treePrototypes.Length), position = new Vector3(x / AnchoNuevo, 0f, z / FondoNuevo), widthScale = ev, heightScale = ev, color = Color.white, lightmapColor = Color.white });
                    enLlano++;
                }
            plugin.Log("Expansión: " + enLlano + " árboles en bosquecillos del llano");
            dn.treeInstances = arboles.ToArray();
            dn.RefreshPrototypes();
            var ta = dn.treeInstances;
            for (int i = 0; i < ta.Length; i++) ta[i].position.y = dn.GetInterpolatedHeight(ta[i].position.x, ta[i].position.z) / dn.size.y;
            dn.treeInstances = ta;
            plugin.Log("Expansión: tipos de árbol disponibles: " + nombres);

            var go = Terrain.CreateTerrainGameObject(dn);
            { var tc = go.GetComponent<TerrainCollider>(); if (tc != null) tc.terrainData = dn; }   // la colisión sigue a la malla visible (no se atraviesa nada)
            go.name = "Coop Terreno Norte";
            go.transform.position = orig.transform.position + new Vector3(NuevoX0, 0f, d.size.z);
            nuevo = go.GetComponent<Terrain>();
            datosNuevos = dn;
            nuevo.materialType = orig.materialType;
            nuevo.materialTemplate = orig.materialTemplate;
            nuevo.basemapDistance = orig.basemapDistance;
            nuevo.heightmapPixelError = 1f;   // máximo detalle: con un error mayor la malla que se ve queda por encima de la colisión en las lomas (parece suelo que se atraviesa)
            nuevo.treeDistance = orig.treeDistance;
            nuevo.treeBillboardDistance = Mathf.Max(orig.treeBillboardDistance, 400f);   // los árboles cercanos son modelos, no tarjetas negras
            nuevo.treeCrossFadeLength = orig.treeCrossFadeLength;
            nuevo.treeMaximumFullLODCount = Mathf.Max(orig.treeMaximumFullLODCount, 200);
            nuevo.detailObjectDistance = orig.detailObjectDistance;
            nuevo.detailObjectDensity = orig.detailObjectDensity;
            nuevo.castShadows = orig.castShadows;
            nuevo.reflectionProbeUsage = orig.reflectionProbeUsage;
            nuevo.legacyShininess = orig.legacyShininess;
            nuevo.legacySpecular = orig.legacySpecular;
            go.layer = orig.gameObject.layer;
            go.tag = orig.gameObject.tag;
            // Sin SetNeighbors: solo sirve entre terrenos de igual tamaño y resolución.
            ColocarRocasCarbonizadas(go, dn, rng, mcx, mcz, mr);
            // primero el llano (cambia la altura del borde) y después el bosque exterior, que arranca a la altura del borde: si no, quedaba un escalón de ~55 m
            try { AplanarLlanoEste(); } catch (System.Exception e) { plugin.Log("Llano este: error " + e); }
            try { CrearBosqueExterior(orig, nuevo, rng); } catch (System.Exception e) { plugin.Log("Bosque exterior: error " + e); }
            try { ConstruirCastillo(go, dn); } catch (System.Exception e) { plugin.Log("Castillo: error " + e); }
            plugin.Log("Expansión: terreno norte " + AnchoNuevo + " × " + FondoNuevo + " m creado, " + arboles.Count + " árboles gigantes, montaña en (" + Mathf.RoundToInt(mcx) + ", " + Mathf.RoundToInt(mcz) + "), en " + (Time.realtimeSinceStartup - t0).ToString("0.0") + " s");

            // ---- 3. NavMesh del terreno nuevo (y del paso), en segundo plano ----
            Vector3 o = orig.transform.position;
            // Puntos de partida de los enlaces: el último suelo con camino ORIGINAL antes del río
            // (se buscan antes de agregar el camino nuevo, que se superpone en el terraplén).
            BuscarOrigenesEnlaces(o, d.size.z);
            const float Sur = 0f;   // solo el terreno nuevo: sin superponerse al camino original
            var limites = new Bounds(o + new Vector3(NuevoX0 + AnchoNuevo / 2f, 80f, d.size.z + (FondoNuevo - Sur) / 2f), new Vector3(AnchoNuevo, 240f, FondoNuevo + Sur));
            var fuentes = new List<NavMeshBuildSource>();
            NavMeshBuilder.CollectSources(limites, ~0, NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), fuentes);
            var ajustes = NavMesh.GetSettingsByIndex(0);
            ajustes.overrideVoxelSize = true; ajustes.voxelSize = 0.5f;
            ajustes.overrideTileSize = true; ajustes.tileSize = 256;
            navNuevo = new NavMeshData(ajustes.agentTypeID);
            navInstancia = NavMesh.AddNavMeshData(navNuevo);
            navOp = NavMeshBuilder.UpdateNavMeshDataAsync(navNuevo, ajustes, fuentes, limites);
            plugin.Log("Expansión: horneando mapa de caminos (" + fuentes.Count + " fuentes)...");
        }

        // ---------------- Tierra quemada y rocas carbonizadas ----------------

        float sueloLlanoY;   // altura (mundo) del llano del terreno norte: por debajo, dentro del foso, mueres
        static Texture2D texNegra;
        static Texture2D TexNegra()
        {
            if (texNegra != null) return texNegra;
            var t = new Texture2D(8, 8, TextureFormat.RGB24, true) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Repeat };
            for (int y = 0; y < 8; y++) for (int x = 0; x < 8; x++) t.SetPixel(x, y, Color.black);
            t.Apply(); return texNegra = t;
        }
        static Texture2D texCarbon;
        static Texture2D TexCarbon()
        {
            if (texCarbon != null) return texCarbon;
            const int N = 256;
            var t = new Texture2D(N, N, TextureFormat.ARGB32, true) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Repeat };
            var rng = new System.Random(11);
            for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
            {
                float n = Mathf.PerlinNoise(x * 0.06f, y * 0.06f) * 0.5f + Mathf.PerlinNoise(x * 0.25f, y * 0.25f) * 0.5f;
                float g = 0.035f + n * 0.07f;
                Color c = new Color(g, g * 0.93f, g * 0.88f, 1f);
                if (rng.NextDouble() < 0.0012) c = new Color(0.55f, 0.16f, 0.04f, 1f);   // alguna brasa
                t.SetPixel(x, y, c);
            }
            t.Apply();
            return t;
        }

        /// Rocas del bosque copiadas y ennegrecidas, repartidas por la zona nueva y más juntas en la montaña.
        void ColocarRocasCarbonizadas(GameObject terreno, TerrainData dn, System.Random rng, float mcx, float mcz, float mr)
        {
            GameObject modelo = null;
            foreach (var g in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                string b = Catalogo.BaseDe(g.name);
                if (b != "Rock1" && b != "Rock5" && b != "Rock9" && b != "Rock10" && b != "Rock13") continue;
                int hh, ee; Coop.Piezas.Calidad(g, out hh, out ee);
                if (ee == hh) { modelo = g; break; }
            }
            if (modelo == null) { plugin.Log("Expansión: no hay una roca que se pueda copiar"); return; }
            var oscuro = new Material(Shader.Find("Standard")) { name = "coop_roca_carbon", color = new Color(0.07f, 0.06f, 0.055f) };
            oscuro.SetFloat("_Glossiness", 0.05f);
            int puestas = 0; var tintadas = new Dictionary<Material, Material>();
            for (int i = 0; i < 160; i++)
            {
                float x, z;
                if (i < 60) { float ang = (float)rng.NextDouble() * Mathf.PI * 2f, d2 = (float)rng.NextDouble() * mr * 1.1f; x = mcx + Mathf.Cos(ang) * d2; z = mcz + Mathf.Sin(ang) * d2; }
                else { x = Pared + 30f + (float)rng.NextDouble() * (AnchoNuevo - 2f * Pared - 60f); z = 120f + (float)rng.NextDouble() * (FondoNuevo - Pared - 160f); }
                if (x < Pared || x > AnchoNuevo - Pared || z < 100f || z > FondoNuevo - Pared) continue;
                if (EnZonaCastillo(x, z, 20f)) continue;
                if (Mathf.Abs(x - EscX) < 18f && z > EscZ0 - 16f && z < EscZ2 + 4f) continue;   // nada de rocas sobre la escalinata
                float y = dn.GetInterpolatedHeight(x / AnchoNuevo, z / FondoNuevo);
                int faltan;
                var r = Coop.Piezas.Clonar(modelo, terreno.transform.position + new Vector3(x, y - 0.15f, z), Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f), out faltan);
                if (r == null) continue;
                r.name = "coop_roca_carbonizada";
                r.transform.localScale = modelo.transform.localScale * (0.7f + (float)rng.NextDouble() * 2.3f);
                // Se conserva la textura de la roca y solo se oscurece (antes era un color liso y las rocas se veían sin textura).
                foreach (var rd in r.GetComponentsInChildren<Renderer>(true))
                {
                    var orig = rd.sharedMaterials; var ms = new Material[orig.Length];
                    for (int k = 0; k < ms.Length; k++)
                    {
                        Material t0 = orig[k], t1;
                        if (t0 == null) { ms[k] = oscuro; continue; }
                        if (!tintadas.TryGetValue(t0, out t1))
                        {
                            t1 = new Material(t0) { name = "coop_roca_carbon_" + t0.name };
                            if (t1.HasProperty("_Color")) t1.color = new Color(0.34f, 0.32f, 0.3f); else if (t1.HasProperty("_TintColor")) t1.SetColor("_TintColor", new Color(0.34f, 0.32f, 0.3f)); else t1 = oscuro;
                            tintadas[t0] = t1;
                        }
                        ms[k] = t1;
                    }
                    rd.sharedMaterials = ms;
                }
                r.transform.SetParent(terreno.transform, true);
                puestas++;
            }
            plugin.Log("Expansión: " + puestas + " rocas carbonizadas");
        }

        GameObject bloqueadorRio; readonly List<GameObject> tramosRio = new List<GameObject>();

        /// El río del norte tiene un muro invisible a lo largo; se cambia por dos tramos que dejan
        /// libre el paso (el original vuelve a activarse al quitar la expansión).
        void AbrirBloqueadorRio(Vector3 o)
        {
            bloqueadorRio = GameObject.Find("collider_river_blocker");
            if (bloqueadorRio == null) return;
            var cols = bloqueadorRio.GetComponentsInChildren<Collider>();
            if (cols.Length == 0) return;
            Bounds b = cols[0].bounds;
            foreach (var cc in cols) b.Encapsulate(cc.bounds);
            float a0 = o.x + PasoX0 - 10f, a1 = o.x + PasoX1 + 10f;
            // Se quita el muro del río entero (había un muro invisible a la orilla); los tramos solo se crean si hace falta.
            foreach (var tramo in new Vector2[0])
            {
                if (tramo.y - tramo.x < 1f) continue;
                var g = new GameObject("coop_bloqueador_rio");
                g.layer = bloqueadorRio.layer;
                g.transform.position = new Vector3((tramo.x + tramo.y) / 2f, b.center.y, b.center.z);
                var bc = g.AddComponent<BoxCollider>();
                bc.size = new Vector3(tramo.y - tramo.x, b.size.y, b.size.z);
                tramosRio.Add(g);
            }
            foreach (var cc in cols) cc.enabled = false;
            plugin.Log("Expansión: muro del río abierto en x " + Mathf.RoundToInt(a0 - o.x) + "–" + Mathf.RoundToInt(a1 - o.x) + " (" + cols.Length + " colisiones)");
        }

        readonly List<Collider> cubosQuitados = new List<Collider>();

        /// Otros cubos invisibles del juego original ("Cube", capa Ignore Raycast) que cierran la orilla norte, junto al paso:
        /// se desactivan mientras dure la expansión (la jaula grande ya se reemplaza aparte).
        void QuitarCubosNorte(Vector3 o, float zMax)
        {
            foreach (var mc in Object.FindObjectsOfType<MeshCollider>())
            {
                if (mc == null || !mc.enabled || mc.isTrigger || mc.name != "Cube" || mc == jaula) continue;
                var b = mc.bounds;
                if (b.size.x > 1500f && b.size.z > 1500f) continue;           // la jaula del mapa
                if (b.max.z - o.z < zMax - 600f) continue;                     // solo los que llegan a la orilla norte
                mc.enabled = false; cubosQuitados.Add(mc);
                plugin.Log("Expansión: cubo invisible quitado en (" + Mathf.RoundToInt(b.center.x - o.x) + ", " + Mathf.RoundToInt(b.center.z - o.z) + ") tam " + b.size);
            }
        }

        /// Altura del terreno original ANTES de abrir el paso (si no hay expansión, la de ahora).
        static float AlturaOriginalEn(Vector3 p)
        {
            var t = Anclaje.Principal();
            float[,] a;
            if (t != null && t.terrainData != null && respaldo.TryGetValue(t.terrainData, out a))
            {
                Vector3 o = t.transform.position, sz = t.terrainData.size;
                float lx = p.x - o.x, lz = p.z - o.z;
                if (lx >= 0f && lz >= 0f && lx <= sz.x && lz <= sz.z)
                {
                    int R = a.GetLength(0);
                    return AlturaMuestra(a, R, sz.x / (R - 1), lx, lz) * sz.y + o.y;
                }
            }
            return AlturaEn(p);
        }

        static float AlturaMuestra(float[,] a, int R, float paso, float x, float z)
        {
            float fx = Mathf.Clamp(x / paso, 0f, R - 1.001f), fz = Mathf.Clamp(z / paso, 0f, R - 1.001f);
            int x0 = (int)fx, z0 = (int)fz; float tx = fx - x0, tz = fz - z0;
            return Mathf.Lerp(Mathf.Lerp(a[z0, x0], a[z0, x0 + 1], tx), Mathf.Lerp(a[z0 + 1, x0], a[z0 + 1, x0 + 1], tx), tz);
        }

        /// Bajo las piezas horneadas grandes que quedaron en el aire, el terreno vuelve a la altura original (solo sube), con orilla suave de 8 m.
        static int RestaurarRelieveBajo(float[,] h, float[,] viejo, int R, float paso, List<Vector4> rects)
        {
            const float Orilla = 8f; int n = 0;
            foreach (var r in rects)
            {
                int x0 = Mathf.Max(0, Mathf.FloorToInt((r.x - 1.5f - Orilla) / paso)), x1 = Mathf.Min(R - 1, Mathf.CeilToInt((r.z + 1.5f + Orilla) / paso));
                int z0 = Mathf.Max(0, Mathf.FloorToInt((r.y - 1.5f - Orilla) / paso)), z1 = Mathf.Min(R - 1, Mathf.CeilToInt((r.w + 1.5f + Orilla) / paso));
                for (int xi = x0; xi <= x1; xi++) for (int zi = z0; zi <= z1; zi++)
                {
                    float x = xi * paso, z = zi * paso;
                    float dx = Mathf.Max(0f, Mathf.Max(r.x - 1.5f - x, x - r.z - 1.5f)), dz = Mathf.Max(0f, Mathf.Max(r.y - 1.5f - z, z - r.w - 1.5f));
                    float dd = Mathf.Sqrt(dx * dx + dz * dz), w = dd <= 0f ? 1f : Mathf.SmoothStep(1f, 0f, dd / Orilla);
                    float v = Mathf.Lerp(h[zi, xi], viejo[zi, xi], w);
                    if (v > h[zi, xi]) { h[zi, xi] = v; n++; }
                }
            }
            return n;
        }

        /// Sube el terreno bajo cada pieza flotante hasta su base (solo sube, nunca baja), con una orilla suave de 7 m para que no quede un escalón.
        int SubirTerrenoBajo(Terrain orig, float[,] h, int R, float paso, List<Vector4> rellenos)
        {
            if (rellenos.Count == 0) { orig.terrainData.SetHeights(0, 0, h); return 0; }
            Vector3 o = orig.transform.position; float altoT = orig.terrainData.size.y;
            const float Orilla = 7f;
            var cambio = new Dictionary<int, float>();
            foreach (var rr in rellenos)
            {
                float objetivo = (rr.w - o.y) / altoT, rad = rr.z + 0.8f;
                int x0 = Mathf.Max(0, Mathf.FloorToInt((rr.x - rad - Orilla) / paso)), x1 = Mathf.Min(R - 1, Mathf.CeilToInt((rr.x + rad + Orilla) / paso));
                int z0 = Mathf.Max(0, Mathf.FloorToInt((rr.y - rad - Orilla) / paso)), z1 = Mathf.Min(R - 1, Mathf.CeilToInt((rr.y + rad + Orilla) / paso));
                for (int xi = x0; xi <= x1; xi++) for (int zi = z0; zi <= z1; zi++)
                {
                    float dx = xi * paso - rr.x, dz = zi * paso - rr.y, dd = Mathf.Sqrt(dx * dx + dz * dz);
                    float w = dd <= rad ? 1f : Mathf.SmoothStep(1f, 0f, (dd - rad) / Orilla);
                    if (w <= 0f) continue;
                    float v = Mathf.Lerp(h[zi, xi], objetivo, w);
                    int k = zi * R + xi; float prev;
                    if (v > h[zi, xi] && (!cambio.TryGetValue(k, out prev) || v > prev)) cambio[k] = v;
                }
            }
            foreach (var kv in cambio) h[kv.Key / R, kv.Key % R] = kv.Value;
            orig.terrainData.SetHeights(0, 0, h);
            return cambio.Count;
        }

        /// El paso a la zona nueva baja (o sube) el terreno hasta unos 12 m; los objetos del juego que estaban sobre el terreno original
        /// (la casa del árbol con su botín, rocas…) se quedaban flotando con las raíces en el aire. Se mueven con el suelo.
        void AjustarObjetosAlPaso(Terrain orig, float[,] viejo, float[,] nuevoH, int R, float paso)
        {
            Vector3 o = orig.transform.position; float altoT = orig.terrainData.size.y;
            int movidos = 0, ocultos = 0; float mayor = 0f;
            var restaurar = new List<Vector4>();   // (x0, z0, x1, z1): huellas de piezas horneadas grandes donde vuelve el relieve original
            var rellenos = new List<Vector4>();   // (x, z, radio, altura) en ejes del terreno: suelo que se sube bajo la hierba horneada
            // se recorre el árbol de objetos hasta 4 niveles: la casa del árbol y su botín cuelgan de un grupo, no son raíces
            var pila = new Stack<KeyValuePair<Transform, int>>();
            foreach (var g in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects()) if (g != null && g.activeInHierarchy) pila.Push(new KeyValuePair<Transform, int>(g.transform, 0));
            while (pila.Count > 0)
            {
                var kv = pila.Pop(); Transform t = kv.Key; int nivel = kv.Value;
                if (t == null) continue;
                var g = t.gameObject;
                if (g.name.StartsWith("coop_") || g.GetComponent<Terrain>() != null || g.GetComponent<Camera>() != null || g.GetComponent<TakeDamage>() != null || g.GetComponent<FPSPlayer>() != null) continue;
                if (System.Array.IndexOf(Unicos, Catalogo.BaseDe(g.name)) >= 0) continue;   // los lugares únicos tienen su suelo protegido
                bool horneado = false;
                foreach (var mr in g.GetComponentsInChildren<MeshRenderer>(true)) if (mr.isPartOfStaticBatch) { horneado = true; break; }
                if (horneado)
                {
                    // no se puede mover (malla horneada): se revisa CADA pieza (antes se juzgaba el grupo entero y, si medía más de 12 m,
                    // sus matas quedaban flotando). Si una pieza chica (pasto, matas, piedras) quedó en el aire sobre el suelo rebajado,
                    // se SUBE el terreno bajo ella hasta su base (pedido 2026-11-17: la hierba debe quedar apoyada, no oculta).
                    foreach (var mr in g.GetComponentsInChildren<MeshRenderer>(true))
                    {
                        if (mr == null || !mr.isPartOfStaticBatch) continue;
                        Bounds bh = mr.bounds; float lxh = bh.center.x - o.x, lzh = bh.center.z - o.z;
                        if (lzh < pasoZ0 - 80f || lxh < PasoX0 - PasoBorde || lxh > PasoX1 + PasoBorde) continue;
                        if (Mathf.Max(bh.size.x, bh.size.z) > 14f)
                        {
                            // pieza grande (telones de hierba "DeadSide" que cubren laderas): se devuelve el relieve ORIGINAL bajo toda su huella
                            if (Mathf.Max(bh.size.x, bh.size.z) > 90f) continue;
                            float bajada = 0f;
                            for (int sx2 = 0; sx2 <= 4; sx2++) for (int sz2 = 0; sz2 <= 4; sz2++)
                            {
                                float qx = bh.min.x - o.x + bh.size.x * sx2 / 4f, qz = bh.min.z - o.z + bh.size.z * sz2 / 4f;
                                bajada = Mathf.Max(bajada, (AlturaMuestra(viejo, R, paso, qx, qz) - AlturaMuestra(nuevoH, R, paso, qx, qz)) * altoT);
                            }
                            if (bajada < 0.4f) continue;
                            restaurar.Add(new Vector4(bh.min.x - o.x, bh.min.z - o.z, bh.max.x - o.x, bh.max.z - o.z));
                            plugin.Log("Expansión: relieve original devuelto bajo " + mr.name + " (" + bh.size.x.ToString("0") + "×" + bh.size.z.ToString("0") + " m, el paso lo había bajado " + bajada.ToString("0.0") + " m)");
                            continue;
                        }
                        float sueloNuevo = AlturaMuestra(nuevoH, R, paso, lxh, lzh) * altoT + o.y;
                        float sueloViejo = AlturaMuestra(viejo, R, paso, lxh, lzh) * altoT + o.y;
                        if (bh.min.y - sueloNuevo <= 0.15f) continue;            // ya apoyada (o enterrada)
                        if (Mathf.Abs(bh.min.y - sueloViejo) > 3f) continue;     // no estaba sobre el suelo original (colgada, en un árbol…)
                        rellenos.Add(new Vector4(lxh, lzh, Mathf.Max(bh.extents.x, bh.extents.z), bh.min.y + 0.08f));
                    }
                    continue;   // mover el transform separaría la colisión de la malla dibujada (que no se mueve)
                }
                Vector3 p = t.position;
                float lx = p.x - o.x, lz = p.z - o.z;
                bool movido = false;
                if (lz >= pasoZ0 - 80f && lx >= PasoX0 - 60f && lx <= PasoX1 + 60f && (g.GetComponent<Renderer>() != null || g.GetComponent<Collider>() != null || nivel == 0))
                {
                    float hv = AlturaMuestra(viejo, R, paso, lx, lz) * altoT + o.y, hn2 = AlturaMuestra(nuevoH, R, paso, lx, lz) * altoT + o.y;
                    float dy = hn2 - hv;
                    if (Mathf.Abs(dy) >= 0.4f && Mathf.Abs(p.y - hv) <= 8f)   // estaba sobre el suelo (no colgado, en un árbol o bajo tierra a propósito)
                    {
                        t.position = p + Vector3.up * dy; movido = true;
                        movidos++; if (Mathf.Abs(dy) > mayor) mayor = Mathf.Abs(dy);
                    }
                }
                if (!movido && nivel < 4) foreach (Transform hijo in t) if (hijo.gameObject.activeInHierarchy) pila.Push(new KeyValuePair<Transform, int>(hijo, nivel + 1));
            }
            int devueltas = RestaurarRelieveBajo(nuevoH, viejo, R, paso, restaurar);
            ocultos = SubirTerrenoBajo(orig, nuevoH, R, paso, rellenos);   // (también aplica SetHeights con lo devuelto)
            if (devueltas > 0) plugin.Log("Expansión: relieve original devuelto en " + devueltas + " muestras bajo " + restaurar.Count + " piezas grandes");
            int celdas = RefrescarPasto(orig.terrainData, PasoX0 - PasoBorde - 40f, pasoZ0 - 160f, PasoX1 + PasoBorde + 40f, orig.terrainData.size.z);
            int atg = RefrescarHierbaATG(orig);
            plugin.Log("Expansión: hierba regenerada sobre el suelo nuevo del paso (" + celdas + " celdas de detalle por capa, " + atg + " GrassManager reiniciados)");
            plugin.Log("Expansión: " + movidos + " objetos del juego movidos con el suelo del paso (hasta " + mayor.ToString("0.0") + " m), terreno subido bajo " + rellenos.Count + " piezas horneadas que flotaban (" + ocultos + " muestras)");
        }

        readonly List<Renderer> telones = new List<Renderer>();

        /// "Plane_woods": planos con una foto de árboles (los árboles 2D) que el juego pone alrededor del mapa como horizonte.
        /// Se veían como losas con árboles planos: se ocultan todos mientras dure la expansión (detrás está el bosque exterior).
        void OcultarTelones(Vector3 o, float zMax)
        {
            telones.Clear(); cubosQuitados.Clear();
            foreach (var r in Resources.FindObjectsOfTypeAll<MeshRenderer>())
            {
                if (r == null || !r.gameObject.scene.IsValid() || !r.name.StartsWith("Plane_woods") || !r.enabled) continue;
                // todos (pedido 2026-11-17): también los del este, oeste y sur se veían como paredes con árboles planos;
                // detrás ya está el bosque exterior (BosqueExterior.cs)
                r.enabled = false; telones.Add(r);
            }
            // "lo_CobbleRock_F_*": montaña lejana del juego (3700 × 1070 m) que está más allá del borde norte del mapa original.
            // La zona nueva queda encima: sus trozos de grava salían del suelo como un piso que se atraviesa. Se oculta con sus colisiones.
            foreach (var r in Resources.FindObjectsOfTypeAll<MeshRenderer>())
            {
                if (r == null || !r.gameObject.scene.IsValid() || !r.name.StartsWith("lo_CobbleRock") || !r.enabled) continue;
                r.enabled = false; telones.Add(r);
                foreach (var c in r.GetComponentsInChildren<Collider>(true)) if (c.enabled) { c.enabled = false; cubosQuitados.Add(c); }
            }
            plugin.Log("Expansión: " + telones.Count + " telones de árboles 2D (Plane_woods) y montaña lejana ocultos");
        }

        Collider jaula; readonly List<GameObject> paredes = new List<GameObject>();

        /// El mapa jugable está encerrado en un cubo invisible (MeshCollider "Cube", capa Ignore Raycast).
        /// Se reemplaza por paredes equivalentes con un hueco en la cara norte, un pasillo hasta el
        /// terreno nuevo y paredes alrededor de la zona nueva.
        void AbrirJaula(Vector3 o, float zMax)
        {
            jaula = null;
            foreach (var mc in Object.FindObjectsOfType<MeshCollider>())
                if (mc.name == "Cube" && mc.enabled && mc.bounds.size.x > 1000f && mc.bounds.size.z > 1000f) { jaula = mc; break; }
            if (jaula == null) { plugin.Log("Expansión: no encontré la jaula del mapa"); return; }
            Bounds b = jaula.bounds;
            int capa = jaula.gameObject.layer;
            float y0 = b.min.y, alto = b.size.y;
            float h0 = o.x + PasoX0 - 10f, h1 = o.x + PasoX1 + 10f;            // hueco en la cara norte
            float zN = b.max.z, zT = o.z + zMax;                                // cara norte y comienzo del terreno nuevo
            float nx0 = o.x + NuevoX0, nx1 = nx0 + AnchoNuevo, nz1 = zT + FondoNuevo;
            // Caras de la jaula original
            Muro(capa, b.min.x, b.max.x, b.min.z, b.min.z, y0, alto);          // sur
            Muro(capa, b.min.x, b.min.x, b.min.z, b.max.z, y0, alto);          // oeste
            Muro(capa, b.max.x, b.max.x, b.min.z, b.max.z, y0, alto);          // este
            // (quitado: el muro invisible entre el bosque y la zona nueva; solo quedan los bordes exteriores)
            // (quitadas también las paredes del pasillo: antes dejaban un muro invisible a los lados del paso)
            // Zona nueva
            Muro(capa, nx0, nx0, zT, nz1, y0, alto);
            Muro(capa, nx1, nx1, zT, nz1, y0, alto);
            Muro(capa, nx0, nx1, nz1, nz1, y0, alto);
            // Huecos que quedaban al abrir el norte: la franja z (cara norte original → comienzo de la zona nueva) a los lados del mapa viejo,
            // y el borde sur de la zona nueva donde el mapa viejo no la cubre (x más allá del este/oeste del mapa original). Sin estas
            // paredes se podía caminar hasta una esquina y caer al vacío para siempre.
            Muro(capa, b.min.x, b.min.x, zN, zT, y0, alto);
            Muro(capa, b.max.x, b.max.x, zN, zT, y0, alto);
            if (nx1 > b.max.x + 1f) Muro(capa, b.max.x, nx1, zT, zT, y0, alto);
            if (nx0 < b.min.x - 1f) Muro(capa, nx0, b.min.x, zT, zT, y0, alto);
            jaula.enabled = false;
            plugin.Log("Expansión: jaula del mapa abierta al norte (" + paredes.Count + " paredes)");
        }

        void Muro(int capa, float x0, float x1, float z0, float z1, float y0, float alto)
        {
            var g = new GameObject("coop_pared");
            g.layer = capa;
            g.transform.position = new Vector3((x0 + x1) / 2f, y0 + alto / 2f, (z0 + z1) / 2f);
            var bc = g.AddComponent<BoxCollider>();
            bc.size = new Vector3(Mathf.Max(Mathf.Abs(x1 - x0), 1f), alto, Mathf.Max(Mathf.Abs(z1 - z0), 1f));
            paredes.Add(g);
        }

        /// Altura del agua global del bosque (todo lo que queda debajo es agua helada).
        static float NivelAgua()
        {
            var g = GameObject.Find("AQUASWaterWitchBellvile");
            if (g == null) return 12.8f;
            var r = g.GetComponent<Renderer>();
            return r != null ? r.bounds.center.y : g.transform.position.y;
        }

        /// 1 dentro del paso, 0 fuera, con transición suave.
        static float Mascara(float x)
        {
            if (x < PasoX0 - PasoBorde || x > PasoX1 + PasoBorde) return 0f;
            if (x >= PasoX0 && x <= PasoX1) return 1f;
            float dd = x < PasoX0 ? PasoX0 - x : x - PasoX1;
            return Mathf.SmoothStep(1f, 0f, dd / PasoBorde);
        }

        readonly List<Vector3> origenesEnlace = new List<Vector3>();

        /// Para cada franja (x de 300 a 1200, cada 50 m) busca el punto más al norte al que se llega
        /// a pie desde la llegada del bosque (camino original del juego). El valle se abre donde ese
        /// punto queda más cerca del río. Determinista: depende solo del mapa del juego.
        void ElegirPaso(Vector3 o, float zMax)
        {
            alcanzables.Clear();
            NavMeshHit inicio;
            if (!NavMesh.SamplePosition(o + LlegadaRel, out inicio, 20f, NavMesh.AllAreas)) { plugin.Log("Expansión: sin camino en la llegada"); return; }
            var camino = new NavMeshPath();
            float mejorZ = -1f, mejorX = 750f, mejorY = 20f;
            var porX = new Dictionary<float, Vector3>();
            for (float x = 300f; x <= 1200f; x += 50f)
            {
                for (float z = zMax - 40f; z > zMax - 700f; z -= 20f)
                {
                    Vector3 p = o + new Vector3(x, 0f, z); p.y = AlturaEn(p);
                    NavMeshHit nh;
                    if (float.IsNaN(p.y) || p.y < NivelAgua() + 0.8f || !NavMesh.SamplePosition(p, out nh, 4f, NavMesh.AllAreas)) continue;
                    if (!NavMesh.CalculatePath(inicio.position, nh.position, NavMesh.AllAreas, camino) || camino.status != NavMeshPathStatus.PathComplete) continue;
                    porX[x] = nh.position;
                    break;
                }
            }
            // La ventana de AnchoPaso con el promedio de z alcanzado más al norte.
            foreach (var kv in porX)
            {
                float suma = 0f; int n = 0;
                foreach (var kv2 in porX) if (Mathf.Abs(kv2.Key - kv.Key) <= AnchoPaso / 2f) { suma += kv2.Value.z - o.z; n++; }
                if (n < 3) continue;
                float media = suma / n;
                if (media > mejorZ) { mejorZ = media; mejorX = kv.Key; mejorY = kv.Value.y - o.y; }
            }
            if (mejorZ < 0f) { plugin.Log("Expansión: no encontré por dónde abrir el paso"); mejorZ = zMax - 300f; }
            PasoX0 = mejorX - AnchoPaso / 2f; PasoX1 = mejorX + AnchoPaso / 2f;
            // El valle empieza en el punto alcanzable más al SUR de la ventana (todas las franjas conectan).
            pasoZ0 = zMax; pasoAltura = mejorY;
            foreach (var kv in porX)
                if (kv.Key >= PasoX0 && kv.Key <= PasoX1)
                {
                    pasoZ0 = Mathf.Min(pasoZ0, kv.Value.z - o.z);
                    alcanzables.Add(kv.Value);
                }
            plugin.Log("Expansión: paso en x " + Mathf.RoundToInt(PasoX0) + "–" + Mathf.RoundToInt(PasoX1) + ", valle desde z " + Mathf.RoundToInt(pasoZ0) + " a " + pasoAltura.ToString("0") + " m (" + porX.Count + " franjas medidas)");
        }

        void BuscarOrigenesEnlaces(Vector3 o, float zMax)
        {
            origenesEnlace.Clear();
            origenesEnlace.AddRange(alcanzables);
        }

        /// Enlaces entre el último suelo con camino original y el terraplén (ya con camino nuevo).
        /// Punto de la zona nueva conectado (comprobado) con la llegada del bosque. Los caminos muy
        /// largos llegan "parciales" por límite de cálculo de Unity, así que la zona nueva se valida
        /// desde aquí y no desde la llegada.
        public bool HayEntradaNorte; public Vector3 EntradaNorte;

        void Unir()
        {
            int n = 0;
            HayEntradaNorte = false;
            NavMeshHit inicio;
            bool hayInicio = NavMesh.SamplePosition(Anclaje.Origen() + LlegadaRel, out inicio, 20f, NavMesh.AllAreas);
            var camino = new NavMeshPath();
            foreach (var desde in origenesEnlace)
            {
                // Fin del enlace ya dentro del terreno nuevo (solo existe el camino nuevo ahí).
                Vector3 o = Anclaje.Origen();
                Vector3 hasta = new Vector3(desde.x, 0f, o.z + Anclaje.Principal().terrainData.size.z + 40f); hasta.y = AlturaEn(hasta);
                NavMeshHit nh2;
                if (float.IsNaN(hasta.y) || !NavMesh.SamplePosition(hasta, out nh2, 6f, NavMesh.AllAreas)) continue;
                var enlace = new NavMeshLinkData { startPosition = desde, endPosition = nh2.position, width = 20f, bidirectional = true, costModifier = -1, area = 0, agentTypeID = NavMesh.GetSettingsByIndex(0).agentTypeID };
                enlaces.Add(NavMesh.AddLink(enlace));
                n++;
                if (!HayEntradaNorte && hayInicio && NavMesh.CalculatePath(inicio.position, desde, NavMesh.AllAreas, camino) && camino.status == NavMeshPathStatus.PathComplete)
                { HayEntradaNorte = true; EntradaNorte = nh2.position; }
            }
            plugin.Log("Expansión: " + n + " enlaces entre el bosque y la zona norte" + (HayEntradaNorte ? ", entrada norte en " + (EntradaNorte - Anclaje.Origen()) : " (¡sin entrada comprobada!)"));
        }

        void QuitarExpansion()
        {
            foreach (var e in enlaces) NavMesh.RemoveLink(e);
            enlaces.Clear();
            if (navInstancia.valid) NavMesh.RemoveNavMeshData(navInstancia);
            navOp = null;
            if (nuevo != null) Object.Destroy(nuevo.gameObject);
            if (datosNuevos != null) Object.Destroy(datosNuevos);
            nuevo = null; datosNuevos = null;
            QuitarBosqueExterior();
            foreach (var g in paredes) if (g != null) Object.Destroy(g);
            paredes.Clear();
            if (jaula != null) jaula.enabled = true;
            foreach (var r in telones) if (r != null) r.enabled = true;
            foreach (var c in cubosQuitados) if (c != null) c.enabled = true;
            cubosQuitados.Clear();
            telones.Clear();
            jaula = null;
            foreach (var g in tramosRio) if (g != null) Object.Destroy(g);
            tramosRio.Clear();
            if (bloqueadorRio != null) foreach (var c in bloqueadorRio.GetComponentsInChildren<Collider>(true)) c.enabled = true;
            bloqueadorRio = null;
            RestaurarOriginal();
        }

        /// Devuelve al terreno original su forma (el paso abierto es solo de esta cacería).
        static float pixelErrorOriginal = -1f;
        static void RestaurarOriginal()
        {
            var tp = Anclaje.Principal(); if (tp != null && pixelErrorOriginal >= 0f) tp.heightmapPixelError = pixelErrorOriginal;
            float[,] h;
            if (datosOriginal != null && respaldo.TryGetValue(datosOriginal, out h)) { datosOriginal.SetHeights(0, 0, h); RefrescarPasto(datosOriginal, 0f, 0f, 1e9f, 1e9f); RefrescarHierbaATG(Anclaje.Principal()); }
        }

        /// La hierba de detalle del terreno se genera con la altura que había al crearla: si después se cambia el relieve con SetHeights
        /// (el paso rebaja el suelo hasta 33 m), la hierba vieja sigue dibujándose a la altura anterior y queda FLOTANDO (pedido 2026-11-17).
        /// Volver a asignar las capas de detalle de la zona obliga a Unity a regenerarla sobre el suelo actual. Rango en metros del terreno.
        /// La hierba del bosque NO es la de Unity (su distancia de detalle es 0): la dibuja Advanced Terrain Grass (GrassManager), que al
        /// encenderse copia las alturas del terreno UNA vez y arma sus celdas con esa copia. Tras rebajar el paso, la hierba seguía a la
        /// altura vieja, flotando (pedido 2026-11-17). Apagarlo y encenderlo libera la copia y vuelve a leer el relieve actual.
        public static int RefrescarHierbaATG(Terrain t)
        {
            if (t == null) return 0;
            int n = 0;
            foreach (var gm in t.GetComponents<AdvancedTerrainGrass.GrassManager>())
            {
                if (gm == null || !gm.enabled) continue;
                gm.enabled = false; gm.enabled = true; n++;
            }
            return n;
        }

        public static int RefrescarPasto(TerrainData d, float x0, float z0, float x1, float z1)
        {
            if (d == null || d.detailPrototypes == null || d.detailPrototypes.Length == 0) return 0;
            int W = d.detailWidth, H = d.detailHeight;
            int cx0 = Mathf.Clamp(Mathf.FloorToInt(x0 / d.size.x * W), 0, W - 1), cx1 = Mathf.Clamp(Mathf.CeilToInt(x1 / d.size.x * W), 0, W);
            int cz0 = Mathf.Clamp(Mathf.FloorToInt(z0 / d.size.z * H), 0, H - 1), cz1 = Mathf.Clamp(Mathf.CeilToInt(z1 / d.size.z * H), 0, H);
            int w = cx1 - cx0, h = cz1 - cz0; if (w <= 0 || h <= 0) return 0;
            for (int i = 0; i < d.detailPrototypes.Length; i++)
            {
                var capa = d.GetDetailLayer(cx0, cz0, w, h, i);
                d.SetDetailLayer(cx0, cz0, i, capa);
            }
            return w * h;
        }

        /// Altura del suelo en cualquier terreno (original o nuevo); NaN fuera de todos.
        static float AlturaEn(Vector3 p)
        {
            foreach (var t in Terrain.activeTerrains)
            {
                if (t == null || t.terrainData == null) continue;
                Vector3 o = t.transform.position, s = t.terrainData.size;
                if (p.x < o.x || p.z < o.z || p.x > o.x + s.x || p.z > o.z + s.z) continue;
                return t.SampleHeight(p) + o.y;
            }
            return float.NaN;
        }

        public static string DiagnosticoRespaldo()
        {
            var t = Anclaje.Principal(); if (t == null) return "sin terreno";
            var d = t.terrainData; int R = d.heightmapResolution;
            float paso = d.size.x / (R - 1);
            int xi = Mathf.RoundToInt(950f / paso);
            string r = "terreno id=" + d.GetInstanceID() + " actual(950,borde)=" + (d.GetHeight(xi, R - 1)).ToString("0.0");
            float[,] h;
            foreach (var kv in respaldo) r += " | respaldo id=" + kv.Key.GetInstanceID() + (kv.Key == null ? "(destruido)" : "") + " (950,borde)=" + (kv.Value[R - 1, xi] * d.size.y).ToString("0.0");
            return r;
        }

        public string DescribirExpansion()
        {
            return nuevo == null ? "sin expansión" : "terreno norte en " + nuevo.transform.position + ", navmesh " + (expansionLista ? "listo" : navOp != null ? "horneando " + Mathf.RoundToInt(navOp.progress * 100) + " %" : "?") + ", enlaces " + enlaces.Count;
        }
    }
}

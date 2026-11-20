// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using System.Collections.Generic;
using UnityEngine;

namespace WitchHuntCoop
{
    /// Cabañas cerradas mejoradas (pedido 2026-11-11): más grandes, amuebladas (mesa, camastro, cajas, barril, estante, un cadáver)
    /// y, de vez en cuando, de dos pisos con una escalera por fuera hasta la planta de arriba.
    public partial class Mundo
    {
        public const float EscalaCabana = 1.3f, DesnivelCabana = 0.6f;
        class CofreCabana { public Vector3 Pos; public float Yaw; public int Semilla; }
        readonly List<CofreCabana> cofresDeCabana = new List<CofreCabana>();

        static Material Madera(GameObject casa, string prefijo)
        {
            foreach (var r in casa.GetComponentsInChildren<MeshRenderer>(true))
                if (r.enabled && r.name.StartsWith(prefijo) && r.sharedMaterial != null) return r.sharedMaterial;
            return null;
        }

        static GameObject Caja(Transform padre, string nombre, Vector3 centro, Vector3 tam, Quaternion giro, Material m, Color tinte, bool col)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            if (!col) Object.Destroy(g.GetComponent<Collider>());
            g.name = nombre; g.transform.SetParent(padre, true);
            g.transform.position = centro; g.transform.rotation = giro; g.transform.localScale = tam;
            Material mm = m != null ? new Material(m) : new Material(Shader.Find("Standard"));
            mm.color = tinte; mm.mainTextureScale = new Vector2(1f, 1f);
            g.GetComponent<Renderer>().sharedMaterial = mm;
            return g;
        }

        /// Mesa, camastro, cajas, barril, estante y un cadáver dentro de la cabaña (el mismo reparto en los dos juegos: semilla de la cabaña).
        /// modo 0: cabaña normal; 1: planta baja con escalera interior (sin mesa, estante ni cajas en el lado de la escalera); 2: planta alta con hueco de escalera.
        /// casa de la escopeta: el estante va corrido a la izquierda y aquí queda el sitio del expositor de pared
        public Vector3 sitioExpositor; public Vector3 frenteExpositor; public bool hayExpositor;
        readonly List<Vector3[]> vistasEscalera = new List<Vector3[]>(), vistasExpositor = new List<Vector3[]>();
        /// Orden de prueba: "x y z yaw pitch" para ver la escalera interior (n) o el expositor de la escopeta (n).
        public string OrdenVer(string que, int n)
        {
            var l = que == "expositor" ? vistasExpositor : vistasEscalera;
            if (n < 0 || n >= l.Count) return "no hay (" + l.Count + ")";
            Vector3 desde = l[n][0], hacia = l[n][1], d = hacia - desde;
            float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, pitch = Mathf.Asin(Mathf.Clamp(d.normalized.y, -1f, 1f)) * Mathf.Rad2Deg;
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            return "VER " + desde.x.ToString("F2", ci) + " " + desde.y.ToString("F2", ci) + " " + desde.z.ToString("F2", ci) + " " + yaw.ToString("F0", ci) + " " + pitch.ToString("F0", ci);
        }

        void DecorarCabana(GameObject casa, int semilla, int modo = 0, bool conCadaver = true, bool conCofre = true, bool casaEscopeta = false)
        {
            conCadaver = false;   // pedido 2026-11-18: sin cadáver en ninguna cabaña
            hayExpositor = false;
            BoxCollider piso = null;
            foreach (var bx in casa.GetComponentsInChildren<BoxCollider>(true)) if (bx.name.StartsWith("cabinFloor")) { piso = bx; break; }
            if (piso == null) { plugin.Log("Cabañas: no pude amueblar (sin piso)"); return; }
            var rng = new System.Random(semilla ^ 0x5bd1e995);
            MaterialesMuebles();
            Transform t = piso.transform;
            // ejes horizontales del piso
            int[] hor = new int[2]; int nh = 0;
            for (int i = 0; i < 3 && nh < 2; i++) { Vector3 d = new Vector3(i == 0 ? 1 : 0, i == 1 ? 1 : 0, i == 2 ? 1 : 0); if (Mathf.Abs(t.TransformDirection(d).y) < 0.5f) hor[nh++] = i; }
            if (nh < 2) return;
            Vector3 da = t.TransformDirection(hor[0] == 0 ? Vector3.right : hor[0] == 1 ? Vector3.up : Vector3.forward); da.y = 0f; da.Normalize();
            Vector3 db = t.TransformDirection(hor[1] == 0 ? Vector3.right : hor[1] == 1 ? Vector3.up : Vector3.forward); db.y = 0f; db.Normalize();
            float la = piso.size[hor[0]] * Mathf.Abs(t.lossyScale[hor[0]]), lb = piso.size[hor[1]] * Mathf.Abs(t.lossyScale[hor[1]]);
            Vector3 c = t.TransformPoint(piso.center);
            float y0 = piso.bounds.max.y;
            var puntoSuelo = new System.Func<float, float, Vector3>((a, b) => new Vector3(c.x, y0, c.z) + da * a + db * b);
            var mad = Madera(casa, "cabinWallsInterior") ?? Madera(casa, "porchFloor") ?? Madera(casa, "cabinWallsExterior");
            var hierro = new Material(Shader.Find("Standard")) { color = new Color(0.12f, 0.12f, 0.13f) }; hierro.SetFloat("_Metallic", 0.6f);
            Quaternion g0 = Quaternion.LookRotation(db, Vector3.up);   // "frente" del mueble hacia el eje b
            float ha = la * 0.5f - 0.55f, hb = lb * 0.5f - 0.55f;
            var pad = casa.transform;
            Color tm = new Color(0.34f, 0.22f, 0.12f);
            if (modo == 0) {
            // --- Mesa de tablones con dos taburetes, platos, jarra, libro y vela (MueblesCabana.cs)
            Vector3 mc = puntoSuelo(-ha * 0.55f, -hb * 0.25f);
            Quaternion gm = Quaternion.LookRotation(da, Vector3.up);
            MesaCabana(pad, mc, gm, mad, rng);
            }   // fin de mesa/taburetes/vela (modo 0)
            // --- Camastro (pared opuesta)
            Vector3 bc = puntoSuelo(ha * 0.55f, hb * 0.6f);
            Quaternion gb = Quaternion.LookRotation(db, Vector3.up);
            CamaCabana(pad, bc, gb, mad, rng);
            // --- Cajas y barril (esquina)
            Vector3 cc = puntoSuelo(ha * 0.7f, -hb * 0.7f);
            if (modo != 1) {
            CajasCabana(pad, cc, gb, mad, rng);
            }   // fin de cajas
            if (modo != 2) {
            BarrilCabana(pad, puntoSuelo(-ha * 0.8f, hb * 0.7f), mad, hierro);
            }   // fin de barril y aros
            if (modo == 0) {
            // --- Estante en la pared del fondo
            Vector3 pEst = puntoSuelo(-la * 0.5f + 0.17f, -hb * 0.1f);
            if (casaEscopeta)
            {
                // la escopeta va colgada donde estaba el estante; el estante, 1,4 m a la izquierda (hacia la ventana)
                Vector3 izq = Vector3.Cross(Vector3.up, da).normalized;
                sitioExpositor = puntoSuelo(-la * 0.5f + 0.04f, -hb * 0.1f) + Vector3.up * 1.45f; frenteExpositor = da; hayExpositor = true;
                pEst += izq * 1.4f;
            }
            EstanteCabana(pad, pEst, Quaternion.LookRotation(da, Vector3.up), mad, rng);
            }   // fin de estante
            // --- Cadáver (el del juego, si existe en la escena) tirado cerca de la mesa
            GameObject muerto = null;
            if (conCadaver)
            foreach (var g in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects()) if (Catalogo.BaseDe(g.name) == "floating_body") { muerto = g; break; }   // el mismo cuerpo tendido de las casas de la escopeta
            if (muerto == null) plugin.Log("Cabañas: no hay cuerpo modelo (floating_body) para el cadáver");
            else
            {
                int faltan; Vector3 mp = puntoSuelo(-ha * 0.1f, hb * 0.55f);
                var m = Coop.Piezas.Clonar(muerto, mp, Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f) * muerto.transform.rotation, out faltan);
                if (m != null)
                {
                    m.name = "coop_cadaver"; m.transform.SetParent(pad, true);
                    foreach (var ll in m.GetComponentsInChildren<Light>(true)) ll.enabled = false;
                    foreach (var an in m.GetComponentsInChildren<Animator>(true)) an.enabled = false;   // quieto, sin flotar
                    Bounds cajaM;
                    if (CajaDe(new List<GameObject> { m }, out cajaM))
                    {
                        m.transform.position += new Vector3(mp.x - cajaM.center.x, 0f, mp.z - cajaM.center.z);
                        m.transform.position += new Vector3(0f, y0 - MinYReal(m) + 0.02f, 0f);
                    }
                }
            }
            // sitio de un cofre (se crea en ColocarCofres, para que tenga número y se abra/sincronice como los demás)
            // el cofre iba pegado a la pared de la puerta, a su altura (se veía en medio del umbral): ahora en la esquina del lado de la puerta más alejada de ella (las cajas ocupan la otra)
            Vector3 pc = modo == 1 ? puntoSuelo(ha * 0.92f, -hb * 0.9f) : puntoSuelo(-ha * 0.92f, -hb * 0.9f);
            if (conCofre) cofresDeCabana.Add(new CofreCabana { Pos = pc, Yaw = Quaternion.LookRotation(db, Vector3.up).eulerAngles.y, Semilla = semilla });
            // --- Chimenea de ladrillo pegada a la pared del fondo (+a) con la fogata ADENTRO (pedido 2026-11-17; la del modelo flotaba en medio del cuarto)
            float techoLibre = AlturaLibre(new Vector3(c.x, y0, c.z));
            try { var pch = puntoSuelo(la * 0.5f - 0.36f, -hb * 0.05f); ChimeneaPiedra(pad, pch, da, db, mad, rng, AlturaLibre(pch)); } catch (System.Exception ex) { plugin.Log("Cabañas: error en la chimenea " + ex.Message); }
            try { DetallesCabana(pad, c, da, db, la, lb, y0, techoLibre, rng, modo != 1); } catch (System.Exception ex) { plugin.Log("Cabañas: error en los detalles " + ex.Message); }
            plugin.Log("Cabañas: amueblada (" + la.ToString("0.0") + " × " + lb.ToString("0.0") + " m)");
        }

        static bool EsDeTecho(string n) { return n.IndexOf("Roof", System.StringComparison.OrdinalIgnoreCase) >= 0 || n.StartsWith("cabinUpperWalls") || n.StartsWith("cabinWoodWork") || n.StartsWith("wallTimberJoint"); }

        /// Segunda planta: otra cabaña igual encima de la primera (sin techo abajo) y una escalera por fuera que sube a su porche.
        void SubirPlanta(GameObject casa, Vector3 sitio, float yaw, Cabana cb)
        {
            // altura de una planta = alto de las paredes exteriores
            float alto = 0f; Bounds b = new Bounds(); bool hay = false;
            foreach (var r in casa.GetComponentsInChildren<MeshRenderer>(true))
                if (r.enabled && r.name.StartsWith("cabinWallsExterior")) { if (!hay) { b = r.bounds; hay = true; } else b.Encapsulate(r.bounds); }
            if (!hay) { plugin.Log("Cabañas: no pude medir las paredes para la segunda planta"); return; }
            alto = b.size.y;
            // quitar el techo de abajo
            int quitadas = 0;
            foreach (var r in casa.GetComponentsInChildren<Renderer>(true)) if (EsDeTecho(r.name)) { r.enabled = false; quitadas++; }
            foreach (var col in casa.GetComponentsInChildren<Collider>(true)) if (EsDeTecho(col.name)) col.enabled = false;
            // planta de arriba
            GameObject modelo = null;
            foreach (var g in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects()) if (g.name == "THE_WOOD_CABIN_02") modelo = g;
            if (modelo == null) return;
            var arriba = ArmarCabana(modelo, sitio + Vector3.up * (alto - 0.05f - 0.2f), yaw, casa.name + "_piso2");
            arriba.transform.localScale = arriba.transform.localScale * EscalaCabana;
            foreach (var l in arriba.GetComponentsInChildren<Light>(true)) l.enabled = false;
            QuitarChimeneaYPiedras(arriba);
            DecorarCabana(arriba, cb.Semilla ^ 0x1234, 2);
            // la planta de arriba no lleva lo que cuelga bajo su piso: la escalera de fuera (daba a la nada) y los postes del porche
            {
                Vector3 c2, a2, b2; float l2, w2, yArriba;
                if (Marco(arriba, out c2, out a2, out b2, out l2, out w2, out yArriba))
                {
                    int ocult = 0;
                    foreach (var r in arriba.GetComponentsInChildren<Renderer>(true)) if (!r.name.StartsWith("coop_") && r.enabled && r.bounds.max.y < yArriba - 0.15f) { r.enabled = false; ocult++; }
                    foreach (var col in arriba.GetComponentsInChildren<Collider>(true)) if (!col.name.StartsWith("coop_") && col.enabled && col.bounds.max.y < yArriba - 0.15f) col.enabled = false;
                    plugin.Log("Cabañas: planta alta sin " + ocult + " piezas bajo su piso (escalera de fuera, postes)");
                }
            }
            EscaleraInterior(casa, arriba);
            // (sin escalera exterior, pedido 2026-11-17: la planta alta se sube por una escalera interior)
            plugin.Log("Cabañas: segunda planta (alto " + alto.ToString("0.0") + " m, " + quitadas + " piezas de techo quitadas abajo)");
        }

        // ---------------- Telas, chimenea y escalera interior (pedido 2026-11-17) ----------------

        static Material ladrilloCabana;

        /// Textura de tela hecha en el momento (tipo 0 lino, 1 paja, 2 cuadros) puesta en la caja: antes la cama era de un color liso.
        static void Telar(GameObject g, Color a, Color b, int tipo, int periodo, float tamX, float tamZ)
        {
            const int N = 64;
            var tex = new Texture2D(N, N, TextureFormat.RGB24, true);
            var rng = new System.Random(tipo * 977 + periodo);
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float ruido = 0.88f + 0.24f * (float)rng.NextDouble();
                    Color c;
                    if (tipo == 2)   // cuadros: bandas oscuras cruzadas
                    {
                        bool bx = (x / 8) % 4 == 0, by = (y / 8) % 4 == 0;
                        c = bx && by ? Color.Lerp(a, b, 0.9f) : bx || by ? Color.Lerp(a, b, 0.55f) : a;
                    }
                    else if (tipo == 1)   // paja: trazos horizontales
                        c = ((y + (x / 16) * 5) % 4 < 1) ? b : Color.Lerp(a, b, (float)rng.NextDouble() * 0.5f);
                    else                   // lino: trama fina
                        c = ((x + y) % 2 == 0) ? a : Color.Lerp(a, b, 0.6f);
                    tex.SetPixel(x, y, new Color(c.r * ruido, c.g * ruido, c.b * ruido));
                }
            tex.wrapMode = TextureWrapMode.Repeat; tex.filterMode = FilterMode.Bilinear; tex.Apply();
            var rd = g.GetComponent<Renderer>();
            var mm = new Material(Shader.Find("Standard")) { color = Color.white };
            mm.mainTexture = tex; mm.mainTextureScale = new Vector2(Mathf.Max(1f, tamX * 2f), Mathf.Max(1f, tamZ * 2f));
            mm.SetFloat("_Glossiness", 0.05f);
            rd.sharedMaterial = mm;
        }

        /// La chimenea del modelo (hongo de ladrillo suspendido) y las piedras de apoyo salían mal: se ocultan y se pone una chimenea propia.
        void QuitarChimeneaYPiedras(GameObject casa)
        {
            var lad = Madera(casa, "cabinInteriorFireplace") ?? Madera(casa, "cabinExteriorFireplace");
            if (lad != null) ladrilloCabana = lad;
            int n = 0;
            foreach (var r in casa.GetComponentsInChildren<Renderer>(true))
                if (r.name.StartsWith("cabinInteriorFireplace") || r.name.StartsWith("cabinExteriorFireplace") || r.name.StartsWith("supportStones")) { r.enabled = false; n++; }
            foreach (var c in casa.GetComponentsInChildren<Collider>(true))
                if (c.name.StartsWith("cabinInteriorFireplace") || c.name.StartsWith("cabinExteriorFireplace") || c.name.StartsWith("supportStones")) c.enabled = false;
            plugin.Log("Cabañas: " + n + " piezas de chimenea/piedras de apoyo ocultas en " + casa.name);
        }

        /// Chimenea de ladrillo pegada a la pared: base, dos pilares, dintel y fondo; la fogata va dentro del hueco.
        void Chimenea(Transform pad, Vector3 C, Vector3 da, Vector3 db)
        {
            Quaternion q = Quaternion.LookRotation(-da, Vector3.up);
            Color lad = new Color(0.62f, 0.55f, 0.5f), oscuro = new Color(0.3f, 0.27f, 0.25f);
            PzCh(pad, C, da, db, q, "coop_chimenea_base", 0f, 0.09f, 0.05f, 1.75f, 0.18f, 0.95f, lad);
            PzCh(pad, C, da, db, q, "coop_chimenea_pilar", -0.68f, 0.93f, 0f, 0.4f, 1.5f, 0.74f, lad);
            PzCh(pad, C, da, db, q, "coop_chimenea_pilar", 0.68f, 0.93f, 0f, 0.4f, 1.5f, 0.74f, lad);
            PzCh(pad, C, da, db, q, "coop_chimenea_dintel", 0f, 1.86f, 0f, 1.76f, 0.36f, 0.74f, lad);
            PzCh(pad, C, da, db, q, "coop_chimenea_fondo", 0f, 0.93f, -0.3f, 0.95f, 1.5f, 0.1f, oscuro);
            Fogata.Crear(C + Vector3.up * 0.22f + (-da) * 0.0f, pad);
        }

        GameObject PzCh(Transform pad, Vector3 C, Vector3 da, Vector3 db, Quaternion q, string nom, float u, float v, float w, float sx, float sy, float sz, Color tin)
        {
            var g = Caja(pad, nom, C + db * u + Vector3.up * v + (-da) * w, new Vector3(sx, sy, sz), q, ladrilloCabana, tin, true);
            if (ladrilloCabana != null) g.GetComponent<Renderer>().material.mainTextureScale = new Vector2(Mathf.Max(sx, sz) * 1.3f, sy * 1.3f);
            return g;
        }

        /// Marco (ejes y medidas) del piso de una cabaña.
        static bool Marco(GameObject casa, out Vector3 c, out Vector3 da, out Vector3 db, out float la, out float lb, out float y0)
        {
            c = da = db = Vector3.zero; la = lb = y0 = 0f;
            BoxCollider piso = null;
            foreach (var bx in casa.GetComponentsInChildren<BoxCollider>(true)) if (bx.name.StartsWith("cabinFloor")) { piso = bx; break; }
            if (piso == null) return false;
            Transform t = piso.transform;
            int[] hor = new int[2]; int nh = 0;
            for (int i = 0; i < 3 && nh < 2; i++) { Vector3 d = new Vector3(i == 0 ? 1 : 0, i == 1 ? 1 : 0, i == 2 ? 1 : 0); if (Mathf.Abs(t.TransformDirection(d).y) < 0.5f) hor[nh++] = i; }
            if (nh < 2) return false;
            da = t.TransformDirection(hor[0] == 0 ? Vector3.right : hor[0] == 1 ? Vector3.up : Vector3.forward); da.y = 0f; da.Normalize();
            db = t.TransformDirection(hor[1] == 0 ? Vector3.right : hor[1] == 1 ? Vector3.up : Vector3.forward); db.y = 0f; db.Normalize();
            la = piso.size[hor[0]] * Mathf.Abs(t.lossyScale[hor[0]]); lb = piso.size[hor[1]] * Mathf.Abs(t.lossyScale[hor[1]]);
            c = t.TransformPoint(piso.center); y0 = piso.bounds.max.y;
            return true;
        }

        /// Escalera por dentro: escalones macizos a lo largo de la pared -a hasta un hueco abierto en el suelo de la planta alta.
        void EscaleraInterior(GameObject baja, GameObject alta)
        {
            Vector3 c, da, db, c2, da2, db2; float la, lb, y0, la2, lb2, y1;
            if (!Marco(baja, out c, out da, out db, out la, out lb, out y0) || !Marco(alta, out c2, out da2, out db2, out la2, out lb2, out y1)) { plugin.Log("Cabañas: no pude medir los pisos para la escalera interior"); return; }
            float subida = y1 - y0; if (subida < 1f) return;
            int N = Mathf.CeilToInt(subida / 0.22f); float alza = subida / N;
            float ancho = 1.1f, aC = -la * 0.5f + 0.2f + ancho * 0.5f;
            float b0 = -lb * 0.5f + 0.6f, b1 = lb * 0.5f - 0.25f, huella = (b1 - b0) / N;
            var madera = Madera(baja, "porchFloor") ?? Madera(baja, "cabinWallsExterior");
            Quaternion g = Quaternion.LookRotation(db, Vector3.up);
            for (int i = 0; i < N; i++)
            {
                float alto = alza * (i + 1);
                Vector3 p = c + da * aC + db * (b0 + huella * (i + 0.5f)); p.y = y0 + alto * 0.5f;
                Caja(baja.transform, "coop_escalon", p, new Vector3(ancho, alto, huella + 0.01f), g, madera, new Color(0.62f, 0.46f, 0.3f), true);
            }
            // HUECO REAL (2026-11-18): sobre los escalones de arriba, en coordenadas del mundo (antes se medía en ejes de la planta
            // alta y alguna pieza del modelo seguía tapándolo: la escalera daba contra el techo)
            int i0 = Mathf.Clamp(Mathf.CeilToInt((subida - 1.95f) / alza), 0, N - 1);
            Bounds hueco = new Bounds(); bool hayH = false;
            for (int i = i0; i < N; i++)
            {
                Vector3 p = c + da * aC + db * (b0 + huella * (i + 0.5f));
                foreach (var sa in new[] { -1f, 1f }) foreach (var sb in new[] { -1f, 1f })
                {
                    Vector3 e = p + da * sa * (ancho * 0.5f + 0.25f) + db * sb * (huella * 0.5f + 0.08f);
                    if (!hayH) { hueco = new Bounds(e, Vector3.zero); hayH = true; } else hueco.Encapsulate(e);
                }
            }
            hueco.Encapsulate(new Vector3(hueco.center.x, y1 - 0.9f, hueco.center.z));
            hueco.Encapsulate(new Vector3(hueco.center.x, y1 + 0.45f, hueco.center.z));
            // quitar TODA pieza plana (piso de arriba, cielo raso de abajo, vigas) que cruce el hueco
            int tapas = 0;
            foreach (var casaX in new[] { baja, alta })
            {
                foreach (var r in casaX.GetComponentsInChildren<Renderer>(true))
                    if (r.enabled && !r.name.StartsWith("coop_escalon") && r.bounds.size.y < 1.0f && r.bounds.Intersects(hueco)) { r.enabled = false; tapas++; }
                foreach (var col in casaX.GetComponentsInChildren<Collider>(true))
                    if (col.enabled && !col.isTrigger && !col.name.StartsWith("coop_escalon") && col.bounds.size.y < 1.0f && col.bounds.Intersects(hueco)) col.enabled = false;
            }
            // piso de arriba rehecho por filas de 0,5 m, saltando el hueco
            Color tc = new Color(0.62f, 0.46f, 0.3f); float gr = 0.22f, yc = y1 - gr * 0.5f, fila = 0.5f;
            Quaternion gq = Quaternion.LookRotation(db2, Vector3.up);
            int tablones = 0;
            for (float a = -la2 * 0.5f; a < la2 * 0.5f - 0.01f; a += fila)
            {
                float a1 = Mathf.Min(la2 * 0.5f, a + fila), am = (a + a1) * 0.5f;
                float bIni = float.NaN;
                int nb = Mathf.CeilToInt(lb2 / 0.25f);   // contador entero: con float se perdía la última vuelta y la fila no se cerraba
                for (int kb = 0; kb <= nb; kb++)
                {
                    bool ultima = kb == nb;
                    float bb = Mathf.Min(-lb2 * 0.5f + kb * 0.25f, lb2 * 0.5f);
                    Vector3 w = c2 + da2 * am + db2 * bb; w.y = y1;
                    bool libre = !ultima && !(w.x > hueco.min.x && w.x < hueco.max.x && w.z > hueco.min.z && w.z < hueco.max.z);
                    if (libre && float.IsNaN(bIni)) bIni = bb;
                    if ((!libre || ultima) && !float.IsNaN(bIni))
                    {
                        float bFin = Mathf.Min(bb, lb2 * 0.5f);
                        if (bFin - bIni > 0.05f)
                        {
                            Vector3 p = c2 + da2 * am + db2 * ((bIni + bFin) * 0.5f); p.y = yc;
                            Caja(alta.transform, "coop_suelo_alto", p, new Vector3(a1 - a - 0.01f, gr, bFin - bIni), gq, madera, tc, true);
                            tablones++;
                        }
                        bIni = float.NaN;
                    }
                }
            }
            vistasEscalera.Add(new Vector3[] { c + da * aC + db * (b0 - 1.2f) + Vector3.up * (y0 - c.y + 1.6f), hueco.center });
            plugin.Log("Cabañas: hueco de la escalera abierto (" + tapas + " piezas quitadas, " + tablones + " tablones de piso)");
            plugin.Log("Cabañas: escalera interior de " + N + " escalones (alza " + alza.ToString("0.00") + " m), hueco desde el escalón " + i0);
        }
    }
}

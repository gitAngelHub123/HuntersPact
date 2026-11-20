// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using System.Collections.Generic;
using System.IO;
using PixelCrushers.DialogueSystem;
using UnityEngine;

namespace WitchHuntCoop
{
    /// Las dos minas abandonadas de la ballesta (lugares únicos: una vez por partida, en la zona norte).
    /// En la superficie solo está la boca: un cerro de rocas del juego con un marco de vigas. Con E se
    /// entra a la mina, una red de túneles hecha por el mod fuera del mapa (el terreno no admite
    /// agujeros): pasillos con vigas, rieles, faroles y rocas, con bifurcaciones, vueltas y callejones
    /// sin salida. Al fondo (el punto más lejano de la entrada) hay un minero muerto con su ballesta.
    /// Quien la recoge es el único que la tiene; esa ballesta desaparece también para el compañero.
    public partial class Mundo
    {
        public const string VarBallestasTomadas = "coop_ballestas_tomadas";
        const int MinaAncho = 12, MinaFondo = 12;             // nodos de la red
        const float MinaPaso = 9f, MinaNodo = 4.6f, MinaPasillo = 3.2f, MinaAlto = 3.4f;

        class Mina
        {
            public int N; public GameObject Boca, Red, Ballesta;
            public Vector3 Fuera, Dentro, PuertaFuera, PuertaDentro, PosBallesta;
            public int Nodos, Pasillos, Faroles; public bool Guarida; public Vector3 CentroRed; public float RadioRed = 90f;
        }
        readonly List<Mina> minas = new List<Mina>();

        /// Dónde se arma cada mina (fuera del mapa, al oeste). 2026-11-18: antes iban cada 160 m y el paisaje de la salida de una
        /// (árboles secos, acantilado) se metía dentro de los túneles de la otra: ahora cada 450 m.
        public const float SeparacionMinas = 450f;
        public static Vector3 OrigenMina(Vector3 ancla, int n) { return ancla + new Vector3(-420f, 70f, 320f + n * SeparacionMinas); }
        /// Rectángulo (x, z del mundo) que ocupa la mina n con el paisaje de su salida, para que el bosque exterior no la atraviese.
        public static Rect ZonaMina(Vector3 ancla, int n)
        {
            Vector3 o = OrigenMina(ancla, n);
            return n < 2 ? new Rect(o.x - 95f, o.z - 135f, 95f + MinaAncho * MinaPaso + 10f, 135f + MinaFondo * MinaPaso + 10f)
                         : new Rect(o.x - CuevaAX - 25f, o.z - 135f, 2f * CuevaAX + 50f, 135f + CuevaZ0 + 2f * CuevaAZ + 25f);   // guarida: la cueva (doble desde 2026-11-19)
        }
        Material matRoca, matSuelo, matMadera, matHierro, matNegro, matLuzDia;

        List<Vector4> huellasMinas;
        readonly List<Vector3> arbolesMinaPend = new List<Vector3>();

        /// Agrega al terreno norte los árboles gigantes pedidos alrededor de las bocas de las minas.
        void AgregarArbolesMinas()
        {
            if (nuevo == null || datosNuevos == null) { arbolesMinaPend.Clear(); return; }
            var dn = datosNuevos; var lista = new List<TreeInstance>(dn.treeInstances); var rt = new System.Random(55);
            Vector3 o = nuevo.transform.position; int n = 0;
            // ningún árbol encima de una entrada (los bosquecillos del llano podían caer sobre la boca)
            // 2026-11-18: ningún árbol toca la boca (antes 16 m: los troncos y ramas grandes la tapaban)
            int quitados = lista.RemoveAll(ti => { Vector3 w = new Vector3(ti.position.x * AnchoNuevo, 0f, ti.position.z * FondoNuevo) + o; foreach (var m in minas) if (m.Boca != null && new Vector2(w.x - m.Fuera.x, w.z - m.Fuera.z).magnitude < 30f + 4f * ti.widthScale) return true; return false; });
            if (quitados > 0) plugin.Log("Minas: " + quitados + " árboles quitados de las entradas");
            foreach (var w in arbolesMinaPend)
            {
                float x = w.x - o.x, z = w.z - o.z; if (x < 130f || x > AnchoNuevo - 130f || z < 100f || z > FondoNuevo - 130f || EnZonaCastillo(x, z, 40f)) continue;
                // lejos de cualquier boca: la copa de un árbol gigante mide decenas de metros
                bool pegado = false; foreach (var m in minas) if (m.Boca != null && new Vector2(w.x - m.Fuera.x, w.z - m.Fuera.z).magnitude < 55f) pegado = true;
                if (pegado) continue;
                float ev = 3f + (float)rt.NextDouble() * 1.5f; var pos = new Vector3(x / AnchoNuevo, 0f, z / FondoNuevo);
                pos.y = dn.GetInterpolatedHeight(pos.x, pos.z) / dn.size.y;
                lista.Add(new TreeInstance { prototypeIndex = rt.Next(dn.treePrototypes.Length), position = pos, widthScale = ev, heightScale = ev, color = Color.white, lightmapColor = Color.white }); n++;
            }
            arbolesMinaPend.Clear();
            dn.treeInstances = lista.ToArray(); nuevo.Flush();
            var col = nuevo.GetComponent<TerrainCollider>(); if (col != null) { col.enabled = false; col.enabled = true; }
            plugin.Log("Minas: " + n + " árboles gigantes alrededor de las bocas");
        }
        bool ambienteMinaActivo; UnityEngine.Rendering.AmbientMode ambAntesModo; Color ambAntesColor;

        void ColocarMinas(int semillaPartida)
        {
            minas.Clear(); huellasMinas = null; LimpiarEnemigosMinas();
            arbolesMinaPend.Clear();
            PrepararMateriales();
            var tomadas = ListaLua(VarBallestasTomadas);
            var rng = new System.Random(semillaPartida ^ Hash("coop_minas_ballesta"));
            var ocupados = new List<Vector3> { cabana };
            foreach (var c in casas) if (c.Raiz != null) ocupados.Add(c.Raiz.transform.position);
            for (int n = 0; n < 3; n++)   // 0 y 1: minas de la ballesta; 2: la guarida del murciélago (sin ballesta)
            {
                Vector3 sitio; float yaw;
                // (2026-11-19) con 300 m de separación a veces no había sitio y la mina no se creaba (en una partida faltó la guarida del
                // jefe opcional): se reintenta con separaciones menores antes de rendirse
                bool ubicada = false; sitio = Vector3.zero; yaw = 0f;
                foreach (float sep in new[] { 300f, 220f, 160f, 110f })
                    if (UbicarNorte(rng, ocupados, sep, out sitio, out yaw)) { ubicada = true; if (sep < 300f) plugin.Log("Minas: boca " + (n + 1) + " ubicada con " + sep + " m de separación"); break; }
                if (!ubicada) { plugin.Log("Minas: sin sitio para la boca " + (n + 1)); continue; }
                ocupados.Add(sitio);
                var m = new Mina { N = n };
                ArmarBoca(m, sitio, yaw, rng);
                if (n < 2) ArmarRed(m, new System.Random(semillaPartida ^ Hash("coop_mina_red_" + n)), !tomadas.Contains("mina" + n));
                else ArmarGuarida(m, new System.Random(semillaPartida ^ Hash("coop_mina_red_" + n)));
                minas.Add(m);
                plugin.Log("Minas: mina " + (n + 1) + " con boca en " + (sitio - Ancla()).ToString("F0") + ", " + m.Nodos + " cruces, " + m.Pasillos + " pasillos, " + m.Faroles + " faroles" + (tomadas.Contains("mina" + n) ? " (ballesta ya tomada)" : ""));
            }
            AgregarArbolesMinas();
        }

        /// Como Ubicar, pero solo en la zona norte nueva (o en el tercio norte del bosque si no hay).
        bool UbicarNorte(System.Random rng, List<Vector3> lejosDe, float separacion, out Vector3 pos, out float yaw)
        {
            pos = Vector3.zero; yaw = 0f;
            Vector3 o = Ancla();
            Bounds z = nuevo != null
                ? new Bounds(nuevo.transform.position + new Vector3(AnchoNuevo / 2f, 50f, FondoNuevo / 2f), new Vector3(AnchoNuevo - 2f * Pared - 60f, 200f, FondoNuevo - Pared - 60f))
                : new Bounds(o + new Vector3(1000f, 50f, 1550f), new Vector3(1500f, 200f, 500f));
            for (int k = 0; k < 300; k++)
            {
                Vector3 c = new Vector3(z.min.x + (float)rng.NextDouble() * z.size.x, z.center.y, z.min.z + (float)rng.NextDouble() * z.size.z);
                yaw = (float)rng.NextDouble() * 360f;
                c.y = AlturaEn(c);
                if (float.IsNaN(c.y) || c.y < nivelAgua + 1f || EnCastillo(c)) continue;
                bool cerca = false;
                foreach (var q in lejosDe) if (Vector3.Distance(c, q) < separacion) { cerca = true; break; }
                if (cerca) continue;
                float y; if (!Llano(c, 7f, 2f, out y)) continue;
                if (Choca(huellasMinas ?? (huellasMinas = Huellas()), c.x, c.z, 10f, 3f) || !Despejado(new Vector3(c.x, y, c.z), 12f)) continue;
                UnityEngine.AI.NavMeshHit nh;
                if (!UnityEngine.AI.NavMesh.SamplePosition(c, out nh, 10f, UnityEngine.AI.NavMesh.AllAreas)) continue;
                if (nuevo != null && HayEntradaNorte && (!UnityEngine.AI.NavMesh.CalculatePath(EntradaNorte, nh.position, UnityEngine.AI.NavMesh.AllAreas, camino) || camino.status != UnityEngine.AI.NavMeshPathStatus.PathComplete)) continue;
                pos = new Vector3(c.x, y, c.z);
                return true;
            }
            return false;
        }

        void PrepararMateriales()
        {
            if (matRoca != null) return;
            var raices = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (var r in Object.FindObjectsOfType<MeshRenderer>())
            {
                string b = Catalogo.BaseDe(r.transform.root.name);
                if (matRoca == null && (b == "Rock13" || b == "Rock10" || b == "Rock1") && r.sharedMaterial != null) matRoca = r.sharedMaterial;
                if (matMadera == null && r.name.StartsWith("porchPole") && r.sharedMaterial != null) matMadera = r.sharedMaterial;
            }
            // Texturas del terreno: una de roca para paredes y techo, otra de tierra para el piso.
            var t = Anclaje.Principal();
            if (t != null && t.terrainData != null && t.terrainData.splatPrototypes.Length > 0)
            {
                var sp = t.terrainData.splatPrototypes;
                Texture2D tierra = sp[0].texture, piedra = null;
                var nombres = new List<string>();
                foreach (var x in sp)
                {
                    if (x.texture == null) continue;
                    string n = x.texture.name.ToLowerInvariant(); nombres.Add(x.texture.name);
                    if (piedra == null && (n.Contains("rock") || n.Contains("cliff") || n.Contains("stone") || n.Contains("roca"))) piedra = x.texture;
                    if (n.Contains("dirt") || n.Contains("mud") || n.Contains("soil")) tierra = x.texture;
                }
                plugin.Log("Minas: texturas del terreno: " + string.Join(", ", nombres.ToArray()) + " → paredes " + (piedra != null ? piedra.name : "(roca del juego)") + ", piso " + (tierra != null ? tierra.name : "-"));
                matSuelo = new Material(Shader.Find("Standard")) { name = "coop_mina_suelo", mainTexture = tierra };
                matSuelo.SetFloat("_Glossiness", 0.05f); matSuelo.color = new Color(0.55f, 0.5f, 0.45f);
                if (piedra != null)
                {
                    matRoca = new Material(Shader.Find("Standard")) { name = "coop_mina_roca", mainTexture = piedra };
                    matRoca.SetFloat("_Glossiness", 0.12f); matRoca.color = new Color(0.62f, 0.6f, 0.58f);
                }
            }
            if (matRoca == null) { matRoca = new Material(Shader.Find("Standard")) { name = "coop_mina_roca" }; matRoca.color = new Color(0.3f, 0.3f, 0.3f); }
            if (matSuelo == null) matSuelo = matRoca;
            if (matMadera == null) { matMadera = new Material(Shader.Find("Standard")) { name = "coop_mina_madera", mainTexture = Escopeta.TexturaEscopeta() }; }
            // texturas propias de la mina (con relieve): roca con vetas y grietas, tierra con guijarros, madera de tablones y hierro oxidado
            Texture2D ar, nr, asu, nsu, am, nm, ah, nh;
            TexRocaMina(out ar, out nr, 256); TexTierraMina(out asu, out nsu, 256); TexMaderaMina(out am, out nm, 128); TexHierro(out ah, out nh, 128);
            matRoca = Estandar("coop_mina_roca", ar, nr, Color.white, 0.06f, 0.8f);
            matSuelo = Estandar("coop_mina_suelo", asu, nsu, Color.white, 0.04f, 0.8f);
            matMadera = Estandar("coop_mina_madera", am, nm, new Color(1.2f, 1.2f, 1.2f), 0.1f, 0.8f);
            // madera REAL del juego (2026-11-18: la dibujada no convencía al lado de la roca): los postes del porche de la cabaña
            foreach (var g in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (g.name != "THE_WOOD_CABIN_02") continue;
                Material poste = null;
                foreach (var r in g.GetComponentsInChildren<MeshRenderer>(true))
                    if (r.sharedMaterial != null && r.sharedMaterial.mainTexture != null && (r.name.StartsWith("porchPole") || r.name.StartsWith("cabinWoodWork") || r.name.StartsWith("wallTimberJoint"))) { poste = r.sharedMaterial; break; }
                if (poste != null)
                {
                    matMadera = new Material(poste) { name = "coop_mina_madera" };
                    matMadera.color = new Color(0.55f, 0.45f, 0.36f);   // vieja, oscurecida por la humedad y el hollín
                    matMadera.mainTextureScale = new Vector2(1f, 1f);
                    matMadera.SetFloat("_Glossiness", 0.08f);
                    matMadera.SetFloat("_GlossyReflections", 0f); matMadera.EnableKeyword("_GLOSSYREFLECTIONS_OFF");
                    plugin.Log("Minas: madera de los túneles = " + poste.name + " (de la cabaña del juego)");
                }
                break;
            }
            matHierro = Estandar("coop_mina_hierro", ah, nh, new Color(0.9f, 0.85f, 0.82f), 0.4f, 1.2f); matHierro.SetFloat("_Metallic", 0.6f);
            matNegro = new Material(Shader.Find("Unlit/Color")) { name = "coop_mina_oscuro" }; matNegro.color = Color.black;
            matLuzDia = new Material(Shader.Find("Unlit/Color")) { name = "coop_mina_salida" }; matLuzDia.color = new Color(0.16f, 0.19f, 0.24f);
        }

        // ---------------- Texturas de las minas ----------------

        static void TexRocaMina(out Texture2D albedo, out Texture2D normal, int n)
        {
            var col = new Color[n * n]; var alt = new float[n, n];
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                float g = Ruido(x, y, n, 0.04f) * 0.4f + Ruido(x + 17, y + 3, n, 0.13f) * 0.3f + Ruido(x + 40, y + 90, n, 0.45f) * 0.2f + Ruido(x + 5, y + 11, n, 1.0f) * 0.1f;
                Color c = Color.Lerp(new Color(0.2f, 0.16f, 0.13f), new Color(0.6f, 0.52f, 0.44f), Mathf.Clamp01(g * 1.15f - 0.08f));
                c *= 0.92f + 0.08f * Mathf.Sin(y * 0.2f + Ruido(x, y, n, 0.03f) * 5f);   // estratos casi imperceptibles
                float hoyo = Ruido(x + 80, y + 10, n, 0.9f);
                float h = g;
                if (hoyo > 0.7f) { float k = Mathf.Clamp01((hoyo - 0.7f) * 6f); c *= 1f - 0.45f * k; h -= 0.25f * k; }   // pequeños huecos de la roca
                col[y * n + x] = new Color(c.r, c.g, c.b, 1f); alt[x, y] = h;
            }
            albedo = new Texture2D(n, n, TextureFormat.RGB24, true) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Repeat }; albedo.SetPixels(col); albedo.Apply();
            normal = Normales(alt, n, 2.2f);
        }

        static void TexTierraMina(out Texture2D albedo, out Texture2D normal, int n)
        {
            var col = new Color[n * n]; var alt = new float[n, n];
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                float g = Ruido(x, y, n, 0.07f) * 0.5f + Ruido(x + 9, y + 33, n, 0.3f) * 0.5f;
                Color c = Color.Lerp(new Color(0.2f, 0.145f, 0.095f), new Color(0.5f, 0.38f, 0.26f), g);
                float p = Ruido(x + 70, y + 12, n, 0.62f);   // guijarros: manchas altas de ruido fino
                float h = g * 0.4f;
                if (p > 0.66f) { float k = Mathf.Clamp01((p - 0.66f) * 5f); c = Color.Lerp(c, new Color(0.4f, 0.38f, 0.35f) * (0.7f + g * 0.5f), k); h += k * 0.7f; }
                col[y * n + x] = new Color(c.r, c.g, c.b, 1f); alt[x, y] = h;
            }
            albedo = new Texture2D(n, n, TextureFormat.RGB24, true) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Repeat }; albedo.SetPixels(col); albedo.Apply();
            normal = Normales(alt, n, 1.8f);
        }

        static void TexMaderaMina(out Texture2D albedo, out Texture2D normal, int n)
        {
            var col = new Color[n * n]; var alt = new float[n, n];
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                float veta = Mathf.Sin(x * 0.55f + Ruido(x, y, n, 0.04f) * 9f) * 0.5f + 0.5f;   // la veta corre a lo largo (eje y)
                float g = Ruido(x * 2 % n, y, n, 0.03f) * 0.5f + veta * 0.5f;
                Color c = Color.Lerp(new Color(0.07f, 0.045f, 0.03f), new Color(0.3f, 0.19f, 0.1f), g);
                if (x % 32 < 1 || x % 32 > 30) c *= 0.4f;   // junta entre tablones
                col[y * n + x] = new Color(c.r, c.g, c.b, 1f); alt[x, y] = g * 0.6f - (x % 32 < 1 ? 0.5f : 0f);
            }
            albedo = new Texture2D(n, n, TextureFormat.RGB24, true) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Repeat }; albedo.SetPixels(col); albedo.Apply();
            normal = Normales(alt, n, 1.5f);
        }

        // ---------------- Boca en la superficie ----------------

        /// Un modelo de cada tipo de roca del bosque que se pueda copiar entero (para las rocas pequeñas de las minas).
        List<GameObject> RocasChicas()
        {
            var l = new List<GameObject>(); var vistos = new HashSet<string>();
            foreach (var g in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (g == null) continue;
                string b = Catalogo.BaseDe(g.name);
                if (!(b == "Rock1" || b == "Rock5" || b == "Rock8" || b == "Rock9" || b == "Rock10" || b == "Rock13" || b == "Rock14") || vistos.Contains(b)) continue;
                int hh, ee; Coop.Piezas.Calidad(g, out hh, out ee);
                if (hh > 0 && ee == hh) { l.Add(g); vistos.Add(b); }
            }
            return l;
        }

        /// La boca de la mina excavada en un cerro (pedido 2026-11-17: que se vea natural y se camufle con árboles y rocas): se levanta el
        /// terreno detrás y a los lados de la entrada (hasta ~10 m, con laderas suaves), sin tocar el camino de acceso, y se ponen matorrales
        /// y algunos árboles alrededor. Va antes de las rocas, así quedan medio enterradas en la ladera.
        void CerroBoca(Vector3 sitio, Quaternion giro, System.Random rng)
        {
            Terrain t = null; foreach (var te in Terrain.activeTerrains) { var to = te.transform.position; var ts = te.terrainData.size; if (sitio.x >= to.x && sitio.x <= to.x + ts.x && sitio.z >= to.z && sitio.z <= to.z + ts.z) { t = te; break; } }
            if (t == null) return;
            var td = t.terrainData; Vector3 o = t.transform.position; int R = td.heightmapResolution;
            float px = td.size.x / (R - 1), pz = td.size.z / (R - 1), alto = td.size.y;
            Vector3 fw = giro * Vector3.forward, der = giro * Vector3.right;
            const float Radio = 34f, Cima = 10f;
            int x0 = Mathf.Clamp(Mathf.FloorToInt((sitio.x - o.x - Radio - 4f) / px), 0, R - 1), x1 = Mathf.Clamp(Mathf.CeilToInt((sitio.x - o.x + Radio + 4f) / px), 0, R - 1);
            int z0 = Mathf.Clamp(Mathf.FloorToInt((sitio.z - o.z - Radio - 4f) / pz), 0, R - 1), z1 = Mathf.Clamp(Mathf.CeilToInt((sitio.z - o.z + Radio + 4f) / pz), 0, R - 1);
            int w = x1 - x0 + 1, h = z1 - z0 + 1; if (w < 2 || h < 2) return;
            var hs = td.GetHeights(x0, z0, w, h);
            float baseY = (sitio.y - o.y) / alto; float sem = (float)rng.NextDouble() * 100f;
            for (int zi = 0; zi < h; zi++) for (int xi = 0; xi < w; xi++)
            {
                Vector3 p = new Vector3(o.x + (x0 + xi) * px, 0f, o.z + (z0 + zi) * pz) - sitio;
                float lx = Vector3.Dot(p, der), lz = Vector3.Dot(p, fw);
                // centro del cerro detrás de la boca; el frente (z < 2) baja hasta el suelo para dejar libre la entrada
                float dx = lx / 1.25f, dz = lz - 18f, d = Mathf.Sqrt(dx * dx + dz * dz);
                if (d > Radio) continue;
                float forma = Mathf.SmoothStep(0f, 1f, 1f - d / Radio) * (0.85f + 0.3f * Mathf.PerlinNoise(sem + p.x * 0.08f, sem + p.z * 0.08f));
                float frente = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-2f, 5f, lz)) * Mathf.Max(Mathf.SmoothStep(0f, 1f, (Mathf.Abs(lx) - 6f) / 5f), Mathf.SmoothStep(0f, 1f, (lz - 12f) / 5f));   // corredor de la entrada (|x| < 6, z < 12) plano: nunca tapa la escalera
                float y = baseY + Cima * forma * frente / alto;
                if (y > hs[zi, xi]) hs[zi, xi] = y;
            }
            td.SetHeights(x0, z0, hs);
            // matorrales y algunos árboles alrededor (instancias del terreno), nunca en el camino de entrada
            var lista = new List<TreeInstance>(td.treeInstances); int ix = -1;
            for (int i = 0; i < td.treePrototypes.Length; i++) if (td.treePrototypes[i].prefab != null && td.treePrototypes[i].prefab.name.IndexOf("Bush", System.StringComparison.OrdinalIgnoreCase) >= 0) { ix = i; break; }
            int puestos = 0;
            for (int k = 0; k < 26; k++)
            {
                float ang = (float)rng.NextDouble() * 360f, rad = 6f + (float)rng.NextDouble() * 26f;
                Vector3 q = sitio + fw * 10f + Quaternion.Euler(0f, ang, 0f) * fw * rad - sitio;
                float lx = Vector3.Dot(q, der), lz = Vector3.Dot(q, fw);
                if (Mathf.Abs(lx) < 7f && lz < 12f) continue;   // entrada y su camino
                Vector3 wpos = sitio + q; float nx = (wpos.x - o.x) / td.size.x, nz = (wpos.z - o.z) / td.size.z;
                if (nx < 0f || nx > 1f || nz < 0f || nz > 1f) continue;
                bool arbol = k % 4 == 0 || ix < 0; float ev = arbol ? 1.4f + (float)rng.NextDouble() * 1.2f : 0.8f + (float)rng.NextDouble() * 0.7f;
                int proto = arbol ? rng.Next(td.treePrototypes.Length) : ix;
                lista.Add(new TreeInstance { prototypeIndex = proto, position = new Vector3(nx, td.GetInterpolatedHeight(nx, nz) / alto, nz), widthScale = ev, heightScale = ev, color = Color.white, lightmapColor = Color.white });
                puestos++;
            }
            td.treeInstances = lista.ToArray(); t.Flush();
            if (t != nuevo) RefrescarHierbaATG(t);
            plugin.Log("Minas: cerro de " + Cima + " m sobre la boca en (" + Mathf.RoundToInt(sitio.x) + ", " + Mathf.RoundToInt(sitio.z) + ") con " + puestos + " matorrales y árboles");
        }

        void ArmarBoca(Mina m, Vector3 sitio, float yaw, System.Random rng)
        {
            Quaternion giro = Quaternion.Euler(0f, yaw, 0f);
            Vector3 fw = giro * Vector3.forward, der = giro * Vector3.right;
            try { CerroBoca(sitio, giro, rng); } catch (System.Exception e) { plugin.Log("Minas: error en el cerro de la boca " + e.Message); }
            var boca = new GameObject("coop_mina_boca_" + (m.N + 1));
            boca.transform.position = sitio; boca.transform.rotation = giro;
            m.Boca = boca;
            // Cerro de rocas detrás y a los lados del marco.
            GameObject roca = null;
            foreach (var g in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                if (Catalogo.BaseDe(g.name) == "Rock13") { roca = g; break; }
            if (roca != null)
            {
                // (x de su borde interior o centro, base, borde delantero, escala). Las rocas van con el giro de la
                // boca, así su caja se conoce y se acomodan sin tapar la entrada (túnel de 3,5 × 3,4 m).
                PonerRoca(boca, roca, sitio, giro, -1, -0.9f, -1.2f, 1.7f, 0);
                PonerRoca(boca, roca, sitio, giro, 1, -0.9f, -1.2f, 1.7f, 180);
                PonerRoca(boca, roca, sitio, giro, 0, 3.55f, 1.5f, 1.5f, 90);   // más atrás: colgaba delante de la entrada y la tapaba (2026-11-17)
                PonerRoca(boca, roca, sitio, giro, 0, -0.9f, 5.0f, 2.3f, 0);
                PonerRoca(boca, roca, sitio, giro, 0, 5.2f, 2.5f, 1.6f, 270);
                PonerRoca(boca, roca, sitio, giro, -2, 1.5f, 4.0f, 1.8f, 180);
                PonerRoca(boca, roca, sitio, giro, 2, 1.5f, 4.0f, 1.8f, 0);
            }
            // Peñascos grandes alineados con la mina (pedido 2026-11-13): dos hileras a los lados del camino de entrada y una pared de roca al fondo, todos con el
            // eje largo paralelo a la mina, para que la boca parezca excavada en un cerro y no rodeada de piedras sueltas. El camino de salida (x entre -5 y 5, z < 9) queda libre.
            if (roca != null)
            {
                int nr = 0; var rr = new System.Random((int)(sitio.x * 7f + sitio.z * 13f));
                // (x, z, escala) en los ejes de la mina (z crece hacia el cerro)
                float[][] sitios = {
                    new[] { -10f, 6f, 2.3f }, new[] { 10f, 6f, 2.3f }, new[] { -11.5f, 15f, 2.7f }, new[] { 11.5f, 15f, 2.7f },
                    new[] { -13f, 25f, 2.9f }, new[] { 13f, 25f, 2.9f }, new[] { -5f, 21f, 3.1f }, new[] { 5f, 21f, 3.1f }, new[] { 0f, 29f, 3.4f },
                    new[] { -9f, -4f, 1.9f }, new[] { 9f, -4f, 1.9f }, new[] { -17f, 12f, 2.2f }, new[] { 17f, 12f, 2.2f },
                };
                foreach (var st in sitios)
                {
                    Vector3 pos = sitio + der * st[0] + fw * st[1]; float hy = AlturaEn(pos);
                    if (float.IsNaN(hy)) hy = sitio.y;
                    float yaw2 = (rr.NextDouble() < 0.5 ? 0f : 180f) + ((float)rr.NextDouble() - 0.5f) * 16f;   // eje largo paralelo a la mina, con un poco de desorden natural
                    int f2; var c = Coop.Piezas.Clonar(roca, pos, giro * Quaternion.Euler(0f, yaw2, 0f) * Quaternion.Inverse(Quaternion.Euler(0f, roca.transform.eulerAngles.y, 0f)) * roca.transform.rotation, out f2);
                    if (c == null) continue;
                    c.transform.localScale = roca.transform.localScale * st[2];
                    Bounds bb = new Bounds(); bool hb = false;
                    foreach (var rd in c.GetComponentsInChildren<Renderer>()) { if (!hb) { bb = rd.bounds; hb = true; } else bb.Encapsulate(rd.bounds); }
                    if (hb) c.transform.position += new Vector3(pos.x - bb.center.x, hy - bb.min.y - 0.7f, pos.z - bb.center.z);   // medio hundida en el suelo
                    c.transform.SetParent(boca.transform, true); nr++;
                }
                plugin.Log("Minas: " + nr + " peñascos alineados alrededor de la boca " + (m.N + 1));
            }
            // Rocas pequeñas alrededor de la boca (pedido 2026-11-17): en grupitos, medio enterradas y de varios tipos, para que la mina se
            // camufle con el bosque y no parezca puesta encima. El camino de entrada (x entre -6 y 6, z < 9 en los ejes de la mina) queda libre.
            var chicas = RocasChicas();
            if (chicas.Count > 0)
            {
                var rc = new System.Random(((int)(sitio.x * 31f + sitio.z * 17f)) ^ 0x2f6b);
                int nc = 0;
                for (int grupo = 0; grupo < 11; grupo++)
                {
                    float ang = (float)rc.NextDouble() * 360f, rad = 7f + (float)rc.NextDouble() * 24f;
                    Vector3 centroG = sitio + Quaternion.Euler(0f, ang, 0f) * fw * rad;
                    int cuantas = 2 + rc.Next(4);
                    for (int k = 0; k < cuantas; k++)
                    {
                        Vector3 pos = centroG + new Vector3(((float)rc.NextDouble() - 0.5f) * 5f, 0f, ((float)rc.NextDouble() - 0.5f) * 5f);
                        Vector3 loc = Quaternion.Inverse(giro) * (pos - sitio);
                        if (Mathf.Abs(loc.x) < 6f && loc.z < 9f) continue;
                        float hy = AlturaEn(pos); if (float.IsNaN(hy)) continue;
                        var modelo = chicas[rc.Next(chicas.Count)];
                        Quaternion rot = Quaternion.Euler(((float)rc.NextDouble() - 0.5f) * 30f, (float)rc.NextDouble() * 360f, ((float)rc.NextDouble() - 0.5f) * 30f) * modelo.transform.rotation;
                        int f3; var c = Coop.Piezas.Clonar(modelo, pos, rot, out f3);
                        if (c == null) continue;
                        Bounds bb;
                        if (!CajaDe(new List<GameObject> { c }, out bb)) { Object.Destroy(c); continue; }
                        float objetivo = 0.35f + Mathf.Pow((float)rc.NextDouble(), 2f) * 1.9f;   // casi todas pequeñas (0,35–2,2 m)
                        c.transform.localScale = c.transform.localScale * (objetivo / Mathf.Max(0.05f, Mathf.Max(bb.size.x, Mathf.Max(bb.size.y, bb.size.z))));
                        CajaDe(new List<GameObject> { c }, out bb);
                        c.transform.position += new Vector3(pos.x - bb.center.x, hy - bb.min.y - bb.size.y * 0.3f, pos.z - bb.center.z);   // un tercio enterrada
                        c.name = "coop_mina_roca_chica";
                        c.transform.SetParent(boca.transform, true); nc++;
                    }
                }
                plugin.Log("Minas: " + nc + " rocas pequeñas alrededor de la boca " + (m.N + 1));
            }
            // Árboles gigantes alrededor de la boca (pedido 2026-11-13), salvo por el camino de salida y cerca de los peñascos
            for (int k = 0; k < 26; k++)
            {
                float ang = (float)rng.NextDouble() * 360f, rad = 58f + (float)rng.NextDouble() * 50f;   // 2026-11-18: más lejos (36 m tocaban la boca)
                Vector3 dirT = Quaternion.Euler(0f, ang, 0f) * fw;
                if (Vector3.Dot(dirT, -fw) > 0.93f && rad < 60f) continue;
                arbolesMinaPend.Add(sitio + dirT * rad);
            }
            // Marco de vigas y la oscuridad del túnel detrás.
            var b = new Constructor { Semilla = m.N * 37.3f + 5f };
            float ancho = 3.2f, alto = 3.1f;
            b.Viga(new Vector3(-ancho / 2f, 0f, 0f), new Vector3(-ancho / 2f, alto, 0f), 0.28f, 1);
            b.Viga(new Vector3(ancho / 2f, 0f, 0f), new Vector3(ancho / 2f, alto, 0f), 0.28f, 1);
            b.Viga(new Vector3(-ancho / 2f - 0.3f, alto + 0.14f, 0f), new Vector3(ancho / 2f + 0.3f, alto + 0.14f, 0f), 0.3f, 1);
            // túnel corto que se pierde en la oscuridad (paredes de roca y fondo negro)
            b.Caja(new Vector3(-ancho / 2f - 0.15f, -0.5f, 0.1f), new Vector3(ancho / 2f + 0.15f, alto + 0.3f, 5f), 0, true);
            b.Quad(new Vector3(-ancho / 2f, -0.5f, 4.8f), new Vector3(ancho / 2f, -0.5f, 4.8f), new Vector3(ancho / 2f, alto + 0.3f, 4.8f), new Vector3(-ancho / 2f, alto + 0.3f, 4.8f), 4, true);
            // rieles que salen de la boca
            for (float z = -3f; z < 4.5f; z += 0.7f) b.Viga(new Vector3(-0.75f, 0.04f, z), new Vector3(0.75f, 0.04f, z), 0.1f, 1);
            b.Viga(new Vector3(-0.5f, 0.12f, -3.2f), new Vector3(-0.5f, 0.12f, 4.6f), 0.06f, 3);
            b.Viga(new Vector3(0.5f, 0.12f, -3.2f), new Vector3(0.5f, 0.12f, 4.6f), 0.06f, 3);
            MaterialesCueva();
            var go = b.Crear("coop_mina_marco", new[] { matTunel, matMadera, matCuevaSuelo, matHierro, matNegro }, true);
            go.transform.SetParent(boca.transform, false);
            // farol colgado del marco
            Farol(boca.transform, new Vector3(ancho / 2f + 0.2f, alto - 0.4f, -0.25f));
            m.PuertaFuera = sitio + fw * 0.6f + Vector3.up * 1f;
            m.Fuera = sitio - fw * 2.2f + Vector3.up * 0.3f;
            lugaresCopias.Add(boca);
        }

        /// Roca alineada con la boca. lado: -1/1 = a la izquierda/derecha de la entrada (su borde interior
        /// a 1,9 m del centro); 0 = centrada; -2/2 = bien a los lados. base = altura de su parte de abajo;
        /// frente = z de su borde delantero (z crece hacia el cerro).
        void PonerRoca(GameObject boca, GameObject roca, Vector3 sitio, Quaternion giro, int lado, float baseY, float frente, float escala, float extraYaw)
        {
            int f;
            Quaternion rot = giro * Quaternion.Euler(0f, extraYaw, 0f);
            var c = Coop.Piezas.Clonar(roca, sitio, rot * Quaternion.Inverse(Quaternion.Euler(0f, roca.transform.eulerAngles.y, 0f)) * roca.transform.rotation, out f);
            c.transform.localScale = roca.transform.localScale * escala;
            c.transform.SetParent(boca.transform, true);
            // Caja de la roca en los ejes de la boca (a partir de las esquinas de su caja dibujada).
            Bounds bl = new Bounds(); bool hay = false;
            Quaternion inv = Quaternion.Inverse(giro);
            foreach (var r in c.GetComponentsInChildren<MeshFilter>())
            {
                if (r.sharedMesh == null) continue;
                var mb = r.sharedMesh.bounds;
                for (int k = 0; k < 8; k++)
                {
                    Vector3 esq = new Vector3((k & 1) == 0 ? mb.min.x : mb.max.x, (k & 2) == 0 ? mb.min.y : mb.max.y, (k & 4) == 0 ? mb.min.z : mb.max.z);
                    Vector3 l = inv * (r.transform.TransformPoint(esq) - sitio);
                    if (!hay) { bl = new Bounds(l, Vector3.zero); hay = true; } else bl.Encapsulate(l);
                }
            }
            if (!hay) return;
            float dx = lado == -1 ? (-1.9f - bl.max.x) : lado == 1 ? (1.9f - bl.min.x) : lado == -2 ? (-3.5f - bl.center.x - bl.extents.x * 0.5f) : lado == 2 ? (3.5f - bl.center.x + bl.extents.x * 0.5f) : -bl.center.x;
            Vector3 mover = new Vector3(dx, baseY - bl.min.y, frente - bl.min.z);
            c.transform.position += giro * mover;
        }

        // ---------------- Red de túneles ----------------

        static readonly int[] DX = { 1, -1, 0, 0 }, DZ = { 0, 0, 1, -1 };

        void ArmarRed(Mina m, System.Random rng, bool conBallesta)
        {
            int W = MinaAncho, H = MinaFondo;
            // Laberinto (búsqueda en profundidad al azar) + algunas vueltas extra para que haya varios caminos.
            var abierto = new bool[W, H, 4];
            var visto = new bool[W, H];
            var pila = new Stack<int>();
            pila.Push(0); visto[0, 0] = true;
            while (pila.Count > 0)
            {
                int c = pila.Peek(), x = c % W, z = c / W;
                var opciones = new List<int>();
                for (int d = 0; d < 4; d++) { int nx = x + DX[d], nz = z + DZ[d]; if (nx >= 0 && nz >= 0 && nx < W && nz < H && !visto[nx, nz]) opciones.Add(d); }
                if (opciones.Count == 0) { pila.Pop(); continue; }
                int dd = opciones[rng.Next(opciones.Count)];
                int ax = x + DX[dd], az = z + DZ[dd];
                Abrir(abierto, x, z, dd); visto[ax, az] = true; pila.Push(az * W + ax);
            }
            for (int k = 0; k < 52; k++)   // vueltas extra: muchos caminos y ciclos (pedido 2026-11-12: más larga y confusa; ×2 el 2026-11-13)
            {
                int x = rng.Next(W), z = rng.Next(H), d = rng.Next(4);
                int nx = x + DX[d], nz = z + DZ[d];
                if (nx >= 0 && nz >= 0 && nx < W && nz < H) Abrir(abierto, x, z, d);
            }
            // Distancias desde la entrada: el fondo de la mina es el cruce más lejano.
            var dist = new int[W, H]; var padre = new int[W, H];
            for (int x = 0; x < W; x++) for (int z = 0; z < H; z++) { dist[x, z] = -1; padre[x, z] = -1; }
            var cola = new Queue<int>(); cola.Enqueue(0); dist[0, 0] = 0;
            int lejos = 0;
            while (cola.Count > 0)
            {
                int c = cola.Dequeue(), x = c % W, z = c / W;
                if (dist[x, z] > dist[lejos % W, lejos / W]) lejos = c;
                for (int d = 0; d < 4; d++)
                {
                    if (!abierto[x, z, d]) continue;
                    int nx = x + DX[d], nz = z + DZ[d];
                    if (dist[nx, nz] >= 0) continue;
                    dist[nx, nz] = dist[x, z] + 1; padre[nx, nz] = c; cola.Enqueue(nz * W + nx);
                }
            }
            var camino = new HashSet<int>();
            for (int c = lejos; c >= 0; c = padre[c % W, c / W]) camino.Add(c);

            Vector3 origen = OrigenMina(Ancla(), m.N);
            var red = new GameObject("coop_mina_red_" + (m.N + 1));
            red.transform.position = origen;
            m.Red = red;
            var b = new Constructor { Semilla = m.N * 37.3f + 11f }; string primerRiel = null;
            float hN = MinaNodo / 2f, hP = MinaPasillo / 2f;
            for (int x = 0; x < W; x++)
                for (int z = 0; z < H; z++)
                {
                    m.Nodos++;
                    Vector3 c = new Vector3(x * MinaPaso, 0f, z * MinaPaso);
                    b.Piso(c.x - hN, c.z - hN, c.x + hN, c.z + hN, MinaAlto + 0.3f);
                    for (int d = 0; d < 4; d++)
                    {
                        bool ab = abierto[x, z, d] || (x == 0 && z == 0 && d == 3);   // la salida, al sur de la entrada
                        b.ParedNodo(c, d, hN, ab ? hP : 0f, MinaAlto + 0.3f);
                        if (abierto[x, z, d] && (d == 0 || d == 2))
                        {
                            m.Pasillos++;
                            // pasillo hacia +x o +z
                            Vector3 a = c + new Vector3(DX[d] * hN, 0f, DZ[d] * hN);
                            Vector3 e = c + new Vector3(DX[d] * (MinaPaso - hN), 0f, DZ[d] * (MinaPaso - hN));
                            b.Pasillo(a, e, hP, MinaAlto, d == 0);
                            // vigas cada ~2,2 m
                            float largo = MinaPaso - MinaNodo;
                            for (float t = 0.6f; t < largo; t += 2.2f)
                            {
                                Vector3 p = a + (e - a).normalized * t;
                                Vector3 lado = d == 0 ? Vector3.forward : Vector3.right;
                                b.Viga(p - lado * (hP - 0.15f), p - lado * (hP - 0.15f) + Vector3.up * MinaAlto, 0.24f, 1);
                                b.Viga(p + lado * (hP - 0.15f), p + lado * (hP - 0.15f) + Vector3.up * MinaAlto, 0.24f, 1);
                                b.Viga(p - lado * hP + Vector3.up * (MinaAlto - 0.14f), p + lado * hP + Vector3.up * (MinaAlto - 0.14f), 0.26f, 1);
                            }
                            // rieles por el camino de la entrada al fondo
                            int cA = z * W + x, cB = (z + DZ[d]) * W + (x + DX[d]);
                            if (rng.NextDouble() < (camino.Contains(cA) && camino.Contains(cB) ? 0.45 : 0.4)) { b.Rieles(c, c + new Vector3(DX[d] * MinaPaso, 0f, DZ[d] * MinaPaso)); if (primerRiel == null) primerRiel = "nodo " + x + "," + z + " hacia " + (d == 0 ? "+x" : "+z"); }   // rieles también en ramales falsos: no delatan el camino
                        }
                    }
                    // faroles: en la entrada, el fondo y algunos cruces
                    int id = z * W + x;
                    if (id == 0 || id == lejos || rng.NextDouble() < 0.3)
                    {
                        Farol(red.transform, c + new Vector3(hN - 0.35f, MinaAlto - 0.9f, hN - 0.35f));
                        m.Faroles++;
                    }
                    else   // los demás cruces también llevan una luz cálida (sin farol dibujado): pedido 2026-11-13, "ilumina más, en los cruces"
                    {
                        Luz(red.transform, red.transform.TransformPoint(c + new Vector3(hN - 0.9f, 1.9f, hN - 0.9f)), new Color(1f, 0.74f, 0.42f), 9.5f, 1.3f);
                        m.Faroles++;
                    }
                }
            plugin.Log("Mina " + (m.N + 1) + ": primer tramo con rieles en " + primerRiel);
            // marco de la salida y "luz de afuera" al sur de la entrada
            b.Viga(new Vector3(-hP, 0f, -hN), new Vector3(-hP, MinaAlto, -hN), 0.28f, 1);
            b.Viga(new Vector3(hP, 0f, -hN), new Vector3(hP, MinaAlto, -hN), 0.28f, 1);
            b.Viga(new Vector3(-hP - 0.2f, MinaAlto - 0.1f, -hN), new Vector3(hP + 0.2f, MinaAlto - 0.1f, -hN), 0.3f, 1);
            ParajeExterior(b, hP, hN);   // lo que se ve al mirar hacia la salida: un llano con árboles, no un hueco
            b.Caja(new Vector3(-hP, -0.5f, -hN - 0.6f), new Vector3(hP, MinaAlto, -hN), 0, true);   // el piso de esta caja queda en y=0, igual que el suelo de afuera y de adentro
            MaterialesCueva();
            var go = b.Crear("coop_mina_tuneles", new[] { matTunel, matMadera, matCuevaSuelo, matHierro, matNegro, matLuzDia }, true);
            go.transform.SetParent(red.transform, false);
            // "luna" afuera de la salida: dentro de las minas la luz direccional se apaga, así el paraje y la boca siguen viéndose de noche
            Luz(red.transform, origen + new Vector3(0f, 16f, -hN - 26f), new Color(0.5f, 0.56f, 0.7f), 48f, 0.75f);
            SueloMacizo(red.transform, -hN - 1f, -MinaNodo, W * MinaPaso, H * MinaPaso);
            ParedInvisibleSalida(red.transform, hP, hN);
            m.PuertaDentro = origen + new Vector3(0f, 1f, -hN);
            m.Dentro = origen + new Vector3(0f, 0.3f, -hN + 1.6f);
            ExteriorArboles(red.transform, origen, new System.Random(rng.Next()), hN);

            // Rocas sueltas en los callejones sin salida, y un carro y barriles cerca de la entrada.
            DecorarRed(m, abierto, rng, origen, lejos);

            // Al fondo: el minero muerto con su ballesta.
            Vector3 fondo = origen + new Vector3((lejos % W) * MinaPaso, 0f, (lejos / W) * MinaPaso);
            m.PosBallesta = fondo + new Vector3(0.9f, 0.12f, 0.3f);
            GameObject cuerpo = null;
            foreach (var g in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                if (Catalogo.BaseDe(g.name) == "floating_body") { cuerpo = g; break; }
            if (cuerpo != null)
            {
                int f;
                var c = Coop.Piezas.Clonar(cuerpo, fondo, Quaternion.Euler(0f, rng.Next(360), 0f) * cuerpo.transform.rotation, out f);
                c.name = "coop_mina_minero_" + (m.N + 1);
                foreach (var an in c.GetComponentsInChildren<Animator>(true)) an.enabled = false;
                Bounds bc;
                if (CajaDe(new List<GameObject> { c }, out bc))
                {
                    c.transform.position += new Vector3(fondo.x - bc.center.x, 0f, fondo.z - bc.center.z);
                    c.transform.position += new Vector3(0f, fondo.y - MinYReal(c) - 0.16f, 0f);   // medio enterrado en el piso (pedido 2026-11-13)   // el punto más bajo real de la malla ya deformada (la caja del reposo lo dejaba flotando)
                }
                c.transform.SetParent(red.transform, true);
            }
            if (conBallesta)
            {
                var bal = Ballesta.Modelo("coop_mina_ballesta_" + (m.N + 1));
                bal.transform.position = m.PosBallesta;
                bal.transform.rotation = Quaternion.Euler(0f, rng.Next(360), 0f);
                var luz = new GameObject("coop_luz_ballesta"); luz.transform.SetParent(bal.transform, false); luz.transform.localPosition = Vector3.up * 0.8f;
                var l = luz.AddComponent<Light>(); l.type = LightType.Point; l.color = new Color(1f, 0.85f, 0.6f); l.range = 7f; l.intensity = 2.2f; l.shadows = LightShadows.None;
                m.Ballesta = bal;
            }
            lugaresCopias.Add(red);
        }

        // ---------------- Guarida del murciélago (la tercera mina: sin ballesta) ----------------

        /// Un pasillo largo con rieles que desemboca en una sala enorme (64 × 60 m, 14 m de alto): altares con velas rojas alrededor de un
        /// gran altar central, rieles que se hunden en el suelo, vagonetas medio enterradas y columnas de roca. Solo ambiente (el jefe va después).
        void ArmarGuarida(Mina m, System.Random rng)
        {
            m.Guarida = true; m.RadioRed = 220f;   // la cueva mide ~220 × 224 m (2026-11-19; antes 110 × 112 y radio 130)
            Vector3 origen = OrigenMina(Ancla(), m.N);
            var red = new GameObject("coop_mina_red_" + (m.N + 1));
            red.transform.position = origen; m.Red = red;
            float hN = MinaNodo / 2f, hP = MinaPasillo / 2f;
            const float zSala0 = CuevaZ0, zSala1 = CuevaZ0 + 2f * CuevaAZ, xS = CuevaAX;
            var b = new Constructor { Semilla = m.N * 37.3f + 17f };
            // cruce de entrada (la salida al sur) y pasillo largo hacia la sala
            b.Piso(-hN, -hN, hN, hN, MinaAlto + 0.3f);
            b.ParedNodo(Vector3.zero, 0, hN, 0f, MinaAlto + 0.3f); b.ParedNodo(Vector3.zero, 1, hN, 0f, MinaAlto + 0.3f);
            b.ParedNodo(Vector3.zero, 3, hN, hP, MinaAlto + 0.3f); b.ParedNodo(Vector3.zero, 2, hN, hP, MinaAlto + 0.3f);
            b.Pasillo(new Vector3(0f, 0f, hN), new Vector3(0f, 0f, zSala0), hP, MinaAlto, false); m.Pasillos++;
            for (float t = 0.8f; t < zSala0 - hN; t += 2.2f)
            {
                float z = hN + t;
                b.Viga(new Vector3(-hP + 0.15f, 0f, z), new Vector3(-hP + 0.15f, MinaAlto, z), 0.24f, 1);
                b.Viga(new Vector3(hP - 0.15f, 0f, z), new Vector3(hP - 0.15f, MinaAlto, z), 0.24f, 1);
                b.Viga(new Vector3(-hP, MinaAlto - 0.14f, z), new Vector3(hP, MinaAlto - 0.14f, z), 0.26f, 1);
            }
            // marco de la salida y luz de afuera (igual que en las otras minas)
            b.Viga(new Vector3(-hP, 0f, -hN), new Vector3(-hP, MinaAlto, -hN), 0.28f, 1);
            b.Viga(new Vector3(hP, 0f, -hN), new Vector3(hP, MinaAlto, -hN), 0.28f, 1);
            b.Viga(new Vector3(-hP - 0.2f, MinaAlto - 0.1f, -hN), new Vector3(hP + 0.2f, MinaAlto - 0.1f, -hN), 0.3f, 1);
            ParajeExterior(b, hP, hN);   // lo que se ve al mirar hacia la salida: un llano con árboles, no un hueco
            b.Caja(new Vector3(-hP, -0.5f, -hN - 0.6f), new Vector3(hP, MinaAlto, -hN), 0, true);   // el piso de esta caja queda en y=0, igual que el suelo de afuera y de adentro
            // rieles: del pasillo hacia el centro; tramos a la vista, medio hundidos y del todo enterrados
            float zr = zSala0 - 2f; int tramo = 0;
            b.Rieles(new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, zSala0 - 2f));
            while (zr < CuevaZ0 + 66f * CuevaE)
            {
                float largo = 3f + (float)rng.NextDouble() * 4f; float hund = tramo % 3 == 0 ? 0f : tramo % 3 == 1 ? -0.09f : -0.3f;
                float dx = ((float)rng.NextDouble() - 0.5f) * 0.6f * tramo * 0.2f;
                b.Rieles(new Vector3(dx, hund, zr), new Vector3(dx, hund, zr + largo));
                zr += largo + (tramo % 3 == 2 ? 1.5f : 0f); tramo++;
            }
            // un ramal que se desvía hacia un lado y se pierde bajo tierra
            for (int k = 0; k < 3; k++) { float x0 = -5f - k * 8f, z0 = CuevaZ0 + 26f * CuevaE; b.Rieles(new Vector3(x0, k == 0 ? 0f : -0.09f * k, z0 + k * 3f), new Vector3(x0 - 7f, -0.12f * (k + 1), z0 + 3f + k * 3f)); }
            MaterialesCueva();
            var go = b.Crear("coop_mina_tuneles", new[] { matTunel, matMadera, matCuevaSuelo, matHierro, matNegro, matLuzDia }, true);
            go.transform.SetParent(red.transform, false);
            // "luna" afuera de la salida: dentro de las minas la luz direccional se apaga, así el paraje y la boca siguen viéndose de noche
            Luz(red.transform, origen + new Vector3(0f, 16f, -hN - 26f), new Color(0.5f, 0.56f, 0.7f), 48f, 0.75f);
            m.PuertaDentro = origen + new Vector3(0f, 1f, -hN);
            m.Dentro = origen + new Vector3(0f, 0.3f, -hN + 1.6f);
            m.CentroRed = origen + new Vector3(0f, 0f, (zSala0 + zSala1) / 2f);
            SueloMacizo(red.transform, -xS - 6f, -hN - 1f, xS * 2f + 12f, zSala1 + hN + 6f);
            ParedInvisibleSalida(red.transform, hP, hN);
            ExteriorArboles(red.transform, origen, new System.Random(rng.Next()), hN);
            m.Nodos = 1;

            // Altares
            var roca = new Material(matRoca) { name = "coop_altar_roca" }; roca.color = new Color(0.34f, 0.32f, 0.33f);
            var sangre = new Material(Shader.Find("Standard")) { color = new Color(0.35f, 0.02f, 0.02f), name = "coop_altar_sangre" };
            var cera = new Material(Shader.Find("Standard")) { color = new Color(0.78f, 0.72f, 0.62f) };
            var cza = new Vector3(SitiosAltaresCueva[0].x, AlturaSueloCueva(SitiosAltaresCueva[0].x, SitiosAltaresCueva[0].y) - 0.1f, SitiosAltaresCueva[0].y);
            // altares tallados (2026-11-18: antes cajas con un paño flotando)
            var lados = new Vector2[SitiosAltaresCueva.Length - 1]; System.Array.Copy(SitiosAltaresCueva, 1, lados, 0, lados.Length);
            try { ArmarAltaresGuarida(red.transform, cza, lados, rng); } catch (System.Exception e) { plugin.Log("Minas: altares " + e); }
            Luz(red.transform, origen + cza + new Vector3(0f, 4.5f, 0f), new Color(1f, 0.18f, 0.1f), 20f, 2.4f);
            // luz fría y tenue en lo alto para que la sala se intuya
            foreach (var z in new[] { 40f, 74f, 108f }) { var sz = EscalarSitio(new Vector2(0f, z)); Luz(red.transform, origen + new Vector3(0f, 17f * CuevaE, sz.y), new Color(0.5f, 0.45f, 0.4f), 44f * CuevaE, 0.35f); }
            foreach (var x in new[] { -60f, 60f }) Luz(red.transform, origen + new Vector3(x, 15f * CuevaE, CuevaZ0 + CuevaAZ), new Color(0.5f, 0.45f, 0.4f), 40f * CuevaE, 0.3f);
            // faroles en el pasillo
            for (float z = 4f; z < zSala0; z += 6f) { Farol(red.transform, new Vector3(hP - 0.45f, MinaAlto - 0.9f, z)); m.Faroles++; }

            // vagonetas medio enterradas en el suelo, inclinadas, y montones de barriles
            GameObject carro = null, barriles = null, piedra = null;
            foreach (var g in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                string bn = Catalogo.BaseDe(g.name);
                if (carro == null && bn == "cart01_3") carro = g;
                if (barriles == null && bn == "barrel01_pile") barriles = g;
                if (piedra == null && (bn == "Rock5" || bn == "Rock1")) piedra = g;
            }
            int f;
            if (carro != null)
            {
                var sitios = new[] { new Vector3(-4f, 0f, 27f), new Vector3(7f, 0f, 40f), new Vector3(-13f, 0f, 52f), new Vector3(5f, 0f, 62f), new Vector3(-22f, 0f, 70f), new Vector3(17f, 0f, 76f), new Vector3(25f, 0f, 96f), new Vector3(-26f, 0f, 100f) };
                foreach (var sp0 in sitios)
                {
                    var e2 = EscalarSitio(new Vector2(sp0.x, sp0.z)); var sp = new Vector3(e2.x, 0f, e2.y);
                    var c = Coop.Piezas.Clonar(carro, origen + sp + Vector3.up * AlturaSueloCueva(sp.x, sp.z) + Vector3.down * (0.35f + 0.25f * (float)rng.NextDouble()), Quaternion.Euler(8f + 14f * (float)rng.NextDouble(), rng.Next(360), 6f * ((float)rng.NextDouble() - 0.5f)), out f);
                    c.transform.SetParent(red.transform, true);
                }
            }
            if (barriles != null) foreach (var sp in new[] { new Vector3(-20f, 0f, 30f), new Vector3(22f, 0f, 30f), new Vector3(-24f, 0f, 112f), new Vector3(26f, 0f, 110f) })
            { var e2 = EscalarSitio(new Vector2(sp.x, sp.z)); var c = Coop.Piezas.Clonar(barriles, origen + new Vector3(e2.x, 0f, e2.y) + Vector3.up * AlturaSueloCueva(e2.x, e2.y), Quaternion.Euler(0f, rng.Next(360), 0f), out f); if (QuitarSiInvisible(c)) c.transform.SetParent(red.transform, true); }
            // la sala es una cueva natural (MinaCueva.cs): bóveda, piso, columnas, estalactitas, charcos, antorchas, rocas y andamios
            ConstruirCueva(red.transform, origen, new System.Random(rng.Next()), hP, MinaAlto, piedra);
            lugaresCopias.Add(red);
            plugin.Log("Minas: guarida (3.ª mina) armada, cueva " + (2 * xS) + " × " + (zSala1 - zSala0) + " m, " + (lados.Length + 1) + " altares");
        }

        /// Bloque de roca (con colisión) centrado a yCentro metros sobre el suelo.
        /// Losa gruesa e invisible bajo toda la mina: el piso de la malla es una lámina sin grosor y, al activarse la mina,
        /// un jugador que llega de golpe puede atravesarla. La losa es sólida de verdad (y se extiende bajo el paisaje de la salida).
        static void SueloMacizo(Transform red, float x0, float z0, float ancho, float largo)
        {
            var g = new GameObject("coop_mina_losa"); g.transform.SetParent(red, false);
            g.transform.localPosition = new Vector3(x0 + ancho / 2f, -1.1f, z0 + largo / 2f);
            var bc = g.AddComponent<BoxCollider>(); bc.size = new Vector3(ancho, 2.2f, largo);
            var g2 = new GameObject("coop_mina_losa_afuera"); g2.transform.SetParent(red, false);
            g2.transform.localPosition = new Vector3(0f, -1.1f, z0 - 35f);
            var b2 = g2.AddComponent<BoxCollider>(); b2.size = new Vector3(90f, 2.2f, 70f);
        }

        /// Pared invisible en el hueco de la salida: se ve el paisaje pero no se puede cruzar; solo se sale con E.
        static void ParedInvisibleSalida(Transform red, float hP, float hN)
        {
            var g = new GameObject("coop_mina_pared_salida"); g.transform.SetParent(red, false);
            g.transform.localPosition = new Vector3(0f, MinaAlto / 2f, -hN - 0.15f);
            var bc = g.AddComponent<BoxCollider>(); bc.size = new Vector3(hP * 2f + 1.2f, MinaAlto + 1f, 0.6f);
        }

        /// Lo que se ve desde la salida: el suelo sigue hacia afuera, hay niebla de fondo y (ExteriorArboles) árboles muertos.
        static void ParajeExterior(Constructor b, float hP, float hN)
        {
            float z0 = -hN - 0.6f, z1 = -130f, xs = 90f, alto = 34f;   // más terreno a la vista al salir (antes 90 × 70 m)
            // el paraje es un "hueco": así la roca del acantilado se hunde hacia el cerro y no hacia afuera
            b.Hueco(new Vector3(-xs, 0.05f, z1), new Vector3(xs, alto, z0 - 0.05f));
            // ACANTILADO (2026-11-18): la boca sale de una pared de roca, no de una caja suelta en el llano (se veían árboles por los costados)
            float zc = z0 - 0.02f, mx = hP + 0.15f, my = MinaAlto + 0.05f;
            b.Quad(new Vector3(xs, 0f, zc), new Vector3(mx, 0f, zc), new Vector3(mx, alto, zc), new Vector3(xs, alto, zc), 0, true);
            b.Quad(new Vector3(-mx, 0f, zc), new Vector3(-xs, 0f, zc), new Vector3(-xs, alto, zc), new Vector3(-mx, alto, zc), 0, true);
            b.Quad(new Vector3(mx, my, zc), new Vector3(-mx, my, zc), new Vector3(-mx, alto, zc), new Vector3(mx, alto, zc), 0, true);
            b.Quad(new Vector3(-xs, 0f, z1), new Vector3(xs, 0f, z1), new Vector3(xs, 0f, z0), new Vector3(-xs, 0f, z0), 2, true);              // suelo
            b.Quad(new Vector3(-xs, 0f, z1), new Vector3(-xs, alto, z1), new Vector3(xs, alto, z1), new Vector3(xs, 0f, z1), 5, true);          // fondo de niebla
            b.Quad(new Vector3(-xs, 0f, z0), new Vector3(-xs, alto, z0), new Vector3(-xs, alto, z1), new Vector3(-xs, 0f, z1), 5, true);        // lado izquierdo
            b.Quad(new Vector3(xs, 0f, z1), new Vector3(xs, alto, z1), new Vector3(xs, alto, z0), new Vector3(xs, 0f, z0), 5, true);            // lado derecho
            b.Quad(new Vector3(-xs, alto, z0), new Vector3(-xs, alto, z1), new Vector3(xs, alto, z1), new Vector3(xs, alto, z0), 5, true);      // cielo
        }

        /// Árboles muertos del juego (copias) desparramados al sur de la salida, para que se vea un bosque y no un hueco.
        void ExteriorArboles(Transform red, Vector3 origen, System.Random rng, float hN)
        {
            var modelos = new List<GameObject>();
            foreach (var g in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (g.scene.IsValid() || g.transform.parent != null) continue;
                if (g.name == "Dead_Tree_01" || g.name == "DeadTree_09" || g.name == "DeadTree_10" || g.name == "Dead_Tree_10" || g.name == "Tree8") modelos.Add(g);
            }
            if (modelos.Count == 0) { plugin.Log("Minas: no hay árboles para el paisaje de la salida"); return; }
            int puestos = 0;
            for (int i = 0; i < 80; i++)
            {
                float x = ((float)rng.NextDouble() * 2f - 1f) * 82f, z = -hN - 5f - (float)rng.NextDouble() * 115f;
                if (Mathf.Abs(x) < 3.2f && z > -14f) continue;   // el camino de salida queda libre
                var mod = modelos[rng.Next(modelos.Count)];
                var t = (GameObject)Object.Instantiate(mod, origen + new Vector3(x, 0f, z), Quaternion.Euler(0f, rng.Next(360), 0f));
                t.SetActive(true); t.transform.localScale *= 0.9f + 0.9f * (float)rng.NextDouble();
                t.transform.SetParent(red, true); puestos++;
            }
            // luz fría y tenue de afuera
            Luz(red, origen + new Vector3(0f, 9f, -hN - 14f), new Color(0.45f, 0.55f, 0.75f), 60f, 1.1f);
            Luz(red, origen + new Vector3(-40f, 9f, -hN - 60f), new Color(0.45f, 0.55f, 0.75f), 60f, 0.8f);
            Luz(red, origen + new Vector3(40f, 9f, -hN - 60f), new Color(0.45f, 0.55f, 0.75f), 60f, 0.8f);
            plugin.Log("Minas: " + puestos + " árboles en el paisaje de la salida");
        }

        void Altar(Transform padre, Vector3 centro, Vector3 tam, float yCentro, Material m, float yaw = 0f)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = "coop_altar"; g.transform.SetParent(padre, true);
            g.transform.position = new Vector3(centro.x, centro.y + yCentro, centro.z);
            g.transform.rotation = Quaternion.Euler(0f, yaw, 0f); g.transform.localScale = tam;
            g.GetComponent<Renderer>().sharedMaterial = m;
        }

        void Vela(Transform padre, Vector3 pos, Material cera, float alto)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.Destroy(g.GetComponent<Collider>());
            g.name = "coop_vela_altar"; g.transform.SetParent(padre, true);
            g.transform.position = pos + Vector3.up * (alto / 2f); g.transform.localScale = new Vector3(0.14f, alto / 2f, 0.14f);
            g.GetComponent<Renderer>().sharedMaterial = cera;
            Llama.Crear(pos + Vector3.up * (alto + 0.02f), padre, 0.12f, false, false);
            var l = new GameObject("coop_luz_vela"); l.transform.SetParent(padre, true); l.transform.position = pos + Vector3.up * (alto + 0.2f);
            var lz = l.AddComponent<Light>(); lz.type = LightType.Point; lz.color = new Color(1f, 0.45f, 0.18f); lz.range = 4f; lz.intensity = 0.8f; lz.shadows = LightShadows.None;
        }

        void Luz(Transform padre, Vector3 pos, Color c, float rango, float intensidad)
        {
            var g = new GameObject("coop_luz_sala"); g.transform.SetParent(padre, true); g.transform.position = pos;
            var l = g.AddComponent<Light>(); l.type = LightType.Point; l.color = c; l.range = rango; l.intensity = intensidad; l.shadows = LightShadows.None;
        }

        static void Abrir(bool[,,] a, int x, int z, int d)
        {
            a[x, z, d] = true;
            int nx = x + DX[d], nz = z + DZ[d];
            a[nx, nz, d ^ 1] = true;   // 0↔1, 2↔3
        }

        void DecorarRed(Mina m, bool[,,] abierto, System.Random rng, Vector3 origen, int lejos)
        {
            var raiz = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            GameObject piedra = null, carro = null, barriles = null;
            foreach (var g in raiz)
            {
                string bn = Catalogo.BaseDe(g.name);
                if (piedra == null && (bn == "Rock5" || bn == "Rock1")) piedra = g;
                if (carro == null && bn == "cart01_3") carro = g;
                if (barriles == null && bn == "barrel01_pile") barriles = g;
            }
            int f;
            for (int x = 0; x < MinaAncho; x++)
                for (int z = 0; z < MinaFondo; z++)
                {
                    int salidas = 0; int unica = 0;
                    for (int d = 0; d < 4; d++) if (abierto[x, z, d]) { salidas++; unica = d; }
                    if (salidas != 1 || (x == 0 && z == 0) || z * MinaAncho + x == lejos || piedra == null) continue;
                    // callejón: derrumbe contra la pared del fondo
                    Vector3 c = origen + new Vector3(x * MinaPaso, 0f, z * MinaPaso) - new Vector3(DX[unica], 0f, DZ[unica]) * (MinaNodo / 2f - 0.9f);
                    for (int k = 0; k < 3; k++)
                    {
                        var r = Coop.Piezas.Clonar(piedra, c + new Vector3((float)rng.NextDouble() - 0.5f, 0f, (float)rng.NextDouble() - 0.5f), Quaternion.Euler(0f, rng.Next(360), 0f), out f);
                        if (!QuitarSiInvisible(r)) continue;
                        r.transform.localScale *= 0.55f + 0.25f * (float)rng.NextDouble();
                        r.transform.SetParent(m.Red.transform, true);
                    }
                }
            if (carro != null) { var c = Coop.Piezas.Clonar(carro, origen + new Vector3(1.3f, 0f, 1.2f), Quaternion.Euler(0f, 20f, 0f), out f); if (QuitarSiInvisible(c)) c.transform.SetParent(m.Red.transform, true); }
            if (barriles != null) { var c = Coop.Piezas.Clonar(barriles, origen + new Vector3(-1.4f, 0f, 1.3f), Quaternion.identity, out f); if (QuitarSiInvisible(c)) c.transform.SetParent(m.Red.transform, true); }
        }

        /// Una copia sin nada que se dibuje (malla horneada del juego) quedaba como un bloque invisible con colisión
        /// (la pila de barriles en la entrada de la mina, 2026-11-18): se borra. Devuelve true si se ve y queda.
        bool QuitarSiInvisible(GameObject c)
        {
            if (c == null) return false;
            foreach (var r in c.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (!r.enabled) continue;
                var mf = r.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null && mf.sharedMesh.vertexCount > 0 && !r.isPartOfStaticBatch) return true;
            }
            plugin.Log("Minas: copia invisible quitada (" + c.name + ")");
            Object.Destroy(c); return false;
        }

        void Farol(Transform padre, Vector3 pos)
        {
            GameObject lampara = null;
            foreach (var g in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                if (Catalogo.BaseDe(g.name) == "OilLamp01_gua") { lampara = g; break; }
            Vector3 donde = padre.TransformPoint(pos);
            if (lampara != null)
            {
                int f;
                var c = Coop.Piezas.Clonar(lampara, donde, Quaternion.identity, out f);
                foreach (var l0 in c.GetComponentsInChildren<Light>(true)) Object.Destroy(l0);
                c.transform.SetParent(padre, true);
            }
            var luz = new GameObject("coop_luz_farol");
            luz.transform.SetParent(padre, false);
            luz.transform.position = donde + Vector3.up * 0.3f;
            var l = luz.AddComponent<Light>();
            l.type = LightType.Point; l.color = new Color(1f, 0.72f, 0.4f); l.range = 11f; l.intensity = 1.7f; l.shadows = LightShadows.None;
        }

        // ---------------- Entrar, salir y recoger ----------------

        Mina minaCerca; int accionMina;   // 1 entrar, 2 salir, 3 recoger
        static bool Cerca(Vector3 a, Vector3 b, float horizontal, float alto) { float dx = a.x - b.x, dz = a.z - b.z; return dx * dx + dz * dz < horizontal * horizontal && Mathf.Abs(a.y - b.y) < alto; }

        void ActualizarMinas()
        {
            minaCerca = null; accionMina = 0;
            var j = plugin.Jugador;
            if (j == null) return;
            Vector3 p = j.transform.position;
            // Los túneles (y sus faroles) solo existen mientras alguien esté dentro: fuera no gastan nada.
            var comp = plugin.CompaneroActual;
            foreach (var m in minas)
            {
                if (m.Red == null) continue;
                Vector3 c = m.Guarida ? m.CentroRed : m.Red.transform.position + new Vector3(MinaAncho * MinaPaso / 2f, 0f, MinaFondo * MinaPaso / 2f);
                bool dentro = Vector3.Distance(p, c) < m.RadioRed || (comp != null && comp.Visible && Vector3.Distance(comp.PosicionMundo, c) < m.RadioRed);
                if (m.Red.activeSelf != dentro) m.Red.SetActive(dentro);
            }
            foreach (var m in minas)
            {
                // en horizontal y con tolerancia de altura: con el cerro y las rocas el jugador podía quedar un poco más alto o lejos y no entraba (2026-11-17)
                if (Cerca(p, m.PuertaFuera, 3.2f, 3f)) { minaCerca = m; accionMina = 1; break; }
                if (Cerca(p, m.PuertaDentro, 2.8f, 3f)) { minaCerca = m; accionMina = 2; break; }
                if (m.Ballesta != null && Vector3.Distance(p, m.Ballesta.transform.position) < 2.2f) { minaCerca = m; accionMina = 3; break; }
            }
            // dentro de una mina hay un poco de luz de fondo cálida (si no, fuera del alcance de los faroles todo es negro puro)
            bool dentroMina = false;
            foreach (var m in minas)
            {
                if (m.Red == null || m.Guarida) continue;
                Vector3 c2 = m.Red.transform.position + new Vector3(MinaAncho * MinaPaso / 2f, 0f, MinaFondo * MinaPaso / 2f);
                if (Vector3.Distance(p, c2) < m.RadioRed) dentroMina = true;
            }
            if (dentroMina && !ambienteMinaActivo) { ambAntesModo = RenderSettings.ambientMode; ambAntesColor = RenderSettings.ambientLight; ambienteMinaActivo = true; }
            if (dentroMina) { RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight = new Color(0.13f, 0.1f, 0.08f); }
            else if (ambienteMinaActivo) { RenderSettings.ambientMode = ambAntesModo; RenderSettings.ambientLight = ambAntesColor; ambienteMinaActivo = false; }
            AtmosferaCueva(p, dentroMina);
            SelloGuarida(j);
            if (minaCerca == null || !Controles.Interactuar() || !Entrada.Enfocado) return;
            if (accionMina == 1)
            {
                if (minaCerca.Guarida)
                {
                    // (2026-11-19) a la guarida se entra de a dos: así nadie pelea solo con el jefe ni quedan dos jefes distintos
                    if (!JuntosGuarida(j)) { Viaje.Avisar("Tienen que entrar los dos juntos: tu compañero debe estar a tu lado.", 4f); return; }
                    Teletransportar(j, guaridaSellada ? PasoTrasSello(minaCerca) : minaCerca.Dentro);
                    if (plugin.Conectado && !Viaje.SoloQuedoYo()) Mandar(plugin, 4, w => { });
                }
                else Teletransportar(j, minaCerca.Dentro);
            }
            else if (accionMina == 2)
            {
                if (minaCerca.Guarida && guaridaSellada) { Viaje.Avisar("Una roca sella la salida. No hay vuelta atrás.", 4f); return; }
                Teletransportar(j, minaCerca.Fuera);
            }
            else TomarBallesta(minaCerca, true);
        }

        // ---------------- Guarida: se entra de a dos y la salida se sella (2026-11-19) ----------------

        bool guaridaSellada; GameObject rocaSello; float jefeMuertoDesde = -1f;
        const float AperturaSegura = 60f;   // plan B (2026-11-20): si el jefe murió y nadie tomó la llave en 60 s, la salida se abre igual

        Mina Guarida() { foreach (var m in minas) if (m.Guarida && m.Red != null) return m; return null; }

        /// En coop, el compañero tiene que estar a mi lado (8 m) para entrar; solo, o si él está muerto, entro igual.
        bool JuntosGuarida(FPSPlayer j)
        {
            if (!plugin.Conectado || Viaje.SoloQuedoYo()) return true;
            var c = plugin.CompaneroActual;
            if (c == null || !c.Visible || c.Escena != plugin.Escena) return false;
            Vector3 d = c.PosicionMundo - j.transform.position; d.y = 0f;
            return d.magnitude < 8f;
        }

        /// El compañero entró a la guarida (estando yo a su lado): entro con él, un poco a un costado.
        public void EntrarGuaridaRemoto()
        {
            var g = Guarida(); var j = plugin.Jugador;
            if (g == null || j == null || plugin.Escena != EscenaBosque) return;
            var mu = Muerte.Instancia; if (mu != null && mu.Mio != Muerte.Estado.Vivo) return;
            if (Vector3.Distance(j.transform.position, g.CentroRed) < g.RadioRed && !Cerca(j.transform.position, g.PuertaFuera, 12f, 6f)) return;   // ya estoy adentro
            Teletransportar(j, (guaridaSellada ? PasoTrasSello(g) : g.Dentro) + Vector3.right * 1.2f);
            Viaje.Avisar("Entraste a la mina con tu compañero.", 3f);
        }

        /// Ya en la sala grande (pasado el pasillo de entrada).
        static bool AdentroGuarida(Vector3 p, Mina g)
        {
            Vector3 d = p - g.Red.transform.position;
            return d.z > CuevaZ0 + 3f && Vector3.Distance(p, g.CentroRed) < g.RadioRed;
        }

        /// En la zona que tapa la roca: el cruce donde apareces al entrar y todo el pasillo hasta la sala.
        static bool EnPasilloGuarida(Vector3 p, Mina g)
        {
            Vector3 d = p - g.Red.transform.position; float hN = MinaNodo / 2f;
            return d.z > -hN - 0.8f && d.z < CuevaZ0 + 0.8f && Mathf.Abs(d.x) < hN + 1f && d.y > -3f && d.y < MinaAlto + 3f;
        }

        /// Punto de la sala, delante de la roca (por si alguien entra con la salida ya sellada).
        static Vector3 PasoTrasSello(Mina g) { return g.Red.transform.position + new Vector3(0f, 0.3f, CuevaZ0 + 4f); }

        /// (2026-11-19) En cuanto alguien llega a la sala, la roca rellena toda la entrada y el pasillo; el que se quedó ahí muere aplastado.
        /// Se abre al morir el jefe.
        void SelloGuarida(FPSPlayer j)
        {
            var g = Guarida();
            bool muerto = LuaTexto(VarJefeMina) == "muerto" || (jefeCueva != null && jefeCueva.fase == JefeCueva.Fase.Muerta);
            // (2026-11-19) la salida se abre cuando CUALQUIERA recoge la llave que suelta el jefe (TieneLlave se sincroniza)
            if (muerto) { if (jefeMuertoDesde < 0f) jefeMuertoDesde = Time.time; } else jefeMuertoDesde = -1f;
            bool planB = guaridaSellada && muerto && jefeMuertoDesde >= 0f && Time.time - jefeMuertoDesde > AperturaSegura;   // llave inalcanzable o aviso perdido
            if (guaridaSellada && (g == null || TieneLlave || planB))
            {
                guaridaSellada = false;
                if (rocaSello != null)
                {
                    if (g != null) plugin.StartCoroutine(LevantarSello(rocaSello, g.Red.transform.position + new Vector3(0f, MinaAlto / 2f, CuevaZ0)));
                    else Object.Destroy(rocaSello);
                }
                rocaSello = null;
                if (g != null) Viaje.Avisar("Las rocas de la salida se levantan: ya pueden salir.", 6f);
                plugin.Log("Guarida: salida abierta (" + (planB && !TieneLlave ? "plan B: 60 s tras la muerte del jefe" : "alguien tomó la llave") + ")");
                return;
            }
            if (g == null || muerto) return;
            if (guaridaSellada) { if (rocaSello == null) CrearRocaSello(g, false); return; }   // se destruyó con la mina: la vuelvo a poner
            if (!g.Red.activeSelf) return;
            var c = plugin.CompaneroActual;
            bool alguien = AdentroGuarida(j.transform.position, g) || (plugin.Conectado && c != null && c.Visible && c.Escena == plugin.Escena && AdentroGuarida(c.PosicionMundo, g));
            if (!alguien) return;
            Sellar(g);
            if (plugin.Conectado) Mandar(plugin, 5, w => { });
        }

        /// El compañero llegó a la sala y selló la guarida.
        public void SellarRemoto()
        {
            var g = Guarida();
            if (g == null || guaridaSellada || plugin.Escena != EscenaBosque) return;
            Sellar(g);
        }

        void Sellar(Mina g)
        {
            guaridaSellada = true;
            CrearRocaSello(g, true);
            plugin.Log("Guarida: la roca selló la entrada y el pasillo");
            var j = plugin.Jugador;
            if (j != null && EnPasilloGuarida(j.transform.position, g))
            {
                plugin.Log("Guarida: mi jugador quedó en el pasillo: aplastado");
                if (Muerte.Instancia != null) Muerte.Instancia.Aplastado(); else { j.GodMode = false; j.ApplyDamage(j.hitPoints + 5f); }
            }
        }

        /// Rocas que rellenan el cruce de la entrada y todo el pasillo (con colisión), asomando un poco a la sala.
        void CrearRocaSello(Mina g, bool conEstruendo)
        {
            float hN = MinaNodo / 2f;
            Vector3 o = g.Red.transform.position;
            var raiz = new GameObject("coop_guarida_roca_sello");
            raiz.transform.SetParent(g.Red.transform, false); raiz.transform.position = o;
            var rng = new System.Random(4242); int n = 0;
            for (float z = -hN + 1f; z <= CuevaZ0 + 0.4f; z += 2.2f, n++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "coop_guarida_roca_sello_" + n;
                go.GetComponent<MeshFilter>().sharedMesh = JefeCueva.MallaRoca();
                go.transform.SetParent(raiz.transform, false);
                float ancho = (z < hN + 0.5f ? MinaNodo : MinaPasillo) + 1.3f;
                go.transform.position = o + new Vector3(((float)rng.NextDouble() - 0.5f) * 0.3f, MinaAlto / 2f, z);
                go.transform.rotation = Quaternion.Euler(((float)rng.NextDouble() - 0.5f) * 14f, ((float)rng.NextDouble() - 0.5f) * 30f, ((float)rng.NextDouble() - 0.5f) * 14f);
                go.transform.localScale = new Vector3(ancho, MinaAlto + 1.1f, 3.0f);
                if (matTunel != null) go.GetComponent<Renderer>().sharedMaterial = matTunel;
            }
            rocaSello = raiz;
            Vector3 boca = o + new Vector3(0f, MinaAlto / 2f, CuevaZ0);
            if (!conEstruendo) return;
            var c = Sonidos.Clip("jefe_roca_impacto");
            if (c != null)
            {
                var s = new GameObject("coop_guarida_sello_son"); s.transform.position = boca; var a = s.AddComponent<AudioSource>();
                a.clip = c; a.spatialBlend = 0.6f; a.minDistance = 20f; a.maxDistance = 200f; a.volume = Opciones.FactorVolumen; a.Play();
                Object.Destroy(s, c.length + 0.2f);
            }
            Temblor.Iniciar(0.9f, 1.4f);
            PolvoSello(boca);
            Viaje.Avisar("¡Una roca cayó sobre la salida! No hay vuelta atrás.", 5f);
        }

        /// Las rocas del sello suben despacio y se meten en el techo (unas antes que otras), con un temblor ligero y polvo.
        System.Collections.IEnumerator LevantarSello(GameObject raiz, Vector3 boca)
        {
            if (raiz == null) yield break;
            var piezas = new List<Transform>(); var desde = new List<Vector3>();
            foreach (Transform t in raiz.transform) { piezas.Add(t); desde.Add(t.position); }
            const float dur = 3.5f, sube = MinaAlto + 5f;
            Temblor.Iniciar(0.3f, dur);   // ligero
            PolvoSello(boca);
            var c = Sonidos.Clip("jefe_roca_impacto");
            if (c != null)
            {
                var s = new GameObject("coop_guarida_sello_abre"); s.transform.position = boca; var a = s.AddComponent<AudioSource>();
                a.clip = c; a.pitch = 0.6f; a.spatialBlend = 0.6f; a.minDistance = 15f; a.maxDistance = 160f; a.volume = 0.55f * Opciones.FactorVolumen; a.Play();
                Object.Destroy(s, c.length / 0.6f + 0.2f);
            }
            float t0 = Time.time;
            while (raiz != null && Time.time - t0 < dur + 0.6f)
            {
                for (int i = 0; i < piezas.Count; i++)
                {
                    if (piezas[i] == null) continue;
                    float k = Mathf.Clamp01((Time.time - t0 - i * 0.06f) / dur); k = k * k * (3f - 2f * k);
                    piezas[i].position = desde[i] + Vector3.up * sube * k + new Vector3(Mathf.Sin(Time.time * 37f + i) * 0.04f, 0f, 0f) * (1f - k);
                }
                yield return null;
            }
            if (raiz != null) Object.Destroy(raiz);
            plugin.Log("Guarida: rocas de la salida levantadas");
        }

        static void PolvoSello(Vector3 p)
        {
            var g = new GameObject("coop_guarida_sello_polvo"); g.transform.position = p;
            var ps = g.AddComponent<ParticleSystem>(); ps.Stop();
            var mn = ps.main; mn.loop = false; mn.duration = 0.3f; mn.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.4f);
            mn.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 4.5f); mn.startSize = new ParticleSystem.MinMaxCurve(1.2f, 3f);
            mn.startColor = new Color(0.5f, 0.44f, 0.37f, 0.6f); mn.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.rateOverTime = 0f; em.SetBursts(new[] { new ParticleSystem.Burst(0f, 60) });
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 1.4f;
            g.GetComponent<ParticleSystemRenderer>().sharedMaterial = Potenciado.MaterialGas();
            ps.Play(); Object.Destroy(g, 3f);
        }

        // dentro de la cueva de la guarida: niebla oscura (profundidad) y luz de fondo muy baja; se restaura al salir
        bool atmCueva; bool atmFog; float atmRefl; readonly Dictionary<Light, int> atmSoles = new Dictionary<Light, int>(); FogMode atmModo; Color atmColor, atmAmb; float atmDens; UnityEngine.Rendering.AmbientMode atmAmbModo;
        void AtmosferaCueva(Vector3 p, bool enMina)
        {
            bool dentro = enMina, guarida = false;
            foreach (var m in minas)
                if (m.Guarida && m.Red != null && m.Red.activeSelf)
                {
                    // caja local de la cueva y el pasillo (desde 2026-11-19 la cueva es el doble: antes radio 100 m alrededor del centro)
                    Vector3 d = m.Red.transform.InverseTransformPoint(p);
                    if (Mathf.Abs(d.x) < CuevaAX + 40f && d.z > -26f && d.z < CuevaZ0 + 2f * CuevaAZ + 40f && d.y > -40f && d.y < CuevaH + 40f) dentro = guarida = true;
                }
            if (dentro && !atmCueva)
            {
                atmFog = RenderSettings.fog; atmModo = RenderSettings.fogMode; atmColor = RenderSettings.fogColor; atmDens = RenderSettings.fogDensity;
                atmAmbModo = RenderSettings.ambientMode; atmAmb = RenderSettings.ambientLight; atmRefl = RenderSettings.reflectionIntensity; atmCueva = true;
            }
            if (dentro)
            {
                RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared;
                RenderSettings.fogColor = new Color(0.028f, 0.026f, 0.028f); RenderSettings.fogDensity = guarida ? 0.010f : 0.022f;   // guarida más rala desde que es el doble (se intuye el tamaño)
                if (guarida) { RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight = new Color(0.16f, 0.13f, 0.11f); }
                RenderSettings.reflectionIntensity = 0f;
                // la luna y el sol (luces direccionales) no llegan bajo tierra: no iluminan nada mientras estás en la cueva
                foreach (var l in Object.FindObjectsOfType<Light>())
                    if (l.type == LightType.Directional && l.cullingMask != 0) { if (!atmSoles.ContainsKey(l)) atmSoles[l] = l.cullingMask; l.cullingMask = 0; }
            }
            else if (atmCueva)
            {
                RenderSettings.fog = atmFog; RenderSettings.fogMode = atmModo; RenderSettings.fogColor = atmColor; RenderSettings.fogDensity = atmDens;
                RenderSettings.ambientMode = atmAmbModo; RenderSettings.ambientLight = atmAmb; RenderSettings.reflectionIntensity = atmRefl; atmCueva = false;
                foreach (var kv in atmSoles) if (kv.Key != null) kv.Key.cullingMask = kv.Value;
                atmSoles.Clear();
            }
        }

        const float AlturaLlegada = 1f;   // el jugador reaparece 1 m por encima del piso y cae suavemente

        void Teletransportar(FPSPlayer j, Vector3 p)
        {
            bool afuera = p.x > Ancla().x - 100f;
            foreach (var m in minas) if (m.Red != null && Vector3.Distance(m.Dentro, p) < 1f && !m.Red.activeSelf) m.Red.SetActive(true);   // el piso tiene que estar antes de llegar
            if (afuera) p = PuntoLibreAfuera(p, j);
            p += Vector3.up * AlturaLlegada;
            var rb = j.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = true;   // quieto en el aire hasta que haya piso sólido
            j.transform.position = p;
            plugin.Log("Minas: teletransporte a " + (p - Ancla()).ToString("F1") + (afuera ? " (afuera)" : " (adentro)") + ", 1 m sobre el piso; quieto hasta que el piso sea sólido");
            if (rb != null) { if (esperaPiso != null) plugin.StopCoroutine(esperaPiso); esperaPiso = plugin.StartCoroutine(EsperarPiso(rb, j, p)); }
        }

        Coroutine esperaPiso;

        /// El jugador se queda quieto en el aire hasta que debajo haya piso sólido (colisión cargada y un punto de apoyo medido) durante
        /// varios cuadros seguidos; entonces se suelta y cae 1 m. Si el piso tarda, espera (reactivando la mina); nunca se cae al vacío.
        System.Collections.IEnumerator EsperarPiso(Rigidbody rb, FPSPlayer j, Vector3 p)
        {
            float desde = Time.realtimeSinceStartup; int buenos = 0; bool avisado = false;
            while (rb != null && j != null)
            {
                bool piso = false;
                foreach (var h in Physics.RaycastAll(j.transform.position + Vector3.up * 0.3f, Vector3.down, 4f, ~0, QueryTriggerInteraction.Ignore))
                    if (h.collider.GetComponentInParent<FPSPlayer>() == null && !h.collider.isTrigger) { piso = true; break; }
                buenos = piso ? buenos + 1 : 0;
                if (buenos >= 4 && Time.realtimeSinceStartup - desde > 0.25f) break;
                if (!avisado && Time.realtimeSinceStartup - desde > 3f)
                {
                    avisado = true;
                    plugin.Log("Minas: el piso tarda en cargar; el jugador sigue quieto en el aire");
                    foreach (var m in minas) if (m.Red != null && !m.Red.activeSelf && Vector3.Distance(m.Dentro, p) < 20f) m.Red.SetActive(true);
                }
                yield return null;
            }
            if (rb == null) yield break;
            rb.isKinematic = false; rb.velocity = Vector3.zero; rb.angularVelocity = Vector3.zero; rb.WakeUp();
            plugin.Log("Minas: piso sólido tras " + (Time.realtimeSinceStartup - desde).ToString("0.00") + " s; el jugador cae y queda libre");
            esperaPiso = null;
        }

        /// Un punto de pie sobre el suelo real (el terreno, no el cerro ni la mina) donde cabe el cuerpo sin tocar nada.
        Vector3 PuntoLibreAfuera(Vector3 p, FPSPlayer j)
        {
            Vector3 mejor = p;
            for (int anillo = 0; anillo < 8; anillo++)
                for (int k = 0; k < (anillo == 0 ? 1 : 8); k++)
                {
                    float ang = k * Mathf.PI / 4f, r = anillo * 1.5f;
                    Vector3 c = p + new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r);
                    float suelo = float.NaN;
                    foreach (var t in Terrain.activeTerrains)
                    {
                        var o = t.transform.position; var sz = t.terrainData.size;
                        if (c.x >= o.x && c.x <= o.x + sz.x && c.z >= o.z && c.z <= o.z + sz.z) { suelo = t.SampleHeight(c) + o.y; break; }
                    }
                    foreach (var h in Physics.RaycastAll(c + Vector3.up * 30f, Vector3.down, 60f, ~0, QueryTriggerInteraction.Ignore))
                    {
                        if (h.collider.GetComponentInParent<FPSPlayer>() != null || h.collider is TerrainCollider) continue;
                        if (h.point.y > (float.IsNaN(suelo) ? -1e9f : suelo) && h.point.y < (float.IsNaN(suelo) ? c.y : suelo) + 1.2f) suelo = h.point.y;   // suelo sólido apenas por encima del terreno (rampas, tablas)
                    }
                    if (float.IsNaN(suelo)) suelo = p.y;
                    Vector3 pie = new Vector3(c.x, suelo + 0.15f, c.z);
                    // cabe el cuerpo (cápsula de 0,9 m de radio por 1,9 m de alto) sin tocar colisiones
                    if (!Physics.CheckCapsule(pie + Vector3.up * 0.5f, pie + Vector3.up * 1.5f, 0.45f, ~0, QueryTriggerInteraction.Ignore)) return pie + Vector3.up * 0.9f;   // el jugador se ubica por el centro
                    mejor = pie + Vector3.up * 0.9f;
                }
            plugin.Log("Minas: no hallé un punto libre afuera; uso el más cercano");
            return mejor + Vector3.up * 0.6f;
        }

        System.Collections.IEnumerator AvisoBallesta(int flechas)
        {
            yield return new WaitForSeconds(6.4f);   // el título de la tormenta dura 6 s
            Viaje.Avisar("Conseguiste la ballesta (tecla 6) y " + flechas + " flechas.", 4f);
        }

        void TomarBallesta(Mina m, bool mia)
        {
            if (m.Ballesta == null) return;
            Object.Destroy(m.Ballesta); m.Ballesta = null;
            AnotarLua(VarBallestasTomadas, "mina" + m.N);
            if (mia)
            {
                Ballesta.Dar(true);
                int f = plugin.municion.SumarFlechas(Ballesta.FlechasAlEncontrarla);
                plugin.StartCoroutine(AvisoBallesta(f));   // el aviso sale cuando se va el título de la tormenta
                int n = m.N;
                for (int i = 0; i < 3; i++) plugin.EnviarBallesta(Red.Armar(w => w.Write(n)));
                plugin.Log("Minas: tomé la ballesta de la mina " + (m.N + 1));
                if (Eventos.Instancia != null) Eventos.Instancia.TormentaPorBallesta();   // la ballesta de la mina despierta una tormenta (no la luna de sangre)
            }
            else plugin.Log("Minas: " + plugin.NombreCompanero + " tomó la ballesta de la mina " + (m.N + 1));
        }

        public void RecibirBallesta(BinaryReader r)
        {
            int n = r.ReadInt32();
            foreach (var m in minas) if (m.N == n) { TomarBallesta(m, false); return; }
            AnotarLua(VarBallestasTomadas, "mina" + n);
        }

        static HashSet<string> ListaLua(string var)
        {
            var h = new HashSet<string>(); string s = "";
            try { s = DialogueLua.GetVariable(var).AsString; } catch { }
            if (!string.IsNullOrEmpty(s) && s != "nil") foreach (var x in s.Split('|')) if (x.Length > 0) h.Add(x);
            return h;
        }

        static void AnotarLua(string var, string id)
        {
            var h = ListaLua(var); h.Add(id);
            DialogueLua.SetVariable(var, string.Join("|", new List<string>(h).ToArray()));
        }

        public void DibujarMina()
        {
            if (minaCerca == null || plugin.Jugador == null) return;
            if (estiloCofre == null)
            {
                estiloCofre = new GUIStyle(GUI.skin.label) { fontSize = Opciones.T(20), alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
                estiloCofre.normal.textColor = new Color(1f, 0.9f, 0.6f);
            }
            string t = accionMina == 1 ? (minaCerca.Guarida && plugin.Conectado && !Viaje.SoloQuedoYo() ? "E: entrar a la mina abandonada (los dos juntos)" : "E: entrar a la mina abandonada")
                     : accionMina == 2 ? (minaCerca.Guarida && guaridaSellada ? "Una roca sella la salida" : "E: salir de la mina") : "E: recoger la ballesta";
            GUI.Label(new Rect(0, Screen.height * 0.6f, Screen.width, 30f), t, estiloCofre);
        }

        public string DescribirMinas()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var m in minas) sb.Append("mina ").Append(m.N + 1).Append(" boca ").Append((m.PuertaFuera - Ancla()).ToString("F0")).Append(" dentro ").Append((m.Dentro - Ancla()).ToString("F0"))
                .Append(" ballesta ").Append((m.PosBallesta - Ancla()).ToString("F0")).Append(m.Ballesta != null ? " (está)" : " (tomada)").Append("; ");
            return sb.ToString();
        }

        /// Para las pruebas: 0 boca, 1 dentro, 2 junto a la ballesta.
        public Vector3? PosMina(int n, int donde)
        {
            foreach (var m in minas) if (m.N == n) return (donde == 0 ? m.Fuera : donde == 1 ? m.Dentro : m.PosBallesta + new Vector3(-1.2f, 0.3f, 0f)) - Ancla();
            return null;
        }
        public void OrdenMinaTeleport(FPSPlayer j, int n, bool entrar) { foreach (var m in minas) if (m.N == n) Teletransportar(j, entrar ? m.Dentro : m.Fuera); }
        public void OrdenTomarBallesta(int n) { foreach (var m in minas) if (m.N == n) TomarBallesta(m, true); }

        // ---------------- Constructor de mallas ----------------

        /// Junta quads en una malla con varias submallas (0 roca, 1 madera, 2 suelo, 3 hierro, 4 negro, 5 luz de afuera).
        class Constructor
        {
            List<Vector3> v = new List<Vector3>();
            List<Vector2> uv = new List<Vector2>();
            List<Vector4> tg = new List<Vector4>();
            List<int>[] tri = NuevosTri();
            static List<int>[] NuevosTri() { return new[] { new List<int>(), new List<int>(), new List<int>(), new List<int>(), new List<int>(), new List<int>() }; }
            class Parte { public List<Vector3> V; public List<Vector2> UV; public List<Vector4> T; public List<int>[] Tri; }
            readonly List<Parte> partes = new List<Parte>();
            /// Las mallas de Unity 5.6 admiten 65 535 vértices: cuando el bloque actual se llena, se guarda y se empieza otro.
            void Volcar() { partes.Add(new Parte { V = v, UV = uv, T = tg, Tri = tri }); v = new List<Vector3>(); uv = new List<Vector2>(); tg = new List<Vector4>(); tri = NuevosTri(); }
            const float Teja = 4f;   // metros por repetición de textura

            // ---- Roca rugosa (2026-11-18): las caras de roca y piso se subdividen y se hunden con ruido hacia dentro de la roca ----
            /// false = caras planas como antes.
            public bool Rugoso = true;
            /// desplaza el ruido entre minas (todas usan coordenadas locales parecidas)
            public float Semilla;
            /// huecos interiores (pasillos, cruces, salas): sirven para saber hacia qué lado de una pared está el túnel
            readonly List<Bounds> huecos = new List<Bounds>();
            public void Hueco(Vector3 mn, Vector3 mx) { var bb = new Bounds(); bb.SetMinMax(Vector3.Min(mn, mx), Vector3.Max(mn, mx)); huecos.Add(bb); }
            bool EnHueco(Vector3 p) { foreach (var h in huecos) if (h.Contains(p)) return true; return false; }

            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, int mat, bool dobles)
            {
                if (Rugoso && (mat == 0 || mat == 2) && (b - a).magnitude > 0.9f && (d - a).magnitude > 0.9f) { QuadRugoso(a, b, c, d, mat, dobles); return; }
                QuadPlano(a, b, c, d, mat, dobles);
            }

            void QuadRugoso(Vector3 a, Vector3 b, Vector3 c, Vector3 d, int mat, bool dobles)
            {
                Vector3 n = Vector3.Cross(b - a, d - a).normalized;
                float L1 = (b - a).magnitude, L2 = (d - a).magnitude;
                float celda = Mathf.Max(0.75f, Mathf.Sqrt(L1 * L2 / 2500f));   // 0,75 m (antes 0,55): ~45 % menos vértices, carga más rápida   // caras enormes (acantilado, llano): celdas más grandes
                int nx = Mathf.Max(1, Mathf.CeilToInt(L1 / celda)), ny = Mathf.Max(1, Mathf.CeilToInt(L2 / celda));
                if (v.Count + (nx + 1) * (ny + 1) * 2 > 60000) Volcar();
                Vector3 tn, bn;
                if (Mathf.Abs(n.y) > 0.7f) { tn = Vector3.right; bn = Vector3.forward; }
                else if (Mathf.Abs(n.x) > 0.7f) { tn = Vector3.forward; bn = Vector3.up; }
                else { tn = Vector3.right; bn = Vector3.up; }
                float wf = Vector3.Dot(Vector3.Cross(n, tn), bn) >= 0f ? 1f : -1f;
                // ¿hacia qué lado está el túnel? la roca se hunde hacia el otro (nunca tapa vigas ni rieles)
                Vector3 centro = (a + b + c + d) * 0.25f;
                Vector3 haciaRoca = EnHueco(centro + n * 0.6f) ? -n : (EnHueco(centro - n * 0.6f) ? n : Vector3.zero);
                bool piso = mat == 2;
                float amp = (piso ? 0.06f : (Mathf.Abs(n.y) > 0.7f ? 0.5f : 0.62f)) * Mathf.Clamp(celda / 0.55f, 1f, 4f);
                int i0 = v.Count;
                for (int j = 0; j <= ny; j++)
                    for (int i = 0; i <= nx; i++)
                    {
                        float s1 = i / (float)nx, t1 = j / (float)ny;
                        Vector3 p = Vector3.Lerp(Vector3.Lerp(a, b, s1), Vector3.Lerp(d, c, s1), t1);
                        // bordes fijos (las piezas vecinas empalman sin grietas)
                        float borde = Mathf.Min(Mathf.Min(s1 * L1, (1f - s1) * L1), Mathf.Min(t1 * L2, (1f - t1) * L2));
                        float w = Mathf.SmoothStep(0f, 1f, borde / (0.6f * Mathf.Max(1f, celda / 0.55f)));
                        Vector3 q = p / (1.7f * Mathf.Max(1f, celda / 0.55f)) + new Vector3(Semilla, Semilla * 0.37f, Semilla * 0.71f);
                        float f = Fractal2(q, 11f) * 0.7f + Fractal2(p / 0.45f + Vector3.one * Semilla, 23f) * 0.3f;
                        // crestas: roca picada (aristas marcadas), no ondulada
                        float cresta = 1f - Mathf.Abs(Fractal2(p / 0.9f + Vector3.one * (Semilla + 3f), 31f)) * 1.6f;
                        if (piso) p += n * amp * f * w;
                        else if (haciaRoca != Vector3.zero) p += haciaRoca * amp * Mathf.Clamp01(0.4f + 0.45f * f + 0.35f * cresta) * w;
                        v.Add(p); tg.Add(new Vector4(tn.x, tn.y, tn.z, wf));
                        if (Mathf.Abs(n.y) > 0.7f) uv.Add(new Vector2(p.x / Teja, p.z / Teja));
                        else if (Mathf.Abs(n.x) > 0.7f) uv.Add(new Vector2(p.z / Teja, p.y / Teja));
                        else uv.Add(new Vector2(p.x / Teja, p.y / Teja));
                    }
                var t = tri[mat]; int fila = nx + 1;
                for (int j = 0; j < ny; j++)
                    for (int i = 0; i < nx; i++)
                    {
                        int q0 = i0 + j * fila + i, q1 = q0 + 1, q3 = q0 + fila, q2 = q3 + 1;   // a, b, c, d del cuadrito
                        t.Add(q0); t.Add(q1); t.Add(q2); t.Add(q0); t.Add(q2); t.Add(q3);
                    }
                if (dobles)
                {
                    int j0 = v.Count, cuantos = (nx + 1) * (ny + 1);
                    for (int k = 0; k < cuantos; k++) { v.Add(v[i0 + k]); uv.Add(uv[i0 + k]); tg.Add(new Vector4(tn.x, tn.y, tn.z, -wf)); }
                    for (int j = 0; j < ny; j++)
                        for (int i = 0; i < nx; i++)
                        {
                            int q0 = j0 + j * fila + i, q1 = q0 + 1, q3 = q0 + fila, q2 = q3 + 1;
                            t.Add(q0); t.Add(q2); t.Add(q1); t.Add(q0); t.Add(q3); t.Add(q2);
                        }
                }
            }

            /// Ruido de 2 octavas (la mitad de trabajo que Fractal): para la roca de los túneles, que tiene cientos de miles de vértices.
            static float Fractal2(Vector3 p, float s)
            {
                float a = Ruido3(p.x + s, p.y + s * 0.7f, p.z + s * 1.3f) - 0.5f, b = Ruido3(p.x * 2.07f + s, p.y * 2.07f + s * 0.7f, p.z * 2.07f + s * 1.3f) - 0.5f;
                return (a + b * 0.5f) / 1.5f * 3.2f;
            }

            void QuadPlano(Vector3 a, Vector3 b, Vector3 c, Vector3 d, int mat, bool dobles)
            {
                // UV según el plano de la cara
                Vector3 n = Vector3.Cross(b - a, d - a).normalized;
                if (v.Count > 56000) Volcar();
                // tangente según el mapeo plano de la cara (la u va por x, o por z en las caras de lado x): hace falta para el mapa de relieve
                Vector3 tn, bn;
                if (Mathf.Abs(n.y) > 0.7f) { tn = Vector3.right; bn = Vector3.forward; }
                else if (Mathf.Abs(n.x) > 0.7f) { tn = Vector3.forward; bn = Vector3.up; }
                else { tn = Vector3.right; bn = Vector3.up; }
                float wf = Vector3.Dot(Vector3.Cross(n, tn), bn) >= 0f ? 1f : -1f;
                int i = v.Count;
                foreach (var p in new[] { a, b, c, d })
                {
                    v.Add(p); tg.Add(new Vector4(tn.x, tn.y, tn.z, wf));
                    if (Mathf.Abs(n.y) > 0.7f) uv.Add(new Vector2(p.x / Teja, p.z / Teja));
                    else if (Mathf.Abs(n.x) > 0.7f) uv.Add(new Vector2(p.z / Teja, p.y / Teja));
                    else uv.Add(new Vector2(p.x / Teja, p.y / Teja));
                }
                var t = tri[mat];
                t.Add(i); t.Add(i + 1); t.Add(i + 2); t.Add(i); t.Add(i + 2); t.Add(i + 3);
                if (dobles)
                {
                    // cara de atrás con vértices propios (si se comparten, las normales se anulan)
                    int j = v.Count;
                    for (int k = 0; k < 4; k++) { v.Add(v[i + k]); uv.Add(uv[i + k]); tg.Add(new Vector4(tn.x, tn.y, tn.z, -wf)); }
                    t.Add(j); t.Add(j + 2); t.Add(j + 1); t.Add(j); t.Add(j + 3); t.Add(j + 2);
                }
            }

            /// Piso (suelo) y techo (roca) de un rectángulo.
            public void Piso(float x0, float z0, float x1, float z1, float alto)
            {
                Hueco(new Vector3(x0, 0.05f, z0), new Vector3(x1, alto - 0.05f, z1));
                Quad(new Vector3(x0, 0f, z0), new Vector3(x0, 0f, z1), new Vector3(x1, 0f, z1), new Vector3(x1, 0f, z0), 2, true);
                Quad(new Vector3(x0, alto, z0), new Vector3(x1, alto, z0), new Vector3(x1, alto, z1), new Vector3(x0, alto, z1), 0, true);
            }

            /// Pared vertical de roca entre dos puntos del suelo, con una leve irregularidad hacia adentro.
            public void Pared(Vector3 a, Vector3 b, float alto)
            {
                if ((b - a).sqrMagnitude < 0.0004f) return;
                Quad(a, b, b + Vector3.up * alto, a + Vector3.up * alto, 0, true);
            }

            /// Lado d de un cruce: entero, o con un hueco del ancho del pasillo.
            public void ParedNodo(Vector3 c, int d, float h, float hueco, float alto)
            {
                Vector3 normal = new Vector3(DX[d], 0f, DZ[d]);
                Vector3 lado = new Vector3(-normal.z, 0f, normal.x);
                Vector3 centro = c + normal * h;
                if (hueco <= 0f) { Pared(centro - lado * h, centro + lado * h, alto); return; }
                Pared(centro - lado * h, centro - lado * hueco, alto);
                Pared(centro + lado * hueco, centro + lado * h, alto);
                // dintel sobre el hueco (el pasillo es más bajo que el cruce)
                Quad(centro - lado * hueco + Vector3.up * (alto - 0.3f), centro + lado * hueco + Vector3.up * (alto - 0.3f), centro + lado * hueco + Vector3.up * alto, centro - lado * hueco + Vector3.up * alto, 0, true);
            }

            /// Pasillo recto entre a y b (en x si enX, si no en z): piso, techo y paredes.
            public void Pasillo(Vector3 a, Vector3 b, float h, float alto, bool enX)
            {
                Vector3 lado = enX ? Vector3.forward : Vector3.right;
                Vector3 a0 = a - lado * h, a1 = a + lado * h, b0 = b - lado * h, b1 = b + lado * h;
                Hueco(a0 + Vector3.up * 0.05f, b1 + Vector3.up * (alto - 0.05f));
                Quad(a0, b0, b1, a1, 2, true);
                Quad(a0 + Vector3.up * alto, a1 + Vector3.up * alto, b1 + Vector3.up * alto, b0 + Vector3.up * alto, 0, true);
                Pared(a0, b0, alto); Pared(a1, b1, alto);
            }

            /// Viga cuadrada entre dos puntos (lado = grosor).
            public void Viga(Vector3 a, Vector3 b, float grosor, int mat)
            {
                if (Rugoso && mat == 1 && grosor >= 0.18f) { Tronco(a, b, grosor * 0.56f); return; }
                Vector3 d = (b - a).normalized;
                Vector3 l = Vector3.Cross(Mathf.Abs(d.y) > 0.9f ? Vector3.right : Vector3.up, d).normalized * grosor / 2f;
                Vector3 u = Vector3.Cross(d, l).normalized * grosor / 2f;
                Vector3 a0 = a - l - u, a1 = a + l - u, a2 = a + l + u, a3 = a - l + u;
                Vector3 b0 = b - l - u, b1 = b + l - u, b2 = b + l + u, b3 = b - l + u;
                Quad(a0, b0, b1, a1, mat, false); Quad(a1, b1, b2, a2, mat, false);
                Quad(a2, b2, b3, a3, mat, false); Quad(a3, b3, b0, a0, mat, false);
                Quad(a0, a1, a2, a3, mat, false); Quad(b3, b2, b1, b0, mat, false);
            }

            /// Tronco redondo de 8 caras, algo irregular (postes y cabezales de entibado).
            public void Tronco(Vector3 a, Vector3 b, float r)
            {
                Vector3 d = (b - a).normalized;
                Vector3 l = Vector3.Cross(Mathf.Abs(d.y) > 0.9f ? Vector3.right : Vector3.up, d).normalized;
                Vector3 u = Vector3.Cross(d, l).normalized;
                var A = new Vector3[8]; var B = new Vector3[8];
                float sem = (a.x * 0.37f + a.z * 0.61f + a.y) * 3.1f;
                for (int k = 0; k < 8; k++)
                {
                    float th = (225f + k * 45f) * Mathf.Deg2Rad;
                    float ra = r * (0.9f + 0.18f * Mathf.PerlinNoise(sem, k * 0.7f)), rb = r * (0.9f + 0.18f * Mathf.PerlinNoise(sem + 5f, k * 0.7f));
                    Vector3 dir = l * Mathf.Cos(th) + u * Mathf.Sin(th);
                    A[k] = a + dir * ra; B[k] = b + dir * rb;
                }
                for (int k = 0; k < 8; k++) { int k1 = (k + 1) % 8; QuadPlano(A[k], B[k], B[k1], A[k1], 1, false); }
                QuadPlano(A[0], A[1], A[2], A[3], 1, false); QuadPlano(A[0], A[3], A[4], A[7], 1, false); QuadPlano(A[4], A[5], A[6], A[7], 1, false);
                QuadPlano(B[3], B[2], B[1], B[0], 1, false); QuadPlano(B[7], B[4], B[3], B[0], 1, false); QuadPlano(B[7], B[6], B[5], B[4], 1, false);
            }

            /// Caja de roca abierta por delante (-z): paredes, techo y piso.
            public void Caja(Vector3 mn, Vector3 mx, int mat, bool dobles)
            {
                Hueco(mn + new Vector3(0.05f, 0.55f, 0.05f), mx - Vector3.one * 0.05f);
                Quad(new Vector3(mn.x, mn.y, mn.z), new Vector3(mn.x, mn.y, mx.z), new Vector3(mn.x, mx.y, mx.z), new Vector3(mn.x, mx.y, mn.z), mat, dobles);
                Quad(new Vector3(mx.x, mn.y, mx.z), new Vector3(mx.x, mn.y, mn.z), new Vector3(mx.x, mx.y, mn.z), new Vector3(mx.x, mx.y, mx.z), mat, dobles);
                Quad(new Vector3(mn.x, mx.y, mn.z), new Vector3(mn.x, mx.y, mx.z), new Vector3(mx.x, mx.y, mx.z), new Vector3(mx.x, mx.y, mn.z), mat, dobles);
                Quad(new Vector3(mn.x, mn.y + 0.5f, mn.z), new Vector3(mx.x, mn.y + 0.5f, mn.z), new Vector3(mx.x, mn.y + 0.5f, mx.z), new Vector3(mn.x, mn.y + 0.5f, mx.z), 2, dobles);
            }

            /// Rieles (dos barras de hierro sobre durmientes de madera) entre dos centros de cruce.
            public void Rieles(Vector3 a, Vector3 b)
            {
                Vector3 d = (b - a).normalized; Vector3 l = new Vector3(-d.z, 0f, d.x);
                float largo = Vector3.Distance(a, b);
                // durmientes de madera (techo a 0,13 m, sin tocar el piso) y dos rieles de hierro apoyados encima
                for (float t = 0.3f; t < largo; t += 0.75f) Viga(a + d * t - l * 0.8f + Vector3.up * 0.07f, a + d * t + l * 0.8f + Vector3.up * 0.07f, 0.12f, 1);
                Viga(a - l * 0.5f + Vector3.up * 0.17f, b - l * 0.5f + Vector3.up * 0.17f, 0.08f, 3);
                Viga(a + l * 0.5f + Vector3.up * 0.17f, b + l * 0.5f + Vector3.up * 0.17f, 0.08f, 3);
            }

            public GameObject Crear(string nombre, Material[] mats, bool colision)
            {
                partes.Add(new Parte { V = v, UV = uv, T = tg, Tri = tri });
                var raiz = new GameObject(nombre); int n = 0, total = 0;
                foreach (var pt in partes)
                {
                    if (pt.V.Count == 0) continue;
                    var go = partes.Count == 1 ? raiz : new GameObject(nombre + "_" + (n++));
                    if (go != raiz) go.transform.SetParent(raiz.transform, false);
                    var m = new Mesh { name = go.name };
                    m.SetVertices(pt.V); m.SetUVs(0, pt.UV); m.SetTangents(pt.T);
                    var usados = new List<Material>(); var subs = new List<List<int>>();
                    for (int k = 0; k < pt.Tri.Length && k < mats.Length; k++) if (pt.Tri[k].Count > 0) { usados.Add(mats[k]); subs.Add(pt.Tri[k]); }
                    m.subMeshCount = subs.Count;
                    for (int k = 0; k < subs.Count; k++) m.SetTriangles(subs[k], k);
                    m.RecalculateNormals(); m.RecalculateBounds();
                    go.AddComponent<MeshFilter>().sharedMesh = m;
                    go.AddComponent<MeshRenderer>().sharedMaterials = usados.ToArray();
                    if (colision) go.AddComponent<MeshCollider>().sharedMesh = m;
                    total += pt.V.Count;
                }
                Debug.Log("[WitchHuntCoop] malla de la mina '" + nombre + "': " + total + " vértices en " + partes.Count + " bloque(s)");
                return raiz;
            }
        }
    }
}

// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace WitchHuntCoop
{
    /// Puestos nuevos en cada cacería: una estructura reconstruida (cueva, rocas, cercas...)
    /// con recompensas alrededor y enemigos que aparecen al acercarse. Las estructuras originales
    /// del bosque están horneadas y no se pueden mover; estas se arman con las mallas originales
    /// que quedan en memoria (ver Coop.Piezas).
    public partial class Mundo
    {
        public const int CantidadPuestos = 10;

        /// Estructuras que se reconstruyen completas (medido con la orden "clonables").
        static readonly string[] CatalogoEstructuras = {
            "the_cave", "the_cave (1)", "Cave_Parrent", "Cave_Parrent (1)",
            "rock_pile (1)", "rock_pile (2)", "rock_pile (3)", "rock_pile (6)",
            "lots_of_rocks (1)", "Many_Fences (1)", "StickMan_parrent", "Sack (10)",
        };
        /// Recompensas que se clonan junto a cada puesto.
        static readonly string[] Recompensas = {
            "Gold_Nugget", "Gold_Nugget", "Gold_Nugget", "Sack_of_coins", "Sack_of_coins", "Sack_of_coins", "Goldidol_pickable", "Goldenplate_pickable", "Bronzeplate_pickable",
            "Ammo_Silverbullets", "Ammo_Silverbullets", "Lockpicks_pack", "silverbars_pickable",
        };
        /// Enemigos que genera el anfitrión en los puestos.
        internal static readonly string[] Guardianes = { "UndeadDog", "UndeadDog", "BigSpider_ai", "Deadblind", "Elongata_ai", "Undead_naked", "Snake" };

        class Puesto { public Vector3 Pos; public string Guardian; public int CuantosGuardianes; public bool Generado; public GameObject Estructura; }
        readonly List<Puesto> puestos = new List<Puesto>();
        readonly List<GameObject> clones = new List<GameObject>();

        /// Piezas con las que se arman los puestos (nombre base; solo las que se rearman bien).
        static readonly string[] PiezasPuesto = {
            "rock_pile", "lots_of_rocks", "Many_Fences", "StickMan_parrent", "Stickman2", "Sack", "barrel01_pile", "Barrel_low",
            "wood_logpile01", "bones_parrent", "skull_father", "cross_small", "wooden_cross", "Torch_nofire", "Holder", "SK_BackPack",
            "Horse_Skeleton_Gua", "fi_vil_GaiaWagon05", "Basket_gua", "bucket01", "Sawbuck_gua", "Rock5", "Rock9", "Rock13",
        };
        static readonly string[] RecompensaMenor = { "Bronzeplate_pickable", "Lockpicks_pack", "Bronzestatuette_pickable" };

        void CrearPuestos(int semilla)
        {
            foreach (var g in clones) if (g != null) Object.Destroy(g);
            clones.Clear(); puestos.Clear();

            // Piezas disponibles, en orden fijo (igual en ambos juegos).
            var porTipo = new SortedDictionary<string, GameObject>(System.StringComparer.Ordinal);
            foreach (var g in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (g == null || !g.activeInHierarchy) continue;
                string b = Catalogo.BaseDe(g.name);
                if (System.Array.IndexOf(PiezasPuesto, b) < 0 || porTipo.ContainsKey(b)) continue;
                int h, e; Coop.Piezas.Calidad(g, out h, out e);
                if (e == h) porTipo[b] = g;
            }
            var piezas = new List<GameObject>(porTipo.Values);
            GameObject fogata = null;
            foreach (var g in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                if (g != null && Catalogo.BaseDe(g.name) == "Campfire_gua") { int h, e; Coop.Piezas.Calidad(g, out h, out e); if (e == h) { fogata = g; break; } }
            if (piezas.Count == 0) { plugin.Log("Mundo: no hay piezas para los puestos"); return; }

            var rng = new System.Random(semilla ^ Hash("coop_puestos"));
            var ocupados = new List<Vector3>();
            foreach (var p in piedras) if (p != null) ocupados.Add(p.transform.position);
            var huellas = Huellas();   // nada de puestos encima de lugares, casas, cabañas, minas o relleno
            int hechos = 0, vacios = 0, menores = 0;
            for (int i = 0; i < CantidadPuestos; i++)
            {
                Vector3 sitio = Vector3.zero; float yaw = 0f; bool libre = false;
                for (int k0 = 0; k0 < 8 && !libre; k0++)
                    libre = Ubicar(rng, ocupados, 120f, out sitio, out yaw) && !Choca(huellas, sitio.x, sitio.z, 4f, 2f);
                if (!libre) continue;
                ocupados.Add(sitio);
                bool repartido = rng.NextDouble() < 0.5;          // juntos o separados en la misma zona
                float rMin = repartido ? 12f : 3f, rMax = repartido ? 32f : 9f;
                int nPiezas = 3 + rng.Next(4);
                var estructura = new GameObject("coop_puesto_" + (i + 1));
                estructura.transform.position = sitio;
                clones.Add(estructura);
                int puestas = 0;
                for (int k = 0; k < nPiezas; k++)
                {
                    var modelo = piezas[rng.Next(piezas.Count)];
                    Bounds b = LimitesDe(modelo);
                    float radio = Mathf.Clamp(Mathf.Max(b.extents.x, b.extents.z), 0.5f, 10f);
                    float ang = (float)rng.NextDouble() * Mathf.PI * 2f, dist = rMin + (float)rng.NextDouble() * (rMax - rMin);
                    Vector3 c = sitio + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * dist;
                    float y;
                    if (!Plano(c, radio, radio > 4f ? 2.5f : 1.2f, out y) || y < nivelAgua + 0.8f) continue;
                    if (Choca(huellas, c.x, c.z, radio, 1.0f)) continue;   // ni encima de otra pieza ni de algo ya colocado
                    huellas.Add(new Vector4(c.x, c.z, radio, 0f));
                    c.y = y;
                    Quaternion giro = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                    Vector3 desfase = modelo.transform.position - new Vector3(b.center.x, b.min.y, b.center.z);
                    int faltan;
                    var pieza = Coop.Piezas.Clonar(modelo, c + giro * desfase - Vector3.up * 0.1f, giro * modelo.transform.rotation, out faltan);
                    pieza.name = "coop_puesto_" + (i + 1) + "_" + modelo.name;
                    pieza.transform.SetParent(estructura.transform, true);
                    foreach (var ps in pieza.GetComponentsInChildren<ParticleSystem>(true)) Object.Destroy(ps.gameObject == pieza ? (UnityEngine.Object)ps : ps.gameObject);   // si las partículas están en la raíz, solo se quita el sistema (no el objeto entero)
                    if (radio > 2f)
                    {
                        var obs = pieza.AddComponent<NavMeshObstacle>();
                        obs.shape = NavMeshObstacleShape.Box; obs.carving = true;
                        obs.center = Quaternion.Inverse(pieza.transform.rotation) * (new Vector3(c.x, c.y + b.extents.y, c.z) - pieza.transform.position);
                        obs.size = new Vector3(b.size.x * 0.8f, b.size.y, b.size.z * 0.8f);
                    }
                    puestas++;
                }
                // Fogata con luz en el centro: se ve de lejos.
                float ys;
                if (Plano(sitio, 1.5f, 1f, out ys))
                {
                    // la fogata del juego a veces sale invisible (malla horneada) o no existe: entonces quedaba solo la luz naranja "de la nada"
                    // (reporte 2026-11-17). Si la copia no se ve, va la fogata propia del mod (troncos, llama y luz).
                    bool hayFuego = false;
                    if (fogata != null)
                    {
                        int f2;
                        var fg = Coop.Piezas.Clonar(fogata, new Vector3(sitio.x, ys, sitio.z) + (fogata.transform.position - LimitesDe(fogata).center).y * Vector3.up, fogata.transform.rotation, out f2);
                        if (fg != null && MallaUtilizable(fg)) { fg.transform.SetParent(estructura.transform, true); hayFuego = true; }
                        else if (fg != null) Object.Destroy(fg);
                    }
                    if (!hayFuego) Fogata.Crear(new Vector3(sitio.x, ys + 0.03f, sitio.z), estructura.transform);
                    var gl = new GameObject("coop_puesto_luz");
                    gl.transform.SetParent(estructura.transform, false);
                    gl.transform.position = new Vector3(sitio.x, ys + 1.2f, sitio.z);
                    var luz = gl.AddComponent<Light>();
                    luz.type = LightType.Point; luz.color = new Color(1f, 0.55f, 0.2f); luz.range = 14f; luz.intensity = 2.2f; luz.shadows = LightShadows.None;
                }

                // Recompensa: 1 % nada de valor, 24 % algo menor, 75 % entre 2 y 4 cosas.
                double tirada = rng.NextDouble();
                int nRec; bool menor = false;
                if (tirada < 0.01) { nRec = 0; vacios++; }
                else if (tirada < 0.25) { nRec = 1; menor = true; menores++; }
                else nRec = 2 + rng.Next(3);
                for (int r = 0; r < nRec; r++)
                {
                    string tipo = menor ? RecompensaMenor[rng.Next(RecompensaMenor.Length)] : Recompensas[rng.Next(Recompensas.Length)];
                    float ang = (float)rng.NextDouble() * Mathf.PI * 2f, dist = 2f + (float)rng.NextDouble() * (rMax * 0.6f);
                    Vector3 cand = sitio + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * dist;
                    NavMeshHit nh;
                    if (!NavMesh.SamplePosition(cand + Vector3.up, out nh, 4f, NavMesh.AllAreas)) continue;
                    var botin = ClonarBotin(tipo, nh.position, (float)rng.NextDouble() * 360f, "puesto" + (i + 1) + "_" + r);
                    if (botin != null) clones.Add(botin);
                }

                var pu = new Puesto { Pos = sitio, Estructura = estructura, Guardian = Guardianes[rng.Next(Guardianes.Length)], CuantosGuardianes = 1 + rng.Next(2) };
                puestos.Add(pu);
                hechos++;
                plugin.Log("Mundo: puesto " + (i + 1) + " en " + (sitio - Ancla()) + ": " + puestas + " piezas " + (repartido ? "repartidas" : "juntas") + ", " + (nRec == 0 ? "sin nada de valor" : nRec + (menor ? " cosa menor" : " recompensas")) + ", guardián " + pu.Guardian + " ×" + pu.CuantosGuardianes);
            }
            plugin.Log("Mundo: " + hechos + " puestos creados (" + vacios + " sin nada de valor, " + menores + " con algo menor), " + piezas.Count + " tipos de pieza");
        }

        /// Copia de un recogible (con su lógica de recoger) en otro sitio, registrada para que
        /// se sincronice al recogerla. Su marca de "recogido" no toca al original.
        /// ¿Se puede duplicar este objeto y que se vea? (sin mallas dentro de un lote estático, o con la copia visual que se le hizo)
        static bool MallaUtilizable(GameObject g)
        {
            bool horneado = false;
            foreach (var r in g.GetComponentsInChildren<MeshRenderer>(true)) if (r.isPartOfStaticBatch) { horneado = true; break; }
            if (!horneado) return true;
            // (2026-11-19) las cajas de balas llevan el paquete de cartuchos del mod (coop_paquete) y sus mallas del juego apagadas:
            // sin esto ninguna caja servía de original y los cofres "con balas" quedaban vacíos
            foreach (Transform h in g.transform) if (h.name.StartsWith("coop_visual_") || h.name == "coop_paquete") return true;
            return false;
        }

        GameObject ClonarBotin(string tipo, Vector3 suelo, float yaw, string id)
        {
            GameObject original = null; string idOriginal = null;
            foreach (var o in objetos.Values)
            {
                if (o.Go == null || !o.Go.activeInHierarchy || o.Id.StartsWith("clon:") || TipoDeId(o.Id) != tipo) continue;
                if (!MallaUtilizable(o.Go)) continue;   // con la malla horneada en un lote del juego, la copia saldría invisible
                if (idOriginal == null || string.CompareOrdinal(o.Id, idOriginal) < 0) { original = o.Go; idOriginal = o.Id; }
            }
            if (original == null) return null;
            float sobreSuelo = 0.05f;
            RaycastHit h;
            if (Physics.Raycast(original.transform.position + Vector3.up * 0.5f, Vector3.down, out h, 5f, ~0, QueryTriggerInteraction.Ignore))
                sobreSuelo = Mathf.Clamp(original.transform.position.y - h.point.y, 0f, 1.5f);
            var copia = (GameObject)Object.Instantiate(original, suelo + Vector3.up * sobreSuelo, Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(original.transform.eulerAngles.x, 0f, original.transform.eulerAngles.z));
            foreach (var pd in copia.GetComponentsInChildren<PixelCrushers.DialogueSystem.PersistentDestructible>(true))
            {
                pd.variableName = "coop_clon";        // si algo lo marca, no es el original
                pd.OnLevelWillBeUnloaded();           // no registrar nada al destruirlo
                Object.Destroy(pd);
            }
            copia.name = original.name + " [coop]";
            string cid = "clon:" + id;
            objetos[cid] = new Objeto { Id = cid, Go = copia, Ultima = copia.transform.position };
            Asentar.EnSuelo(copia, 8f);   // botín (también el oro suelto) nunca en el aire
            return copia;
        }

        /// "Sack_of_coins (4)@123,45,678" → "Sack_of_coins"
        static string TipoDeId(string id)
        {
            int a = id.IndexOf('@'); string n = a > 0 ? id.Substring(0, a) : id;
            int p = n.IndexOf(" ("); return p > 0 ? n.Substring(0, p) : n;
        }

        static Bounds LimitesDe(GameObject g)
        {
            Bounds b = new Bounds(g.transform.position, Vector3.one);
            bool primero = true;
            foreach (var r in g.GetComponentsInChildren<MeshRenderer>())
            {
                if (!r.enabled) continue;
                if (primero) { b = r.bounds; primero = false; } else b.Encapsulate(r.bounds);
            }
            return b;
        }

        /// ¿El terreno es lo bastante plano en ese radio? Devuelve la altura más baja (para hundir la base).
        static bool Plano(Vector3 c, float radio, float maxDesnivel, out float y)
        {
            y = c.y;
            float min = float.MaxValue, max = float.MinValue;
            for (int k = 0; k <= 8; k++)
            {
                Vector3 p = k == 8 ? c : c + new Vector3(Mathf.Cos(k * Mathf.PI / 4f), 0f, Mathf.Sin(k * Mathf.PI / 4f)) * radio;
                float h = AlturaEn(p);
                if (float.IsNaN(h)) return false;
                min = Mathf.Min(min, h); max = Mathf.Max(max, h);
            }
            y = min;
            return max - min <= maxDesnivel;
        }

        /// El anfitrión hace aparecer a los guardianes cuando alguien se acerca a un puesto.
        void ActualizarGuardianes()
        {
            try { ActualizarTorre(); } catch (System.Exception e) { if (Time.frameCount % 900 == 0) plugin.Log("Torre: error " + e.Message); }
            if (!plugin.EsAnfitrion || plugin.CombateActual == null || puestos.Count == 0) return;
            var j = plugin.Jugador; var c = plugin.CompaneroActual;
            foreach (var pu in puestos)
            {
                if (pu.Generado) continue;
                bool cerca = j != null && Vector3.Distance(j.transform.position, pu.Pos) < 110f
                          || c != null && c.Visible && Vector3.Distance(c.PosicionMundo, pu.Pos) < 110f;
                if (!cerca) continue;
                pu.Generado = true;
                for (int k = 0; k < pu.CuantosGuardianes; k++)
                {
                    Vector3 p = pu.Pos + new Vector3(Random.Range(-6f, 6f), 0.5f, Random.Range(-6f, 6f));
                    bool pot = UnityEngine.Random.value < Potenciado.ProbabilidadActual || forzarPotenciado;
                    var td = plugin.CombateActual.Generar(pu.Guardian + (pot ? Potenciado.Marca : ""), p);
                    if (td != null) clones.Add(td.transform.root.gameObject);
                }
                plugin.Log("Mundo: guardianes del puesto en " + (pu.Pos - Ancla()) + " (" + pu.Guardian + ")");
            }
        }

        public bool forzarPotenciado;   // prueba: todos los guardianes salen potenciados

        public string DescribirPuestos()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var pu in puestos) sb.Append(pu.Estructura != null ? pu.Estructura.name : "?").Append(" ").Append(pu.Pos - Ancla()).Append(pu.Generado ? " (guardianes)" : "").Append("; ");
            return sb.ToString();
        }
    }
}

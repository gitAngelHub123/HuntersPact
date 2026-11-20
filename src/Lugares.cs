// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using System;
using System.Collections.Generic;
using HutongGames.PlayMaker;
using PixelCrushers.DialogueSystem;
using UnityEngine;
using UnityEngine.AI;

namespace WitchHuntCoop
{
    /// Lugares únicos del bosque, colocados UNA VEZ POR PARTIDA (semilla de partida, guardada en la
    /// partida cooperativa del anfitrión): cuevas (con la celda del perro y la del cañón), torre,
    /// círculo de piedras, casa en el árbol, sombra blanca, puentes y piedras de guardado.
    ///
    /// Para que cada cosa "tenga sentido" donde aparece se guarda el relieve del terreno bajo ella en
    /// su sitio original (una rejilla de alturas en su propio sistema de ejes) y solo se acepta un sitio
    /// nuevo cuyo relieve, con algún giro, se parezca: una cueva va contra una ladera, un puente cruza
    /// agua, una piedra de guardado queda en llano. Si no hay sitio parecido, se queda donde estaba.
    public partial class Mundo
    {
        public const string VarSemillaPartida = "coop_semilla_partida";
        public const string VarLibrosRecogidos = "coop_libros_recogidos";

        public int SemillaPartida;   // 0 = aún no se sabe

        // Posición y giro de cada raíz al cargar la escena (la sombra blanca deambula: hay que medir
        // todos los lugares desde donde los dejó el juego, igual en los dos jugadores).
        readonly Dictionary<Transform, KeyValuePair<Vector3, Quaternion>> inicio = new Dictionary<Transform, KeyValuePair<Vector3, Quaternion>>();

        void FotografiarInicio()
        {
            inicio.Clear();
            foreach (var g in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                if (g != null) inicio[g.transform] = new KeyValuePair<Vector3, Quaternion>(g.transform.position, g.transform.rotation);
        }

        // La sombra blanca aparece en un punto al azar del juego: se mide siempre desde el de referencia.
        static readonly Vector3 SombraRef = new Vector3(783.2f, 83f, 1717f);

        Vector3 PosInicial(Transform t)
        {
            if (t.name.StartsWith("WhiteShadowParrent")) return SombraRef + Ancla();
            KeyValuePair<Vector3, Quaternion> kv; return inicio.TryGetValue(t, out kv) ? kv.Key : t.position;
        }
        Quaternion RotInicial(Transform t) { KeyValuePair<Vector3, Quaternion> kv; return inicio.TryGetValue(t, out kv) ? kv.Value : t.rotation; }
        public int LugaresMovidos, LugaresFijos;
        readonly List<string> informeLugares = new List<string>();

        /// Nombres base (sin " (n)") de los lugares únicos, según la clasificación del usuario.
        static readonly string[] Unicos = {
            "Cave_Parrent", "Cannon_parrent", "Church_Tower_Bell", "StoneHenge", "Tree+Parrent",
            "WhiteShadowParrent_v2", "bridge", "my_wooden_bridge", "save_stone_witch",
        };
        // Lo que nunca se arrastra como "satélite" de un lugar.
        static readonly string[] NoArrastrar = { "Crux_ai", "coop_", "WitchHuntCoop", "Terrain", "!!!", "Dialogue Manager", "Canvas", "THE_WOOD_CABIN",
            "Skill_book", "_pickable", "Ammo_", "Sack_of_coins", "Gold_Nugget", "Lockpicks", "med_plant", "magic_totem" };

        /// Un lugar a mover: una o varias raíces (con sus satélites) que se mueven juntas.
        class Lugar
        {
            public string Nombre;
            public readonly List<Transform> Raices = new List<Transform>();
            public Vector3 Pivote;         // centro del pie de la estructura, a la altura de su base
            public float Yaw0;             // orientación original (de la raíz principal)
            public Bounds Caja;            // caja en el mundo (unión de renderers)
            public Vector2[] Muestras;     // puntos de la rejilla en ejes propios (x, z)
            public float[] Relieve;        // altura del terreno en cada muestra menos la base
            public bool Agua, Puente;
            public Vector2 Pendiente;      // gradiente del terreno (plano ajustado) en ejes propios
            public float Radio;
            public Vector3 Destino; public float GiroY; public bool Movido;
        }

        // ---------------- Semilla de partida ----------------

        /// Anfitrión: lee la semilla de su partida coop; si no hay, la crea (se guarda al guardar).
        public void PrepararSemillaPartida()
        {
            if (!plugin.EsAnfitrion) return;
            int s = 0;
            try { s = DialogueLua.GetVariable(VarSemillaPartida).AsInt; } catch { }
            if (s == 0)
            {
                s = azar.Next(1, int.MaxValue);
                DialogueLua.SetVariable(VarSemillaPartida, s);
                plugin.Log("Mundo: partida nueva, semilla de partida " + s);
            }
            if (s != SemillaPartida) { SemillaPartida = s; plugin.Log("Mundo: semilla de partida " + s); }
        }

        // ---------------- Lugares únicos ----------------

        void ColocarLugares(int semillaPartida)
        {
            LugaresMovidos = LugaresFijos = 0;
            enLugares.Clear();
            informeLugares.Clear();
            Vector3 an = Ancla();
            var lugares = ArmarLugares(an);
            var colocados = new List<Lugar>();
            // Los grandes primero: les cuesta más encontrar sitio.
            lugares.Sort((a, b) => b.Radio.CompareTo(a.Radio) != 0 ? b.Radio.CompareTo(a.Radio) : string.CompareOrdinal(a.Nombre, b.Nombre));
            foreach (var l in lugares)
            {
                // Solo se mueve lo que se puede rearmar entero con sus mallas exactas.
                int hTot = 0, eTot = 0;
                foreach (var t in l.Raices) { int h, e; Coop.Piezas.Calidad(t.gameObject, out h, out e); hTot += h; eTot += e; }
                if (eTot < hTot)
                {
                    LugaresFijos++; colocados.Add(l);
                    string fija = l.Nombre + " se queda en " + (l.Pivote - an).ToString("F0") + " (solo " + eTot + " de " + hTot + " piezas se pueden rearmar)";
                    informeLugares.Add(fija); plugin.Log("Lugar: " + fija);
                    AjustarLugarFijo(l);
                    continue;
                }
                var rng = new System.Random(semillaPartida ^ Hash("coop_lugar_" + l.Nombre));
                bool ok = BuscarSitio(l, rng, colocados);
                if (ok) { Trasladar(l); LugaresMovidos++; }
                else { LugaresFijos++; AjustarLugarFijo(l); }
                colocados.Add(l);
                string linea = l.Nombre + (ok ? " → " + (l.Destino - an).ToString("F0") + " giro " + Mathf.RoundToInt(l.GiroY) + "°" : " se queda en " + (l.Pivote - an).ToString("F0") + " (sin sitio parecido)") + " | " + ultimoDiag;
                informeLugares.Add(linea);
                plugin.Log("Lugar: " + linea);
            }
        }

        string ultimoDiag = "";
        readonly List<string> descartes = new List<string>();

        public string DescribirLugares() { return string.Join("; ", informeLugares.ToArray()); }

        /// Agrupa las raíces únicas con sus satélites (lo que está dentro de su caja: la reja y el
        /// perro dentro de la cueva, el tronco bajo la casa del árbol...). El cañón va con la cueva
        /// que tiene enfrente, que es la que derriba.
        List<Lugar> ArmarLugares(Vector3 an)
        {
            var raices = new List<GameObject>(UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects());
            raices.RemoveAll(g => g == null || !g.activeInHierarchy);
            raices.Sort((a, b) => { int c = string.CompareOrdinal(a.name, b.name); return c != 0 ? c : a.transform.position.x.CompareTo(b.transform.position.x); });

            var unicos = new List<GameObject>();
            foreach (var g in raices) if (Array.IndexOf(Unicos, Catalogo.BaseDe(g.name)) >= 0) unicos.Add(g);

            var grupos = new List<List<GameObject>>();
            var usados = new HashSet<GameObject>();
            GameObject canon = unicos.Find(g => Catalogo.BaseDe(g.name) == "Cannon_parrent");
            if (canon != null)
            {
                GameObject cueva = null; float mejor = 130f;
                foreach (var g in unicos)
                    if (Catalogo.BaseDe(g.name) == "Cave_Parrent")
                    {
                        float d = Vector3.Distance(PosInicial(g.transform), PosInicial(canon.transform));
                        if (d < mejor) { mejor = d; cueva = g; }
                    }
                var gr = new List<GameObject> { canon };
                if (cueva != null) gr.Add(cueva);
                grupos.Add(gr); foreach (var g in gr) usados.Add(g);
            }
            foreach (var g in unicos) if (!usados.Contains(g)) { grupos.Add(new List<GameObject> { g }); usados.Add(g); }

            var lugares = new List<Lugar>();
            foreach (var gr in grupos)
            {
                Bounds caja; if (!CajaDe(gr, out caja)) continue;
                caja.center += PosInicial(gr[0].transform) - gr[0].transform.position;   // lo que se movió desde la carga
                if (Vector3.Distance(Plano(caja.center), Plano(cabana)) < RadioCabana + caja.extents.magnitude) continue;   // nada de la cabaña
                var l = new Lugar { Nombre = gr[0].name + (gr.Count > 1 ? " + " + gr[1].name : ""), Caja = caja };
                foreach (var g in gr) { l.Raices.Add(g.transform); enLugares.Add(g.transform); }
                // Satélites: raíces no únicas dentro de la caja (algo agrandada).
                var grande = caja; grande.Expand(new Vector3(6f, 20f, 6f));
                foreach (var g in raices)
                {
                    if (usados.Contains(g) || unicos.Contains(g)) continue;
                    bool fuera = false;
                    foreach (var n in NoArrastrar) if (g.name.Contains(n)) { fuera = true; break; }
                    if (fuera || g.GetComponentInChildren<FPSPlayer>() != null) continue;
                    if (!grande.Contains(PosInicial(g.transform))) continue;
                    l.Raices.Add(g.transform); usados.Add(g); enLugares.Add(g.transform);
                }
                l.Puente = Catalogo.BaseDe(gr[0].name).Contains("bridge");
                l.Yaw0 = RotInicial(gr[0].transform).eulerAngles.y;
                float suelo = SueloEn(caja.center);
                l.Pivote = new Vector3(caja.center.x, float.IsNaN(suelo) ? caja.min.y : suelo, caja.center.z);
                l.Radio = new Vector2(caja.extents.x, caja.extents.z).magnitude;
                TomarRelieve(l);
                lugares.Add(l);
            }
            return lugares;
        }

        static Vector3 Plano(Vector3 v) { return new Vector3(v.x, 0f, v.z); }

        static bool CajaDe(List<GameObject> gs, out Bounds b)
        {
            b = new Bounds(); bool hay = false;
            foreach (var g in gs)
                foreach (var r in g.GetComponentsInChildren<Renderer>())
                {
                    if (r is ParticleSystemRenderer || r is LineRenderer || r is TrailRenderer) continue;
                    if (!hay) { b = r.bounds; hay = true; } else b.Encapsulate(r.bounds);
                }
            if (!hay)
                foreach (var g in gs)
                    foreach (var c in g.GetComponentsInChildren<Collider>())
                    {
                        if (c.isTrigger) continue;
                        if (!hay) { b = c.bounds; hay = true; } else b.Encapsulate(c.bounds);
                    }
            return hay;
        }

        /// Rejilla de alturas del terreno bajo la caja, en ejes propios de la estructura.
        void TomarRelieve(Lugar l, int N = 7)
        {
            Quaternion inv = Quaternion.Inverse(Quaternion.Euler(0f, l.Yaw0, 0f));
            var muestras = new List<Vector2>(); var alturas = new List<float>();
            float minH = float.MaxValue;
            for (int i = 0; i < N; i++)
                for (int k = 0; k < N; k++)
                {
                    var w = new Vector3(Mathf.Lerp(l.Caja.min.x, l.Caja.max.x, i / (N - 1f)), 0f, Mathf.Lerp(l.Caja.min.z, l.Caja.max.z, k / (N - 1f)));
                    float h = SueloEn(w);
                    if (float.IsNaN(h)) continue;
                    Vector3 loc = inv * (w - Plano(l.Pivote));
                    muestras.Add(new Vector2(loc.x, loc.z)); alturas.Add(h - l.Pivote.y);
                    minH = Mathf.Min(minH, h);
                }
            l.Muestras = muestras.ToArray(); l.Relieve = alturas.ToArray();
            l.Agua = minH < nivelAgua + 0.5f;
            // Plano h = a·x + b·z + c por mínimos cuadrados: su gradiente dice hacia dónde sube.
            float sx = 0, sz = 0, sh = 0, sxx = 0, szz = 0, sxz = 0, sxh = 0, szh = 0; int n = l.Muestras.Length;
            for (int i = 0; i < n; i++)
            {
                float x = l.Muestras[i].x, z = l.Muestras[i].y, h = l.Relieve[i];
                sx += x; sz += z; sh += h; sxx += x * x; szz += z * z; sxz += x * z; sxh += x * h; szh += z * h;
            }
            if (n > 3)
            {
                float mx = sx / n, mz = sz / n, mh = sh / n;
                float cxx = sxx / n - mx * mx, czz = szz / n - mz * mz, cxz = sxz / n - mx * mz, cxh = sxh / n - mx * mh, czh = szh / n - mz * mh;
                float det = cxx * czz - cxz * cxz;
                if (Mathf.Abs(det) > 1e-4f) l.Pendiente = new Vector2((cxh * czz - czh * cxz) / det, (czh * cxx - cxh * cxz) / det);
            }
        }

        static float Rumbo(float x, float z) { return Mathf.Atan2(x, z) * Mathf.Rad2Deg; }

        /// Busca, con la semilla de partida, el sitio cuyo relieve más se parece al original.
        bool BuscarSitio(Lugar l, System.Random rng, List<Lugar> colocados)
        {
            float tam = Mathf.Max(l.Caja.size.x, l.Caja.size.z);
            float tol = 0.6f + 0.05f * tam;   // error medio admitido (m)
            int intentos = 6000;
            float p0 = l.Pendiente.magnitude, rPend = Mathf.Max(tam * 0.35f, 1.5f);
            bool enLadera = p0 > 0.08f;   // más de ~5°: el giro se saca de la pendiente
            int nPend = 0, nLejos = 0;
            float mejorErr = float.MaxValue; Vector3 mejorP = Vector3.zero; float mejorGiro = 0f;
            Bounds zr = ZonaLugares();
            if (l.Puente) { zr = new Bounds(l.Pivote, new Vector3(700f, 400f, 700f)); }   // los puentes, en su mismo río
            int nCabana = 0, nChoca = 0, nRelieve = 0, nAgua = 0, nCerca = 0;
            for (int i = 0; i < intentos; i++)
            {
                float x = zr.min.x + (float)rng.NextDouble() * zr.size.x;
                float z = zr.min.z + (float)rng.NextDouble() * zr.size.z;
                float giro = (float)rng.NextDouble() * 360f;
                float jitter = ((float)rng.NextDouble() - 0.5f) * 20f;
                var c = new Vector3(x, 0f, z);
                if (enLadera)
                {
                    float hx1 = AlturaEn(c + Vector3.right * rPend), hx0 = AlturaEn(c - Vector3.right * rPend);
                    float hz1 = AlturaEn(c + Vector3.forward * rPend), hz0 = AlturaEn(c - Vector3.forward * rPend);
                    if (float.IsNaN(hx1 + hx0 + hz1 + hz0)) { nRelieve++; continue; }
                    float gx = (hx1 - hx0) / (2f * rPend), gz = (hz1 - hz0) / (2f * rPend);
                    float pc = Mathf.Sqrt(gx * gx + gz * gz);
                    if (Mathf.Abs(pc - p0) > 0.4f * p0 + 0.05f) { nPend++; continue; }
                    giro = Rumbo(gx, gz) - Rumbo(l.Pendiente.x, l.Pendiente.y) + jitter;
                }
                if (Vector3.Distance(c, Plano(cabana)) < RadioCabana + l.Radio + 10f) { nCabana++; continue; }
                if (Vector3.Distance(c, Plano(l.Pivote)) < 40f) { nCerca++; continue; }
                bool choca = false;
                foreach (var o in colocados)
                {
                    Vector3 oc = o.Movido ? o.Destino : o.Pivote;
                    if (Vector3.Distance(c, Plano(oc)) < l.Radio + o.Radio + 25f) { choca = true; break; }
                }
                if (!choca && l.Nombre.StartsWith("save_stone"))
                    foreach (var o in colocados)
                        if (o.Nombre.StartsWith("save_stone") && Vector3.Distance(c, Plano(o.Movido ? o.Destino : o.Pivote)) < SeparacionGuardado) { choca = true; break; }
                if (choca) { nChoca++; continue; }
                float err, baseY; bool agua;
                if (!Comparar(l, c, giro, tol, out err, out baseY, out agua)) { nRelieve++; continue; }
                if (agua != l.Agua) { nAgua++; continue; }
                if (l.Puente && baseY > nivelAgua) { nAgua++; continue; }   // un puente tiene agua debajo
                if (err >= mejorErr) continue;
                // Que se pueda llegar a pie (los puentes: al menos una orilla).
                if (hayLlegada && !Alcanzable(l, new Vector3(x, baseY, z), giro)) { nLejos++; continue; }
                mejorErr = err; mejorP = new Vector3(x, baseY, z); mejorGiro = giro;
                if (err < tol * 0.5f) break;   // ya es muy parecido
            }
            descartes.Clear();
            descartes.Add("cabaña=" + nCabana); descartes.Add("choca=" + nChoca); descartes.Add("relieve=" + nRelieve); descartes.Add("agua=" + nAgua); descartes.Add("cerca=" + nCerca); descartes.Add("pendiente=" + nPend); descartes.Add("a_pie=" + nLejos);
            ultimoDiag = "mejor error " + (mejorErr == float.MaxValue ? "—" : mejorErr.ToString("0.00")) + " m (tolerancia " + tol.ToString("0.00") + "), descartes: " + string.Join(" ", descartes.ToArray());
            if (mejorErr > tol) return false;
            l.Destino = mejorP; l.GiroY = mejorGiro; l.Movido = true;
            return true;
        }

        bool Alcanzable(Lugar l, Vector3 p, float giro)
        {
            NavMeshHit nh;
            if (l.Puente)
            {
                Vector3 a, b; ExtremosPuente(l, p + Vector3.up * 2f, Quaternion.Euler(0f, giro - l.Yaw0, 0f), out a, out b);
                foreach (var e in new[] { a, b })
                    if (NavMesh.SamplePosition(e, out nh, 8f, NavMesh.AllAreas) && NavMesh.CalculatePath(nhLlegada.position, nh.position, NavMesh.AllAreas, camino) && camino.status == NavMeshPathStatus.PathComplete) return true;
                return false;
            }
            if (!NavMesh.SamplePosition(p, out nh, l.Radio + 8f, NavMesh.AllAreas)) return false;
            return NavMesh.CalculatePath(nhLlegada.position, nh.position, NavMesh.AllAreas, camino) && camino.status == NavMeshPathStatus.PathComplete;
        }

        /// Compara el relieve en el sitio candidato (girado) con el original. Sale antes si ya es malo.
        bool Comparar(Lugar l, Vector3 c, float giro, float tol, out float err, out float baseY, out bool agua)
        {
            err = float.MaxValue; baseY = 0f; agua = false;
            Quaternion rot = Quaternion.Euler(0f, giro, 0f);
            int n = l.Muestras.Length;
            if (n == 0) return false;
            var d = new float[n];
            float suma = 0f, minH = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                Vector3 w = c + rot * new Vector3(l.Muestras[i].x, 0f, l.Muestras[i].y);
                float h = AlturaEn(w);
                if (float.IsNaN(h)) return false;
                minH = Mathf.Min(minH, h);
                d[i] = h - l.Relieve[i];   // altura que tendría la base según esta muestra
                suma += d[i];
                // Corte rápido tras las primeras muestras.
                if (i == 8)
                {
                    float m0 = suma / 9f, e0 = 0f;
                    for (int k = 0; k < 9; k++) e0 += (d[k] - m0) * (d[k] - m0);
                    if (Mathf.Sqrt(e0 / 9f) > tol * 2.5f) return false;
                }
            }
            float media = suma / n, e = 0f;
            for (int i = 0; i < n; i++) e += (d[i] - media) * (d[i] - media);
            err = Mathf.Sqrt(e / n);
            baseY = media;
            agua = minH < nivelAgua + 0.5f;
            if (!agua && baseY < nivelAgua + 1f) return false;
            return true;
        }

        /// Dónde pueden ir los lugares: el bosque jugable (y la zona norte si existe).
        Bounds ZonaLugares()
        {
            Bounds b = zona;
            if (nuevo != null)
                b.Encapsulate(new Bounds(nuevo.transform.position + new Vector3(AnchoNuevo / 2f, 50f, FondoNuevo / 2f), new Vector3(AnchoNuevo - 2f * Pared - 60f, 200f, FondoNuevo - Pared - 60f)));
            return b;
        }

        /// Suelo ORIGINAL bajo un lugar (antes de abrir el paso a la zona nueva): las posiciones iniciales de sus piezas están puestas sobre
        /// ese suelo. Con el suelo ya rebajado, la casa del árbol y su botín quedaban 6 m por encima del terreno, con las raíces en el aire.
        float SueloEn(Vector3 p) { return AlturaOriginalEn(p); }

        /// Un lugar que se queda en su sitio pero cuyo suelo cambió (el paso a la zona nueva lo rebaja o lo rellena): baja o sube entero
        /// con el suelo. Se hace con Trasladar para que las mallas horneadas también se muevan (si no, se queda la copia dibujada arriba).
        void AjustarLugarFijo(Lugar l)
        {
            if (l.Puente) return;
            int hT = 0, eT = 0;
            foreach (var t in l.Raices) { int h0, e0; Coop.Piezas.Calidad(t.gameObject, out h0, out e0); hT += h0; eT += e0; }
            if (eT < hT) return;   // no se puede rearmar entero: su suelo ya quedó protegido al abrir el paso
            float ahora = AlturaEn(new Vector3(l.Pivote.x, 0f, l.Pivote.z));
            if (float.IsNaN(ahora)) return;
            float dy = ahora - l.Pivote.y;
            if (Mathf.Abs(dy) < 0.4f) return;
            l.Destino = l.Pivote + Vector3.up * dy; l.GiroY = l.Yaw0;
            Trasladar(l);
            plugin.Log("Lugar: " + l.Nombre + " se queda en su sitio pero baja/sube " + dy.ToString("0.0") + " m con el suelo del paso");
        }

        /// Mueve todas las raíces del lugar con el mismo giro y desplazamiento. Lo horneado (static
        /// batching) se dibuja con una copia de sus mallas sueltas; el original (colisiones, lógica,
        /// persistencia) se mueve y deja de dibujar sus piezas horneadas.
        void Trasladar(Lugar l)
        {
            Quaternion giro = Quaternion.Euler(0f, l.GiroY - l.Yaw0, 0f);
            Vector3 viejoPuenteA = Vector3.zero, viejoPuenteB = Vector3.zero;
            if (l.Puente) ExtremosPuente(l, l.Pivote, Quaternion.identity, out viejoPuenteA, out viejoPuenteB);
            foreach (var t in l.Raices)
            {
                if (t == null) continue;
                Vector3 nuevaPos = l.Destino + giro * (PosInicial(t) - l.Pivote);
                Quaternion nuevaRot = giro * RotInicial(t);
                bool horneado = false;
                foreach (var r in t.GetComponentsInChildren<MeshRenderer>(true)) if (r.isPartOfStaticBatch) { horneado = true; break; }
                if (horneado)
                {
                    int faltan;
                    var copia = Coop.Piezas.Clonar(t.gameObject, nuevaPos, nuevaRot, out faltan);
                    copia.name = "coop_lugar_" + t.name;
                    // La copia solo dibuja lo horneado; el resto (y las colisiones) lo pone el original.
                    var origR = t.GetComponentsInChildren<MeshRenderer>(true);
                    var copR = copia.GetComponentsInChildren<MeshRenderer>(true);
                    for (int i = 0; i < origR.Length && i < copR.Length; i++) if (!origR[i].isPartOfStaticBatch) copR[i].enabled = false;
                    foreach (var c in copia.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.Destroy(c);
                    foreach (var ps in copia.GetComponentsInChildren<ParticleSystem>(true)) UnityEngine.Object.Destroy(ps.gameObject == copia ? (UnityEngine.Object)ps : ps.gameObject);   // si las partículas están en la raíz, solo se quita el sistema (no el objeto entero)
                    // las piedras de guardado conservan su luz en la copia (la original se apaga más abajo si queda sin piedra)
                    if (!t.name.StartsWith("save_stone_witch")) foreach (var lu in copia.GetComponentsInChildren<Light>(true)) lu.enabled = false;
                    foreach (var r in origR) if (r.isPartOfStaticBatch) r.enabled = false;
                    lugaresCopias.Add(copia); copiaOriginal[copia] = t.gameObject;
                }
                t.position = nuevaPos; t.rotation = nuevaRot;
            }
            if (l.Puente) EnlazarPuente(l, viejoPuenteA, viejoPuenteB, giro);
            else TaparHueco(l);
        }

        readonly List<GameObject> lugaresCopias = new List<GameObject>();
        readonly Dictionary<GameObject, GameObject> copiaOriginal = new Dictionary<GameObject, GameObject>();

        /// Rocas que vienen con un lugar trasladado: si en su sitio nuevo quedan en el aire (en una ladera, con hueco bajo un costado),
        /// se quitan, la copia que se dibuja y la colisión invisible del original (pedido 2026-11-17: roca flotante al entrar a la zona nueva).
        void QuitarRocasFlotantesLugares()
        {
            int quitadas = 0;
            foreach (var copia in new List<GameObject>(lugaresCopias))
            {
                if (copia == null || copia.name.IndexOf("rock", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                Bounds b = new Bounds(); bool hay = false;
                foreach (var r in copia.GetComponentsInChildren<MeshRenderer>()) { if (!r.enabled) continue; if (!hay) { b = r.bounds; hay = true; } else b.Encapsulate(r.bounds); }
                if (!hay) continue;
                float huecoCentro = b.min.y - AlturaEn(b.center), huecoMax = huecoCentro;
                foreach (var dx in new[] { -0.4f, 0.4f }) foreach (var dz in new[] { -0.4f, 0.4f })
                {
                    float g = AlturaEn(b.center + new Vector3(b.size.x * dx, 0f, b.size.z * dz));
                    if (!float.IsNaN(g)) huecoMax = Mathf.Max(huecoMax, b.min.y - g);
                }
                if (float.IsNaN(huecoCentro) || (huecoCentro < 0.35f && huecoMax < 0.9f)) continue;
                GameObject orig;
                if (copiaOriginal.TryGetValue(copia, out orig) && orig != null)
                {
                    foreach (var c in orig.GetComponentsInChildren<Collider>(true)) c.enabled = false;
                    foreach (var r in orig.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
                }
                plugin.Log("Lugar: roca " + copia.name + " quitada (flotaba " + huecoCentro.ToString("0.0") + " m en el centro, " + huecoMax.ToString("0.0") + " m en un costado)");
                lugaresCopias.Remove(copia); copiaOriginal.Remove(copia); UnityEngine.Object.Destroy(copia); quitadas++;
            }
            if (quitadas > 0) plugin.Log("Lugar: " + quitadas + " rocas flotantes quitadas");
        }

        /// Luces de fuego sueltas (sin piedra ni fogata cerca) se apagan: una luz naranja siempre va con algo.
        void ApagarLucesHuerfanas()
        {
            int apagadas = 0;
            foreach (var l in Resources.FindObjectsOfTypeAll<Light>())
            {
                if (!l.gameObject.scene.IsValid() || !l.enabled || !l.gameObject.activeInHierarchy || l.type == LightType.Directional) continue;
                string raiz = l.transform.root.name;
                if (!raiz.StartsWith("save_stone_witch")) continue;   // solo las piedras de guardado originales
                bool hayPiedra = false;
                foreach (var r in UnityEngine.Object.FindObjectsOfType<MeshRenderer>())
                {
                    if (!r.enabled || !r.gameObject.activeInHierarchy || r.isPartOfStaticBatch) continue;
                    if (Vector3.Distance(r.bounds.center, l.transform.position) < 5f && (r.name.ToLowerInvariant().Contains("stone") || r.transform.root.name.StartsWith("coop_"))) { hayPiedra = true; break; }
                }
                if (!hayPiedra)
                {
                    l.enabled = false; apagadas++;
                    Vector3 sp = l.transform.position; RaycastHit hf;
                    if (Physics.Raycast(sp + Vector3.up * 0.5f, Vector3.down, out hf, 6f, ~0, QueryTriggerInteraction.Ignore)) sp = hf.point;
                    Fogata.Crear(sp, null);   // donde había luz y ya no hay piedra: una mini fogata que la explique
                }
            }
            plugin.Log("Lugares: " + apagadas + " luces de piedra sin piedra cerca apagadas");
        }

        /// Extremos del tablero de un puente (eje largo de su caja) en una posición/rotación dada.
        static void ExtremosPuente(Lugar l, Vector3 centro, Quaternion giro, out Vector3 a, out Vector3 b)
        {
            Vector3 eje = l.Caja.size.x >= l.Caja.size.z ? Vector3.right : Vector3.forward;
            float largo = Mathf.Max(l.Caja.size.x, l.Caja.size.z) * 0.5f + 1.5f;
            a = centro + giro * (eje * largo); b = centro - giro * (eje * largo);
        }

        /// El mapa de caminos del juego está horneado: en el sitio viejo del puente se tapa el paso y
        /// en el nuevo se pone un enlace entre las dos orillas (enemigos y reparto de botín lo usan).
        void EnlazarPuente(Lugar l, Vector3 viejoA, Vector3 viejoB, Quaternion giro)
        {
            var tapa = new GameObject("coop_puente_tapado_" + l.Nombre);
            tapa.transform.position = l.Pivote;
            var obs = tapa.AddComponent<NavMeshObstacle>();
            obs.shape = NavMeshObstacleShape.Box; obs.carving = true; obs.carveOnlyStationary = false;
            obs.size = new Vector3(Mathf.Max(l.Caja.size.x * 0.6f, 4f), 20f, Mathf.Max(l.Caja.size.z * 0.6f, 4f));
            lugaresCopias.Add(tapa);

            Vector3 a, b; ExtremosPuente(l, l.Destino + Vector3.up * 2f, giro, out a, out b);
            NavMeshHit ha, hb;
            if (!NavMesh.SamplePosition(a, out ha, 8f, NavMesh.AllAreas) || !NavMesh.SamplePosition(b, out hb, 8f, NavMesh.AllAreas))
            { plugin.Log("Lugar: el puente " + l.Nombre + " no tiene orillas en el mapa de caminos"); return; }
            var ga = new GameObject("coop_puente_a"); ga.transform.position = ha.position;
            var gb = new GameObject("coop_puente_b"); gb.transform.position = hb.position;
            var enlace = ga.AddComponent<OffMeshLink>();
            enlace.startTransform = ga.transform; enlace.endTransform = gb.transform;
            enlace.biDirectional = true; enlace.activated = true; enlace.costOverride = -1f;
            lugaresCopias.Add(ga); lugaresCopias.Add(gb);
        }

        /// Donde estaba una estructura grande queda un hueco en el mapa de caminos; donde llega, el
        /// mapa la atraviesa. Se tapa el sitio nuevo para que los enemigos la rodeen (las cuevas no:
        /// por dentro se camina).
        void TaparHueco(Lugar l)
        {
            if (l.Radio < 4f || l.Nombre.StartsWith("Cave") || l.Nombre.StartsWith("Cannon")) return;
            var tapa = new GameObject("coop_lugar_obstaculo");
            tapa.transform.position = l.Destino + Vector3.up * (l.Caja.size.y * 0.5f);
            tapa.transform.rotation = Quaternion.Euler(0f, l.GiroY - l.Yaw0, 0f);
            var obs = tapa.AddComponent<NavMeshObstacle>();
            obs.shape = NavMeshObstacleShape.Box; obs.carving = true;
            obs.size = new Vector3(l.Caja.size.x * 0.8f, l.Caja.size.y, l.Caja.size.z * 0.8f);
            lugaresCopias.Add(tapa);
        }

        // ---------------- Libros ----------------

        /// Libros sueltos (Skill_book en la raíz): sitio fijo por partida, luz blanca suave, y si
        /// alguno ya se recogió en esta partida (por cualquiera de los dos) no vuelve a salir.
        void ColocarLibros(int semillaPartida)
        {
            var recogidos = LibrosRecogidos();
            var rng = new System.Random(semillaPartida ^ Hash("coop_libros"));
            var libros = new List<GameObject>();
            foreach (var g in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                if (g.activeInHierarchy && Catalogo.BaseDe(g.name) == "Skill_book") libros.Add(g);
            libros.Sort((a, b) => string.CompareOrdinal(IdDe(a, Ancla()), IdDe(b, Ancla())));
            var cerca = new List<Vector3>();
            foreach (var t in lugaresCopias) if (t != null && t.name.StartsWith("coop_lugar_")) cerca.Add(t.transform.position);
            int puestos = 0, quitados = 0;
            foreach (var g in libros)
            {
                string id = IdDe(g, Ancla());
                if (recogidos.Contains(id)) { g.SetActive(false); quitados++; continue; }
                if (!Rearmable(g)) { Iluminar(g); objetos[id] = new Objeto { Id = id, Go = g, Ultima = g.transform.position, Libro = true }; continue; }   // malla que no se reconstruye bien: se queda donde está
                Vector3 p; float yaw;
                bool ok;
                // La mitad de las veces, junto a un lugar único (dentro de una cueva, al pie de la torre...).
                if (cerca.Count > 0 && rng.NextDouble() < 0.5)
                {
                    Vector3 c = cerca[rng.Next(cerca.Count)];
                    var r2 = new System.Random(rng.Next());
                    ok = UbicarCerca(r2, c, 25f, out p, out yaw);
                    if (!ok) ok = Ubicar(rng, null, 0f, out p, out yaw);
                }
                else ok = Ubicar(rng, null, 0f, out p, out yaw);
                if (ok)
                {
                    Vector3 nuevaP = p + Vector3.up * 0.15f; Quaternion nuevaR = Quaternion.Euler(0f, yaw, 0f);
                    var visual = VisualRearmado(g, nuevaP, nuevaR);
                    g.transform.position = nuevaP; g.transform.rotation = nuevaR; puestos++;
                    if (visual != null) visual.transform.SetParent(g.transform, true);
                }
                Iluminar(g);
                objetos[id] = new Objeto { Id = id, Go = g, Ultima = g.transform.position, Libro = true };
            }
            plugin.Log("Mundo: libros " + puestos + " colocados, " + quitados + " ya recogidos en esta partida");
        }

        bool UbicarCerca(System.Random rng, Vector3 centro, float radio, out Vector3 pos, out float yaw)
        {
            pos = Vector3.zero; yaw = 0f;
            for (int i = 0; i < 30; i++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f, r = (float)rng.NextDouble() * radio;
                Vector3 cand = centro + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                NavMeshHit nh;
                if (!NavMesh.SamplePosition(cand, out nh, 6f, NavMesh.AllAreas)) continue;
                if (nh.position.y < nivelAgua + 0.8f) continue;
                if (hayLlegada && (!NavMesh.CalculatePath(nhLlegada.position, nh.position, NavMesh.AllAreas, camino) || camino.status != NavMeshPathStatus.PathComplete)) continue;
                pos = nh.position; yaw = (float)rng.NextDouble() * 360f;
                return true;
            }
            return false;
        }

        static void Iluminar(GameObject libro)
        {
            if (libro.transform.Find("coop_luz_libro") != null) return;
            var go = new GameObject("coop_luz_libro");
            go.transform.SetParent(libro.transform, false);
            go.transform.localPosition = Vector3.up * 0.6f;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point; l.color = new Color(0.92f, 0.95f, 1f); l.range = 5f; l.intensity = 1.6f; l.shadows = LightShadows.None;
        }

        static string IdDe(GameObject g, Vector3 ancla)
        {
            Vector3 rel = g.transform.position - ancla;
            return g.name + "@" + Mathf.RoundToInt(rel.x * 10) + "," + Mathf.RoundToInt(rel.y * 10) + "," + Mathf.RoundToInt(rel.z * 10);
        }

        static HashSet<string> LibrosRecogidos()
        {
            var h = new HashSet<string>();
            string s = "";
            try { s = DialogueLua.GetVariable(VarLibrosRecogidos).AsString; } catch { }
            if (!string.IsNullOrEmpty(s) && s != "nil") foreach (var x in s.Split('|')) if (x.Length > 0) h.Add(x);
            return h;
        }

        static void AnotarLibro(string id)
        {
            var h = LibrosRecogidos();
            if (!h.Add(id)) return;
            DialogueLua.SetVariable(VarLibrosRecogidos, string.Join("|", new List<string>(h).ToArray()));
        }

        /// El compañero recogió un libro: aquí también cuenta (mismo evento que manda el libro del juego).
        void LibroDelCompanero(string id)
        {
            AnotarLibro(id);
            PlayMakerFSM.BroadcastEvent("SkillBook");
            plugin.Log("Mundo: libro de " + plugin.NombreCompanero + " también para mí (" + id + ")");
        }
    }
}

// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.AI;

namespace WitchHuntCoop
{
    /// Relleno del bosque, distinto en CADA ENTRADA (semilla de la cacería):
    ///  - Árboles: cada uno se corre unos metros (lejos del agua, la cabaña y los lugares únicos).
    ///  - Estructuras de relleno: la original se mueve a un sitio de relieve parecido y se agregan
    ///    dos copias más (×3), con el mismo criterio para que tengan sentido donde salen.
    ///  - Botín (monedas, oro, balas, ganzúas, plantas): ×5 con copias funcionales.
    ///  - Cofres: ×10. Los nuevos son del mod: al abrirlos (E) sueltan un saco de monedas o una caja
    ///    de balas del propio juego (85 %) o están vacíos (15 %). Se sincroniza con el compañero.
    public partial class Mundo
    {
        public const int CopiasRelleno = 2;     // original + 2 = ×3
        public const int CopiasBotin = 4;       // original + 4 = ×5
        public const int CopiasOro = 9;         // oro: original + 9 = ×10
        static bool EsOro(string tipo) { return tipo == "Sack_of_coins" || tipo == "Gold_Nugget" || tipo.StartsWith("Golden") || tipo.StartsWith("Goldidol") || tipo.EndsWith("_gold") || tipo == "silverbars_pickable"; }
        public const int FactorCofres = 10;
        /// Cofres por dificultad (pedido 2026-11-17): ×25 en Normal, ×15 en Difícil y ×10 en Pesadilla (antes ×10 dividido por la dificultad).
        public static int FactorCofresDificultad { get { int d = Opciones.Dificultad; return d == 1 ? 15 : d >= 2 ? 10 : 25; } }
        static readonly string[] Pequenos = { "Bronzevase_pickable", "Dinamicvase_pickable", "Bronzestatuette_pickable", "Bronzeplate_pickable", "Gabled_pickable" };
        static bool EsPequeno(string tipo) { return System.Array.IndexOf(Pequenos, tipo) >= 0; }

        /// Sitios con algo (casas, cabañas, minas, piedras de guardado, puestos, cofres) donde sí puede haber oro.
        List<Vector3> Anclas()
        {
            var l = new List<Vector3>();
            foreach (var c in casas) if (c.Raiz != null) l.Add(c.Raiz.transform.position);
            foreach (var c in cabanas) if (c.Raiz != null) l.Add(c.Raiz.transform.position);
            foreach (var m in minas) if (m.Boca != null) l.Add(m.Boca.transform.position);
            foreach (var g in piedras) if (g != null) l.Add(g.transform.position);
            foreach (var pu in puestos) l.Add(pu.Pos);
            foreach (var c in cofres) if (c.Go != null) l.Add(c.Go.transform.position);
            return l;
        }
        public const float DesplazamientoArbol = 30f;

        /// Dificultad: el botín repartido se divide entre 1,5 (Difícil) o 4 (Pesadilla). Los originales del juego se quedan.
        public static float DivisorBotin { get { int d = Opciones.Dificultad; return d == 1 ? 1.5f : d >= 2 ? 4f : 1f; } }
        /// Copias extra de un objeto que se reparte ×(1+copias), reducidas por la dificultad (parte fraccionaria al azar, igual en los dos juegos).
        static int CopiasSegunDificultad(int copias, System.Random rng)
        {
            float dv = DivisorBotin; if (dv <= 1f) return copias;
            float extra = Mathf.Max(0f, (1 + copias) / dv - 1f);
            int n = (int)extra;
            if (rng.NextDouble() < extra - n) n++;
            return n;
        }

        static readonly string[] NoRelleno = {
            "cool_bridge", "fishnets", "Cube", "Rock8", "InvinsibleShadowCaster", "Crow", "!!!!HumanScale2m",
            "GameObject", "Sign_2_Bellville", "Map", "Door_locked", "Breakable Window", "Floor Gate", "GoodDog",
            "SpiderWeb", "SpuderWeb", "German_Shepherd", "gate_unlockable", "Dead_man", "Paper_safe", "thirdeyedoc", "Chest", "Book_parrent",
        };

        public int RellenoMovidos, RellenoCopias, BotinCopias, CofresNuevos, ArbolesMovidos;
        readonly HashSet<Transform> enLugares = new HashSet<Transform>();   // ya colocados como lugar único

        // ---------------- Estructuras de relleno ----------------

        void ColocarRelleno(int semilla)
        {
            RellenoMovidos = RellenoCopias = 0;
            var rng = new System.Random(semilla ^ Hash("coop_relleno"));
            Vector3 an = Ancla();
            var raices = new List<GameObject>(UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects());
            raices.RemoveAll(g => g == null || !g.activeInHierarchy);
            raices.Sort((a, b) => { int c = string.CompareOrdinal(a.name, b.name); return c != 0 ? c : PosInicial(a.transform).x.CompareTo(PosInicial(b.transform).x); });
            // x, z, radio: todo lo ya colocado (lugares únicos, casas, cabañas, minas, piedras) con su tamaño real
            var ocupado = Huellas();
            rellenoMovido.Clear();
            int nCopiasFallidas = 0;
            foreach (var g in raices)
            {
                if (enLugares.Contains(g.transform)) continue;
                string b = Catalogo.BaseDe(g.name);
                if (Array.IndexOf(Unicos, b) >= 0) continue;
                bool fuera = false;
                foreach (var n in NoArrastrar) if (g.name.Contains(n)) { fuera = true; break; }
                if (!fuera) foreach (var n in NoRelleno) if (b.StartsWith(n)) { fuera = true; break; }
                if (fuera || Array.IndexOf(Tipos, b) >= 0) continue;
                if (g.GetComponentInChildren<FPSPlayer>() != null || g.GetComponentInChildren<Camera>() != null) continue;
                Bounds caja; if (!CajaDe(new List<GameObject> { g }, out caja)) continue;
                caja.center += PosInicial(g.transform) - g.transform.position;
                float tam = Mathf.Max(caja.size.x, caja.size.z);
                if (tam < 0.3f || tam > 60f || caja.center.y > 250f) continue;
                if (Vector3.Distance(Plano(caja.center), Plano(cabana)) < RadioCabana + 10f) continue;
                int hTot, eTot; Coop.Piezas.Calidad(g, out hTot, out eTot);
                if (eTot < hTot) continue;   // no se puede rearmar bien: se queda donde está

                var l = new Lugar { Nombre = g.name, Caja = caja };
                l.Raices.Add(g.transform);
                l.Yaw0 = RotInicial(g.transform).eulerAngles.y;
                float suelo = AlturaEn(caja.center);
                l.Pivote = new Vector3(caja.center.x, float.IsNaN(suelo) ? caja.min.y : suelo, caja.center.z);
                l.Radio = new Vector2(caja.extents.x, caja.extents.z).magnitude;
                TomarRelieve(l, tam < 6f ? 3 : 5);

                // La original y sus copias: cada una en un sitio de relieve parecido.
                for (int k = 0; k <= CopiasRelleno; k++)
                {
                    Vector3 p; float giro;
                    if (!SitioRelleno(l, rng, ocupado, out p, out giro)) { if (k > 0) nCopiasFallidas++; continue; }
                    Quaternion rot = Quaternion.Euler(0f, giro - l.Yaw0, 0f);
                    Vector3 pos = p + rot * (PosInicial(g.transform) - l.Pivote);
                    Quaternion r = rot * RotInicial(g.transform);
                    if (k == 0)
                    {
                        l.Destino = p; l.GiroY = giro; l.Movido = true;
                        Trasladar(l);
                        rellenoMovido.AddRange(l.Raices);
                        RellenoMovidos++;
                    }
                    else
                    {
                        int faltan;
                        var copia = Coop.Piezas.Clonar(g, pos, r, out faltan);
                        copia.name = "coop_relleno_" + g.name;
                        foreach (var ps in copia.GetComponentsInChildren<ParticleSystem>(true)) UnityEngine.Object.Destroy(ps.gameObject == copia ? (UnityEngine.Object)ps : ps.gameObject);   // si las partículas están en la raíz, solo se quita el sistema (no el objeto entero)
                        lugaresCopias.Add(copia);
                        RellenoCopias++;
                    }
                    ocupado.Add(new Vector4(p.x, p.z, l.Radio, 0f));   // también los chicos: dos rellenos nunca en el mismo sitio
                }
            }
            plugin.Log("Relleno: " + RellenoMovidos + " estructuras movidas, " + RellenoCopias + " copias nuevas (" + nCopiasFallidas + " copias sin sitio)");
        }

        /// Igual que con los lugares únicos pero más liviano: sin camino a pie y con menos intentos.
        bool SitioRelleno(Lugar l, System.Random rng, List<Vector4> ocupado, out Vector3 pos, out float giro)
        {
            pos = Vector3.zero; giro = 0f;
            float tam = Mathf.Max(l.Caja.size.x, l.Caja.size.z);
            float tol = 0.5f + 0.06f * tam;
            float p0 = l.Pendiente.magnitude, rPend = Mathf.Max(tam * 0.35f, 1.5f);
            bool enLadera = p0 > 0.08f;
            Bounds zr = ZonaLugares();
            float mejor = float.MaxValue;
            for (int i = 0; i < 260; i++)
            {
                float x = zr.min.x + (float)rng.NextDouble() * zr.size.x;
                float z = zr.min.z + (float)rng.NextDouble() * zr.size.z;
                float g = (float)rng.NextDouble() * 360f;
                var c = new Vector3(x, 0f, z);
                if (Vector3.Distance(c, Plano(cabana)) < RadioCabana + l.Radio + 5f) continue;
                bool choca = false;
                foreach (var o in ocupado) if (new Vector2(o.x - x, o.y - z).magnitude < o.z + l.Radio + 3f) { choca = true; break; }
                if (choca) continue;
                if (enLadera)
                {
                    float hx1 = AlturaEn(c + Vector3.right * rPend), hx0 = AlturaEn(c - Vector3.right * rPend);
                    float hz1 = AlturaEn(c + Vector3.forward * rPend), hz0 = AlturaEn(c - Vector3.forward * rPend);
                    if (float.IsNaN(hx1 + hx0 + hz1 + hz0)) continue;
                    float gx = (hx1 - hx0) / (2f * rPend), gz = (hz1 - hz0) / (2f * rPend);
                    float pc = Mathf.Sqrt(gx * gx + gz * gz);
                    if (Mathf.Abs(pc - p0) > 0.4f * p0 + 0.05f) continue;
                    g = Rumbo(gx, gz) - Rumbo(l.Pendiente.x, l.Pendiente.y) + ((float)rng.NextDouble() - 0.5f) * 20f;
                }
                float err, baseY; bool agua;
                if (!Comparar(l, c, g, tol, out err, out baseY, out agua) || agua != l.Agua) continue;
                if (err < mejor) { mejor = err; pos = new Vector3(x, baseY, z); giro = g; }
                if (err < tol * 0.5f) break;
            }
            return mejor <= tol;
        }

        // ---------------- Botín ×5 ----------------

        /// Igual que MultiplicarBotin, pero repartido en fotogramas (unos 6 ms cada uno): mismo orden y mismas
        /// semillas, así que en los dos juegos salen las mismas copias.
        readonly Dictionary<string, int> fallosBotin = new Dictionary<string, int>();
        System.Collections.IEnumerator MultiplicarBotinGradual(int semilla)
        {
            int miSemilla = semillaAplicada;
            var reloj = System.Diagnostics.Stopwatch.StartNew();
            var pasos = MultiplicarBotinPasos(semilla).GetEnumerator();
            while (true)
            {
                if (miSemilla != semillaAplicada || plugin.Escena != EscenaBosque) yield break;   // se cambió de escena: se abandona
                reloj.Reset(); reloj.Start();
                bool sigue = true;
                while (reloj.ElapsedMilliseconds < 6)
                {
                    try { sigue = pasos.MoveNext(); } catch (Exception e) { plugin.Log("Botín: error " + e); yield break; }
                    if (!sigue) break;
                }
                if (!sigue) break;
                yield return null;
            }
        }

        System.Collections.Generic.IEnumerable<bool> MultiplicarBotinPasos(int semilla)
        {
            BotinCopias = 0;
            var rng = new System.Random(semilla ^ Hash("coop_botin_x5"));
            var originales = new List<Objeto>();
            foreach (var o in objetos.Values)
                if (!o.Libro && !o.Id.StartsWith("clon:") && o.Go != null && o.Go.activeInHierarchy && Vector3.Distance(o.Go.transform.position, cabana) >= RadioCabana) originales.Add(o);
            originales.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            var anclas = Anclas();
            // Mini granja del espantapájaros (junto al viaje rápido de la cabaña del lago): 8 plantas curativas
            var esp = GameObject.Find("scarecrow_gua");
            if (esp != null)
            {
                var rg = new System.Random(semilla ^ Hash("coop_granja_plantas"));
                for (int k = 0; k < 8; k++)
                {
                    float ang = (float)rg.NextDouble() * Mathf.PI * 2f, d = 2f + (float)rg.NextDouble() * 5f;
                    Vector3 p = esp.transform.position + new Vector3(Mathf.Cos(ang) * d, 0f, Mathf.Sin(ang) * d); RaycastHit hg;
                    if (Physics.Raycast(p + Vector3.up * 3f, Vector3.down, out hg, 8f, ~0, QueryTriggerInteraction.Ignore)) p = hg.point;
                    if (ClonarBotin("med_plant", p, (float)rg.NextDouble() * 360f, "granja:" + k) != null) BotinCopias++;
                    yield return true;
                }
            }
            Vector3 centroGrupo = Vector3.zero; int enGrupo = 0;
            foreach (var o in originales)
            {
                string tipo = TipoDeId(o.Id);
                int copias = CopiasSegunDificultad(EsOro(tipo) ? CopiasOro : CopiasBotin, rng);   // el oro, ×10 (pedido 2026-11-10); lo demás ×5; menos en Difícil/Pesadilla
                for (int k = 1; k <= copias; k++)
                {
                    Vector3 p; float yaw; bool ok;
                    if (EsOro(tipo))
                    {
                        // el oro nunca va suelto en el bosque: siempre junto a una casa, cabaña, mina, piedra, puesto o cofre
                        if (anclas.Count == 0) continue;
                        ok = UbicarEnTorno(rng, anclas[rng.Next(anclas.Count)], 7f, out p, out yaw);
                    }
                    else if (EsPequeno(tipo))
                    {
                        // los objetos pequeños (jarrones, platos, estatuillas) salen en grupos de 20, nunca sueltos
                        if (enGrupo <= 0) { Vector3 cg; float yg; if (!Ubicar(rng, null, 0f, out cg, out yg)) continue; centroGrupo = cg; enGrupo = 20; }
                        ok = UbicarEnTorno(rng, centroGrupo, 4f, out p, out yaw); enGrupo--;
                    }
                    else ok = Ubicar(rng, null, 0f, out p, out yaw, tipo == "med_plant" && rng.NextDouble() < 0.8);   // plantas curativas: 80 % a la orilla de los caminos
                    if (!ok) continue;
                    if (ClonarBotin(tipo, p, yaw, "x5:" + k + ":" + o.Id) != null) BotinCopias++; else { int nf; fallosBotin.TryGetValue(tipo, out nf); fallosBotin[tipo] = nf + 1; }
                    yield return true;
                }
            }
            var rf = new System.Text.StringBuilder(); foreach (var kv in fallosBotin) rf.Append(kv.Key).Append("=").Append(kv.Value).Append(" ");
            plugin.Log("Botín: huella=" + huella + " | " + originales.Count + " originales + " + BotinCopias + " copias; sin original utilizable: " + rf + " | intentos de ubicar: " + rUb[0] + " (agua " + rUb[1] + ", camino " + rUb[2] + ", sin navmesh " + rUb[3] + ", sin ruta " + rUb[4] + ", sin entrada norte " + rUb[5] + ")"); fallosBotin.Clear();
            // Herbolario (libro de cada jugador): el doble de plantas curativas, solo en el mundo de quien lo tiene.
            if (LibrosTienda.Tiene(LibrosTienda.Herbolario))
            {
                var rh = new System.Random(semilla ^ Hash("coop_herbolario"));
                int plantas = 0, extra = 0;
                foreach (var o in objetos.Values) if (TipoDeId(o.Id) == "med_plant" && o.Go != null) plantas++;
                for (int k = 0; k < plantas; k++)
                {
                    Vector3 p; float yaw;
                    if (Ubicar(rh, null, 0f, out p, out yaw) && ClonarBotin("med_plant", p, yaw, "herbolario:" + k) != null) extra++;
                    yield return true;
                }
                plugin.Log("Herbolario: " + extra + " plantas curativas más (había " + plantas + ")");
            }
        }

        // ---------------- Cofres ×10 ----------------

        class Cofre { public int N; public GameObject Go; public int Contenido; public bool Abierto; public Transform Tapa; public Vector3 Bisagra; public float Angulo; public Vector3 FrenteLocal = Vector3.forward; }   // 0 vacío, 1 oro, 2 balas
        readonly List<Cofre> cofres = new List<Cofre>();
        static readonly string[] ModelosCofre = { "Chest_static", "ChestParrent", "Chest_locked" };

        void ColocarCofres(int semilla)
        {
            foreach (var c in cofres) if (c.Go != null) UnityEngine.Object.Destroy(c.Go);
            cofres.Clear();
            CofresNuevos = 0;
            // Cofres del juego fuera de la cabaña: de ellos sale el modelo y la cuenta (×10).
            var originales = new List<GameObject>();
            foreach (var g in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                if (g.activeInHierarchy && Array.IndexOf(ModelosCofre, Catalogo.BaseDe(g.name)) >= 0 && Vector3.Distance(g.transform.position, cabana) > RadioCabana) originales.Add(g);
            originales.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            GameObject modelo = null;
            foreach (var g in originales) { int h, e; Coop.Piezas.Calidad(g, out h, out e); if (e == h) { modelo = g; break; } }
            if (modelo == null) { plugin.Log("Cofres: no hay un cofre que se pueda copiar"); return; }
            try { LeerAperturaJuego(originales); } catch (Exception e) { apertura = null; plugin.Log("Cofres: error al leer la apertura del juego " + e.Message); }
            int total = Mathf.Max(Mathf.Max(originales.Count, 5) * FactorCofresDificultad - originales.Count, 0);
            var rng = new System.Random(semilla ^ Hash("coop_cofres"));
            float sobre = 0f; RaycastHit hit;
            if (Physics.Raycast(modelo.transform.position + Vector3.up * 0.5f, Vector3.down, out hit, 5f, ~0, QueryTriggerInteraction.Ignore)) sobre = modelo.transform.position.y - hit.point.y;
            int vacios = 0;
            for (int i = 0; i < total; i++)
            {
                Vector3 p; float yaw;
                if (!Ubicar(rng, null, 0f, out p, out yaw)) continue;
                double tirada = rng.NextDouble();
                int contenido = tirada < 0.15 ? 0 : (tirada < 0.575 ? 1 : 2);
                if (contenido == 0) vacios++;
                int faltan;
                var go = Coop.Piezas.Clonar(modelo, p + Vector3.up * (sobre + 0.07f), Quaternion.Euler(0f, yaw, 0f), out faltan);   // +7 cm: no queda enterrado
                go.name = "coop_cofre_" + i;
                foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true)) UnityEngine.Object.Destroy(ps.gameObject == go ? (UnityEngine.Object)ps : ps.gameObject);   // si las partículas están en la raíz, solo se quita el sistema (no el objeto entero)
                var cofre = new Cofre { N = i, Go = go, Contenido = contenido };
                OrientarCofre(cofre);
                BuscarTapa(cofre);
                cofres.Add(cofre);
                CofresNuevos++;
            }
            // un cofre dentro de cada planta de las cabañas cerradas
            foreach (var cc in cofresDeCabana)
            {
                int faltan2; int i = cofres.Count;
                var go2 = Coop.Piezas.Clonar(modelo, cc.Pos + Vector3.up * (sobre + 0.04f), Quaternion.Euler(0f, cc.Yaw + 180f, 0f), out faltan2);
                if (go2 == null) continue;
                go2.name = "coop_cofre_" + i;
                foreach (var ps2 in go2.GetComponentsInChildren<ParticleSystem>(true)) UnityEngine.Object.Destroy(ps2.gameObject == go2 ? (UnityEngine.Object)ps2 : ps2.gameObject);
                var r2 = new System.Random(cc.Semilla ^ 0x2545F491);
                var cofre2 = new Cofre { N = i, Go = go2, Contenido = r2.NextDouble() < 0.5 ? 1 : 2 };   // nunca vacío
                OrientarCofre(cofre2);
                BuscarTapa(cofre2);
                cofres.Add(cofre2); CofresNuevos++;
            }
            if (cofres.Count > 0) plugin.Log("Cofres: el primero existe=" + (cofres[0].Go != null) + " activo=" + (cofres[0].Go != null && cofres[0].Go.activeInHierarchy) + " pos=" + (cofres[0].Go != null ? cofres[0].Go.transform.position.ToString("F0") : "-") + " hijos=" + (cofres[0].Go != null ? cofres[0].Go.transform.childCount : -1));
            plugin.Log("Cofres: huella=" + huella + " | " + originales.Count + " del juego + " + CofresNuevos + " nuevos (" + vacios + " vacíos), modelo " + modelo.name);
        }

        public string PiezasCofre()
        {
            Cofre primero = null; int nulos = 0;
            foreach (var c in cofres) { if (c.Go != null) { if (primero == null) primero = c; } else nulos++; }
            if (primero == null) return "sin cofres con objeto (" + cofres.Count + " en la lista, " + nulos + " sin objeto)";
            var sb = new System.Text.StringBuilder(primero.Go.name + " en " + (primero.Go.transform.position - Anclaje.Origen()).ToString("F0") + " (" + nulos + " sin objeto): ");
            foreach (var t in primero.Go.GetComponentsInChildren<Transform>(true))
            {
                var r = t.GetComponent<MeshRenderer>(); var mf = t.GetComponent<MeshFilter>();
                sb.Append("\n    ").Append(t == primero.Go.transform ? "(raíz)" : t.name).Append(" local=").Append(t.localPosition.ToString("F2"));
                if (r != null && mf != null && mf.sharedMesh != null) sb.Append(" malla=").Append(mf.sharedMesh.name).Append(" tam=").Append(r.bounds.size.ToString("F2")).Append(" centro=").Append((r.bounds.center - primero.Go.transform.position).ToString("F2"));
            }
            return sb.ToString();
        }

        /// La tapa es la pieza con malla cuyo centro está más alto; la bisagra, su borde de atrás (contrario al frente del cofre).
        void BuscarTapa(Cofre c)
        {
            // la tapa que gira el juego (chest_cap_save), si se conoce
            var tj = apertura != null ? Hijo(c.Go.transform, apertura.Tapa) : null;
            if (tj != null && tj.GetComponent<MeshRenderer>() != null)
            {
                var rj = tj.GetComponent<MeshRenderer>();
                Vector3 f0 = c.Go.transform.forward; f0.y = 0f; f0.Normalize();
                float m0 = Mathf.Abs(Vector3.Dot(rj.bounds.extents, new Vector3(Mathf.Abs(f0.x), 0f, Mathf.Abs(f0.z))));
                c.Tapa = tj; c.Bisagra = rj.bounds.center - f0 * m0 - Vector3.up * rj.bounds.extents.y * 0.6f;
                return;
            }
            Transform mejor = null; float alto = float.MinValue; int piezas = 0;
            foreach (var r in c.Go.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (r.transform == c.Go.transform) continue;
                piezas++;
                if (r.bounds.center.y > alto) { alto = r.bounds.center.y; mejor = r.transform; }
            }
            if (piezas < 2 || mejor == null) return;
            var rm = mejor.GetComponent<MeshRenderer>();
            Vector3 fw = c.Go.transform.forward; fw.y = 0f; fw.Normalize();
            float medio = Mathf.Abs(Vector3.Dot(rm.bounds.extents, new Vector3(Mathf.Abs(fw.x), 0f, Mathf.Abs(fw.z))));
            c.Tapa = mejor; c.Bisagra = rm.bounds.center - fw * medio - Vector3.up * rm.bounds.extents.y * 0.6f;
        }

        const float GradosTapa = 85f, SegundosTapa = 0.7f;   // los del juego (iTweenRotateAdd del cofre); solo si no se pudo usar AbrirComoJuego
        void AnimarTapas()
        {
            foreach (var c in cofres)
            {
                if (!c.Abierto || c.Tapa == null || c.Angulo >= GradosTapa) continue;
                float paso = Mathf.Min(GradosTapa - c.Angulo, GradosTapa / SegundosTapa * Time.deltaTime);
                c.Tapa.RotateAround(c.Bisagra, c.Go.transform.right, -paso);   // se levanta hacia atrás
                c.Angulo += paso;
            }
        }

        Cofre cofreCerca;

        float ultimoDiagCofres; bool cofresPerdidos;
        void ActualizarCofres()
        {
            AnimarTapas();
            if (!cofresPerdidos && cofres.Count > 0 && Time.realtimeSinceStartup - ultimoDiagCofres > 2f)
            {
                ultimoDiagCofres = Time.realtimeSinceStartup;
                int nulos = 0; foreach (var c0 in cofres) if (c0.Go == null) nulos++;
                if (nulos > 0) { cofresPerdidos = true; plugin.Log("Cofres: " + nulos + " de " + cofres.Count + " se destruyeron (" + (Time.realtimeSinceStartup - llegada).ToString("0.0") + " s tras llegar)"); }
            }
            cofreCerca = null;
            var j = plugin.Jugador;
            if (j == null || cofres.Count == 0) return;
            float mejor = 2.6f;
            foreach (var c in cofres)
            {
                if (c.Abierto || c.Go == null) continue;
                float d = Vector3.Distance(j.transform.position, c.Go.transform.position);
                if (d < mejor && !DeFrente(c, j.transform.position)) continue;   // solo de frente, por el lado de la cerradura (pedido 2026-11-19)
                if (d < mejor) { mejor = d; cofreCerca = c; }
            }
            if (cofreCerca != null && Controles.Interactuar() && Entrada.Enfocado) Forzar(cofreCerca);
        }

        public const float RomperGanzua = 0.35f;
        /// Probabilidad de abrir con una ganzúa: 65 %; el libro Llave maestra la sube un 100 % (×2, tope 100 %:
        /// la ganzúa ya no se rompe). Pedido 2026-11-17.
        /// 2026-11-18 (pedido): sin nada 35 %; con la habilidad del juego «Maestro con ganzúas» 65 %; con la Llave maestra 100 %
        /// (tenga o no la habilidad). La habilidad del juego se lee de su variable global UnlockPercentage (35 → 65).
        public static float ExitoGanzua
        {
            get
            {
                if (LibrosTienda.Tiene(LibrosTienda.LlaveMaestra)) return 1f;
                int pct = 35;
                try { var v = HutongGames.PlayMaker.FsmVariables.GlobalVariables.FindFsmInt("UnlockPercentage"); if (v != null && v.Value > 0 && v.Value < 100) pct = v.Value; } catch { }
                return Mathf.Clamp(pct, 35, 65) / 100f;
            }
        }
        public static string TextoForzar(int ganzuas) { return "E: forzar la cerradura (" + Mathf.RoundToInt(ExitoGanzua * 100f) + " % de abrir · ganzúas: " + ganzuas + ")"; }
        public const int MaxGanzuas = 9999;

        public static HutongGames.PlayMaker.FsmInt Ganzuas()
        {
            var g = HutongGames.PlayMaker.FsmVariables.GlobalVariables;
            return g == null ? null : g.FindFsmInt("Lockpicks");
        }

        /// Forzar la cerradura con una ganzúa: si falla (según ExitoGanzua) la ganzúa se rompe y sigue cerrado.
        void Forzar(Cofre c)
        {
            if (c.Abierto) return;
            if (LibrosTienda.Tiene(LibrosTienda.LlaveMaestra)) { AbrirCofre(c, true); return; }   // la llave maestra abre sin ganzúas
            var gz = Ganzuas();
            if (gz == null || gz.Value <= 0) { Viaje.Avisar("Necesitas una ganzúa."); return; }
            if (azar.NextDouble() >= ExitoGanzua)
            {
                gz.Value = gz.Value - 1;
                Viaje.Avisar("La ganzúa se rompió. (Te quedan " + gz.Value + ")");
                plugin.Log("Cofre " + c.N + ": ganzúa rota");
                return;
            }
            AbrirCofre(c, true);
        }

        void AbrirCofre(Cofre c, bool mio)
        {
            if (c.Abierto) return;
            c.Abierto = true;
            bool comoJuego = false;
            try { comoJuego = AbrirComoJuego(c); } catch (Exception e) { plugin.Log("Cofre " + c.N + ": apertura del juego falló: " + e.Message); }
            string que = c.Contenido == 0 ? "vacío" : (c.Contenido == 1 ? "un saco de monedas" : "balas");
            if (c.Contenido != 0)
            {
                // el botín queda DENTRO del cofre: sobre el fondo, en el centro del cuerpo (sin contar la tapa)
                Vector3 frente = c.Go.transform.position;
                Bounds cb = new Bounds(); bool hayCb = false;
                foreach (var rr in c.Go.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (c.Tapa != null && (rr.transform == c.Tapa || rr.transform.IsChildOf(c.Tapa))) continue;
                    if (!hayCb) { cb = rr.bounds; hayCb = true; } else cb.Encapsulate(rr.bounds);
                }
                if (hayCb) frente = new Vector3(cb.center.x, cb.min.y + cb.size.y * 0.3f, cb.center.z);
                // la colisión del cuerpo del cofre envolvía el botín y el rayo de "recoger" chocaba con ella (no se podía tomar):
                // se quitan las que lo tapan y queda solo un fondo sólido. (2026-11-19: ANTES de crear el botín; si no,
                // al asentarlo caía sobre la tapa aún cerrada o quedaba metido en el cuerpo: la caja de balas no se veía.)
                int quitadas = 0;
                foreach (var col in c.Go.GetComponentsInChildren<Collider>(true))
                {
                    if (col.isTrigger || !col.enabled) continue;
                    if (c.Tapa != null && (col.transform == c.Tapa || col.transform.IsChildOf(c.Tapa))) { col.enabled = false; quitadas++; continue; }   // la tapa se va a abrir
                    var bb = col.bounds; bb.Expand(0.1f);
                    if (bb.Contains(frente) || bb.Contains(frente + Vector3.up * 0.25f)) { col.enabled = false; quitadas++; }
                }
                float piso = hayCb ? cb.min.y + 0.09f : frente.y;
                if (quitadas > 0 && hayCb)
                {
                    var fondo = new GameObject("coop_cofre_fondo"); fondo.transform.SetParent(c.Go.transform, true);
                    fondo.transform.position = new Vector3(cb.center.x, cb.min.y + 0.04f, cb.center.z); fondo.transform.rotation = c.Go.transform.rotation;
                    var bx = fondo.AddComponent<BoxCollider>(); bx.size = new Vector3(0.6f, 0.08f, 0.4f);
                }
                var botin = ClonarBotin(c.Contenido == 1 ? "Sack_of_coins" : "Ammo_Silverbullets", frente, c.Go.transform.eulerAngles.y, "cofre:" + c.N);
                if (botin != null && hayCb)
                {
                    // apoyado justo sobre el fondo, en el centro del cofre (no a la altura del original ni dejado caer)
                    Bounds bbot = new Bounds(); bool hayB = false;
                    foreach (var r in botin.GetComponentsInChildren<Renderer>(false))
                    {
                        if (!r.enabled || r is ParticleSystemRenderer) continue;
                        if (!hayB) { bbot = r.bounds; hayB = true; } else bbot.Encapsulate(r.bounds);
                    }
                    if (hayB) botin.transform.position += new Vector3(cb.center.x - bbot.center.x, piso - bbot.min.y, cb.center.z - bbot.center.z);
                    var rbB = botin.GetComponent<Rigidbody>(); if (rbB != null) rbB.isKinematic = true;
                }
                if (botin == null) plugin.Log("Cofre " + c.N + ": no pude crear " + que + " (no hay un original que copiar)");
            }
            if (mio)
            {
                int s = semillaAplicada, n = c.N;
                var datos = Red.Armar(w => { w.Write(s); w.Write(n); });
                for (int i = 0; i < 2; i++) plugin.EnviarCofre(datos);
                Viaje.Avisar("Abriste el cofre.");   // lo que tenga cae al suelo: es sorpresa
            }
            plugin.Log("Cofre " + c.N + " abierto por " + (mio ? "mí" : plugin.NombreCompanero) + ": " + que + (comoJuego ? " (animación del juego)" : " (giro a mano)"));
        }

        public void RecibirCofre(BinaryReader r)
        {
            int s = r.ReadInt32(), n = r.ReadInt32();
            if (s != semillaAplicada) return;
            foreach (var c in cofres) if (c.N == n) { AbrirCofre(c, false); break; }
        }

        public void DibujarCofre()
        {
            if (cofreCerca == null || plugin.Jugador == null) return;
            if (estiloCofre == null)
            {
                estiloCofre = new GUIStyle(GUI.skin.label) { fontSize = Opciones.T(20), alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
                estiloCofre.normal.textColor = new Color(1f, 0.9f, 0.6f);
            }
            var gz = Ganzuas();
            int n = gz != null ? gz.Value : 0;
            GUI.Label(new Rect(0, Screen.height * 0.6f, Screen.width, 30f), LibrosTienda.Tiene(LibrosTienda.LlaveMaestra) ? "E: abrir con la llave maestra" : n > 0 ? TextoForzar(n) : "Necesitas una ganzúa", estiloCofre);
        }
        GUIStyle estiloCofre;

        public string DescribirCofres()
        {
            int ab = 0, v = 0, o = 0, b = 0;
            foreach (var c in cofres) { if (c.Abierto) ab++; if (c.Contenido == 0) v++; else if (c.Contenido == 1) o++; else b++; }
            string cerca = "";
            if (plugin.Jugador != null && cofres.Count > 0)
            {
                Cofre m = null; float dm = 1e9f;
                foreach (var c in cofres) if (c.Go != null) { float d = Vector3.Distance(c.Go.transform.position, plugin.Jugador.transform.position); if (d < dm) { dm = d; m = c; } }
                if (m != null) cerca = " | más cercano: n.º " + m.N + " a " + Mathf.RoundToInt(dm) + " m en " + (m.Go.transform.position - Ancla()).ToString("F0");
            }
            return cofres.Count + " cofres nuevos: " + o + " con oro, " + b + " con balas, " + v + " vacíos; abiertos " + ab + cerca;
        }

        public void OrdenForzar(int n) { foreach (var c in cofres) if (c.N == n) { Forzar(c); return; } }
        public void OrdenAbrirCofre(int n) { foreach (var c in cofres) if (c.N == n) { AbrirCofre(c, true); return; } }
        public Vector3? PosCofre(int n) { foreach (var c in cofres) if (c.N == n && c.Go != null) return c.Go.transform.position - Ancla(); return null; }

        // ---------------- Árboles ----------------

        static readonly Dictionary<TerrainData, TreeInstance[]> arbolesOriginales = new Dictionary<TerrainData, TreeInstance[]>();

        /// Unity conserva los árboles cambiados entre cargas: al salir del bosque se devuelven.
        static void RestaurarArboles()
        {
            foreach (var kv in arbolesOriginales) if (kv.Key != null) kv.Key.treeInstances = kv.Value;
        }

        void MoverArboles(int semilla)
        {
            ArbolesMovidos = 0;
            var t = Anclaje.Principal();
            if (t == null) return;
            var td = t.terrainData;
            TreeInstance[] orig;
            if (!arbolesOriginales.TryGetValue(td, out orig)) { orig = td.treeInstances; arbolesOriginales[td] = orig; }
            var nuevos = (TreeInstance[])orig.Clone();
            Vector3 o = t.transform.position, s = td.size;
            // Nada de árboles dentro de lo colocado: si el sitio nuevo (o el original) cae en una huella,
            // se prueba otro; si no hay, ese árbol se quita en esta entrada.
            var evitar = Huellas();
            evitar.RemoveAll(e => e.z < 2f);   // solo estructuras: junto a una roca o un tronco un árbol está bien
            var quedan = new List<TreeInstance>(nuevos.Length);
            ArbolesQuitados = 0;
            int rCamino = 0, rBorde = 0, rCabana = 0, rChoca = 0, rAgua = 0, rPendiente = 0, origChoca = 0;
            float mayor = 0f; Vector4 laMayor = Vector4.zero;
            foreach (var e in evitar) if (e.z > mayor) { mayor = e.z; laMayor = e; }
            for (int i = 0; i < nuevos.Length; i++)
            {
                var rng = new System.Random(semilla ^ (i * 7919));
                Vector3 w0 = o + Vector3.Scale(orig[i].position, s);
                if (EnPasoNorte(w0.x - o.x, w0.z - o.z)) { ArbolesQuitados++; continue; }   // el paso a la zona nueva queda despejado de árboles (pedido 2026-11-13)
                if (Vector3.Distance(Plano(w0), Plano(cabana)) < RadioCabana + 15f) { quedan.Add(nuevos[i]); continue; }
                bool puesto = false;
                for (int intento = 0; intento < 6 && !puesto; intento++)
                {
                    float a = (float)rng.NextDouble() * Mathf.PI * 2f, r = 4f + (float)rng.NextDouble() * (DesplazamientoArbol - 4f);
                    Vector3 w = w0 + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                    if (w.x < o.x + 5f || w.z < o.z + 5f || w.x > o.x + s.x - 5f || w.z > o.z + s.z - 5f) { rBorde++; continue; }
                    if (Vector3.Distance(Plano(w), Plano(cabana)) < RadioCabana + 15f) { rCabana++; continue; }
                    if (Choca(evitar, w.x, w.z, 0.8f, 4.5f)) { rChoca++; continue; }
                    float h = t.SampleHeight(w) + o.y;
                    if (h < nivelAgua + 1.2f) { rAgua++; continue; }
                    Vector3 nrm = td.GetInterpolatedNormal((w.x - o.x) / s.x, (w.z - o.z) / s.z);
                    if (nrm.y < 0.8f) { rPendiente++; continue; }   // nada de árboles en barrancos
                    if (EsCamino(w)) { rCamino++; continue; }          // ni en los caminos
                    nuevos[i].position = new Vector3((w.x - o.x) / s.x, (h - o.y) / s.y, (w.z - o.z) / s.z);
                    ArbolesMovidos++; puesto = true;
                }
                if (!puesto && EsCamino(w0)) { ArbolesQuitados++; continue; }   // un árbol original sobre el camino se quita
                if (!puesto && Choca(evitar, w0.x, w0.z, 0.8f, 4.5f)) { ArbolesQuitados++; origChoca++; continue; }   // se quedaría dentro de algo
                quedan.Add(nuevos[i]);
            }
            nuevos = quedan.ToArray();
            td.treeInstances = nuevos;
            t.Flush();
            var col = t.GetComponent<TerrainCollider>();
            if (col != null) { col.enabled = false; col.enabled = true; }   // que las colisiones sigan a los árboles
            plugin.Log("Árboles: " + ArbolesMovidos + " de " + orig.Length + " corridos, " + ArbolesQuitados + " quitados por caer dentro de una estructura (" + evitar.Count + " huellas; intentos rechazados: borde " + rBorde + ", cabaña " + rCabana + ", estructura " + rChoca + ", agua " + rAgua + " (nivel " + nivelAgua.ToString("0.0") + "), pendiente " + rPendiente + "; mayor huella " + mayor.ToString("0") + " m en " + new Vector2(laMayor.x, laMayor.y).ToString("F0") + "; terreno en " + o.ToString("F0") + " tamaño " + s.ToString("F0") + ")");
        }
    }
}

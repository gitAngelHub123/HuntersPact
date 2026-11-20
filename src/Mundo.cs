// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.AI;

namespace WitchHuntCoop
{
    /// Bosque distinto en cada cacería (solo en cooperativo):
    ///  - El anfitrión elige una semilla al llegar al bosque y se la manda al invitado.
    ///  - Botín, munición, ganzúas, hechizos (libros, tótems) y plantas se colocan en puntos al
    ///    azar transitables y alcanzables a pie desde la cabaña. La zona de la cabaña (80 m) no se toca.
    ///  - Cada objeto tiene un id estable (tipo + posición original) y su propio generador
    ///    (semilla ⊕ id): los dos juegos calculan lo mismo aunque a uno le falte algún objeto.
    ///  - Lo que recoge uno desaparece en el juego del otro.
    public partial class Mundo
    {
        public const int EscenaBosque = 8;
        // Cabaña única con el teletransporte, relativa al terreno (ver notas): fija.
        public static readonly Vector3 CabanaRel = new Vector3(1120f, 18f, 988f);
        public static readonly Vector3 LlegadaRel = new Vector3(1092f, 14f, 1051f);
        public const float RadioCabana = 100f;   // nada se mueve ni se genera a menos de 100 m de la llegada (pedido 2026-11-10)

        static readonly string[] Tipos = {
            "Ammo_Silverbullets", "Bronzeplate_pickable", "Bronzestatuette_pickable", "Bronzevase_pickable",
            "Dinamicvase_pickable", "Gabled_pickable", "Gabled_pickable_gold", "Gold_Nugget",
            "Goldenfigure_pickable", "Goldenplate_pickable", "Goldidol_pickable", "Lockpicks_pack",
            "Sack_of_coins", "silverbars_pickable", "magic_totem", "med_plant",
        };

        public static string[] TiposBotin { get { return Tipos; } }
        class Objeto { public string Id; public GameObject Go; public Vector3 Ultima; public bool Libro; }

        readonly Plugin plugin;
        public int Semilla;            // 0 = sin aplicar
        int semillaAplicada;
        bool restaurarPendiente;
        int escenaVista = -1;
        float llegada, aplicadoEn; int handleVista = int.MinValue;
        Texture2D negro;

        /// Pantalla negra mientras se arma el mundo (así no se ven aparecer árboles ni hierba) y un fundido suave al terminar.
        public void DibujarCarga()
        {
            if (plugin.Escena != EscenaBosque || !plugin.Conectado || !Guardado.ModoCoop || plugin.Jugador == null) return;
            float ahora = Time.realtimeSinceStartup, a;
            if (semillaAplicada == 0) { if (ahora - llegada > 25f) return; a = 1f; }   // por si algo falla: nunca se queda negro
            else { a = 1f - (ahora - aplicadoEn) / 0.9f; if (a <= 0f) return; }
            if (negro == null) { negro = new Texture2D(1, 1); negro.SetPixel(0, 0, Color.black); negro.Apply(); }
            var prev = GUI.color; GUI.depth = -900; GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(a));
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), negro);
            GUI.color = prev;
        }
        float ultimoEnvio, ultimaRevision;
        readonly Dictionary<string, Objeto> objetos = new Dictionary<string, Objeto>();
        readonly System.Random azar = new System.Random();
        public int Movidos, Fijos, SinSitio;

        // Contexto de colocación (se prepara en Aplicar).
        public readonly int[] rUb = new int[6];   // intentos de Ubicar: total, agua, camino, sin navmesh, sin ruta, sin entrada norte
        public float NivelAguaActual { get { return nivelAgua; } }
        Vector3 cabana; Bounds zona; NavMeshHit nhLlegada; bool hayLlegada; float nivelAgua = -1000f;
        readonly NavMeshPath camino = new NavMeshPath();

        // Puntos de guardado extra del cooperativo.
        public const int PuntosGuardado = 8;   // 8 piedras extra (4 de antes + 4 pedidas el 2026-11-10)
        public const float SeparacionGuardado = 250f;
        const string PiedraModelo = "save_stone_witch (1)";
        readonly List<GameObject> piedras = new List<GameObject>();

        public Mundo(Plugin p) { plugin = p; }

        // ---------------- Bucle ----------------

        public void Actualizar()
        {
            int escena = plugin.Escena;
            int handle = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetHashCode();   // cambia también al recargar la misma escena
            if (escena != escenaVista || handle != handleVista)
            {
                escenaVista = escena; handleVista = handle;
                objetos.Clear(); semillaAplicada = 0;
                QuitarExpansion(); semillaExpansion = 0;
                restaurarPendiente = true;
                llegada = Time.realtimeSinceStartup;
                lugaresCopias.Clear(); copiaOriginal.Clear();
                cofres.Clear(); casas.Clear(); minas.Clear(); cabanas.Clear(); matRoca = null;
                RestaurarArboles();
                if (escena == EscenaBosque) FotografiarInicio();
                // Nueva cacería: el anfitrión elige semilla nueva (la de partida sale de su guardado).
                if (escena == EscenaBosque && plugin.EsAnfitrion) { Semilla = azar.Next(1, int.MaxValue); PrepararSemillaPartida(); }
            }
            // Unity conserva en memoria los datos del terreno entre cargas: si una cacería coop abrió
            // el paso norte, se devuelve la forma original al volver a entrar (también jugando solo).
            // La marca sigue hasta encontrar el terreno con respaldo (en la aldea no lo hay).
            if (restaurarPendiente && semillaExpansion == 0 && Anclaje.Principal() != null)
            {
                float[,] alturas;
                var td = Anclaje.Principal().terrainData;
                if (td != null && respaldo.TryGetValue(td, out alturas))
                {
                    restaurarPendiente = false;
                    td.SetHeights(0, 0, alturas);
                    RefrescarPasto(td, 0f, 0f, 1e9f, 1e9f); RefrescarHierbaATG(Anclaje.Principal());   // si no, la hierba se queda a la altura del paso
                    plugin.Log("Expansión: terreno original restaurado");
                }
            }
            if (escena != EscenaBosque || plugin.Jugador == null || !plugin.Conectado) return;
            float t = Time.realtimeSinceStartup;

            // El anfitrión repite la semilla (UDP) mientras están en el bosque.
            if (plugin.EsAnfitrion && t - ultimoEnvio > 1f)
            {
                ultimoEnvio = t;
                if (SemillaPartida == 0) PrepararSemillaPartida();
                int s = Semilla, sp = SemillaPartida;
                plugin.EnviarMundo(Red.Armar(w => { w.Write(EscenaBosque); w.Write(s); w.Write(sp); }));
            }
            // Esperar a que la escena se asiente (carga de objetos persistentes) antes de mover.
            ActualizarExpansion();
            if (Semilla != 0 && SemillaPartida != 0 && semillaAplicada != Semilla && t - llegada > 1.5f && MapaListo(t) && PrepararExpansion(SemillaPartida)) { var reloj = System.Diagnostics.Stopwatch.StartNew(); Aplicar(Semilla); aplicadoEn = Time.realtimeSinceStartup; plugin.Log("Mundo: armado en " + reloj.ElapsedMilliseconds + " ms (" + (aplicadoEn - llegada).ToString("0.0") + " s desde la llegada)"); }

            if (semillaAplicada != 0) { ActualizarCofres(); ActualizarCasas(); ActualizarMinas(); ActualizarTesoro(); ActualizarVisibilidadCastillo(); ActualizarCabanas(); }
            if (semillaAplicada != 0 && t - ultimaRevision > 0.3f) { ultimaRevision = t; RevisarRecogidos(); ActualizarGuardianes(); }
            if (semillaAplicada != 0) ActualizarRefuerzos();
            if (semillaAplicada != 0) ActualizarCongelados();
            Oyente.Asegurar(plugin);   // (2026-11-19) un solo oyente de audio: el de la cámara del jugador
            SonidosLejanos.Actualizar();   // (2026-11-19) pausa los bucles 3D cortos (cofres, plantas) fuera de su alcance
            if (semillaAplicada != 0) ActualizarRescate();
            if (semillaAplicada != 0) { ActualizarFoso(); ActualizarEnemigosLaberinto(); ActualizarEnemigosMinas(); }
        }

        public void RecibirSemilla(BinaryReader r)
        {
            int escena = r.ReadInt32(), s = r.ReadInt32();
            int sp = r.BaseStream.Position < r.BaseStream.Length ? r.ReadInt32() : 0;
            if (plugin.EsAnfitrion || escena != plugin.Escena) return;
            if (sp != 0 && sp != SemillaPartida) { SemillaPartida = sp; plugin.Log("Mundo: semilla de partida " + sp); }
            if (s != Semilla) { Semilla = s; plugin.Log("Mundo: semilla de la cacería " + s); }
        }

        // ---------------- Mallas horneadas ----------------

        public int VisualesRearmados;

        /// Si la malla del objeto está dentro de un lote estático del juego, al moverlo la malla se queda en el
        /// sitio viejo (el objeto parece desaparecer: se ve su luz o se puede recoger "en el aire"). Se hace una
        /// copia visual en el sitio nuevo y se apaga la parte horneada. Llamar ANTES de mover el objeto.
        /// ¿Se puede mover este objeto y que se siga viendo bien? Sin malla horneada, sí. Con ella, solo si cada pieza se
        /// reconstruye con una malla suelta que cuadra en tamaño y posición (si no, la planta saldría invisible o con la
        /// forma de otro objeto: una vasija dorada en vez de una planta). Lo que no cuadra se queda donde está.
        public int NoRearmables;
        bool Rearmable(GameObject g)
        {
            int h, e;
            Coop.Piezas.Calidad(g, out h, out e);
            if (h == 0 || e == h) return true;
            NoRearmables++;
            return false;
        }

        GameObject VisualRearmado(GameObject g, Vector3 nuevaPos, Quaternion nuevaRot)
        {
            var origR = g.GetComponentsInChildren<MeshRenderer>(true);
            bool horneado = false;
            foreach (var r in origR) if (r.isPartOfStaticBatch) { horneado = true; break; }
            if (!horneado) return null;
            int faltan;
            var copia = Coop.Piezas.Clonar(g, nuevaPos, nuevaRot, out faltan);
            if (copia == null) return null;
            copia.name = "coop_visual_" + g.name;
            var copR = copia.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < origR.Length && i < copR.Length; i++) if (!origR[i].isPartOfStaticBatch) copR[i].enabled = false;   // lo no horneado lo sigue dibujando el original
            foreach (var c in copia.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.Destroy(c);
            foreach (var l in copia.GetComponentsInChildren<Light>(true)) l.enabled = false;
            foreach (var ps in copia.GetComponentsInChildren<ParticleSystem>(true)) UnityEngine.Object.Destroy(ps.gameObject == copia ? (UnityEngine.Object)ps : ps.gameObject);
            foreach (var r in origR) if (r.isPartOfStaticBatch) r.enabled = false;
            VisualesRearmados++;
            return copia;
        }

        // ---------------- Caminos ----------------

        /// ¿Es camino el suelo de este punto? Los caminos son la capa 0 (Dirt_01) del terreno y sus bordes la 4 (Dirt_Rocks).
        public static bool EsCamino(Vector3 w)
        {
            foreach (var t in Terrain.activeTerrains)
            {
                var td = t.terrainData; if (td == null) continue;
                Vector3 o = t.transform.position, sz = td.size;
                if (w.x < o.x || w.z < o.z || w.x > o.x + sz.x || w.z > o.z + sz.z) continue;
                int n = td.alphamapLayers; if (n < 5) return false;
                int ax = Mathf.Clamp((int)((w.x - o.x) / sz.x * td.alphamapWidth), 0, td.alphamapWidth - 1);
                int az = Mathf.Clamp((int)((w.z - o.z) / sz.z * td.alphamapHeight), 0, td.alphamapHeight - 1);
                var a = td.GetAlphamaps(ax, az, 1, 1);
                return a[0, 0, 0] > 0.5f || a[0, 0, 4] > 0.45f;
            }
            return false;
        }

        // ---------------- Colocación ----------------

        System.Diagnostics.Stopwatch cr; long ultimo; System.Text.StringBuilder tiempos = new System.Text.StringBuilder();
        void T(string n) { long x = cr.ElapsedMilliseconds; tiempos.Append(n).Append(' ').Append(x - ultimo).Append(" ms, "); ultimo = x; }

        bool esperandoMapa;
        /// El reparto depende del mapa de caminos (rutas desde la llegada). Si un juego carga más lento y empieza sin él,
        /// salta las comprobaciones y su mundo queda distinto al del compañero: se espera a que exista (máximo 25 s).
        bool MapaListo(float t)
        {
            Vector3 llegadaMundo = Ancla() + LlegadaRel; NavMeshHit nh;
            bool listo = NavMesh.SamplePosition(llegadaMundo, out nh, 20f, NavMesh.AllAreas) && !float.IsNaN(AlturaEn(llegadaMundo));
            if (listo || t - llegada > 25f)
            {
                if (esperandoMapa) plugin.Log("Mundo: " + (listo ? "el mapa de caminos ya está disponible" : "¡se acabó la espera sin mapa de caminos!") + " (" + (t - llegada).ToString("0.0") + " s desde la llegada)");
                esperandoMapa = false; return true;
            }
            if (!esperandoMapa) { esperandoMapa = true; plugin.Log("Mundo: esperando el mapa de caminos antes de repartir el botín"); }
            return false;
        }

        void Aplicar(int semilla)
        {
            semillaAplicada = semilla; cr = System.Diagnostics.Stopwatch.StartNew(); ultimo = 0;
            objetos.Clear(); huella = 0;
            Movidos = Fijos = SinSitio = 0; VisualesRearmados = 0; NoRearmables = 0;
            Vector3 ancla = Ancla();
            cabana = CabanaRel + ancla;
            zona = ZonaJugable(ancla);
            hayLlegada = NavMesh.SamplePosition(LlegadaRel + ancla, out nhLlegada, 20f, NavMesh.AllAreas);
            nivelAgua = NivelAgua();
            // Una vez por partida: lugares únicos, piedras de guardado extra y libros.
            try { ColocarLugares(SemillaPartida); ApagarLucesHuerfanas(); QuitarRocasFlotantesLugares(); T("lugares"); } catch (Exception e) { plugin.Log("Lugar: error " + e); }
            ColocarPiedras(SemillaPartida); T("piedras");
            try { ColocarLibros(SemillaPartida); T("libros"); } catch (Exception e) { plugin.Log("Mundo: error en libros " + e); }
            try { ColocarCasasEscopeta(SemillaPartida); T("casas"); } catch (Exception e) { plugin.Log("Casas: error " + e); }
            try { ColocarMinas(SemillaPartida); T("minas"); } catch (Exception e) { plugin.Log("Minas: error " + e); }
            try { ColocarTesoroCastillo(SemillaPartida); T("tesoro"); } catch (Exception e) { plugin.Log("Tesoro: error " + e); }
            try { ColocarCabanasCerradas(SemillaPartida); T("cabañas"); } catch (Exception e) { plugin.Log("Cabañas: error " + e); }

            foreach (var go in Candidatos())
            {
                Vector3 rel = go.transform.position - ancla;
                string id = go.name + "@" + Mathf.RoundToInt(rel.x * 10) + "," + Mathf.RoundToInt(rel.y * 10) + "," + Mathf.RoundToInt(rel.z * 10);
                if (objetos.ContainsKey(id)) continue;
                objetos[id] = new Objeto { Id = id, Go = go, Ultima = go.transform.position };
                if (Vector3.Distance(go.transform.position, cabana) < RadioCabana) { Fijos++; continue; }

                // Altura del objeto sobre el suelo, para conservarla en el sitio nuevo.
                float sobreSuelo = 0.05f;
                RaycastHit h;
                if (Physics.Raycast(go.transform.position + Vector3.up * 0.5f, Vector3.down, out h, 5f, ~0, QueryTriggerInteraction.Ignore))
                    sobreSuelo = Mathf.Clamp(go.transform.position.y - h.point.y, 0f, 1.5f);

                if (!Rearmable(go)) { Fijos++; continue; }   // no se puede reconstruir bien: se queda en su sitio
                { string tp = TipoDeId(id); if (EsOro(tp) || EsPequeno(tp)) { Fijos++; continue; } }   // oro y objetos pequeños: nunca sueltos en el bosque, se quedan donde el juego los puso (con su casa o estructura)
                var rng = new System.Random(semilla ^ Hash(id));
                Vector3 p; float yaw;
                bool puesto = Ubicar(rng, null, 0f, out p, out yaw, TipoDeId(id) == "med_plant" && rng.NextDouble() < 0.8);   // plantas curativas: 80 % a la orilla de los caminos
                if (puesto)
                {
                    Vector3 nuevaP = p + Vector3.up * sobreSuelo;
                    Quaternion nuevaR = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(go.transform.eulerAngles.x, 0f, go.transform.eulerAngles.z);
                    var visual = VisualRearmado(go, nuevaP, nuevaR);
                    go.transform.position = nuevaP;
                    go.transform.rotation = nuevaR;
                    if (visual != null) visual.transform.SetParent(go.transform, true);
                    objetos[id].Ultima = go.transform.position;
                }
                if (puesto) Movidos++; else SinSitio++;
            }
            plugin.StartCoroutine(MultiplicarBotinGradual(semilla));   // las copias del botín se crean poco a poco (2,5 s de golpe se notaban)
            try { ColocarRelleno(semilla); T("relleno"); } catch (Exception e) { plugin.Log("Relleno: error " + e); }
            try { ColocarDecoracionCastillo(); T("decoración"); } catch (Exception e) { plugin.Log("Castillo: error en la decoración " + e); }
            try { ColocarCofres(semilla); T("cofres"); } catch (Exception e) { plugin.Log("Cofres: error " + e); }
            CrearPuestos(semilla);
            try { /* granja eliminada (pedido 2026-11-12) */ ColocarRuinas(SemillaPartida); T("ruinas"); } catch (Exception e) { plugin.Log("Construcciones: error " + e); } T("puestos");
            try { OroSueltoBoveda(); } catch (Exception e) { plugin.Log("Tesoro: error con el oro suelto " + e.Message); }
            try { CampamentosEnLucesSueltas(); T("luces"); } catch (Exception e) { plugin.Log("Luces: error " + e); }
            try { HacerSolidos(); T("solidez"); } catch (Exception e) { plugin.Log("Solidez: error " + e.Message); }
            try { MoverArboles(semilla); T("árboles"); } catch (Exception e) { plugin.Log("Árboles: error " + e); }
            try { DespejarArbolesEstructuras(); T("despejar"); } catch (Exception e) { plugin.Log("Despejar: error " + e); }
            try { LimpiezaFinal(); T("limpieza"); } catch (Exception e) { plugin.Log("Limpieza final: error " + e); }
            try { CombinarLotes(); T("lotes"); } catch (Exception e) { plugin.Log("Lotes: error " + e.Message); }
            try { plugin.mapa.Regenerar(nuevo != null, nuevo != null ? nuevo.transform.position.z + FondoNuevo : 0f); T("mapa"); } catch (Exception e) { plugin.Log("Mapa: error " + e); }
            plugin.Log("Mundo: " + VisualesRearmados + " objetos con malla horneada rearmados, " + NoRearmables + " se quedan en su sitio por no poder reconstruirse; tiempos: " + tiempos); tiempos.Length = 0;
            plugin.Log("Mundo: diagnóstico → dificultad " + Opciones.Dificultad + ", hayLlegada=" + hayLlegada + ", entradaNorte=" + HayEntradaNorte + ", agua=" + nivelAgua.ToString("0.00") + ", expansión=" + (nuevo != null) + ", huella hasta aquí=" + huella);
            plugin.Log("Mundo: semilla " + semilla + " → " + Movidos + " objetos movidos, " + Fijos + " fijos (cabaña), " + SinSitio + " sin sitio");
        }

        /// Busca un punto al azar transitable, alcanzable a pie desde la llegada, fuera de la zona
        /// de la cabaña y (si se pide) a más de "separacion" de los puntos dados. Determinista
        /// para un mismo generador.
        /// A 1–4 m de un camino (pero no encima).
        static bool OrillaDeCamino(Vector3 c)
        {
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f;
                foreach (float d in new[] { 2f, 4f })
                    if (EsCamino(c + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d))) return true;
            }
            return false;
        }

        /// Punto válido a 1,5–radio m de un centro (mismas reglas que Ubicar: suelo seco, fuera de caminos, con ruta a pie).
        bool UbicarEnTorno(System.Random rng, Vector3 centro, float radio, out Vector3 pos, out float yaw)
        {
            pos = Vector3.zero; yaw = 0f;
            for (int intento = 0; intento < 15; intento++)
            {
                float ang = (float)rng.NextDouble() * Mathf.PI * 2f, d = 1.5f + (float)rng.NextDouble() * Mathf.Max(0.1f, radio - 1.5f);
                yaw = (float)rng.NextDouble() * 360f;
                Vector3 cand = centro + new Vector3(Mathf.Cos(ang) * d, 0f, Mathf.Sin(ang) * d);
                cand.y = AlturaEn(cand);
                if (float.IsNaN(cand.y) || cand.y < nivelAgua + 0.8f) continue;
                if (Vector3.Distance(cand, cabana) < RadioCabana + 5f) continue;
                if (EsCamino(cand) || EnCastillo(cand)) continue;
                NavMeshHit nh;
                if (!NavMesh.SamplePosition(cand, out nh, 6f, NavMesh.AllAreas)) continue;
                bool enNorte = nuevo != null && nh.position.z > nuevo.transform.position.z;
                if (enNorte && !HayEntradaNorte) continue;
                Vector3 origen = enNorte ? EntradaNorte : nhLlegada.position;
                if ((hayLlegada || enNorte) && (!NavMesh.CalculatePath(origen, nh.position, NavMesh.AllAreas, camino) || camino.status != NavMeshPathStatus.PathComplete)) continue;
                Vector3 suelo = nh.position; RaycastHit h;
                if (Physics.Raycast(suelo + Vector3.up * 2f, Vector3.down, out h, 6f, ~0, QueryTriggerInteraction.Ignore)) suelo = h.point;
                pos = suelo;
                unchecked { huella = huella * 31 + (int)(suelo.x * 10f) * 7 + (int)(suelo.z * 10f); }
                return true;
            }
            return false;
        }

        public long huella;   // diagnóstico: resumen de todo lo colocado (debe ser igual en los dos juegos)
        bool Ubicar(System.Random rng, List<Vector3> lejosDe, float separacion, out Vector3 pos, out float yaw, bool orilla = false)
        {
            pos = Vector3.zero; yaw = 0f;
            // Primero la zona, según su superficie (bosque original o expansión norte); luego el punto.
            Bounds zr = zona;
            if (nuevo != null)
            {
                Vector3 o = Ancla();
                var zOrig = new Bounds(o + new Vector3(1000f, 50f, 1000f), new Vector3(1760f, 200f, 1760f));
                var zNueva = new Bounds(nuevo.transform.position + new Vector3(AnchoNuevo / 2f, 50f, FondoNuevo / 2f), new Vector3(AnchoNuevo - 2f * Pared, 200f, FondoNuevo - Pared));
                float pn = (zNueva.size.x * zNueva.size.z) / (zNueva.size.x * zNueva.size.z + zOrig.size.x * zOrig.size.z);
                zr = rng.NextDouble() < pn ? zNueva : zOrig;
            }
            for (int intento = 0; intento < 60; intento++)
            {
                float x = zr.min.x + (float)rng.NextDouble() * zr.size.x;
                float z = zr.min.z + (float)rng.NextDouble() * zr.size.z;
                yaw = (float)rng.NextDouble() * 360f;
                Vector3 cand = new Vector3(x, zona.center.y, z);
                cand.y = AlturaEn(cand);
                rUb[0]++;
                if (float.IsNaN(cand.y) || cand.y < nivelAgua + 0.8f) { rUb[1]++; continue; }   // nada bajo el agua helada
                if (Vector3.Distance(cand, cabana) < RadioCabana + 5f) continue;
                if (EnCastillo(cand)) continue;   // el castillo y su explanada no se tocan
                if (EsCamino(cand)) { rUb[2]++; continue; }   // nada en los caminos (pedido 2026-11-10)
                if (orilla && !OrillaDeCamino(cand)) continue;   // plantas curativas: justo al lado del camino
                if (lejosDe != null)
                {
                    bool cerca = false;
                    foreach (var q in lejosDe) if (Vector3.Distance(cand, q) < separacion) { cerca = true; break; }
                    if (cerca) continue;
                }
                NavMeshHit nh;
                if (!NavMesh.SamplePosition(cand, out nh, 12f, NavMesh.AllAreas)) { rUb[3]++; continue; }
                // En la zona nueva se valida desde la entrada norte (ver Expansion.EntradaNorte).
                bool enNorte = nuevo != null && nh.position.z > nuevo.transform.position.z;
                if (enNorte && !HayEntradaNorte) { rUb[5]++; continue; }
                Vector3 origen = enNorte ? EntradaNorte : nhLlegada.position;
                if ((hayLlegada || enNorte) && (!NavMesh.CalculatePath(origen, nh.position, NavMesh.AllAreas, camino) || camino.status != NavMeshPathStatus.PathComplete)) { rUb[4]++; continue; }
                Vector3 suelo = nh.position;
                RaycastHit h;
                if (Physics.Raycast(suelo + Vector3.up * 2f, Vector3.down, out h, 6f, ~0, QueryTriggerInteraction.Ignore)) suelo = h.point;
                pos = suelo;
                unchecked { huella = huella * 31 + (int)(suelo.x * 10f) * 7 + (int)(suelo.z * 10f); }
                return true;
            }
            return false;
        }

        /// 4 piedras de guardado clonadas de una del juego, repartidas lejos unas de otras
        /// (y de las piedras originales).
        void ColocarPiedras(int semilla)
        {
            foreach (var g in piedras) if (g != null) UnityEngine.Object.Destroy(g);
            piedras.Clear();
            GameObject modelo = null;
            var ocupados = new List<Vector3>();
            foreach (var g in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (!g.scene.IsValid() || !g.name.StartsWith("save_stone_witch") || g.transform.parent != null) continue;
                ocupados.Add(g.transform.position);
                if (g.name == PiedraModelo) modelo = g;
            }
            if (modelo == null) { plugin.Log("Mundo: no encontré la piedra de guardado modelo"); return; }
            float sobreSuelo = 0f;
            RaycastHit h;
            if (Physics.Raycast(modelo.transform.position + Vector3.up * 0.3f, Vector3.down, out h, 5f, ~0, QueryTriggerInteraction.Ignore))
                sobreSuelo = Mathf.Clamp(modelo.transform.position.y - h.point.y, -1f, 1.5f);
            var rng = new System.Random(semilla ^ Hash("coop_guardado"));
            for (int i = 0; i < PuntosGuardado; i++)
            {
                Vector3 p; float yaw;
                float sep = SeparacionGuardado;
                bool ok = Ubicar(rng, ocupados, sep, out p, out yaw);
                if (!ok) ok = Ubicar(rng, ocupados, sep * 0.5f, out p, out yaw);   // mapa apretado: más cerca
                if (!ok) { plugin.Log("Mundo: sin sitio para la piedra de guardado " + (i + 1)); continue; }
                var copia = (GameObject)UnityEngine.Object.Instantiate(modelo, p + Vector3.up * sobreSuelo, Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(modelo.transform.eulerAngles.x, 0f, modelo.transform.eulerAngles.z));
                copia.name = "coop_save_stone_" + (i + 1);
                copia.SetActive(true);
                piedras.Add(copia);
                ocupados.Add(p);
                plugin.Log("Mundo: piedra de guardado " + (i + 1) + " en " + (p - Ancla()));
            }
        }

        public string Piedras()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var g in piedras) if (g != null) sb.Append(g.name).Append(" ").Append(g.transform.position - Ancla()).Append(g.activeInHierarchy ? "" : " (inactiva)").Append("; ");
            return sb.ToString();
        }

        /// Objetos a repartir: los recogibles activos de los tipos de la lista.
        static List<GameObject> Candidatos()
        {
            var lista = new List<GameObject>();
            var vistos = new HashSet<GameObject>();
            foreach (var c in UnityEngine.Object.FindObjectsOfType<Collider>())
            {
                if (c.GetComponent("PickupButton") == null && c.GetComponent<HealthPickup>() == null) continue;
                Transform t = c.transform;
                if ((t.name.StartsWith("!") || t.name == "interact") && t.parent != null) t = t.parent;
                string tipo = t.name; int i = tipo.IndexOf(" ("); if (i > 0) tipo = tipo.Substring(0, i);
                if (Array.IndexOf(Tipos, tipo) < 0) continue;
                if (t.position.sqrMagnitude < 25f) continue;   // reservas del juego en el origen
                if (vistos.Add(t.gameObject)) lista.Add(t.gameObject);
            }
            // Orden estable (no importa para el resultado, pero el registro sale igual en ambos).
            lista.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return lista;
        }

        /// Rectángulo jugable: los terrenos, recortando el borde (montañas del límite).
        static Bounds ZonaJugable(Vector3 ancla)
        {
            var ts = Terrain.activeTerrains;
            Bounds b = new Bounds(ancla + new Vector3(1000f, 30f, 1000f), new Vector3(1800f, 200f, 1800f));
            bool primero = true;
            foreach (var t in ts)
            {
                if (t == null || t.terrainData == null) continue;
                var tb = new Bounds(t.transform.position + t.terrainData.size * 0.5f, t.terrainData.size);
                if (primero) { b = tb; primero = false; } else b.Encapsulate(tb);
            }
            b.Expand(new Vector3(-120f, 0f, -120f));
            return b;
        }

        static int Hash(string s)
        {
            unchecked
            {
                uint h = 2166136261;
                foreach (char ch in s) { h ^= ch; h *= 16777619; }
                return (int)h;
            }
        }

        static Vector3 Ancla()
        {
            return Anclaje.Origen();
        }

        // ---------------- Recogidos ----------------

        void RevisarRecogidos()
        {
            var j = plugin.Jugador;
            if (j == null) return;
            List<string> fuera = null;
            foreach (var o in objetos.Values)
            {
                bool vivo = o.Go != null && o.Go.activeInHierarchy;
                if (vivo) { o.Ultima = o.Go.transform.position; continue; }
                // Desapareció cerca de mí: lo recogí yo (si está lejos puede ser otra cosa del juego).
                if (Vector3.Distance(j.transform.position, o.Ultima) < 6f)
                {
                    (fuera ?? (fuera = new List<string>())).Add(o.Id);
                    if (o.Libro) AnotarLibro(o.Id);
                    string id = o.Id; int s = semillaAplicada;
                    var datos = Red.Armar(w => { w.Write(s); w.Write(id); });
                    for (int i = 0; i < 2; i++) plugin.EnviarRecogido(datos);
                    plugin.Log("Mundo: recogí " + o.Id);
                }
            }
            if (fuera != null) foreach (var id in fuera) objetos.Remove(id);
        }

        public void RecibirRecogido(BinaryReader r)
        {
            int s = r.ReadInt32(); string id = r.ReadString();
            Objeto o;
            if (s != semillaAplicada || !objetos.TryGetValue(id, out o)) return;
            objetos.Remove(id);
            if (o.Go != null) o.Go.SetActive(false);
            if (o.Libro) LibroDelCompanero(id);
            plugin.Log("Mundo: " + plugin.NombreCompanero + " recogió " + id);
        }

        public string Describir()
        {
            int norte = 0; Vector3 o = Ancla();
            foreach (var x in objetos.Values) if (x.Go != null && x.Go.transform.position.z - o.z > 2000f) norte++;
            return "partida=" + SemillaPartida + " lugares movidos=" + LugaresMovidos + " fijos=" + LugaresFijos + " semilla=" + Semilla + " aplicada=" + semillaAplicada + " objetos=" + objetos.Count + " movidos=" + Movidos + " fijos=" + Fijos + " sinSitio=" + SinSitio + " en zona norte=" + norte;
        }

        /// Para pruebas: posición actual de un objeto por tipo (el primero que encuentre).
        public string Donde(string tipo)
        {
            var sb = new System.Text.StringBuilder();
            int n = 0;
            foreach (var o in objetos.Values)
                if (o.Id.StartsWith(tipo) && o.Go != null && n++ < 5) sb.Append(o.Id).Append(" → ").Append(o.Go.transform.position - Ancla()).Append("; ");
            return sb.ToString();
        }
    }
}

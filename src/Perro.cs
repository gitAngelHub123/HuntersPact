// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using System.Collections.Generic;
using System.IO;
using PixelCrushers.DialogueSystem;
using UnityEngine;
using UnityEngine.AI;

namespace WitchHuntCoop
{
    /// Perro compañero del cooperativo.
    ///  - Se libera abriendo la reja de su celda (la del juego). Desde ese momento queda "liberado".
    ///  - Al volver al pueblo pasa a ser compañero, y desde la siguiente entrada al bosque va con
    ///    los dos (estado guardado en la partida de cada uno: variable Lua coop_perro).
    ///  - Lo mueve el anfitrión: sigue a un jugador; si se separan elige a uno al azar (50 %) y no
    ///    cambia hasta que vuelvan a juntarse. Caza enemigos cercanos, nunca jefes. El invitado lo ve
    ///    con lo que le manda el anfitrión.
    public class Perro
    {
        public const string VarEstado = "coop_perro";   // 0 en la celda, 1 liberado, 2 compañero
        const float Separados = 45f, Juntos = 30f, RadioCaza = 25f, Mordisco = 2.4f, DanoMordisco = 30f;
        const float VelocidadCarrera = 15.4f;   // 2,2 veces la de antes (7 m/s)
        const float IntervaloMordisco = 0.55f;  // 2 veces más rápido (antes 1,1 s)
        bool creado; int modoVoz; AudioSource voz; float proxMuerde = -1f; bool jadeando;
        // seguimiento de movimiento de cada enemigo: solo se persigue a los que de verdad se mueven (no a los quietos que no atacan)
        readonly Dictionary<int, Vector3> refPos = new Dictionary<int, Vector3>(); readonly Dictionary<int, float> refT = new Dictionary<int, float>(), movioT = new Dictionary<int, float>();

        readonly Plugin plugin;
        GameObject perro; NavMeshAgent agente; Animator anim; Animation animL;
        int escenaVista = -1;
        bool intentado;
        float proxEstado;
        float proximaRevision, proximoEnvio, proximoMordisco, proximoDiag, atascadoDesde = -1f;
        int seguido = -1;           // -1 juntos, 0 anfitrión, 1 invitado
        Transform presa; Vector3 destinoCaido; float sinCaminoDesde = -1f, velCaido, proxLogCaido, proxDestino; float levantaDesde = -1f, caidoDesde = -1f, sinAvance, ultimaDist; const float TiempoLevantarPerro = 3f, VelocidadAuxilio = 32f;
        bool mordiendo;
        // invitado
        Vector3 posRemota; float yawRemoto, velRemota; bool hayRemoto;
        readonly System.Random azar = new System.Random();

        public Perro(Plugin p) { plugin = p; }

        public static int Estado
        {
            get { try { return DialogueLua.GetVariable(VarEstado).AsInt; } catch { return 0; } }
            set { DialogueLua.SetVariable(VarEstado, value); }
        }

        public void Actualizar(bool conectado)
        {
            int esc = plugin.Escena;
            if (esc != escenaVista)
            {
                escenaVista = esc; intentado = false; perro = null; agente = null; presa = null; seguido = -1; hayRemoto = false;
                if (esc == 7 && Estado == 1) { Estado = 2; plugin.Log("Perro: ahora es compañero (desde la próxima cacería)"); }
            }
            if (esc != Mundo.EscenaBosque || plugin.Jugador == null || !conectado) return;
            float t = Time.realtimeSinceStartup;

            if (Estado == 0 && t > proximaRevision) { proximaRevision = t + 1f; VigilarCelda(); }
            if (plugin.EsAnfitrion && t > proxEstado) { proxEstado = t + 2f; int e = Estado; plugin.EnviarPerro(Red.Armar(w => { w.Write((byte)2); w.Write((byte)e); })); }
            if (Estado != 2) return;
            if (creado && perro == null) { creado = false; intentado = false; plugin.Log("Perro: el modelo desapareció (recarga de escena): se vuelve a crear"); }
            if (perro == null && !intentado && Time.timeSinceLevelLoad > 6f) { intentado = true; Crear(); creado = perro != null; }
            if (perro == null) return;

            if (plugin.EsAnfitrion) Mover(t);
            else MoverRemoto();
        }

        // ---------------- Celda ----------------

        /// La reja abierta: su cadena (con persistencia) desaparece o su lógica pasa a "abierta".
        void VigilarCelda()
        {
            GameObject reja = null;
            foreach (var g in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                if (g.name == "gate_unlockable") { reja = g; break; }
            if (reja == null) return;
            bool abierta = reja.transform.Find("chaine") == null;
            foreach (var f in reja.GetComponents<PlayMakerFSM>())
                if (f.FsmName == "Interact" && (f.ActiveStateName == "State 2" || f.ActiveStateName == "State 5" || f.ActiveStateName == "State 6" || f.ActiveStateName == "State 7")) abierta = true;
            if (!abierta) return;
            Liberar(true);
        }

        void Liberar(bool mio)
        {
            if (Estado != 0) return;
            Estado = 1;
            plugin.Log("Perro: liberado" + (mio ? "" : " por " + plugin.NombreCompanero));
            Viaje.Avisar("El perro está libre. Los acompañará desde la próxima cacería (después de pasar por el pueblo).");
            if (mio) for (int i = 0; i < 3; i++) plugin.EnviarPerro(Red.Armar(w => w.Write((byte)0)));
        }

        public void OrdenLiberar() { Liberar(true); }
        public void OrdenCompanero() { Estado = 2; plugin.Log("Perro: estado = compañero"); }

        // ---------------- Modelo ----------------

        void Crear()
        {
            GameObject modelo = null, guia = null;
            foreach (var g in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (g.name.StartsWith("German_Shepherd")) modelo = g;
                if (g.name == "GoodDog") guia = g;
            }
            // El modelo animado es el que lleva dentro el perro del juego (GoodDog): el Animator con controlador que tenga la malla
            // en su jerarquía (el de la celda suelto es una pose fija, tirado en el suelo).
            GameObject enCelda = modelo; string eleccion = modelo != null ? "raíz " + modelo.name : "ninguno";
            if (guia != null)
            {
                Animator mejor = null;
                foreach (var an in guia.GetComponentsInChildren<Animator>(true))
                {
                    if (an.runtimeAnimatorController == null) continue;
                    bool malla = an.GetComponentInChildren<SkinnedMeshRenderer>(true) != null;
                    if (!malla) continue;
                    if (mejor == null || an.name.StartsWith("German_Shepherd_walk")) mejor = an;
                }
                if (mejor != null) { modelo = mejor.gameObject; eleccion = "animador " + modelo.name + " de GoodDog"; }
                else eleccion += " (GoodDog sin Animator con malla)";
            }
            plugin.Log("Perro: modelo elegido = " + eleccion);
            if (modelo == null) { plugin.Log("Perro: no encontré el modelo del perro"); return; }
            Vector3 inicio = plugin.Jugador.transform.position - plugin.Jugador.transform.forward * 3f;
            NavMeshHit nh;
            if (NavMesh.SamplePosition(inicio, out nh, 10f, NavMesh.AllAreas)) inicio = nh.position;
            perro = (GameObject)Object.Instantiate(modelo, inicio, Quaternion.identity);
            perro.transform.SetParent(null, true);
            perro.transform.localScale = modelo.transform.lossyScale;
            perro.name = "coop_perro";
            foreach (var mb in perro.GetComponentsInChildren<MonoBehaviour>(true)) Object.Destroy(mb);
            foreach (var c in perro.GetComponentsInChildren<Collider>(true)) c.enabled = false;
            foreach (var rb in perro.GetComponentsInChildren<Rigidbody>(true)) rb.isKinematic = true;
            QuitarMoscas(perro);
            perro.SetActive(true);
            // El modelo venía de una parte apagada: se encienden el animador y lo que dibuja.
            foreach (var r in perro.GetComponentsInChildren<Renderer>(true))
                for (Transform x = r.transform; x != null && x != perro.transform.parent; x = x.parent) x.gameObject.SetActive(true);
            anim = perro.GetComponentInChildren<Animator>(true);
            animL = perro.GetComponentInChildren<Animation>(true);
            if (anim != null) { for (Transform x = anim.transform; x != null; x = x.parent) x.gameObject.SetActive(true); anim.enabled = true; anim.cullingMode = AnimatorCullingMode.AlwaysAnimate; anim.applyRootMotion = false; }
            if (animL != null) animL.enabled = true;
            // sonidos: se quitan los del modelo del juego (el lloriqueo en bucle) y se pone una fuente propia
            foreach (var asr in perro.GetComponentsInChildren<AudioSource>(true)) { plugin.Log("Perro: audio del modelo quitado: " + (asr.clip != null ? asr.clip.name : "(sin clip)")); asr.Stop(); Object.Destroy(asr); }
            voz = perro.AddComponent<AudioSource>(); voz.spatialBlend = 1f; voz.minDistance = 3f; voz.maxDistance = 45f; voz.rolloffMode = AudioRolloffMode.Linear; voz.playOnAwake = false;
            if (plugin.EsAnfitrion)
            {
                agente = perro.GetComponent<NavMeshAgent>() ?? perro.AddComponent<NavMeshAgent>();
                if (agente == null) { plugin.Log("Perro: no pude darle movimiento"); return; }
                agente.enabled = true;
                agente.speed = VelocidadCarrera; agente.acceleration = 55f; agente.angularSpeed = 720f; agente.stoppingDistance = 2f; agente.radius = 0.4f; agente.height = 1f;
                agente.Warp(inicio);
            }
            if (!plugin.EsAnfitrion) { var ag = perro.GetComponent<NavMeshAgent>(); if (ag != null) ag.enabled = false; }
            // Ya no está en la celda (ni su guía del juego).
            if (enCelda != null) enCelda.SetActive(false);
            if (guia != null) guia.SetActive(false);
            plugin.Log("Perro: compañero en " + (inicio - Anclaje.Origen()).ToString("F0") + " (animación: " + (anim != null ? "Animator" : animL != null ? "Animation" : "ninguna") + ")");
        }

        /// El perro de la celda lleva moscas (objetos "Bug_…" y sus contenedores) dando vueltas: se quitan del compañero.
        static bool EsMosca(string n) { n = n.ToLowerInvariant(); return n.StartsWith("bug") || n.Contains("fly") || n.Contains("flies") || n.Contains("mosca") || n.Contains("insect"); }
        void QuitarMoscas(GameObject raiz)
        {
            var borrar = new List<GameObject>();
            foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
            {
                if (t == raiz.transform || !EsMosca(t.name)) continue;
                // el contenedor más alto con nombre de mosca (si el padre ya es de moscas, se borra ese)
                Transform top = t; while (top.parent != null && top.parent != raiz.transform && EsMosca(top.parent.name)) top = top.parent;
                if (!borrar.Contains(top.gameObject)) borrar.Add(top.gameObject);
            }
            // sistemas de partículas sueltos (nubes de insectos) también
            foreach (var ps in raiz.GetComponentsInChildren<ParticleSystem>(true)) if (!borrar.Contains(ps.gameObject) && ps.gameObject != raiz) borrar.Add(ps.gameObject);
            foreach (var g in borrar) { plugin.Log("Perro: quitado '" + g.name + "' (moscas)"); Object.Destroy(g); }
            if (borrar.Count == 0) plugin.Log("Perro: no encontré moscas en el modelo");
        }

        // ---------------- Anfitrión: seguir y cazar ----------------

        void Mover(float t)
        {
            if (agente == null) return;
            Vector3 yo = plugin.Jugador.transform.position;
            if (!agente.isOnNavMesh)
            {
                // Fuera del mapa de caminos (nació en un sitio sin camino): junto al jugador.
                NavMeshHit nh0;
                if (NavMesh.SamplePosition(yo - plugin.Jugador.transform.forward * 3f, out nh0, 15f, NavMesh.AllAreas)) { agente.Warp(nh0.position); plugin.Log("Perro: llevado junto a su dueño"); }
                return;
            }
            var comp = plugin.CompaneroActual;
            bool hayOtro = comp != null && comp.Visible;
            Vector3 otro = hayOtro ? comp.PosicionMundo : yo;
            float sep = Vector3.Distance(yo, otro);
            if (!hayOtro) seguido = 0;
            else if (sep > Separados && seguido == -1) { seguido = azar.Next(2); plugin.Log("Perro: se separaron, sigue al " + (seguido == 0 ? "anfitrión" : "invitado")); }
            else if (sep < Juntos) seguido = -1;
            Vector3 dueno = seguido == 1 ? otro : seguido == 0 ? yo : (Vector3.Distance(perro.transform.position, yo) <= Vector3.Distance(perro.transform.position, otro) ? yo : otro);

            // Un jugador caído al suelo pasa por delante de todo: el perro va con él, se queda con él y lo levanta ANTES de atacar a nadie.
            int caido = -1;
            var mu = plugin.MuerteActual;
            if (mu != null)
            {
                if (mu.Mio == Muerte.Estado.Derribado) caido = 0;
                else if (hayOtro && mu.DelOtro == Muerte.Estado.Derribado) caido = 1;
            }
            if (caido >= 0)
            {
                if (caidoDesde < 0f) { caidoDesde = t; sinAvance = t; ultimaDist = 1e9f; }
                if (seguido != caido) { seguido = caido; plugin.Log("Perro: " + (caido == 0 ? "anfitrión" : "invitado") + " caído; el perro va a levantarlo"); }
                dueno = caido == 1 ? otro : yo;
                presa = null; mordiendo = false;
                agente.stoppingDistance = 1.5f;
                float dc = Vector3.Distance(perro.transform.position, dueno);
                agente.speed = VelocidadAuxilio;   // corre mucho más rápido que de costumbre: 2000 m en poco más de un minuto
                if (dc > 3f)
                {
                    NavMeshHit nhd; destinoCaido = NavMesh.SamplePosition(dueno, out nhd, 25f, NavMesh.AllAreas) ? nhd.position : dueno;
                    if (t > proxDestino) { proxDestino = t + 1.5f; agente.SetDestination(destinoCaido); }   // pedirlo cada cuadro reinicia el cálculo y no termina nunca en rutas largas
                    levantaDesde = -1f;
                }
                // llegó al punto transitable más cercano al caído (si el caído está fuera del mapa de caminos no puede acercarse más): vale como llegada
                bool llego = dc <= 3.5f || (dc <= 20f && Vector3.Distance(perro.transform.position, destinoCaido) < 2.5f);
                if (dc < ultimaDist - 2f) { ultimaDist = Mathf.Min(ultimaDist, dc); sinAvance = t; }
                // Sin camino calculable (muy lejos o islas de caminos separadas): avanza a mano en línea recta, también a 32 m/s.
                float velPaso = 0f;
                if (dc > 3.5f && !agente.hasPath)
                {
                    if (sinCaminoDesde < 0f) sinCaminoDesde = t;
                    if (t - sinCaminoDesde > 2f)
                    {
                        Vector3 haciaC = dueno - perro.transform.position; haciaC.y = 0f;
                        float paso = Mathf.Min(VelocidadAuxilio * Time.deltaTime, haciaC.magnitude);
                        Vector3 objetivo = perro.transform.position + haciaC.normalized * paso;
                        NavMeshHit nhp;
                        if (NavMesh.SamplePosition(objetivo, out nhp, 30f, NavMesh.AllAreas)) { agente.Warp(nhp.position); velPaso = VelocidadAuxilio; ultimaDist = Mathf.Min(ultimaDist, dc); sinAvance = t; perro.transform.rotation = Quaternion.LookRotation(haciaC.normalized); }
                    }
                }
                else sinCaminoDesde = -1f;
                velCaido = velPaso;
                if (t > proxLogCaido) { proxLogCaido = t + 3f; plugin.Log("Perro(auxilio): a " + Mathf.RoundToInt(dc) + " m, camino=" + agente.pathStatus + " tiene=" + agente.hasPath + " pendiente=" + agente.pathPending + " enMapa=" + agente.isOnNavMesh + " vel=" + agente.velocity.magnitude.ToString("F1") + " sinCamino=" + (sinCaminoDesde < 0f ? "-" : (t - sinCaminoDesde).ToString("F1"))); }
                // Seguro: si el caído lleva más de 100 s esperando o el perro no avanza en 8 s (sin camino, atascado), llega de golpe.
                if (!llego && (t - caidoDesde > 100f || t - sinAvance > 12f))
                {
                    NavMeshHit nhc;
                    if (NavMesh.SamplePosition(dueno, out nhc, 25f, NavMesh.AllAreas)) { agente.Warp(nhc.position); sinAvance = t; ultimaDist = 1e9f; plugin.Log("Perro: llegó de golpe junto al caído (a " + Mathf.RoundToInt(dc) + " m)"); }
                }
                else if (llego)
                {
                    Mirar(dueno);
                    if (levantaDesde < 0f) levantaDesde = t;
                    mordiendo = false;
                    if (t - levantaDesde >= TiempoLevantarPerro)
                    {
                        levantaDesde = -1f;
                        plugin.Log("Perro: levantó al " + (caido == 0 ? "anfitrión" : "invitado"));
                        if (caido == 0) mu.Levantado(true); else plugin.EnviarLevantar(true);
                    }
                }
                Animar(Mathf.Max(agente.velocity.magnitude, velCaido), false);
                if (t > proximoEnvio)
                {
                    proximoEnvio = t + 0.1f;
                    Vector3 pc = perro.transform.position - Anclaje.Origen(); float yc = perro.transform.eulerAngles.y, vc = Mathf.Max(Mathf.Max(agente.velocity.magnitude, velCaido), velAnim);
                    plugin.EnviarPerro(Red.Armar(w => { w.Write((byte)1); w.Write(pc.x); w.Write(pc.y); w.Write(pc.z); w.Write(yc); w.Write(vc); w.Write(false); }));
                }
                return;
            }
            levantaDesde = -1f; caidoDesde = -1f; sinCaminoDesde = -1f; agente.speed = VelocidadCarrera;

            if (t > proximaRevision) { proximaRevision = t + 0.4f; presa = BuscarPresa(dueno); }
            mordiendo = false;
            if (presa != null)
            {
                agente.stoppingDistance = 1.5f;
                agente.SetDestination(presa.position);
                if (Vector3.Distance(perro.transform.position, presa.position) < Mordisco)
                {
                    mordiendo = true;
                    Mirar(presa.position);
                    if (t > proximoMordisco) { proximoMordisco = t + IntervaloMordisco; Morder(presa); }
                }
            }
            else
            {
                agente.stoppingDistance = 2.5f;
                if (Vector3.Distance(perro.transform.position, dueno) > 3.5f) agente.SetDestination(dueno);
                if (Vector3.Distance(perro.transform.position, dueno) > 120f)
                {
                    NavMeshHit nh;
                    if (NavMesh.SamplePosition(dueno, out nh, 10f, NavMesh.AllAreas))
                    {
                        bool ok = agente.Warp(nh.position);
                        plugin.Log("Perro: salto junto a su dueño " + (ok ? "ok" : "FALLÓ") + " → " + (perro.transform.position - Anclaje.Origen()).ToString("F0"));
                    }
                }
            }
            // Atascado lejos de su dueño (agente sin camino): se recoloca a mano y se rehace el agente.
            float lejos = Vector3.Distance(perro.transform.position, dueno);
            if (lejos > 25f && agente.velocity.sqrMagnitude < 0.01f) { if (atascadoDesde < 0f) atascadoDesde = t; } else atascadoDesde = -1f;
            if (atascadoDesde > 0f && t - atascadoDesde > 3f)
            {
                atascadoDesde = -1f;
                NavMeshHit nh2;
                if (NavMesh.SamplePosition(dueno + Vector3.down, out nh2, 20f, NavMesh.AllAreas))
                {
                    agente.enabled = false;
                    perro.transform.position = nh2.position;
                    agente.enabled = true;
                    plugin.Log("Perro: estaba atascado a " + Mathf.RoundToInt(lejos) + " m; recolocado junto a su dueño (" + (agente.isOnNavMesh ? "en el mapa" : "FUERA del mapa") + ")");
                }
            }
            if (t > proximoDiag && Depuracion.Bot) { proximoDiag = t + 3f; plugin.Log("Perro: dueño a " + Mathf.RoundToInt(lejos) + " m, camino=" + agente.pathStatus + " pendiente=" + agente.pathPending + " tieneCamino=" + agente.hasPath + " vel=" + agente.velocity.magnitude.ToString("0.0")); }
            Animar(agente.velocity.magnitude, mordiendo);
            if (t > proximoEnvio)
            {
                proximoEnvio = t + 0.1f;
                Vector3 p = perro.transform.position - Anclaje.Origen(); float yaw = perro.transform.eulerAngles.y, v = Mathf.Max(agente.velocity.magnitude, velAnim); bool m = mordiendo;
                plugin.EnviarPerro(Red.Armar(w => { w.Write((byte)1); w.Write(p.x); w.Write(p.y); w.Write(p.z); w.Write(yaw); w.Write(v); w.Write(m); }));
            }
        }

        /// El enemigo vivo más cercano al perro (no jefes), si está cerca del perro y de su dueño.
        string diag = "";

        Transform BuscarPresa(Vector3 dueno)
        {
            Transform mejor = null; float dm = RadioCaza;
            int vistos = 0, jefes = 0, sinZona = 0, lejos = 0, quietos = 0, invis = 0, altura = 0; float masCerca = 1e9f;
            foreach (var td in Object.FindObjectsOfType<TakeDamage>())
            {
                if (td == null || td.GetComponentInParent<FPSPlayer>() != null || td.transform.root == perro.transform) continue;
                vistos++;
                if (Dificultad.EsJefe(td)) { jefes++; continue; }   // a los jefes no
                Transform u = Combate.RaizDe(td);
                if (!u.gameObject.activeInHierarchy || u.GetComponentInChildren<CharacterDamagePlaymakerAI>() == null) { sinZona++; continue; }
                // invisibles (todas sus mallas apagadas: noche tranquila, ocultos, bajo tierra en las minas) y quietos que no atacan: no son presa
                if (!Visible(u)) { invis++; continue; }
                if (Mathf.Abs(u.position.y - perro.transform.position.y) > 15f) { altura++; continue; }
                if (!Activo(u)) { quietos++; continue; }
                float d = Vector3.Distance(u.position, perro.transform.position);
                masCerca = Mathf.Min(masCerca, d);
                if (Vector3.Distance(u.position, dueno) >= 40f) { lejos++; continue; }
                if (d < dm) { dm = d; mejor = u; }
            }
            diag = "vistos=" + vistos + " quietos=" + quietos + " invisibles=" + invis + " altura=" + altura + " jefes=" + jefes + " sinZona=" + sinZona + " lejosDueño=" + lejos + " másCerca=" + (masCerca < 1e8f ? masCerca.ToString("0") : "—");
            return mejor;
        }

        static bool Visible(Transform u)
        {
            foreach (var r in u.GetComponentsInChildren<Renderer>(false)) if (r.enabled && r.gameObject.activeInHierarchy && !(r is ParticleSystemRenderer) && !(r is TrailRenderer)) return true;
            return false;
        }

        /// ¿Se ha movido hace poco? (más de 0,6 m en los últimos ~3 s). Un enemigo recién visto no cuenta hasta que se mueva.
        bool Activo(Transform u)
        {
            int id = u.GetInstanceID(); float t = Time.realtimeSinceStartup; Vector3 p = u.position;
            if (!refPos.ContainsKey(id)) { refPos[id] = p; refT[id] = t; movioT[id] = -99f; return false; }
            if ((p - refPos[id]).sqrMagnitude > 0.36f) { movioT[id] = t; refPos[id] = p; refT[id] = t; }
            else if (t - refT[id] > 1.5f) { refPos[id] = p; refT[id] = t; }
            return t - movioT[id] < 3f;
        }

        void Morder(Transform objetivo)
        {
            var zona = objetivo.GetComponentInChildren<CharacterDamagePlaymakerAI>();
            if (zona == null) return;
            zona.ApplyDamage(DanoMordisco, false);   // el combate compartido lo pasa al dueño del monstruo
        }

        void Mirar(Vector3 p)
        {
            Vector3 d = p - perro.transform.position; d.y = 0f;
            if (d.sqrMagnitude > 0.01f) perro.transform.rotation = Quaternion.Slerp(perro.transform.rotation, Quaternion.LookRotation(d), 0.25f);
        }

        // ---------------- Invitado ----------------

        public void Recibir(BinaryReader r)
        {
            byte tipo = r.ReadByte();
            if (tipo == 0) { Liberar(false); return; }
            if (tipo == 2)
            {
                // el anfitrión manda cómo está el perro: el invitado nunca va por detrás (si no, ve al perro de la celda llorando)
                int e = r.ReadByte();
                if (e > Estado) { plugin.Log("Perro: el anfitrión dice estado " + e + " (yo " + Estado + "): igualado"); Estado = e; }
                return;
            }
            posRemota = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle()) + Anclaje.Origen();
            yawRemoto = r.ReadSingle(); velRemota = r.ReadSingle(); mordiendo = r.ReadBoolean(); hayRemoto = true;
        }

        void MoverRemoto()
        {
            if (!hayRemoto) return;
            if (Vector3.Distance(perro.transform.position, posRemota) > 30f) perro.transform.position = posRemota;
            perro.transform.position = Vector3.Lerp(perro.transform.position, posRemota, Mathf.Clamp01(Time.deltaTime * 10f));
            perro.transform.rotation = Quaternion.Slerp(perro.transform.rotation, Quaternion.Euler(0f, yawRemoto, 0f), Mathf.Clamp01(Time.deltaTime * 10f));
            Animar(velRemota, mordiendo);
        }

        // ---------------- Animación ----------------

        string clipActual;

        Vector3 prevPosAnim; float velMedida, velAnim; bool prevValida;

        /// Velocidad real del perro medida por cuánto se mueve (el agente marca 0 cuando lo mueven con Warp o a mano,
        /// y entonces se veía al perro desplazarse sentado).
        float Medir()
        {
            float dt = Time.deltaTime;
            Vector3 p = perro.transform.position;
            if (prevValida && dt > 0f)
            {
                Vector3 d = p - prevPosAnim; d.y = 0f;
                float s = d.magnitude > 4f ? 0f : d.magnitude / dt;   // un salto de teletransporte no cuenta como correr
                velMedida = Mathf.Lerp(velMedida, s, 0.35f);
            }
            prevPosAnim = p; prevValida = true;
            return velMedida;
        }

        void Animar(float vel, bool morder)
        {
            vel = Mathf.Max(vel, Medir()); velAnim = vel;
            if (animL != null)
            {
                string c = morder ? Clip("attack", "bite") : vel > 4f ? Clip("run", "walk") : vel > 0.3f ? Clip("walk", "run") : Clip("idle", "stand");
                Sonar(vel, morder);
                if (c != null && c != clipActual) { animL.CrossFade(c, 0.2f); clipActual = c; }
                if (c != null) animL[c].speed = vel > 0.3f && !morder ? Mathf.Clamp(vel / (c.ToLower().Contains("run") ? 7f : 2.5f), 0.6f, 1.8f) : 1f;
            }
            else if (anim != null)
            {
                // Estados del controlador del juego: walk (también para correr, más rápido), Attack1, Sit.
                string estado = morder ? "Attack1" : vel > 0.3f ? "walk" : "Sit";
                Sonar(vel, morder);
                if (estado != clipActual) { bool hay = anim.HasState(0, Animator.StringToHash(estado)); plugin.Log("Perro(" + (plugin.EsAnfitrion ? "anfitrión" : "invitado") + "): animación " + estado + " vel=" + vel.ToString("0.0") + (hay ? "" : " (ESE ESTADO NO EXISTE)") + " animador=" + (anim.isActiveAndEnabled ? "activo" : "APAGADO")); if (hay) { anim.CrossFade(estado, 0.2f, 0); clipActual = estado; } }
                anim.speed = morder ? 2.6f : vel > 0.3f ? Mathf.Clamp(vel / 2.2f, 0.7f, 6f) : 1f;
            }
        }

        /// Jadeo contento mientras corre o camina (nunca lloriquea); mordisco con gruñido cada vez que ataca (2 veces más rápido).
        void Sonar(float vel, bool morder)
        {
            if (voz == null) return;
            float t = Time.realtimeSinceStartup;
            if (morder)
            {
                if (jadeando) { voz.Stop(); jadeando = false; }
                if (t > proxMuerde) { proxMuerde = t + IntervaloMordisco; var cm = SonidosPerro.MordiscoReal(); voz.pitch = 0.9f + 0.2f * (float)azar.NextDouble(); voz.PlayOneShot(cm != null ? cm : SonidosPerro.Mordisco(), 1f); }
                return;
            }
            if (vel > 0.3f)
            {
                if (!jadeando || !voz.isPlaying || modoVoz != 1) { modoVoz = 1; var cj = SonidosPerro.JadeoReal(); voz.clip = cj != null ? cj : SonidosPerro.Jadeo(); voz.loop = true; voz.volume = cj != null ? 0.45f : 0.55f; voz.pitch = cj != null ? 2.0f : 1f; voz.Play(); jadeando = true; }
            }
            else
            {
                // quieto: respiración tranquila y baja, más lenta que el jadeo de cuando corre
                var cj = SonidosPerro.JadeoReal();
                if (!jadeando || !voz.isPlaying || modoVoz != 2) { modoVoz = 2; voz.clip = cj != null ? cj : SonidosPerro.Jadeo(); voz.loop = true; voz.volume = 0.28f; voz.pitch = 1.45f; voz.Play(); jadeando = true; }
            }
        }

        string Clip(string a, string b)
        {
            string otro = null;
            foreach (AnimationState st in animL)
            {
                string n = st.name.ToLowerInvariant();
                if (n.Contains(a)) return st.name;
                if (otro == null && n.Contains(b)) otro = st.name;
            }
            return otro;
        }

        public string Describir()
        {
            return "voz=" + (voz != null ? (voz.isPlaying ? (voz.clip != null ? voz.clip.name : "disparo") : "callado") : "—") + " vel=" + (agente != null ? agente.velocity.magnitude.ToString("F1") : "—") + " " + "estado=" + Estado + " perro=" + (perro != null ? (perro.transform.position - Anclaje.Origen()).ToString("F0") : "—") + " sigue=" + seguido + " presa=" + (presa != null ? presa.name : "—") + " mordiendo=" + mordiendo + " | " + diag + " agente=" + (agente == null ? "—" : (agente.enabled ? "on" : "off") + (agente.isOnNavMesh ? " en mapa" : " FUERA") + " vel=" + agente.velocity.magnitude.ToString("0.0") + " destino=" + (agente.hasPath ? (agente.destination - Anclaje.Origen()).ToString("F0") : "—") + " raízMov=" + (anim != null && anim.applyRootMotion)) + " anim=" + (anim != null ? anim.name + (anim.isActiveAndEnabled ? "" : "(apagado)") : "—");
        }
    }
}

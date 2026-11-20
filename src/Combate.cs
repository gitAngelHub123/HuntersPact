// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using UnityEngine;
using UnityEngine.AI;

namespace WitchHuntCoop
{
    /// Fase 2: monstruos y daño compartidos.
    ///
    /// - Cada monstruo lo simula el juego del jugador MÁS CERCANO ("dueño"): su IA persigue y
    ///   ataca a ese jugador sin tocar PlayMaker. El dueño envía posición, giro y animación;
    ///   el otro juego los copia encima de su propia copia del monstruo.
    /// - El anfitrión numera los monstruos (catálogo) y decide los dueños; el invitado empareja
    ///   cada número con su monstruo local del mismo nombre más cercano.
    /// - Todo daño a un monstruo se envía al otro juego y se aplica en los dos, así la vida y la
    ///   muerte coinciden.
    public class Combate
    {
        public class Unidad
        {
            public float VelPuesta = -1f, AnimPuesta = -1f;   // lo último que puso Dificultad
            public int Id;
            public TakeDamage Td;
            public Transform Raiz;      // raíz del monstruo (lo que usa CharacterDamagePlaymakerAI)
            public Transform Mover;     // lo que se desplaza (el del NavMeshAgent, o la raíz)
            public NavMeshAgent Agente;
            public Animator Anim;
            public string Nombre;
            public bool DuenoInvitado;  // quién lo simula
            public bool Remota;         // en ESTE juego lo controla el otro
            // estado recibido (relativo al ancla)
            public Vector3 Pos; public float Yaw; public int AnimHash; public float AnimT;
            public bool TieneEstado; public float DistintoDesde;
            public bool MuerteAnotada;  // ya está en la lista de reapariciones (Pesadilla)

            public bool MuerteAvisada;   // invitado: ya avisó al anfitrión de que su copia murió
            public bool VistoVivo;       // invitado: su copia tuvo vida > 0 alguna vez (los jefes tienen Health 0 hasta que empieza su pelea, 2026-11-20)
            public bool JefeMuertoVisto; bool jefeVivo;
            /// Jefe que estuvo vivo y ahora está muerto (para Viaje.MuerteJefe).
            public bool VistoVivoJefe(bool esJefe, bool muerto) { if (!esJefe) return false; if (!muerto) { jefeVivo = true; return false; } return jefeVivo; }
            public bool Viva { get { return Td != null && Raiz != null && Raiz.gameObject.activeInHierarchy; } }
        }

        const float RadioCatalogo = 150f;   // monstruos más lejos de ambos jugadores no se sincronizan
        const float RadioEmparejar = 80f;   // distancia máxima para emparejar con un monstruo local
        const float Histeresis = 5f;        // margen para no cambiar de dueño a cada rato
        const float IntervaloCatalogo = 1f;
        const float IntervaloEstado = 0.1f;

        readonly Plugin plugin;
        readonly Red red;
        readonly Dictionary<int, Unidad> porId = new Dictionary<int, Unidad>();
        readonly Dictionary<TakeDamage, Unidad> porTd = new Dictionary<TakeDamage, Unidad>();
        int siguienteId = 1;
        float ultimoCatalogo, ultimoEstado, ultimoEscaneo;
        int escena = -2;

        public static Combate Instancia;
        public static bool AplicandoRemoto;   // evita reenviar el daño que llega de la red

        public Combate(Plugin p, Red r) { plugin = p; red = r; Instancia = this; }

        public int Cantidad { get { return porId.Count; } }
        public IEnumerable<Unidad> Unidades { get { return porId.Values; } }

        public void Reiniciar()
        {
            foreach (var u in porId.Values) Liberar(u);
            porId.Clear(); porTd.Clear(); caidos.Clear(); muertosAqui.Clear();
        }

        // ---------------- Bucle ----------------

        /// posPropia/posOtro en coordenadas de mundo; posOtro null si el compañero no está en la escena.
        public void Actualizar(bool conectado, int escenaLocal, Vector3? posPropia, Vector3? posOtro, Vector3 ancla)
        {
            if (escenaLocal != escena) { Reiniciar(); escena = escenaLocal; }
            if (!conectado || posPropia == null) { if (porId.Count > 0) Reiniciar(); return; }
            float t = Time.realtimeSinceStartup;
            posJug1 = posPropia; posJug2 = posOtro;

            if (red.EsAnfitrion)
            {
                Reaparicion(t);
                if (t - ultimoEscaneo > 0.5f) { ultimoEscaneo = t; Escanear(posPropia.Value, posOtro); }
                DecidirDuenos(posPropia.Value, posOtro);
                if (t - ultimoCatalogo > IntervaloCatalogo) { ultimoCatalogo = t; EnviarCatalogo(escenaLocal, ancla); }
            }

            // (2026-11-20) muerte de un jefe (en cualquiera de los dos juegos): el viaje al pueblo que lanza el juego se permite siempre
            foreach (var u in porId.Values)
                if (!u.JefeMuertoVisto && u.Td != null && u.VistoVivoJefe(Dificultad.EsJefe(u.Td), Eventos.Muerto(u.Td)))
                {
                    u.JefeMuertoVisto = true; Viaje.MuerteJefe = t; plugin.Log("Combate: murió el jefe " + u.Nombre + " (el viaje al pueblo que siga queda permitido)");
                    if (u.Nombre != null && u.Nombre.IndexOf("Worm", StringComparison.OrdinalIgnoreCase) >= 0)
                    { Viaje.GusanoMuerto = t; plugin.Log("Combate: murió el gusano gigante: empieza el tramo final (los viajes ya no exigen estar a 15 m)"); }
                }

            // Invitado: si su copia de un monstruo del anfitrión murió aquí (p. ej. con la espada, o se perdió algún
            // golpe por la red), se avisa al anfitrión para que mate el suyo; si no, el anfitrión lo seguía listando
            // vivo y el invitado lo volvía a crear en el mismo sitio una y otra vez (enemigos "duplicados", 2026-11-17).
            if (!red.EsAnfitrion)
                foreach (var u in porId.Values)
                {
                    if (u.MuerteAvisada || u.Td == null) continue;
                    if (!Eventos.Muerto(u.Td)) { u.VistoVivo = true; continue; }
                    // (2026-11-20) solo si antes estuvo viva: la copia del hombre lobo llegaba con Health 0 (pelea sin empezar) y el
                    // invitado avisaba su "muerte" al acercarse: el anfitrión mataba al jefe sin pelea y la historia avanzaba sola.
                    if (!u.VistoVivo) continue;
                    u.MuerteAvisada = true; muertosAqui[u.Id] = t;
                    int idM = u.Id;
                    red.Enviar(TipoMensaje.MuerteInvitado, Red.Armar(w => w.Write(idM)));
                    plugin.Log("Combate: mi copia del monstruo " + idM + " murió; aviso al anfitrión");
                }

            // Quitar muertos/destruidos
            List<int> fuera = null;
            foreach (var u in porId.Values) if (u.Td == null || u.Raiz == null) (fuera ?? (fuera = new List<int>())).Add(u.Id);
            if (fuera != null) foreach (int id in fuera) Quitar(id);

            if (t - ultimoEstado > IntervaloEstado) { ultimoEstado = t; EnviarEstados(ancla); }
        }

        // ---------------- Pesadilla: los enemigos normales y guardianes reviven al minuto ----------------

        public const float SegundosReaparicion = 60f;   // Pesadilla: reviven al minuto (antes 5 minutos; pedido 2026-11-17)
        class Caido { public string Nombre; public Vector3 Pos; public float Hora; }
        readonly List<Caido> caidos = new List<Caido>();
        float proxReaparicion; Vector3? posJug1, posJug2;

        void Reaparicion(float t)
        {
            if (Opciones.Dificultad < 2) { if (caidos.Count > 0) caidos.Clear(); return; }
            if (t < proxReaparicion) return;
            proxReaparicion = t + 1f;
            // anotar los que acaban de morir (jefes, aliados y el perro no)
            foreach (var u in porId.Values)
            {
                if (u.MuerteAnotada || u.Td == null || u.Raiz == null || u.Remota) continue;
                if (Dificultad.EsJefe(u.Td) || Aliados.Protegido(u.Raiz) || u.Nombre == null) continue;
                if (!Eventos.Muerto(u.Td)) continue;
                u.MuerteAnotada = true;
                bool repetido = false;   // un mismo cadáver puede aparecer como dos unidades (Crux_ai agrupa varias)
                foreach (var o in caidos) if ((o.Pos - u.Raiz.position).sqrMagnitude < 9f) { repetido = true; break; }
                if (repetido) continue;
                caidos.Add(new Caido { Nombre = u.Nombre, Pos = u.Raiz.position, Hora = t });
            }
            // los que llevan 1 minuto caídos vuelven (si nadie está encima del sitio)
            for (int i = caidos.Count - 1; i >= 0; i--)
            {
                var c = caidos[i];
                if (t - c.Hora < SegundosReaparicion) continue;
                bool cerca = false;
                if (posJug1.HasValue && (posJug1.Value - c.Pos).sqrMagnitude < 400f) cerca = true;
                if (posJug2.HasValue && (posJug2.Value - c.Pos).sqrMagnitude < 400f) cerca = true;
                if (cerca) continue;   // se espera a que nadie esté mirando
                // y solo si algún jugador está a menos de 400 m: más lejos no aparece nada (pedido 2026-11-17)
                float rp = Mundo.RadioMaxActual * Mundo.RadioMaxActual;
                bool alAlcance = (posJug1.HasValue && (posJug1.Value - c.Pos).sqrMagnitude < rp) || (posJug2.HasValue && (posJug2.Value - c.Pos).sqrMagnitude < rp);
                if (!alAlcance) continue;
                caidos.RemoveAt(i);
                var td = Generar(c.Nombre, c.Pos);
                int bar = c.Nombre.IndexOf('/');
                if (td == null && bar > 0 && bar < c.Nombre.Length - 1) td = Generar(c.Nombre.Substring(bar + 1), c.Pos);   // si "Tipo" no tiene prefab, se prueba con el nombre del objeto
                plugin.Log("Pesadilla: '" + c.Nombre + "' reapareció al minuto" + (td == null ? " (no se pudo generar)" : ""));
            }
        }

        /// Aplica la posición/animación recibida a los monstruos que controla el otro juego.
        public void LateUpdate(Vector3 ancla, float dt)
        {
            foreach (var u in porId.Values)
            {
                Dificultad.Acelerar(u);
                // remota = el dueño es el otro: (anfitrión y dueño invitado) o (invitado y dueño anfitrión)
                bool remota = red.EsAnfitrion ? u.DuenoInvitado : !u.DuenoInvitado;
                if (remota != u.Remota) CambiarControl(u, remota);
                if (!u.Remota || !u.TieneEstado || !u.Viva || u.Mover == null) continue;

                Vector3 destino = u.Pos + ancla;
                float k = 1f - Mathf.Exp(-12f * dt);
                if ((u.Mover.position - destino).sqrMagnitude > 15f * 15f) k = 1f;
                u.Mover.position = Vector3.Lerp(u.Mover.position, destino, k);
                u.Mover.rotation = Quaternion.Slerp(u.Mover.rotation, Quaternion.Euler(0f, u.Yaw, 0f), k);

                if (u.Anim != null && u.Anim.isActiveAndEnabled && u.AnimHash != 0)
                {
                    int actual = u.Anim.GetCurrentAnimatorStateInfo(0).fullPathHash;
                    if (actual == u.AnimHash) u.DistintoDesde = -1f;
                    else if (u.DistintoDesde < 0f) u.DistintoDesde = Time.time;
                    else if (Time.time - u.DistintoDesde > 0.2f && u.Anim.HasState(0, u.AnimHash))
                    {
                        u.Anim.CrossFade(u.AnimHash, 0.15f, 0, u.AnimT - Mathf.Floor(u.AnimT));
                        u.DistintoDesde = -1f;
                    }
                }
            }
        }

        void CambiarControl(Unidad u, bool remota)
        {
            u.Remota = remota;
            if (u.Agente != null && u.Agente.isActiveAndEnabled)
            {
                if (remota) { u.Agente.updatePosition = false; u.Agente.updateRotation = false; }
                else
                {
                    try { u.Agente.Warp(u.Mover.position); } catch { }
                    u.Agente.updatePosition = true; u.Agente.updateRotation = true;
                }
            }
            u.TieneEstado = false;
            u.DistintoDesde = -1f;
        }

        void Liberar(Unidad u)
        {
            if (u.Remota && u.Agente != null) { u.Agente.updatePosition = true; u.Agente.updateRotation = true; }
        }

        void Quitar(int id)
        {
            Unidad u;
            if (!porId.TryGetValue(id, out u)) return;
            Liberar(u);
            porId.Remove(id);
            if (u.Td != null) porTd.Remove(u.Td);
        }

        // ---------------- Anfitrión: numerar y decidir dueños ----------------

        void Escanear(Vector3 posPropia, Vector3? posOtro)
        {
            foreach (var td in UnityEngine.Object.FindObjectsOfType<TakeDamage>())
            {
                if (porTd.ContainsKey(td)) continue;
                if (td.transform.root.name.StartsWith("coop_aliado")) continue;   // los aliados de la bola no son monstruos
                Vector3 p = td.transform.position;
                float d = Vector3.Distance(p, posPropia);
                if (posOtro != null) d = Mathf.Min(d, Vector3.Distance(p, posOtro.Value));
                if (d > RadioCatalogo) continue;
                Registrar(siguienteId++, td);
            }
        }

        Unidad Registrar(int id, TakeDamage td)
        {
            var u = new Unidad { Id = id, Td = td, Raiz = RaizDe(td), DistintoDesde = -1f };
            u.Agente = u.Raiz.GetComponent<NavMeshAgent>();
            u.Mover = u.Raiz;
            u.Anim = u.Raiz.GetComponentInChildren<Animator>();
            u.Nombre = NombreDe(td);
            porId[id] = u; porTd[td] = u;
            plugin.Log("Monstruo " + id + ": " + u.Nombre + " (mueve " + u.Mover.name + (u.Agente != null ? ", agente" : "") + ")");
            return u;
        }

        /// Raíz de UN monstruo: el ancestro (o él mismo) con NavMeshAgent. No sirve transform.root:
        /// varios monstruos comparten padre (p. ej. Crux_ai con perro, torturado y crawlers).
        public static Transform RaizDe(TakeDamage td)
        {
            Transform x = td.transform;
            while (x != null)
            {
                if (x.GetComponent<NavMeshAgent>() != null) return x;
                x = x.parent;
            }
            return td.transform;
        }

        /// Nombre para emparejar entre juegos: tipo (padre de la raíz) + objeto con la vida.
        /// Ej.: "UndeadDog/PlaymakerAI", "Undead/women_Undead".
        public static string NombreDe(TakeDamage td)
        {
            Transform r = RaizDe(td);
            string tipo = r.parent != null ? r.parent.name : r.name;
            return NombreBase(tipo) + "/" + NombreBase(td.name);
        }

        static string NombreBase(string n)
        {
            n = n.Replace("(Clone)", "").Trim();
            int i = n.IndexOf(" (");
            return i > 0 ? n.Substring(0, i) : n;
        }

        void DecidirDuenos(Vector3 posPropia, Vector3? posOtro)
        {
            foreach (var u in porId.Values)
            {
                if (!u.Viva || u.Mover == null) continue;
                if (posOtro == null) { u.DuenoInvitado = false; continue; }
                float dA = Vector3.Distance(u.Mover.position, posPropia);
                float dI = Vector3.Distance(u.Mover.position, posOtro.Value);
                if (!u.DuenoInvitado && dI < dA - Histeresis) u.DuenoInvitado = true;
                else if (u.DuenoInvitado && dA < dI - Histeresis) u.DuenoInvitado = false;
            }
        }

        void EnviarCatalogo(int escenaLocal, Vector3 ancla)
        {
            var lista = new List<Unidad>();
            foreach (var u in porId.Values) if (u.Viva && u.Mover != null) lista.Add(u);
            red.Enviar(TipoMensaje.Catalogo, Red.Armar(w =>
            {
                w.Write(escenaLocal);
                w.Write(lista.Count);
                foreach (var u in lista)
                {
                    w.Write(u.Id); w.Write(u.Nombre);
                    Vector3 p = u.Mover.position - ancla;
                    w.Write(p.x); w.Write(p.y); w.Write(p.z);
                    w.Write(u.DuenoInvitado);
                }
            }));
        }

        // ---------------- Invitado: recibir catálogo ----------------

        public void RecibirCatalogo(BinaryReader r, int escenaLocal, Vector3 ancla)
        {
            int esc = r.ReadInt32();
            int n = r.ReadInt32();
            var vistos = new HashSet<int>();
            for (int i = 0; i < n; i++)
            {
                int id = r.ReadInt32(); string nombre = r.ReadString();
                Vector3 p = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle()) + ancla;
                bool duenoInv = r.ReadBoolean();
                if (esc != escenaLocal) continue;
                vistos.Add(id);
                Unidad u;
                if (!porId.TryGetValue(id, out u))
                {
                    float cuando;
                    if (muertosAqui.TryGetValue(id, out cuando) && Time.realtimeSinceStartup - cuando < 30f) continue;   // lo maté aquí: no se recrea mientras el anfitrión se entera
                    TakeDamage td = Emparejar(nombre, p);
                    // El anfitrión tiene uno que aquí no apareció: se genera solo si está cerca; lejos del
                    // jugador el propio juego lo borraría al instante (y se volvería a generar sin fin).
                    if (td == null)
                    {
                        var yo = plugin.Jugador;
                        if (yo != null && Vector3.Distance(yo.transform.position, p) > 160f) continue;
                        td = Generar(nombre, p);
                    }
                    if (td == null) continue;
                    u = Registrar(id, td);
                    if (!duenoInv) { u.Pos = p - ancla; }
                }
                u.DuenoInvitado = duenoInv;
            }
            // Monstruos que el anfitrión ya no lista (muertos o lejos): se sueltan.
            List<int> fuera = null;
            foreach (int id in porId.Keys) if (!vistos.Contains(id)) (fuera ?? (fuera = new List<int>())).Add(id);
            if (fuera != null) foreach (int id in fuera) Quitar(id);
            OcultarHuerfanos(ancla);
        }

        /// El anfitrión manda sobre qué monstruos existen. Cada juego hace aparecer los suyos al
        /// azar, así que el invitado puede tener monstruos que el anfitrión no tiene: el compañero
        /// los vería atacar a la nada (o el invitado vería uno de más). Se ocultan los que estén
        /// cerca del invitado y sigan sin pareja en dos catálogos seguidos.
        readonly Dictionary<TakeDamage, int> sinPareja = new Dictionary<TakeDamage, int>();
        public int Ocultados;

        void OcultarHuerfanos(Vector3 ancla)
        {
            var j = UnityEngine.Object.FindObjectOfType<FPSPlayer>();
            if (j == null) return;
            var vistos = new HashSet<TakeDamage>();
            foreach (var td in UnityEngine.Object.FindObjectsOfType<TakeDamage>())
            {
                if (porTd.ContainsKey(td)) continue;
                Transform raiz = RaizDe(td);
                if (raiz == td.transform && td.GetComponentInParent<NavMeshAgent>() == null) continue; // no es un monstruo
                if (Vector3.Distance(td.transform.position, j.transform.position) > RadioCatalogo - 30f) continue;
                vistos.Add(td);
                int n; sinPareja.TryGetValue(td, out n); n++;
                sinPareja[td] = n;
                if (n >= 2)
                {
                    plugin.Log("Ocultado (el anfitrión no lo tiene): " + NombreDe(td) + " en " + td.transform.position);
                    raiz.gameObject.SetActive(false);
                    Ocultados++;
                    sinPareja.Remove(td);
                }
            }
            var viejos = new List<TakeDamage>();
            foreach (var k in sinPareja.Keys) if (!vistos.Contains(k)) viejos.Add(k);
            foreach (var k in viejos) sinPareja.Remove(k);
        }

        TakeDamage Emparejar(string nombre, Vector3 p)
        {
            TakeDamage mejor = null, historia = null; float dMejor = RadioEmparejar, dHistoria = float.MaxValue;
            foreach (var td in UnityEngine.Object.FindObjectsOfType<TakeDamage>())
            {
                if (porTd.ContainsKey(td)) continue;
                if (NombreDe(td) != nombre) continue;
                float d = Vector3.Distance(td.transform.position, p);
                if (d < dMejor) { dMejor = d; mejor = td; }
                if (d < dHistoria && Dificultad.EsJefe(td)) { dHistoria = d; historia = td; }
            }
            // (2026-11-20) Criaturas de la historia (jefes, árbol de sacrificio…): caminan por todo el bosque y cada juego tiene la suya en
            // otro sitio. Si no hay una cerca, el invitado trae LA SUYA a donde está la del anfitrión (no se crea otra: su muerte tiene que
            // ocurrir en los dos juegos para que la historia avance igual en ambos).
            if (mejor == null && historia != null)
            {
                var rz = RaizDe(historia);
                var ag = rz.GetComponentInChildren<NavMeshAgent>();
                NavMeshHit nh; Vector3 destino = p;
                if (NavMesh.SamplePosition(p, out nh, 10f, NavMesh.AllAreas)) destino = nh.position;
                if (ag != null && ag.isOnNavMesh) { try { ag.Warp(destino); } catch { rz.position = destino; } } else rz.position = destino;
                plugin.Log("Combate: " + nombre + " (de la historia) traído junto al del anfitrión; estaba a " + Mathf.RoundToInt(dHistoria) + " m");
                mejor = historia;
            }
            return mejor;
        }

        /// ¿Este monstruo está en la lista sincronizada entre los dos juegos? (orden 'danarnombre')
        public bool Sincronizado(TakeDamage td) { return td != null && porTd.ContainsKey(td); }

        /// Crea una copia del monstruo (desde el prefab en memoria) en la posición dada.
        readonly HashSet<string> sinPrefab = new HashSet<string>();

        public TakeDamage Generar(string nombre, Vector3 pos)
        {
            int barra = nombre.IndexOf('/');
            if (barra > 0) nombre = nombre.Substring(0, barra);
            bool refuerzo = nombre.EndsWith(Mundo.MarcaRefuerzo);   // enemigo de refuerzo: va a por el jugador desde que aparece
            if (refuerzo) nombre = nombre.Substring(0, nombre.Length - Mundo.MarcaRefuerzo.Length);
            bool potenciado = nombre.EndsWith(Potenciado.Marca);
            if (potenciado) nombre = nombre.Substring(0, nombre.Length - Potenciado.Marca.Length);
            if (sinPrefab.Contains(nombre)) return null;   // ya se buscó y no existe (búsqueda cara)
            GameObject prefab = null;
            foreach (var g in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (g.name != nombre || g.scene.IsValid() || g.transform.parent != null) continue;
                if (g.GetComponentInChildren<TakeDamage>(true) == null) continue;
                prefab = g; break;
            }
            if (prefab != null && Dificultad.EsDeHistoria(prefab.transform))
            {   // (2026-11-20) nunca criaturas de la historia (Elongata envía "Tree1_killed" al morir: contaba como árbol destruido)
                if (sinPrefab.Add(nombre)) plugin.Log("No se genera '" + nombre + "': es una criatura de la historia (no se volverá a avisar)");
                return null;
            }
            if (prefab == null && nombre == "Undead")
            {
                // Los zombis "Undead" no tienen prefab: viven en una reserva (PoolMember, hijos de Crux_ai) de la escena. Se saca uno libre de la reserva.
                foreach (var g in Resources.FindObjectsOfTypeAll<GameObject>())
                {
                    if (g.name != "Undead" || !g.scene.IsValid() || g.activeSelf) continue;
                    var tdp = g.GetComponentInChildren<TakeDamage>(true); if (tdp == null) continue;
                    NavMeshHit nh2; Vector3 p2 = pos;
                    if (NavMesh.SamplePosition(pos, out nh2, 8f, NavMesh.AllAreas)) p2 = nh2.position;
                    g.transform.position = p2; g.SetActive(true);
                    var ag2 = g.GetComponentInChildren<NavMeshAgent>(); if (ag2 != null) { try { ag2.Warp(p2); } catch { } }
                    if (potenciado) Potenciado.Aplicar(g);
                    plugin.Log("Generado 'Undead' desde la reserva en " + p2);
                    return g.GetComponentInChildren<TakeDamage>();
                }
            }
            if (prefab == null)
            {
                if (sinPrefab.Add(nombre)) plugin.Log("No hay prefab para generar '" + nombre + "' (no se volverá a avisar)");
                return null;
            }
            // Al punto transitable más cercano del mapa de navegación (si no, el juego lo
            // destruye por "inalcanzable"); si no hay, al suelo bajo el punto.
            NavMeshHit nh;
            if (NavMesh.SamplePosition(pos, out nh, 8f, NavMesh.AllAreas)) pos = nh.position;
            else
            {
                RaycastHit hit;
                if (Physics.Raycast(pos + Vector3.up * 1.5f, Vector3.down, out hit, 10f)) pos = hit.point;
            }
            var go = (GameObject)UnityEngine.Object.Instantiate(prefab, pos, Quaternion.identity);
            if (Dificultad.EsDeHistoria(go.transform))
            {   // por si el prefab no dejó leer sus máquinas de estados: se revisa la copia antes de activarla
                UnityEngine.Object.Destroy(go); sinPrefab.Add(nombre);
                plugin.Log("No se genera '" + nombre + "': es una criatura de la historia (revisada la copia)");
                return null;
            }
            go.SetActive(true);
            var ag = go.GetComponentInChildren<NavMeshAgent>();
            if (ag != null) { try { ag.Warp(pos); } catch { } }
            if (potenciado) Potenciado.Aplicar(go);
            if (refuerzo) go.name += Mundo.MarcaRefuerzo;
            plugin.Log("Generado '" + nombre + "'" + (potenciado ? " POTENCIADO" : "") + " en " + pos);
            return go.GetComponentInChildren<TakeDamage>();
        }

        // ---------------- Estado de los monstruos que simulo yo ----------------

        void EnviarEstados(Vector3 ancla)
        {
            var mios = new List<Unidad>();
            foreach (var u in porId.Values) if (!u.Remota && u.Viva && u.Mover != null) mios.Add(u);
            if (mios.Count == 0) return;
            red.Enviar(TipoMensaje.Monstruos, Red.Armar(w =>
            {
                w.Write(mios.Count);
                foreach (var u in mios)
                {
                    w.Write(u.Id);
                    Vector3 p = u.Mover.position - ancla;
                    w.Write(p.x); w.Write(p.y); w.Write(p.z);
                    w.Write(u.Mover.eulerAngles.y);
                    int hash = 0; float tt = 0f;
                    if (u.Anim != null && u.Anim.isActiveAndEnabled && u.Anim.runtimeAnimatorController != null)
                    {
                        var st = u.Anim.GetCurrentAnimatorStateInfo(0);
                        hash = st.fullPathHash; tt = st.normalizedTime;
                    }
                    w.Write(hash); w.Write(tt);
                }
            }));
        }

        public void RecibirEstados(BinaryReader r)
        {
            int n = r.ReadInt32();
            for (int i = 0; i < n; i++)
            {
                int id = r.ReadInt32();
                Vector3 p = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                float yaw = r.ReadSingle(); int hash = r.ReadInt32(); float tt = r.ReadSingle();
                Unidad u;
                if (!porId.TryGetValue(id, out u) || !u.Remota) continue;
                u.Pos = p; u.Yaw = yaw; u.AnimHash = hash; u.AnimT = tt; u.TieneEstado = true;
            }
        }

        // ---------------- Daño ----------------

        /// Llamado (vía Harmony) cuando ESTE juego daña a un monstruo.
        public void DanoLocal(CharacterDamagePlaymakerAI zona, float dano, bool fuego)
        {
            if (zona == null || zona.ai == null) return;
            Unidad u;
            if (!porTd.TryGetValue(zona.ai, out u)) return;   // monstruo no compartido
            string ruta = RutaRelativa(u.Raiz, zona.transform);
            red.Enviar(TipoMensaje.Dano, Red.Armar(w => { w.Write(u.Id); w.Write(ruta); w.Write(dano); w.Write(fuego); }));
        }

        readonly Dictionary<int, float> muertosAqui = new Dictionary<int, float>();

        /// Anfitrión: el invitado mató su copia de este monstruo; se mata también aquí (como un golpe del compañero).
        public void RecibirMuerteInvitado(BinaryReader r)
        {
            int id = r.ReadInt32();
            Unidad u;
            if (!porId.TryGetValue(id, out u) || !u.Viva || u.Td == null || Eventos.Muerto(u.Td)) return;
            AplicandoRemoto = true;
            try { u.Td.Damage(999999f, false); plugin.Log("Combate: el compañero mató al monstruo " + id + " (" + u.Nombre + "); muerto también aquí"); }
            catch (Exception e) { plugin.Log("Combate: no pude matar el monstruo " + id + ": " + e.Message); }
            finally { AplicandoRemoto = false; }
        }

        public void RecibirDano(BinaryReader r)
        {
            int id = r.ReadInt32(); string ruta = r.ReadString(); float dano = r.ReadSingle(); bool fuego = r.ReadBoolean();
            Unidad u;
            if (!porId.TryGetValue(id, out u) || !u.Viva) return;
            Transform zonaT = ruta.Length == 0 ? u.Raiz : u.Raiz.Find(ruta);
            CharacterDamagePlaymakerAI zona = zonaT != null ? zonaT.GetComponent<CharacterDamagePlaymakerAI>() : null;
            if (zona == null) zona = u.Raiz.GetComponentInChildren<CharacterDamagePlaymakerAI>();
            if (zona == null) return;
            AplicandoRemoto = true;
            try { zona.ApplyDamage(dano, fuego); }
            finally { AplicandoRemoto = false; }
        }

        static string RutaRelativa(Transform raiz, Transform t)
        {
            var partes = new List<string>();
            while (t != null && t != raiz) { partes.Insert(0, t.name); t = t.parent; }
            return string.Join("/", partes.ToArray());
        }

        public string Describir()
        {
            int remotas = 0; foreach (var u in porId.Values) if (u.Remota) remotas++;
            var sb = new System.Text.StringBuilder();
            sb.Append("monstruos=").Append(porId.Count).Append(" (controlados por el otro: ").Append(remotas).Append(")");
            foreach (var u in porId.Values)
                if (u.Viva && u.Mover != null)
                    sb.Append(" [").Append(u.Id).Append(' ').Append(u.Nombre).Append(u.Remota ? " remoto" : " local")
                      .Append(" en ").Append(u.Mover.position.ToString("F0")).Append(']');
            return sb.ToString();
        }
    }

    [HarmonyPatch(typeof(CharacterDamagePlaymakerAI), "ApplyDamage")]
    static class ParcheDano
    {
        static void Postfix(CharacterDamagePlaymakerAI __instance, float damage, bool ignite)
        {
            if (Combate.AplicandoRemoto || Combate.Instancia == null) return;
            try { Combate.Instancia.DanoLocal(__instance, damage, ignite); }
            catch (Exception e) { Debug.LogError("[WitchHuntCoop] daño: " + e); }
        }
    }
}

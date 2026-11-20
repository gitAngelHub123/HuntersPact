// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace WitchHuntCoop
{
    /// Jefe OPCIONAL de la guarida (2026-11-18): la criatura voladora del juego (Night_Flyer, que en el juego solo se ve de cerca
    /// al derribarla) a doble tamaño, animada por el mod. DOS FASES (pedido del usuario):
    ///  1) en el aire (vida > 50 %): cuelga del techo, despierta, vuela en círculos y se lanza en picada; si alcanza a un jugador
    ///     lo AGARRA con las garras, lo sube 10–20 m y lo SUELTA (el golpe de la caída lo pone el propio juego).
    ///  2) en el suelo (vida ≤ 50 %): cae, camina apoyándose en las alas, persigue a uno de los dos jugadores, lo golpea con las
    ///     alas de cerca y le LANZA ROCAS de lejos.
    /// El anfitrión decide (IA, vida, rocas) y manda el estado; cada juego aplica el daño a SU jugador; el invitado manda su daño
    /// al jefe y avisa cuando lo agarran.
    public class JefeCueva : MonoBehaviour
    {
        public enum Fase : byte { Colgada = 0, Despertar = 1, Vuelo = 2, Picada = 3, Subida = 4, Muerta = 5, Agarre = 6, Caida = 7, Tierra = 8, Golpe = 9, Lanzar = 10 }
        public const string Nombre = "La Madre de la Guarida";
        public const float Escala = 2f;

        public Fase fase = Fase.Colgada;
        public float vida, vidaMax;
        public bool autoridad;
        public System.Action<Vector3> alMorir;
        public System.Action<float> alDanarRemoto;            // invitado → anfitrión: daño
        public System.Action alAgarrarmeRemoto;               // invitado → anfitrión: me agarró
        public System.Action<Vector3, Vector3> alLanzarRoca;  // anfitrión → invitado: roca (origen, velocidad)

        Vector3 centro, colgada; float suelo, techo;
        float tFase, fasAleteo, fasPaso, giroCirculo, plegado = 1f, proxPicada, proxSonido, proxRoca, proxGolpe, cambioPresa, altAgarre;
        Vector3 vel, objetivo, destinoRemoto; Quaternion rotRemota; bool hayRemoto;
        int presa;   // 0: jugador de este juego, 1: compañero (en fase 2)
        bool golpeAplicado, rocaLanzada, alaGolpe;
        // (2026-11-19) más listo: si no avanza en 2 s cambia de objetivo o rodea; una roca a cada jugador y 8 al cielo
        public const float AlcanceRoca = 100f, TiempoTrabado = 2f;
        // (2026-11-19) fase 1: vuela ×3 y ataca ×2,5 (picada más rápida y más seguida)
        public const float VueloX = 3f, AtaqueX = 2.5f;
        // (2026-11-19) no sale de la cueva: la cueva es un óvalo (superelipse) con bóveda; vuela dentro de un volumen seguro
        public float cuevaAX = 110f, cuevaAZ = 112f, cuevaPared = 20f, cuevaAlto = 56f, cuevaN = 3f;
        public const float RadioSeguro = 0.70f, MargenTecho = 12f;
        // formas de vuelo de la fase 1: 0 círculo, 1 ocho, 2 de esquina a esquina, 3 ola (sube y baja), 4 espiral que se cierra
        int patron = -1; float patronIni, patronHasta, patronR, patronAlto, patronDir, patronAng; Vector3 patronA, patronB;
        Vector3 garrasPrev, cuerpoPrev; bool hayGarrasPrev;
        // (2026-11-20) el agarre no ocurría NUNCA: Pensar() pasaba de Picada a Subida en el mismo cuadro en que llegaba (antes de Agarres()),
        // y el cuerpo apuntaba a 1 m de los pies con las garras ~2 m más abajo (al suelo). Ahora: ventana de 0,3 s tras la picada, el cuerpo
        // baja hasta que las GARRAS queden a la altura del pecho, y cuenta también el paso del cuerpo cerca.
        float picadaHasta = -1f;
        const float AlturaGarras = 1.6f * Escala * 0.6f;   // las garras cuelgan esto bajo el cuerpo
        int picadasN, agarresN;
        public const int RocasCielo = 8;
        bool rocaLanzada2, cieloLanzado; float rodeoHasta, trabaT, trabaDist, forzarHasta; int rodeoLado = 1, forzar = -1;
        Transform modelo;
        readonly List<DamageReceiverObject> receptores = new List<DamageReceiverObject>();
        const float VidaReceptor = 1000000f;

        // agarre de MI jugador (cada juego lleva al suyo)
        bool llevando; float llevandoDesde, proxAgarre; Rigidbody rbJugador; bool kinAntes;
        // (2026-11-19) al llegar arriba sostiene a la presa 5 s más y le hace 1 de daño cada 2 s mientras la lleva
        public const float SostenerAgarre = 5f, DanoAgarre = 1f, CadaDanoAgarre = 2f;
        // (2026-11-19) fase 2: muerde sin parar si estás pegado (4–5 de daño por segundo); la roca hace 20
        public const float MordiscoAlcance = 3.8f, DanoRoca = 20f; float proxMordisco;
        public const float TiempoRocas = 10f, TiempoCuerpo = 30f; bool modoRocas; float modoHasta;
        public static float PiernaMuslo = -90f, PiernaRodilla = 40f;   // 2026-11-19: con +65 quedaban hacia arriba (captura del usuario); −90 las baja al suelo   // fase 2: ángulos de las piernas (orden de prueba: jefecueva piernas <muslo> <rodilla>)
        // daño de caída al soltarte desde lo alto (2026-11-19: no hacía daño desde 30 m)
        bool esperandoCaida; float caidaY, caidaHasta, ultimoY, quietoDesde = -1f;
        float arribaDesde = -1f, proxDanoAgarre;

        class Hueso { public Transform T; public Quaternion Rest; }
        Hueso brazoD, brazoI, anteD, anteI, manoD, manoI, cabeza;
        readonly List<Hueso> dedosD = new List<Hueso>(), dedosI = new List<Hueso>(), cola = new List<Hueso>(), piernas = new List<Hueso>();
        float ladoD = 1f;
        AudioClip sonAleteo, sonGrito;
        AudioSource voz;

        class Roca { public GameObject Go; public Vector3 V; public float Fin; }
        readonly List<Roca> rocas = new List<Roca>();
        static Material matRocaLanzada;

        static int Dif { get { return Mathf.Clamp(Opciones.Dificultad, 0, 2); } }

        // ---------------- Crear ----------------

        public static JefeCueva Crear(Vector3 centroArena, float sueloY, float techoY, bool autoridad, int dificultad, bool coop)
        {
            var piv = new GameObject("coop_jefe_cueva");
            var modelo = MurcielagoJuego.Clonar(Vector3.zero, Quaternion.identity);
            if (modelo == null) { Object.Destroy(piv); return null; }
            // el cadáver del juego lleva dentro el libro del aire (lo suelta al morir en el juego): fuera
            foreach (var t in modelo.GetComponentsInChildren<Transform>(true)) if (t != null && t.name == "Skill_book") Object.Destroy(t.gameObject);
            var j = piv.AddComponent<JefeCueva>();
            j.modelo = modelo.transform;
            j.autoridad = autoridad;
            float[] factor = { 1f, 1.6f, 2.5f };
            j.vidaMax = j.vida = 1600f * factor[Mathf.Clamp(dificultad, 0, 2)] * (coop ? 1.35f : 1f) * 2f;   // ×2 (pedido 2026-11-19: de x6 a x12)
            j.centro = centroArena; j.suelo = sueloY; j.techo = techoY;
            j.Armar();
            return j;
        }

        static Transform Buscar(Transform raiz, string parte)
        {
            foreach (var t in raiz.GetComponentsInChildren<Transform>(true)) if (t.name.EndsWith(parte)) return t;
            return null;
        }
        static Hueso H(Transform t) { return t == null ? null : new Hueso { T = t, Rest = t.localRotation }; }
        static bool DedoHoja(string n) { string s = n.TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9'); return n.Length == s.Length + 2; }

        void Armar()
        {
            modelo.SetParent(transform, false);
            modelo.localScale = modelo.localScale * Escala;
            Transform tCab = Buscar(modelo, " Head"), tPel = Buscar(modelo, " Pelvis") ?? Buscar(modelo, " Spine");
            Transform tBD = Buscar(modelo, " R UpperArm"), tBI = Buscar(modelo, " L UpperArm");
            if (tCab != null && tPel != null && tBD != null && tBI != null)
            {
                Vector3 eje = transform.InverseTransformDirection(tCab.position - tPel.position).normalized;
                Vector3 der = transform.InverseTransformDirection(tBD.position - tBI.position).normalized;
                Vector3 lomo = Vector3.Cross(eje, der).normalized;
                modelo.localRotation = Quaternion.Inverse(Quaternion.LookRotation(eje, lomo)) * modelo.localRotation;
            }
            var rs = modelo.GetComponentsInChildren<Renderer>();
            if (rs.Length > 0) { var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); modelo.position += transform.position - b.center; }
            if (tBD != null) ladoD = transform.InverseTransformPoint(tBD.position).x >= 0f ? 1f : -1f;
            brazoD = H(tBD); brazoI = H(tBI);
            anteD = H(Buscar(modelo, " R Forearm")); anteI = H(Buscar(modelo, " L Forearm"));
            manoD = H(Buscar(modelo, " R Hand")); manoI = H(Buscar(modelo, " L Hand"));
            cabeza = H(tCab);
            foreach (var t in modelo.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name;
                if (n.Contains(" R Finger") && DedoHoja(n)) dedosD.Add(H(t));
                else if (n.Contains(" L Finger") && DedoHoja(n)) dedosI.Add(H(t));
                else if (n.Contains("Tail")) cola.Add(H(t));
                else if (n.EndsWith(" Thigh") || n.EndsWith(" Calf")) piernas.Add(H(t));
            }
            Zona(transform, Vector3.zero, 1.5f);
            if (anteD != null) Zona(anteD.T, Vector3.zero, 1.3f);
            if (anteI != null) Zona(anteI.T, Vector3.zero, 1.3f);
            if (manoD != null) Zona(manoD.T, Vector3.zero, 1.2f);
            if (manoI != null) Zona(manoI.T, Vector3.zero, 1.2f);
            if (cabeza != null) Zona(cabeza.T, Vector3.zero, 0.9f);
            foreach (var c in Resources.FindObjectsOfTypeAll<AudioClip>())
            {
                if (sonAleteo == null && c.name.StartsWith("WHOOSH_Air_Slow")) sonAleteo = c;
                if (sonGrito == null && (c.name.StartsWith("Black Magic - Demonic Voice") || c.name.StartsWith("demonic_dog_gothit"))) sonGrito = c;
            }
            Sonidos.Clip("jefe_picada"); Sonidos.Clip("jefe_aleteo"); Sonidos.Clip("jefe_lanzar"); Sonidos.Clip("jefe_ambiente"); Sonidos.Clip("jefe_musica_fase1"); Sonidos.Clip("jefe_musica_fase2"); Sonidos.Clip("jefe_musica_fase2_entrada");   // empiezan a cargar (en segundo plano)
            voz = gameObject.AddComponent<AudioSource>(); voz.spatialBlend = 1f; voz.minDistance = 8f; voz.maxDistance = 90f; voz.rolloffMode = AudioRolloffMode.Linear;
            colgada = new Vector3(centro.x, techo - 2.6f * Escala, centro.z + 6f);
            transform.position = colgada;
            transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
            fase = Fase.Colgada; plegado = 1f;
        }

        void Zona(Transform padre, Vector3 local, float radioMundo)
        {
            var g = new GameObject("coop_jefe_zona"); g.layer = 27;
            g.transform.SetParent(padre, false); g.transform.localPosition = local;
            var sc = g.AddComponent<SphereCollider>();
            sc.radius = radioMundo / Mathf.Max(0.0001f, g.transform.lossyScale.x);
            var dr = g.AddComponent<DamageReceiverObject>(); dr.hitPoints = VidaReceptor;
            receptores.Add(dr);
        }

        // ---------------- Daño al jefe ----------------

        float DanoRecibido()
        {
            float d = 0f;
            foreach (var r in receptores) { if (r == null) continue; if (r.hitPoints < VidaReceptor) { d += VidaReceptor - r.hitPoints; r.hitPoints = VidaReceptor; } }
            return d;
        }

        public void Danar(float d)
        {
            if (fase == Fase.Muerta || d <= 0f) return;
            vida -= d;
            if (fase == Fase.Colgada) CambiarFase(Fase.Despertar);
            if (vida <= 0f) { vida = 0f; CambiarFase(Fase.Muerta); return; }
            // a la mitad de la vida pasa a la fase 2 (cae al suelo)
            if (vida <= vidaMax * 0.5f && EnAire) CambiarFase(Fase.Caida);
        }

        bool EnAire { get { return fase == Fase.Despertar || fase == Fase.Vuelo || fase == Fase.Picada || fase == Fase.Subida || fase == Fase.Agarre; } }
        public bool FaseDos { get { return fase == Fase.Caida || fase == Fase.Tierra || fase == Fase.Golpe || fase == Fase.Lanzar; } }

        void CambiarFase(Fase f)
        {
            if (fase == f) return;
            if (fase == Fase.Picada) picadaHasta = Time.time + 0.3f;   // el agarre se sigue comprobando un instante tras la picada
            if (f == Fase.Picada) { picadasN++; Debug.Log("[WitchHuntCoop] Jefe de la cueva: picada " + picadasN + " (agarres " + agarresN + ")"); }
            fase = f; tFase = 0f; golpeAplicado = false; rocaLanzada = false; rocaLanzada2 = false; cieloLanzado = false;
            if (f == Fase.Agarre) arribaDesde = -1f;
            // picada: sonido propio del mod (2026-11-19); si no cargó, el grito del juego
            // sonidos CC0 elegidos por el usuario (2026-11-19): rugido al lanzarse en picada, gruñido al lanzar la roca
            var sonPicada = f == Fase.Picada ? Sonidos.Clip("jefe_picada") : f == Fase.Lanzar ? Sonidos.Clip("jefe_lanzar") : null;
            if (sonPicada != null) voz.PlayOneShot(sonPicada, 1f);
            else if ((f == Fase.Despertar || f == Fase.Picada || f == Fase.Caida || f == Fase.Lanzar) && sonGrito != null) voz.PlayOneShot(sonGrito, 1f);
            if (f == Fase.Muerta && sonGrito != null) { voz.pitch = 0.7f; voz.PlayOneShot(sonGrito, 1f); }
            if (f == Fase.Golpe) alaGolpe = !alaGolpe;
        }

        // ---------------- Cada cuadro ----------------

        void Update()
        {
            float dt = Time.deltaTime; tFase += dt;
            float d = DanoRecibido();
            if (d > 0f) { if (autoridad) Danar(d); else if (alDanarRemoto != null) alDanarRemoto(d); }
            if (autoridad) Pensar(dt); else Seguir(dt);
            Agarres();
            Ataques();
            MoverRocas(dt);
            Musica(dt);
            Ambiente(dt);
        }

        // gruñido de fondo en bucle mientras la murciélaga vive (se oye al acercarse; 3D) — pedido 2026-11-19
        AudioSource ambiente;
        void Ambiente(float dt)
        {
            if (ambiente == null)
            {
                var c = Sonidos.Clip("jefe_ambiente"); if (c == null) return;
                ambiente = gameObject.AddComponent<AudioSource>();
                ambiente.clip = c; ambiente.loop = true; ambiente.spatialBlend = 1f; ambiente.rolloffMode = AudioRolloffMode.Linear;
                ambiente.minDistance = 10f; ambiente.maxDistance = 90f; ambiente.volume = 0f; ambiente.Play();
            }
            float objetivo = fase == Fase.Muerta ? 0f : 0.85f * Opciones.FactorVolumen;
            ambiente.volume = Mathf.MoveTowards(ambiente.volume, objetivo, dt / (objetivo > ambiente.volume ? 1.5f : 2.5f));
            if (fase == Fase.Muerta && ambiente.volume <= 0.001f && ambiente.isPlaying) ambiente.Stop();
        }

        // ---------------- Música (2026-11-19) ----------------
        // Fase 1 en bucle desde que despierta hasta que cae al suelo; fase 2 en bucle hasta que muere; al morir se apaga
        // despacio. Fundido entre pistas; se apaga si te alejas de la guarida (>180 m) y vuelve al regresar.
        AudioSource musica; int musicaPista; bool entradaSonando;   // fase 2: primero la entrada épica (una vez), luego el bucle
        public const float VolumenMusica = 0.55f;
        void Musica(float dt)
        {
            int quiero = (fase == Fase.Colgada || fase == Fase.Muerta) ? 0 : (FaseDos ? 2 : 1);
            var j = Mio;
            bool cerca = j != null && (j.transform.position - centro).sqrMagnitude < 180f * 180f;
            if (musica == null)
            {
                musica = gameObject.AddComponent<AudioSource>();
                musica.spatialBlend = 0f; musica.loop = true; musica.volume = 0f; musica.priority = 0; musica.bypassReverbZones = true;
            }
            if (quiero != 0 && quiero != musicaPista)
            {
                if (musica.isPlaying && musica.volume > 0.02f) { musica.volume = Mathf.MoveTowards(musica.volume, 0f, dt / 1.5f); return; }
                var entrada = quiero == 2 ? Sonidos.Clip("jefe_musica_fase2_entrada") : null;
                var c = entrada ?? Sonidos.Clip(quiero == 1 ? "jefe_musica_fase1" : "jefe_musica_fase2");
                if (c == null) return;   // todavía cargando
                entradaSonando = entrada != null;
                musica.clip = c; musica.loop = !entradaSonando; musica.time = 0f; musica.Play(); musicaPista = quiero;
                musica.volume = entradaSonando ? VolumenMusica * Opciones.FactorVolumen : musica.volume;   // la entrada empieza de golpe
                Debug.Log("[WitchHuntCoop] Jefe de la cueva: música de la fase " + quiero + (entradaSonando ? " (entrada épica, " + c.length.ToString("0") + " s)" : " (" + c.length.ToString("0") + " s en bucle)"));
            }
            if (entradaSonando && (!musica.isPlaying || musica.time >= musica.clip.length - 0.03f))
            {
                var bucle = Sonidos.Clip("jefe_musica_fase2");
                if (bucle != null)
                {
                    entradaSonando = false; musica.clip = bucle; musica.loop = true; musica.time = 0f; musica.Play();
                    Debug.Log("[WitchHuntCoop] Jefe de la cueva: fin de la entrada; música de la fase 2 en bucle (" + bucle.length.ToString("0") + " s)");
                }
            }
            float objetivo = (quiero != 0 && quiero == musicaPista && cerca) ? VolumenMusica * Opciones.FactorVolumen : 0f;
            musica.volume = Mathf.MoveTowards(musica.volume, objetivo, dt / (objetivo > musica.volume ? 2f : 3f));
            if (quiero == 0 && musica.isPlaying && musica.volume <= 0.001f) { musica.Stop(); musicaPista = 0; }
        }

        FPSPlayer Mio { get { var pl = Escopeta.plugin; return pl != null ? pl.Jugador : null; } }

        Vector3 PosPresa(out float dist)
        {
            var pl = Escopeta.plugin; dist = 9999f;
            Vector3 mio = Mio != null ? Mio.transform.position : centro;
            var c = pl != null ? pl.CompaneroActual : null;
            bool hayComp = c != null && c.Visible;
            Vector3 p = (presa == 1 && hayComp) ? c.PosicionMundo : mio;
            dist = Vector3.Distance(p, transform.position);
            return p;
        }

        bool PosJugador(int i, out Vector3 p)
        {
            p = Vector3.zero;
            if (i == 0) { if (Mio == null) return false; p = Mio.transform.position; return true; }
            var pl = Escopeta.plugin; var c = pl != null ? pl.CompaneroActual : null;
            if (c == null || !c.Visible) return false;
            p = c.PosicionMundo; return true;
        }

        /// Objetivo de la fase 2: el más cercano, salvo que se haya trabado y esté yendo a por el otro.
        Vector3 Objetivo(out float dist, out int idx)
        {
            Vector3 p;
            if (forzar >= 0 && Time.time < forzarHasta && PosJugador(forzar, out p)) { idx = forzar; dist = Vector3.Distance(p, transform.position); return p; }
            idx = -1; dist = 9999f; Vector3 mejor = centro;
            for (int i = 0; i < 2; i++)
                if (PosJugador(i, out p)) { float d = Vector3.Distance(p, transform.position); if (d < dist) { dist = d; mejor = p; idx = i; } }
            return mejor;
        }

        /// No avanza hacia su presa: la cambia por el otro jugador; si ya lo hizo (o no hay otro), se planta a tirar rocas. Y rodea por un lado.
        void Destrabar(int idx)
        {
            Vector3 otro;
            if (idx >= 0 && Time.time > forzarHasta && PosJugador(1 - idx, out otro))
            {
                forzar = 1 - idx; forzarHasta = Time.time + 10f;
                Debug.Log("[WitchHuntCoop] Jefe de la cueva: trabado, cambia de objetivo");
            }
            else
            {
                forzar = -1; modoRocas = true; modoHasta = Time.time + TiempoRocas; proxRoca = Time.time + 0.4f;
                Debug.Log("[WitchHuntCoop] Jefe de la cueva: trabado, se planta a tirar rocas");
            }
            rodeoLado = Random.value < 0.5f ? 1 : -1; rodeoHasta = Time.time + 1.3f;
        }

        /// ¿Hay algo grande delante (estalagmita, roca gigante, pared)? Lo chico lo pisa.
        bool Obstaculo(Vector3 d, float largo)
        {
            int mascara = ~((1 << 27) | (1 << 11) | (1 << 8));
            Vector3 o = transform.position + Vector3.up * 0.6f;
            foreach (var hh in Physics.RaycastAll(o, d, largo, mascara, QueryTriggerInteraction.Ignore))
            {
                var c = hh.collider;
                if (c is TerrainCollider || c.transform.IsChildOf(transform) || hh.normal.y > 0.6f) continue;   // suelo o rampa: se camina
                var b = c.bounds.size; if (Mathf.Max(b.x, b.z) < 2.5f || b.y < 2.5f) continue;
                return true;
            }
            return false;
        }

        /// Desvía el paso para rodear lo que tenga delante (sigue un rato por el mismo lado para no titubear).
        Vector3 Esquivar(Vector3 d)
        {
            if (Time.time < rodeoHasta) return Quaternion.Euler(0f, 70f * rodeoLado, 0f) * d;
            if (!Obstaculo(d, 7f)) return d;
            bool der = !Obstaculo(Quaternion.Euler(0f, 60f, 0f) * d, 7f), izq = !Obstaculo(Quaternion.Euler(0f, -60f, 0f) * d, 7f);
            rodeoLado = der && !izq ? 1 : izq && !der ? -1 : (Random.value < 0.5f ? 1 : -1);
            rodeoHasta = Time.time + 1.0f;
            return Quaternion.Euler(0f, 70f * rodeoLado, 0f) * d;
        }

        Vector3 JugadorMasCercano(out float dist)
        {
            var pl = Escopeta.plugin; Vector3 mejor = centro; dist = 9999f;
            if (Mio != null) { float dd = Vector3.Distance(Mio.transform.position, transform.position); if (dd < dist) { dist = dd; mejor = Mio.transform.position; } }
            var c = pl != null ? pl.CompaneroActual : null;
            if (c != null && c.Visible) { float dd = Vector3.Distance(c.PosicionMundo, transform.position); if (dd < dist) { dist = dd; mejor = c.PosicionMundo; } }
            return mejor;
        }

        float SueloEn(Vector3 p)
        {
            RaycastHit h;
            int mascara = ~((1 << 27) | (1 << 11) | (1 << 8));
            // (2026-11-19) solo el suelo y lo grande: las rocas, esqueletos y cosas chicas lo enganchaban y lo frenaban
            float mejor = float.NegativeInfinity;
            foreach (var hh in Physics.RaycastAll(new Vector3(p.x, suelo + 6f, p.z), Vector3.down, 14f, mascara, QueryTriggerInteraction.Ignore))
            {
                if (hh.collider.transform.IsChildOf(transform)) continue;
                bool grande = hh.collider is TerrainCollider || hh.collider.bounds.size.x > 6f || hh.collider.bounds.size.z > 6f;
                if (grande && hh.point.y > mejor) mejor = hh.point.y;
            }
            if (!float.IsNegativeInfinity(mejor)) return mejor;
            if (Physics.Raycast(new Vector3(p.x, suelo + 6f, p.z), Vector3.down, out h, 14f, mascara, QueryTriggerInteraction.Ignore) && h.point.y < suelo + 1.2f) return h.point.y;
            return suelo;
        }

        void Pensar(float dt)
        {
            float dist; Vector3 jug = JugadorMasCercano(out dist);
            switch (fase)
            {
                case Fase.Colgada:
                    transform.position = colgada + Vector3.right * Mathf.Sin(Time.time * 0.6f) * 0.15f;
                    transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward) * Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 0.4f) * 6f);
                    // despierta a menos de 100 m de cualquiera de los dos (pedido 2026-11-18; antes 30 m bajo ella), o con el primer disparo que le pegue (Danar)
                    if (dist < 100f) CambiarFase(Fase.Despertar);
                    break;
                case Fase.Despertar:
                    vel += Vector3.down * 9f * dt;
                    transform.position += vel * dt;
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(Vector3.ProjectOnPlane(jug - transform.position, Vector3.up).normalized + Vector3.up * 0.2f, Vector3.up), dt * 2.5f);
                    if (tFase > 1.4f) { vel = transform.forward * 8f; giroCirculo = Mathf.Atan2(transform.position.z - centro.z, transform.position.x - centro.x); proxPicada = Time.time + 4f / AtaqueX; CambiarFase(Fase.Vuelo); }
                    break;
                case Fase.Vuelo:
                {
                    if (patron < 0 || Time.time > patronHasta) ElegirPatron();
                    objetivo = PuntoPatron();
                    Volar(objetivo, 10f * VueloX, dt);
                    if (Time.time > proxPicada && dist < 70f) { objetivo = jug + Vector3.up * (1.2f + AlturaGarras); CambiarFase(Fase.Picada); }
                    break;
                }
                case Fase.Picada:   // va a TU posición con las garras por delante
                    if (tFase < 0.6f / AtaqueX * 1.5f) objetivo = jug + Vector3.up * (1.2f + AlturaGarras);   // las garras al pecho
                    Volar(objetivo, 24f * AtaqueX, dt);
                    if (Vector3.Distance(transform.position, objetivo) < 2.2f || transform.position.y < suelo + 1.6f || tFase > 4f / AtaqueX) CambiarFase(Fase.Subida);
                    break;
                case Fase.Agarre:   // sube con la presa a 30–50 m, la sostiene 5 s y la suelta
                {
                    // sube en diagonal hacia el centro, donde la bóveda es más alta (cerca de las paredes el techo baja)
                    Vector3 haciaC = new Vector3(centro.x - transform.position.x, 0f, centro.z - transform.position.z);
                    Vector3 arriba = new Vector3(transform.position.x, altAgarre, transform.position.z) + (haciaC.magnitude > 1f ? haciaC.normalized * Mathf.Min(haciaC.magnitude, 14f) : Vector3.zero);
                    Volar(arriba, 7f * VueloX, dt);
                    if (arribaDesde < 0f && (transform.position.y > altAgarre - 0.8f || tFase > 5f)) arribaDesde = Time.time;   // llegó arriba
                    if (arribaDesde >= 0f && Time.time - arribaDesde > SostenerAgarre) { arribaDesde = -1f; proxPicada = Time.time + (6f + Random.value * 3f) / AtaqueX; CambiarFase(Fase.Vuelo); }
                    break;
                }
                case Fase.Subida:
                    objetivo = new Vector3(transform.position.x, suelo + 12f, transform.position.z) + (centro - transform.position).normalized * 6f;
                    Volar(objetivo, 12f * VueloX, dt);
                    if (tFase > 2.2f / VueloX * 1.4f) { proxPicada = Time.time + (5f + Random.value * 4f) / AtaqueX; CambiarFase(Fase.Vuelo); }
                    break;
                case Fase.Caida:   // fase 2: cae al suelo
                {
                    vel += Vector3.down * 14f * dt; vel.x *= 0.97f; vel.z *= 0.97f;
                    Vector3 p = transform.position + vel * dt;
                    float s = SueloEn(p) + 1.4f;
                    if (p.y <= s) { p.y = s; vel = Vector3.zero; }
                    transform.position = p;
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized, Vector3.up) * Quaternion.Euler(18f, 0f, 0f), dt * 3f);
                    if (tFase > 2.6f && vel == Vector3.zero) { cambioPresa = Time.time + 12f; proxRoca = Time.time + 1f; modoRocas = true; modoHasta = Time.time + TiempoRocas; CambiarFase(Fase.Tierra); }
                    break;
                }
                case Fase.Tierra:   // camina apoyado en las alas hacia su presa
                {
                    if (Time.time > cambioPresa) { presa = Random.value < 0.5f ? 0 : 1; cambioPresa = Time.time + 12f + Random.value * 8f; }
                    // (2026-11-19) ciclo: 10 s quieto tirando rocas al más cercano, 30 s persiguiendo para morder, y otra vez
                    if (Time.time > modoHasta) { modoRocas = !modoRocas; modoHasta = Time.time + (modoRocas ? TiempoRocas : TiempoCuerpo); proxRoca = Time.time + 0.6f; }
                    float dObj; int iObj; Vector3 obj = Objetivo(out dObj, out iObj);
                    float distH = new Vector2(obj.x - transform.position.x, obj.z - transform.position.z).magnitude;
                    float dN; Vector3 cerca = JugadorMasCercano(out dN);
                    float distN = new Vector2(cerca.x - transform.position.x, cerca.z - transform.position.z).magnitude;
                    if (modoRocas && distH > AlcanceRoca) { modoRocas = false; modoHasta = Time.time + TiempoCuerpo; }   // nadie a tiro: va a buscarlos
                    if (modoRocas) Encarar(obj, dt); else Caminar(obj, dt);
                    // trabado: si en 2 s no se acercó ni 3 m a su presa, cambia de objetivo o se planta a tirar rocas
                    if (!modoRocas && distN > MordiscoAlcance + 1.5f)
                    {
                        if (trabaT <= 0f) { trabaT = Time.time; trabaDist = distH; }
                        else if (Time.time - trabaT > TiempoTrabado)
                        {
                            if (trabaDist - distH < 3f) Destrabar(iObj);
                            trabaT = 0f;
                        }
                    }
                    else trabaT = 0f;
                    if (distN < MordiscoAlcance && Time.time > proxGolpe) { proxGolpe = Time.time + 0.9f; CambiarFase(Fase.Golpe); }   // mordisco al que esté pegado
                    else if (modoRocas && Time.time > proxRoca) { proxRoca = Time.time + 2.0f + Random.value * 0.8f; objetivo = obj; CambiarFase(Fase.Lanzar); }
                    break;
                }
                case Fase.Golpe:
                {
                    float dp; Vector3 obj = JugadorMasCercano(out dp);
                    Encarar(obj, dt);
                    if (tFase > 0.9f) CambiarFase(Fase.Tierra);
                    break;
                }
                case Fase.Lanzar:
                {
                    float dp; int ip; Vector3 obj = Objetivo(out dp, out ip);
                    Encarar(obj, dt);
                    // una roca a cada jugador (si está a tiro) y una lluvia de rocas al cielo que caen por la cueva
                    if (!rocaLanzada && tFase > 0.55f) { rocaLanzada = true; if (dp < AlcanceRoca) LanzarRoca(obj, 2f); }
                    if (!rocaLanzada2 && tFase > 0.75f)
                    {
                        rocaLanzada2 = true; Vector3 otro;
                        if (ip >= 0 && PosJugador(1 - ip, out otro) && Vector3.Distance(otro, transform.position) < AlcanceRoca) LanzarRoca(otro, -2f);
                    }
                    if (!cieloLanzado && tFase > 0.85f) { cieloLanzado = true; LluviaRocas(RocasCielo); }
                    if (tFase > 1.1f) CambiarFase(Fase.Tierra);
                    break;
                }
                case Fase.Muerta:
                    Caer(dt);
                    break;
            }
            Animar(dt);
        }

        void Volar(Vector3 hacia, float rapidez, float dt)
        {
            Vector3 deseo = (hacia - transform.position);
            if (deseo.sqrMagnitude > 0.01f) deseo = deseo.normalized * rapidez;
            Vector3 antes = vel;
            vel = Vector3.MoveTowards(vel, deseo, dt * rapidez * 1.6f);
            Vector3 p = transform.position + vel * dt;
            p = DentroCueva(p);
            transform.position = p;
            if (vel.sqrMagnitude > 0.5f)
            {
                Vector3 a0 = Vector3.ProjectOnPlane(antes, Vector3.up), a1 = Vector3.ProjectOnPlane(vel, Vector3.up);
                float giro = Vector3.Angle(a0, a1) * Mathf.Sign(Vector3.Cross(a0, a1).y) / Mathf.Max(dt, 0.001f);
                var q = Quaternion.LookRotation(vel.normalized, Vector3.up) * Quaternion.Euler(0f, 0f, Mathf.Clamp(-giro * 0.35f, -40f, 40f));
                transform.rotation = Quaternion.Slerp(transform.rotation, q, dt * 4f);
            }
        }

        /// 0 en el centro de la cueva, 1 en la pared (misma forma que la bóveda).
        float RadioEn(Vector3 p)
        {
            return Mathf.Pow(Mathf.Pow(Mathf.Abs(p.x - centro.x) / cuevaAX, cuevaN) + Mathf.Pow(Mathf.Abs(p.z - centro.z) / cuevaAZ, cuevaN), 1f / cuevaN);
        }

        /// Altura máxima segura en ese punto: la bóveda baja hacia las paredes (con margen por las rocas del techo).
        float TechoSeguro(Vector3 p)
        {
            float s = Mathf.Clamp01(RadioEn(p) / 0.95f);
            float bov = suelo + cuevaPared + (cuevaAlto - cuevaPared) * Mathf.Sqrt(1f - s * s) - MargenTecho;
            return Mathf.Max(suelo + 8f, Mathf.Min(techo - 3f, bov));
        }

        /// Mete el punto en el volumen seguro de la cueva (de lado y de alto). Así ni el jefe ni a quien lleva salen del mapa.
        Vector3 DentroCueva(Vector3 p)
        {
            float r = RadioEn(p);
            if (r > RadioSeguro) { float k = RadioSeguro / r; p.x = centro.x + (p.x - centro.x) * k; p.z = centro.z + (p.z - centro.z) * k; }
            p.y = Mathf.Clamp(p.y, suelo + 1.2f, TechoSeguro(p) - 2.5f);
            return p;
        }

        /// Punto del borde seguro en un ángulo (forma de superelipse, como la cueva).
        Vector3 PuntoBorde(float ang, float r, float alto)
        {
            float c = Mathf.Cos(ang), s = Mathf.Sin(ang), e = 2f / cuevaN;
            return new Vector3(centro.x + Mathf.Sign(c) * Mathf.Pow(Mathf.Abs(c), e) * cuevaAX * r, alto, centro.z + Mathf.Sign(s) * Mathf.Pow(Mathf.Abs(s), e) * cuevaAZ * r);
        }

        void ElegirPatron()
        {
            int antes = patron;
            do patron = Random.Range(0, 5); while (patron == antes);
            patronIni = Time.time; patronHasta = Time.time + Random.Range(5f, 9f);
            patronDir = Random.value < 0.5f ? 1f : -1f;
            patronAng = Mathf.Atan2(transform.position.z - centro.z, transform.position.x - centro.x);
            patronR = Random.Range(0.3f, 0.62f);
            patronAlto = suelo + Random.Range(9f, 22f);
            if (patron == 2) { patronA = transform.position; patronB = PuntoBorde(patronAng + Mathf.PI + Random.Range(-0.5f, 0.5f), 0.66f, suelo + Random.Range(8f, 24f)); }
            Debug.Log("[WitchHuntCoop] Jefe de la cueva: vuelo " + new[] { "en círculo", "en ocho", "de esquina a esquina", "en ola", "en espiral" }[patron]);
        }

        /// Hacia dónde vuela ahora según la forma elegida (el objetivo va por delante; Volar lo persigue).
        Vector3 PuntoPatron()
        {
            float t = Time.time - patronIni, v = 10f * VueloX * 0.85f;
            switch (patron)
            {
                case 0:   // círculo amplio
                {
                    float a = patronAng + patronDir * (t * v / (patronR * 100f) + 0.35f);
                    return PuntoBorde(a, patronR, patronAlto + 3f * Mathf.Sin(t * 0.8f));
                }
                case 1:   // ocho que cruza el centro
                {
                    float w = t * v / 110f * patronDir + 0.3f;
                    float x = Mathf.Sin(w) * 0.62f, z = Mathf.Sin(w) * Mathf.Cos(w) * 0.62f;
                    float ca = Mathf.Cos(patronAng), sa = Mathf.Sin(patronAng);
                    return new Vector3(centro.x + (x * ca - z * sa) * cuevaAX, patronAlto + 4f * Mathf.Sin(w * 2f), centro.z + (x * sa + z * ca) * cuevaAZ);
                }
                case 2:   // recto de una punta de la cueva a la otra; al llegar, cruza otra vez
                {
                    if (new Vector2(transform.position.x - patronB.x, transform.position.z - patronB.z).magnitude < 12f)
                    {
                        float a = Mathf.Atan2(patronB.z - centro.z, patronB.x - centro.x) + Mathf.PI + Random.Range(-0.6f, 0.6f);
                        patronA = patronB; patronB = PuntoBorde(a, 0.66f, suelo + Random.Range(8f, 24f));
                    }
                    return patronB;
                }
                case 3:   // ola: rodea la cueva subiendo hasta cerca del techo y bajando en picado
                {
                    float a = patronAng + patronDir * (t * v / 60f + 0.35f);
                    Vector3 q = PuntoBorde(a, 0.5f, 0f);
                    q.y = Mathf.Lerp(suelo + 6f, TechoSeguro(q) - 4f, 0.5f + 0.5f * Mathf.Sin(t * 1.6f));
                    return q;
                }
                default:  // espiral que se cierra hacia el centro y se vuelve a abrir
                {
                    float a = patronAng + patronDir * (t * v / 40f + 0.35f);
                    float r = Mathf.Lerp(0.64f, 0.12f, 0.5f + 0.5f * Mathf.Cos(t * 0.45f));
                    return PuntoBorde(a, r, patronAlto + (0.64f - r) * 25f);
                }
            }
        }

        void Caminar(Vector3 hacia, float dt)
        {
            Vector3 d = hacia - transform.position; d.y = 0f;
            float distH = d.magnitude;
            float rapidez = distH > 2.0f ? (4.2f + Dif * 0.6f) * 3f : 0f;   // ×3 (2026-11-19)   // (2026-11-19) se acerca hasta pegarse (antes paraba a 4 m)
            if (distH > 0.1f) d /= distH;
            if (rapidez > 0f) d = Esquivar(d);   // rodea estalagmitas y rocas gigantes (2026-11-19)
            Vector3 p = transform.position + d * rapidez * dt;
            // dentro de la cueva (no atraviesa las paredes: radio de la arena)
            float rc = RadioEn(p); if (rc > 0.93f) { float k = 0.93f / rc; p.x = centro.x + (p.x - centro.x) * k; p.z = centro.z + (p.z - centro.z) * k; }   // (2026-11-19) forma real de la cueva: antes un círculo de 88 m dejaba fuera la zona de la entrada y se trababa
            p.y = Mathf.Lerp(transform.position.y, SueloEn(p) + 1.4f + 0.12f * Mathf.Abs(Mathf.Sin(fasPaso)), dt * 8f);
            transform.position = p;
            fasPaso += dt * rapidez * 1.3f;
            Encarar(Time.time < rodeoHasta ? transform.position + d * 6f : hacia, dt);
        }

        void Encarar(Vector3 hacia, float dt)
        {
            Vector3 d = hacia - transform.position; d.y = 0f;
            if (d.sqrMagnitude < 0.01f) return;
            var q = Quaternion.LookRotation(d.normalized, Vector3.up) * Quaternion.Euler(18f, 0f, 0f);   // cabeza algo baja
            transform.rotation = Quaternion.Slerp(transform.rotation, q, dt * 3.5f);
        }

        bool avisoMuerte;
        void Caer(float dt)
        {
            float s = SueloEn(transform.position) + 0.6f;
            if (transform.position.y > s)
            {
                vel += Vector3.down * 12f * dt; vel.x *= 0.98f; vel.z *= 0.98f;
                Vector3 p = transform.position + vel * dt; if (p.y < s) p.y = s;
                transform.position = p;
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized + Vector3.down * 0.1f, Vector3.down), dt * 2f);
            }
            else
            {
                vel = Vector3.zero;
                if (!avisoMuerte) { avisoMuerte = true; if (alMorir != null) alMorir(transform.position); }
            }
        }

        public void Estado(Vector3 p, Quaternion r, Fase f, float v)
        {
            destinoRemoto = p; rotRemota = r; hayRemoto = true; vida = v;
            if (f != fase) CambiarFase(f);
        }

        /// el anfitrión recibe "el invitado quedó agarrado"
        public void AgarradoRemoto()
        {
            if (!autoridad || (fase != Fase.Picada && fase != Fase.Subida)) return;
            altAgarre = Mathf.Min(TechoSeguro(centro) - 3f, suelo + 30f + Random.value * 20f);   // (2026-11-19) te sube a 30–50 m (sin pasar del techo)
            CambiarFase(Fase.Agarre);
        }

        void Seguir(float dt)
        {
            if (hayRemoto)
            {
                if (fase == Fase.Muerta) Caer(dt);
                else
                {
                    transform.position = Vector3.Lerp(transform.position, destinoRemoto, dt * 10f);
                    transform.rotation = Quaternion.Slerp(transform.rotation, rotRemota, dt * 10f);
                    if (fase == Fase.Tierra) fasPaso += dt * 5f;
                }
            }
            Animar(dt);
        }

        // ---------------- Agarre (fase 1) ----------------

        static float DistSegmento(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 seg = b - a;
            float t = seg.sqrMagnitude > 0.0001f ? Mathf.Clamp01(Vector3.Dot(p - a, seg) / seg.sqrMagnitude) : 0f;
            return Vector3.Distance(p, a + seg * t);
        }

        void Agarres()
        {
            var j = Mio; if (j == null) return;
            if (esperandoCaida && !llevando) DanoCaida(j);
            if (rbJugador == null) rbJugador = j.GetComponent<Rigidbody>() ?? j.GetComponentInParent<Rigidbody>();
            Vector3 garras = transform.position - transform.up * 1.6f * Escala * 0.6f;
            if (!llevando)
            {
                bool enPicada = fase == Fase.Picada || (fase == Fase.Subida && Time.time < picadaHasta);
                if (!enPicada || Time.time < proxAgarre) { hayGarrasPrev = false; return; }
                bool hayPrev = hayGarrasPrev && (garrasPrev - garras).sqrMagnitude < 100f;
                Vector3 ga = hayPrev ? garrasPrev : garras, ca = hayPrev ? cuerpoPrev : transform.position;
                garrasPrev = garras; cuerpoPrev = transform.position; hayGarrasPrev = true;
                Vector3 pj = j.transform.position + Vector3.up * 1f;
                // distancia del pecho del jugador al tramo recorrido por las garras y por el cuerpo en este cuadro
                float dg = DistSegmento(pj, ga, garras), dc = DistSegmento(pj, ca, transform.position);
                if (dg > 3.2f && dc > 3.6f) return;
                agarresN++;
                Debug.Log("[WitchHuntCoop] Jefe de la cueva: agarre " + agarresN + " de " + picadasN + " picadas (garras a " + dg.ToString("0.0") + " m, cuerpo a " + dc.ToString("0.0") + " m)");
                // ¡agarrado!
                llevando = true; llevandoDesde = Time.time; proxDanoAgarre = Time.time + CadaDanoAgarre;
                if (rbJugador != null) { kinAntes = rbJugador.isKinematic; rbJugador.isKinematic = true; rbJugador.velocity = Vector3.zero; }
                // (2026-11-20, pedido) sin aviso en pantalla al agarrar
                // (2026-11-19) sin sonido propio al agarrar: ya suena el rugido de la picada (antes un golpe fuerte que quedaba mal)
                if (autoridad) { altAgarre = Mathf.Min(TechoSeguro(centro) - 3f, suelo + 30f + Random.value * 20f); CambiarFase(Fase.Agarre); }
                else if (alAgarrarmeRemoto != null) alAgarrarmeRemoto();
                return;
            }
            // llevando: el jugador cuelga de las garras
            j.transform.position = garras - Vector3.up * 1.0f;
            if (Time.time >= proxDanoAgarre) { proxDanoAgarre = Time.time + CadaDanoAgarre; j.ApplyDamage(DanoAgarre); }
            bool soltar = fase == Fase.Muerta || FaseDos
                          || (Muerte.Instancia != null && Muerte.Instancia.Mio != Muerte.Estado.Vivo)   // cayó: lo suelta
                          || (fase != Fase.Agarre && Time.time - llevandoDesde > 1.2f)   // el anfitrión no confirmó o ya terminó de subir
                          || Time.time - llevandoDesde > 7f + SostenerAgarre;
            if (soltar)
            {
                llevando = false; proxAgarre = Time.time + 8f;
                esperandoCaida = true; caidaY = ultimoY = j.transform.position.y; caidaHasta = Time.time + 12f; quietoDesde = -1f;
                if (rbJugador != null) { rbJugador.isKinematic = kinAntes; rbJugador.velocity = Vector3.down * 2f; }
                // (2026-11-20, pedido) sin aviso en pantalla al soltar
            }
        }

        /// Al tocar el suelo tras soltarte: (metros − 8) × 2,2 de daño. Con «Piernas de hierro» (skill_ironlegs) no hay daño.
        void DanoCaida(FPSPlayer j)
        {
            float y = j.transform.position.y;
            if (y < ultimoY - 0.02f) quietoDesde = -1f; else if (quietoDesde < 0f) quietoDesde = Time.time;
            ultimoY = Mathf.Min(ultimoY, y);
            bool aterrizo = quietoDesde > 0f && Time.time - quietoDesde > 0.2f;
            if (!aterrizo && Time.time < caidaHasta) return;
            esperandoCaida = false;
            float h = caidaY - y; bool piernas = false;
            try { var b = HutongGames.PlayMaker.FsmVariables.GlobalVariables.FindFsmBool("skill_ironlegs"); piernas = b != null && b.Value; } catch { }
            if (h > 8f && !piernas) { j.ApplyDamage((h - 8f) * 2.2f); Debug.Log("[WitchHuntCoop] Jefe de la cueva: caída de " + h.ToString("0") + " m tras soltarte"); }
        }

        // ---------------- Golpes y rocas (fase 2) ----------------

        void Ataques()
        {
            var j = Mio; if (j == null) return;
            // mordisco (2026-11-19): pegado al cuerpo, en cualquier lado, 4–5 de daño por segundo en golpecitos cada 0,25 s
            if (FaseDos && fase != Fase.Caida && fase != Fase.Muerta)
            {
                Vector3 a = j.transform.position - transform.position; a.y = 0f;
                if (a.magnitude < MordiscoAlcance && Time.time >= proxMordisco)
                {
                    proxMordisco = Time.time + 0.25f;
                    j.ApplyDamage(Random.Range(1.0f, 1.25f));
                }
            }
        }

        void LanzarRoca(Vector3 obj, float lado)
        {
            Vector3 o = transform.position + transform.forward * 4f + Vector3.up * 5f + transform.right * lado;   // por encima del cuerpo, no dentro
            float dist = Vector3.Distance(o, obj);
            float T = Mathf.Clamp(dist / 20f, 0.9f, 2.8f);   // más lenta: se ve venir
            Vector3 g = Physics.gravity;
            Vector3 v = (obj + Vector3.up * 0.8f - o) / T - 0.5f * g * T;
            CrearRoca(o, v);
            if (alLanzarRoca != null) alLanzarRoca(o, v);
            Debug.Log("[WitchHuntCoop] Jefe de la cueva: lanza una roca a " + dist.ToString("0") + " m");
        }

        /// Tira rocas hacia el techo que caen en puntos al azar de la cueva (cada juego las ve igual: se mandan como rocas normales).
        void LluviaRocas(int n)
        {
            float radio = 44f * Mundo.CuevaE * 0.92f, g = -Physics.gravity.y;
            for (int i = 0; i < n; i++)
            {
                Vector3 o = transform.position + Vector3.up * 6f + Random.insideUnitSphere * 1.5f;
                Vector2 r = Random.insideUnitCircle * radio;
                Vector3 t = new Vector3(centro.x + r.x, 0f, centro.z + r.y); t.y = SueloEn(t);
                float H = Mathf.Max(o.y + 4f, Mathf.Min(techo - 4f, o.y + 16f + Random.value * 14f));   // altura máxima, bajo el techo
                float vy = Mathf.Sqrt(2f * g * (H - o.y)), T = vy / g + Mathf.Sqrt(2f * Mathf.Max(0.5f, H - t.y) / g);
                Vector3 v = new Vector3((t.x - o.x) / T, vy, (t.z - o.z) / T);
                CrearRoca(o, v);
                if (alLanzarRoca != null) alLanzarRoca(o, v);
            }
            Debug.Log("[WitchHuntCoop] Jefe de la cueva: lluvia de " + n + " rocas");
        }

        public void CrearRoca(Vector3 o, Vector3 v)
        {
            if (matRocaLanzada == null)
            {
                foreach (var m in Resources.FindObjectsOfTypeAll<Material>()) if (m.name == "coop_tunel_roca") { matRocaLanzada = m; break; }
                if (matRocaLanzada == null) matRocaLanzada = new Material(Shader.Find("Standard")) { color = new Color(0.3f, 0.28f, 0.26f) };
                matRocaLanzada = new Material(matRocaLanzada) { color = new Color(0.62f, 0.56f, 0.5f) };   // más clara: se ve en la oscuridad
            }
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);   // angulosa (2026-11-19: no redonda)
            Object.Destroy(go.GetComponent<Collider>());
            go.GetComponent<MeshFilter>().sharedMesh = MallaRoca();
            go.name = "coop_jefe_roca";
            go.transform.position = o; go.transform.localScale = new Vector3(4.4f, 3.6f, 4.0f) * Random.Range(0.85f, 1.15f);   // el doble de grande (2026-11-19)
            var luz = new GameObject("coop_jefe_roca_luz"); luz.transform.SetParent(go.transform, false);
            var lr = luz.AddComponent<Light>(); lr.type = LightType.Point; lr.color = new Color(1f, 0.75f, 0.5f); lr.range = 8f; lr.intensity = 1.4f; lr.shadows = LightShadows.None;
            go.transform.rotation = Random.rotation;
            go.GetComponent<Renderer>().sharedMaterial = matRocaLanzada;
            // estela de polvo para verla en la oscuridad
            var ps = go.AddComponent<ParticleSystem>(); ps.Stop();
            var mn = ps.main; mn.startLifetime = 1.3f; mn.startSpeed = 0.3f; mn.startSize = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
            mn.startColor = new Color(0.55f, 0.48f, 0.4f, 0.55f); mn.simulationSpace = ParticleSystemSimulationSpace.World; mn.maxParticles = 120;
            var em = ps.emission; em.rateOverTime = 90f; var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.4f;
            var col = ps.colorOverLifetime; col.enabled = true; var gr = new Gradient();
            gr.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0f, 1f) }); col.color = gr;
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = Potenciado.MaterialGas();
            ps.Play();
            rocas.Add(new Roca { Go = go, V = v, Fin = Time.time + 9f });   // 9 s: las del cielo tardan más
        }

        void MoverRocas(float dt)
        {
            var j = Mio;
            for (int i = rocas.Count - 1; i >= 0; i--)
            {
                var r = rocas[i];
                if (r.Go == null) { rocas.RemoveAt(i); continue; }
                r.V += Physics.gravity * dt;
                Vector3 p = r.Go.transform.position + r.V * dt;
                r.Go.transform.position = p; r.Go.transform.Rotate(300f * dt, 120f * dt, 0f);
                bool choca = p.y < SueloEn(p) + 0.3f || Time.time > r.Fin;
                if (j != null && Vector3.Distance(j.transform.position + Vector3.up * 1f, p) < 2.8f) choca = true;
                if (!choca) continue;
                if (j != null && Vector3.Distance(j.transform.position + Vector3.up * 0.8f, p) < 3.8f)
                {
                    j.ApplyDamage(DanoRoca);   // 20 (2026-11-19)
                }
                PolvoImpacto(p); ExplosionRoca(p, j);
                Object.Destroy(r.Go); rocas.RemoveAt(i);
            }
        }

        static Mesh mallaRoca;
        /// Bloque de roca anguloso: un cubo con las esquinas corridas al azar (siempre igual en los dos juegos).
        internal static Mesh MallaRoca()
        {
            if (mallaRoca != null) return mallaRoca;
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Cube); var m = Object.Instantiate(tmp.GetComponent<MeshFilter>().sharedMesh); Object.Destroy(tmp);
            var v = m.vertices; var r = new System.Random(77); var corr = new Dictionary<Vector3, Vector3>();
            for (int i = 0; i < v.Length; i++)
            {
                Vector3 k = new Vector3(Mathf.Round(v[i].x * 10), Mathf.Round(v[i].y * 10), Mathf.Round(v[i].z * 10));
                Vector3 d; if (!corr.TryGetValue(k, out d)) { d = new Vector3((float)r.NextDouble() - 0.5f, (float)r.NextDouble() - 0.5f, (float)r.NextDouble() - 0.5f) * 0.35f; corr[k] = d; }
                v[i] += d;
            }
            m.vertices = v; m.RecalculateNormals(); m.RecalculateBounds(); m.name = "coop_roca_angulosa";
            return mallaRoca = m;
        }

        /// Al chocar la roca: explosión (sonido) y temblor de la cueva, más fuerte cuanto más cerca (2026-11-19).
        static void ExplosionRoca(Vector3 p, FPSPlayer j)
        {
            var c = Sonidos.Clip("jefe_roca_impacto");
            if (c != null)
            {
                var g = new GameObject("coop_jefe_roca_boom"); g.transform.position = p; var a = g.AddComponent<AudioSource>();
                a.clip = c; a.spatialBlend = 1f; a.minDistance = 15f; a.maxDistance = 140f; a.rolloffMode = AudioRolloffMode.Linear; a.volume = Opciones.FactorVolumen; a.Play();
                Object.Destroy(g, c.length + 0.2f);
            }
            float d = j != null ? Vector3.Distance(j.transform.position, p) : 999f;
            if (d < 90f) Temblor.Iniciar(Mathf.Lerp(1f, 0.25f, Mathf.InverseLerp(10f, 90f, d)), 0.8f);
        }

        /// Nube de polvo y fragmentos donde cae la roca (2026-11-19: que se note el golpe).
        static void PolvoImpacto(Vector3 p)
        {
            var g = new GameObject("coop_jefe_roca_polvo"); g.transform.position = p;
            var ps = g.AddComponent<ParticleSystem>(); ps.Stop();
            var mn = ps.main; mn.loop = false; mn.duration = 0.2f; mn.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
            mn.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 7f); mn.startSize = new ParticleSystem.MinMaxCurve(1.5f, 4.0f);
            mn.startColor = new Color(0.5f, 0.44f, 0.37f, 0.6f); mn.gravityModifier = 0.15f; mn.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.rateOverTime = 0f; em.SetBursts(new[] { new ParticleSystem.Burst(0f, 80) });
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Hemisphere; sh.radius = 0.6f;
            var col = ps.colorOverLifetime; col.enabled = true; var gr = new Gradient();
            gr.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) }); col.color = gr;
            g.GetComponent<ParticleSystemRenderer>().sharedMaterial = Potenciado.MaterialGas();
            ps.Play(); Object.Destroy(g, 2.5f);
        }

        void OnDestroy()
        {
            foreach (var r in rocas) if (r.Go != null) Object.Destroy(r.Go);
            if (llevando && rbJugador != null) rbJugador.isKinematic = kinAntes;
        }

        // ---------------- Animación de huesos ----------------

        void Animar(float dt)
        {
            bool suelo2 = FaseDos;
            float objetivoPlegado = fase == Fase.Colgada ? 1f : fase == Fase.Muerta ? 0.55f : fase == Fase.Picada ? 0.45f : suelo2 ? 0.55f : 0f;
            plegado = Mathf.MoveTowards(plegado, objetivoPlegado, dt * 1.5f);
            float frec = fase == Fase.Vuelo || fase == Fase.Subida || fase == Fase.Despertar || fase == Fase.Agarre ? 1.7f : fase == Fase.Picada ? 0.6f : 0.3f;
            float prev = Mathf.Sin(fasAleteo);
            fasAleteo += dt * frec * Mathf.PI * 2f;
            float s = Mathf.Sin(fasAleteo);
            var sonAla = Sonidos.Clip("jefe_aleteo") ?? sonAleteo;   // aleteo de alas grandes (CC0, elegido por el usuario)
            if (prev > 0f && s <= 0f && sonAla != null && EnAire && Time.time > proxSonido) { voz.PlayOneShot(sonAla, 0.9f); proxSonido = Time.time + 0.25f; }
            float amp = fase == Fase.Picada ? 10f : (EnAire ? 48f : 0f);
            float aleteo = amp * s + (fase == Fase.Vuelo ? 6f : 0f);
            float barrido = fase == Fase.Picada ? 35f : 8f * Mathf.Sin(fasAleteo + 1.2f);

            Vector3 fwd = transform.forward, up = transform.up, der = transform.right;
            foreach (var lado in new[] { 1f, -1f })
            {
                bool esR = lado == ladoD;
                Hueso brazo = esR ? brazoD : brazoI, ante = esR ? anteD : anteI, mano = esR ? manoD : manoI;
                var dedos = esR ? dedosD : dedosI;
                if (brazo != null) brazo.T.localRotation = brazo.Rest;
                if (ante != null) ante.T.localRotation = ante.Rest;
                if (mano != null) mano.T.localRotation = mano.Rest;
                foreach (var dd in dedos) dd.T.localRotation = dd.Rest;
                float subir = Mathf.Lerp(aleteo, -70f, plegado), atras = Mathf.Lerp(barrido, 25f, plegado);
                float paso = 0f, golpe = 0f;
                if (suelo2)
                {
                    // caminar: las alas plegadas hacen de patas delanteras (alternadas)
                    paso = 28f * Mathf.Sin(fasPaso + (lado > 0 ? 0f : Mathf.PI));
                    subir = -35f + 10f * Mathf.Sin(fasPaso + (lado > 0 ? 0f : Mathf.PI));
                    if (fase == Fase.Golpe && (alaGolpe == (lado > 0)))
                    {
                        float k = Mathf.Clamp01(tFase / 0.45f), vuelta = tFase < 0.45f ? Mathf.Sin(k * Mathf.PI * 0.5f) : Mathf.Clamp01(1f - (tFase - 0.45f) / 0.45f);
                        golpe = 85f * vuelta; subir = Mathf.Lerp(-35f, 25f, vuelta);
                    }
                    if (fase == Fase.Lanzar) { float k = Mathf.Clamp01(tFase / 0.55f); subir = Mathf.Lerp(-35f, 45f, Mathf.Sin(k * Mathf.PI)); }
                }
                if (brazo != null) brazo.T.rotation = Quaternion.AngleAxis(subir * lado, fwd) * Quaternion.AngleAxis((atras - golpe) * lado, up) * Quaternion.AngleAxis(paso, der) * brazo.T.rotation;
                float plAnte = suelo2 ? 70f : Mathf.Lerp(0f, 95f, plegado);
                if (ante != null) ante.T.rotation = Quaternion.AngleAxis(Mathf.Lerp(-aleteo * 0.35f, 0f, plegado) * lado, fwd) * Quaternion.AngleAxis(plAnte * lado, up) * ante.T.rotation;
                float plMano = suelo2 ? -80f : Mathf.Lerp(0f, -110f, plegado);
                if (mano != null) mano.T.rotation = Quaternion.AngleAxis(Mathf.Lerp(-aleteo * 0.25f, 0f, plegado) * lado, fwd) * Quaternion.AngleAxis(plMano * lado, up) * mano.T.rotation;
                foreach (var dd in dedos) dd.T.rotation = Quaternion.AngleAxis(Mathf.Lerp(0f, -25f, plegado) * lado, up) * dd.T.rotation;
            }
            if (cabeza != null)
            {
                cabeza.T.localRotation = cabeza.Rest;
                float mira = fase == Fase.Picada ? 0f : 10f * Mathf.Sin(Time.time * 0.9f);
                // mordisco: echa la cabeza atrás (abre), la lanza adelante de golpe (cierra) y vuelve; el modelo no tiene mandíbula
                float k = Mathf.Clamp01(tFase / 0.9f);
                float mordida = k < 0.35f ? Mathf.Lerp(0f, -32f, k / 0.35f) : k < 0.48f ? Mathf.Lerp(-32f, 22f, (k - 0.35f) / 0.13f) : Mathf.Lerp(22f, 0f, (k - 0.48f) / 0.52f);
                float baja = fase == Fase.Muerta ? 30f : fase == Fase.Golpe ? mordida : 6f * Mathf.Sin(Time.time * 1.3f);
                cabeza.T.rotation = Quaternion.AngleAxis(mira, up) * Quaternion.AngleAxis(baja, der) * cabeza.T.rotation;
            }
            int n = 0;
            foreach (var c in cola) { c.T.localRotation = c.Rest; c.T.rotation = Quaternion.AngleAxis(8f * Mathf.Sin(Time.time * 2f - n * 0.6f), up) * c.T.rotation; n++; }
            foreach (var p in piernas)
            {
                p.T.localRotation = p.Rest;
                if (suelo2)
                {
                    // (2026-11-19) fase 2: piernas apoyadas hacia el suelo (antes quedaban estiradas como en vuelo) y paso alternado
                    bool muslo = p.T.name.EndsWith(" Thigh");
                    float paso = (muslo ? 18f : 10f) * Mathf.Sin(fasPaso + (p.T.name.Contains(" R ") ? Mathf.PI : 0f));
                    p.T.rotation = Quaternion.AngleAxis((muslo ? PiernaMuslo : PiernaRodilla) + paso, der) * p.T.rotation;
                }
                else if (fase == Fase.Picada || fase == Fase.Agarre) p.T.rotation = Quaternion.AngleAxis(-45f, der) * p.T.rotation;   // garras por delante
                else if (fase != Fase.Colgada) p.T.rotation = Quaternion.AngleAxis(4f * Mathf.Sin(fasAleteo - 0.8f), der) * p.T.rotation;
            }
        }

        // ---------------- Barra de vida ----------------

        static Texture2D blanco;
        void OnGUI()
        {
            if (Depuracion.ModoFoto) return;   // fotos para publicar: sin barra de vida
            if (fase == Fase.Colgada || fase == Fase.Muerta) return;
            var j = Mio; if (j == null) return;
            if (Vector3.Distance(j.transform.position, centro) > 90f * Mundo.CuevaE) return;
            if (blanco == null) { blanco = new Texture2D(1, 1); blanco.SetPixel(0, 0, Color.white); blanco.Apply(); }
            float w = Screen.width * 0.42f, h = 14f, x = (Screen.width - w) / 2f, y = 46f;
            var c0 = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.7f); GUI.DrawTexture(new Rect(x - 2f, y - 2f, w + 4f, h + 4f), blanco);
            GUI.color = new Color(0.55f, 0.04f, 0.04f, 0.95f); GUI.DrawTexture(new Rect(x, y, w * Mathf.Clamp01(vida / vidaMax), h), blanco);
            // (2026-11-19) sin marca de la mitad: que nadie sepa al principio que tiene segunda fase
            GUI.color = c0;
            var st = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 18, fontStyle = FontStyle.Bold };
            st.normal.textColor = new Color(0.9f, 0.85f, 0.8f);
            GUI.Label(new Rect(x, y - 28f, w, 26f), Nombre + (FaseDos ? " — enfurecida" : ""), st);
        }

        // ---------------- Red ----------------

        public byte[] EstadoBytes()
        {
            var ms = new MemoryStream(); var w = new BinaryWriter(ms);
            w.Write((byte)0);
            var p = transform.position; var r = transform.rotation;
            w.Write(p.x); w.Write(p.y); w.Write(p.z); w.Write(r.x); w.Write(r.y); w.Write(r.z); w.Write(r.w);
            w.Write((byte)fase); w.Write(vida);
            return ms.ToArray();
        }
    }

    public partial class Mundo
    {
        JefeCueva jefeCueva; float proxEnvioJefe;
        /// Quién decide al jefe (2026-11-18): antes siempre el anfitrión, y si él estaba lejos de la cueva (su guarida ni existía)
        /// los disparos del invitado no llegaban a nadie y el jefe nunca despertaba. Ahora decide el que esté en la cueva: el que
        /// la arma sin recibir el estado del otro decide; si los dos deciden a la vez, el invitado cede; si el que decide se va
        /// (3 s sin su estado), toma el mando el que queda, con la vida y la fase que lleva.
        float ultimoEstadoJefe = -100f;

        static void Mandar(Plugin plugin, byte tipo, System.Action<BinaryWriter> escribir)
        {
            var ms = new MemoryStream(); var w = new BinaryWriter(ms); w.Write(tipo); escribir(w); plugin.EnviarJefeCueva(ms.ToArray());
        }

        /// Llamado cada cuadro desde ActualizarJefeMina (anfitrión e invitado).
        void ActualizarJefeCueva()
        {
            if (plugin.Escena != EscenaBosque) { jefeCueva = null; return; }
            bool muerto = LuaTexto(VarJefeMina) == "muerto";
            Mina g = null; foreach (var m in minas) if (m.Guarida && m.Red != null) { g = m; break; }
            if (g == null) return;
            if (jefeCueva != null && !g.Red.activeSelf) { Object.Destroy(jefeCueva.gameObject); jefeCueva = null; return; }   // nadie en la guarida
            if (jefeCueva == null)
            {
                if (muerto || !g.Red.activeSelf) return;
                var j = plugin.Jugador; var c = plugin.CompaneroActual;
                bool cerca = (j != null && Vector3.Distance(j.transform.position, g.CentroRed) < 80f * CuevaE) || (c != null && c.Visible && Vector3.Distance(c.PosicionMundo, g.CentroRed) < 80f * CuevaE);
                if (!cerca) return;
                var red = g.Red.transform;
                Vector3 origen = red.position;
                float techo = origen.y + AlturaTecho(red, 0f, CuevaZC + 6f), suelo = origen.y + AlturaSueloCueva(0f, CuevaZC);
                bool autoridad = !plugin.Conectado || Time.time - ultimoEstadoJefe > 2f;
                jefeCueva = JefeCueva.Crear(g.CentroRed, suelo, techo, autoridad, Opciones.Dificultad, plugin.Conectado);
                if (jefeCueva == null) { plugin.Log("Jefe de la cueva: no encontré el modelo del juego"); return; }
                jefeCueva.transform.SetParent(red, true);
                jefeCueva.cuevaAX = CuevaAX; jefeCueva.cuevaAZ = CuevaAZ; jefeCueva.cuevaPared = CuevaHPared; jefeCueva.cuevaAlto = CuevaH; jefeCueva.cuevaN = CuevaN;
                jefeCueva.alMorir = p => { plugin.Log("Jefe de la cueva: muerto en " + (p - Ancla()).ToString("F0")); if (jefeCueva != null && jefeCueva.autoridad) SoltarLlave(p - Ancla() + Vector3.up * 0.3f, true); };
                jefeCueva.alDanarRemoto = d => Mandar(plugin, 1, w => w.Write(d));
                jefeCueva.alAgarrarmeRemoto = () => Mandar(plugin, 2, w => { });
                jefeCueva.alLanzarRoca = (o, v) => { if (plugin.Conectado) Mandar(plugin, 3, w => { w.Write(o.x); w.Write(o.y); w.Write(o.z); w.Write(v.x); w.Write(v.y); w.Write(v.z); }); };
                plugin.Log("Jefe de la cueva: " + JefeCueva.Nombre + " colgada del techo (vida " + jefeCueva.vidaMax.ToString("0") + ", " + (autoridad ? "decide este juego" : "decide el otro jugador") + ")");
            }
            if (!jefeCueva.autoridad && plugin.Conectado && Time.time - ultimoEstadoJefe > 3f && jefeCueva.fase != JefeCueva.Fase.Muerta)
            {
                jefeCueva.autoridad = true;
                plugin.Log("Jefe de la cueva: el otro jugador ya no lo controla; ahora decide este juego (vida " + jefeCueva.vida.ToString("0") + ", fase " + jefeCueva.fase + ")");
            }
            if (jefeCueva.autoridad && plugin.Conectado && Time.time > proxEnvioJefe)
            {
                proxEnvioJefe = Time.time + 0.1f;
                plugin.EnviarJefeCueva(jefeCueva.EstadoBytes());
            }
        }

        public void RecibirJefeCueva(BinaryReader r)
        {
            byte tipo = r.ReadByte();
            if (tipo == 0)
            {
                var p = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                var q = new Quaternion(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                var f = (JefeCueva.Fase)r.ReadByte(); float v = r.ReadSingle();
                ultimoEstadoJefe = Time.time;
                if (jefeCueva != null && jefeCueva.autoridad && !plugin.EsAnfitrion) { jefeCueva.autoridad = false; plugin.Log("Jefe de la cueva: el anfitrión también lo controla; decide él"); }
                if (jefeCueva != null && !jefeCueva.autoridad) jefeCueva.Estado(p, q, f, v);
            }
            else if (tipo == 1) { float d = r.ReadSingle(); if (jefeCueva != null && jefeCueva.autoridad) jefeCueva.Danar(d); }
            else if (tipo == 2) { if (jefeCueva != null) jefeCueva.AgarradoRemoto(); }
            else if (tipo == 3)
            {
                var o = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle()); var v = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                if (jefeCueva != null && !jefeCueva.autoridad) jefeCueva.CrearRoca(o, v);
            }
            else if (tipo == 4) EntrarGuaridaRemoto();
            else if (tipo == 5) SellarRemoto();   // el compañero llegó a la sala: la roca sella la entrada   // el compañero entró a la guarida conmigo al lado: entro con él
        }

        public string OrdenJefeCueva(string arg)
        {
            if (jefeCueva == null) return "no hay jefe (hay que estar en la guarida)";
            if (arg == "despertar") { jefeCueva.Danar(1f); return "despierta"; }
            if (arg.StartsWith("piernas"))
            {
                var pp = arg.Split(' '); var ci = System.Globalization.CultureInfo.InvariantCulture;
                if (pp.Length >= 3) { JefeCueva.PiernaMuslo = float.Parse(pp[1], ci); JefeCueva.PiernaRodilla = float.Parse(pp[2], ci); }
                return "piernas: muslo " + JefeCueva.PiernaMuslo + "°, rodilla " + JefeCueva.PiernaRodilla + "°";
            }
            if (arg == "fase2") { jefeCueva.Danar(Mathf.Max(1f, jefeCueva.vida - jefeCueva.vidaMax * 0.5f + 1f)); return "fase " + jefeCueva.fase + " vida " + jefeCueva.vida.ToString("0"); }
            if (arg.StartsWith("danar")) { float d = 300f; float.TryParse(arg.Substring(5).Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out d); jefeCueva.Danar(d); return "vida " + jefeCueva.vida.ToString("0"); }
            return "fase " + jefeCueva.fase + " vida " + jefeCueva.vida.ToString("0") + "/" + jefeCueva.vidaMax.ToString("0") + " en " + (jefeCueva.transform.position - Ancla()).ToString("F1");
        }
    }
}

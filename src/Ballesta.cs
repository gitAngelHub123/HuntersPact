// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using System.Collections.Generic;
using UnityEngine;

namespace WitchHuntCoop
{
    /// Ballesta (arma 6): copia del mosquete con un modelo de ballesta. Cada flecha que impacta vale
    /// por 3 disparos (300 de daño), alcance de 150 m, 1 flecha por recarga de su propia reserva.
    /// Las flechas se ven volar y quedan clavadas un rato donde pegan.
    /// Libros: Puntas de acero (+50 %), Disparo triple (3 flechas rectas por disparo), Bolsa ampliada.
    public static class Ballesta
    {
        public const string Nombre = "Crossbow_coop";
        public const string VarTiene = "coop_tiene_ballesta";
        public const int Indice = 6;               // tecla 6
        public const int Dano = 300;               // = 3 disparos de pistola (100)
        public const float Alcance = 150f, Dispersion = 0.002f, Recarga = 6f, VelocidadFlecha = 90f;   // recarga = 3 s para colocar la flecha + 3 s para tensar la cuerda
        public const int FlechasAlEncontrarla = 10;

        /// Ballesta hecha con el propio mosquete del juego: su culata de madera, su empuñadura, su gatillo y su cañón acortado (hace de riel),
        /// más un arco de acero con la textura del mosquete. Antes era un modelo dibujado por el mod.
        public static readonly ArmaCopia Copia = new ArmaCopia(Nombre, VarTiene, "Ballesta", Indice, MallaBallesta, Escopeta.TexturaEscopeta,
            0.80f, 0.85f, Vector3.zero,
            wb => { wb.projectileCount = 1; wb.damage = Dano; wb.range = Alcance; wb.shotSpread = Dispersion; wb.reloadTime = Recarga; })
            { DelMosquete = true, ConArco = true, CorteCanon = 0.34f, LargoCanon = 0.42f, SeparacionCanon = 0f, SubirArma = 0.0f };

        public static bool Tiene { get { return Copia.Tiene; } }
        public static void Dar(bool si) { Copia.Dar(si); }

        public static Vector3 PuntoCuerda;   // punto de la cuerda (medio), en el espacio del modelo: donde la mano la agarra
        public static void Registro(string m) { Escopeta.plugin.Log(m); }
        static int cargaVista = -1;

        // ---------------- Animación de los brazos, la cuerda y la flecha ----------------
        // ang: grados de los brazos (+ = hacia adelante). slack: 0 = cuerda tensa en la nuez, 1 = cuerda floja colgando entre las puntas.
        static float tFuego = -99f, tRecarga = -99f, tFinRecarga = -99f; static bool recargaVista; static int cargaAnt = -1;
        public static float AngManual = float.NaN, SlackManual = float.NaN, FlechaManual = float.NaN;   // para fotos fijas (orden "ballesta pose")
        const float AngReposo = 0f, AngFlojo = 4f, AngDisparo = 24f, AngTenso = -6f;
        static float Suave(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }

        /// Disparo triple (pedido 2026-11-18): las dos flechas extra van ENCIMA de la central, paralelas y juntas como un haz
        /// (dos apoyadas sobre la del medio). Se ven solo con el modo de 3 flechas activo (tecla X con la ballesta en la mano) y
        /// solo tantas como flechas queden en la reserva. Cada disparo triple gasta 3 flechas (1 del cargador + 2 de la reserva).
        const string PrefTriple = "coop_ballesta_triple";
        public static bool ModoTriple
        {
            get { try { return PlayerPrefs.GetInt(PrefTriple, 1) == 1; } catch { return true; } }
            set { try { PlayerPrefs.SetInt(PrefTriple, value ? 1 : 0); PlayerPrefs.Save(); } catch { } }
        }
        static bool TripleActivo { get { return LibrosTienda.Tiene(LibrosTienda.BallestaTriple) && ModoTriple; } }
        static readonly Transform[] laterales = new Transform[2];
        static float anchoFlecha = -1f; static Transform anchoDe;
        static void FlechasTriple(WeaponBehavior w)
        {
            var d = Copia.Din; if (d == null || d.Flecha == null) return;
            bool triple = TripleActivo;
            var cam = Camera.main;
            if (anchoDe != d.Flecha)
            {
                anchoDe = d.Flecha; anchoFlecha = 0.02f;
                var mf = d.Flecha.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    Vector3 e = Vector3.Scale(mf.sharedMesh.bounds.size, d.Flecha.lossyScale);
                    float[] v = { Mathf.Abs(e.x), Mathf.Abs(e.y), Mathf.Abs(e.z) }; System.Array.Sort(v);
                    anchoFlecha = Mathf.Max(v[1], 0.002f);   // ancho de las plumas (el valor del medio; el mayor es el largo)
                    Registro("Ballesta: flecha cargada " + e.ToString("F4") + " → ancho " + anchoFlecha.ToString("F4"));
                }
            }
            int extra = w != null ? Mathf.Clamp(w.bulletsLeft - 1, 0, 2) : 2;   // se ven las que están cargadas de verdad
            if (w != null && w.bulletsLeft < 1) extra = Mathf.Clamp(w.ammo - 1, 0, 2);   // durante la recarga (cargador aún en 0) entran las que alcancen
            for (int i = 0; i < 2; i++)
            {
                if (laterales[i] == null)
                {
                    if (!triple) continue;
                    var g = (GameObject)Object.Instantiate(d.Flecha.gameObject);
                    g.name = "coop_flecha_lateral_" + i;
                    foreach (Transform h in g.transform) Object.Destroy(h.gameObject);
                    laterales[i] = g.transform;
                }
                var t = laterales[i];
                if (t.parent != d.Flecha.parent) t.SetParent(d.Flecha.parent, false);
                bool ver = triple && d.Flecha.gameObject.activeSelf && i < extra;
                if (t.gameObject.activeSelf != ver) t.gameObject.SetActive(ver);
                if (!ver || cam == null) continue;
                // haz de 3: las dos extra apoyadas encima de la central, una algo a la izquierda y otra algo a la derecha, todas paralelas
                var pa = d.Flecha.parent;
                float lado = i == 0 ? -1f : 1f;
                Vector3 offMundo = cam.transform.up * anchoFlecha * 0.62f + cam.transform.right * lado * anchoFlecha * 0.42f;
                t.localPosition = d.Flecha.localPosition + (pa != null ? pa.InverseTransformVector(offMundo) : offMundo);
                t.localRotation = d.Flecha.localRotation;
                t.localScale = d.Flecha.localScale;
                t.gameObject.layer = d.Flecha.gameObject.layer;
            }
        }

        /// Tecla X (la misma del modo de la escopeta) con la ballesta en la mano: 1 flecha o 3 flechas.
        static void AlternarModo(PlayerWeapons pw)
        {
            if (!Copia.EnLaMano(pw)) return;
            if (!(Entrada.Enfocado && !Entrada.PanelAbierto && Controles.Capturando == null && Controles.Pulsada("modo_escopeta"))) return;
            if (!LibrosTienda.Tiene(LibrosTienda.BallestaTriple)) { Viaje.Avisar("Necesitas el libro «Disparo triple» para disparar 3 flechas", 2.5f); return; }
            ModoTriple = !ModoTriple;
            Viaje.Avisar(ModoTriple ? "Ballesta: 3 flechas por disparo" : "Ballesta: 1 flecha por disparo", 2.5f);
        }

        static void Animar(WeaponBehavior w)
        {
            var d = Copia.Din; if (d == null || d.Flecha == null) return;
            float t = Time.time, ang, slack, subida = 0f; bool flecha;
            // el juego sube el cargador un cuadro DESPUÉS de que termina el clip de recarga: sin esto, ese cuadro se veía con la pose de «vacía» (V)
            bool recargandoAntes = recargaVista;
            // la animación de recarga del propio arma marca el avance (reloadState del juego no se usa)
            bool recargando = false; float pAnim = 0f;
            try { var an = w.weaponMesh != null ? w.weaponMesh.GetComponent<Animation>() : null; if (an != null && an.IsPlaying("Reload")) { recargando = true; pAnim = Mathf.Clamp01(an["Reload"].normalizedTime); } } catch (System.Exception) { }
            if (recargando && !recargaVista) tRecarga = t;
            if (recargando != recargaVista || w.bulletsLeft != cargaAnt) Escopeta.plugin.Log("Ballesta anim: recargando=" + recargando + " cargador=" + w.bulletsLeft + " t=" + t.ToString("F2"));
            if (recargandoAntes && !recargando) tFinRecarga = t;
            cargaAnt = w.bulletsLeft; recargaVista = recargando;
            bool cargada = w.bulletsLeft >= 1 || t - tFinRecarga < 0.5f;
            if (recargando)
            {
                float p = pAnim;
                // el arma baja fuera de la vista durante casi toda la recarga: lo visible es el principio y el final, así que la flecha entra y los brazos se asientan al subir
                // parte A (0–3 s): el arma baja fuera de la vista y se coloca la flecha. Parte B (3–6 s): la mano jala la cuerda hasta la nuez.
                float tb = (p - 0.5f) * 6f;   // segundos dentro de la parte B
                float pull = Suave((tb - 0.9f) / 1.0f), relaja = 0f;
                ang = p < 0.5f ? AngFlojo : (Mathf.Lerp(AngFlojo, AngTenso, pull));
                slack = p < 0.5f ? 1f : 1f - pull;
                flecha = p >= 0.25f; subida = 1f - Suave((p - 0.25f) / 0.10f);
            }
            else if (cargada) { ang = AngTenso; slack = 0f;   // cargada: cuerda tensa y brazos horizontales hasta que se dispare
                flecha = true; }
            else { ang = AngFlojo; slack = 1f; flecha = false; }
            float dt = t - tFuego;
            if (dt >= 0f && dt < 1.2f && !recargando) ang += (AngDisparo - AngFlojo) * Mathf.Exp(-5.5f * dt) * Mathf.Cos(26f * dt);   // golpe hacia adelante y rebote amortiguado
            if (!float.IsNaN(AngManual)) ang = AngManual;
            if (!float.IsNaN(SlackManual)) slack = SlackManual;
            if (!float.IsNaN(FlechaManual)) { flecha = FlechaManual > 0f; subida = Mathf.Clamp01(FlechaManual - 1f); }
            float giro = 0f;
            if (recargando && pAnim >= 0.5f) { float tg = (pAnim - 0.5f) * 6f; giro = GiroMax * Suave((tg - 0.9f) / 1.0f); }   // últimos 3 s: se abren a la horizontal mientras se jala la cuerda y se quedan así
            else if (!recargando && cargada) giro = GiroMax;   // cargada: horizontales hasta el disparo
            else if (!recargando && t - tFuego >= 0f && t - tFuego < 0.15f) giro = GiroMax * (1f - Suave((t - tFuego) / 0.15f));   // al disparar vuelven de golpe a la V
            if (!float.IsNaN(GiroManual)) giro = GiroManual;
            Pose(d, ang, slack, flecha, subida, giro);
            if (grabando) Grabar(t, recargando, pAnim);
        }

        // Grabación de cuadros fijos para revisar la animación (orden "ballesta grabar").
        static bool grabando; static int sigFuego, sigRecarga; static float[] tFuegoCap = { 0.02f, 0.07f, 0.13f, 0.20f, 0.30f, 0.45f, 0.70f };
        static float[] pRecargaCap = { 0.08f, 0.25f, 0.52f, 0.62f, 0.70f, 0.78f, 0.88f, 0.98f };
        public static void EmpezarGrabar() { grabando = true; sigFuego = 0; sigRecarga = 0; }
        static void Grabar(float t, bool recargando, float pAnim)
        {
            string dir = System.IO.Path.Combine(BepInEx.Paths.BepInExRootPath, "capturas");
            float dt = t - tFuego;
            if (dt >= 0f && dt < 5f && !recargando && sigFuego < tFuegoCap.Length && dt >= tFuegoCap[sigFuego])
            { Application.CaptureScreenshot(System.IO.Path.Combine(dir, "anim_fuego_" + sigFuego + ".png")); sigFuego++; }
            if (recargando && sigRecarga < pRecargaCap.Length && pAnim >= pRecargaCap[sigRecarga])
            { Application.CaptureScreenshot(System.IO.Path.Combine(dir, "anim_recarga_" + sigRecarga + ".png")); sigRecarga++; }
            if (sigFuego >= tFuegoCap.Length && sigRecarga >= pRecargaCap.Length) grabando = false;
        }

        public static float GiroManual = float.NaN, GiroMax = 58f, SignoGiro = -1f;   // giro de los brazos hacia afuera (grados) al colocar la flecha
        static void Pose(ArmaCopia.DinBallesta d, float ang, float slack, bool flecha, float subida, float giro = 0f)
        {
            var q = Quaternion.AngleAxis(-ang, d.L);   // sentido + = punta hacia adelante (D)
            var punta = new Vector3[2];
            for (int i = 0; i < 2; i++)
            {
                // el brazo derecho (i = 0) gira hacia la derecha y el izquierdo (i = 1) hacia la izquierda, sobre el eje de avance
                var qi = Quaternion.AngleAxis((i == 0 ? -1f : 1f) * SignoGiro * giro, d.D) * q;
                d.Brazo[i].localRotation = qi; punta[i] = d.Base[i] + qi * d.PuntaOff[i];
            }
            Vector3 flojo = d.Frente;   // cuerda floja: de cada punta baja a un punto delante del soporte, en medio de los dos brazos
            Vector3 nuez = Vector3.Lerp(d.Nuez, flojo, Mathf.Clamp01(slack)); PuntoCuerda = nuez;
            for (int i = 0; i < 2; i++)
            {
                Vector3 v = nuez - punta[i]; float len = v.magnitude;
                var c = d.Cuerda[i]; c.localPosition = punta[i];
                if (len > 1e-6f) c.localRotation = Quaternion.LookRotation(v / len, d.Up);
                c.localScale = new Vector3(1f, 1f, len);
            }
            d.Flecha.gameObject.SetActive(flecha);
            d.Flecha.localPosition = d.Up * (0.045f * d.U * subida);
        }

        public static string Giro(string a, string signo) { float v; GiroManual = float.TryParse(a, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v) ? v : float.NaN; if (signo == "-") SignoGiro = -1f; else if (signo == "+") SignoGiro = 1f; return "giro=" + GiroManual + " signo=" + SignoGiro; }
        public static string Pose(string a, string sl, string fl)
        {
            float v;
            AngManual = float.TryParse(a, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v) ? v : float.NaN;
            SlackManual = float.TryParse(sl, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v) ? v : float.NaN;
            FlechaManual = float.TryParse(fl, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v) ? v : float.NaN;
            return "pose ang=" + AngManual + " slack=" + SlackManual + " flecha=" + FlechaManual;
        }

        public static void Actualizar()
        {
            var w = Copia.Actualizar();
            try { MejorarSueltas(); } catch (System.Exception e) { Registro("Ballesta: sueltas " + e.Message); }
            if (w == null) { cargaVista = -1; MoverFlechas(); return; }
            bool triple = TripleActivo;
            w.projectileCount = 1;
            // modo de 3 flechas: el cargador es de 3 (el contador muestra 3, el disparo gasta las 3 y la recarga pone 3); modo de 1: cargador de 1
            w.bulletsPerClip = triple ? 3 : 1;
            w.range = 0.05f;   // el disparo del juego no pega: el daño lo hacen las flechas al llegar
            AnimBallesta.Asegurar();
            w.reloadAnimSpeed = AnimBallesta.Largo / Mathf.Max(0.5f, w.reloadTime);
            SinPolvora(w);
            var pw = Rendimiento.Armas;
            try { AlternarModo(pw); } catch (System.Exception e) { Registro("Ballesta: modo " + e.Message); }
            if (Copia.EnLaMano(pw))
            {
                // Disparo: el cargador baja en 1 (el juego gasta una); en modo de 3 salen todas las cargadas (hasta 3) y el cargador queda en 0.
                if (cargaVista >= 1 && w.bulletsLeft == cargaVista - 1 && !recargaVista)
                {
                    tFuego = Time.time; tFinRecarga = -99f;
                    int n = triple ? Mathf.Clamp(cargaVista, 1, 3) : 1;
                    if (triple) w.bulletsLeft = 0;
                    try { Lanzar(n); } catch (System.Exception e) { Escopeta.plugin.Log("Ballesta: error al lanzar " + e); }
                }
                cargaVista = w.bulletsLeft;
            }
            else cargaVista = -1;
            try { Animar(w); } catch (System.Exception e) { Escopeta.plugin.Log("Ballesta: error de animación " + e); }
            try { FlechasTriple(w); } catch (System.Exception e) { Escopeta.plugin.Log("Ballesta: error en las flechas del disparo triple " + e.Message); }
            MoverFlechas();
        }

        // ---------------- Sin humo ni estampido de mosquete ----------------

        static GameObject silenciada; static bool sonidoBuscado;
        static void SinPolvora(WeaponBehavior w)
        {
            if (silenciada == Copia.Arma) return;
            silenciada = Copia.Arma;
            w.useBarrelSmoke = false;
            foreach (var ps in Copia.Arma.GetComponentsInChildren<ParticleSystem>(true)) { var em = ps.emission; em.enabled = false; }
            foreach (var pe in Copia.Arma.GetComponentsInChildren<ParticleEmitter>(true)) { pe.emit = false; pe.enabled = false; }
            foreach (var l in Copia.Arma.GetComponentsInChildren<Light>(true)) l.enabled = false;
            if (!sonidoBuscado)
            {
                sonidoBuscado = true;
                // Un chasquido seco del juego en vez del disparo: lo más parecido que haya a una ballesta.
                string[] claves = { "crossbow", "bow", "arrow", "whoosh", "swish", "swing", "woosh", "throw" };
                var clips = Resources.FindObjectsOfTypeAll<AudioClip>();
                foreach (var k in claves)
                {
                    foreach (var c in clips) if (c.name.ToLowerInvariant().Contains(k)) { sonido = c; break; }
                    if (sonido != null) break;
                }
                var lista = new System.Text.StringBuilder();
                foreach (var c in clips) { string n = c.name.ToLowerInvariant(); if (n.Contains("swing") || n.Contains("bow") || n.Contains("whoosh") || n.Contains("swish") || n.Contains("hit") || n.Contains("click")) lista.Append(c.name).Append(' '); }
                Escopeta.plugin.Log("Ballesta: sonido de disparo = " + (sonido != null ? sonido.name : "(ninguno)") + " | candidatos: " + lista);
            }
            if (sonido != null) w.fireSnd = sonido;
        }
        static AudioClip sonido;

        // ---------------- Flechas que se ven ----------------

        class Flecha { public GameObject Go; public Vector3 Hasta; public bool Clavada; public float Fin; public CharacterDamagePlaymakerAI Zona; public DamageReceiverObject Receptor; public float Dano; }
        static readonly List<Flecha> flechas = new List<Flecha>();
        static Mesh mallaFlecha; static Material matFlecha;

        static void Lanzar(int n) { Lanzar(n, Copia.Arma != null ? Copia.Arma.GetComponent<WeaponBehavior>() : null); }

        /// La flecha hace el daño al llegar (el disparo instantáneo del juego no se usa: alcance casi nulo).
        static void Lanzar(int n, WeaponBehavior wb)
        {
            var cam = Camera.main;
            if (cam == null) { Escopeta.plugin.Log("Ballesta: sin cámara para la flecha"); return; }
            var j = Escopeta.plugin != null ? Escopeta.plugin.Jugador : null;
            for (int k = 0; k < n; k++)
            {
                // haz: la central y las otras apoyadas encima (izquierda y derecha), paralelas: vuelan juntas
                Vector3 off = k == 0 ? Vector3.zero : cam.transform.up * 0.045f + cam.transform.right * (k == 1 ? -0.035f : 0.035f);
                Vector3 desde = cam.transform.position + cam.transform.right * 0.12f - cam.transform.up * 0.08f + cam.transform.forward * 0.5f + off;
                Vector3 hasta = cam.transform.position + cam.transform.forward * Alcance + off;
                Transform pego = null; CharacterDamagePlaymakerAI zona = null; DamageReceiverObject receptor = null;
                int mascara = wb != null && wb.bulletMask.value != 0 ? wb.bulletMask.value : ~0;
                foreach (var h in OrdenadosPorDistancia(Physics.RaycastAll(cam.transform.position + off, cam.transform.forward, Alcance, mascara, QueryTriggerInteraction.Ignore)))
                {
                    if (j != null && h.collider.transform.IsChildOf(j.transform.root)) continue;
                    if (h.collider.transform.root.name.StartsWith("coop_aliado")) continue;
                    hasta = h.point; pego = h.collider.transform;
                    zona = h.collider.GetComponent<CharacterDamagePlaymakerAI>();
                    receptor = h.collider.GetComponent<DamageReceiverObject>();   // jefe de la cueva (JefeCueva)
                    break;
                }
                var go = NuevaFlecha("coop_flecha");
                go.transform.position = desde;
                go.transform.rotation = Quaternion.LookRotation(hasta - desde) * Quaternion.Euler(0f, -90f, 0f);   // X del modelo hacia el blanco
                var f = new Flecha { Go = go, Hasta = hasta, Fin = Time.time + 25f, Zona = zona, Receptor = receptor, Dano = DanoActual() };
                if (pego != null) Escopeta.plugin.Log("Ballesta: flecha hacia " + pego.name + (zona != null ? " (enemigo)" : ""));
                flechas.Add(f);
                if (pego != null) { var p = pego; f.Go.AddComponent<Clavar>().Destino = p; }
            }
            Escopeta.plugin.Log("Ballesta: " + n + " flecha(s) lanzada(s)" + (flechas.Count > 0 ? " hacia " + (flechas[flechas.Count - 1].Hasta - Anclaje.Origen()).ToString("F1") : ""));
            while (flechas.Count > 40) { if (flechas[0].Go != null) Object.Destroy(flechas[0].Go); flechas.RemoveAt(0); }
        }

        /// Daño de una flecha: 300, +50 % con Puntas de acero.
        static float DanoActual() { return Dano * (LibrosTienda.Tiene(LibrosTienda.BallestaDano) ? 1.5f : 1f); }

        static RaycastHit[] OrdenadosPorDistancia(RaycastHit[] h) { System.Array.Sort(h, (a, b) => a.distance.CompareTo(b.distance)); return h; }

        /// Al llegar, la flecha queda hija de lo que golpeó (un monstruo que se mueve se la lleva).
        class Clavar : MonoBehaviour { public Transform Destino; }

        static void MoverFlechas()
        {
            for (int i = flechas.Count - 1; i >= 0; i--)
            {
                var f = flechas[i];
                if (f.Go == null || Time.time > f.Fin) { if (f.Go != null) Object.Destroy(f.Go); flechas.RemoveAt(i); continue; }
                if (f.Clavada) continue;
                Vector3 p = Vector3.MoveTowards(f.Go.transform.position, f.Hasta, VelocidadFlecha * Time.deltaTime);
                f.Go.transform.position = p;
                if ((p - f.Hasta).sqrMagnitude < 0.0001f)
                {
                    f.Clavada = true;
                    f.Go.transform.position = f.Hasta - f.Go.transform.right * 0.12f;   // media flecha hundida
                    var c = f.Go.GetComponent<Clavar>();
                    if (c != null && c.Destino != null) f.Go.transform.SetParent(c.Destino, true);
                    if (f.Zona != null && f.Zona.gameObject.activeInHierarchy) f.Zona.ApplyDamage(f.Dano, false);   // el combate compartido lo pasa al dueño del monstruo
                    if (f.Receptor != null && f.Receptor.gameObject.activeInHierarchy) f.Receptor.ApplyDamage(f.Dano);
                }
            }
        }

        /// Flecha suelta (para disparar o para dejar en el mundo).
        public static GameObject NuevaFlecha(string nombre)
        {
            if (mallaFlecha == null) mallaFlecha = MallaFlecha();
            if (matFlecha == null)
            {
                matFlecha = new Material(Shader.Find("Standard")) { name = "coop_flecha" };
                matFlecha.mainTexture = Escopeta.TexturaEscopeta();
                matFlecha.SetFloat("_Glossiness", 0.3f);
            }
            var go = new GameObject(nombre);
            go.AddComponent<MeshFilter>().sharedMesh = mallaFlecha;
            var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = matFlecha;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        /// La ballesta suelta en el mundo (al fondo de las minas).
        public static GameObject Modelo(string nombre)
        {
            var g = new GameObject(nombre);
            var hijo = new GameObject("malla");
            hijo.transform.SetParent(g.transform, false);
            hijo.transform.localScale = Vector3.one * 1.1f;
            hijo.transform.localPosition = new Vector3(-0.12f, 0f, 0f);
            hijo.AddComponent<MeshFilter>().sharedMesh = mallaSuelta ?? (mallaSuelta = MallaBallesta());
            sueltas.Add(g);   // se cambia al modelo nuevo (el de la ballesta en la mano) en cuanto el arma esté creada
            var mr = hijo.AddComponent<MeshRenderer>();
            if (matSuelto == null)
            {
                matSuelto = new Material(Shader.Find("Standard")) { name = "coop_ballesta_suelta" };
                matSuelto.mainTexture = Escopeta.TexturaEscopeta();
                matSuelto.SetFloat("_Glossiness", 0.3f);
            }
            mr.sharedMaterial = matSuelto;
            return g;
        }
        static Mesh mallaSuelta; static Material matSuelto;
        static readonly List<GameObject> sueltas = new List<GameObject>();

        /// La ballesta suelta (minas) con el MISMO modelo que la del jugador (pedido 2026-11-17: seguía con la piel vieja):
        /// copia del modelo del arma (cuerpo, brazos, cuerdas y flecha) sin lógica, de ~85 cm, tendida en el suelo.
        static void MejorarSueltas()
        {
            sueltas.RemoveAll(x => x == null);
            if (sueltas.Count == 0 || Copia.Modelo == null) return;
            int n = 0;
            foreach (var g in sueltas) if (ArmaSuelta.Poner(g, Copia.Modelo, 2.0f, false)) n++;   // tendida, no ladeada (2026-11-17)
            if (n > 0) Registro("Ballesta: " + n + " sueltas con el modelo nuevo");
        }

        // ---------------- Modelos hechos aquí ----------------

        static readonly Vector2 Madera = new Vector2(0.25f, 0.5f), Metal = new Vector2(0.75f, 0.5f), Cuerda = new Vector2(0.30f, 0.9f);

        /// Barra recta entre dos puntos (sección alto × ancho, "arriba" = Y).
        static void Barra(List<Vector3> v, List<Vector2> uv, List<int> tr, Vector3 a, Vector3 b, float alto, float ancho, Vector2 col)
        {
            Vector3 d = (b - a); if (d.sqrMagnitude < 1e-8f) return;
            Vector3 lado = Vector3.Cross(Vector3.up, d.normalized);
            if (lado.sqrMagnitude < 1e-6f) lado = Vector3.forward;
            lado = lado.normalized * ancho * 0.5f;
            Vector3 up = Vector3.Cross(d.normalized, lado.normalized).normalized * alto * 0.5f;
            Vector3 a0 = a - lado - up, a1 = a + lado - up, a2 = a + lado + up, a3 = a - lado + up;
            Vector3 b0 = b - lado - up, b1 = b + lado - up, b2 = b + lado + up, b3 = b - lado + up;
            Escopeta.Cara(v, uv, tr, a0, a3, a2, a1, col); Escopeta.Cara(v, uv, tr, b0, b1, b2, b3, col);
            Escopeta.Cara(v, uv, tr, a0, a1, b1, b0, col); Escopeta.Cara(v, uv, tr, a1, a2, b2, b1, col);
            Escopeta.Cara(v, uv, tr, a2, a3, b3, b2, col); Escopeta.Cara(v, uv, tr, a3, a0, b0, b3, col);
        }

        /// Ballesta: culata, riel, arco de acero, cuerda tensada, estribo, gatillo y una flecha cargada.
        /// X hacia adelante, Y arriba, origen en la empuñadura; largo total ~0,8 m.
        internal static Mesh MallaBallesta()
        {
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var tr = new List<int>();
            // culata (perfil en XY)
            Escopeta.Prisma(v, uv, tr, new[] { new Vector2(-0.30f, -0.075f), new Vector2(-0.30f, 0.02f), new Vector2(-0.10f, 0.03f), new Vector2(0.46f, 0.03f),
                                               new Vector2(0.46f, 0.0f), new Vector2(-0.03f, -0.012f), new Vector2(-0.10f, -0.06f) }, 0.04f, Madera);
            Escopeta.Caja(v, uv, tr, new Vector3(-0.02f, 0.03f, -0.007f), new Vector3(0.46f, 0.04f, 0.007f), Metal);        // riel
            Escopeta.Caja(v, uv, tr, new Vector3(0.11f, 0.03f, -0.015f), new Vector3(0.15f, 0.055f, 0.015f), Metal);        // nuez
            // arco: dos tramos por lado, curvado hacia atrás
            for (int s = -1; s <= 1; s += 2)
            {
                Barra(v, uv, tr, new Vector3(0.46f, 0.035f, 0f), new Vector3(0.45f, 0.035f, s * 0.17f), 0.026f, 0.022f, Metal);
                Barra(v, uv, tr, new Vector3(0.45f, 0.035f, s * 0.17f), new Vector3(0.40f, 0.035f, s * 0.31f), 0.022f, 0.018f, Metal);
                Barra(v, uv, tr, new Vector3(0.40f, 0.04f, s * 0.31f), new Vector3(0.13f, 0.045f, 0f), 0.005f, 0.005f, Cuerda);   // cuerda
            }
            // estribo delante del arco
            Barra(v, uv, tr, new Vector3(0.46f, 0.0f, -0.035f), new Vector3(0.56f, -0.01f, -0.035f), 0.012f, 0.012f, Metal);
            Barra(v, uv, tr, new Vector3(0.46f, 0.0f, 0.035f), new Vector3(0.56f, -0.01f, 0.035f), 0.012f, 0.012f, Metal);
            Barra(v, uv, tr, new Vector3(0.56f, -0.01f, -0.04f), new Vector3(0.56f, -0.01f, 0.04f), 0.012f, 0.012f, Metal);
            // gatillo largo bajo la culata
            Barra(v, uv, tr, new Vector3(0.02f, -0.01f, 0f), new Vector3(-0.06f, -0.05f, 0f), 0.008f, 0.008f, Metal);
            // flecha cargada
            AgregarFlecha(v, uv, tr, new Vector3(0.13f, 0.05f, 0f), 0.36f);
            var m = new Mesh { name = "coop_ballesta" };
            m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(tr, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        static void AgregarFlecha(List<Vector3> v, List<Vector2> uv, List<int> tr, Vector3 cola, float largo)
        {
            Barra(v, uv, tr, cola, cola + new Vector3(largo, 0f, 0f), 0.009f, 0.009f, Madera);
            Vector3 punta = cola + new Vector3(largo, 0f, 0f);
            Barra(v, uv, tr, punta, punta + new Vector3(0.04f, 0f, 0f), 0.016f, 0.016f, Metal);
            Barra(v, uv, tr, punta + new Vector3(0.04f, 0f, 0f), punta + new Vector3(0.06f, 0f, 0f), 0.006f, 0.006f, Metal);
            // plumas
            Barra(v, uv, tr, cola + new Vector3(0.01f, 0.012f, 0f), cola + new Vector3(0.07f, 0.012f, 0f), 0.016f, 0.002f, Cuerda);
            Barra(v, uv, tr, cola + new Vector3(0.01f, 0f, 0.012f), cola + new Vector3(0.07f, 0f, 0.012f), 0.002f, 0.016f, Cuerda);
            Barra(v, uv, tr, cola + new Vector3(0.01f, 0f, -0.012f), cola + new Vector3(0.07f, 0f, -0.012f), 0.002f, 0.016f, Cuerda);
        }

        /// Flecha sola (0,42 m), cola en el origen, punta hacia +X.
        static Mesh MallaFlecha()
        {
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var tr = new List<int>();
            AgregarFlecha(v, uv, tr, Vector3.zero, 0.36f);
            var m = new Mesh { name = "coop_flecha" };
            m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(tr, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        public static string Describir() { return Copia.Describir(); }
    }
}

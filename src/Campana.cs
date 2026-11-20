// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using System.IO;
using UnityEngine;

namespace WitchHuntCoop
{
    /// Reproduce la campanada de la Luna de sangre (ver CampanaSintesis: generada por el mod, sin sonidos ajenos).
    public static class Campana
    {
        static AudioClip clip;
        static AudioSource fuente;

        // Sonido propio del jugador (no viene con el mod): BepInEx/plugins/sonidos_coop/campana_luna.ogg o .wav.
        public const string Carpeta = "sonidos_coop", Archivo = "campana_luna";
        static WWW carga; static AudioClip propio; static string rutaPropia;

        /// Grabación que trae el mod: campanas de iglesia reales de dominio público ("Church Bells",
        /// Leverkusen 2007, de natalie, pdsounds.org). "natural" o "oscura" (más lenta y grave, con eco).
        public const string PrefVariante = "coop_campana_entrada";   // (nombre nuevo: la entrada es la versión oscura)
        public static string Variante { get { return PlayerPrefs.GetString(PrefVariante, "oscura"); } set { PlayerPrefs.SetString(PrefVariante, value); PlayerPrefs.Save(); } }

        /// Al abrir el juego: primero el sonido propio del jugador (si puso uno); si no, la grabación del mod.
        public static void BuscarPropio(Plugin plugin)
        {
            try { PrepararUnica(plugin); } catch (System.Exception e) { plugin.Log("Campana: campana sola: " + e.Message); }
            try
            {
                string dir = Path.Combine(BepInEx.Paths.PluginPath, Carpeta);
                Directory.CreateDirectory(dir);
                string leeme = Path.Combine(dir, "LEEME.txt");
                if (!File.Exists(leeme))
                    File.WriteAllText(leeme, "Pon aquí tu propio sonido para la Luna de sangre con el nombre campana_luna.ogg o campana_luna.wav\r\n(MP3 no sirve: conviértelo a OGG o WAV). Si no hay archivo, suenan las campanas de iglesia del mod.\r\n");
                foreach (var ext in new[] { ".ogg", ".wav" })
                {
                    string r = Path.Combine(dir, Archivo + ext);
                    if (!File.Exists(r)) continue;
                    Cargar(r);
                    plugin.Log("Campana: se usará el sonido propio " + r);
                    return;
                }
                // La grabación del mod: se saca de la DLL a la caché y se carga como cualquier OGG.
                string nombre = "sonidos.campana_iglesia_" + Variante + ".ogg";
                using (var st = typeof(Campana).Assembly.GetManifestResourceStream(nombre))
                {
                    if (st == null) { plugin.Log("Campana: no está " + nombre + "; suena la campana generada"); return; }
                    string cache = Path.Combine(BepInEx.Paths.CachePath, "coop_campana_" + Variante + ".ogg");
                    Directory.CreateDirectory(Path.GetDirectoryName(cache));
                    var buf = new byte[st.Length]; int leidos = 0;
                    while (leidos < buf.Length) { int k = st.Read(buf, leidos, buf.Length - leidos); if (k <= 0) break; leidos += k; }
                    File.WriteAllBytes(cache, buf);
                    Cargar(cache);
                    plugin.Log("Campana: campanas de iglesia del mod (" + Variante + ")");
                }
            }
            catch (System.Exception e) { plugin.Log("Campana: no se pudo preparar el sonido: " + e.Message); }
        }

        // La campana sola de la catedral de Colonia (Petersglocke, CC0), un golpe por segundo durante 1 minuto.
        static WWW cargaUnica; static AudioClip unica;

        static void PrepararUnica(Plugin plugin)
        {
            if (unica != null || cargaUnica != null) return;
            using (var st = typeof(Campana).Assembly.GetManifestResourceStream("sonidos.campana_iglesia_unica.ogg"))
            {
                if (st == null) return;
                string cache = Path.Combine(BepInEx.Paths.CachePath, "coop_campana_unica.ogg");
                Directory.CreateDirectory(Path.GetDirectoryName(cache));
                var buf = new byte[st.Length]; int leidos = 0;
                while (leidos < buf.Length) { int k = st.Read(buf, leidos, buf.Length - leidos); if (k <= 0) break; leidos += k; }
                File.WriteAllBytes(cache, buf);
                cargaUnica = new WWW("file:///" + cache.Replace('\\', '/'));
                plugin.Log("Campana: campana sola (1 minuto) lista para cargar");
            }
        }

        public static AudioClip ClipUnica()
        {
            if (unica != null) return unica;
            if (cargaUnica != null && cargaUnica.isDone && string.IsNullOrEmpty(cargaUnica.error))
            {
                var c = cargaUnica.GetAudioClip(false, false);
                if (c != null && c.loadState == AudioDataLoadState.Loaded && c.length > 0f) { unica = c; cargaUnica = null; }
            }
            return unica;
        }

        /// La campana sola durante un minuto (si no está, el repique normal).
        public static void TocarUnica(float volumen = 1f)
        {
            var c = ClipUnica();
            if (c == null) { Tocar(volumen); return; }
            var cam = Camera.main;
            if (cam == null) return;
            Fuente(cam).PlayOneShot(c, volumen * Opciones.FactorVolumen);
        }

        static AudioSource Fuente(Camera cam)
        {
            if (fuente == null || fuente.gameObject != cam.gameObject)
            {
                fuente = Registrar(cam.gameObject.AddComponent<AudioSource>());
                fuente.playOnAwake = false; fuente.spatialBlend = 0f; fuente.loop = false; fuente.priority = 0;
            }
            return fuente;
        }

        // ---------------- La secuencia de la Luna de sangre ----------------

        // todas las fuentes de sonido que se hayan creado (si la cámara cambia, las viejas siguen sonando: hay que pararlas todas)
        static readonly System.Collections.Generic.List<AudioSource> todas = new System.Collections.Generic.List<AudioSource>();
        static AudioSource Registrar(AudioSource a) { todas.Add(a); return a; }
        static AudioSource fondoA, fondoB; static bool fondoActivo, usarA; static float proximoFondo, volFondo = 0.8f;
        public const float CicloFondo = 60f;   // la campana sola: 30 golpes cada 2 s; se encadena cada 60 s sin que se note

        /// La entrada (campana iglesia oscura o el sonido propio del jugador).
        public static void TocarEntrada(float volumen = 1f) { Tocar(volumen, 1f); }

        /// Lo que dura la entrada (la campana sola empieza justo cuando termina).
        public static float DuracionEntrada() { var c = Clip(); return c != null && c.length > 1f ? c.length : 20f; }

        /// La campana sola empieza dentro de "segundos" y sigue en bucle hasta Detener().
        public static void ProgramarFondo(float segundos, float volumen)
        {
            fondoActivo = true; volFondo = volumen; proximoFondo = Time.realtimeSinceStartup + segundos; usarA = true;
        }

        public static string Estado()
        {
            var c = ClipUnica();
            return "fondo=" + fondoActivo + " próximo en " + (proximoFondo - Time.realtimeSinceStartup).ToString("0.0") + " s, clip sola=" + (c != null ? c.length.ToString("0.0") + " s " + c.loadState : "null") + (cargaUnica != null ? " (cargando: " + cargaUnica.isDone + " " + cargaUnica.error + ")" : "") + ", cámara=" + (Camera.main != null) + ", A=" + (fondoA != null && fondoA.isPlaying) + " B=" + (fondoB != null && fondoB.isPlaying);
        }

        public static void ActualizarFondo()
        {
            if (!fondoActivo || Time.realtimeSinceStartup < proximoFondo) return;
            var c = ClipUnica(); var cam = Camera.main;
            if (c == null || cam == null) { proximoFondo = Time.realtimeSinceStartup + 1f; return; }
            // dos fuentes que se turnan: la cola de una copia se solapa con el comienzo de la siguiente
            var f = usarA ? (fondoA != null && fondoA.gameObject == cam.gameObject ? fondoA : (fondoA = NuevaFuente(cam))) : (fondoB != null && fondoB.gameObject == cam.gameObject ? fondoB : (fondoB = NuevaFuente(cam)));
            usarA = !usarA;
            f.Stop(); f.clip = c; f.volume = volFondo * Opciones.FactorVolumen; f.Play();
            Debug.Log("[WitchHuntCoop] Campana: campana sola de fondo (" + (usarA ? "B" : "A") + ") a los " + Time.realtimeSinceStartup.ToString("0.0") + " s");
            proximoFondo += CicloFondo;
            if (proximoFondo < Time.realtimeSinceStartup) proximoFondo = Time.realtimeSinceStartup + CicloFondo;
        }

        static AudioSource NuevaFuente(Camera cam)
        {
            var f = Registrar(cam.gameObject.AddComponent<AudioSource>());
            f.playOnAwake = false; f.spatialBlend = 0f; f.loop = false; f.priority = 0;
            return f;
        }

        /// Se acaba la luna: callan todas las campanas.
        public static void Detener()
        {
            fondoActivo = false;
            if (fondoA != null) fondoA.Stop();
            if (fondoB != null) fondoB.Stop();
            if (fuente != null) fuente.Stop();
            foreach (var a in todas) if (a != null) a.Stop();   // también las de cámaras anteriores
            todas.RemoveAll(a => a == null);
        }

        static void Cargar(string ruta)
        {
            rutaPropia = ruta; propio = null;
            carga = new WWW("file:///" + ruta.Replace('\\', '/'));
        }

        /// Cambia entre la versión natural y la oscura (para las pruebas y para elegir).
        public static void CambiarVariante(Plugin plugin, string v) { Variante = v; BuscarPropio(plugin); }

        public static AudioClip Clip()
        {
            if (propio != null) return propio;
            if (carga != null && carga.isDone)
            {
                if (string.IsNullOrEmpty(carga.error))
                {
                    var c = carga.GetAudioClip(false, false);
                    if (c != null && c.loadState == AudioDataLoadState.Loaded && c.length > 0f) { propio = c; carga = null; return propio; }
                    if (c != null && c.loadState == AudioDataLoadState.Loading) return Sintetica();   // aún cargando
                }
                Debug.Log("[WitchHuntCoop] Campana: no se pudo leer " + rutaPropia + " (" + carga.error + "); suena la del mod");
                carga = null;
            }
            return Sintetica();
        }

        static AudioClip Sintetica()
        {
            if (clip != null) return clip;
            var datos = CampanaSintesis.Generar();
            clip = AudioClip.Create("coop_campana", datos.Length, 1, CampanaSintesis.Frecuencia, false);
            clip.SetData(datos, 0);
            return clip;
        }

        /// Suena en los oídos del jugador (sin posición), con un poco de variación de tono.
        public static void Tocar(float volumen = 1f, float tono = 1f)
        {
            var cam = Camera.main;
            if (cam == null) return;
            if (fuente == null || fuente.gameObject != cam.gameObject)
            {
                fuente = Registrar(cam.gameObject.AddComponent<AudioSource>());
                fuente.playOnAwake = false; fuente.spatialBlend = 0f; fuente.loop = false;
                fuente.priority = 0;
            }
            fuente.pitch = tono;
            fuente.PlayOneShot(Clip(), volumen * Opciones.FactorVolumen);
        }

        /// Para escucharla fuera del juego: la guarda como WAV.
        public static string GuardarWav()
        {
            string ruta = Path.Combine(Path.Combine(BepInEx.Paths.BepInExRootPath, "capturas"), "campana_luna.wav");
            Directory.CreateDirectory(Path.GetDirectoryName(ruta));
            File.WriteAllBytes(ruta, CampanaSintesis.Wav(CampanaSintesis.Generar()));
            return ruta;
        }
    }
}

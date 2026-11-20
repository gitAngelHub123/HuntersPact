// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace WitchHuntCoop
{
    /// Opciones del cooperativo (se guardan en PlayerPrefs, como las del juego) y su aplicación.
    ///  Rendimiento: distancia de dibujado, densidad del bosque, sombras, escala de resolución.
    ///  Cooperativo: mostrar nombre del compañero, dificultad y frecuencia de eventos (estas dos las
    ///  decide el anfitrión y se mandan al invitado).
    ///  Interfaz: contador de FPS, volumen de los efectos del mod, tamaño de los textos del mod.
    public static class Opciones
    {
        public static Plugin plugin;

        static int Leer(string k, int def) { return PlayerPrefs.GetInt("coop_op_" + k, def); }
        static void Escribir(string k, int v) { PlayerPrefs.SetInt("coop_op_" + k, v); PlayerPrefs.Save(); }

        // (índices: 0 = lo más alto; en el panel se muestran de más bajo a más alto)
        public static readonly string[] NombresDistancia = { "Máxima", "Lejana", "Media", "Corta" };
        public static readonly float[] Distancias = { 0f, 400f, 300f, 200f };   // la cámara del juego ve hasta 500 m
        public static readonly string[] NombresDensidad = { "Alta", "Media", "Baja" };
        public static readonly string[] NombresSombras = { "Altas", "Bajas", "Apagadas" };
        public const string VarFuegoAmigo = "coop_fuego_amigo";   // se elige al crear la partida
        public static int FuegoAmigoElegido = -1;
        static bool fuegoMio { get { try { return PixelCrushers.DialogueSystem.DialogueLua.GetVariable(VarFuegoAmigo).AsBool; } catch { return false; } } }
        static int fuegoRecibido = -1;
        public static bool FuegoAmigo { get { return plugin != null && plugin.Conectado && !plugin.EsAnfitrion && fuegoRecibido >= 0 ? fuegoRecibido == 1 : fuegoMio; } }
        public const string VarDificultad = "coop_dificultad";   // se elige al crear la partida y queda en ella
        public static readonly string[] NombresDificultad = { "Normal", "Difícil", "Pesadilla" };
        public static readonly float[] FactoresDificultad = { 1f, 1.5f, 2f };
        public static readonly string[] NombresEventos = { "Apagados", "Pocos", "Normal", "Frecuentes" };
        public static readonly float[] FactoresEventos = { 0f, 0.5f, 1f, 3f };
        public static readonly string[] NombresTexto = { "Normal", "Grande", "Muy grande" };
        public static readonly float[] EscalasTexto = { 1f, 1.25f, 1.5f };

        public static int Distancia { get { return Leer("distancia", 0); } set { Escribir("distancia", value); } }
        public static int Densidad { get { return Leer("densidad", 0); } set { Escribir("densidad", value); } }
        public static int Sombras { get { return Leer("sombras", 0); } set { Escribir("sombras", value); } }
        /// Escala de resolución en %, de 50 a 150 (100 = normal).
        public static int EscalaPct { get { return Mathf.Clamp(Leer("escala_pct", 100), 50, 150); } set { Escribir("escala_pct", Mathf.Clamp(value, 50, 150)); } }
        public static bool ContadorFps { get { return Leer("contador_fps", 0) == 1; } set { Escribir("contador_fps", value ? 1 : 0); } }
        public static bool NombreCompanero { get { return Leer("nombre_companero", 1) == 1; } set { Escribir("nombre_companero", value ? 1 : 0); } }
        /// Volumen general de TODO el juego, de 0 a 100 % (se aplica con AudioListener.volume).
        public static int VolumenGeneral { get { return Mathf.Clamp(Leer("volumen_general", 100), 0, 100); } set { Escribir("volumen_general", Mathf.Clamp(value, 0, 100)); } }
        public static int VolumenEfectos { get { return Leer("volumen_efectos", 100); } set { Escribir("volumen_efectos", Mathf.Clamp(value, 0, 100)); } }
        public static int Brillo { get { return Mathf.Clamp(Leer("brillo", 50), 0, 100); } set { Escribir("brillo", Mathf.Clamp(value, 0, 100)); } }   // 50 = como el juego
        public static int Fov { get { return Mathf.Clamp(Leer("fov", 50), 0, 100); } set { Escribir("fov", Mathf.Clamp(value, 0, 100)); } }       // 50 = como el juego; 0 = −25°, 100 = +35°
        public static float FovDelta { get { int f = Fov; return f <= 50 ? (f - 50) * 0.5f : (f - 50) * 0.7f; } }
        public static int Texto { get { return Leer("texto", 0); } set { Escribir("texto", value); ReiniciarEstilos(); } }

        // Las decide el anfitrión: el invitado usa lo que le llega.
        /// La dificultad es de la partida: se elige al crearla (anfitrión) y queda guardada en ella.
        static int dificultadMia
        {
            get
            {
                try { var v = PixelCrushers.DialogueSystem.DialogueLua.GetVariable(VarDificultad); if (v.AsString != "nil" && v.AsString != "") return Mathf.Clamp(v.AsInt, 0, 2); } catch { }
                return 0;
            }
        }
        /// La que eligió el anfitrión en "Partida nueva" (se guarda en la partida al empezarla).
        public static int DificultadElegida = -1;
        static int eventosMios { get { return Leer("eventos", 2); } }
        static int dificultadRecibida = -1, eventosRecibidos = -1;
        public static int Dificultad { get { return plugin != null && plugin.Conectado && !plugin.EsAnfitrion && dificultadRecibida >= 0 ? dificultadRecibida : dificultadMia; } set { PixelCrushers.DialogueSystem.DialogueLua.SetVariable(VarDificultad, Mathf.Clamp(value, 0, 2)); } }
        public static int Eventos { get { return plugin != null && plugin.Conectado && !plugin.EsAnfitrion && eventosRecibidos >= 0 ? eventosRecibidos : eventosMios; } set { Escribir("eventos", value); } }
        public static bool LasDecideOtro { get { return plugin != null && plugin.Conectado && !plugin.EsAnfitrion; } }

        public static float FactorDificultad { get { return FactoresDificultad[Mathf.Clamp(Dificultad, 0, 2)]; } }
        public static float FactorEventos { get { return FactoresEventos[Mathf.Clamp(Eventos, 0, 3)]; } }
        public static float FactorVolumen { get { return VolumenEfectos / 100f; } }
        public static int T(int tam) { return Mathf.RoundToInt(tam * EscalasTexto[Mathf.Clamp(Texto, 0, 2)]); }

        // ---------------- Red: el anfitrión manda dificultad y eventos ----------------

        static float proximoEnvio;
        public static void Enviar()
        {
            if (plugin == null || !plugin.Conectado || !plugin.EsAnfitrion || Time.realtimeSinceStartup < proximoEnvio) return;
            proximoEnvio = Time.realtimeSinceStartup + 2f;
            byte d = (byte)dificultadMia, e = (byte)eventosMios;
            plugin.EnviarAjustes(new[] { d, e, (byte)(fuegoMio ? 1 : 0) });
        }

        public static void Recibir(byte[] datos)
        {
            if (datos == null || datos.Length < 2) return;
            if (datos[0] != dificultadRecibida || datos[1] != eventosRecibidos)
                plugin.Log("Opciones: el anfitrión eligió dificultad " + NombresDificultad[Mathf.Clamp(datos[0], 0, 2)] + " y eventos " + NombresEventos[Mathf.Clamp(datos[1], 0, 3)]);
            dificultadRecibida = datos[0]; eventosRecibidos = datos[1];
            if (datos.Length >= 3) fuegoRecibido = datos[2];
        }

        // ---------------- Aplicar (cada cuadro, al final; solo toca lo que cambió) ----------------

        static readonly Dictionary<Camera, float> lejosOriginal = new Dictionary<Camera, float>();
        static readonly Dictionary<Terrain, float[]> terrenoOriginal = new Dictionary<Terrain, float[]>();
        static bool calidadGuardada; static ShadowQuality sombrasOriginal; static float distanciaSombras, lodOriginal; static ShadowResolution resolucionSombras;
        static float proximoTerreno;

        // ---- medición de FPS para diagnóstico (orden "medir [segundos]") ----
        static bool midiendo; static float medFin, medSuma, medPeor; static int medN; static readonly List<float> medTiempos = new List<float>();
        public static void Medir(float seg) { midiendo = true; medFin = Time.realtimeSinceStartup + seg; medSuma = 0f; medN = 0; medPeor = 0f; medTiempos.Clear(); }
        static void Medicion()
        {
            if (!midiendo) return;
            float dt = Time.unscaledDeltaTime; medSuma += dt; medN++; medTiempos.Add(dt); if (dt > medPeor) medPeor = dt;
            if (Time.realtimeSinceStartup < medFin) return;
            midiendo = false;
            medTiempos.Sort(); float p99 = medTiempos[Mathf.Min(medTiempos.Count - 1, (int)(medTiempos.Count * 0.99f))];
            plugin.Log("MEDICIÓN: " + (medN / medSuma).ToString("0.0") + " FPS medios (" + (medSuma / medN * 1000f).ToString("0.0") + " ms), p99 " + (p99 * 1000f).ToString("0.0") + " ms, peor " + (medPeor * 1000f).ToString("0.0") + " ms | límite coop " + Pantalla.Fps + ", targetFrameRate " + Application.targetFrameRate + ", vSyncCount " + QualitySettings.vSyncCount + ", monitor " + Screen.currentResolution.refreshRate + " Hz, pantalla " + Screen.width + "x" + Screen.height + " completa=" + Screen.fullScreen + ", escala " + EscalaPct + " %, sombras " + QualitySettings.shadows + ", calidad " + QualitySettings.GetQualityLevel());
        }

        public static void Aplicar()
        {
            Medicion();
            float vg = VolumenGeneral / 100f;
            if (Mathf.Abs(AudioListener.volume - vg) > 0.001f) AudioListener.volume = vg;

            // Distancia de dibujado: el plano lejano de la cámara (y el detalle de lejos).
            var cam = Camera.main;
            if (cam != null)
            {
                float orig;
                if (!lejosOriginal.TryGetValue(cam, out orig)) { orig = cam.farClipPlane; lejosOriginal[cam] = orig; if (lejosOriginal.Count > 8) lejosOriginal.Clear(); }
                float d = Distancias[Mathf.Clamp(Distancia, 0, 3)];
                float objetivo = d <= 0f ? orig : Mathf.Min(orig, d);
                if (Mathf.Abs(cam.farClipPlane - objetivo) > 0.5f) cam.farClipPlane = objetivo;
            }
            if (!calidadGuardada)
            {
                calidadGuardada = true;
                sombrasOriginal = QualitySettings.shadows; distanciaSombras = QualitySettings.shadowDistance; resolucionSombras = QualitySettings.shadowResolution; lodOriginal = QualitySettings.lodBias;
            }
            float lod = Distancia == 0 ? lodOriginal : lodOriginal * (Distancia == 1 ? 0.85f : Distancia == 2 ? 0.7f : 0.55f);
            if (Mathf.Abs(QualitySettings.lodBias - lod) > 0.001f) QualitySettings.lodBias = lod;
            // Sombras.
            int so = Sombras;
            var sq = so == 0 ? sombrasOriginal : so == 1 ? ShadowQuality.HardOnly : ShadowQuality.Disable;
            if (QualitySettings.shadows != sq) QualitySettings.shadows = sq;
            float sd = so == 0 ? distanciaSombras : so == 1 ? Mathf.Min(distanciaSombras, 35f) : distanciaSombras;
            if (Mathf.Abs(QualitySettings.shadowDistance - sd) > 0.01f) QualitySettings.shadowDistance = sd;
            var sr = so == 1 ? ShadowResolution.Low : resolucionSombras;
            if (QualitySettings.shadowResolution != sr) QualitySettings.shadowResolution = sr;
            // Densidad del bosque (una vez por segundo: hay pocas cosas que cambiar).
            if (Time.realtimeSinceStartup >= proximoTerreno)
            {
                proximoTerreno = Time.realtimeSinceStartup + 1f;
                int de = Densidad;
                foreach (var t in Terrain.activeTerrains)
                {
                    if (t == null) continue;
                    float[] o;
                    if (!terrenoOriginal.TryGetValue(t, out o))
                    {
                        // Árboles lejanos: modelos y no tarjetas negras planas (se veían como una cortina 2D al otro lado del río).
                        // Sin árboles 2D (pedido 2026-11-17: "los árboles en 2D quítalos todos"): la distancia de tarjetas es la misma que la de dibujado
                        // (como mucho 650 m: más allá la niebla los tapa) y caben muchos más árboles en 3D a la vez.
                        float dist = Mathf.Min(t.treeDistance, 650f);
                        o = new[] { dist, dist, t.detailObjectDistance, t.detailObjectDensity, Mathf.Max(t.treeMaximumFullLODCount, 1500) };
                        terrenoOriginal[t] = o;
                        if (terrenoOriginal.Count > 16) { terrenoOriginal.Clear(); terrenoOriginal[t] = o; }
                    }
                    float f = de == 0 ? 1f : de == 1 ? 0.6f : 0.35f;
                    float td = o[0] * f, tb = o[1] * f, dd = o[2] * f, dn = o[3] * (de == 0 ? 1f : de == 1 ? 0.7f : 0.4f);
                    int lodCount = de == 0 ? (int)o[4] : Mathf.Min((int)o[4], de == 1 ? 50 : 20);
                    if (Mathf.Abs(t.treeDistance - td) > 0.5f) t.treeDistance = td;
                    if (Mathf.Abs(t.treeBillboardDistance - tb) > 0.5f) t.treeBillboardDistance = tb;
                    if (Mathf.Abs(t.detailObjectDistance - dd) > 0.5f) t.detailObjectDistance = dd;
                    if (Mathf.Abs(t.detailObjectDensity - dn) > 0.01f) t.detailObjectDensity = dn;
                    if (t.treeMaximumFullLODCount != lodCount) t.treeMaximumFullLODCount = lodCount;
                }
            }
            EscalaRender.Aplicar(EscalaPct / 100f);
            Enviar();
        }

        // ---------------- Contador de FPS ----------------

        static float fpsSuave = 60f; static GUIStyle estiloFps;
        public static void DibujarBrillo() { Vista.DibujarBrillo(Brillo); }

        public static void DibujarFps()
        {
            if (!ContadorFps) return;
            if (Event.current.type == EventType.Repaint && Time.unscaledDeltaTime > 0f) fpsSuave = Mathf.Lerp(fpsSuave, 1f / Time.unscaledDeltaTime, 0.05f);
            if (estiloFps == null) { estiloFps = new GUIStyle(GUI.skin.label) { fontSize = T(16), fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperRight }; }
            estiloFps.normal.textColor = fpsSuave >= 55f ? new Color(0.5f, 1f, 0.5f) : fpsSuave >= 30f ? new Color(1f, 0.85f, 0.3f) : new Color(1f, 0.35f, 0.3f);
            GUI.Label(new Rect(Screen.width - 160, 8, 150, 26), Mathf.RoundToInt(fpsSuave) + " FPS", estiloFps);
        }

        // ---------------- Tamaño de los textos ----------------

        /// Al cambiar el tamaño, se borran los estilos ya hechos (se rehacen con el tamaño nuevo).
        public static void ReiniciarEstilos()
        {
            estiloFps = null;
            if (plugin == null) return;
            var objetos = new List<object> { plugin, plugin.mundo, plugin.municion, plugin.aliados, plugin.eventos, plugin.mapa, plugin.perro };
            var tipos = new List<System.Type> { typeof(Muerte), typeof(Viaje), typeof(Oro), typeof(Mundo), typeof(Municion), typeof(Aliados), typeof(Eventos), typeof(Mapa) };
            foreach (var o in objetos)
            {
                if (o == null) continue;
                foreach (var f in o.GetType().GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic))
                    if (f.FieldType == typeof(GUIStyle)) f.SetValue(o, null);
            }
            foreach (var t in tipos)
                foreach (var f in t.GetFields(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic))
                    if (f.FieldType == typeof(GUIStyle)) f.SetValue(null, null);
            if (Muerte.Instancia != null)
                foreach (var f in typeof(Muerte).GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)) if (f.FieldType == typeof(GUIStyle)) f.SetValue(Muerte.Instancia, null);
        }

        public static string Describir()
        {
            return "distancia " + NombresDistancia[Distancia] + ", bosque " + NombresDensidad[Densidad] + ", sombras " + NombresSombras[Sombras] + ", escala " + EscalaPct + " %"
                + ", contador " + ContadorFps + ", nombre compañero " + NombreCompanero + ", dificultad " + NombresDificultad[Dificultad] + ", eventos " + NombresEventos[Eventos]
                + ", volumen general " + VolumenGeneral + " %, volumen efectos " + VolumenEfectos + " %, texto " + NombresTexto[Texto]
                + " | cámara lejos " + (Camera.main != null ? Camera.main.farClipPlane.ToString("0") : "—") + ", sombras " + QualitySettings.shadows + " " + QualitySettings.shadowDistance.ToString("0") + " m, lod " + QualitySettings.lodBias.ToString("0.00");
        }
    }
}

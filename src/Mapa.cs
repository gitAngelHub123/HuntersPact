// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using System.Collections.Generic;
using System.IO;
using HutongGames.PlayMaker;
using PixelCrushers.DialogueSystem;
using UnityEngine;

namespace WitchHuntCoop
{
    /// Mapa del cooperativo:
    ///  - Si uno encuentra el mapa de la cabaña (variable global HasMap), se activa también al otro.
    ///  - Las piedras de guardado que cualquiera de los dos haya visto (a menos de 15 m) se marcan sobre
    ///    el mapa del juego. Como las piedras son de la semilla de partida, la lista se guarda en la
    ///    partida (variable Lua) y sirve en las siguientes entradas.
    public class Mapa
    {
        public const string VarPiedras = "coop_piedras_vistas";
        const float RadioVista = 15f;

        readonly Plugin plugin;
        bool teniaMapa;
        float proximaRevision, proximasPiedras;
        readonly HashSet<string> vistas = new HashSet<string>();
        readonly List<KeyValuePair<string, Transform>> piedras = new List<KeyValuePair<string, Transform>>();
        int escenaPiedras = -1;
        GameObject imagenMapa;
        Camera camaraMapa;
        Texture2D icono;

        public Mapa(Plugin p) { plugin = p; }

        static FsmBool Tiene() { var g = FsmVariables.GlobalVariables; return g == null ? null : g.FindFsmBool("HasMap"); }

        public void Actualizar(bool conectado)
        {
            var hm = Tiene();
            if (hm == null) return;
            if (conectado && hm.Value && !teniaMapa)
            {
                for (int i = 0; i < 3; i++) plugin.EnviarMapa(Red.Armar(w => { w.Write((byte)0); w.Write(""); }));
                plugin.Log("Mapa: lo encontré, se lo paso al compañero");
            }
            teniaMapa = hm.Value;
            if (!conectado || plugin.Jugador == null) return;

            if (escenaPiedras != plugin.Escena) { escenaPiedras = plugin.Escena; piedras.Clear(); imagenMapa = null; if (plugin.Escena != Mundo.EscenaBosque) RestaurarFondo(); camaraMapa = null; Cargar(); }
            if (Time.realtimeSinceStartup < proximaRevision) return;
            proximaRevision = Time.realtimeSinceStartup + 0.5f;
            if (plugin.Escena == Mundo.EscenaBosque && Time.realtimeSinceStartup > proximasPiedras) { proximasPiedras = Time.realtimeSinceStartup + 3f; piedras.Clear(); BuscarPiedras(); }
            Vector3 yo = plugin.Jugador.transform.position;
            foreach (var kv in piedras)
            {
                if (kv.Value == null || vistas.Contains(kv.Key)) continue;
                if (Vector3.Distance(kv.Value.position, yo) > RadioVista) continue;
                Marcar(kv.Key);
                string id = kv.Key;
                for (int i = 0; i < 2; i++) plugin.EnviarMapa(Red.Armar(w => { w.Write((byte)1); w.Write(id); }));
            }
        }

        void BuscarPiedras()
        {
            foreach (var g in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                if (g.activeInHierarchy && (g.name.StartsWith("save_stone_witch") || g.name.StartsWith("coop_save_stone_")))
                    piedras.Add(new KeyValuePair<string, Transform>(g.name, g.transform));
        }

        void Cargar()
        {
            vistas.Clear();
            string s = "";
            try { s = DialogueLua.GetVariable(VarPiedras).AsString; } catch { }
            if (!string.IsNullOrEmpty(s) && s != "nil") foreach (var x in s.Split('|')) if (x.Length > 0) vistas.Add(x);
        }

        void Marcar(string id)
        {
            if (!vistas.Add(id)) return;
            DialogueLua.SetVariable(VarPiedras, string.Join("|", new List<string>(vistas).ToArray()));
            plugin.Log("Mapa: piedra de guardado vista: " + id);
            Viaje.Avisar("Piedra de guardado marcada en el mapa.");
        }

        public void Recibir(BinaryReader r)
        {
            byte tipo = r.ReadByte(); string id = r.ReadString();
            if (tipo == 0)
            {
                var hm = Tiene();
                if (hm != null && !hm.Value) { hm.Value = true; teniaMapa = true; plugin.Log("Mapa: " + plugin.NombreCompanero + " encontró el mapa; ahora también lo tengo"); Viaje.Avisar(plugin.NombreCompanero + " encontró el mapa."); }
            }
            else if (tipo == 1) Marcar(id);
        }

        // ---------------- Mapa actualizado ----------------

        UnityEngine.UI.Image fondo; Sprite fondoOriginal; Texture2D fondoNuevo; int escenaFondo = -1;
        Vector3 camPosOriginal; float camTamOriginal; bool camGuardada;

        /// Tras armar el bosque de esta entrada: foto aérea con el encuadre de la cámara del mapa (que
        /// se agranda para incluir la zona norte) y se pone en lugar del dibujo fijo del mapa.
        public void Regenerar(bool zonaNorte, float norteHasta)
        {
            if (camaraMapa == null)
                foreach (var c in Resources.FindObjectsOfTypeAll<Camera>())
                    if (c.name == "Map_Camera" && c.gameObject.scene.IsValid()) { camaraMapa = c; break; }
            if (camaraMapa == null) { plugin.Log("Mapa: no encontré la cámara del mapa"); return; }
            if (!camGuardada || escenaFondo != plugin.Escena) { camPosOriginal = camaraMapa.transform.position; camTamOriginal = camaraMapa.orthographicSize; camGuardada = true; }
            Vector3 an = Anclaje.Origen();
            if (zonaNorte)
            {
                float zMax = norteHasta - an.z;
                float centroZ = (-60f + zMax) / 2f;
                camaraMapa.transform.position = new Vector3(an.x + Mundo.NuevoX0 + Mundo.AnchoNuevo / 2f, camPosOriginal.y, an.z + centroZ);   // la zona nueva es más ancha que el bosque original
                camaraMapa.orthographicSize = Mathf.Max(camTamOriginal, (zMax + 60f) / 2f + 40f, Mundo.AnchoNuevo / 2f + 40f);
            }
            // Foto con el mismo encuadre (cuadrado, ortográfica, desde arriba).
            const int N = 1024;
            var rt = new RenderTexture(N, N, 24);
            var go = new GameObject("CoopMapaCam");
            var cam = go.AddComponent<Camera>();
            cam.enabled = false; cam.orthographic = true; cam.orthographicSize = camaraMapa.orthographicSize; cam.aspect = 1f;
            go.transform.position = new Vector3(camaraMapa.transform.position.x, an.y + 800f, camaraMapa.transform.position.z);
            go.transform.rotation = camaraMapa.transform.rotation;
            cam.nearClipPlane = 1f; cam.farClipPlane = 2000f;
            cam.cullingMask = ~((1 << 5) | (1 << 23));
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.12f, 0.14f, 0.16f);
            cam.targetTexture = rt;
            var terrenos = Terrain.activeTerrains;
            var antes = new float[terrenos.Length];
            var antesD = new float[terrenos.Length];
            for (int i = 0; i < terrenos.Length; i++)
            {
                antes[i] = terrenos[i].treeBillboardDistance; terrenos[i].treeBillboardDistance = 5000f;
                antesD[i] = terrenos[i].treeDistance; terrenos[i].treeDistance = 5000f;
            }
            // Objetos gigantes (planos de agua, cielo, paredes del límite) taparían el mapa.
            var ocultos = new List<Renderer>();
            foreach (var r in Object.FindObjectsOfType<Renderer>())
                if (r.enabled && (r.bounds.size.x > 500f || r.bounds.size.z > 500f)) { r.enabled = false; ocultos.Add(r); }
            bool niebla = RenderSettings.fog; var modo = RenderSettings.ambientMode; var col = RenderSettings.ambientLight;
            RenderSettings.fog = false; RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight = new Color(0.75f, 0.72f, 0.66f);
            var gl = new GameObject("CoopMapaLuz"); var luz = gl.AddComponent<Light>(); luz.type = LightType.Directional; luz.intensity = 1.2f;
            gl.transform.rotation = Quaternion.Euler(60f, 30f, 0f);
            try
            {
                cam.Render();
                RenderTexture.active = rt;
                if (fondoNuevo == null) fondoNuevo = new Texture2D(N, N, TextureFormat.RGB24, false);
                fondoNuevo.ReadPixels(new Rect(0, 0, N, N), 0, 0); fondoNuevo.Apply();
                RenderTexture.active = null;
            }
            finally
            {
                cam.targetTexture = null; Object.Destroy(rt); Object.Destroy(go); Object.Destroy(gl);
                for (int i = 0; i < terrenos.Length; i++) if (terrenos[i] != null) { terrenos[i].treeBillboardDistance = antes[i]; terrenos[i].treeDistance = antesD[i]; }
                foreach (var r in ocultos) if (r != null) r.enabled = true;
                RenderSettings.fog = niebla; RenderSettings.ambientMode = modo; RenderSettings.ambientLight = col;
            }
            // El dibujo del mapa: la imagen más grande bajo MapImage / Canvas_map.
            fondo = null; float area = 0f;
            foreach (var im in Resources.FindObjectsOfTypeAll<UnityEngine.UI.Image>())
            {
                if (!im.gameObject.scene.IsValid()) continue;
                string ruta = im.transform.parent != null ? im.transform.parent.name + "/" + im.name : im.name;
                if (!(ruta.Contains("MapImage") || ruta.Contains("Canvas_map") || im.name.Contains("Map"))) continue;
                var r = im.rectTransform.rect; float a = r.width * r.height;
                if (a > area) { area = a; fondo = im; }
            }
            if (fondo == null) { plugin.Log("Mapa: no encontré la imagen del mapa"); return; }
            if (fondoOriginal == null || escenaFondo != plugin.Escena) fondoOriginal = fondo.sprite;
            escenaFondo = plugin.Escena;
            fondo.sprite = Sprite.Create(fondoNuevo, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f));
            plugin.Log("Mapa: redibujado con el bosque de esta entrada (" + fondo.name + ", encuadre " + Mathf.RoundToInt(camaraMapa.orthographicSize) + ")");
            try { System.IO.File.WriteAllBytes(System.IO.Path.Combine(System.IO.Path.Combine(BepInEx.Paths.BepInExRootPath, "capturas"), "mapa_nuevo.png"), fondoNuevo.EncodeToPNG()); } catch { }
        }

        /// Fuera del bosque, el mapa vuelve a ser el del juego.
        public void RestaurarFondo()
        {
            if (fondo != null && fondoOriginal != null) fondo.sprite = fondoOriginal;
            if (camaraMapa != null && camGuardada) { camaraMapa.transform.position = camPosOriginal; camaraMapa.orthographicSize = camTamOriginal; }
            fondo = null; fondoOriginal = null; camGuardada = false;
        }

        public string Describir()
        {
            var hm = Tiene();
            return "HasMap=" + (hm != null && hm.Value) + " piedras en escena=" + piedras.Count + " vistas=" + string.Join(",", new List<string>(vistas).ToArray());
        }

        /// Marcas sobre el mapa del juego mientras está abierto: se proyecta la posición con su cámara.
        public void Dibujar()
        {
            // (2026-11-20) Rendimiento: los iconos del mod están apagados (DibujarIconosMod); antes igual se recorrían TODOS los objetos
            // del juego en cada cuadro buscando la imagen del mapa (si no estaba en la escena, sin parar): era lo más caro de la interfaz.
            if (!dibujarIconos) return;
            if (vistas.Count == 0 || piedras.Count == 0) return;
            if ((imagenMapa == null || camaraMapa == null) && Time.unscaledTime < proxBusquedaMapa) return;
            if (imagenMapa == null)
            {
                proxBusquedaMapa = Time.unscaledTime + 2f;
                foreach (var g in Resources.FindObjectsOfTypeAll<GameObject>())
                    if (g.name == "MapImage" && g.scene.IsValid()) { imagenMapa = g; break; }
            }
            if (imagenMapa == null || !imagenMapa.activeInHierarchy) return;
            if (camaraMapa == null)
            {
                proxBusquedaMapa = Time.unscaledTime + 2f;
                foreach (var c in Resources.FindObjectsOfTypeAll<Camera>())
                    if (c.name == "Map_Camera" && c.gameObject.scene.IsValid()) { camaraMapa = c; break; }
            }
            if (camaraMapa == null) return;
            var rt = imagenMapa.GetComponent<RectTransform>();
            if (rt == null) return;
            var esquinas = new Vector3[4];
            rt.GetWorldCorners(esquinas);   // lienzo en pantalla: esquinas en píxeles (y hacia arriba)
            float x0 = esquinas[0].x, y0 = esquinas[0].y, an = esquinas[2].x - x0, al = esquinas[2].y - y0;
            if (icono == null) icono = Icono();
            if (estilo == null)
            {
                estilo = new GUIStyle(GUI.skin.label) { fontSize = Opciones.T(13), fontStyle = FontStyle.Bold };
                estilo.normal.textColor = new Color(0.55f, 0.9f, 1f);
            }
            foreach (var kv in piedras)
            {
                if (kv.Value == null || !vistas.Contains(kv.Key)) continue;
                Vector3 v = camaraMapa.WorldToViewportPoint(kv.Value.position);
                if (v.x < 0f || v.x > 1f || v.y < 0f || v.y > 1f) continue;
                float px = x0 + v.x * an, py = Screen.height - (y0 + v.y * al);
                if (DibujarIconosMod) GUI.DrawTexture(new Rect(px - 11f, py - 11f, 22f, 22f), icono);   // pedido 2026-11-11: solo los iconos del juego original
            }
        }
        GUIStyle estilo;
        const bool DibujarIconosMod = false;
        static readonly bool dibujarIconos = DibujarIconosMod;   // variable (no constante) para que el compilador no avise de código inalcanzable
        float proxBusquedaMapa;

        /// Icono de la piedra de guardado: una piedra amarilla (sin texto), queda en el mapa para siempre.
        static Texture2D Icono()
        {
            const int N = 22;
            var t = new Texture2D(N, N, TextureFormat.ARGB32, false) { hideFlags = HideFlags.HideAndDontSave };
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    // óvalo algo más alto que ancho, con un lado más estrecho arriba: una lápida/piedra
                    float cx = (x - N / 2f + 0.5f) / 8.5f, cy = (y - N / 2f + 0.5f) / 10f;
                    float d = cx * cx + cy * cy * (cy > 0 ? 1.35f : 0.85f);
                    Color c = Color.clear;
                    if (d < 1f) c = y > N * 0.55f && x > 6 && x < 14 ? new Color(1f, 0.92f, 0.35f, 1f) : new Color(1f, 0.82f, 0.15f, 1f);
                    else if (d < 1.28f) c = new Color(0.12f, 0.08f, 0f, 0.95f);
                    t.SetPixel(x, y, c);
                }
            t.Apply();
            return t;
        }
    }
}

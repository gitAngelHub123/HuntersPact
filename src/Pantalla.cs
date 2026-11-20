// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace WitchHuntCoop
{
    /// Opciones de pantalla del cooperativo (botón "Pantalla" en el menú principal y en la pausa):
    /// resolución y modo (pantalla completa, en ventana o pantalla completa en ventana). Unity 5.6 no
    /// tiene el modo sin bordes: se hace quitando el borde a la ventana (user32) y llevándola al
    /// tamaño del monitor. El modo elegido se guarda y se vuelve a aplicar al abrir el juego.
    public class Pantalla
    {
        public enum Modo { Completa = 0, Ventana = 1, SinBordes = 2 }
        static readonly string[] NombresModo = { "Pantalla completa", "En ventana", "Pantalla completa en ventana" };
        const string PrefModo = "coop_pantalla_modo", PrefAncho = "coop_pantalla_ancho", PrefAlto = "coop_pantalla_alto";

        readonly Plugin plugin;
        bool abierto, enfocar;
        Modo modo;
        List<Resolution> resoluciones;
        int indice;
        Vector2 desplazamiento;
        EventSystem sistemaApagado;
        Button botonPausa; int escenaPausa = -1;
        bool aplicadoAlInicio;
        float reaplicarSinBordes = -1f;

        public bool Abierto { get { return abierto; } }

        public Pantalla(Plugin p)
        {
            plugin = p;
            modo = (Modo)PlayerPrefs.GetInt(PrefModo, Screen.fullScreen ? 0 : 1);
        }

        // ---------------- user32 (ventana sin bordes) ----------------

        [DllImport("user32.dll")] static extern IntPtr GetActiveWindow();
        delegate bool CadaVentana(IntPtr h, IntPtr l);
        [DllImport("user32.dll")] static extern bool EnumWindows(CadaVentana f, IntPtr l);
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
        [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, System.Text.StringBuilder s, int n);
        [DllImport("kernel32.dll")] static extern uint GetCurrentProcessId();
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] static extern IntPtr GetWindowLongPtr(IntPtr h, int i);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] static extern IntPtr SetWindowLongPtr(IntPtr h, int i, IntPtr v);
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr d, int x, int y, int cx, int cy, uint f);
        const int GWL_STYLE = -16;
        const long WS_POPUP = 0x80000000L, WS_VISIBLE = 0x10000000L, WS_OVERLAPPEDWINDOW = 0x00CF0000L;
        const uint SWP_FRAMECHANGED = 0x0020, SWP_SHOWWINDOW = 0x0040;
        static IntPtr ventana;

        static IntPtr Ventana()
        {
            if (ventana != IntPtr.Zero) return ventana;
            // La ventana de Unity de ESTE proceso (no la activa: puede no tener el foco).
            try
            {
                uint yo = GetCurrentProcessId();
                EnumWindows((h, l) =>
                {
                    uint pid; GetWindowThreadProcessId(h, out pid);
                    if (pid != yo || !IsWindowVisible(h)) return true;
                    var sb = new System.Text.StringBuilder(64); GetClassName(h, sb, 64);
                    if (sb.ToString() != "UnityWndClass") return true;
                    ventana = h; return false;
                }, IntPtr.Zero);
            }
            catch { }
            if (ventana == IntPtr.Zero) { try { ventana = GetActiveWindow(); } catch { } }
            return ventana;
        }

        void SinBordes(bool si, int ancho, int alto)
        {
            var h = Ventana();
            if (h == IntPtr.Zero) { plugin.Log("Pantalla: no encontré la ventana del juego"); return; }
            try
            {
                long estilo = si ? (WS_POPUP | WS_VISIBLE) : (WS_OVERLAPPEDWINDOW | WS_VISIBLE);
                SetWindowLongPtr(h, GWL_STYLE, new IntPtr(estilo));
                if (si) SetWindowPos(h, IntPtr.Zero, 0, 0, ancho, alto, SWP_FRAMECHANGED | SWP_SHOWWINDOW);
                else SetWindowPos(h, IntPtr.Zero, 60, 60, ancho, alto, SWP_FRAMECHANGED | SWP_SHOWWINDOW);
            }
            catch (Exception e) { plugin.Log("Pantalla: no se pudo cambiar el borde: " + e.Message); }
        }

        // ---------------- Aplicar ----------------

        void Aplicar()
        {
            var r = resoluciones[Mathf.Clamp(indice, 0, resoluciones.Count - 1)];
            var monitor = Monitor();
            PlayerPrefs.SetInt(PrefModo, (int)modo); PlayerPrefs.SetInt(PrefAncho, r.width); PlayerPrefs.SetInt(PrefAlto, r.height); PlayerPrefs.Save();
            switch (modo)
            {
                case Modo.Completa:
                    SinBordes(false, r.width, r.height);
                    Screen.SetResolution(r.width, r.height, true);
                    break;
                case Modo.Ventana:
                    Screen.SetResolution(r.width, r.height, false);
                    SinBordes(false, r.width, r.height);
                    break;
                case Modo.SinBordes:
                    // Ventana del tamaño del monitor y sin borde (después de que Unity cambie el tamaño).
                    Screen.SetResolution(monitor.width, monitor.height, false);
                    reaplicarSinBordes = Time.realtimeSinceStartup + 0.3f;
                    break;
            }
            plugin.Log("Pantalla: " + NombresModo[(int)modo] + " " + (modo == Modo.SinBordes ? monitor.width + "x" + monitor.height : r.width + "x" + r.height));
        }

        static Resolution Monitor()
        {
            // En ventana, currentResolution es la del escritorio; si no, la mayor disponible.
            var c = Screen.currentResolution;
            if (c.width > 0) return c;
            var rs = Screen.resolutions; return rs.Length > 0 ? rs[rs.Length - 1] : c;
        }

        public void Actualizar()
        {
            if (!aplicadoAlInicio && Time.realtimeSinceStartup > 3f)
            {
                aplicadoAlInicio = true;
                if (PlayerPrefs.GetInt(PrefModo, -1) == (int)Modo.SinBordes)
                {
                    var m = Monitor();
                    Screen.SetResolution(m.width, m.height, false);
                    reaplicarSinBordes = Time.realtimeSinceStartup + 0.5f;
                }
            }
            if (reaplicarSinBordes > 0f && Time.realtimeSinceStartup > reaplicarSinBordes)
            {
                reaplicarSinBordes = -1f;
                var m = Monitor();
                SinBordes(true, m.width, m.height);
            }
            ProtegerPausa();
            AplicarFps();   // en cada cuadro: el juego vuelve a poner su propio límite
            if (plugin.Escena != 3) { if (Guardado.ModoCoop) PreparaPausaCoop(); else { BotonPausa(); OcultarAbandonar(); } }
            else FilaOpciones();
            PausaSinDetener();
        }

        /// (2026-11-20) En cooperativo el menú de pausa NO detiene el tiempo: el juego lo ponía en 0 (y pausaba el audio), y como el
        /// anfitrión manda sobre enemigos y jefe, al otro se le congelaba todo. Con el menú abierto el mundo sigue y solo se bloquean
        /// los controles de este jugador (Entrada.PausaCoop). Jugando solo, la pausa sigue igual que en el juego.
        void PausaSinDetener()
        {
            bool coop = plugin.Conectado && PausaAbierta;
            if (coop)
            {
                if (Time.timeScale < 0.99f) Time.timeScale = 1f;
                if (AudioListener.pause) AudioListener.pause = false;
            }
            if (coop != Entrada.PausaCoop) plugin.Log("Pausa: " + (coop ? "menú abierto sin detener el juego (cooperativo)" : "menú cerrado"));
            Entrada.PausaCoop = coop;
        }

        // ---------------- Esc al cancelar una tecla ----------------

        readonly Dictionary<Rewired.ControllerMap, bool> mapasEsc = new Dictionary<Rewired.ControllerMap, bool>();

        /// Mientras se espera una tecla (y un momento después de cancelar con Esc) los mapas de teclado y ratón del juego (Rewired)
        /// se apagan, para que ese Esc solo cancele la edición y no cierre la pausa que hay detrás. No se toca el menú de pausa.
        void ProtegerPausa()
        {
            bool bloquear = Controles.Capturando != null || Time.realtimeSinceStartup < Controles.HastaEsc;
            try
            {
                if (bloquear && mapasEsc.Count == 0)
                {
                    var j = Rewired.ReInput.players.GetPlayer(0); if (j == null) return;
                    foreach (var m in j.controllers.maps.GetAllMaps())
                        if (m.controllerType == Rewired.ControllerType.Keyboard || m.controllerType == Rewired.ControllerType.Mouse)
                        { mapasEsc[m] = m.enabled; m.enabled = false; }
                }
                else if (!bloquear && mapasEsc.Count > 0)
                {
                    foreach (var kv in mapasEsc) kv.Key.enabled = kv.Value;
                    mapasEsc.Clear();
                }
            }
            catch (System.Exception e) { mapasEsc.Clear(); plugin.Log("ProtegerPausa: " + e.Message); }
        }

        // ---------------- Fila en las Opciones del menú principal ----------------

        static readonly string[] CortosModo = { "Completa", "En ventana", "Sin bordes" };

        // ---------------- Límite de FPS ----------------

        public const string PrefFps = "coop_fps";
        public static readonly int[] OpcionesFps = { 0, 30, 60, 90, 120 };   // 0 = sin límite
        public static int Fps { get { return PlayerPrefs.GetInt(PrefFps, 0); } }
        static string NombreFps(int f) { return f <= 0 ? "Sin límite" : f + " FPS"; }
        float proximoFps;

        public void CambiarFps(int f)
        {
            PlayerPrefs.SetInt(PrefFps, f); PlayerPrefs.Save();
            AplicarFps();
            plugin.Log("Pantalla: límite de FPS " + NombreFps(f));
            if (textoFps != null) textoFps.text = NombreFps(f);
        }

        /// Con un límite, se apaga la sincronía vertical (si no, el límite no cuenta); sin límite, manda el juego.
        public static void AplicarFps()
        {
            int f = Fps;
            if (f > 0) { if (QualitySettings.vSyncCount != 0) QualitySettings.vSyncCount = 0; if (Application.targetFrameRate != f) Application.targetFrameRate = f; }
            else if (Application.targetFrameRate != -1) Application.targetFrameRate = -1;
        }

        Button botonFps; Text textoFps;
        Button botonFila; Text textoFila;

        /// En el panel de Opciones del menú principal, la casilla "Ventana" se cambia por una fila
        /// "Modo de pantalla" (copia de la fila de Sync vertical) que alterna completa / en ventana /
        /// sin bordes y se aplica al momento. La resolución se sigue eligiendo con el botón del juego.
        void FilaOpciones()
        {
            if (botonFila != null) { if (textoFila != null) textoFila.text = CortosModo[(int)ModoActual()]; return; }
            Transform vsync = null, ventana = null;
            foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
            {
                if (!t.gameObject.scene.IsValid() || t.parent == null || t.parent.name != "OptionsUI") continue;
                if (t.name == "VSync") vsync = t; else if (t.name == "Windowed") ventana = t;
            }
            if (vsync == null || ventana == null) return;
            var fila = (GameObject)UnityEngine.Object.Instantiate(vsync.gameObject, vsync.parent, false);
            fila.name = "CoopModoPantalla";
            foreach (var f in fila.GetComponentsInChildren<PlayMakerFSM>(true)) UnityEngine.Object.DestroyImmediate(f);
            foreach (var mb in fila.GetComponentsInChildren<MonoBehaviour>(true))
                if (mb != null && (mb.GetType().Name.StartsWith("PlayMaker") || mb is EventTrigger)) UnityEngine.Object.DestroyImmediate(mb);
            Text etiqueta = null; Button boton = null;
            foreach (Transform h in fila.transform)
            {
                if (h.name == "Text") etiqueta = h.GetComponent<Text>();
                else if (h.name == "ResButton") boton = h.GetComponent<Button>();
                else h.gameObject.SetActive(false);   // "Disabled" y lo demás de la fila copiada
            }
            if (etiqueta == null || boton == null) { UnityEngine.Object.Destroy(fila); return; }
            etiqueta.text = "Modo de pantalla";
            textoFila = boton.GetComponentInChildren<Text>(true);
            boton.onClick = new Button.ButtonClickedEvent();
            boton.onClick.AddListener(() => CambiarModo((Modo)(((int)ModoActual() + 1) % 3)));
            // En el lugar de la fila "Ventana" (texto a la misma altura), que se esconde.
            float yTexto = ((RectTransform)vsync).anchoredPosition.y + ((RectTransform)etiqueta.transform).anchoredPosition.y;
            float yVentana = ((RectTransform)ventana).anchoredPosition.y;
            var rt = (RectTransform)fila.transform;
            rt.anchoredPosition = ((RectTransform)vsync).anchoredPosition + new Vector2(0f, yVentana - yTexto);
            ventana.gameObject.SetActive(false);
            fila.SetActive(true);
            botonFila = boton;
            if (textoFila != null) textoFila.text = CortosModo[(int)ModoActual()];
            plugin.Log("Pantalla: fila 'Modo de pantalla' agregada a las Opciones (en lugar de 'Ventana')");

            // Fila "Límite de FPS", dos filas más abajo (debajo de "Control suave de la cámara"); el panel negro crece.
            var filaFps = (GameObject)UnityEngine.Object.Instantiate(fila, fila.transform.parent, false);
            filaFps.name = "CoopLimiteFps";
            Text etF = null; Button boF = null;
            foreach (Transform h in filaFps.transform) { if (h.name == "Text") etF = h.GetComponent<Text>(); else if (h.name == "ResButton") boF = h.GetComponent<Button>(); }
            if (etF != null && boF != null)
            {
                etF.text = "Límite de FPS";
                textoFps = boF.GetComponentInChildren<Text>(true);
                boF.onClick = new Button.ButtonClickedEvent();
                boF.onClick.AddListener(() =>
                {
                    int i = System.Array.IndexOf(OpcionesFps, Fps);
                    CambiarFps(OpcionesFps[(i + 1) % OpcionesFps.Length]);
                });
                // en el lugar de "Sync vertical" (con un límite se apaga; "Sin límite" la deja como esté)
                ((RectTransform)filaFps.transform).anchoredPosition = ((RectTransform)vsync).anchoredPosition;
                vsync.gameObject.SetActive(false);
                if (textoFps != null) textoFps.text = NombreFps(Fps);
                botonFps = boF;
                filaFps.SetActive(true);
                plugin.Log("Pantalla: fila 'Límite de FPS' agregada a las Opciones");

                // Fila "Opciones del mod": abre el panel con todo lo demás (rendimiento, cooperativo, interfaz).
                var filaMod = (GameObject)UnityEngine.Object.Instantiate(fila, fila.transform.parent, false);
                filaMod.name = "CoopOpcionesMod";
                Text etM = null; Button boM = null;
                foreach (Transform h in filaMod.transform) { if (h.name == "Text") etM = h.GetComponent<Text>(); else if (h.name == "ResButton") boM = h.GetComponent<Button>(); }
                if (etM != null && boM != null)
                {
                    etM.text = "Más opciones";
                    var tb = boM.GetComponentInChildren<Text>(true); if (tb != null) tb.text = "Abrir";
                    boM.onClick = new Button.ButtonClickedEvent();
                    boM.onClick.AddListener(() => Abrir());
                    ((RectTransform)filaMod.transform).anchoredPosition = rt.anchoredPosition + new Vector2(0f, -70f);   // debajo de "Control suave de la cámara"
                    foreach (Transform t in fila.transform.parent)
                        if (t.name.StartsWith("SemiBlack")) { var rb = (RectTransform)t; rb.sizeDelta += new Vector2(0f, 36f); rb.anchoredPosition -= new Vector2(0f, 18f); break; }
                    filaMod.SetActive(true);
                }
            }
        }

        Modo ModoActual()
        {
            if (Screen.fullScreen) return Modo.Completa;
            return PlayerPrefs.GetInt(PrefModo, 1) == (int)Modo.SinBordes ? Modo.SinBordes : Modo.Ventana;
        }

        /// Cambia el modo con la resolución que esté puesta.
        public void CambiarModo(Modo m)
        {
            var actual = new Resolution { width = Screen.width, height = Screen.height };
            if (m == Modo.Completa) actual = Monitor();   // la escala de resolución se aplica sobre la del monitor, no sobre la ya escalada
            resoluciones = new List<Resolution> { actual }; indice = 0;
            if (m != Modo.SinBordes && ModoActual() == Modo.SinBordes)
            {
                // saliendo de sin bordes: la ventana ocupa el monitor; se vuelve a una resolución algo menor
                var rs = new List<Resolution>(Screen.resolutions);
                rs.Sort((a, b) => (b.width * b.height).CompareTo(a.width * a.height));
                foreach (var r in rs) if (r.width < Monitor().width) { resoluciones[0] = r; break; }
            }
            modo = m;
            Aplicar();
            if (textoFila != null) textoFila.text = CortosModo[(int)m];
        }

        // ---------------- Botón en la pausa ----------------

        /// Copia del botón "Controles asignados" del menú de pausa, justo debajo.
        // ---------------- Menú de pausa de la partida cooperativa ----------------
        // Continuar · Opciones · Menú principal (con confirmación). Se dibuja aquí; los botones del juego quedan activos pero
        // invisibles (así se usa su lógica para reanudar y volver al menú). "Abandonar" no existe en partida.

        GameObject fondoPausa, menuPausa; PlayMakerFSM fsmPausa; float proximaBusquedaPausa; bool confirmandoSalida;
        static readonly string[] BotonesJuegoPausa = { "Resume", "MainMenu", "Quit", "Controls", "CoopPantallaPausa" };

        void PreparaPausaCoop()
        {
            if (fondoPausa == null || menuPausa == null)
            {
                if (Time.realtimeSinceStartup < proximaBusquedaPausa) return;
                proximaBusquedaPausa = Time.realtimeSinceStartup + 1f;
                foreach (var g in Resources.FindObjectsOfTypeAll<GameObject>())
                {
                    if (!g.scene.IsValid()) continue;
                    if (g.name == "PauseBackground") fondoPausa = g;
                    else if (g.name == "PauseMenuUI" && g.transform.parent != null && g.transform.parent.name == "PauseBackground") menuPausa = g;
                }
                foreach (var f in Resources.FindObjectsOfTypeAll<PlayMakerFSM>())
                    if (f.FsmName == "PauseMenu" && f.gameObject.scene.IsValid() && f.gameObject.name.StartsWith("Canvas_RFPS")) { fsmPausa = f; break; }
                if (fondoPausa == null || menuPausa == null) return;
            }
            foreach (var n in BotonesJuegoPausa)
            {
                var t = menuPausa.transform.Find(n); if (t == null) continue;
                var cg = t.GetComponent<CanvasGroup>(); if (cg == null) cg = t.gameObject.AddComponent<CanvasGroup>();
                if (cg.alpha != 0f || cg.blocksRaycasts || cg.interactable) { cg.alpha = 0f; cg.blocksRaycasts = false; cg.interactable = false; }
            }
        }

        /// La pausa está abierta de verdad: el fondo está activo Y la máquina de la pausa salió de su estado en reposo
        /// ("State 1"). El juego usa ese mismo fondo para la pantalla de la historia del principio, que no es una pausa.
        GameObject pantallaCarga; float proxBusquedaCarga;

        /// La pantalla de carga entre escenas ("Cargando…", "Consejo: …") usa el mismo fondo que la pausa: no es una pausa.
        bool CargandoEscena
        {
            get
            {
                if (pantallaCarga == null)
                {
                    if (Time.realtimeSinceStartup < proxBusquedaCarga) return false;
                    proxBusquedaCarga = Time.realtimeSinceStartup + 1f;
                    foreach (var g in Resources.FindObjectsOfTypeAll<GameObject>())
                    {
                        if (g.name != "LoadingScreen" || !g.scene.IsValid()) continue;
                        for (var x = g.transform.parent; x != null; x = x.parent) if (x.name.StartsWith("Canvas_RFPS")) { pantallaCarga = g; break; }
                        if (pantallaCarga != null) break;
                    }
                }
                return pantallaCarga != null && pantallaCarga.activeInHierarchy;
            }
        }

        float ultimaCargaVista, proxBusquedaTextoCarga;

        /// Segunda comprobación de la pantalla de carga, por su texto ("Consejo: …", "Presiona cualquier botón para guardar y continuar"):
        /// al pasar del pueblo al bosque la referencia guardada de la pantalla puede estar vieja y la pausa se dibujaba encima.
        bool TextoDeCargaVisible()
        {
            float ahora = Time.realtimeSinceStartup;
            if (ahora >= proxBusquedaTextoCarga)
            {
                proxBusquedaTextoCarga = ahora + 0.2f;
                foreach (var tx in (fondoPausa != null ? fondoPausa.transform.root.GetComponentsInChildren<UnityEngine.UI.Text>(false) : new UnityEngine.UI.Text[0]))
                {
                    if (tx == null || !tx.gameObject.scene.IsValid() || !tx.gameObject.activeInHierarchy || string.IsNullOrEmpty(tx.text)) continue;
                    if (tx.text.StartsWith("Consejo") || tx.text.IndexOf("guardar y continuar", System.StringComparison.OrdinalIgnoreCase) >= 0
                        || tx.text.IndexOf("any button to save", System.StringComparison.OrdinalIgnoreCase) >= 0) { ultimaCargaVista = ahora; break; }
                }
            }
            return ahora - ultimaCargaVista < 1.5f;
        }

        bool PausaAbierta
        {
            get
            {
                // primero lo barato: casi siempre la pausa está cerrada y no hace falta buscar la pantalla de carga
                if (fondoPausa == null || !fondoPausa.activeInHierarchy) return false;
                if (fsmPausa == null || fsmPausa.Fsm == null || fsmPausa.Fsm.ActiveState == null) return false;
                if (fsmPausa.ActiveStateName == "State 1") return false;
                return !CargandoEscena && !TextoDeCargaVisible();
            }
        }

        /// Pulsa uno de los botones del juego (Resume, MainMenu) como si se hiciera clic.
        void PulsarBotonJuego(string nombre)
        {
            if (menuPausa == null) return;
            var t = menuPausa.transform.Find(nombre); if (t == null) return;
            var b = t.GetComponent<Button>(); if (b != null) { plugin.Log("Pausa: " + nombre); b.onClick.Invoke(); }
        }

        /// Para probar sin hacer clic: continuar | confirmar | no | si | opciones
        public string OrdenPausa(string a)
        {
            if (!PausaAbierta) return "la pausa no está abierta";
            switch (a)
            {
                case "continuar": PulsarBotonJuego("Resume"); break;
                case "confirmar": confirmandoSalida = true; break;
                case "no": confirmandoSalida = false; break;
                case "si": confirmandoSalida = false; PulsarBotonJuego("MainMenu"); break;
                case "opciones": Abrir(); break;
            }
            return "pausa abierta=" + PausaAbierta + " confirmando=" + confirmandoSalida;
        }

        void DibujarPausaCoop()
        {
            if (!Guardado.ModoCoop || plugin.Escena == 3 || !PausaAbierta) { confirmandoSalida = false; return; }
            Estilos();
            GUI.depth = -105;
            var prev = GUI.color;
            float w = Mathf.Min(360f, Screen.width - 40f), h = 54f, g = 14f, x = (Screen.width - w) / 2f, y = Screen.height * 0.40f;
            var grande = new GUIStyle(sBotonSec) { fontSize = Opciones.T(24) };
            if (!confirmandoSalida)
            {
                if (GUI.Button(new Rect(x, y, w, h), "Continuar", grande)) { PulsarBotonJuego("Resume"); }
                if (GUI.Button(new Rect(x, y + (h + g), w, h), "Opciones", grande)) { Abrir(); }
                if (GUI.Button(new Rect(x, y + 2 * (h + g), w, h), "Menú principal", grande)) { confirmandoSalida = true; }
            }
            else
            {
                float ww = Mathf.Min(520f, Screen.width - 40f), hh = 190f, xx = (Screen.width - ww) / 2f, yy = y - 20f;
                GUI.color = new Color(1f, 1f, 1f, 1f); GUI.Box(new Rect(xx, yy, ww, hh), GUIContent.none, sCaja);
                GUI.DrawTexture(new Rect(xx, yy, ww, 3f), tBoton);
                var titulo = new GUIStyle(sTitulo) { fontSize = Opciones.T(26), wordWrap = true };
                GUI.Label(new Rect(xx + 20f, yy + 20f, ww - 40f, 70f), "¿Estás seguro que quieres salir?", titulo);
                float bw = (ww - 60f) / 2f;
                if (GUI.Button(new Rect(xx + 20f, yy + 112f, bw, 54f), "No", grande)) { confirmandoSalida = false; }
                if (GUI.Button(new Rect(xx + 40f + bw, yy + 112f, bw, 54f), "Sí", sBoton)) { confirmandoSalida = false; PulsarBotonJuego("MainMenu"); }
            }
            GUI.color = prev;
        }

        float proximaRevisionQuit;

        /// En partida cooperativa el botón "Abandonar" del menú de pausa no está (solo se sale desde el menú principal).
        void OcultarAbandonar()
        {
            if (Time.realtimeSinceStartup < proximaRevisionQuit) return;
            proximaRevisionQuit = Time.realtimeSinceStartup + 0.5f;
            if (!Guardado.ModoCoop) return;
            foreach (var b in Resources.FindObjectsOfTypeAll<Button>())
                if (b.gameObject.scene.IsValid() && b.name == "Quit" && b.transform.parent != null && b.transform.parent.name == "PauseMenuUI" && b.gameObject.activeSelf)
                    b.gameObject.SetActive(false);
        }

        void BotonPausa()
        {
            if (escenaPausa == plugin.Escena && botonPausa != null) return;
            // "Opciones" entre "Menú principal" y "Abandonar", con el estilo de esos botones; "Abandonar" baja un lugar.
            // El menú de pausa sobrevive al cambio de escena: se reutiliza el botón que ya exista (uno solo).
            Button quit = null, menu = null; var nuestros = new List<GameObject>(); var otros = new List<RectTransform>();
            foreach (var b in Resources.FindObjectsOfTypeAll<Button>())
            {
                if (!b.gameObject.scene.IsValid() || b.transform.parent == null || b.transform.parent.name != "PauseMenuUI") continue;
                if (b.name == "CoopPantallaPausa") { nuestros.Add(b.gameObject); continue; }
                if (b.name == "Quit") quit = b;
                else if (b.name == "MainMenu") menu = b;
                otros.Add((RectTransform)b.transform);
            }
            if (quit == null || menu == null) return;
            escenaPausa = plugin.Escena;
            var rm = (RectTransform)menu.transform;
            // paso entre botones: la distancia de "Menú principal" al botón de arriba (Continuar)
            float paso = 0f;
            foreach (var o in otros)
                if (o != rm && o.anchoredPosition.y > rm.anchoredPosition.y && (paso == 0f || o.anchoredPosition.y - rm.anchoredPosition.y < paso)) paso = o.anchoredPosition.y - rm.anchoredPosition.y;
            if (paso <= 0f) paso = 50f;
            GameObject go = nuestros.Count > 0 ? nuestros[0] : null;
            for (int i = 1; i < nuestros.Count; i++) UnityEngine.Object.Destroy(nuestros[i]);   // copias de versiones anteriores
            if (go == null)
            {
                go = (GameObject)UnityEngine.Object.Instantiate(quit.gameObject, quit.transform.parent, false);
                go.name = "CoopPantallaPausa";
                foreach (var f in go.GetComponentsInChildren<PlayMakerFSM>(true)) UnityEngine.Object.DestroyImmediate(f);
                foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
                    if (mb != null && (mb.GetType().Name.StartsWith("PlayMaker") || mb is EventTrigger || mb.GetType().Namespace == "PixelCrushers.DialogueSystem")) UnityEngine.Object.DestroyImmediate(mb);
            }
            botonPausa = go.GetComponent<Button>();
            botonPausa.onClick = new Button.ButtonClickedEvent();
            botonPausa.onClick.AddListener(Abrir);
            var t = go.GetComponentInChildren<Text>(true); if (t != null) t.text = "Opciones";
            // posiciones fijas, a partir de "Menú principal" (no se acumulan corrimientos)
            ((RectTransform)go.transform).anchoredPosition = rm.anchoredPosition - new Vector2(0f, paso);
            ((RectTransform)quit.transform).anchoredPosition = rm.anchoredPosition - new Vector2(0f, 2f * paso);
            go.SetActive(true);
            plugin.Log("Pantalla: botón 'Opciones' en el menú de pausa (paso " + paso.ToString("0") + ")");
        }

        // ---------------- Cuadro ----------------

        public void Abrir()
        {
            var lista = new List<Resolution>();
            foreach (var r in Screen.resolutions)
            {
                bool rep = false;
                foreach (var x in lista) if (x.width == r.width && x.height == r.height) { rep = true; break; }
                if (!rep) lista.Add(r);
            }
            // además de las del monitor, las más comunes que entren en él (en ventana sirven todas)
            var mon = Monitor();
            int[,] comunes = { { 1024, 576 }, { 1152, 648 }, { 1280, 720 }, { 1280, 800 }, { 1366, 768 }, { 1440, 900 }, { 1600, 900 }, { 1680, 1050 }, { 1920, 1080 }, { 1920, 1200 }, { 2560, 1080 }, { 2560, 1440 }, { 3440, 1440 }, { 3840, 2160 } };
            for (int k = 0; k < comunes.GetLength(0); k++)
            {
                int cw = comunes[k, 0], ch = comunes[k, 1];
                if (cw > mon.width || ch > mon.height) continue;
                bool rep = false;
                foreach (var x in lista) if (x.width == cw && x.height == ch) { rep = true; break; }
                if (!rep) lista.Add(new Resolution { width = cw, height = ch });
            }
            if (lista.Count == 0) lista.Add(Screen.currentResolution);
            lista.Sort((a, b) => (b.width * b.height).CompareTo(a.width * a.height));
            resoluciones = lista;
            indice = 0;
            for (int i = 0; i < lista.Count; i++) if (lista[i].width == Screen.width && lista[i].height == Screen.height) indice = i;
            modo = Screen.fullScreen ? Modo.Completa : (PlayerPrefs.GetInt(PrefModo, 1) == (int)Modo.SinBordes ? Modo.SinBordes : Modo.Ventana);
            abierto = true; enfocar = true; nombreEditado = null;
            var es = EventSystem.current;
            if (es != null && es.enabled) { es.enabled = false; sistemaApagado = es; }
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        }

        void Cerrar()
        {
            abierto = false;
            if (sistemaApagado != null) sistemaApagado.enabled = true;
            sistemaApagado = null;
        }

        GUIStyle sTitulo, sEtiqueta, sOpcion, sOpcionSel, sBoton, sBotonSec, sNota, sFondo, sCaja;
        Texture2D tFondo, tCaja, tOpc, tOpcSel, tBoton, tBotonHover, tSec, tSecHover;
        Font fuente; float proximaFuente;

        static Texture2D Color1(Color c) { var t = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave }; t.SetPixel(0, 0, c); t.Apply(); return t; }

        void Estilos()
        {
            if (fuente == null && Time.realtimeSinceStartup > proximaFuente)
            {
                proximaFuente = Time.realtimeSinceStartup + 2f;
                foreach (var f in Resources.FindObjectsOfTypeAll<Font>()) if (f.name == "BoltonTitlingBold") { fuente = f; sTitulo = null; break; }
            }
            if (sTitulo != null && tFondo != null) return;
            tFondo = Color1(new Color(0f, 0f, 0f, 0.7f)); tCaja = Color1(new Color(0.07f, 0.07f, 0.08f, 1f));
            tOpc = Color1(new Color(0.16f, 0.16f, 0.18f, 1f)); tOpcSel = Color1(new Color(0.55f, 0.12f, 0.1f, 1f));
            tBoton = Color1(new Color(0.55f, 0.12f, 0.1f, 1f)); tBotonHover = Color1(new Color(0.7f, 0.17f, 0.13f, 1f));
            tSec = Color1(new Color(0.2f, 0.2f, 0.22f, 1f)); tSecHover = Color1(new Color(0.28f, 0.28f, 0.31f, 1f));
            sFondo = new GUIStyle(); sFondo.normal.background = tFondo;
            sCaja = new GUIStyle(); sCaja.normal.background = tCaja;
            sTitulo = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 30 };
            if (fuente != null) sTitulo.font = fuente;
            sTitulo.normal.textColor = new Color(0.95f, 0.93f, 0.88f);
            sEtiqueta = new GUIStyle(GUI.skin.label) { fontSize = 15 }; sEtiqueta.normal.textColor = new Color(0.75f, 0.73f, 0.68f);
            sOpcion = new GUIStyle(GUI.skin.button) { fontSize = 16, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(12, 12, 6, 6), border = new RectOffset(0, 0, 0, 0) };
            sOpcion.normal.background = tOpc; sOpcion.hover.background = tSec; sOpcion.active.background = tSecHover;
            sOpcion.normal.textColor = sOpcion.hover.textColor = sOpcion.active.textColor = new Color(0.9f, 0.9f, 0.9f);
            sOpcionSel = new GUIStyle(sOpcion); sOpcionSel.normal.background = tOpcSel; sOpcionSel.hover.background = tBotonHover;
            sOpcionSel.normal.textColor = sOpcionSel.hover.textColor = Color.white;
            sBoton = new GUIStyle(GUI.skin.button) { fontSize = 20, alignment = TextAnchor.MiddleCenter, border = new RectOffset(0, 0, 0, 0) };
            if (fuente != null) sBoton.font = fuente;
            sBoton.normal.background = tBoton; sBoton.hover.background = tBotonHover; sBoton.active.background = tBotonHover;
            sBoton.normal.textColor = sBoton.hover.textColor = sBoton.active.textColor = Color.white;
            sBotonSec = new GUIStyle(sBoton); sBotonSec.normal.background = tSec; sBotonSec.hover.background = tSecHover; sBotonSec.active.background = tSecHover;
            sNota = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true }; sNota.normal.textColor = new Color(0.62f, 0.6f, 0.56f);
        }

        int pestana; Vector2 desplOpciones; string nombreEditado;
        public int Pestana { get { return pestana; } set { pestana = Mathf.Clamp(value, 0, 4); } }
        static readonly string[] Pestanas = { "Pantalla", "Gráficos", "Cooperativo", "Interfaz", "Controles" };

        /// Una fila de botones: el elegido en rojo.
        int Fila(string titulo, string[] nombres, int actual, string nota = null)
        {
            GUILayout.Label(titulo, sEtiqueta);
            GUILayout.BeginHorizontal();
            int r = actual;
            for (int i = 0; i < nombres.Length; i++)
                if (GUILayout.Button(nombres[i], i == actual ? sOpcionSel : sOpcion, GUILayout.Height(30))) r = i;
            GUILayout.EndHorizontal();
            // sin subtítulos: solo títulos (pedido 2026-11-10)
            GUILayout.Space(8);
            return r;
        }

        /// Pestaña Controles: cada acción con su tecla; se pulsa y se toca la nueva (Esc cancela).
        void DibujarControles()
        {
            Controles.Iniciar(plugin);
            string grupo = null;
            foreach (var a in Controles.Lista)
            {
                if (a.Grupo != grupo) { grupo = a.Grupo; GUILayout.Space(6); GUILayout.Label(grupo, sEtiqueta); }
                GUILayout.BeginHorizontal();
                GUILayout.Label(a.Nombre, sOpcion, GUILayout.Width(330), GUILayout.Height(28));
                bool esperando = Controles.Capturando == a;
                string texto = esperando ? "Pulsa una tecla…" : Controles.Texto(a);
                if (GUILayout.Button(texto, esperando ? sOpcionSel : sOpcion, GUILayout.Width(165), GUILayout.Height(28)) && !esperando) Controles.Empezar(a);
                GUILayout.EndHorizontal();
            }
            GUILayout.Space(10);
            if (!string.IsNullOrEmpty(Controles.Aviso)) GUILayout.Label(Controles.Aviso, sNota);
            if (GUILayout.Button("Restablecer controles", sBotonSec, GUILayout.Height(32))) Controles.Restablecer();
        }

        bool listaRes; Vector2 desplRes;
        GUIStyle sBarra, sPerilla;

        /// Igual que Fila, pero las opciones (guardadas de más alto a más bajo) se muestran de más bajo a más alto.
        int FilaDeMenosAMas(string titulo, string[] nombres, int actual)
        {
            int n = nombres.Length;
            var inv = new string[n];
            for (int i = 0; i < n; i++) inv[i] = nombres[n - 1 - i];
            int r = Fila(titulo, inv, n - 1 - actual);
            return n - 1 - r;
        }

        /// Barra horizontal con el valor en %, de a 1 %.
        int Barra(string titulo, int valor, int min, int max)
        {
            if (sBarra == null)
            {
                sBarra = new GUIStyle(GUI.skin.horizontalSlider) { fixedHeight = 10f, margin = new RectOffset(4, 4, 12, 12), border = new RectOffset(0, 0, 0, 0) };
                sBarra.normal.background = Color1(new Color(0.34f, 0.34f, 0.37f, 1f));   // la pista se ve sobre el fondo oscuro
                sPerilla = new GUIStyle(GUI.skin.horizontalSliderThumb) { fixedWidth = 16f, fixedHeight = 26f, border = new RectOffset(0, 0, 0, 0) };
                sPerilla.normal.background = tBoton; sPerilla.hover.background = tBotonHover; sPerilla.active.background = tBotonHover;
            }
            GUILayout.Label(titulo + ": " + valor + " %", sEtiqueta);
            float v = GUILayout.HorizontalSlider(valor, min, max, sBarra, sPerilla, GUILayout.Height(30));
            GUILayout.Space(8);
            return Mathf.Clamp(Mathf.RoundToInt(v), min, max);
        }

        /// Panel "Opciones del mod" (en Opciones del menú principal y en la pausa).
        public void Dibujar()
        {
            if (!abierto) { DibujarPausaCoop(); return; }
            Estilos();
            Event e = Event.current;
            if (Controles.Capturando != null && Controles.Procesar(e)) { if (e.type == EventType.Repaint) { } }   // esperando una tecla: Esc cancela (no cierra el panel)
            else if (e.type == EventType.KeyDown)
            {
                if (e.keyCode == KeyCode.Escape) { Cerrar(); e.Use(); return; }
                if ((e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) && pestana == 0) { Aplicar(); Cerrar(); e.Use(); return; }
            }
            GUI.depth = -110;
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), GUIContent.none, sFondo);
            float w = Mathf.Min(620f, Screen.width - 40f), h = Mathf.Min(660f, Screen.height - 40f);
            var r = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);
            GUI.Box(r, GUIContent.none, sCaja);
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, 3f), tBoton);
            GUILayout.BeginArea(new Rect(r.x + 24, r.y + 14, r.width - 48, r.height - 28));
            GUILayout.Label("Opciones", sTitulo, GUILayout.Height(42));
            GUILayout.BeginHorizontal();
            for (int i = 0; i < Pestanas.Length; i++)
                if (GUILayout.Button(Pestanas[i], i == pestana ? sOpcionSel : sOpcion, GUILayout.Height(30))) pestana = i;
            GUILayout.EndHorizontal();
            GUILayout.Space(10);
            desplOpciones = GUILayout.BeginScrollView(desplOpciones);
            switch (pestana)
            {
                case 0:
                    GUILayout.Label("Modo", sEtiqueta);
                    GUILayout.BeginHorizontal();
                    for (int i = 0; i < 3; i++)
                        if (GUILayout.Button(CortosModo[i], (int)modo == i ? sOpcionSel : sOpcion, GUILayout.Height(30))) modo = (Modo)i;
                    GUILayout.EndHorizontal();
                    GUILayout.Space(8);
                    GUILayout.Label("Resolución", sEtiqueta);
                    if (modo == Modo.SinBordes)
                    {
                        var m = Monitor();
                        GUILayout.Label(m.width + " × " + m.height, sOpcion, GUILayout.Height(30));
                    }
                    else
                    {
                        // solo la elegida; al pulsarla se despliega la lista
                        var rr = resoluciones[Mathf.Clamp(indice, 0, resoluciones.Count - 1)];
                        if (GUILayout.Button(rr.width + " × " + rr.height + "   ▼", listaRes ? sOpcionSel : sOpcion, GUILayout.Height(30))) listaRes = !listaRes;
                        if (listaRes)
                        {
                            desplRes = GUILayout.BeginScrollView(desplRes, GUILayout.Height(Mathf.Min(220f, resoluciones.Count * 28f + 6f)));
                            for (int i = 0; i < resoluciones.Count; i++)
                                if (GUILayout.Button(resoluciones[i].width + " × " + resoluciones[i].height, i == indice ? sOpcionSel : sOpcion, GUILayout.Height(26))) { indice = i; listaRes = false; }
                            GUILayout.EndScrollView();
                        }
                    }
                    GUILayout.Space(8);
                    int fi = System.Array.IndexOf(OpcionesFps, Fps); if (fi < 0) fi = 0;
                    var nombresFps = new string[OpcionesFps.Length];
                    for (int i = 0; i < OpcionesFps.Length; i++) nombresFps[i] = OpcionesFps[i] <= 0 ? "Sin límite" : OpcionesFps[i].ToString();
                    int nf = Fila("Límite de FPS", nombresFps, fi);
                    if (nf != fi) CambiarFps(OpcionesFps[nf]);
                    int esc = Barra("Escala de resolución", Opciones.EscalaPct, 50, 150);
                    if (esc != Opciones.EscalaPct) Opciones.EscalaPct = esc;
                    break;
                case 1:
                    int nd = FilaDeMenosAMas("Distancia de dibujado", Opciones.NombresDistancia, Opciones.Distancia);
                    if (nd != Opciones.Distancia) Opciones.Distancia = nd;
                    int nb = FilaDeMenosAMas("Densidad del bosque", Opciones.NombresDensidad, Opciones.Densidad);
                    if (nb != Opciones.Densidad) Opciones.Densidad = nb;
                    int ns = FilaDeMenosAMas("Sombras", Opciones.NombresSombras, Opciones.Sombras);
                    if (ns != Opciones.Sombras) Opciones.Sombras = ns;
                    int br = Barra("Brillo (gamma)", Opciones.Brillo, 0, 100);
                    if (br != Opciones.Brillo) Opciones.Brillo = br;
                    int fv = Barra("Campo de visión (FOV)", Opciones.Fov, 0, 100);
                    if (fv != Opciones.Fov) Opciones.Fov = fv;
                    int cf = Fila("Contador de FPS", new[] { "No", "Sí" }, Opciones.ContadorFps ? 1 : 0);
                    if ((cf == 1) != Opciones.ContadorFps) Opciones.ContadorFps = cf == 1;
                    break;
                case 2:
                    GUILayout.Label("Tu nombre", sEtiqueta);
                    if (nombreEditado == null) nombreEditado = plugin.NombrePropio;
                    GUILayout.BeginHorizontal();
                    nombreEditado = GUILayout.TextField(nombreEditado ?? "", 20, sOpcion, GUILayout.Height(30));
                    if (GUILayout.Button("Guardar", sBotonSec, GUILayout.Width(150), GUILayout.Height(30))) plugin.CambiarNombre(nombreEditado);
                    GUILayout.EndHorizontal();
                    GUILayout.Space(8);
                    int mn = Fila("Mostrar nombre y distancia del compañero", new[] { "No", "Sí" }, Opciones.NombreCompanero ? 1 : 0);
                    if ((mn == 1) != Opciones.NombreCompanero) Opciones.NombreCompanero = mn == 1;
                    if (!Opciones.LasDecideOtro)   // la frecuencia de los eventos la elige solo el anfitrión
                    {
                        int nev = Fila("Frecuencia de los eventos", Opciones.NombresEventos, Opciones.Eventos);
                        if (nev != Opciones.Eventos) Opciones.Eventos = nev;
                    }
                    break;
                case 4:
                    DibujarControles();
                    break;
                case 3:
                    int volg = Barra("Volumen general", Opciones.VolumenGeneral, 0, 100);
                    if (volg != Opciones.VolumenGeneral) Opciones.VolumenGeneral = volg;
                    int vol = Barra("Volumen de efectos", Opciones.VolumenEfectos, 0, 100);
                    if (vol != Opciones.VolumenEfectos) Opciones.VolumenEfectos = vol;
                    int nt = Fila("Tamaño de los textos", Opciones.NombresTexto, Opciones.Texto);
                    if (nt != Opciones.Texto) Opciones.Texto = nt;
                    break;
            }
            GUILayout.EndScrollView();
            GUILayout.Space(8);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Cerrar", sBotonSec, GUILayout.Height(40))) Cerrar();
            if (pestana == 0) { GUILayout.Space(12); if (GUILayout.Button("Aplicar pantalla", sBoton, GUILayout.Height(40))) { Aplicar(); Cerrar(); } }
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
            enfocar = false;
        }

        public void OrdenCerrar() { Cerrar(); }
        public void OrdenAplicar(int m, string tam)
        {
            Abrir(); modo = (Modo)m;
            var p = tam.Split('x');
            for (int i = 0; i < resoluciones.Count; i++) if (resoluciones[i].width.ToString() == p[0] && resoluciones[i].height.ToString() == p[1]) indice = i;
            Aplicar(); Cerrar();
        }
        public string Describir() { long st = 0; try { var h = Ventana(); if (h != IntPtr.Zero) st = GetWindowLongPtr(h, GWL_STYLE).ToInt64(); } catch { } return "ventana=" + ((st & WS_POPUP) != 0 ? "SIN BORDE" : (st & 0x00C00000L) != 0 ? "con borde" : "?") + " pantalla " + Screen.width + "x" + Screen.height + " completa=" + Screen.fullScreen + " modo guardado=" + PlayerPrefs.GetInt(PrefModo, -1) + " monitor=" + Monitor().width + "x" + Monitor().height; }
    }
}

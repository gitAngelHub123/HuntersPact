// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace WitchHuntCoop
{
    /// Menú principal: agrega "Crear sala" y "Unirse a sala" con el mismo aspecto que los
    /// botones del juego (son copias de "Opciones" sin su lógica). Cada uno abre un cuadro
    /// para el nombre, la IP y el puerto. Con la sala activa los botones pasan a "Cerrar sala"
    /// o "Salir de la sala" y abajo se ve el estado de la conexión.
    public class MenuSala
    {
        const int EscenaMenu = 3;
        enum Cuadro { Ninguno, Crear, Unirse }

        readonly Plugin plugin;
        Button botonCrear, botonUnirse, botonPantalla;
        Text textoCrear, textoUnirse;
        Font fuente, fuenteEstilos;
        // Posiciones a mano (el menú del juego no usa LayoutGroup): lugar del primer botón nuestro,
        // distancia entre botones y los botones del juego que van debajo con su posición original.
        bool aMano;
        Vector2 hueco;
        float paso;
        readonly List<KeyValuePair<RectTransform, Vector2>> debajo = new List<KeyValuePair<RectTransform, Vector2>>();
        Cuadro cuadro;
        bool enfocar;
        EventSystem sistemaApagado;
        string ipLocal;

        bool submenu;
        float proximoSubmenu;

        // Inicio automático: al conectarse en el menú, el anfitrión cuenta 3 s y avisa al invitado;
        // los dos pulsan "Continuar" (o "Nueva partida" si no hay partida cooperativa).
        const float Cuenta = 3f;
        bool iniciadoEstaConexion;   // ya se inició (o se canceló) con esta conexión
        float cuentaHasta;           // anfitrión: momento de empezar (0 = sin cuenta)
        bool pendienteIniciar;       // invitado: llegó la orden y el menú aún no está listo

        public bool Abierto { get { return cuadro != Cuadro.Ninguno; } }

        public MenuSala(Plugin p) { plugin = p; }

        bool EnMenu { get { return SceneManager.GetActiveScene().buildIndex == EscenaMenu; } }

        // ---------------- Botones del menú ----------------

        float vigilarContinuar;   // si "Continuar" no cargó nada (partida vacía o rota), se empieza una nueva

        public void Actualizar()
        {
            if (vigilarContinuar > 0f && Time.realtimeSinceStartup > vigilarContinuar)
            {
                vigilarContinuar = 0f;
                if (EnMenu)
                {
                    var nueva = Buscar("NewGame");
                    plugin.Log("Inicio: 'Continuar' no cargó la partida (vacía o sin empezar); se empieza una nueva.");
                    plugin.Aviso = "Tu partida cooperativa estaba vacía: empieza una nueva.";
                    if (nueva != null) { var r = Guardado.RespaldarCoop(); if (r != null) plugin.Log("Inicio: Continuar no cargó; partida respaldada por si acaso en " + r); Guardado.ReiniciarHistoria(plugin); confirmarNueva = true; nueva.onClick.Invoke(); }
                }
            }
            if (!plugin.Conectado) { iniciadoEstaConexion = false; cuentaHasta = 0f; pendienteIniciar = false; }
            // "¿Comenzar nueva partida?" (escena 4): el inicio automático también confirma.
            // "Start a new game?" (escena 4): no se muestra; se marca el modo permanente según lo elegido y se confirma.
            if (confirmarNueva && SceneManager.GetActiveScene().buildIndex == 4)
            {
                foreach (var cv in UnityEngine.Object.FindObjectsOfType<Canvas>()) if (cv.enabled && !cv.name.StartsWith("coop")) cv.enabled = false;
                if (Time.timeSinceLevelLoad > 0.3f)
                {
                    var si = Buscar("New Game");
                    if (si != null)
                    {
                        confirmarNueva = false;
                        foreach (var tg in UnityEngine.Object.FindObjectsOfType<Toggle>()) tg.isOn = permaElegido;
                        var pm = HutongGames.PlayMaker.FsmVariables.GlobalVariables.FindFsmBool("perma_mode");
                        if (pm != null) pm.Value = permaElegido;
                        plugin.Log("Inicio: partida nueva" + (permaElegido ? " en modo permanente" : "") + " (sin la pantalla de confirmación).");
                        si.onClick.Invoke();
                    }
                }
            }
            if (!EnMenu) { if (Abierto) Cerrar(); pendienteIniciar = false; cuentaHasta = 0f; return; }
            if (botonCrear == null) Crear();
            if (botonCrear == null) return;

            // Los botones del juego se ocultan al abrir Opciones o Créditos: los nuestros los siguen.
            var opciones = Buscar("Options");
            bool visibles = opciones != null && opciones.gameObject.activeInHierarchy;
            bool activa = plugin.RedActiva;
            bool verCrear = visibles && (!activa || plugin.EsAnfitrion);
            bool verUnirse = visibles && (!activa || !plugin.EsAnfitrion || plugin.SalaSteamEsperando);   // anfitrión en Steam esperando: «Invitar amigo»
            botonCrear.gameObject.SetActive(verCrear);
            botonUnirse.gameObject.SetActive(verUnirse);
            if (botonPantalla != null) botonPantalla.gameObject.SetActive(visibles);
            if (aMano && visibles)
            {
                // Sin huecos: los visibles van seguidos y lo de abajo sube o baja según cuántos hay.
                int n = 0;
                if (verCrear) ((RectTransform)botonCrear.transform).anchoredPosition = hueco - new Vector2(0f, paso * n++);
                if (verUnirse) ((RectTransform)botonUnirse.transform).anchoredPosition = hueco - new Vector2(0f, paso * n++);
                if (botonPantalla != null) ((RectTransform)botonPantalla.transform).anchoredPosition = hueco - new Vector2(0f, paso * n++);
                foreach (var kv in debajo)
                    if (kv.Key != null) kv.Key.anchoredPosition = kv.Value - new Vector2(0f, paso * n);
            }
            if (Time.realtimeSinceStartup > proximoSubmenu)
            {
                proximoSubmenu = Time.realtimeSinceStartup + 0.3f;
                var borrar = Buscar("delete_saves_button");   // solo existe con el panel de Opciones abierto
                submenu = borrar != null && borrar.gameObject.activeInHierarchy;
            }
            InicioAutomatico();
            textoCrear.text = activa ? "Cerrar sala" : "Crear sala";
            textoUnirse.text = plugin.SalaSteamEsperando ? "Invitar amigo" : activa ? "Salir de la sala" : "Unirse a sala";
        }

        static Button Buscar(string nombre)
        {
            foreach (var b in UnityEngine.Object.FindObjectsOfType<Button>())
                if (b.name == nombre) return b;
            return null;
        }

        void Crear()
        {
            var nueva = Buscar("NewGame");
            var opciones = Buscar("Options");
            var creditos = Buscar("Credits");
            var salir = Buscar("Quit");
            if (nueva == null || opciones == null) return;

            var rtNueva = (RectTransform)nueva.transform;
            var rtOpc = (RectTransform)opciones.transform;
            paso = rtNueva.anchoredPosition.y - rtOpc.anchoredPosition.y;   // distancia entre botones
            if (Mathf.Abs(paso) < 1f) paso = 45f;

            botonCrear = Copiar(opciones, "CoopCrearSala", out textoCrear);
            botonUnirse = Copiar(opciones, "CoopUnirseSala", out textoUnirse);
            if (botonCrear == null || botonUnirse == null) return;
            // (La pantalla ya no tiene botón propio en el menú principal: está dentro de Opciones.)
            botonCrear.onClick.AddListener(() => Pulsado(true));
            botonUnirse.onClick.AddListener(() => Pulsado(false));

            var padre = rtOpc.parent;
            var grupo = padre != null ? padre.GetComponent<LayoutGroup>() : null;
            if (grupo != null)
            {
                // Menú ordenado por el propio Unity: basta con ponerlos tras "Nueva partida".
                int i = nueva.transform.GetSiblingIndex();
                botonCrear.transform.SetSiblingIndex(i + 1);
                botonUnirse.transform.SetSiblingIndex(i + 2);
                if (botonPantalla != null) botonPantalla.transform.SetSiblingIndex(i + 3);
            }
            else
            {
                // Posiciones a mano: los nuevos ocupan el sitio de "Opciones" y lo de abajo baja
                // (se recoloca en cada fotograma en Actualizar).
                aMano = true;
                hueco = rtOpc.anchoredPosition;
                debajo.Clear();
                foreach (var b in new[] { opciones, creditos, salir })
                {
                    if (b == null || b.transform.parent != padre) continue;
                    var rt = (RectTransform)b.transform;
                    debajo.Add(new KeyValuePair<RectTransform, Vector2>(rt, rt.anchoredPosition));
                }
            }

            var t = opciones.GetComponentInChildren<Text>(true);
            if (t != null) fuente = t.font;
            if (fuente != null) plugin.Log("Menú: letra '" + fuente.name + "' dinámica=" + fuente.dynamic + " tamaño=" + fuente.fontSize);
            plugin.Log("Menú: botones de sala agregados (paso " + paso.ToString("0") + ", " + (grupo != null ? "con" : "sin") + " LayoutGroup).");
        }

        /// Copia un botón del juego quitándole todo lo que no sea aspecto: sus máquinas de
        /// PlayMaker, traducciones y eventos harían lo mismo que el original.
        static Button Copiar(Button original, string nombre, out Text texto)
        {
            // Se copia dentro de un objeto inactivo para que no se ejecute nada al nacer.
            var cuna = new GameObject("CoopCuna");
            cuna.SetActive(false);
            var copia = UnityEngine.Object.Instantiate(original.gameObject, cuna.transform);
            copia.name = nombre;

            var quitar = new List<Component>();
            foreach (var c in copia.GetComponentsInChildren<Component>(true))
            {
                if (c == null || c is Transform || c is CanvasRenderer || c is Animator) continue;
                if (c is Graphic || c is Button || c is BaseMeshEffect || c is LayoutElement || c is CanvasGroup) continue;
                quitar.Add(c);   // PlayMakerFSM, EventTrigger, traductores y demás
            }
            foreach (var c in quitar) UnityEngine.Object.DestroyImmediate(c);

            var boton = copia.GetComponent<Button>();
            if (boton == null) boton = copia.AddComponent<Button>();
            boton.onClick = new Button.ButtonClickedEvent();
            boton.interactable = true;
            texto = copia.GetComponentInChildren<Text>(true);

            copia.transform.SetParent(original.transform.parent, false);
            copia.transform.localScale = original.transform.localScale;
            copia.SetActive(true);
            UnityEngine.Object.Destroy(cuna);
            return boton;
        }

        // ---------------- Inicio automático ----------------

        bool MenuListo
        {
            get
            {
                if (Time.timeSinceLevelLoad < 1.5f) return false;   // el menú se recarga al cambiar de modo
                var c = Buscar("Continue");
                var n = Buscar("NewGame");   // sin partida guardada, "Continuar" no aparece
                return (c != null && c.isActiveAndEnabled) || (n != null && n.isActiveAndEnabled);
            }
        }

        // Al conectarse los dos en el menú aparece la ventana de elección: el anfitrión elige
        // "Continuar" (su partida cooperativa) o "Partida nueva" (semilla nueva); el invitado espera.
        bool Eligiendo { get { return plugin.Conectado && !iniciadoEstaConexion && EnMenu && MenuListo && !Abierto; } }

        void InicioAutomatico()
        {
            if (!plugin.Conectado || iniciadoEstaConexion) return;
            if (!plugin.EsAnfitrion && pendienteIniciar && MenuListo)
            {
                pendienteIniciar = false; iniciadoEstaConexion = true;
                if (sistemaElegir != null) { sistemaElegir.enabled = true; sistemaElegir = null; }   // los botones del juego tienen que responder
                IniciarLocal();
            }
            // Mientras se elige, los botones del menú del juego no responden (la ventana va encima).
            var es = EventSystem.current;
            if (Eligiendo) { if (es != null && es.enabled) { es.enabled = false; sistemaElegir = es; } }
            else if (sistemaElegir != null) { sistemaElegir.enabled = true; sistemaElegir = null; }
        }
        EventSystem sistemaElegir;

        /// El anfitrión eligió: 0 continuar, 1 partida nueva.
        void Elegir(byte modo)
        {
            if (!plugin.EsAnfitrion || iniciadoEstaConexion) return;
            iniciadoEstaConexion = true;
            modoInicio = modo;
            if (sistemaElegir != null) { sistemaElegir.enabled = true; sistemaElegir = null; }
            plugin.Log("Inicio: el anfitrión eligió " + (modo == 0 ? "continuar" : "partida nueva"));
            plugin.EnviarIniciar(modoInicio, (byte)(permaElegido ? 1 : 0));
            IniciarLocal();
        }

        public void OrdenElegir(int modo, int dificultad = 0, bool perma = false) { if (modo == 1) { Opciones.DificultadElegida = dificultad; permaElegido = perma; } Elegir((byte)modo); }

        GUIStyle sGrande, sTituloEleccion, sEspera;
        GUIStyle sDescDif;

        /// Lo que trae cada dificultad (los números salen de Opciones.FactoresDificultad y de la configuración del mod).
        static string DescripcionDificultad(int d)
        {
            switch (d)
            {
                case 1: return "• Botín (ganzúas, balas, plantas) ×3,3 · oro ×6,7 · cofres ×15\n• Daño de enemigos normales: 3-4,5 de daño, jefes 15-22 de daño, vida de enemigos x1,5, vida de jefes x3\n• Enemigos potenciados: 4,5 %\n• Empiezas con 50 de oro";
                case 2: return "• Botín (ganzúas, balas, plantas) ×1,25 · oro ×2,5 · cofres ×10\n• Daño de enemigos normales: 6-10 de daño, jefes 30-50 de daño, vida de enemigos x2, vida de jefes x4\n• Enemigos potenciados: 6 %\n• Los enemigos normales y guardianes reviven al minuto\n• En el bosque pierdes 1 de vida cada 2 minutos (nunca te mata)\n• Empiezas con 50 de oro";
                default: return "• Botín (ganzúas, balas, plantas) ×5 · oro ×10 · cofres ×25\n• Daño de enemigos normales: 2-3 de daño, jefes 10-15 de daño, vida normal de enemigos, vida de jefes x2\n• Enemigos potenciados: 3 %\n• Empiezas con 50 de oro";
            }
        }
        bool eligiendoDificultad, permaElegido, fuegoAmigoElegido; int dificultadSel;

        void DibujarEleccion()
        {
            GUI.depth = -100;
            var prev = GUI.color; GUI.color = Color.black;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = prev;
            if (sGrande == null)
            {
                sDescDif = new GUIStyle(sEtiqueta) { fontSize = 14, wordWrap = true, alignment = TextAnchor.UpperLeft };
                sDescDif.normal.textColor = new Color(0.88f, 0.84f, 0.74f);
                sGrande = new GUIStyle(sBoton) { fontSize = 26 };
                sTituloEleccion = new GUIStyle(sTitulo) { fontSize = 24, wordWrap = true, alignment = TextAnchor.MiddleCenter };
                sEspera = new GUIStyle(sTitulo) { fontSize = 20, wordWrap = true, alignment = TextAnchor.MiddleCenter };
            }
            float w = Mathf.Min(620f, Screen.width - 40f), h = Mathf.Min(670f, Screen.height - 20f);
            var r = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);
            GUILayout.BeginArea(r);
            GUILayout.Label("Conectado con " + plugin.NombreCompanero, sTituloEleccion, GUILayout.Height(60));
            GUILayout.Space(16);
            if (plugin.EsAnfitrion)
            {
                bool hay = Guardado.ExisteCoop();
                GUI.enabled = hay;
                if (GUILayout.Button("Continuar", sGrande, GUILayout.Height(64))) Elegir(0);
                GUI.enabled = true;
                GUILayout.Space(14);
                if (!eligiendoDificultad)
                {
                    if (GUILayout.Button("Partida nueva", sGrande, GUILayout.Height(64))) eligiendoDificultad = true;
                }
                else
                {
                    // la dificultad se elige una sola vez, al crear la partida
                    GUILayout.Label("Modo permanente", sEtiqueta);
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("No", !permaElegido ? sBoton : sBotonSec, GUILayout.Height(34))) permaElegido = false;
                    if (GUILayout.Button("Sí", permaElegido ? sBoton : sBotonSec, GUILayout.Height(34))) permaElegido = true;
                    GUILayout.EndHorizontal();
                    GUILayout.Space(10);
                    GUILayout.Label("Fuego amigo (poder dispararle al compañero)", sEtiqueta);
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("No", !fuegoAmigoElegido ? sBoton : sBotonSec, GUILayout.Height(34))) fuegoAmigoElegido = false;
                    if (GUILayout.Button("Sí", fuegoAmigoElegido ? sBoton : sBotonSec, GUILayout.Height(34))) fuegoAmigoElegido = true;
                    GUILayout.EndHorizontal();
                    GUILayout.Space(10);
                    GUILayout.Label("Dificultad de la partida nueva", sEtiqueta);
                    GUILayout.BeginHorizontal();
                    for (int i = 0; i < Opciones.NombresDificultad.Length; i++)
                        if (GUILayout.Button(Opciones.NombresDificultad[i], i == dificultadSel ? sBoton : sBotonSec, GUILayout.Height(48))) dificultadSel = i;   // solo se marca (rojo); la partida empieza con "Iniciar partida"
                    GUILayout.EndHorizontal();
                    GUILayout.Space(6);
                    GUILayout.Label(DescripcionDificultad(dificultadSel), sDescDif, GUILayout.Height(150));
                    GUILayout.Space(6);
                    if (GUILayout.Button("Iniciar partida", sGrande, GUILayout.Height(64)))
                    {
                        Opciones.DificultadElegida = dificultadSel; Opciones.FuegoAmigoElegido = fuegoAmigoElegido ? 1 : 0;
                        eligiendoDificultad = false; Elegir(1);
                    }
                    if (GUILayout.Button("Volver", sBotonSec, GUILayout.Height(30))) eligiendoDificultad = false;
                }
            }
            else
            {
                GUILayout.Space(30);
                GUILayout.Label("Esperando a que " + plugin.NombreCompanero + " elija: continuar la partida o empezar una nueva...", sEspera, GUILayout.Height(110));
            }
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(plugin.EsAnfitrion ? "Cerrar sala" : "Salir de la sala", sBotonSec, GUILayout.Height(36)))
            {
                plugin.OrdenDesconectar();
                plugin.Aviso = plugin.EsAnfitrion ? "Sala cerrada." : "Saliste de la sala.";
            }
            GUILayout.EndArea();
        }

        byte modoInicio;
        bool confirmarNueva;

        public void RecibirIniciar(byte modo, bool perma = false)
        {
            modoInicio = modo; permaElegido = perma;
            if (!EnMenu || iniciadoEstaConexion) return;
            pendienteIniciar = true;
            if (Abierto) Cerrar();
        }

        void IniciarLocal()
        {
            if (Abierto) Cerrar();
            // Lo decide el anfitrión: continuar su partida coop o empezar una nueva (si este PC no
            // tiene partida coop, no hay qué continuar: también nueva).
            var continuar = Buscar("Continue");
            bool nueva = modoInicio == 1 || !Guardado.ExisteCoop();
            Button b = !nueva && continuar != null && continuar.interactable ? continuar : Buscar("NewGame");
            if (b == null) { plugin.Log("Inicio automático: no encontré Continuar ni Nueva partida."); return; }
            plugin.Log("Inicio automático: pulsando " + b.name + ".");
            confirmarNueva = b.name == "NewGame";
            if (confirmarNueva) { plugin.Log("Inicio: partida nueva; la anterior se reemplaza sin respaldo (pedido 2026-11-19)"); Guardado.ReiniciarHistoria(plugin); }
            vigilarContinuar = !confirmarNueva ? Time.realtimeSinceStartup + 8f : 0f;
            b.onClick.Invoke();
        }

        public string TextoConectado(string companero)
        {

            if (iniciadoEstaConexion) return "";   // ya se pulsó: está cargando
            if (!plugin.EsAnfitrion && !iniciadoEstaConexion)
                return "Conectado con " + companero + " – esperando a que empiece la partida...";
            return "Conectado con " + companero + " – elijan Continuar o Nueva partida";
        }

        void Pulsado(bool crear)
        {
            if (!crear && plugin.SalaSteamEsperando) { plugin.InvitarSteam(); return; }   // (2026-11-20) Steam: invitar
            if (!plugin.RedActiva && plugin.UsarSteam)
            {
                // Steam: crear = sala de amigos + ventana de invitar; unirse = aceptar la invitación del amigo en Steam
                if (crear) plugin.OrdenAnfitrionSteam();
                else { Red.AbrirAmigos(); plugin.Aviso = "Acepta la invitación de tu amigo en Steam (Shift+Tab → Amigos o el chat)."; }
                return;
            }
            if (plugin.RedActiva)
            {
                plugin.OrdenDesconectar();
                plugin.Aviso = crear ? "Sala cerrada." : "Saliste de la sala.";
                return;
            }
            Abrir(crear ? Cuadro.Crear : Cuadro.Unirse);
        }

        public void OrdenAbrir(bool crear) { Pulsado(crear); }
        public void OrdenAceptar() { if (Abierto) Aceptar(); }
        public void OrdenCerrar() { Cerrar(); }

        // ---------------- Cuadro de datos ----------------

        void Abrir(Cuadro c)
        {
            cuadro = c;
            enfocar = true;
            if (c == Cuadro.Crear && ipLocal == null) ipLocal = BuscarIpLocal();
            // Con el cuadro abierto, los botones del juego que quedan debajo no deben recibir clics.
            var es = EventSystem.current;
            if (es != null && es.enabled) { es.enabled = false; sistemaApagado = es; }
        }

        void Cerrar()
        {
            cuadro = Cuadro.Ninguno;
            if (sistemaApagado != null) sistemaApagado.enabled = true;
            sistemaApagado = null;
        }

        void Aceptar()
        {
            if (cuadro == Cuadro.Unirse && plugin.IpTexto.Trim().Length == 0) { error = "Escribe la IP de quien creó la sala."; return; }
            int puerto;
            if (!int.TryParse(plugin.PuertoTexto.Trim(), out puerto) || puerto <= 0 || puerto > 65535) { error = "El puerto debe ser un número entre 1 y 65535."; return; }
            error = null;
            var c = cuadro;
            Cerrar();
            if (c == Cuadro.Crear) plugin.OrdenAnfitrion(); else plugin.OrdenUnirse("");
        }

        static string BuscarIpLocal()
        {
            try
            {
                // Sin enviar nada: el sistema elige la interfaz con la que saldría a la red local.
                using (var s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
                {
                    s.Connect(new IPEndPoint(IPAddress.Parse("192.168.1.1"), 9));
                    var ip = ((IPEndPoint)s.LocalEndPoint).Address;
                    if (!IPAddress.IsLoopback(ip) && !ip.Equals(IPAddress.Any)) return ip.ToString();
                }
            }
            catch { }
            try
            {
                foreach (var ip in Dns.GetHostAddresses(Dns.GetHostName()))
                    if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip)) return ip.ToString();
            }
            catch { }
            return "";
        }

        // ---------------- Dibujo ----------------

        string error;
        GUIStyle sTitulo, sEtiqueta, sCampo, sBoton, sBotonSec, sNota, sError, sEstado, sSombra, sFondo, sCaja;
        Texture2D tFondo, tCaja, tCampo, tCampoFoco, tBoton, tBotonHover, tBotonSec, tBotonSecHover;

        static Texture2D Color1(Color c)
        {
            var t = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            t.SetPixel(0, 0, c); t.Apply();
            return t;
        }

        void Estilos()
        {
            if (sTitulo != null && tFondo != null && fuenteEstilos == fuente) return;
            fuenteEstilos = fuente;
            tFondo = Color1(new Color(0f, 0f, 0f, 0.7f));
            tCaja = Color1(new Color(0.07f, 0.07f, 0.08f, 1f));
            tCampo = Color1(new Color(0.16f, 0.16f, 0.18f, 1f));
            tCampoFoco = Color1(new Color(0.22f, 0.22f, 0.25f, 1f));
            tBoton = Color1(new Color(0.55f, 0.12f, 0.1f, 1f));
            tBotonHover = Color1(new Color(0.7f, 0.17f, 0.13f, 1f));
            tBotonSec = Color1(new Color(0.2f, 0.2f, 0.22f, 1f));
            tBotonSecHover = Color1(new Color(0.28f, 0.28f, 0.31f, 1f));
            bool dinamica = fuente != null;   // la letra del juego (aunque no sea dinámica)

            sFondo = new GUIStyle(); sFondo.normal.background = tFondo;
            sCaja = new GUIStyle(); sCaja.normal.background = tCaja;

            sTitulo = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 30 };
            if (dinamica) sTitulo.font = fuente;
            sTitulo.normal.textColor = new Color(0.95f, 0.93f, 0.88f);

            sEtiqueta = new GUIStyle(GUI.skin.label) { fontSize = 15 };
            sEtiqueta.normal.textColor = new Color(0.75f, 0.73f, 0.68f);

            sCampo = new GUIStyle(GUI.skin.textField) { fontSize = 18, padding = new RectOffset(10, 10, 7, 7), border = new RectOffset(0, 0, 0, 0) };
            sCampo.normal.background = tCampo; sCampo.hover.background = tCampoFoco;
            sCampo.focused.background = tCampoFoco; sCampo.active.background = tCampoFoco;
            sCampo.normal.textColor = sCampo.hover.textColor = sCampo.focused.textColor = sCampo.active.textColor = Color.white;

            sBoton = new GUIStyle(GUI.skin.button) { fontSize = 20, alignment = TextAnchor.MiddleCenter, border = new RectOffset(0, 0, 0, 0) };
            if (dinamica) sBoton.font = fuente;
            sBoton.normal.background = tBoton; sBoton.hover.background = tBotonHover; sBoton.active.background = tBotonHover;
            sBoton.normal.textColor = sBoton.hover.textColor = sBoton.active.textColor = Color.white;
            sBotonSec = new GUIStyle(sBoton);
            sBotonSec.normal.background = tBotonSec; sBotonSec.hover.background = tBotonSecHover; sBotonSec.active.background = tBotonSecHover;

            sNota = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true };
            sNota.normal.textColor = new Color(0.62f, 0.6f, 0.56f);
            sError = new GUIStyle(sNota);
            sError.normal.textColor = new Color(1f, 0.45f, 0.4f);

            sEstado = new GUIStyle(GUI.skin.label) { fontSize = 17, alignment = TextAnchor.MiddleCenter };
            if (dinamica) sEstado.font = fuente;
            sEstado.normal.textColor = new Color(1f, 0.85f, 0.5f);
            sSombra = new GUIStyle(sEstado);
            sSombra.normal.textColor = new Color(0f, 0f, 0f, 0.85f);
        }

        public void Dibujar()
        {
            if (!EnMenu) return;
            Estilos();
            if (Eligiendo) { DibujarEleccion(); return; }
            if (!Abierto) { DibujarEstado(); return; }

            Event e = Event.current;
            if (e.type == EventType.KeyDown)
            {
                if (e.keyCode == KeyCode.Escape) { Cerrar(); e.Use(); return; }
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) { Aceptar(); e.Use(); }
            }

            GUI.depth = -100;
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), GUIContent.none, sFondo);
            bool crear = cuadro == Cuadro.Crear;
            float w = 440f, h = crear ? 380f : 440f;
            var r = new Rect((Screen.width - w) / 2f, Mathf.Max(8f, Screen.height * 0.58f - h / 2f), w, h);
            GUI.Box(r, GUIContent.none, sCaja);
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, 3f), tBoton);

            GUILayout.BeginArea(new Rect(r.x + 28, r.y + 20, r.width - 56, r.height - 40));
            GUILayout.Label(crear ? "Crear sala" : "Unirse a sala", sTitulo, GUILayout.Height(44));
            GUILayout.Space(10);

            GUILayout.Label("Tu nombre", sEtiqueta);
            GUI.SetNextControlName("coopNombre");
            plugin.NombreTexto = GUILayout.TextField(plugin.NombreTexto, 24, sCampo);
            GUILayout.Space(8);

            if (!crear)
            {
                GUILayout.Label("IP de quien creó la sala", sEtiqueta);
                GUI.SetNextControlName("coopIp");
                plugin.IpTexto = GUILayout.TextField(plugin.IpTexto, 64, sCampo);
                GUILayout.Space(8);
            }

            GUILayout.Label("Puerto", sEtiqueta);
            plugin.PuertoTexto = GUILayout.TextField(plugin.PuertoTexto, 5, sCampo);
            GUILayout.Space(10);

            if (error != null) GUILayout.Label(error, sError);
            else if (crear)
                GUILayout.Label(string.IsNullOrEmpty(ipLocal)
                    ? "Tu compañero se une con tu IP y este puerto."
                    : "Tu compañero se une con la IP " + ipLocal + " (misma red) y este puerto.", sNota);
            else
                GUILayout.Label("En el mismo PC usa 127.0.0.1.", sNota);

            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Cancelar", sBotonSec, GUILayout.Height(42))) Cerrar();
            GUILayout.Space(12);
            if (GUILayout.Button(crear ? "Crear" : "Unirse", sBoton, GUILayout.Height(42))) Aceptar();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            if (enfocar && e.type == EventType.Repaint)
            {
                enfocar = false;
                string ip = plugin.IpTexto.Trim();
                GUI.FocusControl(!crear && (ip.Length == 0 || ip.EndsWith(".")) ? "coopIp" : "coopNombre");
            }
        }

        void DibujarEstado()
        {
            string texto = plugin.TextoEstadoSala();
            if (string.IsNullOrEmpty(texto) || submenu) return;
            var r = new Rect(0, Screen.height * 0.915f, Screen.width, 28f);
            GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), texto, sSombra);
            GUI.Label(r, texto, sEstado);
        }
    }
}

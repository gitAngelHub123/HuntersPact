// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using System;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WitchHuntCoop
{
    [BepInPlugin(Guid, Nombre, Version)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "angel.witchhunt.coop";
        public const string Nombre = "Hunters' Pact";   // (2026-11-20) nombre público del mod (GUID y DLL se mantienen para no duplicar instalaciones)
        public const string Version = "0.2.0";

        const float IntervaloEstado = 0.05f;   // 20 envíos por segundo
        const float IntervaloHola = 1f;
        const float TiempoLimite = 60f;   // sin datos durante esto = desconectado (sin contar el tiempo que este juego estuvo congelado)
        const float SenalDebil = 2f;      // a partir de aquí se avisa en pantalla

        ConfigEntry<string> cfgNombre, cfgIp;
        ConfigEntry<int> cfgPuerto;
        ConfigEntry<KeyCode> cfgTecla;
        int difLogueada = -1;
        ConfigEntry<float> cfgVidaJefes, cfgVidaNormales, cfgVelocidad, cfgDanoMin, cfgDanoMax, cfgJefeMin, cfgJefeMax;

        readonly Red red = new Red();
        bool conectado;
        string nombreCompanero = "";
        float ultimoRecibido, ultimoEstado, ultimoHola, ultimoFotogramaRed;
        float ultimoFotograma;   // para detectar congelamientos del juego
        string aviso = "";
        public string Aviso { get { return aviso; } set { aviso = value; } }

        // Panel
        bool panelAbierto;
        Rect ventana = new Rect(40, 40, 340, 250);
        string ipTexto, puertoTexto, nombreTexto;
        public string IpTexto { get { return ipTexto; } set { ipTexto = value; } }
        public string PuertoTexto { get { return puertoTexto; } set { puertoTexto = value; } }
        public string NombreTexto { get { return nombreTexto; } set { nombreTexto = value; } }
        MenuSala menuSala;
        public MenuSala MenuSalaActual { get { return menuSala; } }
        public bool RedActiva { get { return red.Activo; } }
        // (2026-11-20) Steam P2P: la versión pública usa Steam (sala de amigos + invitación); la de pruebas, IP (2 copias, misma cuenta)
#if PRUEBAS
        public static bool ForzarIp = true;
#else
        public static bool ForzarIp = false;
#endif
        public bool UsarSteam { get { return !ForzarIp && Red.SteamDisponible; } }
        public bool SalaSteamEsperando { get { return red.ModoSteam && red.EsAnfitrion && red.Activo && !conectado; } }
        bool avisoInvitacion;
        Behaviour mouseLookDesactivado;

        // Jugador local
        FPSPlayer jugador;

        // Compañero
        Companero companero;

        Depuracion depuracion;
        Combate combate;
        public Combate CombateActual { get { return combate; } }
        Oro oro;
        Muerte muerte;
        public Muerte MuerteActual { get { return muerte; } }
        public Mundo mundo;
        public bool Conectado { get { return conectado; } }
        public bool EsAnfitrion { get { return red.EsAnfitrion; } }
        public Companero CompaneroActual { get { return companero; } }
        public FPSPlayer Jugador { get { return jugador; } }
        public KeyCode TeclaPanel { get { return cfgTecla.Value; } set { cfgTecla.Value = value; Config.Save(); } }
        public string NombreCompanero { get { return nombreCompanero; } }
        public int Escena { get { return EscenaActual(); } }
        public void EnviarMundo(byte[] datos) { red.Enviar(TipoMensaje.Mundo, datos); }
        public void EnviarRecogido(byte[] datos) { red.Enviar(TipoMensaje.Recogido, datos); }
        public void EnviarPerro(byte[] datos) { red.Enviar(TipoMensaje.Perro, datos); }
        public Perro perro; public Limpieza limpieza;
        public Aliados aliados;
        public Despertar despertar;
        public Eventos eventos;
        public Kit kit;
        public TiendaUI tiendaUI;
        public Pantalla pantalla;
        public void EnviarLibroTienda(byte[] datos) { red.Enviar(TipoMensaje.LibroTienda, datos); }
        public Municion municion;
        public void EnviarMapa(byte[] datos) { red.Enviar(TipoMensaje.Mapa, datos); }
        public Mapa mapa;
        public void EnviarCofre(byte[] datos) { red.Enviar(TipoMensaje.Cofre, datos); }
        public void EnviarEscopeta(byte[] datos) { red.Enviar(TipoMensaje.Escopeta, datos); }
        public void EnviarBallesta(byte[] datos) { red.Enviar(TipoMensaje.Ballesta, datos); }
        public void EnviarTesoro(byte[] datos) { red.Enviar(TipoMensaje.Tesoro, datos); }
        public void EnviarJefeCueva(byte[] datos) { red.Enviar(TipoMensaje.JefeCueva, datos); }
        public void EnviarSonidoCompanero(byte[] datos) { red.Enviar(TipoMensaje.SonidoCompanero, datos); }
        public void EnviarArmaCompanero(byte[] datos) { red.Enviar(TipoMensaje.ArmaCompanero, datos); }
        public void EnviarAliado(byte[] datos) { red.Enviar(TipoMensaje.Aliado, datos); }
        public void EnviarAliadoDano(byte[] datos) { red.Enviar(TipoMensaje.AliadoDano, datos); }
        public void EnviarEvento(byte[] datos) { for (int i = 0; i < 2; i++) red.Enviar(TipoMensaje.Evento, datos); }
        public void EnviarCabana(byte[] datos) { red.Enviar(TipoMensaje.Cabana, datos); }
        public void EnviarAjustes(byte[] datos) { red.Enviar(TipoMensaje.Ajustes, datos); }
        public string NombrePropio { get { return cfgNombre.Value; } }
        /// Cambia el nombre del jugador (se guarda y se le avisa al compañero).
        public void CambiarNombre(string n)
        {
            n = string.IsNullOrEmpty(n) ? "Cazador" : n.Trim();
            if (n.Length > 20) n = n.Substring(0, 20);
            cfgNombre.Value = n; nombreTexto = n;
            if (conectado) EnviarHola();
            Log("Nombre: " + n);
        }
        public void EnviarLevantar(bool porPerro = false) { var d = new byte[] { (byte)(porPerro ? 1 : 0) }; for (int i = 0; i < 3; i++) red.Enviar(TipoMensaje.Levantar, d); }
        public void EnviarIniciar(byte modo, byte perma = 0) { for (int i = 0; i < 3; i++) red.Enviar(TipoMensaje.Iniciar, new[] { modo, perma }); }   // UDP: por si se pierde uno

        public void EnviarFuegoAmigo(byte[] datos) { red.Enviar(TipoMensaje.FuegoAmigo, datos); }
        public Companero CompaneroObj { get { return companero; } }
        public void EnviarVotoViaje(byte[] datos) { red.Enviar(TipoMensaje.ViajeVoto, datos); }

        public void EnviarViaje(string destino)
        {
            var datos = Red.Armar(w => w.Write(destino));
            for (int i = 0; i < 3; i++) red.Enviar(TipoMensaje.Viajar, datos);   // UDP: por si se pierde uno
        }
        const int EscenaMenu = 3;
        public Tienda tienda;

        void Awake()
        {
            // (2026-11-19) En el bosque algo invisible llenaba las 42 voces reales con prioridad alta y Unity silenciaba los disparos, pasos y
            // cambios de arma (prioridad normal). Probado en vivo con la orden "voces": con 128 reales y 2048 virtuales vuelven a sonar.
            try
            {
                var cfgAudio = AudioSettings.GetConfiguration();
                if (cfgAudio.numRealVoices < 128 || cfgAudio.numVirtualVoices < 2048)
                {
                    cfgAudio.numRealVoices = 128; cfgAudio.numVirtualVoices = 2048;
                    Logger.LogInfo("Sonido: voces de Unity a 128 reales / 2048 virtuales (" + (AudioSettings.Reset(cfgAudio) ? "OK" : "FALLÓ") + ")");
                }
            }
            catch (Exception e) { Logger.LogWarning("Sonido: no pude cambiar las voces: " + e.Message); }
            if (GetComponent<VistaFovInicio>() == null) { gameObject.AddComponent<VistaFovInicio>(); gameObject.AddComponent<VistaFovFin>(); }
            cfgNombre = Config.Bind("General", "Nombre", Environment.UserName, "Tu nombre, lo ve el otro jugador.");
            cfgIp = Config.Bind("Red", "IP", "192.168.1.", "IP del anfitrión al que te unes.");
            cfgPuerto = Config.Bind("Red", "Puerto", 27777, "Puerto UDP (el anfitrión debe tenerlo abierto).");
            cfgTecla = Config.Bind("General", "TeclaPanel", KeyCode.F8, "Tecla para abrir el panel cooperativo.");
            cfgVidaJefes = Config.Bind("Dificultad", "VidaJefes", 2f, "Multiplicador de vida de los jefes cuando juegan 2 (1 = como el original). Debe ser igual en ambos jugadores.");
            cfgVelocidad = Config.Bind("Dificultad", "VelocidadEnemigos", 1.35f, "Multiplicador de velocidad de los enemigos (correr y atacar) cuando juegan 2.");
            cfgDanoMin = Config.Bind("Dificultad", "DanoEnemigoMin", 2f, "Daño mínimo de cada golpe enemigo cuando juegan 2 (0 = daño original).");
            cfgDanoMax = Config.Bind("Dificultad", "DanoEnemigoMax", 3f, "Daño máximo de cada golpe enemigo cuando juegan 2 (0 = daño original).");
            cfgJefeMin = Config.Bind("Dificultad", "DanoJefeMin", 10f, "Daño mínimo de cada golpe de un jefe cuando juegan 2.");
            cfgJefeMax = Config.Bind("Dificultad", "DanoJefeMax", 15f, "Daño máximo de cada golpe de un jefe cuando juegan 2.");
            cfgVidaNormales = Config.Bind("Dificultad", "VidaNormales", 1f, "Multiplicador de vida de los enemigos normales cuando juegan 2. Debe ser igual en ambos jugadores.");
            ipTexto = cfgIp.Value;
            puertoTexto = cfgPuerto.Value.ToString();
            nombreTexto = cfgNombre.Value;
            // Sin esto Unity se pausa al perder el foco y deja de enviar/recibir.
            Application.runInBackground = true;
            new HarmonyLib.Harmony(Guid).PatchAll(typeof(Plugin).Assembly);
            muerte = new Muerte(this);
            Viaje.plugin = this;
            mundo = new Mundo(this);
            menuSala = new MenuSala(this);
            mapa = new Mapa(this);
            municion = new Municion(this);
            perro = new Perro(this); limpieza = new Limpieza(this);
            aliados = new Aliados(this);
            despertar = new Despertar(this);
            eventos = new Eventos(this);
            CampanaSintesis.Preparar();   // la campana de la luna de sangre, lista de antemano
            Campana.BuscarPropio(this);   // o el sonido que haya puesto el jugador
            Sonidos.Preparar("tormenta_lluvia"); Sonidos.Preparar("tormenta_trueno");
            kit = new Kit(this);
            tiendaUI = new TiendaUI(this);
            pantalla = new Pantalla(this);
            Habilidades.plugin = this;
            Escopeta.plugin = this;
            Opciones.plugin = this;
            Camera.onPostRender += LibrosTienda.Silueta;
            Camera.onPreCull += muerte.Camara;
            Logger.LogInfo(Nombre + " " + Version + " cargado.");
        }

        void OnDestroy()
        {
            if (muerte != null) Camera.onPreCull -= muerte.Camara;
            Camera.onPostRender -= LibrosTienda.Silueta;
            Desconectar(true);
        }

        void OnApplicationFocus(bool foco)
        {
            Entrada.Enfocado = foco;
        }

        void OnApplicationQuit()
        {
            Desconectar(true);
        }

        // ---------------- Bucle ----------------

        float proximaRevisionArmas;

        void Update()
        {
            float ahora = Time.realtimeSinceStartup;
            if (ultimoFotograma > 0f && ahora - ultimoFotograma > 1f)
                Logger.LogWarning("El juego estuvo congelado " + (ahora - ultimoFotograma).ToString("0.0") + " s (escena " + EscenaActual() + ").");
            ultimoFotograma = ahora;


            if (jugador == null) jugador = FindObjectOfType<FPSPlayer>();

#if PRUEBAS
            // (2026-11-20) órdenes de prueba (BepInEx/coop-ordenes.txt) SOLO en la versión de pruebas: compilar con -p:Pruebas=true
            if (depuracion == null) depuracion = new Depuracion(this);
            depuracion.Actualizar();
#endif

#if PRUEBAS
            Vuelo.Procesar(this);   // F9: modo vuelo (atraviesa todo, sin daño): solo en pruebas (2026-11-20)
#endif
#if PRUEBAS
            if (Entrada.Enfocado && !Entrada.PanelAbierto && Input.GetKeyDown(KeyCode.F11)) { Oro.Dar(10000); Viaje.Avisar("+10000 de oro (F11, solo para pruebas)"); Log("F11: +10000 de oro"); }
#endif
            ProcesarPaquetes();
            VigilarAnfitrionAlMenu();
            if (conectado && Time.realtimeSinceStartup > proximoPing)
            {
                proximoPing = Time.realtimeSinceStartup + 1f;
                float ahoraPing = Time.realtimeSinceStartup;
                red.Enviar(TipoMensaje.PingPong, Red.Armar(w => { w.Write((byte)0); w.Write(ahoraPing); }));
            }
            if (!conectado) { pingMs = -1f; ultimoPong = 0f; }
            Controles.Iniciar(this); Controles.Actualizar();
            if (conectado) FuegoAmigo.Actualizar(this);
            if (conectado) ArmaCompanero.Actualizar(this);   // (2026-11-20) qué arma tengo en la mano
            if (conectado) SonidosCompanero.Actualizar(this);   // (2026-11-20) disparos, recarga, cambio de arma y pasos del compañero
#if PRUEBAS
            if (Controles.Capturando == null && Controles.Pulsada("panel")) AlternarPanel();   // F8 (panel de conexión) solo en pruebas (2026-11-20)
#endif
            Viaje.Actualizar();

            // Esto corre SIEMPRE, también con la red apagada (jugando solo): el modo de guardado,
            // la dificultad (se apaga sin compañero), la muerte y la restauración del terreno.
            if (muerte == null) muerte = new Muerte(this);
            // Red de seguridad: la aldea prohíbe las armas y a veces el aviso "No debería sacar las armas aquí"
            // se queda puesto al salir (viaje conjunto, votación, cabañas). Fuera de la aldea/menú, vivo y sin
            // conversación, se vuelven a permitir.
            if (ahora > proximaRevisionArmas)
            {
                proximaRevisionArmas = ahora + 1f;
                var esc = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                if (muerte.Mio == Muerte.Estado.Vivo && esc.buildIndex != Muerte.EscenaAldea && esc.buildIndex != EscenaMenu
                    && esc.name.IndexOf("Bellville", StringComparison.OrdinalIgnoreCase) < 0 && Time.timeSinceLevelLoad > 3f)
                {
                    var pwx = FindObjectOfType<PlayerWeapons>();
                    if (pwx != null && pwx.NoWeaponsAllowed) { pwx.NoWeaponsAllowed = false; Log("Armas permitidas de nuevo fuera de la aldea (" + esc.name + ")."); }
                }
            }
            Dificultad.Activa = conectado;
            float fd = Opciones.FactorDificultad;   // Normal ×1, Difícil ×1,5, Pesadilla ×2 (lo elige el anfitrión)
            Dificultad.VidaJefes = cfgVidaJefes.Value * fd;
            Dificultad.VidaNormales = cfgVidaNormales.Value * fd;
            Dificultad.Velocidad = cfgVelocidad.Value;
            DanoAlJugador.Min = cfgDanoMin.Value * fd; DanoAlJugador.Max = cfgDanoMax.Value * fd;
            DanoAlJugador.JefeMin = cfgJefeMin.Value * fd; DanoAlJugador.JefeMax = cfgJefeMax.Value * fd;
            if (Opciones.Dificultad >= 2) { DanoAlJugador.Min = 6f; DanoAlJugador.Max = 10f; DanoAlJugador.JefeMin = 30f; DanoAlJugador.JefeMax = 50f; }   // Pesadilla: valores fijos (pedido 2026-11-11)
            { int dd = Opciones.Dificultad; if (dd != difLogueada) { difLogueada = dd; Log("Dificultad aplicada: " + Opciones.NombresDificultad[Mathf.Clamp(dd, 0, 2)] + " → golpe " + DanoAlJugador.Min + "-" + DanoAlJugador.Max + ", jefes " + DanoAlJugador.JefeMin + "-" + DanoAlJugador.JefeMax + ", vida jefes ×" + Dificultad.VidaJefes + ", vida normales ×" + Dificultad.VidaNormales + ", botín ÷" + Mundo.DivisorBotin + ", potenciados " + (Potenciado.ProbabilidadActual * 100).ToString("0.0") + " %"); } }
            // El modo de guardado solo cambia en el menú principal: así nunca se mezclan
            // la partida de solitario y la cooperativa a mitad de una sesión.
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex == EscenaMenu && Guardado.ModoCoop != red.Activo)
            {
                Guardado.ModoCoop = red.Activo;
                Log("Partida: " + (red.Activo ? "COOPERATIVA (" + Guardado.Coop + ")" : "solitario (" + Guardado.Solo + ")"));
                // El menú lee el guardado al abrirse y lo deja en memoria: se recarga para que
                // "Continuar" use la partida del modo elegido.
                UnityEngine.SceneManagement.SceneManager.LoadScene(EscenaMenu);
            }
            Rendimiento.Cuadro();
            Rendimiento.Medir("muerte", muerte.Actualizar);
            Rendimiento.Medir("mundo", mundo.Actualizar);
            Rendimiento.Medir("menuSala", menuSala.Actualizar);
            Rendimiento.Medir("mapa", () => mapa.Actualizar(conectado));
            Rendimiento.Medir("municion", municion.Actualizar);
            Rendimiento.Medir("perro", () => perro.Actualizar(conectado));
            Rendimiento.Medir("limpieza", limpieza.Actualizar);
            Rendimiento.Medir("aliados", () => aliados.Actualizar(conectado));
            if (conectado) Rendimiento.Medir("despertar", () => despertar.Actualizar());
            Rendimiento.Medir("eventos", () => eventos.Actualizar(conectado));
            Rendimiento.Medir("elementales", () => Elemental.Actualizar(this));
            Rendimiento.Medir("kit", kit.Actualizar);
            Rendimiento.Medir("tiendaUI", tiendaUI.Actualizar);
            Rendimiento.Medir("destreza", VentanaDestreza.Actualizar);
            Rendimiento.Medir("pantalla", pantalla.Actualizar);
            Rendimiento.Medir("habilidades", Habilidades.Actualizar);
            Rendimiento.Medir("campana", Campana.ActualizarFondo);
            Rendimiento.Medir("escopeta", Escopeta.Actualizar);
            Rendimiento.Medir("ballesta", Ballesta.Actualizar);
            Rendimiento.Medir("libros", () => LibrosTienda.Actualizar(jugador));

            // (2026-11-20) ANTES del corte por red inactiva: quien recibe la invitación todavía no tiene sala (antes nunca la procesaba)
            try { RevisarInvitacionSteam(); } catch (Exception e) { Logger.LogWarning("Steam: " + e.Message); }
            if (!red.Activo) return;
            float t = Time.realtimeSinceStartup;
            // si ESTE juego estuvo congelado (carga, ventana en segundo plano…), ese tiempo no cuenta como silencio del compañero:
            // sus mensajes siguen esperando en la red sin leer. Antes, tras 38 s congelado, se daba al otro por desconectado.
            if (ultimoFotogramaRed > 0f && t - ultimoFotogramaRed > 2f) ultimoRecibido += t - ultimoFotogramaRed;
            ultimoFotogramaRed = t;

            if (!conectado && !red.EsAnfitrion && t - ultimoHola > IntervaloHola)
            {
                ultimoHola = t;
                EnviarHola();
            }

            if (conectado && t - ultimoEstado > IntervaloEstado)
            {
                ultimoEstado = t;
                EnviarEstado();
            }

            if (conectado && t - ultimoRecibido > TiempoLimite)
            {
                aviso = nombreCompanero + " dejó de responder.";
                Logger.LogWarning(aviso);
                if (!red.EsAnfitrion) ExpulsarAlMenu(nombreCompanero + " dejó de responder: se cerró la sala.");   // el anfitrión desapareció
                else PerderCompanero();
            }

            if (companero != null) Rendimiento.Medir("compañero", () => companero.Actualizar(Time.deltaTime, EscenaActual(), Ancla()));

            if (combate == null) combate = new Combate(this, red);
            if (oro == null) oro = new Oro(red);
            if (tienda == null) tienda = new Tienda(this);
            tienda.Actualizar(jugador);
            Vector3? propia = jugador != null ? (Vector3?)Pies(jugador) : null;
            Vector3? otro = companero != null && companero.Visible ? (Vector3?)companero.PosicionMundo : null;
            Rendimiento.Medir("oro", () => oro.Actualizar(conectado, EscenaActual(), jugador != null));
            Rendimiento.Medir("combate", () => combate.Actualizar(conectado, EscenaActual(), propia, otro, Ancla()));
        }

        void LateUpdate()
        {
            Pantalla.AplicarFps();   // al final del cuadro, después de que el juego ponga el suyo
            Opciones.Aplicar();
            if (panelAbierto) LiberarCursor();
            if (Seguir) MirarCompanero(false);
            if (companero != null) companero.AplicarMirada();
            Rendimiento.Medir("arma compañero", () => ArmaCompanero.Colocar(this));   // después de la mirada: la mano ya está en su sitio
            if (combate != null && conectado) Rendimiento.Medir("combate(tarde)", () => combate.LateUpdate(Ancla(), Time.deltaTime));
        }

        // ---------------- Red ----------------

        void ProcesarPaquetes()
        {
            Paquete p;
            while (red.Sacar(out p))
            {
                try { Procesar(p); }
                catch (Exception e) { Logger.LogError("Paquete " + p.Tipo + ": " + e); }
            }
        }

        void Procesar(Paquete p)
        {
            // El anfitrión acepta a un solo compañero (máximo 2 jugadores).
            if (red.EsAnfitrion)
            {
                if (red.Companero == null && p.Tipo == TipoMensaje.Hola)
                {
                    red.Companero = p.Origen;
                }
                else if (red.Companero == null || !red.Companero.Equals(p.Origen))
                {
                    if (p.Tipo == TipoMensaje.Hola) red.Enviar(TipoMensaje.Lleno, null, p.Origen);
                    return;
                }
            }
            else if (!p.Origen.Equals(red.Companero) && !p.Origen.Address.Equals(red.Companero.Address))
            {
                return;
            }

            ultimoRecibido = Time.realtimeSinceStartup;

            switch (p.Tipo)
            {
                case TipoMensaje.Hola:
                {
                    string nombre = Red.Lector(p.Datos).ReadString();
                    if (!conectado)
                    {
                        conectado = true;
                        nombreCompanero = nombre;
                        aviso = "Conectado con " + nombre + ".";
                        Logger.LogInfo(aviso + " (" + p.Origen + ")");
                        if (red.EsAnfitrion) EnviarHola();
                    }
                    else if (nombre != nombreCompanero)
                    {
                        nombreCompanero = nombre;   // el compañero cambió su nombre con la partida en marcha
                        if (companero != null) companero.Nombre = nombre;
                        Log("El compañero ahora se llama " + nombre);
                    }
                    break;
                }
                case TipoMensaje.Estado:
                {
                    if (!conectado) break;
                    if (companero == null) companero = new Companero();
                    companero.Nombre = nombreCompanero;
                    companero.Recibir(Red.Lector(p.Datos));
                    muerte.DelOtro = companero.EstadoVida;
                    break;
                }
                case TipoMensaje.Catalogo:
                    if (conectado && combate != null && !red.EsAnfitrion) combate.RecibirCatalogo(Red.Lector(p.Datos), EscenaActual(), Ancla());
                    break;
                case TipoMensaje.Monstruos:
                    if (conectado && combate != null) combate.RecibirEstados(Red.Lector(p.Datos));
                    break;
                case TipoMensaje.Mundo:
                    if (conectado) mundo.RecibirSemilla(Red.Lector(p.Datos));
                    break;
                case TipoMensaje.Recogido:
                    if (conectado) mundo.RecibirRecogido(Red.Lector(p.Datos));
                    break;
                case TipoMensaje.PingPong:
                {
                    if (!conectado) break;
                    var rd = Red.Lector(p.Datos); byte clase = rd.ReadByte(); float t0 = rd.ReadSingle();
                    if (clase == 0) { var resp = Red.Armar(w => { w.Write((byte)1); w.Write(t0); }); red.Enviar(TipoMensaje.PingPong, resp); }
                    else
                    {
                        float ms = (Time.realtimeSinceStartup - t0) * 1000f;
                        if (ms >= 0f && ms < 10000f) { pingMs = pingMs < 0f ? ms : Mathf.Lerp(pingMs, ms, 0.35f); ultimoPong = Time.realtimeSinceStartup; }
                    }
                    break;
                }
                case TipoMensaje.FuegoAmigo:
                    if (conectado) FuegoAmigo.Recibir(this, Red.Lector(p.Datos));
                    break;
                case TipoMensaje.ViajeVoto:
                    if (conectado) Viaje.RecibirVoto(Red.Lector(p.Datos));
                    break;
                case TipoMensaje.Viajar:
                    if (conectado) Viaje.Recibir(Red.Lector(p.Datos).ReadString());
                    break;
                case TipoMensaje.Oro:
                    if (conectado && oro != null) oro.Recibir(Red.Lector(p.Datos), this);
                    break;
                case TipoMensaje.Dano:
                    if (conectado && combate != null) combate.RecibirDano(Red.Lector(p.Datos));
                    break;
                case TipoMensaje.MuerteInvitado:
                    if (conectado && combate != null) combate.RecibirMuerteInvitado(Red.Lector(p.Datos));
                    break;
                case TipoMensaje.Perro:
                    if (conectado) perro.Recibir(Red.Lector(p.Datos));
                    break;
                case TipoMensaje.LibroTienda:
                    if (conectado) LibrosTienda.Recibir(this, Red.Lector(p.Datos));
                    break;
                case TipoMensaje.Mapa:
                    if (conectado) mapa.Recibir(Red.Lector(p.Datos));
                    break;
                case TipoMensaje.Cofre:
                    if (conectado) mundo.RecibirCofre(Red.Lector(p.Datos));
                    break;
                case TipoMensaje.Escopeta:
                    if (conectado) mundo.RecibirEscopeta(Red.Lector(p.Datos));
                    break;
                case TipoMensaje.Tesoro:
                    if (conectado) mundo.RecibirTesoro(Red.Lector(p.Datos));
                    break;
                case TipoMensaje.JefeCueva:
                    if (conectado) mundo.RecibirJefeCueva(Red.Lector(p.Datos));
                    break;
                case TipoMensaje.ArmaCompanero:
                    if (conectado) ArmaCompanero.Recibir(Red.Lector(p.Datos));
                    break;
                case TipoMensaje.SonidoCompanero:
                    if (conectado) SonidosCompanero.Recibir(this, Red.Lector(p.Datos));
                    break;
                case TipoMensaje.Ballesta:
                    if (conectado) mundo.RecibirBallesta(Red.Lector(p.Datos));
                    break;
                case TipoMensaje.Ajustes:
                    if (conectado) Opciones.Recibir(p.Datos);
                    break;
                case TipoMensaje.Cabana:
                    if (conectado) mundo.RecibirCabana(Red.Lector(p.Datos));
                    break;
                case TipoMensaje.Evento:
                    if (conectado) eventos.Recibir(Red.Lector(p.Datos));
                    break;
                case TipoMensaje.Levantar:
                    if (conectado) muerte.Levantado(p.Datos != null && p.Datos.Length > 0 && p.Datos[0] == 1);
                    break;
                case TipoMensaje.AliadoDano:
                    if (conectado) aliados.RecibirDano(Red.Lector(p.Datos));
                    break;
                case TipoMensaje.Aliado:
                    if (conectado) aliados.Recibir(Red.Lector(p.Datos));
                    break;
                case TipoMensaje.Iniciar:
                    if (conectado && !red.EsAnfitrion) menuSala.RecibirIniciar(p.Datos != null && p.Datos.Length > 0 ? p.Datos[0] : (byte)0, p.Datos != null && p.Datos.Length > 1 && p.Datos[1] == 1);
                    break;
                case TipoMensaje.Adios:
                    aviso = nombreCompanero + " se desconectó.";
                    Logger.LogInfo(aviso);
                    if (!red.EsAnfitrion) ExpulsarAlMenu(nombreCompanero + " cerró la sala.");   // el anfitrión se fue: el invitado sale también
                    else PerderCompanero();
                    break;
                case TipoMensaje.Lleno:
                    aviso = "La partida ya tiene 2 jugadores.";
                    Logger.LogWarning(aviso);
                    Desconectar(false);
                    break;
            }
        }

        void EnviarHola()
        {
            string n = cfgNombre.Value;
            red.Enviar(TipoMensaje.Hola, Red.Armar(w => w.Write(n)));
        }

        void EnviarEstado()
        {
            // Se envía siempre (también en menús o cargando) para que la conexión no caduque;
            // sin jugador se manda escena -1 y el otro simplemente no dibuja nada.
            int escena = -1;
            Vector3 pos = Vector3.zero;
            float yaw = 0f, pitch = 0f;
            if (jugador != null)
            {
                escena = EscenaActual();
                pos = Pies(jugador) - Ancla();
                Transform cam = Camera.main != null ? Camera.main.transform : jugador.transform;
                yaw = cam.eulerAngles.y;
                pitch = cam.eulerAngles.x;
            }
            red.Enviar(TipoMensaje.Estado, Red.Armar(w =>
            {
                w.Write(escena);
                w.Write(pos.x); w.Write(pos.y); w.Write(pos.z);
                w.Write(yaw); w.Write(pitch);
                w.Write((byte)muerte.Mio);
            }));
        }

        void PerderCompanero()
        {
            conectado = false;
            nombreCompanero = "";
            if (companero != null) { companero.Destruir(); companero = null; }
            if (red.EsAnfitrion) red.Companero = null;   // queda esperando a otro
        }

        void Anfitrion()
        {
            try
            {
                Guardar();
                red.Anfitrion(cfgPuerto.Value);
                aviso = "Esperando compañero en el puerto " + cfgPuerto.Value + "...";
                Logger.LogInfo(aviso);
            }
            catch (Exception e) { aviso = "No se pudo abrir el puerto: " + e.Message; Logger.LogError(e); }
        }

        void Unirse()
        {
            try
            {
                Guardar();
                red.Unirse(cfgIp.Value, cfgPuerto.Value);
                ultimoHola = 0;
                aviso = "Buscando a " + cfgIp.Value + ":" + cfgPuerto.Value + "...";
                Logger.LogInfo(aviso);
            }
            catch (Exception e) { aviso = "No se pudo conectar: " + e.Message; Logger.LogError(e); }
        }

        /// El anfitrión se fue: el invitado deja la sala y vuelve al menú principal (nunca se queda solo en una partida ajena).
        void ExpulsarAlMenu(string motivo)
        {
            Logger.LogInfo("Sala cerrada por el anfitrión: " + motivo);
            PerderCompanero();
            red.Cerrar();
            aviso = motivo;
            Viaje.Cancelar();
            if (EscenaActual() != EscenaMenu) UnityEngine.SceneManagement.SceneManager.LoadScene(EscenaMenu);
        }

        // ---------------- Calidad de la conexión ----------------

        float pingMs = -1f, ultimoPong, proximoPing;
        public float PingFalsoMs = -1f;   // prueba: fija el retardo mostrado

        /// Retardo de ida y vuelta (ms); si hace más de 3 s que no vuelve ninguna respuesta, cuenta como pésimo.
        public float RetardoMs
        {
            get
            {
                if (PingFalsoMs >= 0f) return PingFalsoMs;
                if (!conectado) return -1f;
                if (pingMs < 0f || Time.realtimeSinceStartup - ultimoPong > 3f) return Time.realtimeSinceStartup - ultimoRecibido > 3f ? 999f : (pingMs < 0f ? 0f : pingMs);
                return pingMs;
            }
        }

        static Texture2D punto;

        /// Icono de señal de 3 barras abajo a la izquierda (no sale con menos de 30 ms): verde (30–100 ms), amarillo (101–200) y rojo (más de 200).
        void DibujarSenal()
        {
            float ms = RetardoMs;
            if (ms < 0f || ms < 30f) return;   // con menos de 30 ms la conexión es perfecta: no se muestra el icono
            if (punto == null) { punto = new Texture2D(1, 1); punto.SetPixel(0, 0, Color.white); punto.Apply(); }
            int activas = ms <= 100f ? 3 : (ms <= 200f ? 2 : 1);
            Color col = activas == 3 ? new Color(0.30f, 0.85f, 0.40f) : (activas == 2 ? new Color(0.96f, 0.80f, 0.20f) : new Color(0.90f, 0.25f, 0.20f));
            Color gris = new Color(0.42f, 0.42f, 0.44f, 0.85f);
            float x0 = 14f, base0 = Screen.height - 12f, ancho = 6f, hueco = 3f;
            float[] altos = { 8f, 14f, 20f };
            var prev = GUI.color;
            for (int i = 0; i < 3; i++)
            {
                GUI.color = i < activas ? col : gris;
                GUI.DrawTexture(new Rect(x0 + i * (ancho + hueco), base0 - altos[i], ancho, altos[i]), punto);
            }
            GUI.color = prev;
        }

        int escenaPrev = -1;

        /// El anfitrión pulsó "Menú principal" (pasó de una partida al menú): se cierra la sala y el invitado sale solo.
        void VigilarAnfitrionAlMenu()
        {
            int ea = EscenaActual();
            if (escenaPrev != Mundo.EscenaBosque && ea == Mundo.EscenaBosque && escenaPrev > 0) StartCoroutine(Viaje.LlegadaCabana());   // viaje a la cabaña del lago
            if (conectado && escenaPrev == Mundo.EscenaBosque && ea == 7) { try { Viaje.LlegueAlPuebloDesdeBosque(); } catch (System.Exception e) { Logger.LogWarning("Viaje: " + e.Message); } }
            if (red.Activo && red.EsAnfitrion && ea == EscenaMenu && escenaPrev > 0 && escenaPrev != EscenaMenu)
            {
                Logger.LogInfo("El anfitrión volvió al menú: se cierra la sala.");
                Desconectar(true);
                aviso = "Sala cerrada.";
            }
            // el invitado volvió al menú desde la partida (Menú principal → Sí): deja la sala; el anfitrión sigue jugando
            if (red.Activo && !red.EsAnfitrion && ea == EscenaMenu && escenaPrev > 0 && escenaPrev != EscenaMenu)
            {
                Logger.LogInfo("El invitado volvió al menú: sale de la sala.");
                Desconectar(true);
                aviso = "Saliste de la sala.";
            }
            escenaPrev = ea;
        }

        void Desconectar(bool avisar)
        {
            if (avisar && conectado) for (int i = 0; i < 3; i++) red.Enviar(TipoMensaje.Adios, null);   // UDP: por si se pierde uno
            PerderCompanero();
            red.Cerrar();
        }

        /// Crea la sala de Steam (solo amigos, 2 jugadores) y abre la ventana para invitar.
        public void OrdenAnfitrionSteam()
        {
            Guardar(); UsarNombreSteam();
            red.alCrearSala = () => { aviso = "Sala de Steam creada: invita a tu amigo (botón «Invitar amigo» o Shift+Tab)."; Logger.LogInfo("Steam: sala creada " + red.Sala.m_SteamID); };
            red.AnfitrionSteam();
            aviso = "Creando la sala de Steam...";
            Logger.LogInfo("Steam: creando sala de amigos");
        }

        public void InvitarSteam() { red.Invitar(); }

        /// Si el jugador nunca cambió su nombre (el del sistema), usa el de Steam.
        void UsarNombreSteam()
        {
            try { if (cfgNombre.Value == Environment.UserName || cfgNombre.Value == "Cazador") { var n = Steamworks.SteamFriends.GetPersonaName(); if (!string.IsNullOrEmpty(n)) { cfgNombre.Value = n; nombreTexto = n; } } } catch { }
        }

        /// Invitación de Steam aceptada (o el juego se abrió desde una): entrar a esa sala desde el menú principal.
        void RevisarInvitacionSteam()
        {
            Red.IniciarSteam();
            if (Red.InvitacionPendiente.m_SteamID == 0) return;
            if (EscenaActual() != EscenaMenu)
            {
                if (!avisoInvitacion) { avisoInvitacion = true; Viaje.Avisar("Te invitaron a una sala de Steam: vuelve al menú principal para entrar.", 8f); }
                return;
            }
            var sala = Red.InvitacionPendiente; Red.InvitacionPendiente = Steamworks.CSteamID.Nil; avisoInvitacion = false;
            if (red.Activo) { Desconectar(true); }
            Guardar(); UsarNombreSteam();
            red.alEntrarSala = d => { ultimoHola = 0; aviso = "Entraste a la sala de " + red.NombreSteam(red.Companero) + ": conectando..."; Logger.LogInfo("Steam: dentro de la sala; anfitrión " + d.m_SteamID); };
            red.UnirseSala(sala);
            aviso = "Entrando a la sala de Steam...";
            Logger.LogInfo("Steam: aceptada la invitación a la sala " + sala.m_SteamID);
        }

        void Guardar()
        {
            cfgNombre.Value = string.IsNullOrEmpty(nombreTexto.Trim()) ? "Cazador" : nombreTexto.Trim();
            cfgIp.Value = ipTexto.Trim();
            int puerto;
            if (int.TryParse(puertoTexto, out puerto) && puerto > 0 && puerto < 65536) cfgPuerto.Value = puerto;
            puertoTexto = cfgPuerto.Value.ToString();
            Config.Save();
        }

        // ---------------- Órdenes de prueba (Depuracion) ----------------

        public void Log(string texto) { Logger.LogInfo(texto); }

        public bool Seguir;   // prueba: la cámara apunta al compañero en cada fotograma

        public void OrdenAnfitrion() { Anfitrion(); }

        public void OrdenUnirse(string ip)
        {
            if (ip.Length > 0) ipTexto = ip;
            Unirse();
        }

        public void OrdenNombre(string n)
        {
            nombreTexto = n;
            Guardar();
        }

        public void OrdenDesconectar() { Desconectar(true); }

        /// Texto bajo el menú principal: en qué está la sala.
        public string TextoEstadoSala()
        {
            if (!red.Activo) return aviso.StartsWith("Sala cerrada") || aviso.StartsWith("Saliste") || aviso.StartsWith("No se pudo") || aviso.StartsWith("La partida ya") ? aviso : "";
            if (conectado) return menuSala.TextoConectado(nombreCompanero);
            if (!string.IsNullOrEmpty(red.UltimoError)) return "Error de red: " + red.UltimoError;
            if (red.ModoSteam)   // (2026-11-20) sala de Steam
                return red.EsAnfitrion ? (red.SalaLista ? "Sala de Steam abierta – invita a tu amigo (botón «Invitar amigo»)..." : "Creando la sala de Steam...")
                                       : "Entrando a la sala de Steam de tu amigo...";
            return red.EsAnfitrion
                ? "Sala abierta en el puerto " + cfgPuerto.Value + " – esperando compañero..."
                : "Buscando la sala de " + cfgIp.Value + ":" + cfgPuerto.Value + "...";
        }

        public string Estado()
        {
            string s = "escena=" + EscenaActual() + " conectado=" + conectado + " compañero='" + nombreCompanero + "'";
            if (jugador != null) s += " pies=" + Pies(jugador) + " rel=" + (Pies(jugador) - Ancla());
            if (companero != null) s += " | " + companero.Describir();
            if (combate != null) s += " | " + combate.Describir();
            return s;
        }

        /// Gira la cámara (SmoothMouseLook) para mirar al compañero.
        public void OrdenMirar() { MirarCompanero(true); }

        /// Prueba: inclina la cámara propia (grados, positivo = arriba).
        public void OrdenInclinar(float grados)
        {
            var ml = FindObjectOfType<SmoothMouseLook>();
            if (ml == null) { Log("  sin cámara"); return; }
            ml.rotationY = grados;
            Log("  cámara inclinada " + grados + "°");
        }

        void MirarCompanero(bool avisar)
        {
            var ml = FindObjectOfType<SmoothMouseLook>();
            if (ml == null || companero == null || !companero.Visible || Camera.main == null) { if (avisar) Log("  no se puede mirar"); return; }
            Vector3 d = companero.PosicionMundo + Vector3.up * 1.2f - Camera.main.transform.position;
            float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            float pitch = Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;
            ml.rotationX = Mathf.DeltaAngle(ml.originalRotation.eulerAngles.y, yaw);
            ml.rotationY = pitch;
            if (avisar) Log("  mirando hacia yaw=" + Mathf.RoundToInt(yaw) + " a " + Mathf.RoundToInt(d.magnitude) + " m");
        }

        public void OrdenJunto()
        {
            if (jugador == null || companero == null || !companero.Visible) { Log("  no hay compañero visible"); return; }
            // En el mismo punto que el compañero (suelo libre seguro); la figura no tiene colisión.
            Vector3 destino = companero.PosicionMundo + Vector3.up * 0.3f;
            Vector3 desplazamiento = destino - Pies(jugador);
            jugador.transform.position += desplazamiento;
            var rb = jugador.GetComponent<Rigidbody>();
            if (rb != null) { rb.position = jugador.transform.position; rb.velocity = Vector3.zero; }
            Log("  teletransportado a " + destino);
        }

        // ---------------- Coordenadas ----------------

        /// Punto del suelo bajo el jugador: parte baja de su cápsula de colisión.
        static Vector3 Pies(FPSPlayer j)
        {
            var col = j.GetComponent<CapsuleCollider>();
            if (col != null && col.enabled)
            {
                Bounds b = col.bounds;
                return new Vector3(b.center.x, b.min.y, b.center.z);
            }
            return j.transform.position;
        }

        static int EscenaActual()
        {
            return SceneManager.GetActiveScene().buildIndex;
        }

        /// El juego recentra el mundo al alejarse 700 m (WorldRecenter); el terreno se
        /// mueve con todo lo demás, así que las posiciones se miden respecto a él.
        static Vector3 Ancla()
        {
            return Anclaje.Origen();
        }

        // ---------------- Panel ----------------

        void AlternarPanel()
        {
            panelAbierto = !panelAbierto;
            Entrada.PanelAbierto = panelAbierto;
            if (panelAbierto)
            {
                var ml = FindObjectOfType<SmoothMouseLook>();
                if (ml != null && ml.enabled) { ml.enabled = false; mouseLookDesactivado = ml; }

                // PlayMakerGUI vuelve a bloquear el cursor en cada repintado (controlMouseCursor),
                // y CruxHideMouse lo encierra en cada Update: se apagan mientras el panel está abierto.
                guisPlayMaker.Clear();
                foreach (var g in FindObjectsOfType<PlayMakerGUI>())
                {
                    if (g.controlMouseCursor) { g.controlMouseCursor = false; guisPlayMaker.Add(g); }
                }
                ocultadores.Clear();
                foreach (var c in FindObjectsOfType<CruxHideMouse>())
                {
                    if (c.enabled) { c.enabled = false; ocultadores.Add(c); }
                }
                LiberarCursor();
                StartCoroutine(MantenerCursorLibre());
            }
            else
            {
                if (mouseLookDesactivado != null) mouseLookDesactivado.enabled = true;
                mouseLookDesactivado = null;
                foreach (var g in guisPlayMaker) if (g != null) g.controlMouseCursor = true;
                guisPlayMaker.Clear();
                foreach (var c in ocultadores) if (c != null) c.enabled = true;
                ocultadores.Clear();
                if (jugador != null)
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
            }
        }

        readonly System.Collections.Generic.List<PlayMakerGUI> guisPlayMaker = new System.Collections.Generic.List<PlayMakerGUI>();
        readonly System.Collections.Generic.List<CruxHideMouse> ocultadores = new System.Collections.Generic.List<CruxHideMouse>();

        static void LiberarCursor()
        {
            if (Cursor.lockState != CursorLockMode.None) Cursor.lockState = CursorLockMode.None;
            if (!Cursor.visible) Cursor.visible = true;
        }

        /// Por si algún otro script lo vuelve a bloquear: se libera al final de cada fotograma.
        System.Collections.IEnumerator MantenerCursorLibre()
        {
            var fin = new WaitForEndOfFrame();
            while (panelAbierto)
            {
                yield return fin;
                if (panelAbierto) LiberarCursor();
            }
        }

        void OnGUI() { Rendimiento.Medir("interfaz", Interfaz); }

        void Interfaz()
        {
            if (Depuracion.ModoFoto) return;   // (2026-11-20) fotos para publicar: sin interfaz del mod
            // IMGUI llama a OnGUI varias veces por cuadro (Layout, Repaint y un evento por tecla/ratón). Lo que solo se dibuja (marcadores, avisos,
            // textos) se pinta una sola vez, en Repaint: en los otros pasos gastaba tiempo del hilo principal sin mostrar nada.
            bool pintar = Event.current != null && Event.current.type == EventType.Repaint;
            if (pintar)
            {
                // Solo con un jugador en el mapa (no en menús ni pantallas de carga).
                if (companero != null && jugador != null && Camera.main != null && Opciones.NombreCompanero && (tienda == null || !tienda.Abierta)) companero.DibujarNombre(Camera.main);   // con la tienda abierta no se dibuja encima (2026-11-19)
                Opciones.DibujarBrillo();
                Opciones.DibujarFps();
                // Con la tienda abierta no: la tienda ya muestra las monedas y el marcador taparía su aviso.
                if (jugador != null && Camera.main != null && (tienda == null || !tienda.Abierta)) Oro.Dibujar();
                if (jugador != null) muerte.Dibujar();
                if (jugador != null) { Viaje.Dibujar(); Viaje.DibujarVoto(); }
                if (jugador != null) { mundo.DibujarCofre(); mundo.DibujarCasa(); mundo.DibujarMina(); mundo.DibujarCabana(); mundo.DibujarTesoro(); }
                if (jugador != null && conectado) mapa.Dibujar();
                if (jugador != null) { municion.Dibujar(); aliados.Dibujar(); eventos.Dibujar(); Elemental.Dibujar(); }
            }

            mundo.DibujarCarga();
            menuSala.Dibujar();
            pantalla.Dibujar();

            if (panelAbierto)
            {
                ventana = GUILayout.Window(0x57484350, ventana, DibujarPanel, Nombre + " " + Version);
            }
            else if (red.Activo && EscenaActual() != EscenaMenu)   // en el menú lo muestra MenuSala
            {
                if (conectado) DibujarSenal();   // icono de 3 barras con la calidad de la conexión (sin texto)
            }
        }

        void DibujarPanel(int id)
        {
            GUILayout.Label("Tu nombre:");
            nombreTexto = GUILayout.TextField(nombreTexto, 24);
            GUILayout.Label("IP del anfitrión (solo para unirse):");
            ipTexto = GUILayout.TextField(ipTexto, 64);
            GUILayout.Label("Puerto:");
            puertoTexto = GUILayout.TextField(puertoTexto, 5);

            GUILayout.Space(6);
            if (!red.Activo)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Ser anfitrión")) Anfitrion();
                if (GUILayout.Button("Unirse")) Unirse();
                GUILayout.EndHorizontal();
            }
            else if (GUILayout.Button("Desconectar"))
            {
                Desconectar(true);
                aviso = "Desconectado.";
            }

            if (GUILayout.Button(Vuelo.Activo ? "Volar: ACTIVADO (" + Vuelo.Tecla + ")" : "Volar (admin, " + Vuelo.Tecla + ")")) Vuelo.Alternar(jugador);
            if (!string.IsNullOrEmpty(aviso)) GUILayout.Label(aviso);
            if (!string.IsNullOrEmpty(red.UltimoError)) GUILayout.Label("Error de red: " + red.UltimoError);
            GUILayout.Label("Cerrar panel: " + cfgTecla.Value);
            GUI.DragWindow();
        }
    }
}

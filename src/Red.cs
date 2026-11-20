// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Steamworks;

namespace WitchHuntCoop
{
    /// Tipos de mensaje. Cada paquete: 'W' 'H' versión tipo + datos.
    public enum TipoMensaje : byte
    {
        Hola = 1,     // nombre del jugador (también sirve de respuesta)
        Estado = 2,   // escena, posición y orientación
        Adios = 3,    // desconexión voluntaria
        Lleno = 4,    // el anfitrión ya tiene compañero
        Catalogo = 5, // anfitrión → invitado: monstruos numerados y su dueño
        Monstruos = 6,// posición/animación de los monstruos que simula quien lo envía
        Dano = 7,     // daño a un monstruo
        Oro = 8,      // oro compartido: aporte acumulado y bolsa actual
        Viajar = 9,   // el compañero partió a otro nivel: viajar con él
        Mundo = 10,   // anfitrión → invitado: semilla del bosque de esta cacería
        Recogido = 11,// alguien recogió un objeto repartido: quitarlo en el otro juego
        Iniciar = 12, // anfitrión → invitado: los dos están en el menú, empezar la partida
        Mapa = 14,    // mapa encontrado (0) o piedra de guardado vista (1, id)
        Cofre = 13,
        Perro = 16,   // perro: liberado (0) o posición del compañero que mueve el anfitrión (1)
        Escopeta = 17,// alguien recogió la escopeta de una casa (número de casa)
        PingPong = 27, // medición del retardo: pregunta (0) y respuesta (1) con la hora del que preguntó
        FuegoAmigo = 26, // daño de un disparo que el compañero te hizo (fuego amigo activado en la partida)
        ViajeVoto = 25, // votación para viajar: pedir (0), sí (1), no (2), cancelar (3); cartel de la cabaña descubierto (4)
        Ajustes = 24, // anfitrión → invitado: dificultad y frecuencia de eventos elegidas en las opciones
        AliadoDano = 23, // un monstruo de mi juego golpeó a un aliado del compañero (hueco, daño)
        Cabana = 22,  // alguien abrió una cabaña cerrada (número)
        Evento = 21,  // evento del bosque: tipo (0 = terminó) y segundos que quedan
        Levantar = 20,// el compañero terminó de levantarte (mantuvo E 6 s)
        Aliado = 19,  // un aliado de la bola de cristal: hueco, vivo, tipo, posición, giro, animación
        SonidoCompanero = 31, // (2026-11-20) disparo (0), recarga (1) o cambio de arma (2) del compañero + número de arma: el otro lo hace sonar
        JefeCueva = 30, // jefe de la cueva: estado del anfitrión (0: pos, giro, fase, vida) o daño del invitado (1: daño)
        MuerteInvitado = 29, // el invitado mató su copia de un monstruo del anfitrión (id): el anfitrión mata el suyo
        Tesoro = 28,  // castillo: el jefe de la mina soltó la llave (0, pos), llave tomada (1), bóveda abierta (2), montón de oro tomado (3, bóveda?, número)
        Ballesta = 18,// alguien recogió la ballesta de una mina (número de mina)
        LibroTienda = 15, // alguien compró un libro del cooperativo (nombre de la variable)   // alguien abrió un cofre del mod (semilla de la cacería + número)
    }

    public class Paquete
    {
        public TipoMensaje Tipo;
        public byte[] Datos;
        public IPEndPoint Origen;
    }

    /// UDP mínimo para 2 jugadores. Un hilo recibe y deja los paquetes en una cola;
    /// el hilo principal de Unity los procesa en Update.
    public class Red
    {
        public const byte Version = 1;
        const int SIO_UDP_CONNRESET = -1744830452;

        UdpClient udp;
        Thread hilo;
        volatile bool activo;
        readonly Queue<Paquete> cola = new Queue<Paquete>();
        readonly object candado = new object();

        public bool EsAnfitrion { get; private set; }
        public IPEndPoint Companero;   // con quién hablamos
        public string UltimoError;

        public bool Activo { get { return activo; } }

        public void Anfitrion(int puerto)
        {
            Cerrar();
            EsAnfitrion = true;
            Companero = null;
            Abrir(new IPEndPoint(IPAddress.Any, puerto));
        }

        public void Unirse(string ip, int puerto)
        {
            Cerrar();
            EsAnfitrion = false;
            IPAddress dir;
            if (!IPAddress.TryParse(ip.Trim(), out dir))
            {
                dir = Dns.GetHostEntry(ip.Trim()).AddressList[0];
            }
            Companero = new IPEndPoint(dir, puerto);
            Abrir(new IPEndPoint(IPAddress.Any, 0));
        }

        void Abrir(IPEndPoint local)
        {
            udp = new UdpClient(local);
            // En Windows/Wine, un "puerto inalcanzable" hace fallar Receive; lo desactivamos.
            try { udp.Client.IOControl(SIO_UDP_CONNRESET, new byte[] { 0, 0, 0, 0 }, null); } catch { }
            activo = true;
            hilo = new Thread(Recibir) { IsBackground = true, Name = "WitchHuntCoop.Red" };
            hilo.Start();
        }

        public void Cerrar()
        {
            if (ModoSteam)
            {
                try
                {
                    foreach (var id in pares.Values) SteamNetworking.CloseP2PSessionWithUser(id);
                    if (Sala.m_SteamID != 0) SteamMatchmaking.LeaveLobby(Sala);
                }
                catch { }
                pares.Clear(); Sala = CSteamID.Nil; ModoSteam = false;
            }
            activo = false;
            if (udp != null)
            {
                try { udp.Close(); } catch { }
                udp = null;
            }
            hilo = null;
            lock (candado) cola.Clear();
        }

        void Recibir()
        {
            IPEndPoint origen = new IPEndPoint(IPAddress.Any, 0);
            while (activo)
            {
                try
                {
                    byte[] buf = udp.Receive(ref origen);
                    if (buf.Length < 4 || buf[0] != (byte)'W' || buf[1] != (byte)'H' || buf[2] != Version)
                        continue;
                    byte[] datos = new byte[buf.Length - 4];
                    Buffer.BlockCopy(buf, 4, datos, 0, datos.Length);
                    var p = new Paquete { Tipo = (TipoMensaje)buf[3], Datos = datos, Origen = new IPEndPoint(origen.Address, origen.Port) };
                    lock (candado) cola.Enqueue(p);
                }
                catch (SocketException e)
                {
                    if (!activo) return;
                    if (e.ErrorCode == 10054) continue; // conexión reiniciada: ignorar
                    UltimoError = e.Message;
                }
                catch (ObjectDisposedException) { return; }
                catch (Exception e)
                {
                    if (!activo) return;
                    UltimoError = e.Message;
                }
            }
        }

        public bool Sacar(out Paquete p)
        {
            if (ModoSteam && activo) LeerSteam();
            lock (candado)
            {
                if (cola.Count > 0) { p = cola.Dequeue(); return true; }
            }
            p = null;
            return false;
        }

        public void Enviar(TipoMensaje tipo, byte[] datos, IPEndPoint destino = null)
        {
            destino = destino ?? Companero;
            if (destino == null || (udp == null && !ModoSteam)) return;
            byte[] buf = new byte[4 + (datos == null ? 0 : datos.Length)];
            buf[0] = (byte)'W'; buf[1] = (byte)'H'; buf[2] = Version; buf[3] = (byte)tipo;
            if (datos != null) Buffer.BlockCopy(datos, 0, buf, 4, datos.Length);
            if (ModoSteam) { EnviarSteam(tipo, datos, buf, destino); return; }
            try { udp.Send(buf, buf.Length, destino); }
            catch (Exception e) { UltimoError = e.Message; }
        }

        // ---------------- Steam P2P (2026-11-20): sala de amigos + invitación; los paquetes viajan por Steam ----------------
        // Cada amigo de Steam se "disfraza" de dirección (10.x.x.x:7 con su número de cuenta) para que el resto del mod
        // (saludo, compañero, sala llena, desconexión) funcione igual que con IP. Lo frecuente va rápido (sin reenvío) y
        // todo lo demás confiable: Steam reenvía lo que se pierde (llave, sello, cofres, compras, votos, daño, muertes).

        public bool ModoSteam { get; private set; }
        public CSteamID Sala { get; private set; }
        public bool SalaLista { get { return ModoSteam && Sala.m_SteamID != 0; } }
        readonly Dictionary<string, CSteamID> pares = new Dictionary<string, CSteamID>();
        CallResult<LobbyCreated_t> crSala; CallResult<LobbyEnter_t> crEntrar;
        static Callback<P2PSessionRequest_t> cbSesion; static Callback<GameLobbyJoinRequested_t> cbInvitacion;
        static Red instancia;
        /// Sala de Steam a la que me invitaron (aceptada en Steam o al abrir el juego con +connect_lobby); la toma Plugin.
        public static CSteamID InvitacionPendiente;
        public Action alCrearSala;              // anfitrión: la sala de Steam quedó creada
        public Action<CSteamID> alEntrarSala;   // invitado: entré a la sala (dueño = anfitrión)
        byte[] bufSteam = new byte[16384];

        static System.Reflection.PropertyInfo propIniciado; static bool buscadoIniciado;
        /// ¿El juego inició Steam? (SteamManager.Initialized es interno del juego: se lee por reflexión)
        public static bool SteamDisponible
        {
            get
            {
                try
                {
                    if (!buscadoIniciado)
                    {
                        buscadoIniciado = true;
                        var t = typeof(FPSPlayer).Assembly.GetType("SteamManager");
                        if (t != null) propIniciado = t.GetProperty("Initialized", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                    }
                    return propIniciado != null && (bool)propIniciado.GetValue(null, null);
                }
                catch { return false; }
            }
        }

        /// Una sola vez, cuando Steam ya está listo: aceptar sesiones P2P, recibir invitaciones y leer +connect_lobby.
        public static void IniciarSteam()
        {
            if (cbSesion != null || !SteamDisponible) return;
            cbSesion = Callback<P2PSessionRequest_t>.Create(r =>
            {
                if (instancia != null && instancia.ModoSteam && instancia.activo) SteamNetworking.AcceptP2PSessionWithUser(r.m_steamIDRemote);
            });
            cbInvitacion = Callback<GameLobbyJoinRequested_t>.Create(r => { InvitacionPendiente = r.m_steamIDLobby; });
            var a = Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++)
                if (a[i] == "+connect_lobby") { ulong id; if (ulong.TryParse(a[i + 1], out id) && id != 0) InvitacionPendiente = new CSteamID(id); }
        }

        public void AnfitrionSteam()
        {
            Cerrar(); instancia = this;
            EsAnfitrion = true; ModoSteam = true; Companero = null; activo = true; Sala = CSteamID.Nil; UltimoError = null;
            crSala = CallResult<LobbyCreated_t>.Create((r, fallo) =>
            {
                if (!ModoSteam) return;
                if (fallo || r.m_eResult != EResult.k_EResultOK) { UltimoError = "Steam no pudo crear la sala (" + r.m_eResult + ")"; return; }
                Sala = new CSteamID(r.m_ulSteamIDLobby);
                SteamMatchmaking.SetLobbyData(Sala, "witchhuntcoop", Plugin.Version);
                Invitar();
                if (alCrearSala != null) alCrearSala();
            });
            crSala.Set(SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, 2));
        }

        /// Abre la ventana de Steam para invitar a un amigo a la sala.
        public void Invitar() { try { if (SalaLista) SteamFriends.ActivateGameOverlayInviteDialog(Sala); } catch (Exception e) { UltimoError = e.Message; } }

        public static void AbrirAmigos() { try { SteamFriends.ActivateGameOverlay("friends"); } catch { } }

        public void UnirseSala(CSteamID sala)
        {
            Cerrar(); instancia = this;
            EsAnfitrion = false; ModoSteam = true; activo = true; Sala = sala; UltimoError = null;
            crEntrar = CallResult<LobbyEnter_t>.Create((r, fallo) =>
            {
                if (!ModoSteam) return;
                if (fallo || r.m_EChatRoomEnterResponse != 1) { UltimoError = "No se pudo entrar a la sala de Steam (" + r.m_EChatRoomEnterResponse + ")"; activo = false; return; }
                var dueno = SteamMatchmaking.GetLobbyOwner(sala);
                Companero = Falso(dueno);
                if (alEntrarSala != null) alEntrarSala(dueno);
            });
            crEntrar.Set(SteamMatchmaking.JoinLobby(sala));
        }

        IPEndPoint Falso(CSteamID id)
        {
            uint c = id.GetAccountID().m_AccountID;
            var ep = new IPEndPoint(new IPAddress(new byte[] { 10, (byte)(c >> 16), (byte)(c >> 8), (byte)c }), 7);
            pares[ep.ToString()] = id;
            return ep;
        }

        /// Nombre de Steam del compañero (para los avisos).
        public string NombreSteam(IPEndPoint ep)
        {
            CSteamID id;
            try { return ep != null && pares.TryGetValue(ep.ToString(), out id) ? SteamFriends.GetFriendPersonaName(id) : ""; } catch { return ""; }
        }

        static bool Frecuente(TipoMensaje t, byte[] d)
        {
            if (t == TipoMensaje.Estado || t == TipoMensaje.Monstruos || t == TipoMensaje.PingPong || t == TipoMensaje.Aliado) return true;
            if ((t == TipoMensaje.JefeCueva) && d != null && d.Length > 0 && d[0] == 0) return true;   // estado del jefe (10 por segundo)
            if (t == TipoMensaje.Perro && d != null && d.Length > 0 && d[0] == 1) return true;         // posición del perro
            return false;
        }

        void EnviarSteam(TipoMensaje tipo, byte[] datos, byte[] buf, IPEndPoint destino)
        {
            CSteamID id;
            if (!pares.TryGetValue(destino.ToString(), out id)) return;
            var modo = Frecuente(tipo, datos) && buf.Length <= 1100 ? EP2PSend.k_EP2PSendUnreliable : EP2PSend.k_EP2PSendReliable;
            try { if (!SteamNetworking.SendP2PPacket(id, buf, (uint)buf.Length, modo, 0)) UltimoError = "Steam no pudo enviar (" + tipo + ")"; }
            catch (Exception e) { UltimoError = e.Message; }
        }

        void LeerSteam()
        {
            uint tam; int vueltas = 0;
            try
            {
                while (vueltas++ < 400 && SteamNetworking.IsP2PPacketAvailable(out tam, 0))
                {
                    if (tam > bufSteam.Length) bufSteam = new byte[tam];
                    uint leidos; CSteamID de;
                    if (!SteamNetworking.ReadP2PPacket(bufSteam, tam, out leidos, out de, 0)) break;
                    if (leidos < 4 || bufSteam[0] != (byte)'W' || bufSteam[1] != (byte)'H' || bufSteam[2] != Version) continue;
                    var datos = new byte[leidos - 4];
                    Buffer.BlockCopy(bufSteam, 4, datos, 0, datos.Length);
                    var p = new Paquete { Tipo = (TipoMensaje)bufSteam[3], Datos = datos, Origen = Falso(de) };
                    lock (candado) cola.Enqueue(p);
                }
            }
            catch (Exception e) { UltimoError = e.Message; }
        }

        // ---- Ayudas para armar/leer datos ----
        public static byte[] Armar(Action<BinaryWriter> escribir)
        {
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                escribir(w);
                w.Flush();
                return ms.ToArray();
            }
        }

        public static BinaryReader Lector(byte[] datos)
        {
            return new BinaryReader(new MemoryStream(datos));
        }
    }
}

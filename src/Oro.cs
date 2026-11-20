// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using System;
using System.IO;
using HutongGames.PlayMaker;
using UnityEngine;

namespace WitchHuntCoop
{
    /// Oro compartido: lo que cualquiera de los dos recoge o gasta cambia la bolsa de ambos.
    /// UDP puede perder paquetes, así que no se envía "sumé 10" sino el total acumulado de
    /// lo que este jugador ha cambiado desde que se conectaron; el otro aplica la diferencia
    /// con lo último que vio. Un paquete perdido se corrige con el siguiente.
    public class Oro
    {
        public const string Variable = "Coins";

        readonly Red red;
        int aporteMio;        // suma de mis cambios locales desde la conexión
        int vistoOtro;        // último aporte del otro ya aplicado
        int ultimo;           // último valor de la bolsa que observé
        bool listo;           // ya tengo un valor de partida
        bool sincronizado;    // invitado: ya copió la bolsa del anfitrión
        int epoca;            // anfitrión: sube cada vez que su bolsa se recarga (escena, guardado)
        int epocaVista = -1;  // invitado: última época del anfitrión que copió
        float ultimoEnvio;
        int escena = -2;
        public string Origen = "?";

        public Oro(Red r) { red = r; }

        public void Reiniciar()
        {
            aporteMio = 0; vistoOtro = 0; listo = false; sincronizado = false;
        }

        // ---------- Acceso a la variable del juego ----------
        // PlayMaker global (int o float). Si no existe, null.
        static NamedVariable Global()
        {
            var g = FsmVariables.GlobalVariables;
            if (g == null) return null;
            var i = g.FindFsmInt(Variable);
            if (i != null) return i;
            return g.FindFsmFloat(Variable);
        }

        public static int? Leer()
        {
            var v = Global();
            if (v is FsmInt) return ((FsmInt)v).Value;
            if (v is FsmFloat) return Mathf.RoundToInt(((FsmFloat)v).Value);
            return null;
        }

        static void Escribir(int n)
        {
            var v = Global();
            if (v is FsmInt) ((FsmInt)v).Value = n;
            else if (v is FsmFloat) ((FsmFloat)v).Value = n;
        }

        /// Gasto hecho por el mod (p. ej. la tienda del cooperativo): se trata como cualquier
        /// cambio local, así que también se descuenta en el juego del compañero.
        /// Oro que da el mod (tesoro del castillo): sube la bolsa y, como cualquier cambio local, se comparte con el compañero.
        public static void Dar(int n)
        {
            int? c = Leer();
            if (c != null) Escribir(c.Value + n);
        }

        public static void Gastar(int n)
        {
            int? c = Leer();
            if (c != null) Escribir(Mathf.Max(0, c.Value - n));
        }

        public static string Describir()
        {
            var v = Global();
            return v == null ? "sin variable global '" + Variable + "'" : v.GetType().Name + " " + Variable + "=" + Leer();
        }

        // ---------- Bucle ----------
        public void Actualizar(bool conectado, int escenaLocal, bool hayJugador)
        {
            int? c = Leer();
            // Al cambiar de escena o sin jugador el juego puede recargar la bolsa: no es un cambio del jugador.
            if (escenaLocal != escena || !hayJugador || c == null)
            {
                // Tras cambiar de escena (o recargar la partida al morir) cada juego trae la bolsa de
                // su propio guardado: el invitado vuelve a tomar la del anfitrión.
                if (escenaLocal != escena && !red.EsAnfitrion) sincronizado = false;
                escena = escenaLocal; listo = false; return;
            }
            if (!listo) { ultimo = c.Value; listo = true; epoca++; }
            if (!conectado) { aporteMio = 0; vistoOtro = 0; sincronizado = false; epocaVista = -1; ultimo = c.Value; return; }

            if (c.Value != ultimo) { aporteMio += c.Value - ultimo; ultimo = c.Value; }

            float t = Time.realtimeSinceStartup;
            if (t - ultimoEnvio > 0.5f)
            {
                ultimoEnvio = t;
                int actual = c.Value;
                int ep = epoca;
                red.Enviar(TipoMensaje.Oro, Red.Armar(w => { w.Write(aporteMio); w.Write(actual); w.Write(ep); }));
            }
        }

        public void Recibir(BinaryReader r, Plugin plugin)
        {
            int aporte = r.ReadInt32(), totalOtro = r.ReadInt32(), epocaOtro = r.ReadInt32();
            int? c = Leer();
            if (c == null || !listo) return;
            // El invitado copia la bolsa del anfitrión al empezar y cada vez que la del anfitrión
            // se recarga (cambió de escena o recargó la partida al morir).
            if (!red.EsAnfitrion && (!sincronizado || epocaOtro != epocaVista))
            {
                epocaVista = epocaOtro;
                Escribir(totalOtro);
                // aporteMio NO se reinicia: el anfitrión lleva la cuenta acumulada de lo que le he enviado.
                ultimo = totalOtro; vistoOtro = aporte; sincronizado = true;
                plugin.Log("Oro sincronizado con el anfitrión: " + totalOtro);
                return;
            }
            int d = aporte - vistoOtro;
            vistoOtro = aporte;
            if (d == 0) return;
            int nuevo = Mathf.Max(0, c.Value + d);
            Escribir(nuevo);
            ultimo = nuevo;
            plugin.Log("Oro " + (d > 0 ? "+" : "") + d + " del compañero → " + nuevo);
        }

        // ---------- Marcador abajo al centro ----------
        static GUIStyle estilo, sombra;

        public static void Dibujar()
        {
            int? c = Leer();
            if (c == null) return;
            if (estilo == null)
            {
                estilo = new GUIStyle(GUI.skin.label) { fontSize = Opciones.T(22), alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
                estilo.normal.textColor = new Color(1f, 0.82f, 0.25f);
                sombra = new GUIStyle(estilo);
                sombra.normal.textColor = new Color(0f, 0f, 0f, 0.8f);
            }
            string txt = "● " + c.Value + " oro";
            var rect = new Rect(Screen.width / 2f - 120f, Screen.height - 46f, 240f, 34f);
            GUI.Label(new Rect(rect.x + 2, rect.y + 2, rect.width, rect.height), txt, sombra);
            GUI.Label(rect, txt, estilo);
        }
    }
}

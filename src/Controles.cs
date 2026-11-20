// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using System.Collections.Generic;
using Rewired;
using UnityEngine;

namespace WitchHuntCoop
{
    /// Controles editables (pedido 2026-11-10): todas las acciones del juego (Rewired) y las del mod (armas 1–5, estoque,
    /// los dos votos y el panel). Pestaña "Controles" de las opciones del mod. Lo cambiado se guarda en PlayerPrefs y se
    /// vuelve a aplicar al abrir el juego; "Restablecer" devuelve todo a como venía.
    public static class Controles
    {
        public enum Origen { Rewired, Mod }

        public class Accion
        {
            public string Id, Nombre, Grupo;
            public Origen Fuente;
            public string RewiredNombre; public Pole Polo = Pole.Positive;   // acción del juego
            public KeyCode PorDefecto;                                       // acción del mod
            // lo que traía el juego (se guarda al arrancar)
            public string Original;
        }

        public static readonly List<Accion> Lista = new List<Accion>();
        static readonly Dictionary<string, KeyCode> teclasMod = new Dictionary<string, KeyCode>();
        static bool iniciado, aplicadoGuardado;
        static Plugin plugin;

        static void A(string id, string nombre, string grupo, string rewired, Pole polo = Pole.Positive)
        { Lista.Add(new Accion { Id = id, Nombre = nombre, Grupo = grupo, Fuente = Origen.Rewired, RewiredNombre = rewired, Polo = polo }); }
        static void M(string id, string nombre, string grupo, KeyCode def)
        { Lista.Add(new Accion { Id = id, Nombre = nombre, Grupo = grupo, Fuente = Origen.Mod, PorDefecto = def }); }

        public static void Iniciar(Plugin p)
        {
            if (iniciado) return;
            iniciado = true; plugin = p;
            A("adelante", "Avanzar", "Movimiento", "Vertical", Pole.Positive);
            A("atras", "Retroceder", "Movimiento", "Vertical", Pole.Negative);
            A("izquierda", "Moverse a la izquierda", "Movimiento", "Horizontal", Pole.Negative);
            A("derecha", "Moverse a la derecha", "Movimiento", "Horizontal", Pole.Positive);
            A("saltar", "Saltar", "Movimiento", "Jump");
            A("agacharse", "Agacharse", "Movimiento", "Crouch");
            A("correr", "Correr", "Movimiento", "Sprint");
            A("disparar", "Disparar", "Combate", "Fire");
            A("apuntar", "Apuntar", "Combate", "Zoom");
            A("recargar", "Recargar", "Combate", "Reload");
            A("guardar_arma", "Guardar el arma", "Combate", "Holster Weapon");
            M("arma1", "Pistola", "Armas", KeyCode.Alpha1);
            M("arma2", "Mosquete (rifle)", "Armas", KeyCode.Alpha2);
            M("arma3", "Escopeta recortada", "Armas", KeyCode.Alpha3);
            M("arma4", "Ballesta", "Armas", KeyCode.Alpha4);
            M("arma5", "Bola de cristal", "Armas", KeyCode.Alpha5);
            M("estoque", "Estoque (cuerpo a cuerpo)", "Armas", KeyCode.F);
            M("modo_escopeta", "Escopeta: doble / un cañón · Ballesta: 3 / 1 flecha", "Armas", KeyCode.X);
            A("usar", "Usar / interactuar", "Acciones", "Use");
            A("mapa", "Mapa", "Acciones", "Map");
            A("inventario", "Inventario", "Acciones", "Inventory");
            A("linterna", "Linterna", "Acciones", "Headlamp");
            A("huevo", "Plantar huevo", "Acciones", "Plant Egg");
            A("ojo", "Ojo de bruja", "Acciones", "skill_evilsight");
            M("voto_si", "Votar que sí (viajes)", "Cooperativo", KeyCode.Y);
            M("voto_no", "Votar que no (viajes)", "Cooperativo", KeyCode.N);
            foreach (var a in Lista)
            {
                if (a.Fuente == Origen.Mod) { teclasMod[a.Id] = a.PorDefecto; }
                else a.Original = LeerRewired(a);
            }
            teclasMod["panel"] = plugin != null ? plugin.TeclaPanel : KeyCode.F8;   // sin fila en la pestaña: sigue siendo F8 (o lo que diga la configuración)
            CargarMod();
        }

        // ---------------- Teclas del mod ----------------

        static void CargarMod()
        {
            foreach (var a in Lista)
            {
                if (a.Fuente != Origen.Mod) continue;
                string s = null; try { s = PlayerPrefs.GetString("coop_ctrl_" + a.Id, ""); } catch { }
                KeyCode k;
                if (a.Id == "panel" && plugin != null) { teclasMod[a.Id] = plugin.TeclaPanel; continue; }
                if (!string.IsNullOrEmpty(s) && TryKey(s, out k)) teclasMod[a.Id] = k;
            }
        }

        static bool TryKey(string s, out KeyCode k)
        {
            k = KeyCode.None;
            try { k = (KeyCode)System.Enum.Parse(typeof(KeyCode), s); return true; } catch { return false; }
        }

        public static KeyCode Tecla(string id) { KeyCode k; return teclasMod.TryGetValue(id, out k) ? k : KeyCode.None; }
        public static bool Pulsada(string id) { var k = Tecla(id); return k != KeyCode.None && Input.GetKeyDown(k); }

        // ---------------- Teclas del juego (Rewired) ----------------

        static Player Jugador() { try { return ReInput.isReady ? ReInput.players.GetPlayer(0) : null; } catch { return null; } }

        /// "Interactuar": la acción Use del juego (si el jugador la cambia, el mod la sigue).
        public static bool Interactuar() { if (Entrada.UsarPulso) { Entrada.UsarPulso = false; return true; } var p = Jugador(); return p != null && p.GetButtonDown("Use"); }
        public static bool InteractuarMantenida() { var p = Jugador(); return p != null && p.GetButton("Use"); }

        static ActionElementMap Buscar(Accion a, out ControllerMap mapa)
        {
            mapa = null; var p = Jugador(); if (p == null) return null;
            var acc = ReInput.mapping.GetAction(a.RewiredNombre); if (acc == null) return null;
            foreach (var tipo in new[] { ControllerType.Keyboard, ControllerType.Mouse })
                foreach (var m in p.controllers.maps.GetMaps(tipo, 0))
                    foreach (var em in m.GetElementMapsWithAction(acc.id))
                        if (acc.type == InputActionType.Button || em.axisContribution == a.Polo) { mapa = m; return em; }
            return null;
        }

        /// "K:Space", "M:Left Mouse Button" o "" (sin asignar).
        static string LeerRewired(Accion a)
        {
            ControllerMap m; var em = Buscar(a, out m);
            if (em == null) return "";
            return m.controllerType == ControllerType.Keyboard ? "K:" + em.keyCode : "M:" + em.elementIdentifierName;
        }

        public static string Texto(Accion a)
        {
            if (a.Fuente == Origen.Mod) return NombreTecla(Tecla(a.Id));
            string s = LeerRewired(a);
            if (string.IsNullOrEmpty(s)) return "sin asignar";
            return s.StartsWith("K:") ? NombreTecla((KeyCode)System.Enum.Parse(typeof(KeyCode), s.Substring(2))) : NombreRaton(s.Substring(2));
        }

        public static string NombreTecla(KeyCode k)
        {
            switch (k)
            {
                case KeyCode.None: return "sin asignar";
                case KeyCode.Space: return "Espacio";
                case KeyCode.LeftShift: return "Mayús izq.";
                case KeyCode.RightShift: return "Mayús der.";
                case KeyCode.LeftControl: return "Ctrl izq.";
                case KeyCode.RightControl: return "Ctrl der.";
                case KeyCode.LeftAlt: return "Alt izq.";
                case KeyCode.RightAlt: return "Alt der.";
                case KeyCode.Tab: return "Tab";
                case KeyCode.Return: return "Intro";
                case KeyCode.Backspace: return "Retroceso";
                case KeyCode.BackQuote: return "`";
                case KeyCode.UpArrow: return "↑"; case KeyCode.DownArrow: return "↓"; case KeyCode.LeftArrow: return "←"; case KeyCode.RightArrow: return "→";
                case KeyCode.Mouse0: return "Clic izq."; case KeyCode.Mouse1: return "Clic der."; case KeyCode.Mouse2: return "Clic central";
                case KeyCode.Mouse3: return "Botón 4"; case KeyCode.Mouse4: return "Botón 5";
            }
            string s = k.ToString();
            if (s.StartsWith("Alpha")) return s.Substring(5);
            if (s.StartsWith("Keypad")) return "Num " + s.Substring(6);
            return s;
        }

        static string NombreRaton(string n)
        {
            if (n.Contains("Left")) return "Clic izq."; if (n.Contains("Right")) return "Clic der."; if (n.Contains("Middle")) return "Clic central";
            if (n.Contains("4")) return "Botón 4"; if (n.Contains("5")) return "Botón 5";
            return n;
        }

        // ---------------- Cambiar una tecla ----------------

        public static float HastaEsc;           // hasta cuándo el Esc de cancelar no debe llegar al juego
        public static Accion Capturando;       // la acción esperando una tecla
        public static string Aviso = "";

        public static void Empezar(Accion a) { Capturando = a; Aviso = ""; }

        /// Desde OnGUI mientras se espera: la tecla o el botón del ratón que llegue se asigna (Esc cancela).
        public static bool Procesar(Event e)
        {
            if (Capturando == null) return false;
            KeyCode k = KeyCode.None;
            if (e.type == EventType.KeyDown && e.keyCode != KeyCode.None) k = e.keyCode;
            else if (e.type == EventType.MouseDown && e.button >= 0 && e.button <= 4) k = KeyCode.Mouse0 + e.button;
            else if (e.type == EventType.KeyDown) { e.Use(); return true; }
            if (k == KeyCode.None) return true;   // otro evento: se sigue esperando
            e.Use();
            if (k == KeyCode.Escape) { Capturando = null; HastaEsc = Time.realtimeSinceStartup + 0.4f; return true; }
            Asignar(Capturando, k);
            Capturando = null;
            return true;
        }

        public static void Asignar(Accion a, KeyCode k)
        {
            if (a.Fuente == Origen.Mod)
            {
                if (k >= KeyCode.Mouse0 && k <= KeyCode.Mouse6) { Aviso = "Esta acción solo admite teclas."; return; }
                teclasMod[a.Id] = k;
                if (a.Id == "panel" && plugin != null) plugin.TeclaPanel = k;
                try { PlayerPrefs.SetString("coop_ctrl_" + a.Id, k.ToString()); PlayerPrefs.Save(); } catch { }
            }
            else
            {
                if (!PonerRewired(a, k)) { Aviso = "No se pudo cambiar (" + a.Nombre + ")."; return; }
                try { PlayerPrefs.SetString("coop_ctrl_" + a.Id, k.ToString()); PlayerPrefs.Save(); } catch { }
            }
            Aviso = "";
            foreach (var o in Lista) if (o != a && Texto(o) == Texto(a) && Texto(a) != "sin asignar") { Aviso = "«" + NombreTecla(k) + "» también se usa en «" + o.Nombre + "»."; break; }
        }

        static bool PonerRewired(Accion a, KeyCode k)
        {
            var p = Jugador(); if (p == null) return false;
            var acc = ReInput.mapping.GetAction(a.RewiredNombre); if (acc == null) return false;
            ControllerMap mapaViejo; var viejo = Buscar(a, out mapaViejo);
            bool raton = k >= KeyCode.Mouse0 && k <= KeyCode.Mouse4;
            ControllerMap destino = null;
            foreach (var m in p.controllers.maps.GetMaps(raton ? ControllerType.Mouse : ControllerType.Keyboard, 0)) { destino = m; if (viejo != null && m == mapaViejo) break; }
            if (destino == null) return false;
            if (viejo != null && mapaViejo != destino) mapaViejo.DeleteElementMap(viejo.id);   // cambia de teclado a ratón (o al revés)
            ElementAssignment asig;
            if (!raton) asig = new ElementAssignment(k, ModifierKeyFlags.None, acc.id, a.Polo);
            else
            {
                string nombre = k == KeyCode.Mouse0 ? "Left Mouse Button" : k == KeyCode.Mouse1 ? "Right Mouse Button" : k == KeyCode.Mouse2 ? "Middle Mouse Button" : k == KeyCode.Mouse3 ? "Mouse Button 4" : "Mouse Button 5";
                int idElem = -1;
                foreach (var ei in ReInput.controllers.Mouse.ElementIdentifiers) if (ei.name == nombre) { idElem = ei.id; break; }
                if (idElem < 0) return false;
                asig = new ElementAssignment(ControllerType.Mouse, ControllerElementType.Button, idElem, AxisRange.Positive, KeyCode.None, ModifierKeyFlags.None, acc.id, a.Polo, false);
            }
            if (viejo != null && mapaViejo == destino) asig.elementMapId = viejo.id;
            return destino.ReplaceOrCreateElementMap(asig);
        }

        // ---------------- Guardado y restablecer ----------------

        /// Al abrir el juego (cuando Rewired ya está listo): se vuelven a aplicar las teclas guardadas.
        public static void Actualizar()
        {
            if (!iniciado || aplicadoGuardado || Jugador() == null) return;
            aplicadoGuardado = true;
            int n = 0;
            foreach (var a in Lista)
            {
                if (a.Fuente != Origen.Rewired) continue;
                string s = null; try { s = PlayerPrefs.GetString("coop_ctrl_" + a.Id, ""); } catch { }
                KeyCode k;
                if (!string.IsNullOrEmpty(s) && TryKey(s, out k) && PonerRewired(a, k)) n++;
            }
            if (n > 0 && plugin != null) plugin.Log("Controles: " + n + " teclas guardadas aplicadas");
        }

        public static void Restablecer()
        {
            foreach (var a in Lista)
            {
                try { PlayerPrefs.DeleteKey("coop_ctrl_" + a.Id); } catch { }
                if (a.Fuente == Origen.Mod) { teclasMod[a.Id] = a.PorDefecto; if (a.Id == "panel" && plugin != null) plugin.TeclaPanel = a.PorDefecto; }
                else if (!string.IsNullOrEmpty(a.Original) && a.Original.StartsWith("K:"))
                { KeyCode k; if (TryKey(a.Original.Substring(2), out k)) PonerRewired(a, k); }
                else if (!string.IsNullOrEmpty(a.Original) && a.Original.StartsWith("M:"))
                {
                    string n = a.Original.Substring(2);
                    KeyCode k = n.Contains("Left") ? KeyCode.Mouse0 : n.Contains("Right") ? KeyCode.Mouse1 : n.Contains("Middle") ? KeyCode.Mouse2 : n.Contains("4") ? KeyCode.Mouse3 : KeyCode.Mouse4;
                    PonerRewired(a, k);
                }
            }
            try { PlayerPrefs.Save(); } catch { }
            Aviso = "Controles restablecidos.";
        }

        public static string Describir()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var a in Lista) sb.Append(a.Nombre).Append('=').Append(Texto(a)).Append("; ");
            return sb.ToString();
        }
    }
}

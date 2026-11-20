// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using System;
using PixelCrushers.DialogueSystem;
using UnityEngine;

namespace WitchHuntCoop
{
    /// Artefactos nuevos del cooperativo. La lista de la tienda del juego la arma PlayMaker por
    /// dentro; en vez de meter botones ahí (frágil), se dibuja una sección propia al lado
    /// mientras la tienda está abierta. Lo comprado se guarda como variable de Lua, que el juego
    /// guarda junto con el resto de la partida.
    public class Tienda
    {
        public const string VarLibro = "coop_libro_curacion";
        public const int PrecioLibro = 120;
        public const float FactorLibro = 3f;

        readonly Plugin plugin;
        GameObject canvas;
        float buscarCanvas;
        string mensaje = ""; float mensajeHasta;
        float regenPuesta = -1f;

        public Tienda(Plugin p) { plugin = p; }

        public static bool TieneLibro
        {
            get { try { return DialogueLua.GetVariable(VarLibro).AsBool; } catch { return false; } }
        }

        public bool Abierta
        {
            get
            {
                // tras viajar entre escenas puede quedar más de un Canvas_shop (uno inactivo para siempre): se prefiere el activo
                // y se vuelve a buscar si el guardado no lo está (antes la tienda nunca contaba como abierta: sin rojo en las filas)
                if ((canvas == null || !canvas.activeInHierarchy) && Time.realtimeSinceStartup > buscarCanvas)
                {
                    buscarCanvas = Time.realtimeSinceStartup + 0.5f;
                    GameObject cualquiera = null;
                    foreach (var g in Resources.FindObjectsOfTypeAll<GameObject>())
                        if (g.name == "Canvas_shop" && g.scene.IsValid()) { if (g.activeInHierarchy) { canvas = g; cualquiera = null; break; } if (cualquiera == null) cualquiera = g; }
                    if (cualquiera != null && (canvas == null || !canvas.activeInHierarchy)) canvas = cualquiera;
                }
                return canvas != null && canvas.activeInHierarchy;
            }
        }

        /// El libro duplica la curación natural. La habilidad del juego (curación rápida) o un
        /// cambio de escena pueden cambiar healthRegenRate: si cambió, ese es el nuevo valor base.
        public void Actualizar(FPSPlayer j)
        {
            if (j == null) { regenPuesta = -1f; return; }
            if (!TieneLibro) return;
            if (Mathf.Abs(j.healthRegenRate - regenPuesta) > 1e-5f)
            {
                regenPuesta = j.healthRegenRate * FactorLibro;
                j.healthRegenRate = regenPuesta;
                plugin.Log("Libro de curación: regeneración ×" + FactorLibro + " (" + regenPuesta.ToString("0.###") + "/s)");
            }
        }

        public const int PrecioGanzua = 5;

        public const int PrecioFlechas = 10, FlechasPorCompra = 2;

        /// 2 flechas por 10 de oro (de la bolsa compartida); las flechas son de quien las compra.
        /// Compra balas de plata para UN arma de fuego (pistola, mosquete o escopeta), pagando de la bolsa de oro compartida.
        public string ComprarBalas(int tipo, int cuantas, int precio, string arma)
        {
            int tope = Municion.MaxDe(tipo);
            if (plugin.municion.BalasQueTengo(tipo) >= tope) return "Ya llevas el máximo de balas para la " + arma + " (" + tope + ").";
            int? oro = Oro.Leer();
            if (oro == null || oro.Value < precio) return "No tienen oro suficiente (" + precio + ").";
            Oro.Gastar(precio);
            int n = plugin.municion.SumarBalas(tipo, cuantas);
            plugin.Log("Tienda: " + n + " balas de plata para " + arma + " por " + precio + " oro (ahora " + plugin.municion.BalasQueTengo(tipo) + ")");
            return "Compraste " + n + (n == 1 ? " bala" : " balas") + " para la " + arma + " (tienes " + plugin.municion.BalasQueTengo(tipo) + ").";
        }

        public string ComprarFlechas()
        {
            int tope = Municion.MaxDe(Municion.Flechas);
            if (plugin.municion.FlechasQueTengo >= tope) return "Ya llevas el máximo de flechas (" + tope + ").";
            int? oro = Oro.Leer();
            if (oro == null || oro.Value < PrecioFlechas) return "No tienen oro suficiente (" + PrecioFlechas + ").";
            Oro.Gastar(PrecioFlechas);
            int n = plugin.municion.SumarFlechas(FlechasPorCompra);
            plugin.Log("Tienda: " + n + " flechas por " + PrecioFlechas + " oro (ahora " + plugin.municion.FlechasQueTengo + ")");
            return "Compraste " + n + " flechas (tienes " + plugin.municion.FlechasQueTengo + ").";
        }

        public string ComprarGanzuas(int n)
        {
            var gz = Mundo.Ganzuas();
            if (gz == null) return "No se pueden comprar ganzúas aquí.";
            n = Mathf.Min(n, Mundo.MaxGanzuas - gz.Value);
            if (n <= 0) return "Ya llevas el máximo de ganzúas (" + Mundo.MaxGanzuas + ").";
            int? oro = Oro.Leer();
            if (oro == null || oro.Value < n * PrecioGanzua) return "No tienen oro suficiente (" + n * PrecioGanzua + ").";
            Oro.Gastar(n * PrecioGanzua);
            gz.Value = gz.Value + n;
            plugin.Log("Tienda: " + n + " ganzúas por " + n * PrecioGanzua + " oro (ahora " + gz.Value + ")");
            return "Compraste " + n + (n == 1 ? " ganzúa." : " ganzúas.");
        }

        public void Comprar() { Avisar(LibrosTienda.Comprar(plugin, LibrosTienda.Lista[0])); }

        void Avisar(string m) { mensaje = m; mensajeHasta = Time.realtimeSinceStartup + 3f; }

        static GUIStyle titulo, nombre, texto, tenido, caja;
        Vector2 desplazamiento;

        public void Dibujar()
        {
            if (!Abierta) return;
            if (titulo == null)
            {
                titulo = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, wordWrap = true };
                titulo.normal.textColor = new Color(1f, 0.82f, 0.25f);
                nombre = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, wordWrap = true };
                nombre.normal.textColor = new Color(0.95f, 0.92f, 0.85f);
                texto = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true };
                texto.normal.textColor = new Color(0.82f, 0.8f, 0.74f);
                tenido = new GUIStyle(texto) { fontStyle = FontStyle.Bold };
                tenido.normal.textColor = new Color(0.55f, 0.85f, 0.55f);
                caja = new GUIStyle(GUI.skin.box);
            }
            float w = 250f, h = Mathf.Min(Screen.height - 40f, 560f);
            var r = new Rect(Screen.width - w - 8f, Screen.height * 0.5f - h * 0.5f, w, h);
            GUI.Box(r, "", caja); GUI.Box(r, "", caja); GUI.Box(r, "", caja);
            GUILayout.BeginArea(new Rect(r.x + 10, r.y + 8, r.width - 20, r.height - 16));
            GUILayout.Label("Libros del cooperativo", titulo);
            GUILayout.Label("Se pagan con el oro compartido y los tienen los dos.", texto);
            if (Time.realtimeSinceStartup < mensajeHasta) GUILayout.Label(mensaje, tenido);
            desplazamiento = GUILayout.BeginScrollView(desplazamiento);
            var gz = Mundo.Ganzuas();
            GUILayout.Space(4);
            GUILayout.Label("Ganzúas (tienes " + (gz != null ? gz.Value : 0) + ")", nombre);
            GUILayout.Label(PrecioGanzua + " oro cada una, máximo " + Mundo.MaxGanzuas + ". Son solo tuyas.", texto);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Comprar 1", GUILayout.Height(26))) Avisar(ComprarGanzuas(1));
            if (GUILayout.Button("Comprar 10", GUILayout.Height(26))) Avisar(ComprarGanzuas(10));
            GUILayout.EndHorizontal();
            foreach (var l in LibrosTienda.Lista)
            {
                GUILayout.Space(6);
                GUILayout.Label(l.Nombre, nombre);
                GUILayout.Label(l.Efecto, texto);
                if (LibrosTienda.Tiene(l.Var)) GUILayout.Label("✔ Ya lo tienen", tenido);
                else if (GUILayout.Button("Comprar — " + l.Precio + " oro", GUILayout.Height(26))) Avisar(LibrosTienda.Comprar(plugin, l));
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
    }
}

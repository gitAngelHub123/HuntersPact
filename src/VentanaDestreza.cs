// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using HutongGames.PlayMaker;
using UnityEngine;
using UnityEngine.UI;

namespace WitchHuntCoop
{
    /// La ventana "NUEVA DESTREZA CONSEGUIDA" del juego para los libros del cooperativo: solo en el
    /// juego de quien compra. Se abre con el mismo evento que usa el juego (SkillWindow: pausa,
    /// desenfoque y "presiona cualquier botón para cerrar"); el icono va por la variable global
    /// achievement_texture y el nombre y la descripción se escriben después, porque el juego los
    /// busca en su tabla de traducciones, donde estos libros no existen.
    public static class VentanaDestreza
    {
        static LibrosTienda.Libro pendiente;
        static float hasta;
        static Text nombre, descripcion;

        static LibrosTienda.Libro enEspera;
        static Plugin pluginEspera;
        static float mostrarEn;

        /// Cierra la tienda como la X del juego (oculta Canvas_shop y manda Close_Shop a
        /// inventory_and_map, que quita el desenfoque y esconde el cursor) y abre la ventana un
        /// momento después, para que el Unblur del cierre no le quite el desenfoque a la ventana.
        public static void MostrarTrasCerrarTienda(Plugin plugin, LibrosTienda.Libro l)
        {
            foreach (var g in Resources.FindObjectsOfTypeAll<GameObject>())
                if (g.name == "Canvas_shop" && g.scene.IsValid() && g.activeSelf) g.SetActive(false);
            foreach (var f in Resources.FindObjectsOfTypeAll<PlayMakerFSM>())
                if (f.FsmName == "inventory_and_map" && f.gameObject.scene.IsValid() && f.gameObject.name.StartsWith("Canvas_RFPS")) { f.SendEvent("Close_Shop"); break; }
            enEspera = l; pluginEspera = plugin; mostrarEn = Time.realtimeSinceStartup + 0.2f;
        }

        public static void Mostrar(Plugin plugin, LibrosTienda.Libro l)
        {
            var g = FsmVariables.GlobalVariables;
            var tx = g.FindFsmTexture("achievement_texture");
            var icono = TiendaUI.Icono("skill", l.Var);
            if (tx != null && icono != null) tx.Value = icono;
            var sn = g.FindFsmString("skill_name"); if (sn != null) sn.Value = l.Nombre;
            var sd = g.FindFsmString("skill_description"); if (sd != null) sd.Value = l.Efecto;
            PlayMakerFSM ventana = null;
            foreach (var f in Resources.FindObjectsOfTypeAll<PlayMakerFSM>())
                if (f.FsmName == "SkillWindow" && f.gameObject.scene.IsValid() && f.gameObject.name.StartsWith("Canvas_RFPS")) { ventana = f; break; }
            if (ventana == null) { plugin.Log("Destreza: no encontré la ventana del juego"); return; }
            ventana.SendEvent("SkillWindow");
            pendiente = l; hasta = Time.realtimeSinceStartup + 2.5f;
            nombre = descripcion = null;
            plugin.Log("Destreza: ventana de " + l.Nombre);
        }

        /// Para pruebas: como pulsar una tecla con la ventana abierta.
        public static void Cerrar()
        {
            foreach (var f in Resources.FindObjectsOfTypeAll<PlayMakerFSM>())
                if (f.FsmName == "SkillWindow" && f.gameObject.scene.IsValid() && f.gameObject.name.StartsWith("Canvas_RFPS")) f.SendEvent("FINISHED");
        }

        /// Mientras la ventana se abre, su lógica pone textos de la tabla del juego: se reemplazan.
        public static void Actualizar()
        {
            if (enEspera != null && Time.realtimeSinceStartup >= mostrarEn) { var l = enEspera; enEspera = null; Mostrar(pluginEspera, l); }
            if (pendiente == null) return;
            if (Time.realtimeSinceStartup > hasta) { pendiente = null; return; }
            if (nombre == null || descripcion == null)
                foreach (var t in Resources.FindObjectsOfTypeAll<Text>())
                {
                    if (!t.gameObject.scene.IsValid() || t.transform.parent == null || t.transform.parent.name != "NewSkill") continue;
                    if (t.name == "text_skill_name") nombre = t;
                    else if (t.name == "text_description") descripcion = t;
                }
            if (nombre != null && nombre.text != pendiente.Nombre) nombre.text = pendiente.Nombre;
            if (descripcion != null && descripcion.text != pendiente.Efecto) descripcion.text = pendiente.Efecto;
        }
    }
}

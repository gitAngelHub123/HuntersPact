// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using UnityEngine;

namespace WitchHuntCoop
{
    /// Busca modelos y animaciones que ya trae el juego (no se distribuye nada con el mod).
    public static class Modelos
    {
        public const string Cazador = "Witch_Hunter";       // el cazador (humanoide, solo "idle")
        public const string FuenteAnim = "Undead_naked";    // momia humanoide con caminar/correr
        public const string ClipCaminar = "unarmed_walk_forward_inPlace";
        public const string ClipCorrer = "Standard Run message";

        public static GameObject Buscar(string nombre)
        {
            GameObject mejor = null;
            foreach (var g in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (g.name != nombre) continue;
                if (g.GetComponent<Animator>() != null) return g;
                if (mejor == null) mejor = g;
            }
            return mejor;
        }

        public static AnimationClip Clip(GameObject dueno, string nombre)
        {
            if (dueno == null) return null;
            var an = dueno.GetComponentInChildren<Animator>(true);
            if (an == null || an.runtimeAnimatorController == null) return null;
            foreach (var c in an.runtimeAnimatorController.animationClips)
                if (c != null && c.name == nombre) return c;
            return null;
        }

        /// Copia del clip sin eventos de animación: los de la momia llaman a funciones de su IA
        /// ("..._message") que el compañero no tiene y llenarían el registro de errores.
        public static AnimationClip SinEventos(AnimationClip c)
        {
            if (c == null) return null;
            if (c.events == null || c.events.Length == 0) return c;
            var copia = Object.Instantiate(c);
            copia.name = c.name;
            copia.events = new AnimationEvent[0];
            return copia;
        }

        /// Quita todo lo que tenga comportamiento (IA, PlayMaker, física, sonido) y deja mallas y Animator.
        public static void Desactivar(GameObject go)
        {
            foreach (var c in go.GetComponentsInChildren<Component>(true))
            {
                if (c == null || c is Transform || c is Renderer || c is MeshFilter || c is Animator || c is Animation) continue;
                if (c is Behaviour) ((Behaviour)c).enabled = false;
                if (c is Collider) ((Collider)c).enabled = false;
                if (c is Rigidbody) ((Rigidbody)c).isKinematic = true;
                if (c is MonoBehaviour || c is Collider || c is AudioSource) Object.Destroy(c);
            }
        }
    }
}

// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using System.Collections.Generic;
using UnityEngine;

namespace WitchHuntCoop
{
    /// Poses a mano para los cadáveres copiados (el del juego viene tendido): giros por hueso sobre su rotación de partida.
    /// Pose.Sentado deja al minero sentado contra la pared (pedido 2026-11-17: que no tape la ballesta).
    public static class PoseCuerpo
    {
        public static readonly Dictionary<string, Vector3> Sentado = new Dictionary<string, Vector3>();
        static readonly Dictionary<Transform, Quaternion> base0 = new Dictionary<Transform, Quaternion>();

        public static string Huesos(GameObject g)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var s in g.GetComponentsInChildren<SkinnedMeshRenderer>(true)) { foreach (var b in s.bones) if (b != null) sb.Append(b.name).Append(" | "); break; }
            return sb.ToString();
        }

        /// Aplica la pose (nombre de hueso → giro local en grados) sobre la rotación original de cada hueso.
        public static void Aplicar(GameObject g, Dictionary<string, Vector3> pose)
        {
            foreach (var t in g.GetComponentsInChildren<Transform>(true))
            {
                Quaternion q; if (!base0.TryGetValue(t, out q)) { q = t.localRotation; base0[t] = q; }
                Vector3 e; t.localRotation = pose.TryGetValue(t.name, out e) ? q * Quaternion.Euler(e) : q;
            }
        }
    }
}

// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using UnityEngine;

namespace WitchHuntCoop
{
    /// Referencia común de posiciones. El juego recentra el mundo al alejarse 700 m (WorldRecenter)
    /// y mueve todo, terreno incluido; por eso las posiciones se miden respecto al terreno.
    /// Con la expansión hay dos terrenos: la referencia es siempre el original de la escena
    /// (Terrain.activeTerrain podría devolver el nuevo).
    public static class Anclaje
    {
        static Terrain cache;

        public static Terrain Principal()
        {
            if (cache != null && cache.isActiveAndEnabled) return cache;
            cache = null;
            foreach (var t in Terrain.activeTerrains)
                if (t != null && !t.name.StartsWith("Coop ")) { cache = t; break; }
            return cache;
        }

        public static Vector3 Origen()
        {
            var t = Principal();
            return t != null ? t.transform.position : Vector3.zero;
        }
    }
}

using UnityEngine;

namespace WitchHuntCoop
{
    /// "Todo objeto que esté flotando en el aire debe estar en el suelo" (pedido 2026-11-17): baja un objeto hasta la primera superficie
    /// sólida que tenga debajo (suelo, mesa, piso de una torre…), sin contar sus propias colisiones. Solo baja (nunca sube ni hunde).
    public static class Asentar
    {
        public static float EnSuelo(GameObject g, float maxBajada = 40f)
        {
            if (g == null) return 0f;
            Bounds b = new Bounds(); bool hay = false;
            foreach (var r in g.GetComponentsInChildren<Renderer>(false))
            {
                if (!r.enabled || r is ParticleSystemRenderer) continue;
                if (!hay) { b = r.bounds; hay = true; } else b.Encapsulate(r.bounds);
            }
            if (!hay) return 0f;
            var propias = g.GetComponentsInChildren<Collider>(false);
            var estaban = new bool[propias.Length];
            for (int i = 0; i < propias.Length; i++) { estaban[i] = propias[i].enabled; propias[i].enabled = false; }
            float bajada = 0f;
            try
            {
                // varios rayos (centro y cerca de las esquinas de la base): se toma el apoyo más alto para no hundirlo en un borde
                float mejor = float.NegativeInfinity; RaycastHit hit;
                foreach (var d in new[] { Vector2.zero, new Vector2(-0.3f, -0.3f), new Vector2(0.3f, -0.3f), new Vector2(-0.3f, 0.3f), new Vector2(0.3f, 0.3f) })
                {
                    var o = new Vector3(b.center.x + b.extents.x * d.x, b.min.y + 0.25f, b.center.z + b.extents.z * d.y);
                    if (Physics.Raycast(o, Vector3.down, out hit, maxBajada + 0.25f, ~0, QueryTriggerInteraction.Ignore)) mejor = Mathf.Max(mejor, hit.point.y);
                }
                if (!float.IsNegativeInfinity(mejor) && b.min.y - mejor > 0.06f) { bajada = b.min.y - mejor; g.transform.position += Vector3.down * bajada; }
            }
            finally { for (int i = 0; i < propias.Length; i++) propias[i].enabled = estaban[i]; }
            return bajada;
        }
    }
}

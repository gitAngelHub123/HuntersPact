// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using UnityEngine;

namespace WitchHuntCoop
{
    /// Temblor de cámara (2026-11-19: la cueva tiembla cuando cae la roca del jefe). Se aplica justo antes de dibujar
    /// y se deshace justo después: no se acumula ni pelea con el control de la cámara del juego.
    public static class Temblor
    {
        static float hasta, fuerza, dur = 1f; static bool enganchado;
        static Vector3 offPos; static Quaternion offRot = Quaternion.identity; static Camera aplicada;

        public static void Iniciar(float f, float duracion)
        {
            fuerza = Mathf.Max(Time.time < hasta ? fuerza : 0f, f); dur = duracion; hasta = Time.time + duracion;
            if (enganchado) return;
            Camera.onPreCull += Antes; Camera.onPostRender += Despues; enganchado = true;
        }

        static void Antes(Camera c)
        {
            if (c != Camera.main || Time.time >= hasta || aplicada != null) return;
            float k = (hasta - Time.time) / dur * fuerza;
            offPos = Random.insideUnitSphere * 0.15f * k;
            offRot = Quaternion.Euler(Random.Range(-1.6f, 1.6f) * k, Random.Range(-1.6f, 1.6f) * k, Random.Range(-2.2f, 2.2f) * k);
            c.transform.position += offPos; c.transform.rotation = c.transform.rotation * offRot; aplicada = c;
        }

        static void Despues(Camera c)
        {
            if (c != aplicada) return;
            c.transform.rotation = c.transform.rotation * Quaternion.Inverse(offRot); c.transform.position -= offPos; aplicada = null;
        }
    }
}

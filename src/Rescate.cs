// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using UnityEngine;

namespace WitchHuntCoop
{
    /// Red de seguridad contra el vacío (pedido 2026-11-17): si el jugador atraviesa una esquina o un borde sin pared y cae fuera del mapa,
    /// no se cae para siempre sin morir: vuelve a un sitio seguro por el que pasó hace unos segundos.
    public partial class Mundo
    {
        const float LimiteVacio = -80f;                 // por debajo de esta altura absoluta no hay nada del juego (las minas están más arriba)
        readonly Vector3[] seguros = new Vector3[4]; int nSeguros; float proxSeguro, proxRescate;
        public int Rescates;

        void ActualizarRescate()
        {
            if (Time.time < proxRescate) return;
            proxRescate = Time.time + 0.4f;
            var j = plugin.Jugador;
            if (j == null || plugin.Escena != EscenaBosque) { nSeguros = 0; return; }
            var mu = plugin.MuerteActual;
            if (mu != null && mu.Mio != Muerte.Estado.Vivo) return;
            Vector3 p = j.transform.position;
            float h = AlturaEn(p);
            bool cae = p.y < LimiteVacio || (!float.IsNaN(h) && p.y < h - 30f);
            if (cae)
            {
                Vector3 donde = nSeguros > 0 ? seguros[0] : Anclaje.Origen() + LlegadaRel;
                j.transform.position = donde + Vector3.up * 1.5f;
                var rb = j.GetComponent<Rigidbody>(); if (rb != null) rb.velocity = Vector3.zero;
                Rescates++;
                plugin.Log("Rescate: el jugador cayó fuera del mapa (" + (p - Anclaje.Origen()).ToString("F0") + "); vuelve a " + (donde - Anclaje.Origen()).ToString("F0"));
                Viaje.Avisar("Caíste fuera del mapa: vuelves al último sitio seguro.");
                return;
            }
            // sitios seguros: suelo firme, sobre el agua y lejos de caer; se guarda uno cada ~3 s (el más viejo = unos 9 s atrás)
            if (Time.time >= proxSeguro && !float.IsNaN(h) && Mathf.Abs(p.y - h) < 2.5f && p.y > nivelAguaSegura())
            {
                proxSeguro = Time.time + 3f;
                if (nSeguros < seguros.Length) seguros[nSeguros++] = p;
                else { for (int i = 0; i < seguros.Length - 1; i++) seguros[i] = seguros[i + 1]; seguros[seguros.Length - 1] = p; }
            }
        }

        float nivelAguaSegura() { return nivelAgua > -999f ? nivelAgua + 0.5f : 13f; }
    }
}

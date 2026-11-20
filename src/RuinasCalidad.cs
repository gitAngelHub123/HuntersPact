// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using System.Collections.Generic;
using UnityEngine;

namespace WitchHuntCoop
{
    /// Casas en ruinas con calidad (2026-11-18): en vez de cajas de colores, paredes de tablas sueltas con el borde
    /// de arriba roto, tablas quemadas, cimientos y chimenea de piedras, vigas del techo caídas, escombros y ceniza; el establo
    /// con tablas verticales, armazón de vigas, techo de tablas con huecos y fardos de paja. Geometría propia del mod con la
    /// madera de la cabaña del juego y el granito de las minas.
    public partial class Mundo
    {
        Material matTablaVieja, matTablaQuemada, matCeniza;

        void MaterialesRuinas()
        {
            if (matTablaVieja != null) return;
            MaterialesMuebles();
            Material baseM = null;
            foreach (var g in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                if (g.name == "THE_WOOD_CABIN_02") { baseM = Madera(g, "cabinWallsExterior") ?? Madera(g, "cabinWallsInterior") ?? Madera(g, "porchFloor"); break; }
            if (baseM == null) baseM = MatMadera(new Color(0.5f, 0.42f, 0.34f), 1f);
            matTablaVieja = Tinte(baseM, new Color(0.62f, 0.56f, 0.5f));
            matTablaQuemada = Tinte(baseM, new Color(0.12f, 0.1f, 0.09f));
            matCeniza = Liso("coop_ceniza", new Color(0.08f, 0.075f, 0.07f), 0.02f);
        }

        // ---------------- Casa en ruinas ----------------

        /// Una casa quemada y derrumbada: cimiento de piedra, piso de tablas con huecos, paredes de tablas horizontales con el
        /// borde de arriba roto (puerta y ventanas), esquineros, chimenea de piedra medio caída, vigas del techo caídas y escombros.
        void CasaRotaReal(Transform raiz, Vector3 pos, float yaw, System.Random rng)
        {
            MaterialesRuinas();
            var g = new GameObject("casa_rota"); g.transform.SetParent(raiz, false);
            g.transform.position = pos; g.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            Quaternion q = g.transform.rotation;
            float L = 7f + (float)rng.NextDouble() * 3f, A = 6f + (float)rng.NextDouble() * 2f;
            var mv = new Malla(); var mq = new Malla(); var mp = new Malla(); var mc = new Malla();
            System.Func<Vector3, Vector3> W = l => pos + q * l;   // local → mundo
            const float yPiso = 0.42f;

            // cimiento de piedras alrededor
            foreach (var lado in new[] { 0, 1, 2, 3 })
            {
                bool largo = lado % 2 == 0; float len = largo ? L : A;
                Vector3 c = lado == 0 ? new Vector3(0f, 0f, A / 2f) : lado == 1 ? new Vector3(L / 2f, 0f, 0f) : lado == 2 ? new Vector3(0f, 0f, -A / 2f) : new Vector3(-L / 2f, 0f, 0f);
                var qs = q * Quaternion.Euler(0f, lado * 90f, 0f);
                MuroPiedras(mp, W(c) + Vector3.down * 0.35f, qs, rng, -len / 2f - 0.2f, len / 2f + 0.2f, 0f, yPiso + 0.33f, 0.5f);
            }
            // piso de tablas: algunas faltan, otras quemadas o rotas
            for (float x = -L / 2f + 0.25f; x < L / 2f - 0.2f; x += 0.22f)
            {
                if (rng.NextDouble() < 0.18) continue;
                float z0 = -A / 2f + 0.2f, z1 = A / 2f - 0.2f;
                if (rng.NextDouble() < 0.3) { if (rng.NextDouble() < 0.5) z0 += (float)rng.NextDouble() * A * 0.6f; else z1 -= (float)rng.NextDouble() * A * 0.6f; }
                var m = rng.NextDouble() < 0.45 ? mq : mv;
                Bloque(m, W(new Vector3(x, yPiso, (z0 + z1) / 2f)), new Vector3(0.2f, 0.04f, z1 - z0), q * Quaternion.Euler(0f, 0f, ((float)rng.NextDouble() - 0.5f) * 3f));
            }
            // paredes: tablas horizontales hasta un borde roto; puerta en el lado 2 (sur) y ventanas
            float semilla = (float)rng.NextDouble() * 100f;
            for (int lado = 0; lado < 4; lado++)
            {
                bool largo = lado % 2 == 0; float len = largo ? L : A;
                Vector3 c = lado == 0 ? new Vector3(0f, 0f, A / 2f) : lado == 1 ? new Vector3(L / 2f, 0f, 0f) : lado == 2 ? new Vector3(0f, 0f, -A / 2f) : new Vector3(-L / 2f, 0f, 0f);
                var qs = q * Quaternion.Euler(0f, lado * 90f, 0f);
                float alto = lado == 2 ? 0.6f + (float)rng.NextDouble() * 1.2f : 1.2f + (float)rng.NextDouble() * 2.4f;   // una pared casi en el suelo
                float puerta = lado == 2 ? ((float)rng.NextDouble() - 0.5f) * (len - 2f) : 999f;
                float ventana = lado != 2 && rng.NextDouble() < 0.7 ? ((float)rng.NextDouble() - 0.5f) * (len - 2.5f) : 999f;
                const float hT = 0.21f, paso = 0.35f;
                for (float y = yPiso; y < yPiso + 3.6f; y += hT)
                {
                    float x = -len / 2f;
                    while (x < len / 2f)
                    {
                        float x1 = Mathf.Min(len / 2f, x + paso);
                        float xm = (x + x1) / 2f;
                        float borde = yPiso + alto * (0.55f + 0.7f * Mathf.PerlinNoise(semilla + lado * 7f + xm * 0.45f, 0.3f)) - 0.6f * Mathf.Abs(Mathf.PerlinNoise(semilla + xm * 1.3f, lado) - 0.5f);
                        bool hueco = y > borde
                                     || (Mathf.Abs(xm - puerta) < 0.55f && y < yPiso + 2.1f)
                                     || (Mathf.Abs(xm - ventana) < 0.5f && y > yPiso + 1.0f && y < yPiso + 1.9f);
                        if (!hueco)
                        {
                            // tramo de tabla: se junta con los siguientes mientras no haya hueco (tablas largas, no cuadraditos)
                            float xs = x; x = x1;
                            while (x < len / 2f)
                            {
                                float n1 = Mathf.Min(len / 2f, x + paso), nm = (x + n1) / 2f;
                                float b2 = yPiso + alto * (0.55f + 0.7f * Mathf.PerlinNoise(semilla + lado * 7f + nm * 0.45f, 0.3f)) - 0.6f * Mathf.Abs(Mathf.PerlinNoise(semilla + nm * 1.3f, lado) - 0.5f);
                                bool h2 = y > b2 || (Mathf.Abs(nm - puerta) < 0.55f && y < yPiso + 2.1f) || (Mathf.Abs(nm - ventana) < 0.5f && y > yPiso + 1.0f && y < yPiso + 1.9f);
                                if (h2) break;
                                x = n1;
                            }
                            float cerca = Mathf.Clamp01((y - yPiso) / Mathf.Max(0.3f, alto));
                            var m = rng.NextDouble() < 0.25 + 0.6f * cerca ? mq : mv;   // más quemadas arriba
                            var qi = qs * Quaternion.Euler(((float)rng.NextDouble() - 0.5f) * 2f, ((float)rng.NextDouble() - 0.5f) * 1.5f, ((float)rng.NextDouble() - 0.5f) * 2f);
                            Bloque(m, W(c) + qs * new Vector3((xs + x) / 2f, y + hT / 2f, 0f), new Vector3(x - xs, hT - 0.012f, 0.06f), qi);
                        }
                        else x = x1;
                    }
                }
                // marcos de puerta y ventana
                if (puerta < 900f) foreach (var s in new[] { -0.6f, 0.6f }) Bloque(mv, W(c) + qs * new Vector3(puerta + s, yPiso + 1.05f, 0.05f), new Vector3(0.1f, 2.1f, 0.12f), qs);
                if (ventana < 900f) Bloque(mq, W(c) + qs * new Vector3(ventana, yPiso + 0.98f, 0.06f), new Vector3(1.15f, 0.08f, 0.16f), qs);
            }
            // esquineros (algunos partidos)
            foreach (var sx in new[] { -1f, 1f }) foreach (var sz in new[] { -1f, 1f })
            {
                float h = 0.8f + (float)rng.NextDouble() * 2.8f;
                Bloque(mq, W(new Vector3(sx * L / 2f, yPiso + h / 2f, sz * A / 2f)), new Vector3(0.2f, h, 0.2f), q * Quaternion.Euler(((float)rng.NextDouble() - 0.5f) * 4f, 0f, ((float)rng.NextDouble() - 0.5f) * 4f));
            }
            // vigas del techo caídas: apoyadas de una pared al piso, y otras tiradas
            for (int k = 0; k < 4; k++)
            {
                float x = -L / 2f + 1f + k * (L - 2f) / 3f + ((float)rng.NextDouble() - 0.5f) * 0.8f;
                Vector3 a = W(new Vector3(x, yPiso + 1.5f + (float)rng.NextDouble() * 1.2f, A / 2f - 0.15f));
                Vector3 b = W(new Vector3(x + ((float)rng.NextDouble() - 0.5f) * 1.5f, yPiso + 0.1f, -A * 0.1f + (float)rng.NextDouble() * A * 0.3f));
                Palo(mq, a, b, 0.09f, 7);
            }
            for (int k = 0; k < 7; k++)   // tablas del techo caídas por el piso
            {
                Vector3 p = W(new Vector3(((float)rng.NextDouble() - 0.5f) * L * 0.9f, yPiso + 0.06f + k * 0.03f, ((float)rng.NextDouble() - 0.5f) * A * 0.9f));
                Bloque(rng.NextDouble() < 0.6 ? mq : mv, p, new Vector3(1.4f + (float)rng.NextDouble() * 1.8f, 0.035f, 0.24f), q * Quaternion.Euler(((float)rng.NextDouble() - 0.5f) * 14f, (float)rng.NextDouble() * 180f, ((float)rng.NextDouble() - 0.5f) * 14f));
            }
            // chimenea de piedra medio caída en la pared norte, con un montón de piedras al pie
            float xch = ((float)rng.NextDouble() - 0.5f) * (L - 3f);
            var qn = q;
            MuroPiedras(mp, W(new Vector3(xch, yPiso, A / 2f + 0.45f)), qn, rng, -0.65f, 0.65f, 0f, 1.6f + (float)rng.NextDouble() * 2.2f, 0.9f);
            for (int k = 0; k < 14; k++)
            {
                Vector3 p = W(new Vector3(xch + ((float)rng.NextDouble() - 0.5f) * 2.4f, yPiso + 0.08f + (float)rng.NextDouble() * 0.15f, A / 2f - 0.6f - (float)rng.NextDouble() * 1.4f));
                float s = 0.18f + 0.22f * (float)rng.NextDouble();
                Bloque(mp, p, new Vector3(s * 1.4f, s, s), Quaternion.Euler((float)rng.NextDouble() * 40f, (float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 40f), 0.6f);
            }
            // ceniza: manchas oscuras sobre el piso
            for (int k = 0; k < 3; k++)
                Charco(mc, W(new Vector3(((float)rng.NextDouble() - 0.5f) * L * 0.6f, yPiso + 0.03f, ((float)rng.NextDouble() - 0.5f) * A * 0.6f)), 0.8f + (float)rng.NextDouble() * 1.2f, rng);
            mv.CrearMundo("coop_ruina_tablas", g.transform, matTablaVieja, true);
            mq.CrearMundo("coop_ruina_quemadas", g.transform, matTablaQuemada, true);
            mp.CrearMundo("coop_ruina_piedras", g.transform, matTunel, true);
            mc.CrearMundo("coop_ruina_ceniza", g.transform, matCeniza, false);
        }
    }
}

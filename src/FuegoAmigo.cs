// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using System.Collections.Generic;
using UnityEngine;

namespace WitchHuntCoop
{
    /// Fuego amigo (pedido 2026-11-10): se elige al crear la partida nueva. Con él activado, los disparos de un jugador
    /// pueden herir al compañero. Cada juego detecta SUS disparos (baja la munición del cargador), traza el rayo (uno por
    /// perdigón) y, si el primer obstáculo es el cuerpo del compañero, le manda el daño; el otro juego se lo aplica.
    public static class FuegoAmigo
    {
        static WeaponBehavior armaVista; static int balasVistas = -1;
        /// Otro módulo cambió la munición del arma a propósito (no es un disparo nuevo).
        public static void Ajustar(WeaponBehavior wb, int balas) { if (armaVista == wb) balasVistas = balas; }

        const string NombreColision = "coop_ff_colisionador";

        public static void Actualizar(Plugin plugin)
        {
            var c = plugin.CompaneroObj;
            bool activo = Opciones.FuegoAmigo;
            if (c != null && c.Cuerpo != null) AsegurarColisionador(c.Cuerpo, activo);
            if (!activo || c == null || c.Cuerpo == null || !c.Cuerpo.activeInHierarchy || plugin.Jugador == null || Camera.main == null) { armaVista = null; return; }
            var pw = Rendimiento.Armas;
            if (pw == null || pw.weaponOrder == null || pw.currentWeapon < 0 || pw.currentWeapon >= pw.weaponOrder.Length || pw.weaponOrder[pw.currentWeapon] == null) { armaVista = null; return; }
            var wb = pw.weaponOrder[pw.currentWeapon].GetComponent<WeaponBehavior>();
            if (wb == null) { armaVista = null; return; }
            if (wb != armaVista) { armaVista = wb; balasVistas = wb.bulletsLeft; return; }
            if (wb.bulletsLeft < balasVistas && wb.bulletsPerClip > 0) Disparo(plugin, wb, c);
            balasVistas = wb.bulletsLeft;
        }

        /// Un colisionador en el cuerpo del compañero (disparador, para no empujar a nadie); solo existe con el fuego amigo.
        static void AsegurarColisionador(GameObject cuerpo, bool activo)
        {
            var t = cuerpo.transform.Find(NombreColision);
            if (t == null)
            {
                var g = new GameObject(NombreColision); g.transform.SetParent(cuerpo.transform, false);
                var cc = g.AddComponent<CapsuleCollider>(); cc.isTrigger = true; cc.center = new Vector3(0f, 0.9f, 0f); cc.radius = 0.35f; cc.height = 1.8f;
                t = g.transform;
            }
            var col = t.GetComponent<Collider>(); if (col != null && col.enabled != activo) col.enabled = activo;
        }

        static void Disparo(Plugin plugin, WeaponBehavior wb, Companero c)
        {
            var cam = Camera.main.transform;
            int rayos = Mathf.Max(1, wb.projectileCount);
            if (wb.gameObject.name == Escopeta.Nombre) rayos = (Escopeta.Doble && balasVistas >= 2) ? Escopeta.Perdigones : Escopeta.Perdigones / 2;   // el modo de disparo ya cambió al contar
            float alcance = wb.range > 0.5f ? wb.range : 100f;
            // daño por impacto: un disparo de bala ronda el 40 % de su daño contra enemigos; un perdigón, el 60 %
            float porImpacto = Mathf.Clamp(wb.damage * (rayos > 1 ? 0.6f : 0.4f), 4f, 45f);
            float total = 0f; int aciertos = 0;
            for (int k = 0; k < rayos; k++)
            {
                Vector3 dir = cam.forward;
                if (rayos > 1 || wb.shotSpread > 0f) dir = (dir + Random.insideUnitSphere * wb.shotSpread).normalized;
                if (!ImpactaAlCompanero(cam.position, dir, alcance, c.Cuerpo, plugin.Jugador.transform)) continue;
                total += porImpacto; aciertos++;
            }
            if (aciertos == 0) return;
            plugin.Log("Fuego amigo: " + aciertos + " impactos en " + plugin.NombreCompanero + ", " + total.ToString("0") + " de daño");
            var datos = Red.Armar(w => w.Write(total));
            for (int i = 0; i < 3; i++) plugin.EnviarFuegoAmigo(datos);
            Viaje.Avisar("¡Le diste a " + plugin.NombreCompanero + "!");
        }

        /// ¿El primer obstáculo del rayo es el cuerpo del compañero? (se ignora al propio jugador y a los disparadores)
        static bool ImpactaAlCompanero(Vector3 origen, Vector3 dir, float alcance, GameObject cuerpo, Transform propio)
        {
            var hits = Physics.RaycastAll(new Ray(origen, dir), alcance, ~0, QueryTriggerInteraction.Collide);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var h in hits)
            {
                if (h.collider == null) continue;
                if (h.collider.transform.IsChildOf(cuerpo.transform)) return true;
                if (h.collider.transform.IsChildOf(propio) || h.collider.transform.root == propio.root) continue;
                if (h.collider.isTrigger) continue;
                return false;   // algo sólido por delante
            }
            return false;
        }

        /// Estado para la orden 'fuegoamigo' (2026-11-20).
        public static string Informe(Plugin plugin)
        {
            var c = plugin.CompaneroObj;
            string txt = "fuego amigo " + (Opciones.FuegoAmigo ? "ACTIVO" : "apagado") + (plugin.EsAnfitrion ? " (anfitrión)" : " (invitado)");
            if (c == null || c.Cuerpo == null) return txt + " | sin cuerpo del compañero";
            var t = c.Cuerpo.transform.Find(NombreColision); var col = t != null ? t.GetComponent<Collider>() : null;
            txt += " | cuerpo activo=" + c.Cuerpo.activeInHierarchy + " escala=" + c.Cuerpo.transform.lossyScale + " pos=" + c.Cuerpo.transform.position;
            txt += col == null ? " | SIN colisionador" : " | colisionador enabled=" + col.enabled + " capa=" + t.gameObject.layer + " caja=" + col.bounds.center + " tam=" + col.bounds.size;
            if (armaVista != null) txt += " | arma=" + armaVista.gameObject.name + " balas=" + armaVista.bulletsLeft + "/" + armaVista.bulletsPerClip + " daño=" + armaVista.damage + " alcance=" + armaVista.range;
            else txt += " | sin arma vigilada";
            return txt;
        }

        static float ultimoAviso;

        /// Me llegó un disparo del compañero.
        public static void Recibir(Plugin plugin, System.IO.BinaryReader r)
        {
            float d = r.ReadSingle();
            if (!Opciones.FuegoAmigo || plugin.Jugador == null || plugin.Jugador.GodMode) return;
            if (Time.realtimeSinceStartup - ultimoAviso < 0.15f) return;   // UDP: el mensaje llega repetido
            ultimoAviso = Time.realtimeSinceStartup;
            plugin.Jugador.ApplyDamage(Mathf.Clamp(d, 0f, 150f));
            plugin.Log("Fuego amigo: recibí " + d.ToString("0") + " de daño de " + plugin.NombreCompanero);
        }
    }
}

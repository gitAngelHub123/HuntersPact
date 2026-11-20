// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using System.Collections.Generic;
using UnityEngine;

namespace WitchHuntCoop
{
    /// Mapa de ocupación: la huella real (centro y radio en el suelo) de todo lo que el mundo procedural
    /// ya colocó: lugares únicos, casas de la escopeta, cabañas cerradas, bocas de mina, relleno, puestos
    /// y piedras de guardado. Lo usan el relleno, los puestos, las cabañas y los árboles para no
    /// meterse unos dentro de otros.
    public partial class Mundo
    {
        /// Objetos del relleno que se movieron (los originales; las copias están en lugaresCopias).
        readonly List<Transform> rellenoMovido = new List<Transform>();
        public int ArbolesQuitados;

        /// Huella de un objeto: caja de lo que se dibuja (solo partes encendidas y razonables).
        static bool Huella(GameObject g, out Vector4 h)
        {
            h = Vector4.zero;
            if (g == null || !g.activeInHierarchy) return false;
            Bounds b = new Bounds(); bool hay = false;
            foreach (var r in g.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled || r is ParticleSystemRenderer || r is LineRenderer || r is TrailRenderer) continue;
                var rb = r.bounds;
                if (rb.size.x > 80f || rb.size.z > 80f) continue;   // mallas combinadas de la escena: no son de esta pieza
                // partes rezagadas en la posición original del objeto copiado (no son de esta pieza)
                Vector3 dc = rb.center - g.transform.position; dc.y = 0f;
                if (dc.sqrMagnitude > 40f * 40f) continue;
                if (!hay) { b = rb; hay = true; } else b.Encapsulate(rb);
            }
            if (!hay) return false;
            h = new Vector4(b.center.x, b.center.z, Mathf.Max(0.4f, Mathf.Max(b.extents.x, b.extents.z)), 0f);
            return true;
        }

        /// Agrega la huella de un objeto; si es demasiado grande para ser una sola estructura (un grupo de
        /// piezas repartidas), agrega la de cada pieza.
        static void Agregar(GameObject g, List<Vector4> l, int nivel = 0)
        {
            Vector4 h;
            if (!Huella(g, out h)) return;
            if (h.z <= 35f || nivel >= 2 || g.transform.childCount == 0) { if (h.z <= 35f) l.Add(h); return; }
            foreach (Transform hijo in g.transform) Agregar(hijo.gameObject, l, nivel + 1);
        }

        /// Todas las huellas de lo colocado hasta ahora.
        List<Vector4> Huellas()
        {
            var l = new List<Vector4>();
            Vector4 h;
            foreach (var c in lugaresCopias)
            {
                if (c == null) continue;
                // un puesto o un grupo grande: cada pieza por separado (si no, el círculo tapa todo el claro)
                if (c.name.StartsWith("coop_puesto_") && c.transform.childCount > 0)
                {
                    foreach (Transform hijo in c.transform) Agregar(hijo.gameObject, l);
                    continue;
                }
                Agregar(c, l);
            }
            foreach (var t in enLugares) if (t != null) Agregar(t.gameObject, l);
            // los lugares únicos del juego que se quedaron fijos (cuevas, cañón, círculo de piedras, torre...)
            foreach (var g in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                if (g != null && System.Array.IndexOf(Unicos, Catalogo.BaseDe(g.name)) >= 0) Agregar(g, l);
            foreach (var t in rellenoMovido) if (t != null) Agregar(t.gameObject, l);
            foreach (var p in piedras) if (p != null) Agregar(p, l);
            foreach (var g in clones)
            {
                if (g == null || !g.name.StartsWith("coop_puesto_")) continue;   // (en "clones" también están los guardianes)
                foreach (Transform hijo in g.transform) Agregar(hijo.gameObject, l);
            }
            return l;
        }

        public string DescribirHuellas()
        {
            var l = new List<KeyValuePair<float, string>>();
            Vector4 h;
            foreach (var c in lugaresCopias) if (c != null && Huella(c, out h)) l.Add(new KeyValuePair<float, string>(h.z, c.name));
            foreach (var t in enLugares) if (t != null && Huella(t.gameObject, out h)) l.Add(new KeyValuePair<float, string>(h.z, "lugar " + t.name));
            foreach (var t in rellenoMovido) if (t != null && Huella(t.gameObject, out h)) l.Add(new KeyValuePair<float, string>(h.z, "relleno " + t.name));
            foreach (var g in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                if (g != null && System.Array.IndexOf(Unicos, Catalogo.BaseDe(g.name)) >= 0 && Huella(g, out h)) l.Add(new KeyValuePair<float, string>(h.z, "único " + g.name));
            l.Sort((a, b) => b.Key.CompareTo(a.Key));
            var sb = new System.Text.StringBuilder(l.Count + " huellas; mayores: ");
            for (int i = 0; i < l.Count && i < 25; i++) sb.Append(l[i].Value).Append('=').Append(l[i].Key.ToString("0")).Append("m; ");
            return sb.ToString();
        }

        /// Rendimiento: todo lo que colocó el mundo y ya no se mueve (relleno, casas, cabañas, bocas de mina,
        /// puestos) se combina en lotes estáticos: la tarjeta de video lo dibuja en muchas menos llamadas.
        /// Se dejan fuera lo que se mueve o se anima (puertas, cadáveres, armas sueltas, túneles que se apagan).
        void CombinarLotes()
        {
            var objs = new List<GameObject>();
            System.Action<GameObject> agregar = null;
            agregar = g =>
            {
                if (g == null || !g.activeInHierarchy) return;
                string n = g.name;
                if (n.StartsWith("coop_puerta") || n.StartsWith("coop_mina_red") || n.Contains("cadaver") || n.Contains("minero") || n.Contains("escopeta_arma") || n.Contains("ballesta")) return;
                bool tieneMovil = g.GetComponent<Animator>() != null || g.GetComponent<SkinnedMeshRenderer>() != null || g.GetComponent<Light>() != null;
                // si algún hijo se mueve (puerta), se combinan los hijos por separado
                bool hijoMovil = false;
                foreach (Transform h in g.transform) if (h.name.StartsWith("coop_puerta")) { hijoMovil = true; break; }
                if (hijoMovil) { foreach (Transform h in g.transform) agregar(h.gameObject); return; }
                if (!tieneMovil && g.GetComponentInChildren<MeshFilter>() != null) objs.Add(g);
            };
            foreach (var c in lugaresCopias) agregar(c);
            foreach (var g in clones) if (g != null && g.name.StartsWith("coop_puesto_")) foreach (Transform h in g.transform) if (h.GetComponent<Light>() == null) agregar(h.gameObject);
            if (objs.Count == 0) return;
            int antes = 0;
            foreach (var g in objs) foreach (var r in g.GetComponentsInChildren<MeshRenderer>()) if (r.enabled) antes++;
            var raiz = new GameObject("coop_lotes");
            StaticBatchingUtility.Combine(objs.ToArray(), raiz);
            lugaresCopias.Add(raiz);
            plugin.Log("Lotes: " + objs.Count + " objetos (" + antes + " piezas dibujadas) combinados en lotes estáticos");
        }

        /// ¿Está libre el sitio de cualquier cosa física del juego (rocas, troncos, cercas...), aparte del suelo?
        static bool Despejado(Vector3 sitio, float radio)
        {
            foreach (var c in Physics.OverlapSphere(sitio + Vector3.up * (radio * 0.5f + 0.5f), radio, ~0, QueryTriggerInteraction.Ignore))
            {
                if (c == null || c is TerrainCollider) continue;
                if (c.GetComponentInParent<FPSPlayer>() != null) continue;
                if (c.attachedRigidbody != null && !c.attachedRigidbody.isKinematic) continue;   // cosas sueltas que se mueven
                return false;
            }
            return true;
        }

        public void OrdenArboles() { MoverArboles(Semilla); }

        static bool Choca(List<Vector4> huellas, float x, float z, float radio, float margen)
        {
            foreach (var o in huellas)
            {
                float dx = o.x - x, dz = o.y - z, r = o.z + radio + margen;
                if (dx * dx + dz * dz < r * r) return true;
            }
            return false;
        }
    }
}

// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using System.Collections.Generic;
using UnityEngine;

namespace WitchHuntCoop
{
    /// Luces que alumbran "de la nada" (pedido 2026-11-17): cuando el mod mueve objetos del juego, a veces una luz se queda sin la
    /// fogata, el farol o la piedra que la explicaba. Al terminar de armar el mundo se buscan las luces cerca del suelo sin ninguna malla
    /// a su lado; cada una se apaga y en su lugar queda un campamento: fogata (con su propia luz), piedras alrededor del fuego, troncos
    /// para sentarse, una caja, un barril, un petate y algo de oro. Todo sale de la posición, así que es igual en los dos juegos.
    public partial class Mundo
    {
        static readonly string[] OroCampamento = { "Gold_Nugget", "Gold_Nugget", "Sack_of_coins" };

        void CampamentosEnLucesSueltas()
        {
            // mallas que pueden explicar una luz (las enormes no: agua, telones, cielo, niebla)
            var fuentes = new List<Bounds>();
            foreach (var r in Object.FindObjectsOfType<Renderer>())
            {
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                var b = r.bounds;
                if (Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z)) > 30f) continue;
                fuentes.Add(b);
            }
            int sueltas = 0, campamentos = 0;
            var hechos = new List<Vector3>();
            foreach (var l in Object.FindObjectsOfType<Light>())
            {
                if (l == null || !l.enabled || !l.gameObject.activeInHierarchy || l.type == LightType.Directional || l.intensity <= 0.01f || l.range < 1f) continue;
                if (l.GetComponentInParent<FPSPlayer>() != null || l.GetComponentInParent<Camera>() != null || l.GetComponentInParent<TakeDamage>() != null) continue;
                if (l.GetComponentInParent<Rigidbody>() != null || l.GetComponentInParent<UnityEngine.AI.NavMeshAgent>() != null) continue;
                if (l.name == "coop_espectro") continue;   // espectros del laberinto y del castillo: llevan su propio orbe visible
                // las luces que pone el mod (castillo, laberinto, minas, cabañas, puestos) ya llevan su farol o su fogata: el castillo está
                // dibujado en lotes grandes y aquí no se vería su antorcha. Solo se revisan las luces del juego que quedaron sueltas al mover cosas.
                bool delMod = false;
                for (Transform t = l.transform; t != null; t = t.parent) if (t.name.StartsWith("coop_") || t.name == "Coop Terreno Norte" || t.name.StartsWith("WitchHuntCoop")) { delMod = true; break; }
                if (delMod) continue;
                Vector3 p = l.transform.position;
                float suelo = AlturaEn(p);
                if (float.IsNaN(suelo) || p.y - suelo > 4f || p.y < suelo - 1.5f) continue;   // solo luces junto al suelo del bosque
                bool tiene = false;
                foreach (var b in fuentes) if (b.SqrDistance(p) < 2.5f * 2.5f) { tiene = true; break; }
                if (tiene) continue;
                sueltas++;
                l.enabled = false;
                // una luz suelta muy cerca de otra ya resuelta comparte campamento
                bool cerca = false;
                foreach (var h in hechos) if ((h - p).sqrMagnitude < 12f * 12f) { cerca = true; break; }
                if (cerca) continue;
                Vector3 base0 = new Vector3(p.x, suelo, p.z);
                RaycastHit hit;
                if (Physics.Raycast(p + Vector3.up * 2f, Vector3.down, out hit, 8f, ~0, QueryTriggerInteraction.Ignore) && hit.collider.GetComponentInParent<FPSPlayer>() == null) base0 = hit.point;
                try { Campamento(base0); campamentos++; hechos.Add(p); }
                catch (System.Exception e) { plugin.Log("Luces: error al armar un campamento " + e.Message); }
            }
            plugin.Log("Luces: " + sueltas + " luces sin nada que las explique → " + campamentos + " campamentos con fogata, objetos y oro");
        }

        void Campamento(Vector3 c)
        {
            var rng = new System.Random(Mathf.RoundToInt(c.x) * 73856093 ^ Mathf.RoundToInt(c.z) * 19349663);
            var raiz = new GameObject("coop_campamento");
            raiz.transform.position = c;
            Fogata.Crear(c + Vector3.up * 0.03f, raiz.transform);
            GameObject cabana = null;
            foreach (var g in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects()) if (g.name == "THE_WOOD_CABIN_02") { cabana = g; break; }
            Material mad = cabana != null ? (Madera(cabana, "porchFloor") ?? Madera(cabana, "cabinWallsExterior")) : null;
            Color tm = new Color(0.34f, 0.22f, 0.12f);
            float yaw0 = (float)rng.NextDouble() * 360f;
            System.Func<float, float, Vector3> anillo = (ang, rad) =>
            {
                Vector3 q = c + Quaternion.Euler(0f, yaw0 + ang, 0f) * Vector3.forward * rad;
                float h = AlturaEn(q); if (!float.IsNaN(h)) q.y = h;
                return q;
            };
            // piedras alrededor del fuego
            var chicas = RocasChicas();
            for (int i = 0; i < 8 && chicas.Count > 0; i++)
            {
                Vector3 q = anillo(i * 45f + (float)rng.NextDouble() * 12f, 0.72f);
                var modelo = chicas[rng.Next(chicas.Count)];
                int f; var piedra = Coop.Piezas.Clonar(modelo, q, Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f) * modelo.transform.rotation, out f);
                if (piedra == null) continue;
                Bounds bb;
                if (!CajaDe(new List<GameObject> { piedra }, out bb)) { Object.Destroy(piedra); continue; }
                piedra.transform.localScale *= (0.24f + (float)rng.NextDouble() * 0.1f) / Mathf.Max(0.05f, Mathf.Max(bb.size.x, Mathf.Max(bb.size.y, bb.size.z)));
                CajaDe(new List<GameObject> { piedra }, out bb);
                piedra.transform.position += new Vector3(q.x - bb.center.x, q.y - bb.min.y - bb.size.y * 0.35f, q.z - bb.center.z);
                foreach (var col in piedra.GetComponentsInChildren<Collider>()) Object.Destroy(col);
                piedra.name = "coop_campamento_piedra";
                piedra.transform.SetParent(raiz.transform, true);
            }
            // dos troncos para sentarse, mirando al fuego
            for (int i = 0; i < 2; i++)
            {
                float ang = i * 150f + 30f;
                Vector3 q = anillo(ang, 1.9f);
                var tronco = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                tronco.name = "coop_campamento_tronco"; tronco.transform.SetParent(raiz.transform, true);
                tronco.transform.position = q + Vector3.up * 0.18f;
                tronco.transform.rotation = Quaternion.Euler(0f, yaw0 + ang, 90f);
                tronco.transform.localScale = new Vector3(0.36f, 0.75f, 0.36f);
                tronco.GetComponent<Renderer>().sharedMaterial = mad != null ? new Material(mad) { color = tm } : new Material(Shader.Find("Standard")) { color = tm };
            }
            // caja, barril y petate
            Vector3 qc = anillo(250f, 2.6f);
            Caja(raiz.transform, "coop_campamento_caja", qc + Vector3.up * 0.3f, new Vector3(0.6f, 0.6f, 0.6f), Quaternion.Euler(0f, yaw0 + 17f, 0f), mad, tm * 1.1f, true);
            Caja(raiz.transform, "coop_campamento_caja", qc + Vector3.up * 0.8f, new Vector3(0.4f, 0.4f, 0.4f), Quaternion.Euler(0f, yaw0 + 51f, 0f), mad, tm, true);
            var barril = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            barril.name = "coop_campamento_barril"; barril.transform.SetParent(raiz.transform, true);
            barril.transform.position = anillo(285f, 2.7f) + Vector3.up * 0.45f; barril.transform.localScale = new Vector3(0.62f, 0.45f, 0.62f);
            barril.GetComponent<Renderer>().sharedMaterial = mad != null ? new Material(mad) { color = new Color(0.32f, 0.2f, 0.11f) } : new Material(Shader.Find("Standard")) { color = new Color(0.32f, 0.2f, 0.11f) };
            Vector3 qp = anillo(100f, 2.5f);
            var petate = Caja(raiz.transform, "coop_campamento_petate", qp + Vector3.up * 0.05f, new Vector3(0.75f, 0.08f, 1.8f), Quaternion.Euler(0f, yaw0 + 100f, 0f), null, Color.white, false);
            Telar(petate, new Color(0.4f, 0.16f, 0.1f), new Color(0.2f, 0.09f, 0.06f), 2, 6, 0.75f, 1.8f);
            // un poco de oro (con id fijo por la posición: al recogerlo desaparece en los dos juegos)
            int nOro = 2 + rng.Next(3), puestos = 0;
            for (int k = 0; k < nOro; k++)
            {
                Vector3 qo = anillo(40f + k * 47f + (float)rng.NextDouble() * 20f, 1.2f + (float)rng.NextDouble() * 0.6f);
                string id = "campamento:" + Mathf.RoundToInt(c.x) + ":" + Mathf.RoundToInt(c.z) + ":" + k;
                string tipo = OroCampamento[rng.Next(OroCampamento.Length)];
                if (ClonarBotin(tipo, qo, (float)rng.NextDouble() * 360f, id) != null || ClonarBotin(tipo == "Sack_of_coins" ? "Gold_Nugget" : "Sack_of_coins", qo, (float)rng.NextDouble() * 360f, id) != null) puestos++;
            }
            lugaresCopias.Add(raiz);   // los árboles no caen encima
            plugin.Log("Luces: campamento en " + (c - Ancla()).ToString("F0") + " con " + puestos + " de oro");
        }
    }
}

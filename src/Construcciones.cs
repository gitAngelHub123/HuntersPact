// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace WitchHuntCoop
{
    /// Pueblos destruidos con botín y guardianes (pedido 2026-11-10; la granja se quitó del juego el 2026-11-18). Una vez por partida (semilla de
    /// partida), iguales en los dos juegos.
    public partial class Mundo
    {
        static Texture2D texMadera, texHierba, texMetal;
        static readonly Dictionary<int, Material> matsConstruccion = new Dictionary<int, Material>();
        public string ResumenConstrucciones = "";

        // ---------------- Materiales ----------------

        static Material MatMadera(Color c, float repeticiones = 1f)
        {
            int clave = Mathf.RoundToInt(c.r * 255) * 65536 + Mathf.RoundToInt(c.g * 255) * 256 + Mathf.RoundToInt(c.b * 255) + (int)(repeticiones * 1000) * 16777216;
            Material m;
            if (matsConstruccion.TryGetValue(clave, out m) && m != null) return m;
            if (texMadera == null)
            {
                texMadera = new Texture2D(64, 64, TextureFormat.RGB24, true) { wrapMode = TextureWrapMode.Repeat };
                var r = new System.Random(3);
                for (int y = 0; y < 64; y++)
                    for (int x = 0; x < 64; x++)
                    {
                        float tabla = (x % 16 == 0) ? 0.55f : 1f;   // juntas entre tablas
                        float veta = 0.82f + 0.18f * Mathf.Sin(y * 0.55f + x * 0.31f + (float)r.NextDouble());
                        texMadera.SetPixel(x, y, Color.white * (tabla * veta));
                    }
                texMadera.Apply();
            }
            m = new Material(Shader.Find("Standard")) { color = c, mainTexture = texMadera, name = "coop_construccion" };
            m.mainTextureScale = new Vector2(repeticiones, repeticiones);
            m.SetFloat("_Glossiness", 0.08f);
            matsConstruccion[clave] = m;
            return m;
        }

        static Material MatMetal(Color c)
        {
            int clave = -(Mathf.RoundToInt(c.r * 255) * 65536 + Mathf.RoundToInt(c.g * 255) * 256 + Mathf.RoundToInt(c.b * 255)) - 1;
            Material m;
            if (matsConstruccion.TryGetValue(clave, out m) && m != null) return m;
            m = new Material(Shader.Find("Standard")) { color = c, name = "coop_metal" };
            m.SetFloat("_Metallic", 0.6f); m.SetFloat("_Glossiness", 0.35f);
            matsConstruccion[clave] = m;
            return m;
        }

        static GameObject Caja(Transform padre, string nombre, Vector3 centroLocal, Vector3 tam, Material m, Quaternion? rot = null, PrimitiveType tipo = PrimitiveType.Cube)
        {
            var g = GameObject.CreatePrimitive(tipo);
            g.name = nombre;
            g.transform.SetParent(padre, false);
            g.transform.localPosition = centroLocal;
            g.transform.localRotation = rot ?? Quaternion.identity;
            g.transform.localScale = tam;
            g.GetComponent<Renderer>().sharedMaterial = m;
            g.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            return g;
        }

        // ---------------- Búsqueda de sitios ----------------

        /// Un sitio llano (poca diferencia de altura en el rectángulo), con tierra firme y camino hasta él.
        bool SitioLlano(System.Random rng, Vector3 tam, float desnivelMax, Bounds dentro, float lejosCabana, out Vector3 centro)
        {
            centro = Vector3.zero;
            for (int i = 0; i < 500; i++)
            {
                float x = dentro.min.x + tam.x * 0.5f + (float)rng.NextDouble() * (dentro.size.x - tam.x);
                float z = dentro.min.z + tam.z * 0.5f + (float)rng.NextDouble() * (dentro.size.z - tam.z);
                if (Vector3.Distance(new Vector3(x, 0f, z), new Vector3(cabana.x, 0f, cabana.z)) < lejosCabana) continue;
                float mn = float.MaxValue, mx = float.MinValue; bool malo = false;
                for (int a = 0; a <= 4 && !malo; a++)
                    for (int b = 0; b <= 4; b++)
                    {
                        var p = new Vector3(x - tam.x * 0.5f + tam.x * a / 4f, 0f, z - tam.z * 0.5f + tam.z * b / 4f);
                        float h = AlturaEn(p);
                        if (float.IsNaN(h) || h < nivelAgua + 1.5f || EsCamino(p)) { malo = true; break; }
                        mn = Mathf.Min(mn, h); mx = Mathf.Max(mx, h);
                    }
                if (malo || mx - mn > desnivelMax) continue;
                var c = new Vector3(x, (mn + mx) * 0.5f, z);
                NavMeshHit nh;
                if (!NavMesh.SamplePosition(c, out nh, 8f, NavMesh.AllAreas)) continue;
                if (hayLlegada && (!NavMesh.CalculatePath(nhLlegada.position, nh.position, NavMesh.AllAreas, camino) || camino.status != NavMeshPathStatus.PathComplete)) continue;
                centro = c; return true;
            }
            return false;
        }

        // ---------------- Pueblos destruidos ----------------

        public const int PueblosDestruidos = 3;

        void ColocarRuinas(int semilla)
        {
            var rng = new System.Random(semilla ^ Hash("coop_ruinas"));
            var area = zona; area.Expand(new Vector3(-50f, 0f, -50f));
            int hechos = 0;
            for (int p = 0; p < PueblosDestruidos; p++)
            {
                Vector3 centro;
                if (!SitioLlano(rng, new Vector3(70f, 0f, 70f), 5f, area, RadioCabana + 120f, out centro)) { plugin.Log("Ruinas: sin sitio para el pueblo " + (p + 1)); continue; }
                var raiz = new GameObject("coop_pueblo_destruido_" + (p + 1)); raiz.transform.position = centro;
                int casas = 5 + rng.Next(3);
                var rngB = new System.Random(semilla ^ Hash("coop_ruinas_casas_botin_" + p));   // aparte: no cambia el resto del pueblo
                string[] botinCasa = { "Sack_of_coins", "Sack_of_coins", "Gold_Nugget", "Ammo_Silverbullets", "Goldidol_pickable" };
                int conBotin = 0;
                for (int i = 0; i < casas; i++)
                {
                    float a = (float)i / casas * Mathf.PI * 2f + (float)rng.NextDouble() * 0.5f, r = 18f + (float)rng.NextDouble() * 18f;
                    Vector3 pc = centro + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                    float h = AlturaEn(pc); if (float.IsNaN(h) || h < nivelAgua + 1f) continue; pc.y = h;
                    CasaRotaReal(raiz.transform, pc, (float)rng.NextDouble() * 360f, rng);
                    // botín dentro de algunas casas (pedido 2026-11-18): ~45 %, 1 o 2 cosas en el suelo de la casa
                    if (rngB.NextDouble() < 0.45)
                    {
                        int nb = 1 + rngB.Next(2);
                        for (int k = 0; k < nb; k++)
                        {
                            Vector3 pb = pc + new Vector3(((float)rngB.NextDouble() - 0.5f) * 2.4f, 0f, ((float)rngB.NextDouble() - 0.5f) * 2.4f);
                            RaycastHit hb; if (Physics.Raycast(pb + Vector3.up * 2.5f, Vector3.down, out hb, 6f, ~0, QueryTriggerInteraction.Ignore)) pb.y = hb.point.y; else pb.y = AlturaEn(pb);
                            if (ClonarBotin(botinCasa[rngB.Next(botinCasa.Length)], pb, (float)rngB.NextDouble() * 360f, "ruinas:" + p + ":casa" + i + ":" + k) != null) conBotin++;
                        }
                    }
                }
                plugin.Log("Ruinas: pueblo " + (p + 1) + " con " + conBotin + " cosas dentro de las casas");
                // fogata apagada en la plaza, carretas rotas y botín
                var carbon = MatMadera(new Color(0.08f, 0.07f, 0.06f), 1f);
                // fogata grande en la plaza (pedido 2026-11-18: antes un disco oscuro), con anillo de piedras y luz cálida
                Vector3 pf = new Vector3(centro.x, AlturaEn(centro), centro.z);
                Fogata.Crear(pf, raiz.transform, 2.6f);
                MaterialesMuebles();
                var anillo = new Malla();
                for (int k = 0; k < 16; k++)
                {
                    float an = k / 16f * Mathf.PI * 2f, sz = 0.35f + 0.15f * (float)rng.NextDouble();
                    Vector3 ps = pf + new Vector3(Mathf.Cos(an) * 1.9f, 0.12f, Mathf.Sin(an) * 1.9f);
                    Bloque(anillo, ps, new Vector3(sz * 1.3f, sz * 0.8f, sz), Quaternion.Euler((float)rng.NextDouble() * 20f, an * Mathf.Rad2Deg, (float)rng.NextDouble() * 20f), 0.6f);
                }
                anillo.CrearMundo("coop_fogata_piedras", raiz.transform, matTunel, true);
                var lzf = new GameObject("coop_luz_fogata_pueblo"); lzf.transform.SetParent(raiz.transform, true); lzf.transform.position = pf + Vector3.up * 2.2f;
                var lf = lzf.AddComponent<Light>(); lf.type = LightType.Point; lf.color = new Color(1f, 0.55f, 0.22f); lf.range = 22f; lf.intensity = 2.2f; lf.shadows = LightShadows.None;
                lzf.AddComponent<Titilar>();
                for (int k = 0; k < 7; k++)
                {
                    Vector3 pb = centro + new Vector3((float)rng.NextDouble() * 30f - 15f, 0f, (float)rng.NextDouble() * 30f - 15f);
                    float hb = AlturaEn(pb); if (float.IsNaN(hb) || hb < nivelAgua + 0.8f) continue; pb.y = hb;
                    string tipo = k < 3 ? "Sack_of_coins" : (k < 5 ? "Gold_Nugget" : (k == 5 ? "Ammo_Silverbullets" : "Goldidol_pickable"));
                    ClonarBotin(tipo, pb, (float)rng.NextDouble() * 360f, "ruinas:" + p + ":" + k);
                }
                puestos.Add(new Puesto { Pos = centro, Guardian = Guardianes[rng.Next(Guardianes.Length)], CuantosGuardianes = 3 + rng.Next(3), Estructura = raiz });
                ResumenConstrucciones += "pueblo destruido " + (p + 1) + " en " + (centro - Ancla()).ToString("F0") + "; ";
                hechos++;
            }
            plugin.Log("Ruinas: " + hechos + " pueblos destruidos");
        }

        /// Una casa en ruinas: tres paredes con huecos y partes caídas, un trozo de techo hundido y tablas por el suelo.
        void CasaRota(Transform raiz, Vector3 pos, float yaw, System.Random rng)
        {
            var quemada = MatMadera(new Color(0.18f, 0.14f, 0.11f), 3f);
            var vieja = MatMadera(new Color(0.34f, 0.28f, 0.22f), 3f);
            var g = new GameObject("casa_rota"); g.transform.SetParent(raiz, false);
            g.transform.position = pos; g.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            float L = 7f + (float)rng.NextDouble() * 3f, A = 6f + (float)rng.NextDouble() * 2f;
            Caja(g.transform, "base", new Vector3(0f, 0.1f, 0f), new Vector3(L, 0.2f, A), quemada);
            // cada pared con altura distinta (rota) y alguna caída
            float[] altos = { 2.2f + (float)rng.NextDouble() * 1.8f, 1.2f + (float)rng.NextDouble() * 2f, 2.5f + (float)rng.NextDouble() * 1.5f, 0.5f };
            Caja(g.transform, "pared1", new Vector3(0f, altos[0] / 2f, A / 2f), new Vector3(L, altos[0], 0.3f), vieja);
            Caja(g.transform, "pared2", new Vector3(-L / 2f, altos[1] / 2f, 0f), new Vector3(0.3f, altos[1], A), quemada);
            Caja(g.transform, "pared3", new Vector3(L / 2f, altos[2] / 2f, -A * 0.15f), new Vector3(0.3f, altos[2], A * 0.7f), vieja);
            Caja(g.transform, "pared4", new Vector3(0f, altos[3] / 2f, -A / 2f), new Vector3(L, altos[3], 0.3f), quemada);
            // techo hundido: una tabla inclinada apoyada en una pared
            Caja(g.transform, "techo_caido", new Vector3(-L * 0.15f, 1.6f, A * 0.15f), new Vector3(L * 0.55f, 0.2f, A * 0.6f), vieja, Quaternion.Euler(18f + (float)rng.NextDouble() * 15f, (float)rng.NextDouble() * 20f, 8f));
            for (int i = 0; i < 4; i++)
                Caja(g.transform, "tabla", new Vector3(((float)rng.NextDouble() - 0.5f) * L * 1.4f, 0.12f, ((float)rng.NextDouble() - 0.5f) * A * 1.4f), new Vector3(2f + (float)rng.NextDouble() * 1.5f, 0.1f, 0.25f), quemada, Quaternion.Euler(0f, (float)rng.NextDouble() * 180f, (float)rng.NextDouble() * 12f));
        }
    }
}

// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using System.Collections.Generic;
using UnityEngine;

namespace WitchHuntCoop
{
    /// Joyas de la bóveda (pedido 2026-11-18): en vez de las copas, cofres de oro abiertos rebosando monedas, gemas y collares;
    /// lingotes de oro y plata apilados; gemas talladas (talla brillante, facetas planas) de varios colores, diamantes y collares
    /// de eslabones sueltos sobre el manto de oro. Una sonda de reflejos de la sala hace que el metal y las piedras brillen.
    public partial class Mundo
    {
        static Material matPlataJ, matOroJ, matDiamante; static Material[] matGemas;
        static Mesh mallaGema, mallaEslabon;

        static Material MetalJ(Color c, float liso)
        {
            var m = new Material(Shader.Find("Standard")) { color = c };
            m.SetFloat("_Metallic", 1f); m.SetFloat("_Glossiness", liso);
            return m;
        }
        static Material PiedraJ(Color c, float brillo)
        {
            var m = new Material(Shader.Find("Standard")) { color = c };
            m.SetFloat("_Metallic", 0.35f); m.SetFloat("_Glossiness", 0.97f);
            m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", c * brillo);   // algo de luz propia: en la penumbra de la bóveda siguen destellando
            return m;
        }
        static void MaterialesJoyas()
        {
            if (matPlataJ != null) return;
            matPlataJ = MetalJ(new Color(0.86f, 0.87f, 0.9f), 0.9f);
            matOroJ = MetalJ(new Color(1f, 0.76f, 0.3f), 0.86f);
            matDiamante = PiedraJ(new Color(0.88f, 0.94f, 1f), 0.22f);
            matGemas = new[]
            {
                PiedraJ(new Color(0.78f, 0.02f, 0.07f), 0.35f),   // rubí
                PiedraJ(new Color(0.02f, 0.62f, 0.22f), 0.3f),    // esmeralda
                PiedraJ(new Color(0.06f, 0.16f, 0.88f), 0.35f),   // zafiro
                PiedraJ(new Color(0.52f, 0.1f, 0.78f), 0.3f),     // amatista
                PiedraJ(new Color(1f, 0.62f, 0.05f), 0.3f),       // topacio
                PiedraJ(new Color(0.05f, 0.75f, 0.8f), 0.3f),     // aguamarina
            };
            mallaGema = MallaGema(); mallaEslabon = MallaEslabon();
        }

        /// Talla brillante: tabla octogonal arriba, corona, cintura y pabellón que termina en punta. Facetas planas (vértices propios).
        static Mesh MallaGema()
        {
            const int N = 16; float r = 0.5f, rt = 0.3f, yt = 0.17f, yp = -0.43f;
            var v = new List<Vector3>(); var t = new List<int>();
            System.Action<Vector3, Vector3, Vector3> tri = (a, b, c) => { t.Add(v.Count); v.Add(a); t.Add(v.Count); v.Add(b); t.Add(v.Count); v.Add(c); };
            Vector3 cimaC = new Vector3(0f, yt, 0f), punta = new Vector3(0f, yp, 0f);
            for (int i = 0; i < N; i++)
            {
                float a0 = i * Mathf.PI * 2f / N, a1 = (i + 1) * Mathf.PI * 2f / N, am = (a0 + a1) / 2f;
                Vector3 g0 = new Vector3(Mathf.Cos(a0) * r, 0f, Mathf.Sin(a0) * r), g1 = new Vector3(Mathf.Cos(a1) * r, 0f, Mathf.Sin(a1) * r);
                Vector3 t0 = new Vector3(Mathf.Cos(a0) * rt, yt, Mathf.Sin(a0) * rt), t1 = new Vector3(Mathf.Cos(a1) * rt, yt, Mathf.Sin(a1) * rt);
                Vector3 gm = new Vector3(Mathf.Cos(am) * r * 0.82f, -0.12f, Mathf.Sin(am) * r * 0.82f);   // facetas del pabellón partidas: más destellos
                tri(cimaC, t1, t0);                // tabla
                tri(t0, t1, g1); tri(t0, g1, g0);  // corona
                tri(g0, g1, gm); tri(g0, gm, punta); tri(gm, g1, punta);   // pabellón
            }
            var m = new Mesh { name = "coop_gema" }; m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        /// Eslabón: un toro ovalado.
        static Mesh MallaEslabon()
        {
            const int A = 12, B = 6; float R = 1f, rr = 0.28f;
            var v = new List<Vector3>(); var t = new List<int>();
            for (int i = 0; i <= A; i++)
                for (int j = 0; j <= B; j++)
                {
                    float u = i * Mathf.PI * 2f / A, w = j * Mathf.PI * 2f / B;
                    Vector3 c = new Vector3(Mathf.Cos(u) * R * 1.45f, 0f, Mathf.Sin(u) * R);
                    Vector3 n = new Vector3(Mathf.Cos(u), 0f, Mathf.Sin(u));
                    v.Add(c + n * Mathf.Cos(w) * rr + Vector3.up * Mathf.Sin(w) * rr);
                }
            for (int i = 0; i < A; i++)
                for (int j = 0; j < B; j++)
                {
                    int a = i * (B + 1) + j, b = (i + 1) * (B + 1) + j;
                    t.Add(a); t.Add(b); t.Add(a + 1); t.Add(b); t.Add(b + 1); t.Add(a + 1);
                }
            var m = new Mesh { name = "coop_eslabon" }; m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        static GameObject Pieza(Transform padre, string nombre, Mesh malla, Material mat, Vector3 pos, Quaternion rot, Vector3 esc)
        {
            var g = new GameObject(nombre); g.transform.SetParent(padre, false);
            g.transform.localPosition = pos; g.transform.localRotation = rot; g.transform.localScale = esc;
            g.AddComponent<MeshFilter>().sharedMesh = malla; var r = g.AddComponent<MeshRenderer>(); r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            g.AddComponent<NoSolido>();
            return g;
        }
        static GameObject Caja(Transform padre, string nombre, Material mat, Vector3 pos, Vector3 tam, Quaternion rot)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube); g.name = nombre;
            Object.DestroyImmediate(g.GetComponent<Collider>());   // al instante: con Destroy seguía chocando este cuadro y el cofre se apoyaba sobre sí mismo (flotaba)
            g.transform.SetParent(padre, false); g.transform.localPosition = pos; g.transform.localRotation = rot; g.transform.localScale = tam;
            g.GetComponent<Renderer>().sharedMaterial = mat; g.AddComponent<NoSolido>();
            return g;
        }

        static void Gema(Transform padre, Vector3 pos, float tam, Material mat, System.Random rng)
        {
            var rot = Quaternion.Euler(((float)rng.NextDouble() - 0.5f) * 70f, (float)rng.NextDouble() * 360f, ((float)rng.NextDouble() - 0.5f) * 70f);
            Pieza(padre, "coop_gema", mallaGema, mat, pos, rot, Vector3.one * tam);
        }

        /// Collar: eslabones que cuelgan entre dos puntos (catenaria) con un colgante de gema al medio.
        static void Collar(Transform padre, Vector3 a, Vector3 b, float caida, Material metal, Material gema, System.Random rng)
        {
            float largo = Vector3.Distance(a, b) + caida * 1.6f; int n = Mathf.Clamp(Mathf.RoundToInt(largo / 0.028f), 12, 90);
            Vector3 prev = a; float giro = 0f;
            for (int i = 1; i <= n; i++)
            {
                float u = i / (float)n;
                Vector3 p = Vector3.Lerp(a, b, u) + Vector3.down * caida * 4f * u * (1f - u);
                Vector3 dir = (p - prev).normalized; if (dir.sqrMagnitude < 1e-6f) dir = Vector3.forward;
                giro += 90f;   // cada eslabón cruzado con el anterior, como una cadena de verdad
                Pieza(padre, "coop_eslabon", mallaEslabon, metal, (p + prev) / 2f, Quaternion.LookRotation(dir) * Quaternion.Euler(0f, 0f, giro) * Quaternion.Euler(0f, 90f, 0f), Vector3.one * 0.011f);
                prev = p;
            }
            Vector3 medio = Vector3.Lerp(a, b, 0.5f) + Vector3.down * caida;
            Pieza(padre, "coop_colgante", mallaGema, gema, medio + Vector3.down * 0.035f, Quaternion.Euler(180f, (float)rng.NextDouble() * 360f, 0f), Vector3.one * 0.07f);
            Pieza(padre, "coop_engarce", mallaEslabon, metal, medio, Quaternion.Euler(90f, 0f, 0f), Vector3.one * 0.016f);
        }

        /// Cofre de oro abierto, con bandas de plata, tapa curva levantada y rebosante de monedas, gemas, diamantes y un collar colgando.
        void CofreTesoro(Transform pa, Vector3 pos, float yaw, Material oroMonedas, System.Random rng)
        {
            var c = new GameObject("coop_boveda_cofre"); c.transform.SetParent(pa, false);
            c.transform.localPosition = pos; c.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var t = c.transform; float W = 0.9f, D = 0.55f, H = 0.42f, g = 0.035f;
            // cuerpo: cuatro paredes y fondo (hueco, se ve lo que hay dentro)
            Caja(t, "pared", matOroJ, new Vector3(0f, H / 2f, -D / 2f + g / 2f), new Vector3(W, H, g), Quaternion.identity);
            Caja(t, "pared", matOroJ, new Vector3(0f, H / 2f, D / 2f - g / 2f), new Vector3(W, H, g), Quaternion.identity);
            Caja(t, "pared", matOroJ, new Vector3(-W / 2f + g / 2f, H / 2f, 0f), new Vector3(g, H, D), Quaternion.identity);
            Caja(t, "pared", matOroJ, new Vector3(W / 2f - g / 2f, H / 2f, 0f), new Vector3(g, H, D), Quaternion.identity);
            Caja(t, "fondo", matOroJ, new Vector3(0f, 0.02f, 0f), new Vector3(W, 0.04f, D), Quaternion.identity);
            // bandas de plata, esquineras y cerradura
            foreach (float x in new[] { -W * 0.3f, W * 0.3f })
            {
                Caja(t, "banda", matPlataJ, new Vector3(x, H / 2f, -D / 2f - 0.004f), new Vector3(0.07f, H + 0.01f, 0.012f), Quaternion.identity);
                Caja(t, "banda", matPlataJ, new Vector3(x, H / 2f, D / 2f + 0.004f), new Vector3(0.07f, H + 0.01f, 0.012f), Quaternion.identity);
            }
            Caja(t, "borde", matPlataJ, new Vector3(0f, H, 0f), new Vector3(W + 0.02f, 0.03f, D + 0.02f), Quaternion.identity);
            Caja(t, "borde", matPlataJ, new Vector3(0f, 0.025f, 0f), new Vector3(W + 0.03f, 0.05f, D + 0.03f), Quaternion.identity);
            Caja(t, "cerradura", matPlataJ, new Vector3(0f, H - 0.08f, -D / 2f - 0.012f), new Vector3(0.1f, 0.12f, 0.02f), Quaternion.identity);
            Gema(t, new Vector3(0f, H - 0.08f, -D / 2f - 0.03f), 0.05f, matGemas[0], rng);
            // tapa curva (medio cilindro de tablas de oro) abierta hacia atrás, con sus bandas de plata
            var bisagra = new GameObject("tapa"); bisagra.transform.SetParent(t, false);
            bisagra.transform.localPosition = new Vector3(0f, H, D / 2f); bisagra.transform.localRotation = Quaternion.Euler(-105f, 0f, 0f);
            const int S = 9; float rT = D / 2f;
            for (int i = 0; i < S; i++)
            {
                float a0 = i * Mathf.PI / S, a1 = (i + 1) * Mathf.PI / S, am = (a0 + a1) / 2f;
                Vector3 pc = new Vector3(0f, Mathf.Sin(am) * rT, -rT + Mathf.Cos(am) * rT);
                float ancho = 2f * rT * Mathf.Sin((a1 - a0) / 2f) + 0.004f;
                var rot = Quaternion.Euler(-am * Mathf.Rad2Deg, 0f, 0f);
                Caja(bisagra.transform, "tabla_tapa", matOroJ, pc, new Vector3(W, g, ancho), rot);
                foreach (float x in new[] { -W * 0.3f, W * 0.3f }) Caja(bisagra.transform, "banda_tapa", matPlataJ, pc + rot * Vector3.up * 0.012f, new Vector3(0.07f, 0.012f, ancho + 0.002f), rot);
            }
            // relleno: monedas (montaña de oro chata), gemas y diamantes encima, un collar que cuelga por el borde
            var monton = new GameObject("monedas"); monton.transform.SetParent(t, false); monton.transform.localPosition = new Vector3(0f, H - 0.12f, 0f);
            monton.transform.localScale = new Vector3(1f, 1f, 0.62f);
            MontanaOro(monton.transform, oroMonedas, 0.42f, 0.2f, rng.Next());
            foreach (var mf in monton.GetComponentsInChildren<MeshFilter>()) mf.gameObject.AddComponent<NoSolido>();
            int ng = 7 + rng.Next(6);
            for (int k = 0; k < ng; k++)
            {
                float x = ((float)rng.NextDouble() - 0.5f) * (W - 0.2f), z = ((float)rng.NextDouble() - 0.5f) * (D - 0.2f);
                float y = H + 0.06f * (1f - Mathf.Abs(x) / W) + 0.02f;
                bool diam = rng.NextDouble() < 0.3;
                Gema(t, new Vector3(x, y, z), diam ? 0.05f + 0.03f * (float)rng.NextDouble() : 0.06f + 0.05f * (float)rng.NextDouble(), diam ? matDiamante : matGemas[rng.Next(matGemas.Length)], rng);
            }
            float lado = rng.NextDouble() < 0.5 ? -1f : 1f;
            Collar(t, new Vector3(lado * W * 0.15f, H + 0.05f, -D * 0.1f), new Vector3(lado * W * 0.45f, H - 0.02f, -D / 2f - 0.03f), 0.12f, rng.NextDouble() < 0.5 ? matOroJ : matPlataJ, matGemas[rng.Next(matGemas.Length)], rng);
            Collar(t, new Vector3(-lado * W * 0.2f, H + 0.04f, D * 0.1f), new Vector3(-lado * W / 2f - 0.02f, H - 0.03f, D * 0.15f), 0.09f, matPlataJ, matDiamante, rng);
        }

        /// Pila de lingotes (de oro o de plata) en capas cruzadas.
        void Lingotes(Transform pa, Vector3 pos, float yaw, bool plata, System.Random rng)
        {
            var p = new GameObject(plata ? "coop_boveda_lingotes_plata" : "coop_boveda_lingotes_oro"); p.transform.SetParent(pa, false);
            p.transform.localPosition = pos; p.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var mat = plata ? matPlataJ : matOroJ;
            float L = 0.3f, A = 0.12f, Al = 0.07f; int capas = 3 + rng.Next(3);
            for (int c = 0; c < capas; c++)
            {
                int n = 4 - c / 2; bool cruz = c % 2 == 1;
                for (int i = 0; i < n; i++)
                {
                    float o = (i - (n - 1) / 2f) * (A + 0.012f);
                    Vector3 lp = cruz ? new Vector3(0f, Al / 2f + c * Al, o) : new Vector3(o, Al / 2f + c * Al, 0f);
                    var rot = Quaternion.Euler(0f, cruz ? 90f : 0f, 0f);
                    Caja(p.transform, "lingote", mat, lp, new Vector3(A, Al, L), rot);
                    Caja(p.transform, "lingote_cima", mat, lp + Vector3.up * (Al / 2f + 0.006f), new Vector3(A * 0.82f, 0.012f, L * 0.86f), rot);   // tapa biselada
                }
            }
        }

        /// Sube (o baja) la pieza hasta que su base quede apoyada sobre el manto de oro, que tiene relieve de hasta un metro
        /// (sin esto los cofres quedaban hundidos en el oro y no se veían).
        static int rechazos;
        static bool SobreOro(GameObject g, float hundir)
        {
            Bounds b = new Bounds(); bool hay = false;
            foreach (var r in g.GetComponentsInChildren<Renderer>()) { if (!hay) { b = r.bounds; hay = true; } else b.Encapsulate(r.bounds); }
            if (!hay) return true;
            float mejor = float.NegativeInfinity; bool enManto = true; RaycastHit h; string choques = "";
            foreach (var d in new[] { Vector2.zero, new Vector2(-0.4f, -0.4f), new Vector2(0.4f, -0.4f), new Vector2(-0.4f, 0.4f), new Vector2(0.4f, 0.4f) })
            {
                var o = new Vector3(b.center.x + b.extents.x * d.x, b.max.y + 3f, b.center.z + b.extents.z * d.y);
                if (Physics.Raycast(o, Vector3.down, out h, 8f, ~(1 << 2), QueryTriggerInteraction.Ignore))
                {
                    mejor = Mathf.Max(mejor, h.point.y);
                    choques += h.collider.name + "@" + h.point.y.ToString("0.0") + " ";
                    string n = h.collider.name.ToLowerInvariant(); if (!n.Contains("manto") && !n.Contains("oro") && !n.Contains("piso")) enManto = false;
                }
                else enManto = false;
            }
            float dy = float.IsNegativeInfinity(mejor) ? 99f : mejor - b.min.y - hundir;
            // fuera del manto de oro (o tendría que subir/bajar mucho): no se pone (2026-11-18: un cofre quedaba en el aire)
            if (!enManto || Mathf.Abs(dy) > 1.2f)
            {
                if (rechazos++ < 6) Escopeta.plugin.Log("Castillo: " + g.name + " rechazado en " + b.center.ToString("F1") + " (base " + b.min.y.ToString("0.0") + ", dy " + dy.ToString("0.0") + "): " + choques);
                Object.DestroyImmediate(g); return false;
            }
            g.transform.position += Vector3.up * dy;
            return true;
        }

        /// Las joyas se apoyan sobre el manto de oro cuando el castillo ya está en su sitio y encendido (al armarlas todavía no lo
        /// está: el castillo se construye en otro lugar y luego se traslada; medir entonces subía los cofres decenas de metros).
        System.Collections.IEnumerator AsentarJoyas(List<KeyValuePair<GameObject, float>> lista)
        {
            yield return null; yield return null;
            while (castilloRaiz != null && !castilloRaiz.activeInHierarchy) yield return new WaitForSeconds(1f);
            yield return new WaitForSeconds(0.5f);
            int ok = 0, fuera = 0;
            foreach (var kv in lista) { if (kv.Key == null) continue; if (SobreOro(kv.Key, kv.Value)) ok++; else fuera++; }
            plugin.Log("Castillo: joyas de la bóveda apoyadas sobre el oro: " + ok + " (" + fuera + " quitadas por no tener oro debajo)");
        }

        /// Reemplaza las copas: cofres, lingotes, gemas sueltas, diamantes y collares sobre el manto de oro. Más una sonda de reflejos.
        void JoyasBoveda(Transform pa, Material oro, System.Func<float, float, float, bool> libre, float y, float bovX, float z0, float z1)
        {
            MaterialesJoyas();
            var rng = new System.Random(77031);
            var porAsentar = new List<KeyValuePair<GameObject, float>>();
            System.Func<float, float, float, bool> sitio = (x, z, r) =>
            {
                if (!libre(x, z, r)) return false;
                foreach (var o in montanasBoveda) if (Vector2.Distance(new Vector2(x, z), new Vector2(o.x, o.y)) < r + o.z + 0.3f) return false;
                return true;
            };
            int cofres = 0, pilas = 0, sueltas = 0, collares = 0;
            for (int i = 0; i < 400 && (cofres < 8 || pilas < 10); i++)
            {
                float x = ((float)rng.NextDouble() * 2f - 1f) * (bovX - 2f), z = z0 + 2f + (float)rng.NextDouble() * (z1 - z0 - 4f);
                if (cofres < 8) { if (!sitio(x, z, 0.8f)) continue; CofreTesoro(pa, new Vector3(x, y - 0.05f, z), (float)rng.NextDouble() * 360f, oro, rng); porAsentar.Add(new KeyValuePair<GameObject, float>(pa.GetChild(pa.childCount - 1).gameObject, 0.08f)); cofres++; }
                else { if (!sitio(x, z, 0.4f)) continue; Lingotes(pa, new Vector3(x, y - 0.03f, z), (float)rng.NextDouble() * 360f, pilas % 2 == 1, rng); porAsentar.Add(new KeyValuePair<GameObject, float>(pa.GetChild(pa.childCount - 1).gameObject, 0.04f)); pilas++; }
            }
            var suelo = new GameObject("coop_boveda_gemas"); suelo.transform.SetParent(pa, false);
            for (int i = 0; i < 600 && sueltas < 90; i++)
            {
                float x = ((float)rng.NextDouble() * 2f - 1f) * (bovX - 1f), z = z0 + 1f + (float)rng.NextDouble() * (z1 - z0 - 2f);
                if (!libre(x, z, 0.1f)) continue;
                bool diam = rng.NextDouble() < 0.3;
                Gema(suelo.transform, new Vector3(x, y + 0.02f, z), diam ? 0.05f + 0.04f * (float)rng.NextDouble() : 0.06f + 0.07f * (float)rng.NextDouble(), diam ? matDiamante : matGemas[rng.Next(matGemas.Length)], rng);
                porAsentar.Add(new KeyValuePair<GameObject, float>(suelo.transform.GetChild(suelo.transform.childCount - 1).gameObject, 0.015f));
                sueltas++;
            }
            for (int i = 0; i < 300 && collares < 12; i++)
            {
                float x = ((float)rng.NextDouble() * 2f - 1f) * (bovX - 2f), z = z0 + 2f + (float)rng.NextDouble() * (z1 - z0 - 4f);
                if (!sitio(x, z, 0.35f)) continue;
                float a = (float)rng.NextDouble() * Mathf.PI;
                Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.22f, c = new Vector3(x, y + 0.02f, z);
                var cg = new GameObject("coop_collar"); cg.transform.SetParent(suelo.transform, false);
                Collar(cg.transform, c - d, c + d, 0.0f, rng.NextDouble() < 0.6 ? matOroJ : matPlataJ, rng.NextDouble() < 0.3 ? matDiamante : matGemas[rng.Next(matGemas.Length)], rng);
                porAsentar.Add(new KeyValuePair<GameObject, float>(cg, 0f)); collares++;
            }
            plugin.StartCoroutine(AsentarJoyas(porAsentar));
            // (sin sonda de reflejos: ponía rayas blancas y moradas en las columnas de piedra, 2026-11-18)
            plugin.Log("Castillo: bóveda con " + cofres + " cofres de tesoro, " + pilas + " pilas de lingotes, " + sueltas + " gemas y diamantes sueltos, " + collares + " collares");
        }
    }
}

// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using System.Collections.Generic;
using System.IO;
using PixelCrushers.DialogueSystem;
using UnityEngine;

namespace WitchHuntCoop
{
    /// Las dos casas abandonadas de la escopeta recortada (lugares únicos: una vez por partida).
    /// Cada una es una copia de la cabaña (solo sus piezas de detalle completo, sin luces ni muebles)
    /// con un cadáver tendido dentro y su escopeta en el piso, junto a él. Quien la recoge (E) es el único
    /// que la tiene; esa escopeta desaparece también para el compañero y no vuelve en esta partida.
    public partial class Mundo
    {
        public const string VarEscopetasTomadas = "coop_escopetas_tomadas";
        class Casa { public int N; public GameObject Raiz, Escopeta; public Vector3 Pos; }
        readonly List<Casa> casas = new List<Casa>();

        List<Vector4> huellasCasas;

        const float BajarCasa = 0.8f;

        void ColocarCasasEscopeta(int semillaPartida)
        {
            casas.Clear(); huellasCasas = null;
            GameObject cabana = null, cuerpo = null;
            foreach (var g in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (g.name == "THE_WOOD_CABIN_02") cabana = g;
                else if (cuerpo == null && Catalogo.BaseDe(g.name) == "floating_body") cuerpo = g;
            }
            if (cabana == null) { plugin.Log("Casas: no encontré la cabaña modelo"); return; }
            var tomadas = Tomadas();
            var rng = new System.Random(semillaPartida ^ Hash("coop_casas_escopeta"));
            var ocupados = new List<Vector3> { this.cabana };
            foreach (var t in lugaresCopias) if (t != null && t.name.StartsWith("coop_lugar_")) ocupados.Add(t.transform.position);
            Bounds b; if (!CajaDe(new List<GameObject> { cabana }, out b)) b = new Bounds(cabana.transform.position, Vector3.one);
            Vector3 desfase = cabana.transform.position - new Vector3(b.center.x, b.min.y, b.center.z);
            for (int n = 0; n < 2; n++)
            {
                Vector3 sitio = Vector3.zero; float yaw = 0f; bool ok = false;
                for (int k = 0; k < 40 && !ok; k++)
                {
                    if (!Ubicar(rng, ocupados, 350f, out sitio, out yaw)) continue;
                    float y; ok = Llano(sitio, 8f, 1.2f, out y) && y > nivelAgua + 1f && !Choca(huellasCasas ?? (huellasCasas = Huellas()), sitio.x, sitio.z, 9f, 3f) && Despejado(sitio, 12f);
                    if (ok) sitio.y = y - BajarCasa;   // más abajo: sobre sus pilares quedaba flotando (pedido 2026-11-17)
                }
                if (!ok) { plugin.Log("Casas: sin sitio llano para la casa " + (n + 1)); continue; }
                ocupados.Add(sitio);
                Quaternion giro = Quaternion.Euler(0f, yaw, 0f);
                int faltan;
                var casa = Coop.Piezas.Clonar(cabana, sitio + giro * desfase, giro * cabana.transform.rotation, out faltan);
                casa.name = "coop_casa_escopeta_" + (n + 1);
                // Solo la cabaña: sin piezas de menos detalle (no hay sus mallas), sin luces ni partículas.
                foreach (var lg in casa.GetComponentsInChildren<LODGroup>(true)) Object.Destroy(lg);
                int ocultas = DejarUnDetalle(casa);
                foreach (var l in casa.GetComponentsInChildren<Light>(true)) l.enabled = false;
                foreach (var ps in casa.GetComponentsInChildren<ParticleSystem>(true)) Object.Destroy(ps.gameObject == casa ? (UnityEngine.Object)ps : ps.gameObject);   // si las partículas están en la raíz, solo se quita el sistema (no el objeto entero)
                PonerPiso(casa);
                lugaresCopias.Add(casa);
                // amueblada como las cabañas cerradas (mesa, cama, cajas, barril, estante, chimenea propia), sin otro cadáver
                // ni cofre, y con luz cálida de velas (pedido 2026-11-17: decorarla más y que se vea mejor)
                try { QuitarChimeneaYPiedras(casa); DecorarCabana(casa, semillaPartida ^ (n + 1) * 7919, 0, false, false, true); } catch (System.Exception e) { plugin.Log("Casas: error al amueblar " + e.Message); }
                Vector3 rack = sitioExpositor, frente = frenteExpositor; bool hayRack = hayExpositor;
                if (hayRack) { Expositor(casa.transform, rack, frente); vistasExpositor.Add(new Vector3[] { rack + frente * 2.6f - Vector3.up * 0.1f, rack }); }
                try { CerrarCasaEscopeta(casa, cabana, n, semillaPartida, sitio, yaw); } catch (System.Exception e) { plugin.Log("Casas: error en la puerta " + e.Message); }
                foreach (var pl in new[] { new Vector3(-2.2f, 2.2f, 1.5f), new Vector3(2.2f, 2.2f, -1.5f) })
                {
                    var lz = new GameObject("coop_casa_luz"); lz.transform.SetParent(casa.transform, false); lz.transform.position = sitio + giro * pl;
                    var lc = lz.AddComponent<Light>(); lc.type = LightType.Point; lc.color = new Color(1f, 0.72f, 0.42f); lc.range = 7f; lc.intensity = 0.9f; lc.shadows = LightShadows.Soft;
                    lz.AddComponent<Titilar>();
                }
                // Cadáver tendido junto a la pared del fondo, y la escopeta sobre él.
                Vector3 dentro = sitio + giro * new Vector3(1.5f, 0.15f, 0.5f);
                Collider piso = null;
                foreach (var col in casa.GetComponentsInChildren<BoxCollider>(true)) if (col.name.StartsWith("cabinFloor")) piso = col;
                if (piso != null)
                {
                    // Sobre el piso, a medio camino entre el centro y la pared del fondo.
                    Bounds bp = piso.bounds;
                    Vector3 fondo = giro * Vector3.forward;
                    dentro = new Vector3(bp.center.x, bp.max.y, bp.center.z) + fondo * Mathf.Min(bp.extents.x, bp.extents.z) * 0.4f;
                }
                else
                {
                    RaycastHit h;
                    if (Physics.Raycast(dentro + Vector3.up * 2.5f, Vector3.down, out h, 4f, ~0, QueryTriggerInteraction.Ignore)) dentro = h.point;
                }
                float pisoY = dentro.y;
                // (sin cadáver: pedido 2026-11-18)
                var casaInfo = new Casa { N = n, Raiz = casa, Pos = dentro };
                string id = "casa" + n;
                if (!tomadas.Contains(id))
                {
                    // Tendida sobre el cuerpo, cruzada (el cañón es el eje X del modelo).
                    var esc = Escopeta.Modelo("coop_casa_escopeta_arma_" + (n + 1) + (hayRack ? "_pared" : ""));
                    // En el piso, junto al cuerpo (hacia la pared del fondo), con una luz tenue para verla.
                    if (hayRack)
                    {
                        // colgada en los ganchos del expositor: a lo largo de la pared, culata a la izquierda y cañón a la derecha
                        Vector3 derecha = Vector3.Cross(Vector3.up, -frente).normalized;
                        esc.transform.position = rack + frente * 0.11f - Vector3.up * 0.04f;
                        esc.transform.rotation = Quaternion.LookRotation(Vector3.Cross(derecha, Vector3.up), Vector3.up);
                        dentro = esc.transform.position;
                    }
                    else
                    {
                        esc.transform.position = new Vector3(dentro.x, pisoY + 0.2f, dentro.z) - giro * Vector3.forward * 1.3f;
                        esc.transform.rotation = giro * Quaternion.Euler(0f, 30f, 0f);
                    }
                    var luz = new GameObject("coop_luz_escopeta");
                    luz.transform.SetParent(esc.transform, false);
                    luz.transform.localPosition = Vector3.up * 0.7f + (hayRack ? frente * 0.6f : Vector3.zero);
                    var l = luz.AddComponent<Light>();
                    l.type = LightType.Point; l.color = new Color(1f, 0.85f, 0.6f); l.range = 4f; l.intensity = 1.3f; l.shadows = LightShadows.None;
                    casaInfo.Escopeta = esc;
                }
                casas.Add(casaInfo);
                plugin.Log("Casas: casa " + (n + 1) + " en " + (sitio - Ancla()).ToString("F0") + (tomadas.Contains(id) ? " (escopeta ya tomada)" : "") + ", " + faltan + " piezas sin malla, " + ocultas + " de menos detalle ocultas");
            }
        }

        /// Sin LODGroup se verían todos los niveles de detalle a la vez: de cada pieza (mismo nombre
        /// base y sitio) queda solo el de más detalle que tenga malla.
        static int DejarUnDetalle(GameObject raiz)
        {
            var mejor = new Dictionary<string, KeyValuePair<int, Renderer>>();
            var todos = new List<KeyValuePair<string, Renderer>>();
            foreach (var r in raiz.GetComponentsInChildren<MeshRenderer>(true))
            {
                int i = r.name.LastIndexOf("_LOD");
                if (i < 0 || i + 4 >= r.name.Length || !char.IsDigit(r.name[i + 4])) continue;
                int nivel = r.name[i + 4] - '0';
                Vector3 p = r.transform.localPosition;
                string clave = r.name.Substring(0, i) + "@" + Mathf.RoundToInt(p.x * 2) + "," + Mathf.RoundToInt(p.y * 2) + "," + Mathf.RoundToInt(p.z * 2);
                todos.Add(new KeyValuePair<string, Renderer>(clave, r));
                if (!r.enabled) continue;
                KeyValuePair<int, Renderer> m;
                if (!mejor.TryGetValue(clave, out m) || nivel < m.Key) mejor[clave] = new KeyValuePair<int, Renderer>(nivel, r);
            }
            int ocultas = 0;
            foreach (var kv in todos)
            {
                KeyValuePair<int, Renderer> m;
                if (mejor.TryGetValue(kv.Key, out m) && m.Value != kv.Value && kv.Value.enabled) { kv.Value.enabled = false; ocultas++; }
            }
            return ocultas;
        }

        /// El piso de la cabaña no tiene malla suelta (solo su colisión): se tapa con una tabla del
        /// tamaño de esa colisión, con la madera del porche.
        /// Expositor de pared para la escopeta: tabla de madera y dos ganchos de hierro en forma de L.
        void Expositor(Transform pad, Vector3 c, Vector3 frente)
        {
            MaterialesMuebles();
            Material mad = null;
            foreach (var r in pad.GetComponentsInChildren<MeshRenderer>(true)) if (r.enabled && r.name.StartsWith("cabinWallsInterior") && r.sharedMaterial != null) { mad = r.sharedMaterial; break; }
            var q = Quaternion.LookRotation(frente, Vector3.up);
            Vector3 der = Vector3.Cross(Vector3.up, -frente).normalized;
            var mt = new Malla(); var mh = new Malla();
            Bloque(mt, c, new Vector3(1.75f, 0.6f, 0.06f), q);                                                     // tabla
            foreach (var s in new[] { -0.38f, 0.38f })
            {
                Vector3 g = c + der * s + frente * 0.03f;
                Bloque(mh, g + Vector3.up * 0.04f, new Vector3(0.03f, 0.16f, 0.02f), q);                          // brazo vertical
                Bloque(mh, g - Vector3.up * 0.05f + frente * 0.06f, new Vector3(0.03f, 0.02f, 0.13f), q);         // brazo hacia afuera
                Bloque(mh, g - Vector3.up * 0.02f + frente * 0.12f, new Vector3(0.03f, 0.06f, 0.02f), q);         // tope
            }
            mt.CrearMundo("coop_expositor_tabla", pad, mad != null ? Tinte(mad, new Color(0.85f, 0.62f, 0.36f)) : matBarro, false);
            mh.CrearMundo("coop_expositor_ganchos", pad, Liso("coop_gancho", new Color(0.08f, 0.08f, 0.08f), 0.35f), false);
        }

        static void PonerPiso(GameObject casa)
        {
            BoxCollider piso = null; Material madera = null;
            foreach (var c in casa.GetComponentsInChildren<BoxCollider>(true)) if (c.name.StartsWith("cabinFloor")) piso = c;
            foreach (var r in casa.GetComponentsInChildren<MeshRenderer>(true))
                if (r.enabled && r.name.StartsWith("porchFloor")) { madera = r.sharedMaterial; break; }
            if (piso == null) return;
            var tabla = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(tabla.GetComponent<Collider>());
            tabla.name = "coop_piso";
            tabla.transform.SetParent(piso.transform, false);
            Vector3 t = piso.size, c0 = piso.center;
            float grosor = Mathf.Min(t.y, 0.08f);
            // El eje local que apunta hacia arriba en el mundo es la altura del piso.
            Vector3 up = piso.transform.InverseTransformDirection(Vector3.up);
            int eje = Mathf.Abs(up.x) > Mathf.Abs(up.y) ? (Mathf.Abs(up.x) > Mathf.Abs(up.z) ? 0 : 2) : (Mathf.Abs(up.y) > Mathf.Abs(up.z) ? 1 : 2);
            float signo = Mathf.Sign(up[eje]);
            float k = 1f / Mathf.Max(0.0001f, Mathf.Abs(piso.transform.lossyScale[eje]));   // unidades locales por metro
            grosor = Mathf.Min(t[eje], 0.08f * k);
            Vector3 escala = t; escala[eje] = grosor;
            Vector3 centro = c0; centro[eje] = c0[eje] + signo * (t[eje] / 2f - grosor / 2f + 0.01f * k);
            tabla.transform.localPosition = centro; tabla.transform.localScale = escala;
            var mr = tabla.GetComponent<MeshRenderer>();
            if (madera != null)
            {
                var m = new Material(madera) { name = "coop_piso_madera" };
                m.mainTextureScale = new Vector2(3f, 3f);
                mr.sharedMaterial = m;
            }
        }

        static HashSet<string> Tomadas()
        {
            var h = new HashSet<string>(); string s = "";
            try { s = DialogueLua.GetVariable(VarEscopetasTomadas).AsString; } catch { }
            if (!string.IsNullOrEmpty(s) && s != "nil") foreach (var x in s.Split('|')) if (x.Length > 0) h.Add(x);
            return h;
        }

        /// Terreno casi plano en un radio: diferencia de altura máxima tolerada; y = la más baja
        /// (la casa se apoya abajo y el resto queda tapado por el suelo).
        bool Llano(Vector3 c, float radio, float tolerancia, out float y)
        {
            float min = float.MaxValue, max = float.MinValue;
            for (int i = -2; i <= 2; i++)
                for (int k = -2; k <= 2; k++)
                {
                    float h = AlturaEn(c + new Vector3(i * radio / 2f, 0f, k * radio / 2f));
                    if (float.IsNaN(h)) { y = 0f; return false; }
                    min = Mathf.Min(min, h); max = Mathf.Max(max, h);
                }
            y = min;
            return max - min <= tolerancia;
        }

        Casa casaCerca;

        void ActualizarCasas()
        {
            casaCerca = null;
            var j = plugin.Jugador;
            if (j == null) return;
            foreach (var c in casas)
                if (c.Escopeta != null && Cerca(j.transform.position, c.Escopeta.transform.position, 2.8f, 2.5f)) { casaCerca = c; break; }   // en horizontal, con tolerancia de altura (no se podía recoger)
            if (casaCerca != null && Controles.Interactuar() && Entrada.Enfocado) TomarEscopeta(casaCerca, true);
        }

        /// Punto más bajo real de un objeto con mallas deformadas (las cajas de los SkinnedMeshRenderer son las del reposo, no las de la pose).
        static float MinYReal(GameObject c)
        {
            float min = float.MaxValue;
            foreach (var r in c.GetComponentsInChildren<Renderer>(false))
            {
                if (!r.enabled || r is ParticleSystemRenderer) continue;
                var sk = r as SkinnedMeshRenderer;
                if (sk != null && sk.sharedMesh != null)
                {
                    var m = new Mesh(); sk.BakeMesh(m);
                    var tr = Matrix4x4.TRS(sk.transform.position, sk.transform.rotation, sk.transform.lossyScale);
                    foreach (var v in m.vertices) { float y = tr.MultiplyPoint3x4(v).y; if (y < min) min = y; }
                    Object.Destroy(m);
                }
                else if (r.bounds.min.y < min) min = r.bounds.min.y;
            }
            return min == float.MaxValue ? c.transform.position.y : min;
        }

        /// Avisos de la escopeta, uno tras otro: a los 5 s (tras el título de la luna de sangre) "¡Conseguiste…!" 3 s y después cómo alternar el disparo 3 s.
        System.Collections.IEnumerator AvisosEscopeta()
        {
            yield return new WaitForSeconds(5f);
            Viaje.Avisar("¡Conseguiste la escopeta recortada!", 3f);
            yield return new WaitForSeconds(3.3f);
            Viaje.Avisar("Alterna entre disparo doble o con un cañón con la tecla " + Controles.NombreTecla(Controles.Tecla("modo_escopeta")) + ".", 3f);
        }

        void TomarEscopeta(Casa c, bool mia)
        {
            if (c.Escopeta == null) return;
            Object.Destroy(c.Escopeta); c.Escopeta = null;
            var h = Tomadas(); h.Add("casa" + c.N);
            DialogueLua.SetVariable(VarEscopetasTomadas, string.Join("|", new List<string>(h).ToArray()));
            if (mia)
            {
                Escopeta.Dar(true);
                plugin.StartCoroutine(AvisosEscopeta());
                int n = c.N;
                for (int i = 0; i < 3; i++) plugin.EnviarEscopeta(Red.Armar(w => w.Write(n)));
                plugin.Log("Casas: tomé la escopeta de la casa " + (c.N + 1));
                if (Eventos.Instancia != null) Eventos.Instancia.LunaPorArma();   // encontrarla despierta la luna de sangre
            }
            else plugin.Log("Casas: " + plugin.NombreCompanero + " tomó la escopeta de la casa " + (c.N + 1));
        }

        public void RecibirEscopeta(BinaryReader r)
        {
            int n = r.ReadInt32();
            foreach (var c in casas) if (c.N == n) { TomarEscopeta(c, false); return; }
            var h = Tomadas(); h.Add("casa" + n);   // aún no estoy en el bosque: queda anotada
            DialogueLua.SetVariable(VarEscopetasTomadas, string.Join("|", new List<string>(h).ToArray()));
        }

        public void DibujarCasa()
        {
            if (casaCerca == null || plugin.Jugador == null) return;
            if (estiloCofre == null)
            {
                estiloCofre = new GUIStyle(GUI.skin.label) { fontSize = Opciones.T(20), alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
                estiloCofre.normal.textColor = new Color(1f, 0.9f, 0.6f);
            }
            GUI.Label(new Rect(0, Screen.height * 0.6f, Screen.width, 30f), "E: recoger la escopeta recortada", estiloCofre);
        }

        public string DescribirCasas()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var c in casas) sb.Append("casa ").Append(c.N + 1).Append(' ').Append((c.Pos - Ancla()).ToString("F0")).Append(c.Escopeta != null ? " con escopeta; " : " sin escopeta; ");
            return sb.ToString();
        }

        public string PiezasCasa()
        {
            if (casas.Count == 0 || casas[0].Raiz == null) return "sin casa";
            var sb = new System.Text.StringBuilder();
            foreach (var r in casas[0].Raiz.GetComponentsInChildren<MeshRenderer>(true))
            {
                var mf = r.GetComponent<MeshFilter>();
                sb.Append(r.name).Append(r.enabled ? "" : "(apagada)").Append('=').Append(mf != null && mf.sharedMesh != null ? mf.sharedMesh.name : "-").Append("; ");
            }
            return sb.ToString();
        }

        public Vector3? PosCasa(int n) { foreach (var c in casas) if (c.N == n) return c.Pos - Ancla(); return null; }
        public void OrdenTomarEscopeta(int n) { foreach (var c in casas) if (c.N == n) TomarEscopeta(c, true); }
    }
}

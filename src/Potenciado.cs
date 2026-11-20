// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using System.Collections.Generic;
using HutongGames.PlayMaker;
using UnityEngine;

namespace WitchHuntCoop
{
    /// Guardián potenciado (3 % de los guardianes de los puestos): ojos rojos y aura roja, detecta
    /// desde más lejos, pega el triple y aguanta el doble. El anfitrión lo decide al generarlo y lo
    /// marca en el nombre ("+P"), que viaja en el catálogo de monstruos: el invitado lo genera igual.
    public class Potenciado : MonoBehaviour
    {
        public const string Marca = " +P";
        public const float FactorVida = 2f, FactorDano = 3f, FactorDeteccion = 1.8f;
        public const double Probabilidad = 0.03;
        /// En la noche ruidosa los potenciados salen el doble.
        public static double ProbabilidadActual { get { return (Eventos.Ruidosa ? Probabilidad * 2 : Probabilidad) * Opciones.FactorDificultad; } }
        readonly List<FsmFloat> distancias = new List<FsmFloat>();

        /// Deja de ser potenciado (al terminar la Luna de sangre).
        public static void Quitar(GameObject go)
        {
            if (go == null) return;
            foreach (var p in go.GetComponentsInChildren<Potenciado>(true))
            {
                foreach (var v in p.distancias) if (v != null) v.Value /= FactorDeteccion;
                p.RestaurarMateriales();
                Destroy(p);
            }
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                if (t != null && (t.name == "coop_aura" || t.name == "coop_ojo" || t.name == "coop_brillo_ojo" || t.name == "coop_humo" || t.name == "coop_gotas")) Destroy(t.gameObject);
            if (go.name.EndsWith(Marca)) go.name = go.name.Substring(0, go.name.Length - Marca.Length);
        }

        Light aura;
        float fase;

        public static bool Es(Component c) { return c != null && c.GetComponentInParent<Potenciado>() != null; }

        /// Convierte un monstruo recién generado en potenciado (visual y detección).
        public static void Aplicar(GameObject go)
        {
            if (go == null || go.GetComponentInChildren<Potenciado>(true) != null) return;
            if (Aliados.Protegido(go.transform)) return;   // la cabra negra: sin ojos rojos
            if (!go.name.EndsWith(Marca)) go.name += Marca;
            // Va en el objeto que camina (el del agente de navegación), no en la raíz, que se queda quieta.
            var ag = go.GetComponentInChildren<UnityEngine.AI.NavMeshAgent>(true);
            var donde = ag != null ? ag.gameObject : go;
            if (donde.GetComponent<Potenciado>() == null)
            {
                var pot = donde.AddComponent<Potenciado>();
                // Deadblind (efecto borroso al mirarlo): potenciado solo en vida, sin aura, ojos, sangre, gas ni más detección
                pot.soloVida = go.name.IndexOf("Deadblind", System.StringComparison.OrdinalIgnoreCase) >= 0 || donde.name.IndexOf("Deadblind", System.StringComparison.OrdinalIgnoreCase) >= 0;
                pot.Montar();
            }
        }

        bool soloVida;

        void Montar()
        {
            if (soloVida) return;
            var rs = GetComponentsInChildren<Renderer>();
            Bounds b = new Bounds(transform.position + Vector3.up, Vector3.one);
            bool hay = false;
            foreach (var r in rs)
            {
                if (r is ParticleSystemRenderer) continue;
                // algunas mallas sueltas del modelo están en el origen del mundo: no cuentan para el tamaño ni el centro
                Vector3 d = r.bounds.center - transform.position; d.y = 0f;
                if (d.sqrMagnitude > 36f) continue;
                if (!hay) { b = r.bounds; hay = true; } else b.Encapsulate(r.bounds);
            }
            float tam = Mathf.Clamp(b.size.magnitude, 1f, 8f);
            // Arañas (pedido 2026-11-19): la bruma gris y los ojos se veían mal en ellas → solo aura roja y cubiertas de sangre.
            bool arana = name.IndexOf("spider", System.StringComparison.OrdinalIgnoreCase) >= 0 || transform.root.name.IndexOf("spider", System.StringComparison.OrdinalIgnoreCase) >= 0;

            // Aura: luz roja que late, colgada del cuerpo para que lo siga.
            Transform cuerpo = rs.Length > 0 ? rs[0].transform : transform;
            var ga = new GameObject("coop_aura");
            ga.transform.SetParent(transform, false);
            ga.transform.position = b.center;
            aura = ga.AddComponent<Light>();
            aura.type = LightType.Point; aura.color = new Color(1f, 0.08f, 0.05f); aura.range = tam * 1.8f; aura.intensity = 2.2f; aura.shadows = LightShadows.None;

            // Cubierto de sangre: el cuerpo se tiñe de rojo oscuro, brilla por dentro y lleva manchas y chorreones encima.
            try { Cubrir(rs, arana); } catch (System.Exception e) { Debug.Log("[WitchHuntCoop] potenciado: no se pudo cubrir de sangre: " + e.Message); }

            // Aura de poder: bruma roja que sube alrededor del cuerpo + gotas de sangre que caen.
            try { if (arana) CrearAura(transform, b, tam, new Color(0.85f, 0.04f, 0.02f), false); else Aura(b, tam); } catch (System.Exception e) { Debug.Log("[WitchHuntCoop] potenciado: no se pudo crear el aura: " + e.Message); }

            // Ojos: dos rendijas encendidas (sprites que miran a la cámara) pegadas a la cabeza, sin esferas. (Las arañas no.)
            Transform cabeza = arana ? null : BuscarCabeza(transform);
            Vector3 centroOjos; Transform padre;
            if (cabeza != null) { padre = cabeza; centroOjos = cabeza.position; }
            else { padre = transform; centroOjos = new Vector3(b.center.x, b.max.y - b.size.y * 0.15f, b.center.z) + transform.forward * b.extents.z * 0.8f; }
            float sep = Mathf.Clamp(tam * 0.035f, 0.04f, 0.18f);
            for (int i = 0; i < (arana ? 0 : 2); i++)
            {
                Vector3 p = centroOjos + transform.right * (i == 0 ? -sep : sep) + transform.forward * sep * 0.8f;
                var ojo = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Destroy(ojo.GetComponent<Collider>());
                ojo.name = "coop_ojo";
                ojo.transform.SetParent(padre, true);
                ojo.transform.position = p;
                ojo.transform.localScale = Vector3.one * (sep * 1.7f) / Mathf.Max(padre.lossyScale.x, 0.001f);
                var rd = ojo.GetComponent<Renderer>();
                rd.material = MaterialBrillo(TexOjo(), new Color(1f, 0.15f, 0.05f, 1f));
                rd.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; rd.receiveShadows = false;
                ojo.AddComponent<MirarCamara>();
                var gl = new GameObject("coop_brillo_ojo");
                gl.transform.SetParent(padre, true);
                gl.transform.position = p;
                var l = gl.AddComponent<Light>();
                l.type = LightType.Point; l.color = Color.red; l.range = sep * 6f; l.intensity = 2.5f; l.shadows = LightShadows.None;
            }

            // Detecta desde más lejos: las distancias de sus FSM (vista, oído, persecución).
            int cambiadas = 0;
            foreach (var f in GetComponentsInChildren<PlayMakerFSM>(true))
            {
                if (f == null || f.FsmVariables == null) continue;
                foreach (var v in f.FsmVariables.FloatVariables)
                {
                    string n = v.Name.ToLowerInvariant();
                    if (n.Contains("attack")) continue;   // alcance del golpe: igual
                    if (n.Contains("range") || n.Contains("distance") || n.Contains("dist") || n.Contains("sight") || n.Contains("detect") || n.Contains("radius") || n.Contains("see") || n.Contains("hear"))
                    { v.Value *= FactorDeteccion; distancias.Add(v); cambiadas++; }
                }
            }
            Debug.Log("[WitchHuntCoop] potenciado: " + name + " (cabeza " + (cabeza != null ? cabeza.name : "—") + ", " + cambiadas + " distancias ×" + FactorDeteccion + ")");
        }

        // ---------------- Diseño: cubierto de sangre, aura y ojos ----------------

        class Original { public Renderer R; public Material[] Mats; public Material[] Propios; }
        readonly List<Original> originales = new List<Original>();

        /// Tiñe de sangre cada malla del cuerpo: color rojo oscuro, resplandor rojo y una capa extra con manchas y chorreones.
        void Cubrir(Renderer[] rs, bool mucha = false)
        {
            var manchas = TexSangre();
            foreach (var r in rs)
            {
                if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;
                if (r.name.StartsWith("coop_")) continue;
                { Vector3 dd = r.bounds.center - transform.position; dd.y = 0f; if (dd.sqrMagnitude > 36f) continue; }
                // El Deadblind lleva una malla de efecto (distorsión borrosa que, al mirarlo, hace daño): no se toca, o el efecto se rompe.
                bool efecto = false;
                foreach (var mm in r.sharedMaterials) if (mm != null && (mm.name.IndexOf("Effect", System.StringComparison.OrdinalIgnoreCase) >= 0 || mm.name.IndexOf("Distort", System.StringComparison.OrdinalIgnoreCase) >= 0 || (mm.shader != null && mm.shader.name.IndexOf("Distort", System.StringComparison.OrdinalIgnoreCase) >= 0))) efecto = true;
                if (efecto) { Debug.Log("[WitchHuntCoop] potenciado: malla de efecto '" + r.name + "' sin tocar"); continue; }
                var orig = new Original { R = r, Mats = r.sharedMaterials };
                var inst = r.materials;   // copias propias: no se toca el material compartido del juego
                foreach (var m in inst)
                {
                    if (m == null) continue;
                    if (m.HasProperty("_Color")) m.color = Color.Lerp(m.color, new Color(0.40f, 0.04f, 0.03f), mucha ? 0.60f : 0.30f);
                    if (m.HasProperty("_EmissionColor")) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", new Color(0.10f, 0.005f, 0.003f)); }
                    if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.8f);   // mojado
                }
                var capa = new Material(Shader.Find("Sprites/Default")) { mainTexture = manchas, color = new Color(0.58f, 0.0f, 0.0f, 1f), name = "coop_sangre" };
                capa.mainTextureScale = mucha ? new Vector2(3f, 3f) : new Vector2(2f, 2f);   // más manchas y chorreones en las arañas
                var todos = new Material[inst.Length + 1];
                for (int i = 0; i < inst.Length; i++) todos[i] = inst[i];
                todos[inst.Length] = capa;
                r.materials = todos;
                orig.Propios = todos;
                originales.Add(orig);
            }
        }

        void RestaurarMateriales()
        {
            foreach (var o in originales)
            {
                if (o.R != null) o.R.sharedMaterials = o.Mats;
                if (o.Propios != null) foreach (var m in o.Propios) if (m != null) Destroy(m);
            }
            originales.Clear();
        }

        void Aura(Bounds b, float tam) { CrearAura(transform, b, tam, new Color(0.55f, 0.55f, 0.58f), true); }

        /// Bruma de color que sube alrededor del cuerpo (y, si se pide, gotas que caen). La usan los potenciados (rojo) y los elementales (verde, azul).
        internal static void CrearAura(Transform padre, Bounds b, float tam, Color col, bool gotas)
        {
            float radio = Mathf.Clamp(Mathf.Min(b.extents.x, b.extents.z) * 1.1f, 0.25f, 2.5f);
            var gh = new GameObject("coop_humo");
            gh.transform.SetParent(padre, false);
            gh.transform.position = new Vector3(b.center.x, b.min.y + 0.2f, b.center.z);
            var ps = gh.AddComponent<ParticleSystem>();
            var main = ps.main; main.loop = true; main.startLifetime = 1.9f; main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(tam * 0.22f, tam * 0.42f);
            main.startColor = new Color(col.r, col.g, col.b, 0.55f);
            main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = 70;
            var em = ps.emission; em.rateOverTime = 30f;
            var fo = ps.shape; fo.shapeType = ParticleSystemShapeType.Circle; fo.radius = radio;
            var ve = ps.velocityOverLifetime; ve.enabled = true; ve.space = ParticleSystemSimulationSpace.World;
            ve.x = new ParticleSystem.MinMaxCurve(0f, 0f); ve.z = new ParticleSystem.MinMaxCurve(0f, 0f); ve.y = new ParticleSystem.MinMaxCurve(b.size.y * 0.35f, b.size.y * 0.7f);
            var cl = ps.colorOverLifetime; cl.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.Lerp(col, Color.white, 0.25f), 0f), new GradientColorKey(col * 0.55f, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.6f, 0.25f), new GradientAlphaKey(0f, 1f) });
            cl.color = g;
            var rend = gh.GetComponent<ParticleSystemRenderer>();
            rend.material = MaterialBrillo(TexBruma(), Color.white);
            rend.renderMode = ParticleSystemRenderMode.Billboard; rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (!gotas) return;

            // Gotas de sangre que caen del cuerpo.
            var gg = new GameObject("coop_gotas");
            gg.transform.SetParent(padre, false);
            gg.transform.position = b.center;
            var pg = gg.AddComponent<ParticleSystem>();
            var mg = pg.main; mg.loop = true; mg.startLifetime = 1.1f; mg.startSpeed = 0f; mg.gravityModifier = 1.3f;
            mg.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.11f); mg.startColor = new Color(0.5f, 0f, 0f, 1f);
            mg.simulationSpace = ParticleSystemSimulationSpace.World; mg.maxParticles = 40;
            var eg = pg.emission; eg.rateOverTime = 18f;
            var sg = pg.shape; sg.shapeType = ParticleSystemShapeType.Box; sg.box = new Vector3(radio * 1.4f, b.size.y * 0.6f, radio * 1.4f);
            var rg = gg.GetComponent<ParticleSystemRenderer>();
            rg.material = new Material(Shader.Find("Sprites/Default")) { mainTexture = TexDisco(), color = Color.white };
            rg.renderMode = ParticleSystemRenderMode.Billboard; rg.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        internal static Material MaterialBrillo(Texture2D tex, Color c)
        {
            // suma la luz (se ve encendido); si ese shader no está en el juego, mezcla normal
            var sh = Shader.Find("Legacy Shaders/Particles/Additive") ?? Shader.Find("Particles/Additive") ?? Shader.Find("Sprites/Default");
            var m = new Material(sh) { mainTexture = tex };
            if (m.HasProperty("_TintColor")) m.SetColor("_TintColor", new Color(c.r * 0.5f, c.g * 0.5f, c.b * 0.5f, c.a * 0.5f)); else m.color = c;
            return m;
        }

        internal static Material MaterialGas() { return MaterialBrillo(TexBruma(), Color.white); }
        static Texture2D texSangre, texBruma, texOjo, texDisco;

        static Texture2D Crear(int n, System.Func<float, float, Color> f)
        {
            var t = new Texture2D(n, n, TextureFormat.ARGB32, false) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Repeat };
            var px = new Color[n * n];
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++) px[y * n + x] = f(x / (float)n, y / (float)n);
            t.SetPixels(px); t.Apply();
            return t;
        }

        /// Manchas oscuras con chorreones hacia abajo (se repite sin costuras).
        static Texture2D TexSangre()
        {
            if (texSangre != null) return texSangre;
            const int N = 256;
            var rng = new System.Random(77);
            var a = new float[N * N];
            for (int k = 0; k < 26; k++)
            {
                float cx = (float)rng.NextDouble() * N, cy = (float)rng.NextDouble() * N, r = 4f + (float)rng.NextDouble() * 15f;
                float largo = r * (1.2f + (float)rng.NextDouble() * 4f);   // chorreón hacia abajo
                for (int y = -(int)largo; y <= (int)r; y++) for (int x = -(int)r; x <= (int)r; x++)
                {
                    float dx = x / r, dy = y > 0 ? y / r : y / largo * 0.35f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (y < 0 && Mathf.Abs(x) > r * 0.28f * (1f + 0.6f * (y / -largo))) { if (d > 0.9f) continue; }
                    if (d > 1f) continue;
                    int px = ((int)cx + x + N) % N, py = ((int)cy - y + N) % N;
                    a[py * N + px] = Mathf.Max(a[py * N + px], Mathf.Clamp01(1.4f - d) * 0.95f);
                }
            }
            texSangre = Crear(N, (u, v) => { float al = a[(int)(v * N) * N + (int)(u * N)]; return new Color(0.55f * al, 0.0f, 0.0f, al); });
            return texSangre;
        }

        static Texture2D TexBruma()
        {
            if (texBruma != null) return texBruma;
            texBruma = Crear(64, (u, v) => { float d = Mathf.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f)) * 2f; float a = Mathf.Clamp01(1f - d); a *= a; return new Color(1f, 1f, 1f, a); });   // blanca: el color lo pone cada humo (antes era rojiza y teñía de rojo las nubes, la niebla del castillo y del laberinto)
            texBruma.wrapMode = TextureWrapMode.Clamp;
            return texBruma;
        }

        /// Rendija encendida: núcleo casi blanco, borde rojo, alargada.
        static Texture2D TexOjo()
        {
            if (texOjo != null) return texOjo;
            texOjo = Crear(64, (u, v) =>
            {
                float dx = (u - 0.5f) * 2f, dy = (v - 0.5f) * 2f * 2.4f;   // estirada de lado a lado
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1f - d); a = a * a * (3f - 2f * a);
                float nucleo = Mathf.Clamp01(1f - d * 2.2f);
                return new Color(1f, 0.25f + nucleo * 0.75f, 0.1f + nucleo * 0.7f, a);
            });
            texOjo.wrapMode = TextureWrapMode.Clamp;
            return texOjo;
        }

        static Texture2D TexDisco()
        {
            if (texDisco != null) return texDisco;
            texDisco = Crear(16, (u, v) => { float d = Mathf.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f)) * 2f; return new Color(1f, 1f, 1f, d < 0.85f ? 1f : 0f); });
            texDisco.wrapMode = TextureWrapMode.Clamp;
            return texDisco;
        }

        static Transform BuscarCabeza(Transform raiz)
        {
            var an = raiz.GetComponentInChildren<Animator>();
            if (an != null && an.isHuman) { var h = an.GetBoneTransform(HumanBodyBones.Head); if (h != null) return h; }
            Transform mejor = null;
            foreach (var t in raiz.GetComponentsInChildren<Transform>())
            {
                string n = t.name.ToLowerInvariant();
                if (n.Contains("head") || n.Contains("cabeza") || n.Contains("skull"))
                    if (mejor == null || t.position.y > mejor.position.y) mejor = t;
            }
            return mejor;
        }

        bool muerto; float proxMuerte; TakeDamage tdPropio;

        void Update()
        {
            if (!muerto && Time.time >= proxMuerte)
            {
                proxMuerte = Time.time + 0.25f;
                if (tdPropio == null) tdPropio = GetComponentInChildren<TakeDamage>(true);
                if (tdPropio != null && Eventos.Muerto(tdPropio)) AlMorir();
            }
            if (aura == null || muerto) return;
            fase += Time.deltaTime * 3f;
            aura.intensity = 1.6f + Mathf.Sin(fase) * 0.8f;
        }

        /// Al morir: se apagan los ojos, la luz, el aura y las gotas (antes quedaban los círculos rojos tirados) y suelta una niebla roja.
        void AlMorir()
        {
            muerto = true;
            if (soloVida) return;
            Vector3 donde = transform.position + Vector3.up * 0.4f;
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                if (t == null) continue;
                if (t.name == "coop_ojo" || t.name == "coop_brillo_ojo" || t.name == "coop_aura") Destroy(t.gameObject);
                else if (t.name == "coop_humo" || t.name == "coop_gotas") { var ps = t.GetComponent<ParticleSystem>(); if (ps != null) ps.Stop(); Destroy(t.gameObject, 2.5f); }
            }
            aura = null;
            GasRojo.Crear(donde);
        }

        /// ¿Hay un potenciado vivo cerca del jugador? (los golpes llegan sin decir quién atacó)
        public static bool Cerca(Vector3 p, float radio)
        {
            foreach (var x in FindObjectsOfType<Potenciado>())
                if ((x.transform.position - p).sqrMagnitude < radio * radio) return true;
            return false;
        }
    }

    /// Hace que un sprite plano mire siempre a la cámara.
    public class MirarCamara : MonoBehaviour
    {
        void LateUpdate() { var c = Camera.main; if (c != null) transform.rotation = Quaternion.LookRotation(transform.position - c.transform.position, c.transform.up); }
    }

    /// Niebla roja que suelta un potenciado al morir: se esparce hasta 10 m, dura 5 s y quita 1 de vida cada 0,5 s a quien esté dentro
    /// (como el veneno de los elementales, no mata: deja al menos 1 de vida).
    public class GasRojo : MonoBehaviour
    {
        public const float Radio = 10f, Duracion = 5f, Tick = 0.5f, Dano = 1f;
        float inicio, proximo; int golpes;
        ParticleSystem ps;
        void OnDestroy() { Activos--; }
        public static int Activos;

        public static void Crear(Vector3 pos)
        {
            if (Activos >= 6) return;   // una Luna con muchas muertes seguidas no llena el bosque de niebla
            var go = new GameObject("coop_gas_rojo");
            go.transform.position = pos;
            go.AddComponent<GasRojo>().Montar();
        }

        void Montar()
        {
            Activos++;
            inicio = Time.time; proximo = Time.time + Tick;
            ps = gameObject.AddComponent<ParticleSystem>();
            var main = ps.main; main.loop = true; main.startLifetime = 2.2f; main.startSpeed = 0.3f;
            main.startSize = new ParticleSystem.MinMaxCurve(3f, 6f);
            main.startColor = new Color(0.5f, 0.5f, 0.53f, 0.45f);   // gris
            main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = 140;
            var em = ps.emission; em.rateOverTime = 45f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 1f;
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.25f, 0.1f), 0f), new GradientColorKey(new Color(0.6f, 0f, 0f), 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.55f, 0.2f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var rend = gameObject.GetComponent<ParticleSystemRenderer>();
            rend.material = Potenciado.MaterialGas();
            rend.renderMode = ParticleSystemRenderMode.Billboard; rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var luz = new GameObject("luz").AddComponent<Light>();
            luz.transform.SetParent(transform, false); luz.type = LightType.Point; luz.color = new Color(0.7f, 0.72f, 0.78f); luz.range = 14f; luz.intensity = 1.2f; luz.shadows = LightShadows.None;
        }

        float RadioAhora { get { return Mathf.Lerp(1.5f, Radio, Mathf.Clamp01((Time.time - inicio) / 1.5f)); } }   // se esparce en ~1,5 s

        void Update()
        {
            float t = Time.time - inicio;
            var sh = ps.shape; sh.radius = RadioAhora * 0.9f;
            if (t > Duracion - 1.2f) { var em = ps.emission; em.rateOverTime = 0f; }
            if (t > Duracion + 2.3f) { Debug.Log("[WitchHuntCoop] gas rojo: " + golpes + " golpes de " + Dano + " al jugador"); Destroy(gameObject); return; }
            if (t <= Duracion && Time.time >= proximo)
            {
                proximo += Tick;
                var j = Object.FindObjectOfType<FPSPlayer>();
                if (j != null && !j.GodMode)
                {
                    Vector3 d = j.transform.position - transform.position;
                    if (Mathf.Abs(d.y) < 5f) { d.y = 0f; if (d.magnitude <= RadioAhora && j.hitPoints > Dano + 1f) { j.ApplyDamage(Dano); golpes++; } }
                }
            }
        }
    }
}

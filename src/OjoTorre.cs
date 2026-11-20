// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using System.Collections.Generic;
using UnityEngine;

namespace WitchHuntCoop
{
    /// El Ojo (pedido 2026-11-18): en lo alto de la Torre del Homenaje, en vez de la bandera, un ojo enorme en llamas entre dos
    /// cuernos de piedra. Sigue con la mirada al jugador más cercano. Todo ser vivo a menos de 500 m que lo tenga a la vista
    /// (sin nada en medio) pierde vida cada segundo: jugadores, monstruos y aliados de la bola. Si lo miras directo a los ojos,
    /// la vista se te nubla un rato. Cada juego daña a su propio jugador y a sus aliados; los monstruos, solo el anfitrión
    /// (el combate compartido reparte el golpe al dueño), así nadie recibe el doble.
    public class OjoTorre : MonoBehaviour
    {
        public const float Alcance = 500f;
        static readonly float[] DanoJugador = { 3f, 4f, 6f };   // por segundo, según la dificultad
        const float DanoMonstruo = 6f, DanoAliado = 4f;
        public Transform Globo;          // gira para mirar
        public Transform Halo;           // resplandor que siempre mira a la cámara (se ve desde lejos)
        public static OjoTorre Actual;
        bool muriendo; float tMuerte;
        float proxGolpe; float nublado; public static float Nublado;   // 0..1, lo lee el efecto de la cámara
        Light luz; float semilla;

        void Start() { luz = GetComponentInChildren<Light>(); semilla = Random.value * 10f; Actual = this; }

        /// La palanca de la torre: el ojo estalla en llamas, se apaga y cae (y deja de hacer daño para siempre).
        public void Morir()
        {
            if (muriendo) return;
            muriendo = true; tMuerte = Time.time; Nublado = 0f;
            if (Globo != null)
            {
                // estallido de fuego
                var b = new GameObject("coop_ojo_estallido"); b.transform.position = Globo.position;
                var ps = b.AddComponent<ParticleSystem>(); var main = ps.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.6f); main.startSpeed = new ParticleSystem.MinMaxCurve(8f, 26f);
                main.startSize = new ParticleSystem.MinMaxCurve(4f, 12f); main.maxParticles = 700; main.loop = false; main.duration = 1f;
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.7f, 0.2f, 1f), new Color(1f, 0.25f, 0.02f, 0.8f));
                var em = ps.emission; em.rateOverTime = 0f; em.SetBursts(new[] { new ParticleSystem.Burst(0f, 500), new ParticleSystem.Burst(0.3f, 200) });
                var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 5f;
                var col = ps.colorOverLifetime; col.enabled = true; var gr = new Gradient();
                gr.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.9f, 0.5f), 0f), new GradientColorKey(new Color(1f, 0.3f, 0.02f), 0.4f), new GradientColorKey(new Color(0.15f, 0.12f, 0.1f), 1f) },
                           new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0f, 1f) });
                col.color = gr;
                var fx = Globo.GetComponentInChildren<ParticleSystemRenderer>(); if (fx != null) b.GetComponent<ParticleSystemRenderer>().sharedMaterial = fx.sharedMaterial;
                Object.Destroy(b, 6f);
                foreach (var p in Globo.GetComponentsInChildren<ParticleSystem>()) { var e2 = p.emission; e2.enabled = false; }
            }
            Viaje.Avisar("¡El Ojo ha caído!", 4f);
        }

        void Morir_Paso()
        {
            float t = Time.time - tMuerte;
            if (luz != null) luz.intensity = t < 0.3f ? 12f : Mathf.Max(0f, 4f * (1f - (t - 0.3f) / 2f));
            if (Halo != null) Halo.localScale = Vector3.Lerp(Halo.localScale, Vector3.zero, Time.deltaTime * 2f);
            if (Globo != null)
            {
                Globo.localScale = Vector3.Lerp(Globo.localScale, Vector3.one * 0.15f, Time.deltaTime * 0.8f);
                if (t > 0.8f) Globo.position += Vector3.down * (t - 0.8f) * 9f * Time.deltaTime;
                foreach (var r in Globo.GetComponentsInChildren<Renderer>()) if (!(r is ParticleSystemRenderer)) foreach (var m in r.materials) if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", Color.Lerp(m.GetColor("_EmissionColor"), Color.black, Time.deltaTime * 1.5f));
            }
            if (t > 4f) { if (Globo != null) Globo.gameObject.SetActive(false); if (Halo != null) Halo.gameObject.SetActive(false); if (luz != null) luz.enabled = false; enabled = false; }
        }

        bool AlaVista(Vector3 desde, Transform blanco, Vector3 punto)
        {
            Vector3 d = punto - desde; float dist = d.magnitude; if (dist < 0.5f) return true;
            RaycastHit[] hs = Physics.RaycastAll(desde, d / dist, dist - 0.3f, ~((1 << 2) | (1 << 27)), QueryTriggerInteraction.Ignore);
            foreach (var h in hs)
            {
                if (h.collider == null) continue;
                if (blanco != null && h.collider.transform.IsChildOf(blanco)) continue;
                if (h.collider.transform.IsChildOf(transform.root) && h.collider.GetComponentInParent<NoSolido>() != null) continue;
                return false;
            }
            return true;
        }

        void Update()
        {
            var pl = Escopeta.plugin; if (pl == null) return;
            if (muriendo) { Morir_Paso(); return; }
            Vector3 ojo = Globo != null ? Globo.position : transform.position;
            if (Halo != null && Camera.main != null)
            {
                Halo.rotation = Quaternion.LookRotation(Halo.position - Camera.main.transform.position);
                float pul = 1f + 0.06f * Mathf.Sin(Time.time * 2.3f) + 0.04f * Mathf.PerlinNoise(Time.time * 4f, semilla);
                Halo.localScale = new Vector3(52f, 34f, 1f) * pul;
            }
            if (luz != null) luz.intensity = 3.2f + 0.8f * Mathf.PerlinNoise(Time.time * 3f, semilla);
            // a quién mira: el jugador más cercano (yo o el compañero) que esté a su alcance
            var yo = pl.Jugador; var c = pl.CompaneroActual;
            Vector3? mirar = null; float mejor = Alcance;
            if (yo != null) { float d = Vector3.Distance(yo.transform.position, ojo); if (d < mejor) { mejor = d; mirar = yo.transform.position + Vector3.up * 1.5f; } }
            if (c != null && c.Visible) { float d = Vector3.Distance(c.PosicionMundo, ojo); if (d < mejor) { mejor = d; mirar = c.PosicionMundo + Vector3.up * 1.5f; } }
            if (Globo != null)
            {
                Quaternion obj = mirar.HasValue ? Quaternion.LookRotation(mirar.Value - ojo) : Quaternion.Euler(10f, Time.time * 8f, 0f);
                Globo.rotation = Quaternion.Slerp(Globo.rotation, obj, Time.deltaTime * 1.4f);
            }
            // ojos nublados: mirarlo directo (a menos de ~7° del centro de la vista) con nada en medio
            var cam = Camera.main;
            bool loMiro = false;
            if (cam != null && yo != null && Vector3.Distance(cam.transform.position, ojo) < Alcance)
            {
                Vector3 a = ojo - cam.transform.position;
                if (Vector3.Angle(cam.transform.forward, a) < 7f && AlaVista(cam.transform.position, yo.transform.root, ojo)) loMiro = true;
            }
            nublado = loMiro ? Mathf.MoveTowards(nublado, 1f, Time.deltaTime / 0.8f) : Mathf.MoveTowards(nublado, 0f, Time.deltaTime / 4f);
            Nublado = nublado;
            if (nublado > 0.01f && cam != null && cam.GetComponent<VistaNublada>() == null) cam.gameObject.AddComponent<VistaNublada>();
            if (Time.time < proxGolpe) return;
            proxGolpe = Time.time + 1f;
            try { Golpear(pl, ojo); } catch (System.Exception e) { pl.Log("Ojo: " + e.Message); }
        }

        void Golpear(Plugin pl, Vector3 ojo)
        {
            // mi jugador
            var yo = pl.Jugador;
            if (yo != null && Vector3.Distance(yo.transform.position, ojo) < Alcance && AlaVista(ojo, yo.transform.root, yo.transform.position + Vector3.up * 1.4f))
            {
                yo.ApplyDamage(DanoJugador[Mathf.Clamp(Opciones.Dificultad, 0, 2)]);
            }
            // mis aliados de la bola
            foreach (var v in Object.FindObjectsOfType<Aliados.VidaAliado>())
                if (v != null && v.Remoto == null && v.Vida > 0f && Vector3.Distance(v.transform.position, ojo) < Alcance && AlaVista(ojo, v.transform.root, v.transform.position + Vector3.up)) v.ApplyDamage(DanoAliado);
            // monstruos: solo el anfitrión (o jugando solo)
            if (pl.Conectado && !pl.EsAnfitrion) return;
            var vistos = new HashSet<Transform>();
            foreach (var z in Object.FindObjectsOfType<CharacterDamagePlaymakerAI>())
            {
                // una zona por monstruo y nunca la de la cabeza (su ApplyDamage busca un FSM "Headshot" que a veces no existe)
                try
                {
                    if (z == null || !z.gameObject.activeInHierarchy || z.headshot || z.ai == null) continue;
                    var raiz = z.transform.root; if (vistos.Contains(raiz)) continue;
                    if (raiz.GetComponentInChildren<FPSPlayer>() != null) continue;
                    if (Eventos.Muerto(z.ai)) continue;
                    if (Vector3.Distance(z.transform.position, ojo) > Alcance) continue;
                    if (!AlaVista(ojo, raiz, z.transform.position)) continue;
                    vistos.Add(raiz);
                    z.ApplyDamage(DanoMonstruo / Mathf.Max(0.1f, z.multiplier), false);
                }
                catch { }
            }
        }

        void OnDestroy() { Nublado = 0f; if (Actual == this) Actual = null; }
    }

    /// Vista nublada: la imagen se reduce y se vuelve a agrandar (borrosa) según OjoTorre.Nublado, con un velo rojizo.
    public class VistaNublada : MonoBehaviour
    {
        void OnRenderImage(RenderTexture src, RenderTexture dst)
        {
            float n = OjoTorre.Nublado;
            if (n < 0.01f) { Graphics.Blit(src, dst); return; }
            int pasos = 1 + Mathf.RoundToInt(n * 4f);   // 1 a 5 mitades: de un poco a muy borroso
            int w = src.width, h = src.height;
            var cadena = new List<RenderTexture>();
            RenderTexture cur = src;
            for (int i = 0; i < pasos; i++)
            {
                w = Mathf.Max(8, w / 2); h = Mathf.Max(8, h / 2);
                var rt = RenderTexture.GetTemporary(w, h, 0, src.format); rt.filterMode = FilterMode.Bilinear;
                Graphics.Blit(cur, rt); cadena.Add(rt); cur = rt;
            }
            for (int i = cadena.Count - 2; i >= 0; i--) { Graphics.Blit(cur, cadena[i]); cur = cadena[i]; }
            Graphics.Blit(cur, dst);
            foreach (var rt in cadena) RenderTexture.ReleaseTemporary(rt);
        }

        void OnGUI()
        {
            float n = OjoTorre.Nublado; if (n < 0.01f) return;
            var c = GUI.color; GUI.color = new Color(0.55f, 0.08f, 0.02f, 0.28f * n);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = c;
        }
    }

    public partial class Mundo
    {
        static Material matOjoIris, matOjoGlobo, matLlamaOjo, matHaloOjo;
        public const string VarOjoMuerto = "coop_ojo_muerto";

        /// La palanca: muere el Ojo (en los dos juegos; queda anotado en la partida).
        public void MatarOjo(bool mio)
        {
            if (LuaBool(VarOjoMuerto) && OjoTorre.Actual == null) return;
            PixelCrushers.DialogueSystem.DialogueLua.SetVariable(VarOjoMuerto, true);
            if (OjoTorre.Actual != null)
            {
                var ojoPos = OjoTorre.Actual.Globo != null ? OjoTorre.Actual.Globo.position : OjoTorre.Actual.transform.position;
                // muerte del Ojo: coro oscuro que se desploma (sonido del mod, elegido por el usuario 2026-11-19); si no cargó, el grito del juego
                var grito = Sonidos.Clip("ojo_muerte") ?? SonidosOjo.Grito();
                plugin.Log("Ojo: sonido de muerte = " + (Sonidos.Clip("ojo_muerte") != null ? "ojo_muerte (mod, " + grito.length.ToString("0.0") + " s)" : grito != null ? "grito del juego (respaldo)" : "ninguno"));
                if (grito != null) { var g = new GameObject("coop_ojo_grito"); g.transform.position = ojoPos; var au = g.AddComponent<AudioSource>(); au.clip = grito; au.spatialBlend = 0.6f; au.minDistance = 120f; au.maxDistance = 2000f; au.volume = 1f; au.Play(); Object.Destroy(g, grito.length + 1f); }
                OjoTorre.Actual.Morir();
                plugin.StartCoroutine(SangreCastillo(ojoPos, true));
            }
            if (mio && Eventos.Instancia != null) Eventos.Instancia.LunaPorArma();   // la muerte del Ojo despierta la luna de sangre
            plugin.Log("Castillo: el Ojo muere (" + (mio ? "palanca accionada aquí" : "la accionó " + plugin.NombreCompanero) + ")");
            if (mio && plugin.Conectado) { var d = Red.Armar(w => { w.Write((byte)4); }); for (int i = 0; i < 3; i++) plugin.EnviarTesoro(d); }
        }

        /// Palanca en el centro de la terraza de la Torre del Homenaje (coordenadas locales del castillo).
        void CrearPalancaOjo(Transform pa, Vector3 centroLocal)
        {
            Sonidos.Clip("ojo_muerte");   // empieza a cargar el sonido de la muerte del Ojo (se carga en segundo plano)
            var raiz = new GameObject("coop_palanca_ojo"); raiz.transform.SetParent(pa, false); raiz.transform.localPosition = centroLocal;
            Pz(raiz.transform, "coop_palanca_pedestal", PrimitiveType.Cube, new Vector3(0f, 0.45f, 0f), new Vector3(0.7f, 0.9f, 0.7f), 0f, matPiedra);
            Pz(raiz.transform, "coop_palanca_moldura", PrimitiveType.Cube, new Vector3(0f, 0.92f, 0f), new Vector3(0.82f, 0.08f, 0.82f), 0f, matPiedra, false);
            Pz(raiz.transform, "coop_palanca_placa", PrimitiveType.Cube, new Vector3(0f, 0.98f, 0f), new Vector3(0.42f, 0.05f, 0.2f), 0f, matHierroC, false);
            var piv = new GameObject("coop_palanca_eje"); piv.transform.SetParent(raiz.transform, false); piv.transform.localPosition = new Vector3(0f, 1.0f, 0f);
            piv.transform.localRotation = Quaternion.Euler(0f, 0f, LuaBool(VarOjoMuerto) ? PalancaOjo.AngDer : PalancaOjo.AngIzq);
            Pz(raiz.transform, "coop_palanca_guia", PrimitiveType.Cube, new Vector3(0f, 1.0f, 0f), new Vector3(0.5f, 0.06f, 0.08f), 0f, matHierroC, false);   // ranura por donde corre
            Pz(piv.transform, "coop_palanca_brazo", PrimitiveType.Cylinder, new Vector3(0f, 0.32f, 0f), new Vector3(0.05f, 0.32f, 0.05f), 0f, matHierroC, false);
            var pomo = Pz(piv.transform, "coop_palanca_pomo", PrimitiveType.Sphere, new Vector3(0f, 0.66f, 0f), new Vector3(0.12f, 0.12f, 0.12f), 0f, matOjoGlobo, false);
            var p = raiz.AddComponent<PalancaOjo>(); p.Eje = piv.transform;
        }

        static Texture2D TexturaIris()
        {
            const int N = 512; var t = new Texture2D(N, N, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, anisoLevel = 4 };
            var px = new Color[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float u = (x + 0.5f) / N * 2f - 1f, v = (y + 0.5f) / N * 2f - 1f;
                    float r = Mathf.Sqrt(u * u + v * v), ang = Mathf.Atan2(v, u);
                    // vetas de fuego que salen del centro (tres escalas) + chispas
                    float vetas = Mathf.PerlinNoise(ang * 3.2f + 10f, r * 6f) * 0.45f + Mathf.PerlinNoise(ang * 9f, r * 14f) * 0.35f + Mathf.PerlinNoise(ang * 24f + 3f, r * 30f) * 0.2f;
                    vetas += Mathf.Pow(Mathf.PerlinNoise(ang * 40f, r * 50f), 6f) * 1.5f;
                    Color c = Color.Lerp(new Color(1f, 0.95f, 0.55f), new Color(1f, 0.45f, 0.02f), Mathf.Clamp01(r * 1.3f));
                    c = Color.Lerp(c, new Color(0.55f, 0.06f, 0f), Mathf.Clamp01((r - 0.55f) * 2.4f));
                    c *= 0.7f + 0.6f * vetas;
                    // pupila: rendija negra vertical con borde candente
                    float rend = Mathf.Abs(u) / Mathf.Lerp(0.02f, 0.13f, Mathf.Clamp01(1f - Mathf.Abs(v) * 1.05f));
                    if (Mathf.Abs(v) < 0.92f && rend < 1f) c = Color.Lerp(new Color(0.02f, 0f, 0f), c, Mathf.SmoothStep(0.55f, 1f, rend));
                    float a = Mathf.Clamp01((1f - r) * 6f);   // borde suave del disco
                    px[y * N + x] = new Color(c.r, c.g, c.b, a);
                }
            t.SetPixels(px); t.Apply(); return t;
        }

        static Texture2D TexturaLlama()
        {
            const int N = 128; var t = new Texture2D(N, N, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color[N * N];
            for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
            {
                float u = (x + 0.5f) / N * 2f - 1f, v = (y + 0.5f) / N * 2f - 1f, r = Mathf.Sqrt(u * u + v * v);
                float a = Mathf.Clamp01(1f - r); a *= a;
                px[y * N + x] = new Color(1f, 0.6f + 0.4f * a, 0.25f * a, a);
            }
            t.SetPixels(px); t.Apply(); return t;
        }

        /// Pone el Ojo en lo alto de la Torre del Homenaje (coordenadas locales del castillo).
        void CrearOjo(Transform pa, Vector3 baseLocal)
        {
            if (matOjoIris == null)
            {
                matOjoIris = new Material(Shader.Find("Standard")) { mainTexture = TexturaIris(), color = Color.white };
                matOjoIris.SetFloat("_Mode", 1f); matOjoIris.EnableKeyword("_ALPHATEST_ON"); matOjoIris.SetFloat("_Cutoff", 0.3f); matOjoIris.renderQueue = 2450;
                matOjoIris.EnableKeyword("_EMISSION"); matOjoIris.SetTexture("_EmissionMap", matOjoIris.mainTexture); matOjoIris.SetColor("_EmissionColor", new Color(5f, 3.2f, 1.3f));
                matOjoGlobo = new Material(Shader.Find("Standard")) { color = new Color(0.35f, 0.05f, 0.01f) };
                matOjoGlobo.SetFloat("_Glossiness", 0.85f); matOjoGlobo.EnableKeyword("_EMISSION"); matOjoGlobo.SetColor("_EmissionColor", new Color(2.2f, 0.45f, 0.05f));
                var sh = Shader.Find("Particles/Additive"); if (sh == null) sh = Shader.Find("Legacy Shaders/Particles/Additive");
                matLlamaOjo = new Material(sh) { mainTexture = TexturaLlama() };
                matHaloOjo = new Material(sh) { mainTexture = TexturaLlama(), color = new Color(1f, 0.45f, 0.12f, 0.75f) };
                if (matHaloOjo.HasProperty("_TintColor")) matHaloOjo.SetColor("_TintColor", new Color(1f, 0.42f, 0.1f, 0.6f));
            }
            var raiz = new GameObject("coop_ojo_torre"); raiz.transform.SetParent(pa, false); raiz.transform.localPosition = baseLocal;
            // dos cuernos de piedra que salen del techo y se curvan hacia arriba y afuera, como garras
            foreach (float s in new[] { -1f, 1f })
            {
                var m = new Malla(); const int SEG = 14, LADOS = 8; Vector3 prev = Vector3.zero;
                var anillos = new List<int>();
                for (int i = 0; i <= SEG; i++)
                {
                    float t = i / (float)SEG;
                    Vector3 c = new Vector3(s * (1.5f + 11f * Mathf.Sin(t * 1.25f) - 3.2f * t * t), 30f * t, 0.6f * Mathf.Sin(t * 3f));   // separados: el ojo (17 m) cabe entre los dos
                    Vector3 dir = i == 0 ? Vector3.up : (c - prev).normalized; prev = c;
                    Vector3 lado = Vector3.Cross(dir, Vector3.forward).normalized, otro = Vector3.Cross(lado, dir);
                    float rad = Mathf.Lerp(2f, 0.08f, Mathf.Pow(t, 0.8f));
                    int b0 = m.Cuenta;
                    for (int k = 0; k <= LADOS; k++) { float a = k * Mathf.PI * 2f / LADOS; float rr = rad * (1f + 0.12f * Mathf.Sin(k * 3f + i)); m.V(c + (lado * Mathf.Cos(a) + otro * Mathf.Sin(a)) * rr, new Vector2(k / (float)LADOS, t * 4f)); }
                    anillos.Add(b0);
                }
                for (int i = 0; i < SEG; i++) for (int k = 0; k < LADOS; k++)
                {
                    int a = anillos[i] + k, b = anillos[i + 1] + k;
                    m.Tri(a, b, a + 1); m.Tri(a + 1, b, b + 1);
                }
                m.Crear("coop_ojo_cuerno", raiz.transform, matPiedra, false);
            }
            // el ojo: globo candente achatado, el iris de fuego con su rendija al frente y un halo de llamas
            var globo = new GameObject("coop_ojo_globo"); globo.transform.SetParent(raiz.transform, false); globo.transform.localPosition = new Vector3(0f, 19f, 0f);
            var esfera = GameObject.CreatePrimitive(PrimitiveType.Sphere); Object.Destroy(esfera.GetComponent<Collider>());
            esfera.name = "coop_ojo_esfera"; esfera.transform.SetParent(globo.transform, false); esfera.transform.localScale = new Vector3(17f, 9.8f, 7.5f);
            esfera.GetComponent<Renderer>().sharedMaterial = matOjoGlobo; esfera.AddComponent<NoSolido>();
            var iris = GameObject.CreatePrimitive(PrimitiveType.Quad); Object.Destroy(iris.GetComponent<Collider>());
            iris.name = "coop_ojo_iris"; iris.transform.SetParent(globo.transform, false); iris.transform.localPosition = new Vector3(0f, 0f, 3.78f);
            iris.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); iris.transform.localScale = new Vector3(15.8f, 9.4f, 1f);
            iris.GetComponent<Renderer>().sharedMaterial = matOjoIris; iris.AddComponent<NoSolido>();
            // llamas alrededor del ojo
            var fuego = new GameObject("coop_ojo_llamas"); fuego.transform.SetParent(globo.transform, false);
            var ps = fuego.AddComponent<ParticleSystem>();
            var main = ps.main; main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.6f); main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 4f);
            main.startSize = new ParticleSystem.MinMaxCurve(4.5f, 9f); main.maxParticles = 900; main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.55f, 0.1f, 0.8f), new Color(1f, 0.25f, 0.02f, 0.6f));
            var em = ps.emission; em.rateOverTime = 240f;
            var shp = ps.shape; shp.shapeType = ParticleSystemShapeType.Sphere; shp.radius = 6.8f;
            var col = ps.colorOverLifetime; col.enabled = true;
            var gr = new Gradient(); gr.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.8f, 0.3f), 0f), new GradientColorKey(new Color(1f, 0.3f, 0.02f), 0.5f), new GradientColorKey(new Color(0.3f, 0.05f, 0f), 1f) },
                                       new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.15f), new GradientAlphaKey(0f, 1f) });
            col.color = gr;
            var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.3f));
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.y = new ParticleSystem.MinMaxCurve(2f, 4f);
            var pr = fuego.GetComponent<ParticleSystemRenderer>(); pr.sharedMaterial = matLlamaOjo;
            // resplandor grande que mira a la cámara: el Ojo se ve brillar desde lejos a través de la niebla
            var halo = GameObject.CreatePrimitive(PrimitiveType.Quad); Object.Destroy(halo.GetComponent<Collider>());
            halo.name = "coop_ojo_halo"; halo.transform.SetParent(raiz.transform, false); halo.transform.position = globo.transform.position;
            halo.GetComponent<Renderer>().sharedMaterial = matHaloOjo; halo.AddComponent<NoSolido>();
            // anillo de llamas alrededor del borde del ojo (gira con él)
            var anillo = new GameObject("coop_ojo_corona"); anillo.transform.SetParent(globo.transform, false); anillo.transform.localPosition = new Vector3(0f, 0f, 1.5f);
            var pa2 = anillo.AddComponent<ParticleSystem>(); var m2 = pa2.main;
            m2.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.1f); m2.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 2f); m2.startSize = new ParticleSystem.MinMaxCurve(2f, 4.5f);
            m2.maxParticles = 800; m2.simulationSpace = ParticleSystemSimulationSpace.Local; m2.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.75f, 0.25f, 0.9f), new Color(1f, 0.35f, 0.05f, 0.7f));
            var e2 = pa2.emission; e2.rateOverTime = 420f;
            var s2 = pa2.shape; s2.shapeType = ParticleSystemShapeType.CircleEdge; s2.radius = 8.2f;   // el círculo queda en el plano XY: el borde del ojo, que mira hacia +Z
            var c2 = pa2.colorOverLifetime; c2.enabled = true; c2.color = gr;
            anillo.GetComponent<ParticleSystemRenderer>().sharedMaterial = matLlamaOjo;
            var luzGo = new GameObject("coop_ojo_luz"); luzGo.transform.SetParent(globo.transform, false);
            var l = luzGo.AddComponent<Light>(); l.type = LightType.Point; l.color = new Color(1f, 0.45f, 0.1f); l.range = 140f; l.intensity = 4f; l.shadows = LightShadows.None;
            var o = raiz.AddComponent<OjoTorre>(); o.Globo = globo.transform; o.Halo = halo.transform;
            if (LuaBool(VarOjoMuerto)) { globo.SetActive(false); halo.SetActive(false); luzGo.SetActive(false); o.enabled = false; plugin.StartCoroutine(SangreCastillo(globo.transform.position, false)); }   // el castillo sigue manchado de sangre
            plugin.Log("Castillo: el Ojo en lo alto de la Torre del Homenaje (alcance " + OjoTorre.Alcance + " m)");
        }
    }

    /// La palanca del Ojo: E cerca (a menos de 2,5 m) la baja y el Ojo muere.
    public class PalancaOjo : MonoBehaviour
    {
        public Transform Eje; bool bajando; float t0; GUIStyle estilo;
        void Start()
        {
            // apoyarla en lo que haya justo debajo (el pilar central de la terraza): sube o baja hasta 3 m
            var propias = GetComponentsInChildren<Collider>(); foreach (var c in propias) c.enabled = false;
            RaycastHit h;
            if (Physics.Raycast(transform.position + Vector3.up * 3f, Vector3.down, out h, 6f, ~(1 << 2), QueryTriggerInteraction.Ignore))
            { transform.position = new Vector3(transform.position.x, h.point.y, transform.position.z); Escopeta.plugin.Log("Castillo: palanca del Ojo apoyada en " + h.collider.name + " a " + h.point.y.ToString("0.0")); }
            else Escopeta.plugin.Log("Castillo: palanca del Ojo sin apoyo debajo");
            foreach (var c in propias) c.enabled = true;
        }
        void Update()
        {
            var pl = Escopeta.plugin; if (pl == null || Eje == null) return;
            if (bajando)
            {
                // de izquierda a derecha: arranca con esfuerzo, se suelta de golpe y rebota un poco al llegar al tope
                float t = Mathf.Clamp01((Time.time - t0) / 0.75f);
                float k = t < 0.35f ? Mathf.Lerp(0f, 0.12f, t / 0.35f) : 0.12f + 0.88f * Mathf.Pow((t - 0.35f) / 0.65f, 2f);
                float reb = t >= 1f ? 0f : 0f;
                float ang = Mathf.Lerp(AngIzq, AngDer, k) + reb;
                if (t >= 1f) { float dt = Time.time - t0 - 0.75f; ang = AngDer + 6f * Mathf.Exp(-6f * dt) * Mathf.Sin(dt * 22f); if (dt > 1f) { bajando = false; ang = AngDer; } }
                Eje.localRotation = Quaternion.Euler(0f, 0f, ang);
                return;
            }
            if (Cerca() && Mundo.LuaBoolPublico(Mundo.VarOjoMuerto) == false && Controles.Interactuar() && Entrada.Enfocado)
            {
                Bajar();
                pl.mundo.MatarOjo(true);
            }
        }
        bool Cerca() { var j = Escopeta.plugin != null ? Escopeta.plugin.Jugador : null; return j != null && Vector3.Distance(j.transform.position, transform.position + Vector3.up * 0.9f) < 2.6f; }
        public const float AngIzq = 45f, AngDer = -60f;   // grados sobre el eje Z: + inclina a la izquierda, − a la derecha
        public void Bajar()
        {
            if (bajando) return;
            bajando = true; t0 = Time.time;
            var c = SonidosOjo.Palanca(); if (c != null) AudioSource.PlayClipAtPoint(c, transform.position + Vector3.up, 1f);
        }
        void OnGUI()
        {
            if (!Cerca() || Mundo.LuaBoolPublico(Mundo.VarOjoMuerto)) return;
            if (estilo == null) estilo = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 22, fontStyle = FontStyle.Bold, normal = { textColor = new Color(1f, 0.85f, 0.4f) } };
            GUI.Label(new Rect(0, Screen.height * 0.6f, Screen.width, 30f), "E: para usar la palanca", estilo);
        }
    }
}

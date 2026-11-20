// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using UnityEngine;

namespace WitchHuntCoop
{
    /// Fuego realista (pedido 2026-11-17: "que no se vean como simples bolas amarillas"): varias capas de partículas con forma de lengua de
    /// fuego, núcleo casi blanco, cuerpo naranja que se apaga en rojo, turbulencia que lo hace bailar, chispas que suben y un poco de humo.
    public static class Llama
    {
        static Texture2D texLengua; static Material matFuego, matHumo;

        /// Lengua de fuego: gota alargada hacia arriba, más intensa abajo y en el centro.
        static Texture2D TexLengua()
        {
            if (texLengua != null) return texLengua;
            const int W = 64, H = 128;
            texLengua = new Texture2D(W, H, TextureFormat.RGBA32, true) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < H; y++) for (int x = 0; x < W; x++)
            {
                float u = (x + 0.5f) / W * 2f - 1f, v = (y + 0.5f) / H;          // v: 0 abajo, 1 arriba
                float ancho = Mathf.Lerp(0.95f, 0.05f, Mathf.Pow(v, 0.75f)) * (v < 0.12f ? Mathf.Sqrt(v / 0.12f) : 1f);
                float d = Mathf.Abs(u) / Mathf.Max(0.01f, ancho);
                float a = Mathf.Clamp01(1f - d); a = a * a * (3f - 2f * a);
                a *= Mathf.Clamp01(1.15f - v) * (0.8f + 0.2f * Mathf.PerlinNoise(x * 0.18f, y * 0.09f));
                texLengua.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            texLengua.Apply();
            return texLengua;
        }

        static Material MatFuego() { if (matFuego == null) { matFuego = Potenciado.MaterialBrillo(TexLengua(), Color.white); matFuego.renderQueue = ColaFuego; } return matFuego; }
        /// El cielo/las nubes del juego (Time of Day) se dibujan después de los transparentes normales y tapaban el fuego donde detrás solo hay cielo
        /// (los faroles se veían vacíos): el fuego se dibuja más tarde.
        const int ColaFuego = 3300;
        static Material matHalo;
        static Material MatHalo() { if (matHalo == null) { matHalo = Potenciado.MaterialGas(); matHalo.renderQueue = ColaFuego; } return matHalo; }

        static Material matFuegoSolido;
        /// Fuego opaco (mezcla alfa): el aditivo desaparece contra la niebla clara del cielo (los faroles parecían vacíos).
        static Material MatFuegoSolido()
        {
            if (matFuegoSolido != null) return matFuegoSolido;
            var sh = Shader.Find("Legacy Shaders/Particles/Alpha Blended") ?? Shader.Find("Particles/Alpha Blended");
            if (sh == null) return MatFuego();
            matFuegoSolido = new Material(sh) { name = "coop_fuego_solido", mainTexture = TexLengua() };
            if (matFuegoSolido.HasProperty("_TintColor")) matFuegoSolido.SetColor("_TintColor", new Color(0.5f, 0.5f, 0.5f, 0.5f));
            matFuegoSolido.renderQueue = ColaFuego;
            return matFuegoSolido;
        }

        static Material MatHumo()
        {
            if (matHumo != null) return matHumo;
            var sh = Shader.Find("Legacy Shaders/Particles/Alpha Blended") ?? Shader.Find("Particles/Alpha Blended") ?? Shader.Find("Sprites/Default");
            matHumo = new Material(sh) { name = "coop_humo" };
            var bruma = Potenciado.MaterialGas();
            if (bruma != null && bruma.mainTexture != null) matHumo.mainTexture = bruma.mainTexture;
            return matHumo;
        }

        static ParticleSystem Capa(Transform padre, string nombre, Vector3 local, Material mat, ParticleSystemRenderMode modo)
        {
            var go = new GameObject(nombre); go.transform.SetParent(padre, false); go.transform.localPosition = local;
            go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);   // el cono emite por el eje Z del objeto: apuntando hacia arriba (antes salía de lado)
            var ps = go.AddComponent<ParticleSystem>();
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.material = mat; r.renderMode = modo; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            return ps;
        }

        static Gradient Grad(Color[] c, float[] tc, float[] a, float[] ta)
        {
            var g = new Gradient();
            var ck = new GradientColorKey[c.Length]; for (int i = 0; i < c.Length; i++) ck[i] = new GradientColorKey(c[i], tc[i]);
            var ak = new GradientAlphaKey[a.Length]; for (int i = 0; i < a.Length; i++) ak[i] = new GradientAlphaKey(a[i], ta[i]);
            g.SetKeys(ck, ak); return g;
        }

        /// e = tamaño (1 ≈ llama de fogata de medio metro). humo/chispas: capas extra (no en faroles ni velas).
        public static GameObject Crear(Vector3 mundo, Transform padre, float e, bool humo, bool chispas)
        {
            var raiz = new GameObject("coop_llama");
            if (padre != null) raiz.transform.SetParent(padre, true);
            raiz.transform.position = mundo; raiz.transform.rotation = Quaternion.identity;
            // escala del mundo del padre (el castillo está agrandado): las partículas se dimensionan en metros reales
            float k = e;
            // cuerpo: lenguas naranjas que bailan y se apagan en rojo
            var cuerpo = Capa(raiz.transform, "cuerpo", Vector3.zero, MatFuegoSolido(), ParticleSystemRenderMode.Billboard);
            {
                var m = cuerpo.main; m.loop = true; m.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.8f); m.startSpeed = new ParticleSystem.MinMaxCurve(0.35f * k, 0.75f * k);
                m.startSize = new ParticleSystem.MinMaxCurve(0.32f * k, 0.55f * k); m.startRotation = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);
                m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = 60; m.scalingMode = ParticleSystemScalingMode.Shape;
                var em = cuerpo.emission; em.rateOverTime = 26f;
                var sh = cuerpo.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 6f; sh.radius = 0.1f * k;
                var col = cuerpo.colorOverLifetime; col.enabled = true;
                // colores siempre encendidos (amarillo → naranja → rojo vivo) y se apaga por transparencia: oscurecer hacia el negro se veía como una caja oscura
                col.color = Grad(new[] { new Color(1f, 0.82f, 0.4f), new Color(1f, 0.55f, 0.12f), new Color(1f, 0.32f, 0.05f), new Color(0.9f, 0.18f, 0.03f) }, new[] { 0f, 0.3f, 0.65f, 1f },
                                 new[] { 0f, 0.9f, 0.55f, 0f }, new[] { 0f, 0.1f, 0.55f, 1f });
                var so = cuerpo.sizeOverLifetime; so.enabled = true; so.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.7f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0.25f)));
                var no = cuerpo.noise; no.enabled = true; no.strength = 0.35f * k; no.frequency = 1.6f; no.scrollSpeed = 1.3f; no.octaveCount = 2; no.damping = true;
            }
            if (k < 0.25f) { raiz.AddComponent<LlamaCercana>(); return raiz; }   // velas: una sola capa (los candelabros llevan 8 cada uno)
            // núcleo: casi blanco, pequeño y rápido, abajo
            var nucleo = Capa(raiz.transform, "nucleo", Vector3.zero, MatFuegoSolido(), ParticleSystemRenderMode.Billboard);
            {
                var m = nucleo.main; m.loop = true; m.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.42f); m.startSpeed = new ParticleSystem.MinMaxCurve(0.25f * k, 0.5f * k);
                m.startSize = new ParticleSystem.MinMaxCurve(0.18f * k, 0.3f * k);
                m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = 40; m.scalingMode = ParticleSystemScalingMode.Shape;
                var em = nucleo.emission; em.rateOverTime = 30f;
                var sh = nucleo.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 3f; sh.radius = 0.05f * k;
                var col = nucleo.colorOverLifetime; col.enabled = true;
                col.color = Grad(new[] { new Color(1f, 0.97f, 0.8f), new Color(1f, 0.88f, 0.5f), new Color(1f, 0.62f, 0.2f) }, new[] { 0f, 0.4f, 1f },
                                 new[] { 0f, 1f, 0f }, new[] { 0f, 0.18f, 1f });
                var so = nucleo.sizeOverLifetime; so.enabled = true; so.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.35f)));
                var no = nucleo.noise; no.enabled = true; no.strength = 0.12f * k; no.frequency = 2.4f; no.scrollSpeed = 2f;
            }
            // halo: resplandor cálido y suave alrededor del fuego (se lee como llama también de lejos)
            {
                var ha = Capa(raiz.transform, "halo", Vector3.up * 0.18f * k, MatHalo(), ParticleSystemRenderMode.Billboard);
                var m = ha.main; m.loop = true; m.startLifetime = 0.5f; m.startSpeed = 0f; m.startSize = new ParticleSystem.MinMaxCurve(0.8f * k, 1.0f * k);
                m.startColor = new Color(1f, 0.45f, 0.12f, 0.13f); m.simulationSpace = ParticleSystemSimulationSpace.Local; m.maxParticles = 6;
                var em = ha.emission; em.rateOverTime = 8f;
                var sh = ha.shape; sh.enabled = false;
                var col = ha.colorOverLifetime; col.enabled = true;
                col.color = Grad(new[] { Color.white, Color.white }, new[] { 0f, 1f }, new[] { 0f, 1f, 0f }, new[] { 0f, 0.5f, 1f });
            }
            if (chispas)
            {
                var ch = Capa(raiz.transform, "chispas", Vector3.up * 0.1f * k, MatFuego(), ParticleSystemRenderMode.Billboard);
                var m = ch.main; m.loop = true; m.startLifetime = new ParticleSystem.MinMaxCurve(1f, 2f); m.startSpeed = new ParticleSystem.MinMaxCurve(0.7f * k, 1.4f * k);
                m.startSize = new ParticleSystem.MinMaxCurve(0.025f * k, 0.05f * k); m.startColor = new Color(1f, 0.6f, 0.2f, 1f);
                m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = 20;
                var em = ch.emission; em.rateOverTime = 5f;
                var sh = ch.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 18f; sh.radius = 0.12f * k;
                var col = ch.colorOverLifetime; col.enabled = true;
                col.color = Grad(new[] { new Color(1f, 0.8f, 0.4f), new Color(1f, 0.3f, 0.05f) }, new[] { 0f, 1f }, new[] { 1f, 1f, 0f }, new[] { 0f, 0.7f, 1f });
                var no = ch.noise; no.enabled = true; no.strength = 0.6f * k; no.frequency = 1.1f; no.scrollSpeed = 0.8f;
            }
            if (humo)
            {
                var hu = Capa(raiz.transform, "humo", Vector3.up * 0.45f * k, MatHumo(), ParticleSystemRenderMode.Billboard);
                var m = hu.main; m.loop = true; m.startLifetime = new ParticleSystem.MinMaxCurve(2f, 3.2f); m.startSpeed = new ParticleSystem.MinMaxCurve(0.35f * k, 0.6f * k);
                m.startSize = new ParticleSystem.MinMaxCurve(0.35f * k, 0.6f * k); m.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
                m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = 18;
                var em = hu.emission; em.rateOverTime = 5f;
                var sh = hu.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 8f; sh.radius = 0.08f * k;
                var col = hu.colorOverLifetime; col.enabled = true;
                col.color = Grad(new[] { new Color(0.16f, 0.14f, 0.13f), new Color(0.11f, 0.11f, 0.11f) }, new[] { 0f, 1f }, new[] { 0f, 0.22f, 0f }, new[] { 0f, 0.25f, 1f });
                var so = hu.sizeOverLifetime; so.enabled = true; so.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(1f, 2.4f)));
                var no = hu.noise; no.enabled = true; no.strength = 0.25f * k; no.frequency = 0.6f; no.scrollSpeed = 0.4f;
            }
            raiz.AddComponent<LlamaCercana>();
            return raiz;
        }
    }

    /// Rendimiento: el fuego de lejos (más de 90 m de la cámara) no se ve y sus partículas se simulan igual cada cuadro; se apagan sus capas
    /// (la luz sigue encendida aparte) y vuelven a encenderse al acercarse.
    public class LlamaCercana : MonoBehaviour
    {
        const float Lejos = 90f;
        float prox; bool encendida = true;
        void Update()
        {
            if (Time.time < prox) return;
            prox = Time.time + 0.5f + Random.value * 0.2f;
            var cam = Camera.main; if (cam == null) return;
            bool cerca = (cam.transform.position - transform.position).sqrMagnitude < Lejos * Lejos;
            if (cerca == encendida) return;
            encendida = cerca;
            foreach (Transform hijo in transform) hijo.gameObject.SetActive(cerca);
        }
    }

    /// Sistema de partículas que solo se dibuja de cerca (chorros de la fuente del jardín).
    public class SoloCerca : MonoBehaviour
    {
        const float Lejos = 80f;
        float prox; ParticleSystem ps; ParticleSystemRenderer rd;
        void Start() { ps = GetComponent<ParticleSystem>(); rd = GetComponent<ParticleSystemRenderer>(); }
        void Update()
        {
            if (Time.time < prox) return;
            prox = Time.time + 0.5f;
            var cam = Camera.main; if (cam == null || ps == null) return;
            bool cerca = (cam.transform.position - transform.position).sqrMagnitude < Lejos * Lejos;
            if (rd != null && rd.enabled != cerca) rd.enabled = cerca;
            var em = ps.emission; if (em.enabled != cerca) { em.enabled = cerca; if (!cerca) ps.Clear(); }
        }
    }
}
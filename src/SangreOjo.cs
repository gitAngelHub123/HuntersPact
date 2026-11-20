// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WitchHuntCoop
{
    /// Sonidos de la muerte del Ojo (pedido 2026-11-18): el grito de dolor y el golpe de la palanca. Se busca uno parecido entre
    /// los sonidos cargados del juego; si no hay, se genera uno (para que nunca falte).
    public static class SonidosOjo
    {
        static AudioClip grito, palanca; static bool buscado;

        static void Buscar()
        {
            if (buscado) return; buscado = true;
            string[] cGrito = { "scream", "shriek", "screech", "roar", "howl", "wail" };
            string[] cPalanca = { "lever", "switch", "latch", "mechanism", "gate_open", "gate", "chain", "unlock" };
            int mejorG = 99, mejorP = 99; var lista = new System.Text.StringBuilder();
            foreach (var ac in Resources.FindObjectsOfTypeAll<AudioClip>())
            {
                if (ac == null || ac.length < 0.2f) continue; string n = ac.name.ToLowerInvariant();
                for (int k = 0; k < cGrito.Length; k++) if (n.Contains(cGrito[k]) && ac.length > 0.8f) { lista.Append(ac.name).Append(' '); if (k < mejorG) { mejorG = k; grito = ac; } break; }
                for (int k = 0; k < cPalanca.Length; k++) if (n.Contains(cPalanca[k]) && ac.length < 3f) { lista.Append(ac.name).Append(' '); if (k < mejorP) { mejorP = k; palanca = ac; } break; }
            }
            if (grito == null) grito = GritoGenerado();
            if (palanca == null) palanca = PalancaGenerada();
            Escopeta.plugin.Log("Ojo: grito = " + grito.name + ", palanca = " + palanca.name + " | candidatos del juego: " + lista);
        }
        public static AudioClip Grito() { Buscar(); return grito; }
        public static AudioClip Palanca() { Buscar(); return palanca; }

        /// Alarido largo: una voz que sube y se quiebra hacia abajo, con armónicos ásperos, temblor y un rugido grave debajo.
        static AudioClip GritoGenerado()
        {
            const int SR = 44100; float dur = 3.2f; int n = (int)(SR * dur); var d = new float[n]; var r = new System.Random(7);
            float fase = 0f, faseB = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR, u = t / dur;
                float f = (u < 0.15f ? Mathf.Lerp(380f, 720f, u / 0.15f) : Mathf.Lerp(720f, 170f, Mathf.Pow((u - 0.15f) / 0.85f, 0.7f))) * (1f + 0.035f * Mathf.Sin(t * 38f) + 0.02f * Mathf.Sin(t * 7f));
                fase += f / SR; faseB += (f * 0.25f) / SR;
                float voz = 0f; for (int h = 1; h <= 7; h++) voz += Mathf.Sin(fase * Mathf.PI * 2f * h) / (h * 0.9f);
                voz = (float)System.Math.Tanh(voz * 1.8f);                                         // áspero
                float ruido = (float)(r.NextDouble() * 2.0 - 1.0);
                float rug = Mathf.Sin(faseB * Mathf.PI * 2f) * 0.6f + ruido * 0.25f;
                float env = Mathf.Clamp01(t / 0.08f) * Mathf.Clamp01((dur - t) / 0.9f);
                d[i] = (voz * 0.55f + ruido * 0.18f * (0.4f + u) + rug * 0.35f) * env * 0.8f;
            }
            var c = AudioClip.Create("coop_grito_ojo", n, 1, SR, false); c.SetData(d, 0); return c;
        }

        /// Palanca de hierro: chirrido corto al arrancar y un golpe metálico seco al llegar al tope.
        static AudioClip PalancaGenerada()
        {
            const int SR = 44100; float dur = 1.1f; int n = (int)(SR * dur); var d = new float[n]; var r = new System.Random(3);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR; float s = 0f;
                if (t < 0.55f) { float f = 900f + 300f * Mathf.Sin(t * 30f); s += Mathf.Sin(t * f * 6.283f) * 0.15f * Mathf.Clamp01(t / 0.05f) * (1f - t / 0.55f) * (0.5f + 0.5f * Mathf.Sin(t * 120f)); }   // chirrido
                float tg = t - 0.62f;
                if (tg > 0f)
                {
                    float e = Mathf.Exp(-tg * 14f);
                    s += (Mathf.Sin(tg * 95f * 6.283f) * 0.8f + Mathf.Sin(tg * 410f * 6.283f) * 0.35f + Mathf.Sin(tg * 1330f * 6.283f) * 0.2f * Mathf.Exp(-tg * 30f)) * e;   // golpe y resonancia del hierro
                    s += (float)(r.NextDouble() * 2.0 - 1.0) * 0.5f * Mathf.Exp(-tg * 60f);
                }
                d[i] = s * 0.9f;
            }
            var c = AudioClip.Create("coop_palanca", n, 1, SR, false); c.SetData(d, 0); return c;
        }
    }

    public partial class Mundo
    {
        static Material matSangreSplat, matGotaSangre;
        GameObject sangreCastillo;

        static Texture2D TexturaSalpicadura()
        {
            const int N = 256; var t = new Texture2D(N, N, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color[N * N]; var r = new System.Random(66);
            var manchas = new List<Vector3>();   // x, y, radio
            manchas.Add(new Vector3(0.5f, 0.5f, 0.22f));
            for (int k = 0; k < 14; k++) { float a = (float)r.NextDouble() * 6.28f, d = 0.12f + (float)r.NextDouble() * 0.2f; manchas.Add(new Vector3(0.5f + Mathf.Cos(a) * d, 0.5f + Mathf.Sin(a) * d, 0.04f + (float)r.NextDouble() * 0.08f)); }
            for (int k = 0; k < 40; k++) { float a = (float)r.NextDouble() * 6.28f, d = 0.25f + (float)r.NextDouble() * 0.22f; manchas.Add(new Vector3(0.5f + Mathf.Cos(a) * d, 0.5f + Mathf.Sin(a) * d, 0.006f + (float)r.NextDouble() * 0.02f)); }
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float u = (x + 0.5f) / N, v = (y + 0.5f) / N, cob = 0f;
                    foreach (var m in manchas) { float dd = Vector2.Distance(new Vector2(u, v), new Vector2(m.x, m.y)) / m.z; cob = Mathf.Max(cob, 1f - dd); }
                    cob += (Mathf.PerlinNoise(u * 14f, v * 14f) - 0.5f) * 0.35f;
                    float a = Mathf.Clamp01(cob * 4f);
                    float oscuro = 0.55f + 0.45f * Mathf.PerlinNoise(u * 6f + 3f, v * 6f);   // sangre seca más oscura en partes
                    px[y * N + x] = new Color(0.32f * oscuro, 0.01f, 0.015f, a);
                }
            t.SetPixels(px); t.Apply(); return t;
        }

        static void MaterialesSangre()
        {
            if (matSangreSplat != null) return;
            matSangreSplat = new Material(Shader.Find("Standard")) { name = "coop_sangre_salpicadura", mainTexture = TexturaSalpicadura(), color = Color.white };
            matSangreSplat.SetFloat("_Mode", 1f); matSangreSplat.SetOverrideTag("RenderType", "TransparentCutout"); matSangreSplat.EnableKeyword("_ALPHATEST_ON");
            matSangreSplat.SetFloat("_Cutoff", 0.5f); matSangreSplat.renderQueue = 2460; matSangreSplat.SetFloat("_Glossiness", 0.82f);
            var sh = Shader.Find("Particles/Alpha Blended"); if (sh == null) sh = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
            var gt = new Texture2D(32, 32, TextureFormat.RGBA32, false); var gp = new Color[32 * 32];
            for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++) { float u = (x + 0.5f) / 16f - 1f, v = (y + 0.5f) / 16f - 1f; float a = Mathf.Clamp01(1f - Mathf.Sqrt(u * u + v * v)); gp[y * 32 + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(a * 2.5f)); }
            gt.SetPixels(gp); gt.Apply();
            matGotaSangre = new Material(sh) { mainTexture = gt };
            if (matGotaSangre.HasProperty("_TintColor")) matGotaSangre.SetColor("_TintColor", new Color(0.45f, 0.02f, 0.02f, 0.9f));
        }

        static ParticleSystem Particulas(string nombre, Vector3 pos, float vida0, float vida1, float vel0, float vel1, float tam0, float tam1, int max, float grav)
        {
            var g = new GameObject(nombre); g.transform.position = pos;
            var ps = g.AddComponent<ParticleSystem>(); var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(vida0, vida1); main.startSpeed = new ParticleSystem.MinMaxCurve(vel0, vel1);
            main.startSize = new ParticleSystem.MinMaxCurve(tam0, tam1); main.maxParticles = max; main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = grav; main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.55f, 0.02f, 0.02f, 1f), new Color(0.3f, 0f, 0.01f, 1f));
            var col = ps.collision; col.enabled = true; col.type = ParticleSystemCollisionType.World; col.bounce = 0f; col.lifetimeLoss = 1f; col.quality = ParticleSystemCollisionQuality.Low;
            g.GetComponent<ParticleSystemRenderer>().sharedMaterial = matGotaSangre;
            return ps;
        }

        /// La muerte del Ojo: chorro de sangre que revienta del ojo, lluvia de sangre sobre el castillo y, al caer, manchas por todo el
        /// castillo (suelos, tejados, muros y el jardín). Con 'vivo' = false (el Ojo ya estaba muerto al cargar) solo las manchas.
        IEnumerator SangreCastillo(Vector3 ojo, bool vivo)
        {
            MaterialesSangre();
            if (vivo)
            {
                var chorro = Particulas("coop_sangre_chorro", ojo, 2.5f, 6f, 18f, 48f, 0.35f, 1.4f, 6000, 1f);
                var em = chorro.emission; em.rateOverTime = 0f; em.SetBursts(new[] { new ParticleSystem.Burst(0f, 2500), new ParticleSystem.Burst(0.4f, 1800), new ParticleSystem.Burst(0.9f, 1500) });
                var sh = chorro.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 6f;
                var m = chorro.main; m.loop = false; m.duration = 1.2f;
                Object.Destroy(chorro.gameObject, 10f);
                var lluvia = Particulas("coop_sangre_lluvia", ojo + Vector3.up * 30f, 3f, 5f, 0f, 2f, 0.15f, 0.45f, 9000, 1.6f);
                var el = lluvia.emission; el.rateOverTime = 2200f;
                var sl = lluvia.shape; sl.shapeType = ParticleSystemShapeType.Box; sl.box = new Vector3(300f, 1f, 300f);
                lluvia.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.Stretch;
                lluvia.GetComponent<ParticleSystemRenderer>().lengthScale = 4f;
                var ml = lluvia.main; ml.loop = true; ml.duration = 1f;
                Object.Destroy(lluvia.gameObject, 22f);
                lluvia.gameObject.AddComponent<ApagarEn>().Segundos = 16f;
                yield return new WaitForSeconds(1.5f);
            }
            // el castillo debe estar encendido para que los rayos choquen con él
            while (castilloRaiz != null && !castilloRaiz.activeInHierarchy) yield return new WaitForSeconds(1f);
            if (castilloRaiz == null) yield break;
            if (sangreCastillo != null) Object.Destroy(sangreCastillo);
            sangreCastillo = new GameObject("coop_sangre_castillo"); sangreCastillo.transform.SetParent(castilloRaiz.transform, true);
            var r = new System.Random(20261118); var malla = new Malla(); int n = 0, bloque = 0; RaycastHit h;
            System.Action<Vector3, Vector3, float, float> mancha = (p, nor, tam, estirar) =>
            {
                Vector3 t1 = Vector3.Cross(nor, Mathf.Abs(nor.y) > 0.9f ? Vector3.forward : Vector3.up).normalized, t2 = Vector3.Cross(nor, t1);
                float giro = (float)r.NextDouble() * 6.28f; Vector3 a1 = (t1 * Mathf.Cos(giro) + t2 * Mathf.Sin(giro)) * tam / 2f, a2 = Vector3.Cross(nor, a1);
                if (estirar > 1f) { a1 = Vector3.down * tam * estirar / 2f; a2 = Vector3.Cross(nor, Vector3.down).normalized * tam / 2f; }   // en los muros, chorreando hacia abajo
                Vector3 c = p + nor * (0.03f + 0.002f * (n % 7));
                int i0 = malla.V(c - a1 - a2, new Vector2(0f, 0f)), i1 = malla.V(c + a1 - a2, new Vector2(1f, 0f)), i2 = malla.V(c + a1 + a2, new Vector2(1f, 1f)), i3 = malla.V(c - a1 + a2, new Vector2(0f, 1f));
                malla.Tri(i0, i2, i1); malla.Tri(i0, i3, i2);
                n++;
                if (malla.Cuenta > 60000) { malla.CrearMundo("coop_sangre_" + (bloque++), sangreCastillo.transform, matSangreSplat, false); malla = new Malla(); }
            };
            int mascara = ~((1 << 2) | (1 << 11) | (1 << 27));
            // desde arriba: suelos, patios, tejados, escaleras y el jardín (radio 170 m alrededor del Ojo)
            for (int k = 0; k < 2600; k++)
            {
                float a = (float)r.NextDouble() * 6.28f, d = Mathf.Sqrt((float)r.NextDouble()) * 170f;
                Vector3 o = new Vector3(ojo.x + Mathf.Cos(a) * d, ojo.y + 40f, ojo.z + Mathf.Sin(a) * d);
                if (!Physics.Raycast(o, Vector3.down, out h, 320f, mascara, QueryTriggerInteraction.Ignore)) continue;
                if (h.collider is TerrainCollider && d > 150f) continue;
                mancha(h.point, h.normal, 1.2f + (float)r.NextDouble() * (d < 60f ? 6f : 3.5f), 1f);
                if (k % 200 == 0) yield return null;
            }
            // contra los muros y torres: rayos horizontales desde el Ojo y desde puntos del patio
            for (int k = 0; k < 900; k++)
            {
                float a = (float)r.NextDouble() * 6.28f, d = (float)r.NextDouble() * 90f;
                Vector3 o = new Vector3(ojo.x + Mathf.Cos(a) * d, ojo.y - 5f - (float)r.NextDouble() * 85f, ojo.z + Mathf.Sin(a) * d);
                Vector3 dir = new Vector3(Mathf.Cos(a + (float)r.NextDouble() * 2f - 1f), -0.15f, Mathf.Sin(a + (float)r.NextDouble() * 2f - 1f)).normalized;
                if (!Physics.Raycast(o, dir, out h, 120f, mascara, QueryTriggerInteraction.Ignore)) continue;
                if (Mathf.Abs(h.normal.y) > 0.5f) { mancha(h.point, h.normal, 1f + (float)r.NextDouble() * 2.5f, 1f); continue; }
                mancha(h.point, h.normal, 0.8f + (float)r.NextDouble() * 1.8f, 2.2f + (float)r.NextDouble() * 2f);
                if (k % 200 == 0) yield return null;
            }
            if (malla.Cuenta > 0) malla.CrearMundo("coop_sangre_" + bloque, sangreCastillo.transform, matSangreSplat, false);
            plugin.Log("Castillo: " + n + " manchas de sangre por todo el castillo (muerte del Ojo)");
        }
    }

    /// Deja de emitir partículas a los N segundos (las que ya cayeron terminan su vida).
    public class ApagarEn : MonoBehaviour
    {
        public float Segundos = 10f; float t0;
        void Start() { t0 = Time.time; }
        void Update() { if (Time.time - t0 > Segundos) { foreach (var p in GetComponentsInChildren<ParticleSystem>()) { var e = p.emission; e.enabled = false; } enabled = false; } }
    }
}

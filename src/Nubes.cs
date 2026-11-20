// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using System.Collections.Generic;
using UnityEngine;

namespace WitchHuntCoop
{
    public partial class Mundo
    {
        /// Nubarrones violeta justo encima del castillo, con tormenta eléctrica permanente (rayos que caen en la montaña y el laberinto).
        void ConstruirNubesCastillo(Transform pa, TerrainData dn)
        {
            float yCima = SueloT(dn, CentroMonte.x, CentroMonte.y);
            float yNube = yCima + 150f;   // la torre llega a unos 80 m sobre la cima
            var raiz = new GameObject("coop_nubes_castillo"); raiz.transform.SetParent(pa, false);
            raiz.transform.localPosition = new Vector3(CentroMonte.x, yNube, CentroMonte.y);
            var capas = new[] { new { dy = 0f, caja = 620f, n = 260, tam = new Vector2(120f, 240f), col = new Color(0.17f, 0.14f, 0.24f, 0.9f) },
                                new { dy = -42f, caja = 420f, n = 140, tam = new Vector2(90f, 170f), col = new Color(0.11f, 0.1f, 0.17f, 0.85f) } };
            foreach (var cp in capas)
            {
                var g = new GameObject("coop_capa_nubes"); g.transform.SetParent(raiz.transform, false); g.transform.localPosition = new Vector3(0f, cp.dy, 0f);
                var ps = g.AddComponent<ParticleSystem>();
                var main = ps.main; main.loop = true; main.startLifetime = 60f; main.startSpeed = 0.5f; main.startSize = new ParticleSystem.MinMaxCurve(cp.tam.x, cp.tam.y);
                main.startColor = cp.col; main.simulationSpace = ParticleSystemSimulationSpace.Local; main.maxParticles = cp.n; main.prewarm = true;
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
                var em = ps.emission; em.rateOverTime = cp.n / 55f;
                var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.box = new Vector3(cp.caja, 30f, cp.caja);
                var cl = ps.colorOverLifetime; cl.enabled = true; var gr = new Gradient();
                gr.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
                cl.color = gr;
                var rr = g.GetComponent<ParticleSystemRenderer>(); rr.material = Potenciado.MaterialGas(); rr.renderMode = ParticleSystemRenderMode.Billboard; rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            var rayos = raiz.AddComponent<RayosCastillo>();
            rayos.YCima = yCima; rayos.Radio = 330f;
            plugin.Log("Nubes: tormenta eléctrica sobre el castillo a " + Mathf.RoundToInt(yNube) + " m (local)");
        }
    }

    /// Rayos que caen desde las nubes sobre el castillo, con destello violeta y trueno.
    public class RayosCastillo : MonoBehaviour
    {
        public float YCima, Radio = 330f;
        float proximo; static Material mat;
        Light luz; float destello;
        float pendienteTrueno = -1f; Vector3 posTrueno;

        void Start()
        {
            proximo = Time.time + 2f;
            var g = new GameObject("coop_luz_rayo"); g.transform.SetParent(transform, false); g.transform.localPosition = new Vector3(0f, -20f, 0f);
            luz = g.AddComponent<Light>(); luz.type = LightType.Point; luz.range = 700f; luz.color = new Color(0.72f, 0.62f, 1f); luz.intensity = 0f; luz.shadows = LightShadows.None;
        }

        void Update()
        {
            if (Time.time >= proximo) { proximo = Time.time + Random.Range(1.5f, 6f); StartCoroutine(Caer()); }
            if (destello > 0f) { destello -= Time.deltaTime * 5f; luz.enabled = true; luz.intensity = Mathf.Max(0f, destello) * 7f; } else { luz.intensity = 0f; luz.enabled = false; }   // apagada entre rayos: una luz de 700 m de alcance encendida le cuesta a todo lo que ilumina
            if (pendienteTrueno > 0f && Time.time >= pendienteTrueno)
            {
                pendienteTrueno = -1f;
                var tr = Sonidos.Clip("tormenta_trueno");
                if (tr != null) AudioSource.PlayClipAtPoint(tr, posTrueno, Random.Range(0.7f, 1f) * Opciones.FactorVolumen);
            }
        }

        System.Collections.IEnumerator Caer()
        {
            if (mat == null) mat = new Material(Shader.Find("Sprites/Default")) { name = "coop_rayo_castillo" };
            float a = Random.value * 6.28f, r = Mathf.Sqrt(Random.value) * Radio;
            Vector3 centro = transform.position;
            Vector3 suelo = new Vector3(centro.x + Mathf.Cos(a) * r, 0f, centro.z + Mathf.Sin(a) * r);
            var t = Terrain.activeTerrain; RaycastHit h;
            if (Physics.Raycast(suelo + Vector3.up * 400f, Vector3.down, out h, 800f)) suelo = h.point; else suelo.y = YCima;
            Vector3 cielo = new Vector3(suelo.x + Random.Range(-40f, 40f), centro.y - 15f, suelo.z + Random.Range(-40f, 40f));
            var go = new GameObject("coop_rayo_castillo");
            Tramo(go.transform, cielo, suelo, 3.2f, 16);
            for (int k = 0; k < 3; k++)
            {
                Vector3 d = Vector3.Lerp(cielo, suelo, Random.Range(0.1f, 0.7f));
                Tramo(go.transform, d, d + new Vector3(Random.Range(-60f, 60f), -Random.Range(20f, 60f), Random.Range(-60f, 60f)), 1.6f, 8);
            }
            // ramificaciones entre las nubes
            Vector3 ar = cielo + new Vector3(Random.Range(-120f, 120f), Random.Range(10f, 30f), Random.Range(-120f, 120f));
            Tramo(go.transform, cielo + Vector3.up * 4f, ar, 2f, 10);
            destello = 1f;
            posTrueno = suelo; float dist = Vector3.Distance(posTrueno, Camera.main != null ? Camera.main.transform.position : suelo);
            pendienteTrueno = Time.time + 0.2f + dist / 160f;
            go.SetActive(true);
            yield return new WaitForSeconds(0.09f); go.SetActive(false); destello = 0.5f;
            yield return new WaitForSeconds(0.07f); go.SetActive(true); destello = 1f;
            yield return new WaitForSeconds(0.12f);
            Destroy(go);
        }

        static void Tramo(Transform pa, Vector3 a, Vector3 b, float ancho, int puntos)
        {
            var g = new GameObject("tramo"); g.transform.SetParent(pa, false);
            var lr = g.AddComponent<LineRenderer>();
            lr.sharedMaterial = mat; lr.useWorldSpace = true;
            lr.startWidth = ancho; lr.endWidth = ancho * 0.45f;
            lr.startColor = new Color(0.9f, 0.85f, 1f, 1f); lr.endColor = new Color(0.7f, 0.65f, 1f, 0.9f);
            lr.positionCount = puntos;
            Vector3 lado = Vector3.Cross(b - a, Vector3.up); if (lado.sqrMagnitude < 0.01f) lado = Vector3.right; lado.Normalize();
            for (int i = 0; i < puntos; i++)
            {
                float t = i / (float)(puntos - 1);
                Vector3 p = Vector3.Lerp(a, b, t);
                if (i > 0 && i < puntos - 1) p += lado * Random.Range(-9f, 9f) + Vector3.up * Random.Range(-4f, 4f);
                lr.SetPosition(i, p);
            }
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; lr.receiveShadows = false;
        }
    }
}

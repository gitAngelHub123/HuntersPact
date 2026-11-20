// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using UnityEngine;

namespace WitchHuntCoop
{
    /// Mini fogata del mod: tres troncos, llama que sube y una luz naranja que parpadea. Va donde el mod pone luz
    /// (dentro de las cabañas cerradas, donde quedaba una luz de piedra sin piedra): una luz naranja siempre lleva algo que la explique.
    public static class Fogata
    {
        public static GameObject Crear(Vector3 pos, Transform padre, float escala = 1f)
        {
            var raiz = new GameObject("coop_fogata");
            if (padre != null) raiz.transform.SetParent(padre, true);
            raiz.transform.position = pos;
            var mad = new Material(Shader.Find("Standard")) { color = new Color(0.16f, 0.1f, 0.06f) };
            for (int i = 0; i < 3; i++)
            {
                var t = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Object.Destroy(t.GetComponent<Collider>());
                t.name = "coop_tronco";
                t.transform.SetParent(raiz.transform, false);
                t.transform.localScale = new Vector3(0.09f, 0.28f, 0.09f);
                t.transform.localRotation = Quaternion.Euler(80f, i * 120f, 0f);
                t.transform.localPosition = new Vector3(0f, 0.06f, 0f);
                t.GetComponent<Renderer>().sharedMaterial = mad;
            }
            // llama realista (capas de lenguas, núcleo, chispas y humo)
            Llama.Crear(raiz.transform.position + Vector3.up * 0.1f * escala, raiz.transform, escala, escala >= 0.8f, escala >= 0.6f);
            // luz
            var lg = new GameObject("coop_luz_fogata"); lg.transform.SetParent(raiz.transform, false); lg.transform.position = raiz.transform.position + Vector3.up * 0.5f * escala;
            var l = lg.AddComponent<Light>(); l.type = LightType.Point; l.color = new Color(1f, 0.6f, 0.25f); l.range = 9f; l.intensity = 2.2f; l.shadows = LightShadows.None;
            raiz.AddComponent<ParpadeoFogata>().Luz = l;
            return raiz;
        }
    }

    public class ParpadeoFogata : MonoBehaviour
    {
        public Light Luz; float f;
        void Update() { if (Luz == null) return; f += Time.deltaTime * 9f; Luz.intensity = 2.0f + Mathf.Sin(f) * 0.25f + Mathf.Sin(f * 2.7f) * 0.15f; }
    }
}

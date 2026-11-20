// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using UnityEngine;

namespace WitchHuntCoop
{
    /// Escala de resolución (50–150 %): la cámara del mundo dibuja en una imagen más chica (más FPS) o
    /// más grande (más nitidez) y una cámara del mod la pone en pantalla, justo antes de la cámara del
    /// arma, así el arma y la interfaz siguen nítidas encima. Al 100 % no se toca nada.
    public static class EscalaRender
    {
        static RenderTexture rt;
        static Camera escalada, pantalla;

        /// Factor actual entre la imagen dibujada y la pantalla (para pasar puntos del mundo a la pantalla).
        public static float Factor { get { return rt != null && escalada != null && escalada.targetTexture == rt ? (float)Screen.width / rt.width : 1f; } }

        /// Punto del mundo en coordenadas de pantalla, teniendo en cuenta la escala.
        public static Vector3 APantalla(Camera cam, Vector3 mundo)
        {
            var p = cam.WorldToScreenPoint(mundo);
            float f = cam == escalada ? Factor : 1f;
            return new Vector3(p.x * f, p.y * f, p.z);
        }

        public static void Aplicar(float escala)
        {
            var cam = Camera.main;
            if (escalada != null && escalada != cam) Quitar();   // cambió la cámara (otra escena)
            if (cam == null || Mathf.Abs(escala - 1f) < 0.005f) { Quitar(); return; }
            int w = Mathf.Max(64, Mathf.RoundToInt(Screen.width * escala)), h = Mathf.Max(64, Mathf.RoundToInt(Screen.height * escala));
            if (rt == null || rt.width != w || rt.height != h)
            {
                if (escalada != null) escalada.targetTexture = null;
                if (rt != null) { rt.Release(); Object.Destroy(rt); }
                rt = new RenderTexture(w, h, 24, RenderTextureFormat.Default) { name = "coop_escala", filterMode = escala < 1f ? FilterMode.Bilinear : FilterMode.Bilinear };
                rt.Create();
            }
            if (cam.targetTexture != rt) cam.targetTexture = rt;
            escalada = cam;
            if (pantalla == null)
            {
                var go = new GameObject("coop_camara_escala");
                Object.DontDestroyOnLoad(go);
                pantalla = go.AddComponent<Camera>();
                // limpia a negro antes de volcar el mundo: con "Nothing", en las pantallas de la intro (logo sobre fondo vacío)
                // quedaba la pantalla sin limpiar y en Proton salía BLANCA (pedido 2026-11-17: antes era negro)
                pantalla.clearFlags = CameraClearFlags.SolidColor; pantalla.backgroundColor = Color.black; pantalla.cullingMask = 0;
                pantalla.useOcclusionCulling = false; pantalla.allowHDR = false; pantalla.allowMSAA = false;
                go.AddComponent<Volcar>();
            }
            // entre la cámara del mundo y la siguiente que dibuje en pantalla (la del arma)
            float siguiente = float.MaxValue;
            foreach (var c in Camera.allCameras)
                if (c != cam && c != pantalla && c.targetTexture == null && c.depth > cam.depth) siguiente = Mathf.Min(siguiente, c.depth);
            float d = siguiente < float.MaxValue ? (cam.depth + siguiente) / 2f : cam.depth + 1f;
            if (Mathf.Abs(pantalla.depth - d) > 0.0001f) pantalla.depth = d;
            if (!pantalla.enabled) pantalla.enabled = true;
        }

        static void Quitar()
        {
            if (escalada != null && escalada.targetTexture == rt) escalada.targetTexture = null;
            escalada = null;
            if (pantalla != null) pantalla.enabled = false;
            if (rt != null) { rt.Release(); Object.Destroy(rt); rt = null; }
        }

        /// Pone la imagen del mundo en pantalla.
        class Volcar : MonoBehaviour
        {
            void OnPostRender()
            {
                if (rt == null || escalada == null || escalada.targetTexture != rt) return;
                // dibuja la imagen del mundo como un rectángulo que llena la pantalla
                GL.PushMatrix();
                GL.LoadPixelMatrix(0, Screen.width, Screen.height, 0);
                Graphics.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), rt);
                GL.PopMatrix();
            }
        }
    }
}

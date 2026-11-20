// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using UnityEngine;

namespace WitchHuntCoop
{
    /// Brillo (0–100 %, 50 = como el juego) y campo de visión (0–100 %, 50 = como el juego; 0 = −25°, 100 = +35°).
    /// El brillo es un velo de pantalla completa: por debajo de 50 multiplica la imagen por un gris (oscurece sin lavarla) y por encima suma luz.
    /// El FOV se aplica sin molestar al juego: al empezar el cuadro se devuelve el valor que él puso y al terminar se le suma el desvío,
    /// así los zooms de puntería y las carreras siguen funcionando.
    public static class Vista
    {
        static Material mat; static bool intentado;

        static Material Material()
        {
            if (mat != null || intentado) return mat;
            intentado = true;
            var sh = Shader.Find("Hidden/Internal-Colored");
            if (sh == null) return null;
            mat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
            mat.SetInt("_Cull", 0); mat.SetInt("_ZWrite", 0); mat.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
            return mat;
        }

        public static void DibujarBrillo(int pct)
        {
            if (pct == 50 || Event.current == null || Event.current.type != EventType.Repaint) return;
            var m = Material(); if (m == null) return;
            bool oscurecer = pct < 50;
            m.SetInt("_SrcBlend", oscurecer ? (int)UnityEngine.Rendering.BlendMode.DstColor : (int)UnityEngine.Rendering.BlendMode.One);
            m.SetInt("_DstBlend", oscurecer ? (int)UnityEngine.Rendering.BlendMode.Zero : (int)UnityEngine.Rendering.BlendMode.One);
            float g = oscurecer ? Mathf.Lerp(0.08f, 1f, pct / 50f) : Mathf.Lerp(0f, 0.3f, (pct - 50f) / 50f);
            m.SetPass(0);
            GL.PushMatrix(); GL.LoadPixelMatrix();
            GL.Begin(GL.QUADS); GL.Color(new Color(g, g, g, 1f));
            GL.Vertex3(0f, 0f, 0f); GL.Vertex3(Screen.width, 0f, 0f); GL.Vertex3(Screen.width, Screen.height, 0f); GL.Vertex3(0f, Screen.height, 0f);
            GL.End(); GL.PopMatrix();
        }

        internal static Camera cam; internal static float guardado; internal static bool aplicado;
    }

    /// Al empezar el cuadro (antes que el juego) devuelve el FOV que el juego había puesto.
    [DefaultExecutionOrder(-32000)]
    public class VistaFovInicio : MonoBehaviour
    {
        void Update()
        {
            if (Vista.aplicado && Vista.cam != null) Vista.cam.fieldOfView = Vista.guardado;
            Vista.aplicado = false;
        }
    }

    /// Al terminar el cuadro (después de todo) le suma el desvío elegido en las opciones.
    [DefaultExecutionOrder(32000)]
    public class VistaFovFin : MonoBehaviour
    {
        void LateUpdate()
        {
            var c = Camera.main;
            float d = Opciones.FovDelta;
            if (c == null || Mathf.Abs(d) < 0.01f) { Vista.aplicado = false; return; }
            Vista.cam = c; Vista.guardado = c.fieldOfView;
            c.fieldOfView = Mathf.Clamp(Vista.guardado + d, 20f, 120f);
            Vista.aplicado = true;
        }
    }
}

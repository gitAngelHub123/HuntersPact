// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using System.Text;
using UnityEngine;

namespace WitchHuntCoop
{
    /// (2026-11-19) Sonido: en las dos copias lo cercano (respiración a 1 m, música 2D) salía "virtual" (silenciado) y un grito a 1 km
    /// sonaba: Unity escuchaba desde otro lado. Unity solo admite UN oyente activo; aquí se deja solo el de la cámara del jugador
    /// (si hay otros activos se apagan y se anota cuáles) y la orden "oyente" muestra todos.
    public static class Oyente
    {
        static float proxRevision;

        static bool EsDeLaCamara(AudioListener l, Camera cam)
        {
            return cam != null && l != null && (l.transform == cam.transform || l.transform.IsChildOf(cam.transform) || cam.transform.IsChildOf(l.transform));
        }

        public static void Asegurar(Plugin plugin)
        {
            float t = Time.unscaledTime;
            if (t < proxRevision) return;
            proxRevision = t + 2f;
            var cam = Camera.main; if (cam == null) return;
            var todos = Object.FindObjectsOfType<AudioListener>();
            int activos = 0; bool hayDeCamara = false;
            foreach (var l in todos) if (l.isActiveAndEnabled) { activos++; if (EsDeLaCamara(l, cam)) hayDeCamara = true; }
            if (activos <= 1 && (hayDeCamara || activos == 0)) return;
            if (!hayDeCamara) return;   // sin oyente en la cámara: no apago nada (la orden "oyente" lo muestra)
            foreach (var l in todos)
            {
                if (!l.isActiveAndEnabled || EsDeLaCamara(l, cam)) continue;
                l.enabled = false;
                plugin.Log("Sonido: oyente de más apagado en " + Ruta(l.transform) + " (a " + Vector3.Distance(l.transform.position, cam.transform.position).ToString("0") + " m de la cámara)");
            }
        }

        public static string Informe()
        {
            var cam = Camera.main; var sb = new StringBuilder();
            sb.Append("cámara principal: ").Append(cam != null ? Ruta(cam.transform) + " en " + cam.transform.position.ToString("F0") : "(ninguna)");
            sb.Append(" | AudioListener.volume=").Append(AudioListener.volume.ToString("0.00")).Append(" pause=").Append(AudioListener.pause);
            foreach (var l in Resources.FindObjectsOfTypeAll<AudioListener>())
            {
                if (l == null || !l.gameObject.scene.IsValid()) continue;
                sb.Append(" | oyente ").Append(Ruta(l.transform)).Append(" enabled=").Append(l.enabled).Append(" activo=").Append(l.gameObject.activeInHierarchy)
                  .Append(" en ").Append(l.transform.position.ToString("F0"));
                if (cam != null) sb.Append(" (a ").Append(Vector3.Distance(l.transform.position, cam.transform.position).ToString("0")).Append(" m de la cámara").Append(EsDeLaCamara(l, cam) ? ", ES la de la cámara" : "").Append(")");
            }
            var cfg = AudioSettings.GetConfiguration();
            sb.Append(" | voces reales ").Append(cfg.numRealVoices).Append(", ").Append(cfg.sampleRate).Append(" Hz, búfer ").Append(cfg.dspBufferSize).Append(", ").Append(cfg.speakerMode);
            return sb.ToString();
        }

        static string Ruta(Transform t)
        {
            var s = t.name; var p = t.parent; int n = 0;
            while (p != null && n++ < 6) { s = p.name + "/" + s; p = p.parent; }
            return s;
        }
    }
}

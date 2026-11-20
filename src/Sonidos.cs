// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace WitchHuntCoop
{
    /// Sonidos que vienen dentro de la DLL (sonidos/*.ogg, grabaciones libres; ver sonidos/CREDITOS.txt).
    /// Unity 5.6 no lee OGG desde memoria: se escriben en la caché de BepInEx y se cargan con WWW.
    public static class Sonidos
    {
        class Carga { public WWW W; public AudioClip Clip; }
        static readonly Dictionary<string, Carga> cargas = new Dictionary<string, Carga>();

        /// Empieza a cargar un sonido incrustado (nombre sin extensión, p. ej. "tormenta_lluvia").
        public static void Preparar(string nombre)
        {
            if (cargas.ContainsKey(nombre)) return;
            var c = new Carga(); cargas[nombre] = c;
            try
            {
                using (var st = typeof(Sonidos).Assembly.GetManifestResourceStream("sonidos." + nombre + ".ogg"))
                {
                    if (st == null) { Debug.Log("[WitchHuntCoop] Sonidos: no está " + nombre); return; }
                    string ruta = Path.Combine(BepInEx.Paths.CachePath, "coop_" + nombre + ".ogg");
                    Directory.CreateDirectory(Path.GetDirectoryName(ruta));
                    var buf = new byte[st.Length]; int leidos = 0;
                    while (leidos < buf.Length) { int k = st.Read(buf, leidos, buf.Length - leidos); if (k <= 0) break; leidos += k; }
                    File.WriteAllBytes(ruta, buf);
                    c.W = new WWW("file:///" + ruta.Replace('\\', '/'));
                }
            }
            catch (System.Exception e) { Debug.Log("[WitchHuntCoop] Sonidos: " + nombre + ": " + e.Message); }
        }

        /// El sonido, si ya está cargado (si no, null y se sigue intentando).
        public static AudioClip Clip(string nombre)
        {
            Carga c;
            if (!cargas.TryGetValue(nombre, out c)) { Preparar(nombre); return null; }
            if (c.Clip != null) return c.Clip;
            if (c.W != null && c.W.isDone && string.IsNullOrEmpty(c.W.error))
            {
                var a = c.W.GetAudioClip(false, false);
                if (a != null && a.loadState == AudioDataLoadState.Loaded && a.length > 0f) { c.Clip = a; c.W = null; }
            }
            return c.Clip;
        }
    }
}

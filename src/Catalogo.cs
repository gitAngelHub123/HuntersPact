// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx;
using UnityEngine;

namespace WitchHuntCoop
{
    /// Orden de prueba "catalogo": fotografía cada tipo de estructura de la escena con una cámara
    /// aparte (sin árboles, pasto ni niebla y con luz extra), sin mover al jugador.
    /// Deja BepInEx/capturas/catalogo/NNN_nombre.png y catalogo.txt con nombre, cantidad y posiciones.
    public static class Catalogo
    {
        static readonly Regex Numero = new Regex(@"\s*(\(\d+\)|\(Clone\))\s*$");

        static readonly string[] Excluir = {
            "coop_", "WitchHuntCoop", "Crux_ai", "Terrain", "_pickable", "Ammo_", "Sack_of_coins", "Gold_Nugget",
            "Lockpicks", "med_plant", "Undead", "Player", "FPS", "Camera", "Canvas", "Werewolf", "Snake", "Spider",
        };

        class Grupo
        {
            public string Nombre;
            public readonly List<Transform> Raices = new List<Transform>();
            public int Mallas, Horneadas;
            public bool Logica;
        }

        public static string BaseDe(string n)
        {
            string b = n;
            for (int i = 0; i < 3; i++) b = Numero.Replace(b, "");
            return b.Trim();
        }

        public static void Hacer(Plugin plugin, string filtro)
        {
            Vector3 an = Anclaje.Origen();
            var grupos = new Dictionary<string, Grupo>();
            foreach (var go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (!go.activeInHierarchy) continue;
                string b = BaseDe(go.name);
                bool fuera = false;
                foreach (var e in Excluir) if (b.IndexOf(e, StringComparison.OrdinalIgnoreCase) >= 0) { fuera = true; break; }
                if (fuera) continue;
                if (filtro.Length > 0 && b.IndexOf(filtro, StringComparison.OrdinalIgnoreCase) < 0) continue;
                var rs = go.GetComponentsInChildren<Renderer>();
                if (rs.Length == 0) continue;
                Bounds bb; if (!Limites(rs, out bb)) continue;
                float tam = bb.size.magnitude;
                if (tam < 0.4f || tam > 400f) continue;   // migas o cosas del tamaño del mapa (paredes, agua)
                Grupo g;
                if (!grupos.TryGetValue(b, out g)) grupos[b] = g = new Grupo { Nombre = b };
                g.Raices.Add(go.transform);
                if (g.Raices.Count == 1)
                {
                    foreach (var r in rs) { g.Mallas++; if (r.isPartOfStaticBatch) g.Horneadas++; }
                    g.Logica = go.GetComponentsInChildren<PlayMakerFSM>().Length > 0;
                }
            }

            string carpeta = Path.Combine(Path.Combine(Paths.BepInExRootPath, "capturas"), "catalogo");
            Directory.CreateDirectory(carpeta);
            foreach (var f in Directory.GetFiles(carpeta, "*.png")) File.Delete(f);

            var lista = new List<Grupo>(grupos.Values);
            lista.Sort((a, c) => string.Compare(a.Nombre, c.Nombre, StringComparison.OrdinalIgnoreCase));

            // Preparar la escena para la foto y recordar cómo estaba.
            var terrenos = Terrain.activeTerrains;
            var arboles = new bool[terrenos.Length];
            for (int i = 0; i < terrenos.Length; i++) { arboles[i] = terrenos[i].drawTreesAndFoliage; terrenos[i].drawTreesAndFoliage = false; }
            bool niebla = RenderSettings.fog;
            var modoAmb = RenderSettings.ambientMode; var colAmb = RenderSettings.ambientLight;
            RenderSettings.fog = false;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.58f);

            var goLuz = new GameObject("CoopCatalogoLuz");
            var luz = goLuz.AddComponent<Light>();
            luz.type = LightType.Directional; luz.intensity = 1.1f; luz.shadows = LightShadows.Soft;
            var goCam = new GameObject("CoopCatalogoCam");
            var cam = goCam.AddComponent<Camera>();
            cam.enabled = false; cam.fieldOfView = 50f; cam.nearClipPlane = 0.1f; cam.farClipPlane = 3000f;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.42f, 0.47f, 0.52f);
            var rt = new RenderTexture(960, 540, 24);
            var tex = new Texture2D(960, 540, TextureFormat.RGB24, false);
            cam.targetTexture = rt;

            var txt = new StringBuilder();
            int n = 0;
            try
            {
                foreach (var g in lista)
                {
                    n++;
                    var raiz = g.Raices[0];
                    Bounds bb; Limites(raiz.GetComponentsInChildren<Renderer>(), out bb);
                    Vector3 c = bb.center;
                    float r = Mathf.Max(bb.extents.magnitude, 0.6f);
                    float dist = Mathf.Clamp(r * 2.3f, 3f, 150f);

                    // De los 8 rumbos, el que deja la cámara más despejada sobre el suelo.
                    Vector3 mejor = c + new Vector3(dist, r, dist) * 0.7f; float mejorH = float.MaxValue;
                    for (int k = 0; k < 8; k++)
                    {
                        float a = k * Mathf.PI / 4f;
                        Vector3 p = c + new Vector3(Mathf.Sin(a) * dist, 0f, Mathf.Cos(a) * dist);
                        float suelo = Suelo(p);
                        float y = Mathf.Max(c.y + r * 0.55f, suelo + 1.8f);
                        float sobre = y - c.y;   // cuanto menos tenga que subir, mejor vista lateral
                        if (sobre < mejorH) { mejorH = sobre; mejor = new Vector3(p.x, y, p.z); }
                    }
                    goCam.transform.position = mejor;
                    goCam.transform.LookAt(c);
                    goLuz.transform.rotation = Quaternion.Euler(45f, goCam.transform.eulerAngles.y + 35f, 0f);

                    cam.Render();
                    RenderTexture.active = rt;
                    tex.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
                    tex.Apply();
                    RenderTexture.active = null;
                    string archivo = n.ToString("000") + "_" + Regex.Replace(g.Nombre, @"[^A-Za-z0-9_\-]+", "_") + ".png";
                    File.WriteAllBytes(Path.Combine(carpeta, archivo), tex.EncodeToPNG());

                    txt.Append(n.ToString("000")).Append('|').Append(g.Nombre).Append('|').Append(g.Raices.Count)
                       .Append('|').Append(bb.size.x.ToString("0.0")).Append('x').Append(bb.size.y.ToString("0.0")).Append('x').Append(bb.size.z.ToString("0.0"))
                       .Append('|').Append(g.Mallas).Append('/').Append(g.Horneadas).Append('|').Append(g.Logica ? "lógica" : "")
                       .Append('|').Append(Agua(c) ? "agua" : "").Append('|');
                    for (int i = 0; i < g.Raices.Count && i < 8; i++)
                    {
                        Vector3 p = g.Raices[i].position - an;
                        txt.Append(Mathf.RoundToInt(p.x)).Append(',').Append(Mathf.RoundToInt(p.y)).Append(',').Append(Mathf.RoundToInt(p.z)).Append(' ');
                    }
                    txt.Append('\n');
                }
            }
            finally
            {
                cam.targetTexture = null;
                UnityEngine.Object.Destroy(rt); UnityEngine.Object.Destroy(tex);
                UnityEngine.Object.Destroy(goCam); UnityEngine.Object.Destroy(goLuz);
                for (int i = 0; i < terrenos.Length; i++) if (terrenos[i] != null) terrenos[i].drawTreesAndFoliage = arboles[i];
                RenderSettings.fog = niebla; RenderSettings.ambientMode = modoAmb; RenderSettings.ambientLight = colAmb;
            }
            File.WriteAllText(Path.Combine(carpeta, "catalogo.txt"), txt.ToString());
            plugin.Log("  catálogo: " + n + " tipos de estructura en " + carpeta);
        }

        /// foto x z alto [nombre]: vista desde arriba (inclinada) de un punto del mapa, sin árboles ni niebla.
        public static void Foto(Plugin plugin, string arg)
        {
            var ps = arg.Split(' ');
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            Vector3 an = Anclaje.Origen();
            float x = float.Parse(ps[0], ci) + an.x, z = float.Parse(ps[1], ci) + an.z, alto = ps.Length > 2 ? float.Parse(ps[2], ci) : 120f;
            string nombre = ps.Length > 3 ? ps[3] : "foto_" + Mathf.RoundToInt(x) + "_" + Mathf.RoundToInt(z);
            var objetivo = new Vector3(x, Suelo(new Vector3(x, 0f, z)), z);
            var cam = objetivo + new Vector3(0f, alto, -alto * 0.55f);
            string ruta = Path.Combine(Path.Combine(Paths.BepInExRootPath, "capturas"), nombre + ".png");
            Fotografiar(cam, objetivo, ruta, 1280, 720, false);
            plugin.Log("  foto en " + ruta);
        }

        /// Una foto con la escena preparada (sin árboles, pasto ni niebla, con luz) y luego restaurada.
        public static void Fotografiar(Vector3 desde, Vector3 hacia, string ruta, int ancho, int alto, bool arboles)
        {
            var terrenos = Terrain.activeTerrains;
            var antes = new bool[terrenos.Length];
            for (int i = 0; i < terrenos.Length; i++) { antes[i] = terrenos[i].drawTreesAndFoliage; terrenos[i].drawTreesAndFoliage = arboles; }
            bool niebla = RenderSettings.fog;
            var modoAmb = RenderSettings.ambientMode; var colAmb = RenderSettings.ambientLight;
            RenderSettings.fog = false;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.58f);
            var goLuz = new GameObject("CoopFotoLuz");
            var luz = goLuz.AddComponent<Light>();
            luz.type = LightType.Directional; luz.intensity = 1.1f; luz.shadows = LightShadows.Soft;
            var goCam = new GameObject("CoopFotoCam");
            var cam = goCam.AddComponent<Camera>();
            cam.enabled = false; cam.fieldOfView = 55f; cam.nearClipPlane = 0.02f; cam.farClipPlane = 4000f;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.42f, 0.47f, 0.52f);
            var rt = new RenderTexture(ancho, alto, 24);
            var tex = new Texture2D(ancho, alto, TextureFormat.RGB24, false);
            try
            {
                cam.targetTexture = rt;
                goCam.transform.position = desde; goCam.transform.LookAt(hacia);
                goLuz.transform.rotation = Quaternion.Euler(50f, goCam.transform.eulerAngles.y + 35f, 0f);
                cam.Render();
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, ancho, alto), 0, 0); tex.Apply();
                RenderTexture.active = null;
                Directory.CreateDirectory(Path.GetDirectoryName(ruta));
                File.WriteAllBytes(ruta, tex.EncodeToPNG());
            }
            finally
            {
                cam.targetTexture = null;
                UnityEngine.Object.Destroy(rt); UnityEngine.Object.Destroy(tex);
                UnityEngine.Object.Destroy(goCam); UnityEngine.Object.Destroy(goLuz);
                for (int i = 0; i < terrenos.Length; i++) if (terrenos[i] != null) terrenos[i].drawTreesAndFoliage = antes[i];
                RenderSettings.fog = niebla; RenderSettings.ambientMode = modoAmb; RenderSettings.ambientLight = colAmb;
            }
        }

        static bool Limites(Renderer[] rs, out Bounds b)
        {
            b = new Bounds(); bool hay = false;
            foreach (var r in rs)
            {
                if (r == null || !r.enabled || r is ParticleSystemRenderer || r is LineRenderer || r is TrailRenderer) continue;
                if (!hay) { b = r.bounds; hay = true; } else b.Encapsulate(r.bounds);
            }
            return hay;
        }

        static float Suelo(Vector3 p)
        {
            float h = float.MinValue;
            foreach (var t in Terrain.activeTerrains)
            {
                Vector3 o = t.transform.position, s = t.terrainData.size;
                if (p.x < o.x || p.z < o.z || p.x > o.x + s.x || p.z > o.z + s.z) continue;
                h = Mathf.Max(h, t.SampleHeight(p) + o.y);
            }
            return h == float.MinValue ? p.y : h;
        }

        /// ¿Está junto al agua? (el agua global del bosque está a y ≈ 12,8)
        static bool Agua(Vector3 c) { return c.y < 16f; }
    }
}

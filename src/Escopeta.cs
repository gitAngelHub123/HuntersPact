// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using System;
using System.Collections.Generic;
using PixelCrushers.DialogueSystem;
using UnityEngine;

namespace WitchHuntCoop
{
    /// Escopeta recortada (arma 4): copia del mosquete del juego (mismas animaciones de apuntar,
    /// disparar y recargar) con 16 perdigones, alcance de 10 m y 2 balas de su propia reserva por
    /// disparo. Del modelo del mosquete se dejan solo los brazos (se quitan los triángulos que mueven
    /// los huesos del arma) y en el hueso del arma se pone una escopeta de dos cañones hecha aquí.
    public static class Escopeta
    {
        public const string Nombre = "Shotgun_coop";
        public const string VarTiene = "coop_tiene_escopeta";
        public const int Indice = 4;               // tecla 4
        public const int Perdigones = 16, DanoPerdigon = 14;
        public const float Alcance = 10f, Dispersion = 0.09f, Recarga = 3.6f;

        public static Plugin plugin;
        public static readonly ArmaCopia Copia = new ArmaCopia(Nombre, VarTiene, "Escopeta", Indice, MallaEscopeta, TexturaEscopeta,
            0.55f, 0.68f, Vector3.zero,   // modelo del mosquete: cañón acortado y doble, a ~68 % del tamaño
            wb => { wb.projectileCount = Perdigones; wb.damage = DanoPerdigon; wb.range = Alcance; wb.shotSpread = Dispersion; wb.reloadTime = Recarga; wb.bulletsPerClip = 2; wb.bulletsToReload = 2; })   // cargador de 2: un cartucho por cañón
            { DelMosquete = true, Bisagra = true };

        public static bool Tiene { get { return Copia.Tiene; } }
        public static void Dar(bool si) { Copia.Dar(si); }
        public static void Fotos() { Copia.Fotos("esc"); }
        public static void FotoSuelta()
        {
            sueltas.RemoveAll(x => x == null);
            if (sueltas.Count == 0 && Camera.main != null)
            {
                // prueba: sin escopetas sueltas en el mundo, se crea una delante de la cámara
                var t = Camera.main.transform; var g = Modelo("coop_prueba_escopeta");
                g.transform.position = t.position + t.forward * 2.5f + Vector3.down * 1.2f;
                plugin.Log("  escopeta de prueba creada");
                return;
            }
            if (sueltas.Count == 0) { plugin.Log("  no hay escopetas sueltas"); return; }
            var c = sueltas[0].GetComponent<Renderer>().bounds.center;
            string dir = System.IO.Path.Combine(BepInEx.Paths.BepInExRootPath, "capturas");
            Catalogo.Fotografiar(c + new Vector3(0.7f, 0.5f, 0.7f), c, System.IO.Path.Combine(dir, "suelta_a.png"), 800, 450, true);
            Catalogo.Fotografiar(c + new Vector3(0f, 1.0f, 0.05f), c, System.IO.Path.Combine(dir, "suelta_arriba.png"), 800, 450, true);
        }
        public static string Describir() { return Copia.Describir(); }

        // ---------------- Doble disparo / un cañón (tecla configurable, X por defecto) ----------------

        const string PrefDoble = "coop_escopeta_doble";
        public static bool Doble
        {
            get { try { return PlayerPrefs.GetInt(PrefDoble, 1) == 1; } catch { return true; } }
            set { try { PlayerPrefs.SetInt(PrefDoble, value ? 1 : 0); PlayerPrefs.Save(); } catch { } }
        }
        static WeaponBehavior wbPrev; static int balasPrev = -1; static bool ultimaGratis;

        static void ModoDeDisparo(WeaponBehavior w, PlayerWeapons pw)
        {
            // Con los dos cañones cargados y el doble activado se disparan los dos a la vez (16 perdigones y 2 cartuchos);
            // con un cañón, 8 perdigones y 1 cartucho (el otro queda cargado para el siguiente disparo).
            w.projectileCount = (Doble && w.bulletsLeft >= 2) ? Perdigones : Perdigones / 2;
            if (!Copia.EnLaMano(pw)) { wbPrev = null; return; }
            if (Entrada.Enfocado && !Entrada.PanelAbierto && Controles.Capturando == null && Controles.Pulsada("modo_escopeta"))
            {
                Doble = !Doble;
                Viaje.Avisar(Doble ? "Modo doble cañón" : "Modo 1 disparo", 2.5f);
            }
            if (wbPrev != w) { wbPrev = w; balasPrev = w.bulletsLeft; return; }
            if (w.bulletsLeft < balasPrev && Doble && w.bulletsLeft > 0 && balasPrev >= 2)
            {
                // salió el disparo doble: el segundo cartucho también se gasta
                w.bulletsLeft = 0;
            }
            if (w.bulletsLeft < balasPrev && LibrosTienda.Tiene(LibrosTienda.SegundaBala))
            {
                // Libro "Segunda bala": 30 % de que el disparo no gaste un cartucho, y nunca dos disparos gratis seguidos
                if (!ultimaGratis && UnityEngine.Random.value < 0.30f) { w.bulletsLeft += 1; ultimaGratis = true; }   // sin texto (pedido 2026-11-19)
                else ultimaGratis = false;
            }
            if (w.bulletsLeft != balasPrev)
            {
                var bl = HutongGames.PlayMaker.FsmVariables.GlobalVariables.FindFsmInt("bullets_left");
                if (bl != null) bl.Value = w.bulletsLeft;
                FuegoAmigo.Ajustar(w, w.bulletsLeft); SonidosCompanero.Ajustar(w, w.bulletsLeft);
            }
            balasPrev = w.bulletsLeft;
        }

        public static void Actualizar()
        {
            var w = Copia.Actualizar();
            Sonidos.Preparar("esc_disparo");
            if (w != null) { var disp = Sonidos.Clip("esc_disparo"); if (disp != null && w.fireSnd != disp) w.fireSnd = disp; }   // disparo de escopeta real (en vez del del mosquete)
            if (sueltas.Count > 0) MejorarSueltas();
            AnimEscopeta.Asegurar();
            if (w == null) return;
            var pw0 = Rendimiento.Armas;
            if (pw0 != null) ModoDeDisparo(w, pw0);
            w.reloadTime = Recarga * (LibrosTienda.Tiene(LibrosTienda.EscopetaRecarga) ? LibrosTienda.FactorRecargaEscopeta : 1f);   // libro "Mano rápida"
            // Recarga: la animación del mosquete (baqueta) se acelera para durar lo que dura la recarga.
            w.reloadAnimSpeed = AnimEscopeta.Largo / Mathf.Max(0.5f, w.reloadTime);   // el clip propio dura AnimEscopeta.Largo s
        }

        /// La escopeta suelta en el mundo (sobre el cadáver de las casas): unos 66 cm, con un
        /// colisionador para que no la atraviesen los rayos de recoger.
        public static GameObject Modelo(string nombre)
        {
            var g = new GameObject(nombre);
            var hijo = new GameObject("malla");
            hijo.transform.SetParent(g.transform, false);
            hijo.transform.localScale = Vector3.one * 1.2f;
            hijo.transform.localPosition = new Vector3(-0.3f, 0f, 0f);   // centro de la escopeta en el origen
            hijo.AddComponent<MeshFilter>().sharedMesh = mallaSuelta ?? (mallaSuelta = MallaEscopeta());
            sueltas.Add(hijo);   // se cambia al modelo nuevo (el del mosquete) en cuanto el arma esté creada
            var mr = hijo.AddComponent<MeshRenderer>();
            if (matSuelto == null)
            {
                matSuelto = new Material(Shader.Find("Standard")) { name = "coop_escopeta_suelta" };
                matSuelto.mainTexture = TexturaEscopeta();
                matSuelto.SetFloat("_Glossiness", 0.35f);
            }
            mr.sharedMaterial = matSuelto;
            return g;
        }
        static Mesh mallaSuelta; static Material matSuelto;
        static readonly List<GameObject> sueltas = new List<GameObject>();

        /// Las escopetas sueltas (casas) usan el mismo modelo que la del jugador: malla y materiales del mosquete
        /// acortado y de doble cañón, centrada, de ~66 cm y tendida (largo → X, alto → Y).
        static void MejorarSueltas()
        {
            sueltas.RemoveAll(x => x == null);
            if (sueltas.Count == 0 || Copia.Modelo == null) return;
            int n = 0;
            foreach (var h in sueltas) if (ArmaSuelta.Poner(h.transform.parent != null ? h.transform.parent.gameObject : h, Copia.Modelo, 1.55f, true)) n++;
            if (n > 0) plugin.Log("Escopeta: " + n + " sueltas con el modelo entero (culata y cañones)");
        }

        // ---------------- Modelo hecho aquí ----------------

        /// Textura de dos zonas: madera (u < 0,5) y metal pavonado (u > 0,5).
        internal static Texture2D TexturaEscopeta()
        {
            var t = new Texture2D(64, 64, TextureFormat.RGB24, false) { hideFlags = HideFlags.HideAndDontSave };
            var r = new System.Random(7);
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    Color c;
                    if (x < 32) { float veta = 0.85f + 0.15f * Mathf.Sin(y * 0.9f + (float)r.NextDouble()); c = new Color(0.36f, 0.21f, 0.11f) * veta; }
                    else { float n = 0.9f + 0.1f * (float)r.NextDouble(); c = new Color(0.16f, 0.16f, 0.18f) * n; }
                    c.a = 1f; t.SetPixel(x, y, c);
                }
            t.Apply();
            return t;
        }

        static readonly Vector2 Madera = new Vector2(0.25f, 0.5f), Metal = new Vector2(0.75f, 0.5f);

        /// Escopeta de dos cañones recortada: cañones, banda, cajón con martillos, guardamonte, caña y
        /// culata de pistola. X hacia la boca, Y arriba, origen en la empuñadura; largo total 0,55 m.
        internal static Mesh MallaEscopeta()
        {
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var tr = new List<int>();
            for (int lado = -1; lado <= 1; lado += 2) Cilindro(v, uv, tr, new Vector3(0.05f, 0.02f, lado * 0.0175f), new Vector3(0.40f, 0.02f, lado * 0.0175f), 0.017f, 14, Metal);
            Caja(v, uv, tr, new Vector3(0.05f, 0.034f, -0.006f), new Vector3(0.40f, 0.04f, 0.006f), Metal);          // banda superior
            Caja(v, uv, tr, new Vector3(0.10f, -0.004f, -0.03f), new Vector3(0.26f, 0.012f, 0.03f), Madera);         // caña
            Caja(v, uv, tr, new Vector3(-0.05f, -0.012f, -0.026f), new Vector3(0.05f, 0.045f, 0.026f), Metal);       // cajón
            for (int lado = -1; lado <= 1; lado += 2) Caja(v, uv, tr, new Vector3(-0.045f, 0.04f, lado * 0.0175f - 0.005f), new Vector3(-0.02f, 0.065f, lado * 0.0175f + 0.005f), Metal);   // martillos
            Caja(v, uv, tr, new Vector3(-0.03f, -0.045f, -0.006f), new Vector3(0.03f, -0.035f, 0.006f), Metal);      // guardamonte (abajo)
            Caja(v, uv, tr, new Vector3(0.025f, -0.045f, -0.006f), new Vector3(0.033f, -0.012f, 0.006f), Metal);     // guardamonte (frente)
            Caja(v, uv, tr, new Vector3(-0.005f, -0.03f, -0.003f), new Vector3(0.002f, -0.012f, 0.003f), Metal);     // gatillo
            // culata de pistola: un prisma inclinado hacia atrás y abajo
            Prisma(v, uv, tr, new[] { new Vector2(-0.05f, 0.04f), new Vector2(-0.05f, -0.012f), new Vector2(-0.12f, -0.09f), new Vector2(-0.155f, -0.075f), new Vector2(-0.10f, 0.0f), new Vector2(-0.07f, 0.04f) }, 0.024f, Madera);
            var m = new Mesh { name = "coop_escopeta" };
            m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(tr, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            // centrar: origen en la empuñadura (x≈-0.05) y largo total 0,55 m
            return m;
        }

        internal static void Cara(List<Vector3> v, List<Vector2> uv, List<int> tr, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 col)
        {
            int i = v.Count; v.Add(a); v.Add(b); v.Add(c); v.Add(d);
            for (int k = 0; k < 4; k++) uv.Add(col);
            tr.Add(i); tr.Add(i + 1); tr.Add(i + 2); tr.Add(i); tr.Add(i + 2); tr.Add(i + 3);
        }

        internal static void Caja(List<Vector3> v, List<Vector2> uv, List<int> tr, Vector3 mn, Vector3 mx, Vector2 col)
        {
            Vector3 p000 = mn, p111 = mx;
            Vector3 p100 = new Vector3(mx.x, mn.y, mn.z), p010 = new Vector3(mn.x, mx.y, mn.z), p001 = new Vector3(mn.x, mn.y, mx.z);
            Vector3 p110 = new Vector3(mx.x, mx.y, mn.z), p101 = new Vector3(mx.x, mn.y, mx.z), p011 = new Vector3(mn.x, mx.y, mx.z);
            Cara(v, uv, tr, p000, p010, p110, p100, col);   // -z
            Cara(v, uv, tr, p101, p111, p011, p001, col);   // +z
            Cara(v, uv, tr, p001, p011, p010, p000, col);   // -x
            Cara(v, uv, tr, p100, p110, p111, p101, col);   // +x
            Cara(v, uv, tr, p010, p011, p111, p110, col);   // +y
            Cara(v, uv, tr, p001, p000, p100, p101, col);   // -y
        }

        internal static void Cilindro(List<Vector3> v, List<Vector2> uv, List<int> tr, Vector3 a, Vector3 b, float r, int lados, Vector2 col)
        {
            for (int k = 0; k < lados; k++)
            {
                float a0 = k * Mathf.PI * 2f / lados, a1 = (k + 1) * Mathf.PI * 2f / lados;
                Vector3 o0 = new Vector3(0f, Mathf.Cos(a0) * r, Mathf.Sin(a0) * r), o1 = new Vector3(0f, Mathf.Cos(a1) * r, Mathf.Sin(a1) * r);
                Cara(v, uv, tr, a + o0, b + o0, b + o1, a + o1, col);
                int i = v.Count; v.Add(b); v.Add(b + o1); v.Add(b + o0); uv.Add(col); uv.Add(col); uv.Add(col); tr.Add(i); tr.Add(i + 1); tr.Add(i + 2);   // boca
                // anillo oscuro en la boca: un cilindro interior pequeño
            }
            Cilindro2(v, uv, tr, b - new Vector3(0.002f, 0f, 0f), b + new Vector3(0.0005f, 0f, 0f), r * 0.65f, lados, new Vector2(0.99f, 0.01f));
        }

        internal static void Cilindro2(List<Vector3> v, List<Vector2> uv, List<int> tr, Vector3 a, Vector3 b, float r, int lados, Vector2 col)
        {
            for (int k = 0; k < lados; k++)
            {
                float a0 = k * Mathf.PI * 2f / lados, a1 = (k + 1) * Mathf.PI * 2f / lados;
                Vector3 o0 = new Vector3(0f, Mathf.Cos(a0) * r, Mathf.Sin(a0) * r), o1 = new Vector3(0f, Mathf.Cos(a1) * r, Mathf.Sin(a1) * r);
                int i = v.Count; v.Add(b); v.Add(b + o1); v.Add(b + o0); uv.Add(col); uv.Add(col); uv.Add(col); tr.Add(i); tr.Add(i + 1); tr.Add(i + 2);
            }
        }

        /// Prisma de un polígono en XY con grosor en Z.
        internal static void Prisma(List<Vector3> v, List<Vector2> uv, List<int> tr, Vector2[] pts, float grosor, Vector2 col)
        {
            float z0 = -grosor / 2f, z1 = grosor / 2f;
            int n = pts.Length;
            for (int k = 0; k < n; k++)
            {
                var p = pts[k]; var q = pts[(k + 1) % n];
                Cara(v, uv, tr, new Vector3(p.x, p.y, z0), new Vector3(q.x, q.y, z0), new Vector3(q.x, q.y, z1), new Vector3(p.x, p.y, z1), col);
            }
            for (int k = 1; k < n - 1; k++)
            {
                int i = v.Count;
                v.Add(new Vector3(pts[0].x, pts[0].y, z0)); v.Add(new Vector3(pts[k + 1].x, pts[k + 1].y, z0)); v.Add(new Vector3(pts[k].x, pts[k].y, z0));
                v.Add(new Vector3(pts[0].x, pts[0].y, z1)); v.Add(new Vector3(pts[k].x, pts[k].y, z1)); v.Add(new Vector3(pts[k + 1].x, pts[k + 1].y, z1));
                for (int j = 0; j < 6; j++) uv.Add(col);
                tr.Add(i); tr.Add(i + 1); tr.Add(i + 2); tr.Add(i + 3); tr.Add(i + 4); tr.Add(i + 5);
            }
        }
    }
}

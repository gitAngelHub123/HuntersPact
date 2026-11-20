// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using System.Collections.Generic;
using System.IO;
using PixelCrushers.DialogueSystem;
using UnityEngine;
using UnityEngine.AI;

namespace WitchHuntCoop
{
    /// Tesoro del castillo (pedido 2026-11-12): una bóveda con puerta de hierro que solo se abre con una llave, y esa llave la suelta el jefe
    /// de la mina (la guarida del murciélago, la tercera mina). La bóveda guarda entre 8000 y 10000 de oro y el resto del castillo entre 300 y 900,
    /// repartidos en montones. Las cantidades salen de la semilla de la partida (cambian en cada partida nueva, no al volver al bosque).
    public partial class Mundo
    {
        public const string VarBoveda = "coop_boveda_abierta", VarLlave = "coop_llave_boveda", VarJefeMina = "coop_jefe_mina", VarLlaveSuelo = "coop_llave_pos", VarTesoroTomado = "coop_tesoro_tomado";
        public const int OroCastilloMin = 300, OroCastilloMax = 900, OroBovedaMin = 8000, OroBovedaMax = 10000;

        class PilaOro { public string Id; public int Monto; public bool Boveda; public GameObject Go; public float Radio; }
        readonly List<PilaOro> pilasOro = new List<PilaOro>();
        GameObject llaveGo; float llaveFase;
        PilaOro pilaCerca; bool llaveCerca, puertaCerca;
        public int OroCastilloTotal, OroBovedaTotal;

        static bool LuaBool(string v) { try { return DialogueLua.GetVariable(v).AsBool; } catch { return false; } }
        public static bool LuaBoolPublico(string v) { return LuaBool(v); }
        static string LuaTexto(string v) { try { string s = DialogueLua.GetVariable(v).AsString; return s == "nil" ? "" : (s ?? ""); } catch { return ""; } }
        public static bool TieneLlave { get { return LuaBool(VarLlave); } }
        public static bool BovedaAbierta { get { return LuaBool(VarBoveda); } }

        Material matOro;
        Material MaterialOro()
        {
            if (matOro != null) return matOro;
            // oro batido: tonos dorados con vetas, golpes de martillo (relieve) y destellos claros
            const int N = 128; var col = new Color[N * N]; var alt = new float[N, N];
            for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
            {
                float g = Ruido(x, y, N, 0.12f) * 0.45f + Ruido(x + 30, y + 8, N, 0.4f) * 0.35f + Ruido(x + 5, y + 70, N, 1.1f) * 0.2f;
                Color c = Color.Lerp(new Color(0.55f, 0.32f, 0.04f), new Color(1f, 0.82f, 0.28f), Mathf.Clamp01(g * 1.3f));
                if (g > 0.68f) c = Color.Lerp(c, new Color(1f, 0.96f, 0.7f), (g - 0.68f) * 3f);   // destello
                col[y * N + x] = new Color(c.r, c.g, c.b, 1f); alt[x, y] = Ruido(x + 12, y + 44, N, 0.3f) * 0.7f + g * 0.3f;
            }
            var ta = new Texture2D(N, N, TextureFormat.RGB24, true) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Repeat }; ta.SetPixels(col); ta.Apply();
            matOro = Estandar("coop_oro", ta, Normales(alt, N, 3.5f), new Color(1f, 0.9f, 0.7f), 0.8f, 1.2f);
            matOro.SetFloat("_Metallic", 0.9f);
            matOro.EnableKeyword("_EMISSION"); matOro.SetColor("_EmissionColor", new Color(0.3f, 0.19f, 0.02f));
            return matOro;
        }

        /// Montón de monedas: pilas de discos de distinta altura, algunas sueltas y un destello dorado. El tamaño crece con la cantidad.
        GameObject CrearMonton(Transform padre, Vector3 mundo, int monto, bool grande)
        {
            var raiz = new GameObject("coop_oro_" + monto); raiz.transform.SetParent(padre, true); raiz.transform.position = mundo;
            float k = grande ? 1.6f : 1f;
            var rng = new System.Random(monto * 7919);
            var mat = MaterialOro();
            if (grande)
            {
                // bóveda bajo la torre (pedido 2026-11-17): mini montañas de oro en vez de pilas de monedas
                MontanaOro(raiz.transform, mat, 1.25f + 0.25f * (float)rng.NextDouble(), 1.0f + 0.35f * (float)rng.NextDouble(), monto * 31 + 7);
                var lb = new GameObject("luz"); lb.transform.SetParent(raiz.transform, false); lb.transform.localPosition = new Vector3(0f, 1.8f, 0f);
                var lzb = lb.AddComponent<Light>(); lzb.type = LightType.Point; lzb.color = new Color(1f, 0.78f, 0.35f); lzb.range = 7f; lzb.intensity = 1.1f; lzb.shadows = LightShadows.None;
                return raiz;
            }
            int pilas = grande ? 9 : 5;
            for (int i = 0; i < pilas; i++)
            {
                float a = i * 2.4f + (float)rng.NextDouble(), r = (i == 0 ? 0f : 0.25f + 0.2f * (float)rng.NextDouble() * k) * (i == 0 ? 1f : 1f + i * 0.12f);
                float h = (0.18f + 0.45f * (float)rng.NextDouble()) * k * (i == 0 ? 1.8f : 1f);
                var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder); Object.Destroy(g.GetComponent<Collider>());
                g.name = "coop_oro_pila"; g.transform.SetParent(raiz.transform, false);
                g.transform.localPosition = new Vector3(Mathf.Cos(a) * r, h / 2f, Mathf.Sin(a) * r);
                g.transform.localScale = new Vector3(0.34f * k, h / 2f, 0.34f * k);
                g.GetComponent<Renderer>().sharedMaterial = mat;
            }
            for (int i = 0; i < (grande ? 10 : 5); i++)   // monedas sueltas
            {
                float a = (float)rng.NextDouble() * 6.28f, r = (0.5f + 0.5f * (float)rng.NextDouble()) * k;
                var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder); Object.Destroy(g.GetComponent<Collider>());
                g.name = "coop_moneda"; g.transform.SetParent(raiz.transform, false);
                g.transform.localPosition = new Vector3(Mathf.Cos(a) * r, 0.02f, Mathf.Sin(a) * r);
                g.transform.localScale = new Vector3(0.16f, 0.012f, 0.16f); g.transform.localRotation = Quaternion.Euler((float)rng.NextDouble() * 10f, (float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 10f);
                g.GetComponent<Renderer>().sharedMaterial = mat;
            }
            var l = new GameObject("luz"); l.transform.SetParent(raiz.transform, false); l.transform.localPosition = new Vector3(0f, 0.9f * k, 0f);
            var lz = l.AddComponent<Light>(); lz.type = LightType.Point; lz.color = new Color(1f, 0.8f, 0.35f); lz.range = 5.5f * k; lz.intensity = 0.9f; lz.shadows = LightShadows.None;
            return raiz;
        }

        /// Llave dorada: anilla, caña y dientes; gira y flota.
        GameObject CrearLlave(Vector3 mundo)
        {
            var raiz = new GameObject("coop_llave_boveda"); raiz.transform.position = mundo;
            var mat = MaterialOro();
            var anilla = GameObject.CreatePrimitive(PrimitiveType.Cylinder); Object.Destroy(anilla.GetComponent<Collider>());
            anilla.transform.SetParent(raiz.transform, false); anilla.transform.localPosition = new Vector3(0f, 0.9f, 0f); anilla.transform.localScale = new Vector3(0.34f, 0.04f, 0.34f); anilla.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            anilla.GetComponent<Renderer>().sharedMaterial = mat;
            var hueco = GameObject.CreatePrimitive(PrimitiveType.Cylinder); Object.Destroy(hueco.GetComponent<Collider>());
            hueco.transform.SetParent(raiz.transform, false); hueco.transform.localPosition = new Vector3(0f, 0.9f, 0.01f); hueco.transform.localScale = new Vector3(0.17f, 0.045f, 0.17f); hueco.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            hueco.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Standard")) { color = new Color(0.03f, 0.03f, 0.04f) };
            Caja(raiz.transform, "coop_llave_cana", raiz.transform.position + Vector3.up * 0.5f, new Vector3(0.07f, 0.7f, 0.07f), Quaternion.identity, mat, Color.white, false);
            Caja(raiz.transform, "coop_llave_diente", raiz.transform.position + new Vector3(0.1f, 0.25f, 0f), new Vector3(0.2f, 0.07f, 0.07f), Quaternion.identity, mat, Color.white, false);
            Caja(raiz.transform, "coop_llave_diente", raiz.transform.position + new Vector3(0.08f, 0.13f, 0f), new Vector3(0.16f, 0.07f, 0.07f), Quaternion.identity, mat, Color.white, false);
            var l = new GameObject("luz"); l.transform.SetParent(raiz.transform, false); l.transform.localPosition = new Vector3(0f, 0.8f, 0f);
            var lz = l.AddComponent<Light>(); lz.type = LightType.Point; lz.color = new Color(1f, 0.85f, 0.4f); lz.range = 9f; lz.intensity = 1.6f; lz.shadows = LightShadows.None;
            raiz.AddComponent<Girar>();
            return raiz;
        }

        // ---------------- Colocación (una vez por entrada al bosque; todo sale de la semilla de la partida) ----------------

        void ColocarTesoroCastillo(int semillaPartida)
        {
            SonidoOroJuego();
            foreach (var p in pilasOro) if (p.Go != null) Object.Destroy(p.Go);
            pilasOro.Clear(); if (llaveGo != null) Object.Destroy(llaveGo); llaveGo = null; OroCastilloTotal = OroBovedaTotal = 0;
            if (castilloRaiz == null) { plugin.Log("Tesoro: sin castillo (no se coloca el oro)"); return; }
            var rng = new System.Random(semillaPartida ^ Hash("coop_tesoro_castillo"));
            int total = rng.Next(OroCastilloMin, OroCastilloMax + 1), boveda = rng.Next(OroBovedaMin, OroBovedaMax + 1);
            OroCastilloTotal = total; OroBovedaTotal = boveda;
            var tomados = ListaLua(VarTesoroTomado);
            Repartir(rng, total, sitiosCastillo, 5, false, tomados, "c");
            boveda = RepartirMontanas(tomados); OroBovedaTotal = boveda;
            if (BovedaAbierta && bovedaPuerta != null) { Object.Destroy(bovedaPuerta); bovedaPuerta = null; }

            // la llave sigue en el suelo si el jefe ya cayó y nadie la tomó
            string ls = LuaTexto(VarLlaveSuelo);
            if (!TieneLlave && ls.Length > 0)
            {
                var t = ls.Split(';');
                float x, y, z;
                if (t.Length == 3 && float.TryParse(t[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out x) && float.TryParse(t[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out y) && float.TryParse(t[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out z))
                    llaveGo = CrearLlave(new Vector3(x, y, z) + Ancla());
            }
            plugin.Log("Tesoro: castillo " + total + " de oro en " + sitiosCastillo.Count + " montones, bóveda " + boveda + " en " + sitiosBoveda.Count + " (" + pilasOro.Count + " sin tomar)" + (BovedaAbierta ? ", bóveda abierta" : ", bóveda cerrada") + (TieneLlave ? ", llave en tu poder" : ""));
        }

        public const int MontanasBoveda = 40, MontanasGrandes = 25;
        readonly List<Vector4> montanasBoveda = new List<Vector4>();   // x, z (locales del castillo), radio, índice
        /// Montos: las 25 primeras de 10 000 (índice 0) a 100 (índice 24); las 15 de más (pedido 2026-11-18) de 3000 a 100.
        /// Progresión geométrica, redondeados a 50.
        public static int MontoMontana(int i)
        {
            // 2026-11-18 (pedido): las 40 entre 3000 (la del centro) y 100, en progresión geométrica, redondeadas a 50
            float t = i / (float)(MontanasBoveda - 1);
            return Mathf.Max(100, Mathf.RoundToInt(3000f * Mathf.Pow(100f / 3000f, t) / 50f) * 50);
        }
        public static float RadioMontana(int monto) { return 1.1f + 4.4f * Mathf.Sqrt((monto - 100f) / 2900f); }

        int RepartirMontanas(HashSet<string> tomados)
        {
            int total = 0, hechas = 0;
            for (int k = 0; k < montanasBoveda.Count; k++)
            {
                var s = montanasBoveda[k];
                int idx = k == 0 ? 0 : (int)s.w; int m = MontoMontana(idx); total += m;
                string id = "m" + idx;
                if (tomados.Contains(id)) continue;
                float R = s.z, H = Mathf.Max(0.6f, R * 0.56f);
                // en coordenadas locales del castillo (agrandado ×1,6), como las montañas decorativas de antes
                var raiz = new GameObject("coop_oro_montana_" + m); raiz.transform.SetParent(castilloRaiz.transform, false);
                raiz.transform.localPosition = new Vector3(s.x, BovPiso, s.y); raiz.transform.localRotation = Quaternion.Euler(0f, idx * 53f, 0f);
                MontanaOro(raiz.transform, MaterialOro(), R, H, m * 31 + 7);
                foreach (var mf in raiz.GetComponentsInChildren<MeshFilter>()) if (mf.name == "coop_cerro_oro") mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
                if (m >= 2000)
                {
                    var lb = new GameObject("luz"); lb.transform.SetParent(raiz.transform, false); lb.transform.localPosition = new Vector3(0f, H + 1f, 0f);
                    var lz = lb.AddComponent<Light>(); lz.type = LightType.Point; lz.color = new Color(1f, 0.78f, 0.35f); lz.range = R * 2f + 4f; lz.intensity = 1.1f; lz.shadows = LightShadows.None;
                }
                Asentar.EnSuelo(raiz);
                pilasOro.Add(new PilaOro { Id = id, Monto = m, Boveda = true, Go = raiz, Radio = R * CastilloEscala });
                hechas++;
            }
            plugin.Log("Tesoro: " + hechas + " de " + montanasBoveda.Count + " montañas de oro recogibles en la bóveda (" + total + " de oro en total)");
            return total;
        }

        /// Reparte `total` entre los sitios con pesos al azar (los últimos de la lista, p. ej. la terraza de la torre, pesan el doble si hay más sitios que montones fijos).
        void Repartir(System.Random rng, int total, List<Vector3> sitios, int redondeo, bool grande, HashSet<string> tomados, string prefijo)
        {
            int n = sitios.Count; if (n == 0) return;
            var w = new double[n]; double suma = 0;
            for (int i = 0; i < n; i++) { w[i] = 1.0 + rng.NextDouble() * 2.0; if (!grande && i == n - 1) w[i] *= 2.0; suma += w[i]; }
            int resto = total;
            for (int i = 0; i < n; i++)
            {
                int m = i == n - 1 ? resto : Mathf.Max(redondeo, (int)(System.Math.Round(total * w[i] / suma / redondeo) * redondeo));
                m = Mathf.Min(m, resto); resto -= m;
                string id = prefijo + i;
                if (tomados.Contains(id)) continue;
                var p = new PilaOro { Id = id, Monto = m, Boveda = grande };
                p.Go = CrearMonton(castilloRaiz.transform, castilloRaiz.transform.TransformPoint(sitios[i]), m, grande);
                Asentar.EnSuelo(p.Go);   // el oro nunca en el aire
                pilasOro.Add(p);
            }
        }

        // ---------------- Interacción ----------------

        void ActualizarTesoro()
        {
            pilaCerca = null; llaveCerca = false; puertaCerca = false;
            var j = plugin.Jugador;
            if (j == null || castilloRaiz == null) { ActualizarJefeMina(); return; }
            Vector3 p = j.transform.position;
            if (llaveGo != null) { llaveFase += Time.deltaTime; }
            foreach (var pl in pilasOro)
            {
                if (pl.Go == null) continue;
                Vector3 d = p - pl.Go.transform.position;
                bool cercaMontana = pl.Radio > 0f && new Vector2(d.x, d.z).magnitude < pl.Radio + 1.4f && d.y > -1.5f && d.y < pl.Radio + 2.5f;   // montañas grandes: basta llegar al borde
                if (cercaMontana || Vector3.Distance(p, pl.Go.transform.position + Vector3.up * 0.6f) < 2.4f) { pilaCerca = pl; break; }
            }
            if (llaveGo != null && Vector3.Distance(p, llaveGo.transform.position + Vector3.up * 0.8f) < 2.6f) llaveCerca = true;
            if (bovedaPuerta != null && Vector3.Distance(p, bovedaPuerta.transform.position) < 4.2f) puertaCerca = true;
            if ((pilaCerca != null || llaveCerca || puertaCerca) && Controles.Interactuar() && Entrada.Enfocado)
            {
                if (llaveCerca) TomarLlave(true);
                else if (pilaCerca != null) TomarMonton(pilaCerca, true);
                else AbrirBoveda(true);
            }
            ActualizarJefeMina();
        }

        /// Sonido de monedas al recoger oro del tesoro (solo lo oye quien lo recoge).
        /// Oro suelto para recoger alrededor de las montañas de la bóveda (ids fijos: al recogerlo desaparece en los dos juegos).
        /// Va después de registrar el botín del juego (ClonarBotin copia un original).
        void OroSueltoBoveda()
        {
            int n = 0;
            for (int i = 0; i < sotanoOroSuelto.Count && castilloRaiz != null; i++)
                { var w = castilloRaiz.transform.TransformPoint(sotanoOroSuelto[i]); string id = "boveda_suelto:" + i; if (ClonarBotin(i % 3 == 2 ? "Sack_of_coins" : "Gold_Nugget", w, i * 47f, id) != null || ClonarBotin(i % 3 == 2 ? "Gold_Nugget" : "Sack_of_coins", w, i * 47f, id) != null) n++; }
            plugin.Log("Tesoro: " + n + " piezas de oro suelto en la bóveda");
        }

        static AudioClip sonidoSaco; static float proxBusquedaSonido;
        /// Sonido de recoger oro del juego original (pedido 2026-11-18: el "moneda" del mod sonaba a subir de nivel; nunca más).
        /// Se busca en los sacos de monedas y pepitas del juego: el pickupSound de su PickupButton/PickupButton1, el clip de su
        /// AudioSource, o un AudioClip que toque alguna acción de sus FSM. Se prefiere el del saco de monedas.
        public static AudioClip SonidoOroJuego()
        {
            if (sonidoSaco != null || Time.realtimeSinceStartup < proxBusquedaSonido) return sonidoSaco;
            proxBusquedaSonido = Time.realtimeSinceStartup + 5f;
            AudioClip deNugget = null; var vistos = new System.Text.StringBuilder();
            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (go == null) continue;
                string n = go.name; bool saco = n.StartsWith("Sack_of_coins"), pepita = n.StartsWith("Gold_Nugget");
                if (!saco && !pepita) continue;
                AudioClip c = null;
                foreach (var pb in go.GetComponentsInChildren<PickupButton>(true)) if (pb.pickupSound != null) { c = pb.pickupSound; break; }
                if (c == null) foreach (var pb in go.GetComponentsInChildren<PickupButton1>(true)) if (pb.pickupSound != null) { c = pb.pickupSound; break; }
                if (c == null) foreach (var au in go.GetComponentsInChildren<AudioSource>(true)) if (au.clip != null) { c = au.clip; break; }
                if (c == null)
                    foreach (var f in go.GetComponentsInChildren<PlayMakerFSM>(true))
                    {
                        if (f.FsmStates == null) continue;
                        foreach (var st in f.FsmStates) { if (st.Actions == null) continue; foreach (var ac in st.Actions)
                        {
                            if (ac == null) continue;
                            foreach (var fi in ac.GetType().GetFields())
                            {
                                object v = fi.GetValue(ac);
                                var ac2 = v as AudioClip; if (ac2 != null) { c = ac2; break; }
                                var fo = v as HutongGames.PlayMaker.FsmObject; if (fo != null && fo.Value is AudioClip) { c = (AudioClip)fo.Value; break; }
                            }
                            if (c != null) break;
                        } if (c != null) break; }
                        if (c != null) break;
                    }
                if (c == null) continue;
                vistos.Append(n).Append('=').Append(c.name).Append(' ');
                if (saco) { sonidoSaco = c; break; }
                if (deNugget == null) deNugget = c;
            }
            // el clip del saco de monedas del juego (sack_of_coins) manda sobre todo lo demás
            foreach (var ac in Resources.FindObjectsOfTypeAll<AudioClip>()) if (ac != null && ac.name == "sack_of_coins") { sonidoSaco = ac; break; }
            if (sonidoSaco == null) sonidoSaco = deNugget;
            // último recurso: un sonido cargado del juego con nombre de monedas u oro (nunca el "moneda" del mod)
            var nombres = new System.Text.StringBuilder();
            if (sonidoSaco == null)
            {
                string[] claves = { "coin", "money", "gold", "cash", "loot", "treasure", "pickup", "pick_up", "collect" };
                AudioClip mejor = null; int mejorK = 99;
                foreach (var ac in Resources.FindObjectsOfTypeAll<AudioClip>())
                {
                    if (ac == null) continue; string ln = ac.name.ToLowerInvariant();
                    for (int k = 0; k < claves.Length; k++) if (ln.Contains(claves[k])) { nombres.Append(ac.name).Append(' '); if (k < mejorK) { mejorK = k; mejor = ac; } break; }
                }
                sonidoSaco = mejor;
            }
            vistos.Append("| sonidos del juego con nombre de oro: ").Append(nombres);
            Escopeta.plugin.Log("Tesoro: sonido del oro del juego = " + (sonidoSaco != null ? sonidoSaco.name : "(no encontrado)") + " | " + vistos);
            return sonidoSaco;
        }

        void SonarMonedas()
        {
            var cam = Camera.main; if (cam == null) return;
            var c = SonidoOroJuego();
            if (c == null) return;   // sin el sonido del juego, en silencio (el de respaldo sonaba a subir de nivel)
            try { GuaAudioSource.Play2DClipAtPoint(c, cam.transform.position, 0.75f); } catch { AudioSource.PlayClipAtPoint(c, cam.transform.position, 0.75f); }
        }

        void TomarMonton(PilaOro p, bool mio)
        {
            if (p.Go != null) Object.Destroy(p.Go); p.Go = null; pilasOro.Remove(p);
            AnotarLua(VarTesoroTomado, p.Id);
            if (mio)
            {
                Oro.Dar(p.Monto); SonarMonedas();
                Viaje.Avisar("+" + p.Monto + " de oro" + (p.Boveda ? " (tesoro de la bóveda)" : "") + ".");
                int idx = int.Parse(p.Id.Substring(1)); bool bov = p.Boveda;
                var datos = Red.Armar(w => { w.Write((byte)3); w.Write(bov); w.Write(idx); });
                for (int i = 0; i < 3; i++) plugin.EnviarTesoro(datos);
            }
            plugin.Log("Tesoro: " + (mio ? "tomé" : plugin.NombreCompanero + " tomó") + " " + p.Monto + " de oro (" + p.Id + ")");
        }

        void TomarLlave(bool mio)
        {
            if (llaveGo != null) Object.Destroy(llaveGo); llaveGo = null;
            DialogueLua.SetVariable(VarLlave, true); DialogueLua.SetVariable(VarLlaveSuelo, "");
            if (mio)
            {
                Viaje.Avisar("Conseguiste la llave de la bóveda del castillo.");
                var datos = Red.Armar(w => w.Write((byte)1));
                for (int i = 0; i < 3; i++) plugin.EnviarTesoro(datos);
            }
            plugin.Log("Tesoro: llave de la bóveda " + (mio ? "tomada por mí" : "tomada por " + plugin.NombreCompanero));
        }

        void AbrirBoveda(bool mio)
        {
            if (bovedaPuerta == null) return;
            if (mio && !TieneLlave) { Viaje.Avisar("La bóveda está cerrada con llave. El jefe de la mina la tiene.", 4f); return; }
            DialogueLua.SetVariable(VarBoveda, true);
            bovedaPuerta.AddComponent<Hundir>();
            bovedaPuerta = null;
            if (mio)
            {
                Viaje.Avisar("La bóveda se abre.");
                var datos = Red.Armar(w => w.Write((byte)2));
                for (int i = 0; i < 3; i++) plugin.EnviarTesoro(datos);
            }
            plugin.Log("Tesoro: bóveda abierta " + (mio ? "por mí" : "por " + plugin.NombreCompanero));
        }

        /// 0 = el jefe cayó y soltó la llave (x, y, z respecto al ancla); 1 = llave tomada; 2 = bóveda abierta; 3 = montón tomado (bóveda?, número).
        public void RecibirTesoro(BinaryReader r)
        {
            byte a = r.ReadByte();
            if (a == 0)
            {
                Vector3 rel = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                if (!TieneLlave && llaveGo == null) SoltarLlave(rel, false);
            }
            else if (a == 1) { if (!TieneLlave) TomarLlave(false); }
            else if (a == 2) { if (!BovedaAbierta) { DialogueLua.SetVariable(VarBoveda, true); if (bovedaPuerta != null) { bovedaPuerta.AddComponent<Hundir>(); bovedaPuerta = null; } } }
            else if (a == 4) { MatarOjo(false); foreach (var pz in Object.FindObjectsOfType<PalancaOjo>()) pz.Bajar(); }
            else if (a == 3)
            {
                bool bov = r.ReadBoolean(); int idx = r.ReadInt32(); string id = (bov ? "v" : "c") + idx;
                foreach (var p in pilasOro) if (p.Id == id) { TomarMonton(p, false); return; }
                AnotarLua(VarTesoroTomado, id);
            }
        }

        void SoltarLlave(Vector3 rel, bool enviar)
        {
            Vector3 w = rel + Ancla();
            RaycastHit h;
            if (Physics.Raycast(w + Vector3.up * 1.5f, Vector3.down, out h, 6f, ~0, QueryTriggerInteraction.Ignore)) w = h.point + Vector3.up * 0.2f;
            rel = w - Ancla();
            DialogueLua.SetVariable(VarJefeMina, "muerto");
            DialogueLua.SetVariable(VarLlaveSuelo, rel.x.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + ";" + rel.y.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + ";" + rel.z.ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
            if (llaveGo != null) Object.Destroy(llaveGo);
            llaveGo = CrearLlave(w);
            Viaje.Avisar("El jefe de la mina cayó y soltó una llave dorada.", 7f);
            if (enviar)
            {
                var datos = Red.Armar(ww => { ww.Write((byte)0); ww.Write(rel.x); ww.Write(rel.y); ww.Write(rel.z); });
                for (int i = 0; i < 3; i++) plugin.EnviarTesoro(datos);
            }
            plugin.Log("Tesoro: llave soltada en " + rel.ToString("F0"));
        }

        public void DibujarTesoro()
        {
            if (plugin.Jugador == null || (pilaCerca == null && !llaveCerca && !puertaCerca)) return;
            if (estiloCofre == null)
            {
                estiloCofre = new GUIStyle(GUI.skin.label) { fontSize = Opciones.T(20), alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
                estiloCofre.normal.textColor = new Color(1f, 0.9f, 0.6f);
            }
            string t = llaveCerca ? "E: tomar la llave dorada" : pilaCerca != null ? "E: tomar el oro" : TieneLlave ? "E: abrir la bóveda con la llave" : "Bóveda cerrada con llave (E para probar)";
            GUI.Label(new Rect(0, Screen.height * 0.6f, Screen.width, 30f), t, estiloCofre);
        }

        // ---------------- El jefe de la mina ----------------

        TakeDamage jefeMina; Vector3 jefeUltima; float jefeProx; bool navGuaridaLista; NavMeshDataInstance navGuarida;
        public static TakeDamage JefeMinaActual;   // para que Dificultad le dé mucha vida

        /// Solo el anfitrión (o quien juega solo): al entrar alguien a la guarida aparece el murciélago, enorme y potenciado; al caer suelta la llave.
        /// Jefe de la guarida: desde 2026-11-18 es JefeCueva (modelo del juego animado por el mod, JefeCueva.cs); antes se generaba
        /// el cadáver Night_Flyer_Ragdoll2 con Generar y caía fuera de la sala.
        void ActualizarJefeMina()
        {
            try { ActualizarJefeCueva(); } catch (System.Exception e) { if (Time.frameCount % 600 == 0) plugin.Log("Jefe de la cueva: error " + e.Message); }
        }

        void ConstruirNavGuarida(Mina g)
        {
            navGuaridaLista = true;
            try
            {
                var b = new Bounds(g.CentroRed + Vector3.up * (CuevaH * 0.45f), new Vector3(2f * CuevaAX + 16f, CuevaH + 10f, 2f * CuevaAZ + 16f));   // toda la cueva (doble desde 2026-11-19)
                var fuentes = new List<NavMeshBuildSource>();
                NavMeshBuilder.CollectSources(b, ~0, NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), fuentes);
                var datos = NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByIndex(0), fuentes, b, Vector3.zero, Quaternion.identity);
                navGuarida = NavMesh.AddNavMeshData(datos);
                plugin.Log("Tesoro: mapa de navegación de la guarida con " + fuentes.Count + " fuentes");
            }
            catch (System.Exception e) { plugin.Log("Tesoro: no pude armar la navegación de la guarida: " + e.Message); }
        }

        void QuitarTesoro()
        {
            if (navGuaridaLista && navGuarida.valid) NavMesh.RemoveNavMeshData(navGuarida);
            navGuaridaLista = false; jefeMina = null; JefeMinaActual = null; jefeProx = 0f; pilasOro.Clear(); llaveGo = null;
        }
    }

    /// La llave gira y sube y baja despacio.
    public class Girar : MonoBehaviour
    {
        Vector3 origen; float f;
        void Start() { origen = transform.position; f = Random.value * 6f; }
        void Update() { f += Time.deltaTime; transform.position = origen + Vector3.up * (0.15f + Mathf.Sin(f * 1.6f) * 0.1f); transform.rotation = Quaternion.Euler(0f, f * 60f, 0f); }
    }

    /// La puerta de la bóveda se hunde en el suelo y desaparece.
    public class Hundir : MonoBehaviour
    {
        float t;
        void Update()
        {
            t += Time.deltaTime;
            transform.position += Vector3.down * Time.deltaTime * 2.2f;
            if (t > 2.6f) Destroy(gameObject);
        }
    }
}

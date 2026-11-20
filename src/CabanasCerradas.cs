// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace WitchHuntCoop
{
    /// Cabañas cerradas con llave (3 por partida, lugares únicos). Son copias de la cabaña con una
    /// puerta de tablas en el marco. La cerradura se fuerza con ganzúa como los cofres (35 % de que
    /// la ganzúa se rompa). Al abrirla, la puerta gira y dentro aparecen 3 cosas del juego (monedas,
    /// balas, plantas o un objeto valioso). Abierta queda abierta para los dos toda la partida.
    public partial class Mundo
    {
        public const string VarCabanasAbiertas = "coop_cabanas_abiertas";
        const int NumCabanas = 3;
        static readonly string[] BotinCabana = { "Sack_of_coins", "Sack_of_coins", "Sack_of_coins", "Gold_Nugget", "Ammo_Silverbullets", "med_plant", "Goldenfigure_pickable", "Goldidol_pickable", "Lockpicks_pack", "silverbars_pickable" };

        class Cabana { public GameObject Bloqueo; public int N; public GameObject Raiz; public Transform Puerta; public Vector3 Centro, Dentro; public bool Abierta; public float Giro; public int Semilla; }
        readonly List<Cabana> cabanas = new List<Cabana>();
        Cabana cabanaCerca;

        void ColocarCabanasCerradas(int semillaPartida)
        {
            cabanas.Clear(); cofresDeCabana.Clear();
            GameObject modelo = null;
            foreach (var g in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects()) if (g.name == "THE_WOOD_CABIN_02") modelo = g;
            if (modelo == null) return;
            var abiertas = ListaLua(VarCabanasAbiertas);
            var rng = new System.Random(semillaPartida ^ Hash("coop_cabanas_cerradas"));
            var ocupados = new List<Vector3> { cabana };
            foreach (var c in casas) if (c.Raiz != null) ocupados.Add(c.Raiz.transform.position);
            foreach (var m in minas) if (m.Boca != null) ocupados.Add(m.Boca.transform.position);
            Bounds puertaLocal; bool hayPuerta = PuertaDelModelo(modelo, out puertaLocal);
            var huellas = Huellas();
            for (int n = 0; n < NumCabanas; n++)
            {
                Vector3 sitio = Vector3.zero; float yaw = 0f; bool ok = false;
                for (int k = 0; k < 40 && !ok; k++)
                {
                    if (!Ubicar(rng, ocupados, 250f, out sitio, out yaw)) continue;
                    float y; ok = Llano(sitio, 8f, 1.2f, out y) && y > nivelAgua + 1f && !Choca(huellas, sitio.x, sitio.z, 9f, 3f) && Despejado(sitio, 12f)  /* sin rocas ni troncos pegados (pedido 2026-11-17) */;
                    if (ok) sitio.y = y;
                }
                if (!ok) { plugin.Log("Cabañas: sin sitio para la cabaña " + (n + 1)); continue; }
                ocupados.Add(sitio);
                var casa = ArmarCabana(modelo, sitio + Vector3.down * DesnivelCabana, yaw, "coop_cabana_cerrada_" + (n + 1));   // 60 cm más baja (quedaba muy elevada: 20 cm el 2026-11-10, +40 el 2026-11-11)
                huellas.Add(new Vector4(sitio.x, sitio.z, 9f, 0f));
                casa.transform.localScale = casa.transform.localScale * EscalaCabana;   // 30 % más grande (pedido 2026-11-11)
                var cb = new Cabana { N = n, Raiz = casa, Semilla = semillaPartida ^ Hash("coop_cabana_botin_" + n) };
                bool doblePiso = rng.NextDouble() < 0.4;   // "de vez en cuando": 2 de cada 5
                cb.Dentro = PuntoDentro(casa, Quaternion.Euler(0f, yaw, 0f), sitio);
                if (hayPuerta) PonerPuerta(cb, casa, puertaLocal);
                QuitarChimeneaYPiedras(casa);   // la chimenea y las piedras del modelo salían mal; la chimenea propia lleva la fogata dentro
                try { DecorarCabana(casa, cb.Semilla, doblePiso ? 1 : 0); } catch (System.Exception e) { plugin.Log("Cabañas: error al amueblar " + e.Message); }
                if (doblePiso) { try { SubirPlanta(casa, sitio + Vector3.down * DesnivelCabana, yaw, cb); } catch (System.Exception e) { plugin.Log("Cabañas: error en la segunda planta " + e.Message); } }
                if (!abiertas.Contains("c" + n)) cb.Bloqueo = PonerBloqueo(casa);   // cerrada de verdad: nadie entra por la puerta ni por las ventanas
                cb.Centro = cb.Puerta != null ? cb.Puerta.position : sitio;
                if (abiertas.Contains("c" + n)) { cb.Abierta = true; GirarPuerta(cb, 1f); }
                cabanas.Add(cb);
                plugin.Log("Cabañas: cabaña cerrada " + (n + 1) + " en " + (sitio - Ancla()).ToString("F0") + (cb.Abierta ? " (ya abierta)" : "") + (hayPuerta ? "" : " SIN PUERTA"));
            }
            cabanas.AddRange(cabanasEscopeta);   // las casas de la escopeta, con su puerta cerrada
            try { IluminarCasasYMinas(); } catch (System.Exception e) { plugin.Log("Luces: error " + e.Message); }
            if (cabanasEscopeta.Count > 0) plugin.Log("Cabañas: " + cabanasEscopeta.Count + " casas de la escopeta con puerta cerrada");
        }

        /// Hueco de la puerta del modelo de cabaña (en ejes del modelo): el marco da el hueco real.
        static bool PuertaDelModelo(GameObject modelo, out Bounds puertaLocal)
        {
            puertaLocal = new Bounds(); bool hayPuerta = false;
            for (int pasada = 0; pasada < 2 && !hayPuerta; pasada++)
            foreach (var r in modelo.GetComponentsInChildren<MeshRenderer>(true))
                if (r.name.StartsWith(pasada == 0 ? "doorFrame_01_LOD0" : "door_01_LOD0"))   // el marco de la puerta da el hueco real (la puerta del modelo está abierta y su caja engaña)
                {
                    var b = r.bounds;
                    for (int k = 0; k < 8; k++)
                    {
                        Vector3 e = modelo.transform.InverseTransformPoint(new Vector3((k & 1) == 0 ? b.min.x : b.max.x, (k & 2) == 0 ? b.min.y : b.max.y, (k & 4) == 0 ? b.min.z : b.max.z));
                        if (!hayPuerta) { puertaLocal = new Bounds(e, Vector3.zero); hayPuerta = true; } else puertaLocal.Encapsulate(e);
                    }
                    break;
                }
            return hayPuerta;
        }

        /// Casas de la escopeta con puerta y cerradura (pedido 2026-11-18): se registran como cabañas cerradas con número 100+n
        /// (mismo cartel, ganzúa, llave maestra, apertura sincronizada y guardada). Se arman antes que las cabañas: se suman después.
        readonly List<Cabana> cabanasEscopeta = new List<Cabana>();
        void CerrarCasaEscopeta(GameObject casa, GameObject modelo, int n, int semillaPartida, Vector3 sitio, float yaw)
        {
            Bounds pl; if (!PuertaDelModelo(modelo, out pl)) { plugin.Log("Casas: la casa " + (n + 1) + " queda sin puerta (no encontré el marco)"); return; }
            int num = 100 + n;
            var cb = new Cabana { N = num, Raiz = casa, Semilla = semillaPartida ^ Hash("coop_casa_escopeta_botin_" + n) };
            cb.Dentro = PuntoDentro(casa, Quaternion.Euler(0f, yaw, 0f), sitio);
            PonerPuerta(cb, casa, pl);
            var abiertas = ListaLua(VarCabanasAbiertas);
            if (!abiertas.Contains("c" + num)) cb.Bloqueo = PonerBloqueo(casa);
            cb.Centro = cb.Puerta != null ? cb.Puerta.position : sitio;
            if (abiertas.Contains("c" + num)) { cb.Abierta = true; GirarPuerta(cb, 1f); }
            cabanasEscopeta.Add(cb);
        }

        /// Copia de la cabaña (solo piezas de detalle completo, sin luces ni partículas, con piso).
        GameObject ArmarCabana(GameObject modelo, Vector3 sitio, float yaw, string nombre)
        {
            Bounds b; if (!CajaDe(new List<GameObject> { modelo }, out b)) b = new Bounds(modelo.transform.position, Vector3.one);
            Vector3 desfase = modelo.transform.position - new Vector3(b.center.x, b.min.y, b.center.z);
            Quaternion giro = Quaternion.Euler(0f, yaw, 0f);
            int faltan;
            var casa = Coop.Piezas.Clonar(modelo, sitio + giro * desfase, giro * modelo.transform.rotation, out faltan);
            casa.name = nombre;
            foreach (var lg in casa.GetComponentsInChildren<LODGroup>(true)) Object.Destroy(lg);
            DejarUnDetalle(casa);
            foreach (var l in casa.GetComponentsInChildren<Light>(true)) l.enabled = false;
            foreach (var ps in casa.GetComponentsInChildren<ParticleSystem>(true)) Object.Destroy(ps.gameObject == casa ? (UnityEngine.Object)ps : ps.gameObject);   // si las partículas están en la raíz, solo se quita el sistema (no el objeto entero)
            PonerPiso(casa);
            lugaresCopias.Add(casa);
            return casa;
        }

        Vector3 PuntoDentro(GameObject casa, Quaternion giro, Vector3 sitio)
        {
            foreach (var col in casa.GetComponentsInChildren<BoxCollider>(true))
                if (col.name.StartsWith("cabinFloor")) { var bp = col.bounds; return new Vector3(bp.center.x, bp.max.y, bp.center.z); }
            return sitio + Vector3.up * 0.2f;
        }

        /// Caja invisible que llena el interior de la cabaña mientras esté cerrada (puerta y ventanas incluidas).
        GameObject PonerBloqueo(GameObject casa)
        {
            BoxCollider piso = null;
            foreach (var col in casa.GetComponentsInChildren<BoxCollider>(true)) if (col.name.StartsWith("cabinFloor")) { piso = col; break; }
            if (piso == null) return null;
            var bl = new GameObject("coop_bloqueo_cabana");
            bl.transform.SetParent(casa.transform, true);
            bl.transform.rotation = piso.transform.rotation;
            bl.transform.position = piso.transform.TransformPoint(piso.center) + Vector3.up * 1.3f;
            var bc = bl.AddComponent<BoxCollider>();
            Vector3 ls = piso.transform.lossyScale;
            Vector3 tam = Vector3.Scale(piso.size, new Vector3(Mathf.Abs(ls.x), Mathf.Abs(ls.y), Mathf.Abs(ls.z)));
            // el eje más delgado del piso es su grosor: los otros dos son el largo y el ancho
            int fino = tam.x <= tam.y && tam.x <= tam.z ? 0 : tam.y <= tam.z ? 1 : 2;
            Vector3 d = new Vector3(tam.x, tam.y, tam.z); d[fino] = 2.4f;
            for (int i = 0; i < 3; i++) if (i != fino) d[i] = Mathf.Max(1f, d[i] - 0.5f);
            bc.size = d;
            return bl;
        }

        /// Puerta de tablas del tamaño del hueco, con la bisagra en un costado y colisión mientras esté cerrada.
        void PonerPuerta(Cabana cb, GameObject casa, Bounds local)
        {
            // eje más delgado = grosor; el más largo horizontal = ancho
            Vector3 s = local.size;
            int ejeGrosor = s.x < s.z ? 0 : 2, ejeAncho = 2 - ejeGrosor;
            var bisagra = new GameObject("coop_puerta_bisagra");
            bisagra.transform.SetParent(casa.transform, false);
            Vector3 lado = Vector3.zero; lado[ejeAncho] = s[ejeAncho] * 0.5f;
            bisagra.transform.localPosition = local.center - lado;
            var tabla = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tabla.name = "coop_puerta";
            tabla.transform.SetParent(bisagra.transform, false);
            Vector3 tam = s; tam[ejeGrosor] = Mathf.Max(Mathf.Min(s[ejeGrosor], 0.12f), 0.08f); tam[ejeAncho] = Mathf.Max(0.6f, s[ejeAncho] - 0.22f); tam.y = Mathf.Max(1.2f, s.y - 0.12f);
            tabla.transform.localPosition = lado;
            tabla.transform.localScale = tam;
            Material madera = null;
            foreach (var r in casa.GetComponentsInChildren<MeshRenderer>(true)) if (r.enabled && (r.name.StartsWith("cabinWallsExterior") || r.name.StartsWith("porchFloor"))) { madera = r.sharedMaterial; break; }
            if (madera != null) { var m = new Material(madera) { name = "coop_puerta_madera" }; m.mainTextureScale = new Vector2(1f, 2f); tabla.GetComponent<Renderer>().sharedMaterial = m; }
            // cerrojo de hierro, del lado contrario a la bisagra
            var cerrojo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(cerrojo.GetComponent<Collider>());
            cerrojo.name = "coop_cerrojo";
            cerrojo.transform.SetParent(bisagra.transform, false);
            cerrojo.transform.localPosition = lado * 1.8f + new Vector3(0f, -0.05f, 0f);
            Vector3 sc = new Vector3(0.1f, 0.12f, 0.1f); sc[ejeGrosor] = tam[ejeGrosor] + 0.08f;
            cerrojo.transform.localScale = sc;
            var mh = new Material(Shader.Find("Standard")) { color = new Color(0.15f, 0.14f, 0.13f) }; mh.SetFloat("_Metallic", 0.7f);
            cerrojo.GetComponent<Renderer>().sharedMaterial = mh;
            cb.Puerta = bisagra.transform;
        }

        void GirarPuerta(Cabana cb, float t)
        {
            if (cb.Puerta == null) return;
            cb.Giro = t;
            cb.Puerta.localRotation = Quaternion.Euler(0f, -100f * t, 0f);
        }

        void ActualizarCabanas()
        {
            cabanaCerca = null;
            var j = plugin.Jugador;
            if (j == null) return;
            foreach (var c in cabanas)
            {
                if (c.Abierta && c.Giro < 1f) GirarPuerta(c, Mathf.Min(1f, c.Giro + Time.deltaTime * 1.2f));
                if (!c.Abierta)
                {
                    // si ya estás dentro (entraste por otro lado) la cabaña no puede seguir cerrada: se abre sola y da su botín
                    Vector3 d = j.transform.position - c.Dentro; float h = new Vector2(d.x, d.z).magnitude;
                    if (h < 1.0f && Mathf.Abs(d.y) < 2.0f) { AbrirCabana(c, true); continue; }   // solo si de verdad estás adentro (no al pasar junto a la puerta)
                    if (Vector3.Distance(j.transform.position, c.Centro) < 2.4f) cabanaCerca = c;
                }
            }
            if (cabanaCerca != null && Controles.Interactuar() && Entrada.Enfocado) ForzarCabana(cabanaCerca);
        }

        void ForzarCabana(Cabana c)
        {
            if (c.Abierta) return;
            if (LibrosTienda.Tiene(LibrosTienda.LlaveMaestra)) { AbrirCabana(c, true); return; }   // la llave maestra abre sin ganzúas
            var gz = Ganzuas();
            if (gz == null || gz.Value <= 0) { Viaje.Avisar("Necesitas una ganzúa."); return; }
            if (azar.NextDouble() >= ExitoGanzua)
            {
                gz.Value = gz.Value - 1;
                Viaje.Avisar("La ganzúa se rompió. (Te quedan " + gz.Value + ")");
                plugin.Log("Cabañas: ganzúa rota en la cabaña " + (c.N + 1));
                return;
            }
            AbrirCabana(c, true);
        }

        void AbrirCabana(Cabana c, bool mia)
        {
            if (c.Abierta) return;
            c.Abierta = true;
            if (c.Bloqueo != null) { Object.Destroy(c.Bloqueo); c.Bloqueo = null; }
            AnotarLua(VarCabanasAbiertas, "c" + c.N);
            // Botín (el mismo en los dos juegos: semilla de la cabaña; ids iguales para quitarlo al recogerlo).
            var r = new System.Random(c.Semilla);
            int hechos = 0;
            // cuántos botines: 1 (100 %), 2 (50 %), 3 (25 %), 4 (10 %), 5 (1 %) — siempre hay al menos uno
            double u = r.NextDouble(); int cuantos = 1;
            if (u < 0.50) cuantos = 2; if (u < 0.25) cuantos = 3; if (u < 0.10) cuantos = 4; if (u < 0.01) cuantos = 5;
            for (int k = 0; k < cuantos; k++)
            {
                string tipo = BotinCabana[r.Next(BotinCabana.Length)];
                Vector3 p = c.Dentro + new Vector3((float)r.NextDouble() * 2f - 1f, 0.05f, (float)r.NextDouble() * 2f - 1f);
                if (ClonarBotin(tipo, p, (float)r.NextDouble() * 360f, "cabana:" + c.N + ":" + k) != null) hechos++;
            }
            if (mia)
            {
                int n = c.N;
                for (int i = 0; i < 3; i++) plugin.EnviarCabana(Red.Armar(w => w.Write(n)));
                Viaje.Avisar("Abriste la cabaña.");
            }
            plugin.Log("Cabañas: cabaña " + (c.N + 1) + " abierta por " + (mia ? "mí" : plugin.NombreCompanero) + " (" + hechos + " cosas dentro)");
        }

        public void RecibirCabana(BinaryReader r)
        {
            int n = r.ReadInt32();
            foreach (var c in cabanas) if (c.N == n) { AbrirCabana(c, false); return; }
            AnotarLua(VarCabanasAbiertas, "c" + n);
        }

        public void DibujarCabana()
        {
            if (cabanaCerca == null || plugin.Jugador == null) return;
            if (estiloCofre == null)
            {
                estiloCofre = new GUIStyle(GUI.skin.label) { fontSize = Opciones.T(20), alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
                estiloCofre.normal.textColor = new Color(1f, 0.9f, 0.6f);
            }
            var gz = Ganzuas(); int n = gz != null ? gz.Value : 0;
            GUI.Label(new Rect(0, Screen.height * 0.6f, Screen.width, 30f), LibrosTienda.Tiene(LibrosTienda.LlaveMaestra) ? "Cabaña cerrada con llave. E: abrir con la llave maestra" : n > 0 ? "Cabaña cerrada con llave. " + TextoForzar(n) : "Cabaña cerrada con llave: necesitas una ganzúa", estiloCofre);
        }

        public string DescribirCabanas()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var c in cabanas) sb.Append("cabaña ").Append(c.N + 1).Append(' ').Append((c.Centro - Ancla()).ToString("F0")).Append(c.Abierta ? " abierta; " : " cerrada; ");
            return sb.ToString();
        }
        public Vector3? PosCabana(int n) { foreach (var c in cabanas) if (c.N == n) return c.Centro - Ancla(); return null; }
        public void OrdenForzarCabana(int n) { foreach (var c in cabanas) if (c.N == n) ForzarCabana(c); }
        public void OrdenAbrirCabana(int n) { foreach (var c in cabanas) if (c.N == n) AbrirCabana(c, true); }

        /// Más luz en cabañas y bocas de mina (pedido 2026-11-18: que se distingan desde lejos): farol con llama junto a la
        /// puerta y una luz cálida amplia; en las minas, dos braseros con fuego a los lados de la entrada.
        void IluminarCasasYMinas()
        {
            int nc = 0, nm = 0;
            foreach (var c in cabanas)
            {
                if (c.Raiz == null) continue;
                Bounds b; if (!CajaDe(new List<GameObject> { c.Raiz }, out b)) continue;
                Vector3 puerta = c.Puerta != null ? c.Puerta.position : c.Centro;
                Vector3 fuera = puerta - b.center; fuera.y = 0f; if (fuera.sqrMagnitude < 0.01f) fuera = Vector3.forward; fuera.Normalize();
                Vector3 lado = Vector3.Cross(Vector3.up, fuera).normalized;
                Vector3 farol = new Vector3(puerta.x, puerta.y, puerta.z) + fuera * 0.35f + lado * 1.1f + Vector3.up * 2.1f;
                Farol(c.Raiz.transform, c.Raiz.transform.InverseTransformPoint(farol));
                Llama.Crear(farol + Vector3.up * 0.05f, c.Raiz.transform, 0.12f, false, false);
                var g = new GameObject("coop_luz_cabana_lejos"); g.transform.SetParent(c.Raiz.transform, true); g.transform.position = puerta + fuera * 2.5f + Vector3.up * 3f;
                var l = g.AddComponent<Light>(); l.type = LightType.Point; l.color = new Color(1f, 0.62f, 0.3f); l.range = 26f; l.intensity = 1.8f; l.shadows = LightShadows.None;
                g.AddComponent<Titilar>();
                nc++;
            }
            foreach (var m in minas)
            {
                if (m.Boca == null) continue;
                var t = m.Boca.transform;
                foreach (var sx in new[] { -2.7f, 2.7f }) Brasero(t, new Vector3(sx, 0f, -1.6f));
                var g = new GameObject("coop_luz_mina_lejos"); g.transform.SetParent(t, false); g.transform.localPosition = new Vector3(0f, 3.5f, -4f);
                var l = g.AddComponent<Light>(); l.type = LightType.Point; l.color = new Color(1f, 0.55f, 0.22f); l.range = 28f; l.intensity = 2f; l.shadows = LightShadows.None;
                g.AddComponent<Titilar>();
                nm++;
            }
            plugin.Log("Luces: " + nc + " cabañas/casas y " + nm + " bocas de mina iluminadas");
        }
    }
}
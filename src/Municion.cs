// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using System.Collections.Generic;
using HutongGames.PlayMaker;
using PixelCrushers.DialogueSystem;
using UnityEngine;

namespace WitchHuntCoop
{
    /// Munición separada por arma (solo en la partida cooperativa): el juego tiene una sola reserva
    /// (variable global silver_bullets_v2) que pasa al arma que tengas en la mano. Aquí se llevan dos
    /// reservas, pistola y mosquete, y al cambiar de arma se carga la suya en la global.
    /// Las cajas de balas son de uno u otro tipo (fijo por caja); al recogerla, lo que sumó la caja
    /// va a la reserva de su arma aunque tengas otra en la mano. También "Segunda bala" (libro).
    public class Municion
    {
        public const string VarPistola = "coop_balas_pistola", VarMosquete = "coop_balas_mosquete", VarEscopeta = "coop_balas_escopeta", VarFlechas = "coop_flechas";
        public const int Pistola = 1, Mosquete = 2, EscopetaR = 3, Flechas = 4;
        public const int MaxFlechas = 20, MaxFlechasBolsa = 60;

        readonly Plugin plugin;
        readonly int[] reserva = new int[5];   // [1] pistola, [2] mosquete, [3] escopeta, [4] flechas
        bool cargadas;
        int armaVista = -1;      // 1, 2 o 0 (otra)
        int ultimaGun = Pistola; // la última arma de fuego en la mano
        int gVista = int.MinValue;
        int escenaVista = -1;

        // Cajas cercanas, para saber de qué tipo era la que se recogió.
        readonly Dictionary<GameObject, int> cajasCerca = new Dictionary<GameObject, int>();
        int tipoRecogido; float horaRecogida = -10f;
        float proximaBusqueda;
        GameObject cajaMirada; int tipoMirada;

        // Segunda bala
        int cargadorVisto = -1; bool ultimaGratis;

        public Municion(Plugin p) { plugin = p; }

        static FsmInt G() { var g = FsmVariables.GlobalVariables; return g == null ? null : g.FindFsmInt("silver_bullets_v2"); }
        static int Max() { var g = FsmVariables.GlobalVariables; var m = g == null ? null : g.FindFsmInt("MaxAmmo_Current"); return m != null && m.Value > 0 ? m.Value : 20; }
        /// Tope de cada reserva: las flechas tienen el suyo (20, o 60 con Bolsa ampliada); las balas, el del juego.
        public static int MaxDe(int t) { return t == Flechas ? (LibrosTienda.Tiene(LibrosTienda.BolsaAmpliada) ? MaxFlechasBolsa : MaxFlechas) : Max(); }

        static int TipoArma(PlayerWeapons pw)
        {
            if (pw == null || pw.weaponOrder == null || pw.currentWeapon < 0 || pw.currentWeapon >= pw.weaponOrder.Length || pw.weaponOrder[pw.currentWeapon] == null) return 0;
            string n = pw.weaponOrder[pw.currentWeapon].name;
            if (n.StartsWith("Pistol")) return Pistola;
            if (n.StartsWith("Musket")) return Mosquete;
            if (n.StartsWith("Shotgun")) return EscopetaR;
            if (n.StartsWith("Crossbow")) return Flechas;
            return 0;
        }

        float proxContador; bool contadorAvisado;
        /// Contador de balas (pedido 2026-11-18: "al cambiar de arma apenas se ven las balas"): el contador del juego son dos
        /// GUIText (texto blanco + sombra). Si al añadir un arma aparece un segundo contador, o el texto queda transparente, solo
        /// se ve la sombra oscura. Cada segundo: se deja solo el contador que usa el juego y el texto opaco.
        void ArreglarContador()
        {
            if (Time.unscaledTime < proxContador) return;
            proxContador = Time.unscaledTime + 1f;
            var pw = Object.FindObjectOfType<PlayerWeapons>();
            var bueno = pw != null ? pw.ammoGuiObjInstance : null;
            var raices = new HashSet<Transform>();
            foreach (var at in Object.FindObjectsOfType<AmmoText>()) if (at != null) raices.Add(at.transform.root);
            int borrados = 0;
            if (bueno != null) foreach (var r in raices) if (r != bueno.transform.root) { Object.Destroy(r.gameObject); borrados++; }
            int opacos = 0;
            foreach (var at in Object.FindObjectsOfType<AmmoText>())
            {
                var gt = at != null ? at.GetComponent<GUIText>() : null;
                if (gt != null && gt.color.a < 0.98f) { var c = gt.color; c.a = 1f; gt.color = c; opacos++; }
            }
            if ((borrados > 0 || opacos > 0) && !contadorAvisado) { contadorAvisado = true; plugin.Log("Munición: contador de balas arreglado (" + borrados + " duplicados, " + opacos + " textos transparentes)"); }
        }

        public void Actualizar()
        {
            try { ArreglarContador(); } catch { }
            var j = plugin.Jugador;
            if (!Guardado.ModoCoop || j == null) { cargadas = false; armaVista = -1; gVista = int.MinValue; return; }
            var g = G();
            var pw = Rendimiento.Armas;
            if (g == null || pw == null) return;
            if (escenaVista != plugin.Escena) { escenaVista = plugin.Escena; cargadas = false; cajasCerca.Clear(); }
            if (!cargadas) Cargar(g.Value);

            BuscarCajas(j.transform.position);
            int arma = TipoArma(pw);
            int gun = arma != 0 ? arma : ultimaGun;

            if (armaVista == -1) { armaVista = arma; ultimaGun = gun; g.Value = reserva[gun]; gVista = g.Value; return; }

            // ¿Cambió la global desde la última vez? (disparo/recarga, caja, cofre, muerte...)
            if (g.Value != gVista)
            {
                int delta = g.Value - gVista;
                if (delta > 0 && Time.time - horaRecogida < 2f && tipoRecogido != 0 && tipoRecogido != ultimaGun)
                {
                    // Caja del otro tipo: lo que sumó va a su arma y la global vuelve a la de la mano.
                    reserva[tipoRecogido] = Mathf.Min(reserva[tipoRecogido] + delta, MaxDe(tipoRecogido));
                    g.Value = gVista;
                    Viaje.Avisar("+" + delta + " balas de " + Nombre(tipoRecogido) + " (" + reserva[tipoRecogido] + ")");
                    plugin.Log("Munición: caja de " + Nombre(tipoRecogido) + " +" + delta + " → " + reserva[tipoRecogido]);
                    tipoRecogido = 0;
                }
                else reserva[ultimaGun] = g.Value;
                Guardar();
            }

            // Nunca por encima del máximo del juego (20, 60 con su bolsa, 120 con Cargamento pesado).
            int tope = MaxDe(ultimaGun);
            if (g.Value > tope) { g.Value = tope; reserva[ultimaGun] = tope; Guardar(); }
            for (int k = 1; k <= 4; k++) if (reserva[k] > MaxDe(k)) reserva[k] = MaxDe(k);

            // Cambio de arma de fuego: la global pasa a ser la reserva de la nueva.
            if (gun != ultimaGun)
            {
                reserva[ultimaGun] = g.Value;
                ultimaGun = gun;
                g.Value = reserva[gun];
                var wb = pw.weaponOrder[pw.currentWeapon] != null ? pw.weaponOrder[pw.currentWeapon].GetComponent<WeaponBehavior>() : null;
                if (wb != null && arma != 0) wb.ammo = g.Value;
                plugin.Log("Munición: ahora " + Nombre(gun) + " (pistola " + reserva[Pistola] + ", mosquete " + reserva[Mosquete] + ", escopeta " + reserva[EscopetaR] + ", flechas " + reserva[Flechas] + ")");
                Guardar();
            }
            armaVista = arma;
            gVista = g.Value;

            SegundaBala(pw, arma);
            DosBalas(pw, arma, g);
            SyncFlechas(pw, arma, g);
        }

        void Cargar(int g)
        {
            cargadas = true;
            int p = -1, m = -1;
            try { var v = DialogueLua.GetVariable(VarPistola); if (v.AsString != "nil" && v.AsString != "") p = v.AsInt; } catch { }
            try { var v = DialogueLua.GetVariable(VarMosquete); if (v.AsString != "nil" && v.AsString != "") m = v.AsInt; } catch { }
            if (p < 0 || m < 0) { p = m = g; plugin.Log("Munición: reservas separadas nuevas (" + g + " para cada arma)"); }
            reserva[Pistola] = p; reserva[Mosquete] = m;
            int e = 0;   // la escopeta llegó después: si la partida no la tenía, su reserva empieza vacía
            try { var v = DialogueLua.GetVariable(VarEscopeta); if (v.AsString != "nil" && v.AsString != "") e = v.AsInt; } catch { }
            reserva[EscopetaR] = e;
            int f = 0;
            try { var v = DialogueLua.GetVariable(VarFlechas); if (v.AsString != "nil" && v.AsString != "") f = v.AsInt; } catch { }
            reserva[Flechas] = f;
            armaVista = -1;
            Guardar();
        }

        void Guardar()
        {
            DialogueLua.SetVariable(VarPistola, reserva[Pistola]);
            DialogueLua.SetVariable(VarMosquete, reserva[Mosquete]);
            DialogueLua.SetVariable(VarEscopeta, reserva[EscopetaR]);
            DialogueLua.SetVariable(VarFlechas, reserva[Flechas]);
        }

        /// Suma flechas (compradas o encontradas). Devuelve cuántas cupieron.
        public int SumarFlechas(int n)
        {
            if (!cargadas) { var g0 = G(); if (g0 != null) Cargar(g0.Value); }
            int cabe = Mathf.Max(0, Mathf.Min(n, MaxDe(Flechas) - reserva[Flechas]));
            reserva[Flechas] += cabe;
            var g = G();
            if (g != null && ultimaGun == Flechas) { g.Value = reserva[Flechas]; gVista = g.Value; }
            Guardar();
            return cabe;
        }
        /// Suma balas a la reserva de un arma de fuego concreta (compra en la tienda). Devuelve cuántas cupieron.
        public int SumarBalas(int tipo, int n)
        {
            if (tipo != Pistola && tipo != Mosquete && tipo != EscopetaR) return 0;
            if (!cargadas) { var g0 = G(); if (g0 != null) Cargar(g0.Value); }
            int cabe = Mathf.Max(0, Mathf.Min(n, MaxDe(tipo) - reserva[tipo]));
            reserva[tipo] += cabe;
            var g = G();
            if (g != null && ultimaGun == tipo) { g.Value = reserva[tipo]; gVista = g.Value; }   // si es el arma de la mano, la global la sigue
            Guardar();
            return cabe;
        }
        public int BalasQueTengo(int tipo) { return reserva[tipo]; }
        public int FlechasQueTengo { get { return reserva[Flechas]; } }

        static string Nombre(int t) { return t == Pistola ? "pistola" : t == Mosquete ? "rifle" : t == Flechas ? "ballesta" : "escopeta"; }

        /// Tipo de una caja de balas: fijo según su identidad (mitad y mitad).
        public static int TipoCaja(GameObject caja)
        {
            Vector3 p = caja.transform.position;
            unchecked
            {
                int h = caja.name.GetHashCode() * 31 + Mathf.RoundToInt(p.x * 3f) * 17 + Mathf.RoundToInt(p.z * 3f);
                int r = ((h % 3) + 3) % 3;   // un tercio para cada arma
                return r == 0 ? Pistola : r == 1 ? Mosquete : EscopetaR;
            }
        }

        readonly HashSet<GameObject> preparadas = new HashSet<GameObject>(), renombradas = new HashSet<GameObject>();
        static Material matPaquete;
        static Material MaterialPaquete()
        {
            if (matPaquete != null) return matPaquete;
            const int W = 128, H = 96;
            var t = new Texture2D(W, H, TextureFormat.ARGB32, false) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp };
            var rng = new System.Random(5);
            for (int y = 0; y < H; y++) for (int x = 0; x < W; x++)
            {
                float n = (float)rng.NextDouble() * 0.07f;
                Color c = new Color(0.80f - n, 0.68f - n, 0.43f - n, 1f);
                bool borde = x < 3 || y < 3 || x > W - 4 || y > H - 4;
                bool etiqueta = x > 22 && x < 106 && y > 26 && y < 70;
                if (etiqueta) c = new Color(0.90f - n, 0.86f - n, 0.66f - n, 1f);
                if (etiqueta && (x < 25 || x > 103 || y < 29 || y > 67)) c = new Color(0.25f, 0.18f, 0.1f, 1f);   // marco de la etiqueta
                if (etiqueta && y > 36 && y < 62 && (y % 8) < 3 && x > 30 && x < 98 - (y * 3) % 23) c = new Color(0.2f, 0.14f, 0.08f, 1f);   // renglones de texto
                if (Mathf.Abs(x - 64) < 2 || Mathf.Abs(y - 48) < 2) c = new Color(0.30f, 0.22f, 0.13f, 1f);   // cordel
                if (borde) c = new Color(0.55f, 0.45f, 0.28f, 1f);
                t.SetPixel(x, y, c);
            }
            t.Apply();
            matPaquete = new Material(Shader.Find("Standard")) { mainTexture = t, name = "coop_paquete_balas" };
            matPaquete.SetFloat("_Glossiness", 0.1f);
            return matPaquete;
        }

        /// El modelo de todas las cajas de balas es un paquete de cartuchos de papel atado con cordel (no el saco de arena del juego).
        void ReemplazarModelo(GameObject caja)
        {
            foreach (var r in caja.GetComponentsInChildren<MeshRenderer>(true)) if (!r.name.StartsWith("coop_")) r.enabled = false;
            foreach (var lg in caja.GetComponentsInChildren<LODGroup>(true)) lg.enabled = false;
            var p = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(p.GetComponent<Collider>());
            p.name = "coop_paquete";
            p.transform.SetParent(caja.transform, false);
            p.transform.localScale = new Vector3(0.26f, 0.08f, 0.17f) / Mathf.Max(0.001f, caja.transform.lossyScale.x);
            p.transform.localPosition = Vector3.up * 0.04f / Mathf.Max(0.001f, caja.transform.lossyScale.y);
            p.transform.localRotation = Quaternion.Euler(0f, (caja.name.GetHashCode() & 255) * 1.4f, 0f);
            p.GetComponent<Renderer>().sharedMaterial = MaterialPaquete();
        }

        static string ArmaDe(int t) { return t == Pistola ? "pistola" : t == Mosquete ? "mosquete" : "escopeta"; }

        /// Cada caja de balas (el paquete de cartuchos del juego) da de 2 a 10 balas y solo para su arma: el nombre que se lee
        /// al mirarla es "Balas de plata (pistola | mosquete | escopeta)".
        void Preparar(GameObject caja, int tipo)
        {
            if (caja == null || !preparadas.Add(caja)) return;
            int h;
            unchecked { Vector3 p = caja.transform.position; h = (caja.name.GetHashCode() * 31 + Mathf.RoundToInt(p.x * 3f) * 17 + Mathf.RoundToInt(p.z * 3f)) * 7 + 13; }
            int cuantas = 2 + (((h % 9) + 9) % 9);   // 2..10
            foreach (var f in caja.GetComponentsInChildren<PlayMakerFSM>(true))
            {
                if (f.FsmName != "Logic") continue;
                foreach (var st in f.FsmStates)
                {
                    if (st.Name != "State 5") continue;
                    foreach (var a in st.Actions)
                    {
                        var ia = a as HutongGames.PlayMaker.Actions.IntAdd;
                        if (ia != null && ia.intVariable != null && ia.intVariable.Name == "silver_bullets_v2") ia.add.Value = cuantas;
                    }
                }
            }
            ReemplazarModelo(caja);
            string nombre = " (" + ArmaDe(tipo) + ")";
            foreach (var c in caja.GetComponentsInChildren<Component>(true))
            {
                if (c == null || c is Transform) continue;
                foreach (var fi in c.GetType().GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                {
                    if (fi.FieldType != typeof(string)) continue;
                    var v = fi.GetValue(c) as string;
                    if (string.IsNullOrEmpty(v) || v.Contains("(")) continue;
                    bool esNombre = v.Equals("Silver bullets", System.StringComparison.OrdinalIgnoreCase) || v.IndexOf("balas de plata", System.StringComparison.OrdinalIgnoreCase) >= 0;
                    if (!esNombre) continue;
                    try { fi.SetValue(c, "Balas de plata" + nombre); renombradas.Add(caja); } catch { }
                }
            }
            plugin.Log("Munición: caja " + caja.name + " → " + ArmaDe(tipo) + ", " + cuantas + " balas" + (renombradas.Contains(caja) ? ", nombre cambiado" : ", (no encontré su nombre)"));
        }

        void BuscarCajas(Vector3 yo)
        {
            // Una caja que estaba al lado y desapareció: se acaba de recoger.
            List<GameObject> fuera = null;
            foreach (var kv in cajasCerca)
                if (kv.Key == null || !kv.Key.activeInHierarchy)
                {
                    tipoRecogido = kv.Value; horaRecogida = Time.time;
                    (fuera ?? (fuera = new List<GameObject>())).Add(kv.Key);
                }
            if (fuera != null) foreach (var f in fuera) cajasCerca.Remove(f);

            if (Time.time < proximaBusqueda) return;
            proximaBusqueda = Time.time + 0.5f;
            cajaMirada = null; float mejor = 3.5f;
            foreach (var c in Object.FindObjectsOfType<PickupButton>())
            {
                Transform t = c.transform;
                while (t.parent != null && !t.name.StartsWith("Ammo_Silverbullets")) t = t.parent;
                if (!t.name.StartsWith("Ammo_Silverbullets")) continue;
                int tipo = TipoCaja(t.gameObject);
                Preparar(t.gameObject, tipo);   // todas las de la escena: modelo, nombre y cantidad
                float d = Vector3.Distance(t.position, yo);
                if (d > 6f) continue;
                cajasCerca[t.gameObject] = tipo;
                if (d < mejor) { mejor = d; cajaMirada = t.gameObject; tipoMirada = tipo; }
            }
        }

        // ---------------- Escopeta: 2 balas por disparo ----------------

        int cargaEscopetaVista = -1;

        /// La escopeta carga 1 disparo, pero cada disparo cuesta 2 balas: al terminar la recarga se
        /// descuenta la segunda; si no queda, la recarga no vale (se devuelve la bala y queda vacía).
        void DosBalas(PlayerWeapons pw, int arma, FsmInt g)
        {
            if (arma != EscopetaR) { cargaEscopetaVista = -1; ammoEscopetaVista = -1; return; }
            var wb = pw.weaponOrder[pw.currentWeapon].GetComponent<WeaponBehavior>();
            if (wb == null) return;
            // El juego solo sincroniza la reserva con SUS armas: aquí se hace a mano. Si la recarga gastó
            // de la munición del arma, eso manda; si no, el arma toma la reserva.
            // (Solo cuenta como gasto si bajó la munición del arma; si subió la reserva —caja, cofre— el arma la toma.)
            if (cargaEscopetaVista >= 0 && ammoEscopetaVista >= 0 && wb.ammo < ammoEscopetaVista && wb.ammo < g.Value) { g.Value = wb.ammo; reserva[EscopetaR] = g.Value; gVista = g.Value; Guardar(); }
            else if (wb.ammo != g.Value) wb.ammo = g.Value;
            // (el cargador de la escopeta ya es de 2: la recarga del juego descuenta las 2 balas sola)
            cargaEscopetaVista = wb.bulletsLeft;
            ammoEscopetaVista = wb.ammo;
        }
        int ammoEscopetaVista = -1;

        // ---------------- Ballesta: el juego no sincroniza sus flechas ----------------

        int ammoBallestaVista = -1;
        void SyncFlechas(PlayerWeapons pw, int arma, FsmInt g)
        {
            if (arma != Flechas) { ammoBallestaVista = -1; return; }
            var wb = pw.weaponOrder[pw.currentWeapon].GetComponent<WeaponBehavior>();
            if (wb == null) return;
            if (ammoBallestaVista >= 0 && wb.ammo < ammoBallestaVista && wb.ammo < g.Value) { g.Value = wb.ammo; reserva[Flechas] = g.Value; gVista = g.Value; Guardar(); }
            else if (wb.ammo != g.Value) wb.ammo = g.Value;
            ammoBallestaVista = wb.ammo;
        }

        // ---------------- Segunda bala ----------------

        /// 30 % de que un disparo no gaste la bala del cargador: se puede volver a disparar sin
        /// recargar, pero nunca dos veces gratis seguidas.
        void SegundaBala(PlayerWeapons pw, int arma)
        {
            if (arma == 0 || arma == EscopetaR || arma == Flechas || !LibrosTienda.Tiene(LibrosTienda.SegundaBala)) { cargadorVisto = -1; return; }
            var wb = pw.weaponOrder[pw.currentWeapon].GetComponent<WeaponBehavior>();
            if (wb == null) return;
            if (cargadorVisto >= 0 && wb.bulletsLeft == cargadorVisto - 1)
            {
                if (!ultimaGratis && Random.value < 0.30f)
                {
                    wb.bulletsLeft += 1; ultimaGratis = true;
                    var bl = FsmVariables.GlobalVariables.FindFsmInt("bullets_left");
                    if (bl != null) bl.Value = wb.bulletsLeft;
                    // (2026-11-19) sin texto en pantalla al salir la segunda bala (pedido)
                }
                else ultimaGratis = false;
            }
            cargadorVisto = wb.bulletsLeft;
        }

        /// Vuelve a leer las reservas de la partida (p. ej. tras darlas el equipo inicial).
        public void Recargar() { cargadas = false; armaVista = -1; gVista = int.MinValue; }

        public string Describir()
        {
            var g = G();
            return "pistola=" + reserva[Pistola] + " mosquete=" + reserva[Mosquete] + " escopeta=" + reserva[EscopetaR] + " flechas=" + reserva[Flechas] + " G=" + (g != null ? g.Value : -1) + " en la mano=" + armaVista + " última de fuego=" + ultimaGun + " coop=" + Guardado.ModoCoop;
        }

        public void Dibujar()
        {
            if (cajaMirada == null || !cajaMirada.activeInHierarchy || !Guardado.ModoCoop) return;
            if (estilo == null)
            {
                estilo = new GUIStyle(GUI.skin.label) { fontSize = Opciones.T(18), alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
                estilo.normal.textColor = new Color(0.85f, 0.9f, 1f);
            }
            if (!renombradas.Contains(cajaMirada)) GUI.Label(new Rect(0, Screen.height * 0.64f, Screen.width, 28f), "Balas de plata (" + ArmaDe(tipoMirada) + ")", estilo);   // si el nombre del juego ya cambió, no se repite
        }
        GUIStyle estilo;
    }
}

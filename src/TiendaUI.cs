// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using System.Collections.Generic;
using HutongGames.PlayMaker;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace WitchHuntCoop
{
    /// Los libros del cooperativo (y las ganzúas) dentro de la interfaz del propio juego:
    ///  - Tienda del comerciante: artículos más de la lista (copias de "Sombra (libro)" sin su lógica),
    ///    con icono, nombre, precio y descripción al pasar el ratón; al hacer clic se compran.
    ///  - Inventario: una fila más de habilidades con el icono de cada libro; oscura (como las del juego
    ///    que aún no tienes) hasta comprarla y clara después.
    public class TiendaUI
    {
        readonly Plugin plugin;
        int escenaVista = -1;
        bool tiendaHecha, inventarioHecho;
        float proximo;
        Texture2D iconoBase, iconoGanzua;
        ScrollRect desliz;
        Color? barraNormal;
        readonly List<KeyValuePair<LibrosTienda.Libro, RawImage>> habilidades = new List<KeyValuePair<LibrosTienda.Libro, RawImage>>();
        readonly Dictionary<LibrosTienda.Libro, GameObject> articulos = new Dictionary<LibrosTienda.Libro, GameObject>();

        /// Color del icono de cada libro (el del libro del juego, teñido).
        static Color ColorDe(string v)
        {
            switch (v)
            {
                case LibrosTienda.Filo: return new Color(1f, 0.45f, 0.4f);
                case LibrosTienda.Pistolero: return new Color(1f, 0.8f, 0.35f);
                case LibrosTienda.Mosquetero: return new Color(1f, 0.6f, 0.25f);
                case LibrosTienda.SegundaBala: return new Color(0.75f, 0.85f, 1f);
                case LibrosTienda.Herbolario: return new Color(0.5f, 1f, 0.5f);
                case LibrosTienda.OjoDeBruja: return new Color(0.85f, 0.5f, 1f);
                default: return new Color(1f, 0.55f, 0.75f);   // curación
            }
        }

        public TiendaUI(Plugin p) { plugin = p; }

        /// Estos artículos de la tienda se llaman solo por su nombre, sin el «(libro)» del final (pedido 2026-11-11).
        public static bool SinLibro(string v)
        {
            return v == LibrosTienda.CargamentoPesado || v == LibrosTienda.BotasLigeras || v == LibrosTienda.ArmaduraTitanio || v == LibrosTienda.CargadorRifle
                || v == LibrosTienda.Filo || v == LibrosTienda.Pistolero || v == LibrosTienda.Mosquetero || v == LibrosTienda.EscopetaRecarga
                || v == LibrosTienda.EscopetaDano || v == LibrosTienda.BallestaDano || v == LibrosTienda.BallestaTriple;
        }

        static readonly Dictionary<string, Texture2D> iconos = new Dictionary<string, Texture2D>();

        /// Iconos propios (en el mod): "item_" (tienda: blanco sobre transparente) o "skill_" (habilidad).
        public static Texture2D Icono(string tipo, string var)
        {
            string n = "iconos." + tipo + "_" + var + ".png";
            Texture2D t;
            if (iconos.TryGetValue(n, out t) && t != null) return t;
            using (var st = typeof(TiendaUI).Assembly.GetManifestResourceStream(n))
            {
                if (st == null) return null;
                var bytes = new byte[st.Length]; st.Read(bytes, 0, bytes.Length);
                t = new Texture2D(2, 2, TextureFormat.ARGB32, false) { hideFlags = HideFlags.HideAndDontSave };
                t.LoadImage(bytes);
                t.wrapMode = TextureWrapMode.Clamp;
            }
            iconos[n] = t;
            return t;
        }

        /// Descripciones nuevas para artículos del juego (las pone su lógica al pasar el ratón; se reemplazan).
        static readonly Dictionary<string, string> textosJuego = new Dictionary<string, string> {
            { "Paquete de balas de plata", "5 balas de plata que pueden usarse en tus armas de fuego." },
            { "Bala de plata", "La bala de plata puede utilizarse en tus armas de fuego." },
            { "Bolsa de munición", "La bolsa de munición permite llevar 60 balas para tus armas de fuego." },
        };
        Text nombreTienda, descTienda;
        // Las dos balas de plata originales del juego (sin arma): el juego las vuelve a mostrar al abrir la tienda.
        readonly List<GameObject> balasJuego = new List<GameObject>();
        Transform contenidoTienda; float proxOcultar;

        /// Las filas originales "Paquete de balas de plata" y "Bala de plata" (sin arma) no deben verse: el juego las vuelve
        /// a crear/mostrar al abrir la tienda, así que se buscan por su texto (no por referencia) varias veces por segundo.
        void OcultarBalasJuego()
        {
            if (Time.realtimeSinceStartup < proxOcultar) return;
            proxOcultar = Time.realtimeSinceStartup + 0.2f;
            foreach (var b in balasJuego) if (b != null && b.activeSelf) b.SetActive(false);
            if (contenidoTienda == null) return;
            foreach (Transform t in contenidoTienda)
            {
                if (!t.gameObject.activeSelf || t.name.StartsWith("coop_")) continue;
                var tx = t.Find("Text"); var tt = tx != null ? tx.GetComponent<Text>() : null;
                if (tt == null) continue;
                string x = tt.text.Trim();
                if (x == "Paquete de balas de plata" || x == "Bala de plata") { t.gameObject.SetActive(false); if (!balasJuego.Contains(t.gameObject)) balasJuego.Add(t.gameObject); }
            }
        }

        public void TextosJuego()
        {
            if (nombreTienda == null) nombreTienda = TextoGlobal("shop_itemname_obj");
            if (descTienda == null) descTienda = TextoGlobal("shop_description_obj");
            if (nombreTienda == null || descTienda == null || !nombreTienda.gameObject.activeInHierarchy) return;
            string d;
            if (textosJuego.TryGetValue(nombreTienda.text.Trim(), out d) && descTienda.text != d) descTienda.text = d;
        }

        /// Rueda del ratón en la lista del comerciante: se lee directamente (no depende de cómo el juego reparta el
        /// evento entre los artículos). Cada muesca mueve unos 2 artículos; con el cursor sobre la tienda abierta.
        float ajusteHasta;

        /// Que la lista llegue hasta el último artículo (los del mod alargan la lista y la altura no siempre se ajusta sola).
        void AjustarContenido()
        {
            if (Time.realtimeSinceStartup < ajusteHasta) return;
            ajusteHasta = Time.realtimeSinceStartup + 1f;
            var c = desliz.content; if (c == null) return;
            var esq = new Vector3[4]; c.GetWorldCorners(esq);
            float fondoLista = esq[0].y, escala = Mathf.Max(0.0001f, c.lossyScale.y), masBajo = float.MaxValue;
            foreach (Transform t in c)
            {
                var r = t as RectTransform; if (r == null || !t.gameObject.activeSelf) continue;
                r.GetWorldCorners(esq);
                if (esq[0].y < masBajo) masBajo = esq[0].y;
            }
            if (masBajo < fondoLista - 0.5f)
            {
                float falta = (fondoLista - masBajo) / escala + 8f;
                c.sizeDelta = new Vector2(c.sizeDelta.x, c.sizeDelta.y + falta);
                plugin.Log("Tienda: la lista se alargó " + falta.ToString("0") + " px para llegar al último artículo");
            }
        }

        public string InfoLista()
        {
            if (desliz == null) return "sin lista";
            var c = desliz.content; var v = desliz.viewport != null ? desliz.viewport : (RectTransform)desliz.transform;
            return "contenido " + c.rect.size + " pivote " + c.pivot + " pos " + c.anchoredPosition + " | vista " + v.rect.size + " | posición " + desliz.verticalNormalizedPosition.ToString("0.00") + " | sensibilidad " + desliz.scrollSensitivity + " | hijos " + c.childCount
                + " | layout " + (c.GetComponent<LayoutGroup>() != null ? c.GetComponent<LayoutGroup>().GetType().Name : "ninguno") + (c.GetComponent<ContentSizeFitter>() != null ? " + ContentSizeFitter" : "");
        }

        /// Rueda del ratón desde donde llegue: Unity, el eje clásico, Rewired (el juego lee el ratón con Rewired) o lo que
        /// el juego ya leyó (SelectorArmas.UltimaRueda). Registra qué fuente respondió.
        float LeerRueda()
        {
            float w = Input.mouseScrollDelta.y; string f = "Unity";
            if (w == 0f) { w = Input.GetAxis("Mouse ScrollWheel") * 10f; f = "eje"; }
            if (w == 0f) { try { var m = Rewired.ReInput.controllers.Mouse; if (m != null) { w = m.GetAxis(2); f = "Rewired"; } } catch { } }
            if (w == 0f) { w = SelectorArmas.UltimaRueda; f = "juego"; }
            SelectorArmas.UltimaRueda = 0f;
            if (w != 0f) plugin.Log("Rueda: " + w.ToString("0.##") + " (fuente " + f + ")");
            return w;
        }

        void RuedaTienda()
        {
            if (desliz == null || !Guardado.ModoCoop || !plugin.tienda.Abierta) return;
            AjustarContenido();
            float w = LeerRueda();
            if (w == 0f) return;
            Desplazar(w);
        }

        /// Mueve la lista: w > 0 sube (rueda hacia delante), w < 0 baja. Unas 2 filas por muesca.
        public bool Desplazar(float w)
        {
            if (desliz == null) return false;
            var contenido = desliz.content; if (contenido == null) return false;
            var vista = desliz.viewport != null ? desliz.viewport : (RectTransform)desliz.transform;
            float rango = contenido.rect.height - vista.rect.height;
            if (rango < 1f) return false;
            desliz.verticalNormalizedPosition = Mathf.Clamp01(desliz.verticalNormalizedPosition + Mathf.Clamp(w, -3f, 3f) * (76f / rango));
            return true;
        }

        readonly List<Image> barras = new List<Image>();
        static readonly Color RojoFila = new Color(0.75f, 0.04f, 0.04f, 1f);

        /// Fila del mod bajo el ratón en rojo, las demás con la raya oscura. Antes se hacía con
        /// PointerEnter/PointerExit, pero la salida se pierde si la tienda se cierra (compra de un
        /// libro) o la lista se mueve bajo el ratón, y quedaban muchas filas rojas (2026-11-17).
        /// Prueba (órdenes "raton", "tiendaresaltar"): posición del ratón simulada para el resaltado de las filas del mod.
        /// null = el ratón real (siempre en el juego normal).
        public static Vector2? RatonPrueba;

        void CamaraYVista(out Camera cam, out RectTransform vista)
        {
            cam = null; vista = null;
            if (barras.Count == 0) return;
            var canvas = barras[0].canvas != null ? barras[0].canvas.rootCanvas : null;
            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay) cam = canvas.worldCamera;
            if (desliz != null) vista = desliz.viewport != null ? desliz.viewport : (RectTransform)desliz.transform;
        }

        void PintarBarras()
        {
            barras.RemoveAll(b => b == null);
            if (barras.Count == 0 || barraNormal == null) return;
            bool abierta = plugin.tienda.Abierta;
            Camera cam = null; RectTransform vista = null;
            if (abierta) CamaraYVista(out cam, out vista);
            Vector2 raton = RatonPrueba ?? (Vector2)Input.mousePosition;
            bool enLista = abierta && (vista == null || RectTransformUtility.RectangleContainsScreenPoint(vista, raton, cam));
            foreach (var b in barras)
            {
                bool encima = enLista && b.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint((RectTransform)b.transform.parent, raton, cam);
                var c = encima ? RojoFila : barraNormal.Value;
                if (b.color != c) b.color = c;
            }
        }

        /// Prueba: filas del mod con su centro en pantalla, si se ven en la lista y si están rojas.
        public string FilasMod()
        {
            barras.RemoveAll(b => b == null);
            var sb = new System.Text.StringBuilder("tienda abierta=" + plugin.tienda.Abierta + " filas del mod=" + barras.Count + " raya normal=" + (barraNormal != null));
            Camera cam; RectTransform vista; CamaraYVista(out cam, out vista);
            foreach (var b in barras)
            {
                var fila = (RectTransform)b.transform.parent;
                Vector2 sp = RectTransformUtility.WorldToScreenPoint(cam, fila.TransformPoint(fila.rect.center));
                bool ve = b.gameObject.activeInHierarchy && (vista == null || RectTransformUtility.RectangleContainsScreenPoint(vista, sp, cam));
                sb.Append("\n    ").Append(fila.name).Append(" pantalla=").Append(sp.ToString("F0")).Append(ve ? " visible" : " fuera").Append(b.color == RojoFila ? " ROJA" : "");
            }
            return sb.ToString();
        }

        /// Prueba: pasa el ratón simulado por el centro de cada fila del mod visible y cuenta cuántas se ponen rojas
        /// (y que no quede otra roja a la vez). Deja el ratón simulado sobre la fila n.º "dejarEn" (−1 = ratón real).
        public string ProbarResaltado(int dejarEn)
        {
            barras.RemoveAll(b => b == null);
            if (!plugin.tienda.Abierta) return "la tienda NO está abierta para el mod (tienda.Abierta=false): ninguna fila se pondría roja";
            if (barras.Count == 0 || barraNormal == null) return "no hay filas del mod (" + barras.Count + ") o falta la raya normal";
            Camera cam; RectTransform vista; CamaraYVista(out cam, out vista);
            int visibles = 0, rojas = 0, dobles = 0; var fallan = new List<string>(); Vector2? dejar = null;
            foreach (var b in barras)
            {
                if (!b.gameObject.activeInHierarchy) continue;
                var fila = (RectTransform)b.transform.parent;
                Vector2 sp = RectTransformUtility.WorldToScreenPoint(cam, fila.TransformPoint(fila.rect.center));
                if (vista != null && !RectTransformUtility.RectangleContainsScreenPoint(vista, sp, cam)) continue;
                if (visibles == dejarEn) dejar = sp;
                visibles++;
                RatonPrueba = sp; PintarBarras();
                if (b.color == RojoFila) rojas++; else fallan.Add(fila.name);
                foreach (var o in barras) if (o != b && o.color == RojoFila) { dobles++; break; }
            }
            RatonPrueba = dejar; PintarBarras();
            return "filas del mod visibles " + visibles + ": se pusieron rojas " + rojas + (fallan.Count > 0 ? " | NO se pusieron rojas: " + string.Join(", ", fallan.ToArray()) : "")
                + (dobles > 0 ? " | con otra fila roja a la vez: " + dobles : "") + (dejar.HasValue ? " | ratón simulado dejado en la fila " + dejarEn + " " + dejar.Value.ToString("F0") : "");
        }

        public void Actualizar()
        {
            PintarBarras();
            ResaltarEquipados();
            RuedaTienda();
            RuedaInventario();
            if (InventarioAbierto && Time.realtimeSinceStartup > proxEquipado) { proxEquipado = Time.realtimeSinceStartup + 0.3f; ArmasEquipadas(); }
            TextosJuego();
            if (plugin.Escena != escenaVista)
            {
                escenaVista = plugin.Escena; tiendaHecha = inventarioHecho = false;
                preciosHechos = false; habilidades.Clear(); articulos.Clear(); balasJuego.Clear(); barras.Clear(); equipadosMod.Clear(); vistaEq = contenidoEq = null; contenidoTienda = null;
            }
            if (Guardado.ModoCoop) OcultarBalasJuego();
            if (!Guardado.ModoCoop || plugin.Jugador == null || Time.realtimeSinceStartup < proximo) return;
            proximo = Time.realtimeSinceStartup + 0.5f;
            if (iconoBase == null) iconoBase = Resources.Load<Texture2D>("item_Skillbook");
            if (iconoGanzua == null)
                foreach (var n in new[] { "item_Lockpicks", "item_lockpicks", "item_Lockpick", "item_lockpick", "Lockpicks", "lockpicks", "item_Lockpicks_pack", "skill_lockpickmaster" })
                {
                    iconoGanzua = Resources.Load<Texture2D>(n);
                    if (iconoGanzua != null) { plugin.Log("Tienda: icono de ganzúa = " + n); break; }
                }
            try { if (!preciosHechos) AjustarPreciosJuego(); } catch (System.Exception e) { preciosHechos = true; plugin.Log("Tienda: error al cambiar precios " + e.Message); }
            try { if (!tiendaHecha) ArmarTienda(); } catch (System.Exception e) { tiendaHecha = true; plugin.Log("Tienda: error al armar " + e); }
            try { if (!inventarioHecho) ArmarInventario(); } catch (System.Exception e) { inventarioHecho = true; plugin.Log("Inventario: error al armar " + e); }
            // Colores de la fila de habilidades según lo comprado.
            foreach (var kv in habilidades)
            {
                if (kv.Value == null) continue;
                // Como las habilidades del juego: apagada (oscura) hasta tenerla, clara después.
                kv.Value.color = new Color(1f, 1f, 1f, LibrosTienda.Tiene(kv.Key.Var) ? 1f : 0.102f);
            }
            // Un libro comprado (por mí o por el compañero) sale de la tienda, como los del juego.
            foreach (var kv in articulos) if (kv.Value != null && kv.Value.activeSelf && LibrosTienda.Tiene(kv.Key.Var)) kv.Value.SetActive(false);
            // La bola de cristal: la lógica de la tienda vuelve a encender todas sus filas al refrescarse (se veía de nuevo
            // para comprar al segundo de comprarla; 2026-11-17): se oculta igual que los libros.
            if (contenidoTienda != null && Aliados.TieneBola) { var fb = contenidoTienda.Find("coop_bola"); if (fb != null && fb.gameObject.activeSelf) fb.gameObject.SetActive(false); }
        }

        static GameObject Buscar(string nombre, string padre)
        {
            foreach (var g in Resources.FindObjectsOfTypeAll<GameObject>())
                if (g.name == nombre && g.scene.IsValid() && (padre == null || (g.transform.parent != null && g.transform.parent.name == padre))) return g;
            return null;
        }

        static void QuitarLogica(GameObject g)
        {
            foreach (var f in g.GetComponentsInChildren<PlayMakerFSM>(true)) Object.DestroyImmediate(f);
            foreach (var mb in g.GetComponentsInChildren<MonoBehaviour>(true))
                if (mb != null && mb.GetType().Name.StartsWith("PlayMaker")) Object.DestroyImmediate(mb);
            foreach (var et in g.GetComponentsInChildren<EventTrigger>(true)) Object.DestroyImmediate(et);
            foreach (var b in g.GetComponentsInChildren<Button>(true)) b.onClick = new Button.ButtonClickedEvent();
        }

        static Text TextoGlobal(string var)
        {
            var g = FsmVariables.GlobalVariables.FindFsmGameObject(var);
            return g != null && g.Value != null ? g.Value.GetComponent<Text>() : null;
        }

        static void AlEntrar(GameObject g, System.Action accion, System.Action salir)
        {
            var et = g.GetComponent<EventTrigger>() ?? g.AddComponent<EventTrigger>();
            var e = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            e.callback.AddListener(_ => accion());
            et.triggers.Add(e);
            if (salir != null)
            {
                var s = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
                s.callback.AddListener(_ => salir());
                et.triggers.Add(s);
            }
        }

        // ---------------- Tienda ----------------

        // ---------------- Precios de artículos del juego (pedido 2026-11-19) ----------------
        /// Artículo del juego → precio nuevo. El juego fija el precio con un StringSwitch (item_name → estado) y en ese estado
        /// un SetIntValue a price_int; con ese mismo price_int muestra el precio y cobra. Se cambia ese valor.
        public static readonly Dictionary<string, int> PreciosJuego = new Dictionary<string, int> { { "skill_ironlegs", 120 } };   // Piernas de hierro: 20 → 120
        bool preciosHechos;

        void AjustarPreciosJuego()
        {
            // El estado "prices" del botón (FSM "description") manda un evento con el nombre del artículo (SendEventByName
            // $item_name) y sus transiciones llevan, por ese nombre, al estado que fija price_int. Se sigue esa transición.
            int cambiados = 0;
            foreach (var f in Resources.FindObjectsOfTypeAll<PlayMakerFSM>())   // también la tienda oculta (Canvas_shop apagado)
            {
                if (f == null || f.Fsm == null || f.FsmStates == null || !f.gameObject.scene.IsValid()) continue;
                foreach (var st in f.FsmStates)
                {
                    if (st.Transitions == null) continue;
                    foreach (var tr in st.Transitions)
                    {
                        int nuevo;
                        if (tr == null || tr.EventName == null || !PreciosJuego.TryGetValue(tr.EventName, out nuevo)) continue;
                        HutongGames.PlayMaker.FsmState est = null;
                        foreach (var s2 in f.FsmStates) if (s2.Name == tr.ToState) { est = s2; break; }
                        HutongGames.PlayMaker.FsmStateAction[] acts = null;
                        try { acts = est != null ? est.Actions : null; } catch { }
                        if (acts == null) continue;
                        foreach (var a2 in acts)
                        {
                            var si = a2 as HutongGames.PlayMaker.Actions.SetIntValue;
                            if (si == null || si.intVariable == null || si.intVariable.Name != "price_int" || si.intValue == null || si.intValue.UseVariable) continue;
                            if (si.intValue.Value != nuevo) { si.intValue.Value = nuevo; cambiados++; }
                            else cambiados++;
                        }
                    }
                }
            }
            if (cambiados > 0) { preciosHechos = true; plugin.Log("Tienda: precios del juego cambiados en " + cambiados + " botones (Piernas de hierro = 120)"); }
        }

        void ArmarTienda()
        {
            GameObject contenido = null;
            foreach (var g in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (g.name != "ScrollContent" || !g.scene.IsValid()) continue;
                for (var x = g.transform; x != null; x = x.parent) if (x.name == "Canvas_shop") { contenido = g; break; }
                if (contenido != null) break;
            }
            if (contenido == null) return;
            contenidoTienda = contenido.transform;
            // Rueda del ratón: la lista del juego casi no se mueve con ella (sensibilidad baja).
            ScrollRect desliz = null;   // (GetComponentInParent no ve objetos ocultos: la tienda está cerrada)
            for (var x = contenido.transform; x != null && desliz == null; x = x.parent) desliz = x.GetComponent<ScrollRect>();
            if (desliz != null)
            {
                plugin.Log("Tienda: desplazamiento de la lista " + desliz.scrollSensitivity + " → 40");
                desliz.vertical = true; desliz.scrollSensitivity = 0f;   // la rueda la maneja RuedaTienda (si no, se movería doble)
                desliz.movementType = ScrollRect.MovementType.Clamped;
            }
            this.desliz = desliz;
            // Modelo: un libro del juego (el último de la lista tiene el icono del libro).
            GameObject modelo = null;
            foreach (Transform t in contenido.transform)
            {
                var tx = t.Find("Text") != null ? t.Find("Text").GetComponent<Text>() : null;
                if (tx != null && tx.text.Contains("(libro)")) modelo = t.gameObject;
            }
            if (modelo == null) foreach (Transform t in contenido.transform) { modelo = t.gameObject; break; }
            if (modelo == null) return;
            tiendaHecha = true;
            if (contenido.transform.Find("coop_ganzua") != null)
            {
                foreach (var l in LibrosTienda.Lista)
                {
                    var t = contenido.transform.Find("coop_libro_" + l.Var);
                    if (t != null) articulos[l] = t.gameObject;
                }
                // La tienda del juego (Canvas_RFPS) sobrevive al cambio de escena con nuestros artículos dentro, pero barras se
                // vacía al cambiar de escena: sin volver a registrar las rayas, tras ir al bosque y volver ninguna fila del mod
                // se ponía roja bajo el ratón (comprobado 2026-11-19: 29 filas antes del viaje, 0 después).
                barras.Clear();
                foreach (Transform f in contenido.transform)
                {
                    if (!f.name.StartsWith("coop_")) continue;
                    var im = f.Find("Image"); var img = im != null ? im.GetComponent<Image>() : null;
                    if (img != null) barras.Add(img);
                }
                if (barraNormal == null) barraNormal = RayaDelJuego(contenido.transform) ?? new Color(0f, 0f, 0f, 0.45f);
                plugin.Log("Tienda: ya estaba armada (tras un viaje); " + barras.Count + " filas del mod vuelven a resaltarse");
                return;
            }

            foreach (var l in LibrosTienda.Lista)
            {
                var lib = l;
                var go = NuevoArticulo(modelo, contenido.transform, "coop_libro_" + l.Var, l.Nombre + (SinLibro(l.Var) ? "" : " (libro)"), l.Precio.ToString(), Color.white);
                AlEntrar(go, () => Describir(lib.Nombre, lib.Efecto, Color.white, Icono("item", lib.Var)), null);
                go.GetComponent<Button>().onClick.AddListener(() => Mensaje(LibrosTienda.Comprar(plugin, lib)));
                go.transform.SetSiblingIndex(articulos.Count);   // al principio de la lista
                if (LibrosTienda.Tiene(l.Var)) go.SetActive(false);
                articulos[l] = go;
            }
            var gz = NuevoArticulo(modelo, contenido.transform, "coop_ganzua", "Ganzúa", Tienda.PrecioGanzua.ToString(), new Color(0.8f, 0.8f, 0.8f));
            gz.transform.SetSiblingIndex(articulos.Count);
            AlEntrar(gz, () => Describir("Ganzúa", "Abre cerraduras y cofres. Cada intento tiene un 35 % de que se rompa.", iconoGanzua != null ? Color.white : new Color(0.8f, 0.8f, 0.8f), iconoGanzua), null);
            gz.GetComponent<Button>().onClick.AddListener(() => Mensaje(plugin.tienda.ComprarGanzuas(1)));
            var fl = NuevoArticulo(modelo, contenido.transform, "coop_flechas", "Flechas (2)", Tienda.PrecioFlechas.ToString(), Color.white);
            fl.transform.SetSiblingIndex(articulos.Count + 1);
            AlEntrar(fl, () => Describir("Flechas (2)", "Dos flechas para la ballesta. Llevas como máximo 20, o 60 con la Bolsa ampliada.", Color.white, Icono("item", "coop_flechas")), null);
            fl.GetComponent<Button>().onClick.AddListener(() => Mensaje(plugin.tienda.ComprarFlechas()));
            // Balas de plata por arma: se ocultan las dos del juego (iban a la última arma de fuego en la mano) y se
            // ponen seis propias en su lugar: paquete y bala para la pistola, el mosquete y la escopeta, al mismo precio.
            int idxBalas = -1, precioPaquete = 15, precioBala = 3;
            foreach (Transform t in contenido.transform)
            {
                var tt = t.Find("Text") != null ? t.Find("Text").GetComponent<Text>() : null;
                if (tt == null || t.name.StartsWith("coop_")) continue;
                bool paq = tt.text.Trim() == "Paquete de balas de plata", bal = tt.text.Trim() == "Bala de plata";
                if (!paq && !bal) continue;
                var pr = t.Find("Price") != null ? t.Find("Price").GetComponent<Text>() : null; int v;
                if (pr != null && int.TryParse(pr.text.Trim(), out v)) { if (paq) precioPaquete = v; else precioBala = v; }
                if (idxBalas < 0 || t.GetSiblingIndex() < idxBalas) idxBalas = t.GetSiblingIndex();
                t.gameObject.SetActive(false);
                balasJuego.Add(t.gameObject);
            }
            if (idxBalas < 0) idxBalas = articulos.Count + 3;
            var armas = new[] { new { Nombre = "pistola", Tipo = Municion.Pistola }, new { Nombre = "mosquete", Tipo = Municion.Mosquete }, new { Nombre = "escopeta", Tipo = Municion.EscopetaR } };
            int pos = idxBalas;
            foreach (var arma in armas)
            {
                var a = arma;
                var paquete = NuevoArticulo(modelo, contenido.transform, "coop_paquete_" + a.Nombre, "Paquete de balas de plata (" + a.Nombre + ")", precioPaquete.ToString(), Color.white);
                paquete.transform.SetSiblingIndex(pos++);
                AlEntrar(paquete, () => Describir("Paquete de balas de plata (" + a.Nombre + ")", "5 balas de plata para la " + a.Nombre + ". Cada arma de fuego tiene su propia reserva.", Color.white, Icono("item", "coop_paquete_balas")), null);
                paquete.GetComponent<Button>().onClick.AddListener(() => Mensaje(plugin.tienda.ComprarBalas(a.Tipo, 5, precioPaquete, a.Nombre)));
                var una = NuevoArticulo(modelo, contenido.transform, "coop_bala_" + a.Nombre, "Bala de plata (" + a.Nombre + ")", precioBala.ToString(), Color.white);
                una.transform.SetSiblingIndex(pos++);
                AlEntrar(una, () => Describir("Bala de plata (" + a.Nombre + ")", "Una bala de plata para la " + a.Nombre + ". Cada arma de fuego tiene su propia reserva.", Color.white, Icono("item", "coop_bala_plata")), null);
                una.GetComponent<Button>().onClick.AddListener(() => Mensaje(plugin.tienda.ComprarBalas(a.Tipo, 1, precioBala, a.Nombre)));
            }
            var bo = NuevoArticulo(modelo, contenido.transform, "coop_bola", "Bola de cristal oscura", Aliados.Precio.ToString(), Color.white);
            bo.transform.SetSiblingIndex(articulos.Count + 2);
            AlEntrar(bo, () => Describir("Bola de cristal oscura", "Arma (tecla 5). Se traga a los enemigos que matas; con clic los invocas como aliados que pelean a tu lado (máximo 2, duran 3 minutos).", Color.white, Icono("item", "coop_bola")), null);
            bo.GetComponent<Button>().onClick.AddListener(() => { Mensaje(plugin.aliados.Comprar()); if (Aliados.TieneBola) bo.SetActive(false); });
            if (Aliados.TieneBola) bo.SetActive(false);
            // Cada artículo (del juego y nuestros) tiene un EventTrigger que se queda con la rueda del ratón
            // y no la deja llegar a la lista: se agrega quien la reenvía.
            int n = 0;
            if (desliz != null)
                foreach (Transform t in contenido.transform)
                    if (t.GetComponent<ReenviarRueda>() == null) { t.gameObject.AddComponent<ReenviarRueda>().Lista = desliz; n++; }
            plugin.Log("Tienda: " + (LibrosTienda.Lista.Length + 1) + " artículos del cooperativo agregados a la lista del comerciante; rueda del ratón en " + n + " artículos");
        }

        GameObject NuevoArticulo(GameObject modelo, Transform padre, string nombre, string texto, string precio, Color tinte)
        {
            var go = (GameObject)Object.Instantiate(modelo, padre, false);
            go.name = nombre;
            QuitarLogica(go);
            var t = go.transform.Find("Text"); if (t != null) t.GetComponent<Text>().text = texto;
            var p = go.transform.Find("Price"); if (p != null) { var pt = p.GetComponent<Text>(); pt.text = precio; }
            // "Image" es la barra de fondo de la fila: transparente, y roja con el ratón encima (como las del juego).
            var im = go.transform.Find("Image");
            var barra = im != null ? im.GetComponent<Image>() : null;
            if (barra != null)
            {
                // Raya oscura como las filas del juego (pedido 2026-11-17): se copia el color de una fila del juego que la tenga.
                var normal = barraNormal ?? (barraNormal = RayaDelJuego(padre) ?? new Color(0f, 0f, 0f, 0.45f));
                barra.color = normal.Value;
                barras.Add(barra);   // el rojo bajo el ratón se pinta cada cuadro en PintarBarras (ver allí por qué)
            }
            go.SetActive(true);
            return go;
        }

        /// Color de la barra "Image" de una fila del juego con raya visible (no del mod ni la roja de selección).
        Color? RayaDelJuego(Transform lista)
        {
            foreach (Transform f in lista)
            {
                if (f.name.StartsWith("coop_")) continue;
                var im = f.Find("Image"); var img = im != null ? im.GetComponent<Image>() : null;
                if (img == null || img.color.a < 0.05f || img.color.r > 0.3f) continue;
                plugin.Log("Tienda: raya de las filas copiada de '" + f.name + "' " + img.color);
                return img.color;
            }
            return null;
        }

        void Describir(string nombre, string desc, Color tinte, Texture2D icono = null)
        {
            if (icono == null) icono = iconoBase;
            var n = TextoGlobal("shop_itemname_obj"); if (n != null) n.text = nombre;
            var d = TextoGlobal("shop_description_obj"); if (d != null) d.text = desc;
            var ig = FsmVariables.GlobalVariables.FindFsmGameObject("shop_image_obj");
            if (ig != null && ig.Value != null)
            {
                var ri = ig.Value.GetComponent<RawImage>();
                if (ri != null && icono != null) { ri.texture = icono; ri.color = tinte; }
                var im = ig.Value.GetComponent<Image>();
                if (im != null) im.color = tinte;
            }
        }

        static readonly Dictionary<string, AudioClip> sonidosTienda = new Dictionary<string, AudioClip>();
        static AudioSource fuenteTienda;

        /// Sonidos del propio juego (los de sus filas de la tienda): "shop_buy2" al comprar y
        /// "fail_deny" si no se pudo. Se cargan de la memoria del juego, no se copian al mod.
        static void SonarTienda(string nombre)
        {
            AudioClip c;
            if (!sonidosTienda.TryGetValue(nombre, out c) || c == null)
            {
                c = null;
                foreach (var a in Resources.FindObjectsOfTypeAll<AudioClip>()) if (a.name == nombre) { c = a; break; }
                sonidosTienda[nombre] = c;
                Ballesta.Registro("Tienda: sonido " + nombre + (c != null ? " encontrado" : " NO encontrado"));
            }
            if (c == null) return;
            if (fuenteTienda == null)
            {
                var go = new GameObject("coop_sonido_tienda");
                Object.DontDestroyOnLoad(go);
                fuenteTienda = go.AddComponent<AudioSource>();
                fuenteTienda.spatialBlend = 0f; fuenteTienda.playOnAwake = false; fuenteTienda.ignoreListenerPause = true;
            }
            fuenteTienda.PlayOneShot(c, Opciones.FactorVolumen);
        }

        void Mensaje(string m)
        {
            SonarTienda(m.StartsWith("Compraste") ? "shop_buy2" : "fail_deny");
            var t = TextoGlobal("shop_message_obj");
            if (t != null) { t.text = m; t.gameObject.SetActive(true); }
            else Viaje.Avisar(m);
        }

        // ---------------- Inventario: fila de habilidades ----------------

        void ArmarInventario()
        {
            var inv = Buscar("Canvas_inventory", null);
            if (inv == null) return;
            // El lienzo sobrevive al cambio de escena: si ya están nuestras habilidades, se reutilizan
            // (crearlas otra vez duplicaría la fila y volvería a bajar "Equipado").
            var cont0 = inv.transform.Find("coop_habilidades/contenido") as RectTransform;
            if (cont0 != null && cont0.Find("coop_habilidad_" + LibrosTienda.Lista[0].Var) != null)
            {
                inventarioHecho = true;
                invGo = inv; contenidoHab = cont0; viewportHab = (RectTransform)cont0.parent;
                foreach (var l in LibrosTienda.Lista)
                {
                    var t = cont0.Find("coop_habilidad_" + l.Var);
                    if (t != null) habilidades.Add(new KeyValuePair<LibrosTienda.Libro, RawImage>(l, t.GetComponent<RawImage>()));
                }
                return;
            }
            var botones = new List<RectTransform>();
            foreach (Transform t in inv.transform) if (t.name.StartsWith("skill_") && t.gameObject.activeSelf) botones.Add((RectTransform)t);
            if (botones.Count == 0) return;
            inventarioHecho = true;
            // Cuadrícula del juego: columnas y filas que ya usan sus habilidades (5 por fila).
            var cols = new List<float>(); var filas = new List<float>();
            foreach (var b in botones) { Agregar(cols, b.anchoredPosition.x); Agregar(filas, b.anchoredPosition.y); }
            cols.Sort(); filas.Sort((a, b) => b.CompareTo(a));   // de arriba abajo
            float pasoX = cols.Count > 1 ? (cols[cols.Count - 1] - cols[0]) / (cols.Count - 1) : 135f;
            float pasoY = filas.Count > 1 ? (filas[0] - filas[filas.Count - 1]) / (filas.Count - 1) : 135f;
            const int Columnas = 5;
            var ocupado = new HashSet<int>();
            foreach (var b in botones)
            {
                int c = Mathf.RoundToInt((b.anchoredPosition.x - cols[0]) / pasoX), f = Mathf.RoundToInt((filas[0] - b.anchoredPosition.y) / pasoY);
                ocupado.Add(f * Columnas + c);
            }
            int ultimo = -1; foreach (int o in ocupado) ultimo = Mathf.Max(ultimo, o);
            var modelo = botones[0].gameObject;
            var desc = TextoGlobal("equipped_description");
            int hueco = ultimo + 1, filaMax = (filas.Count - 1);
            // Todas las habilidades del mismo tamaño que las del juego, 5 por fila, y la lista se desplaza con la rueda.
            foreach (var l in LibrosTienda.Lista)
            {
                var lib = l;
                if (SinLibro(l.Var)) continue;   // las mejoras sin «(libro)» van en «Equipado» (pedido 2026-11-17)
                while (ocupado.Contains(hueco)) hueco++;
                int f = hueco / Columnas, c = hueco % Columnas;
                filaMax = Mathf.Max(filaMax, f);
                var go = (GameObject)Object.Instantiate(modelo, inv.transform, false);
                go.name = "coop_habilidad_" + l.Var;
                QuitarLogica(go);
                var rt = (RectTransform)go.transform;
                rt.anchoredPosition = new Vector2(cols[0] + pasoX * c, filas[0] - pasoY * f);
                var ri = go.GetComponent<RawImage>();
                var tx = Icono("skill", l.Var);
                if (ri != null && tx != null) ri.texture = tx;
                var borde = go.transform.Find("skill_outline");
                Graphic bg = borde != null ? borde.GetComponent<Graphic>() : null;
                if (bg != null) bg.color = new Color(1f, 1f, 1f, 0f);
                AlEntrar(go, () =>
                {
                    if (desc != null) desc.text = lib.Nombre + " - " + lib.Efecto;
                    if (bg != null) bg.color = Color.white;
                }, () => { if (bg != null) bg.color = new Color(1f, 1f, 1f, 0f); });
                go.SetActive(true);
                habilidades.Add(new KeyValuePair<LibrosTienda.Libro, RawImage>(l, ri));
                hueco++;
            }
            // "Equipado" (armas) baja las filas nuevas que se agregaron.
            int nuevas = 2;   // la zona visible de habilidades son 5 filas (3 del juego + 2); el resto se ve con la rueda
            // El panel negro de las habilidades del juego solo cubre sus filas: se alarga hacia abajo.
            if (nuevas > 0)
            {
                RectTransform panel = null; float mejor = float.MaxValue;
                var primero = botones[0].position;
                foreach (Transform t in inv.transform)
                {
                    if (!t.name.StartsWith("Black") || !t.gameObject.activeSelf || t.GetComponent<Image>() == null) continue;
                    var r = (RectTransform)t;
                    var esq = new Vector3[4]; r.GetWorldCorners(esq);
                    // el que contiene la primera habilidad (en coordenadas de pantalla) y es más chico
                    if (primero.x < esq[0].x || primero.x > esq[2].x || primero.y < esq[0].y || primero.y > esq[2].y) continue;
                    float area = (esq[2].x - esq[0].x) * (esq[2].y - esq[0].y);
                    if (area < mejor) { mejor = area; panel = r; }
                }
                if (panel != null)
                {
                    float d = pasoY * nuevas;
                    panel.sizeDelta += new Vector2(0f, d);
                    panel.anchoredPosition -= new Vector2(0f, d * (1f - panel.pivot.y));
                    plugin.Log("Inventario: panel negro " + panel.name + " alargado " + Mathf.RoundToInt(d));
                }
            }
            var equipado = inv.transform.Find("Equipped_parrent") as RectTransform;
            if (equipado != null && nuevas > 0) equipado.anchoredPosition -= new Vector2(0f, pasoY * nuevas - SubirEquipado);
            // +70 px de pantalla más arriba (pedido 2026-11-17, después): la ventana de Habilidades pasa a 4 filas
            var lienzoInv = inv.GetComponentInParent<Canvas>();
            float escalaInv = lienzoInv != null && lienzoInv.rootCanvas.scaleFactor > 0f ? lienzoInv.rootCanvas.scaleFactor : 1f;
            if (equipado != null) equipado.anchoredPosition += new Vector2(0f, SubirEquipadoPantalla / escalaInv);   // 30 más arriba: con todo comprado la 2.ª fila de equipado se cortaba (2026-11-17)
            ArmarDesplazamiento(inv, botones, cols, filas, pasoX, pasoY, filaMax + 1);
            plugin.Log("Inventario: " + LibrosTienda.Lista.Length + " habilidades del cooperativo en la cuadrícula (" + cols.Count + "×" + filas.Count + " del juego, +" + nuevas + " filas)");
        }

        // ---------------- Habilidades con desplazamiento ----------------

        GameObject invGo; RectTransform contenidoHab, viewportHab; float baseContenidoY, maxDesplazo, desplazo, altoFila;
        const float SubirEquipadoPantalla = 70f;   // píxeles de pantalla
        const float FilasVentanaHab = 4f;          // filas de Habilidades a la vista (la 5.ª con la rueda)
        const float SubirEquipado = 48f;   // 30 + 18: deja sitio a 2 filas dentro del marco
        const float AltoEquipado = 84f;     // alto útil dentro del marco (la vista del juego mide 214 y se sale por abajo)
        public static bool InventarioAbierto; float proxEquipado;

        /// Añade a «Equipado» los iconos de escopeta, ballesta y bola de cristal oscura cuando se tienen
        /// (copias de los botones de arma del juego, que se rehacen cada vez que se abre el inventario).
        void ArmasEquipadas()
        {
            var cont = invGo.transform.Find("Equipped_parrent/ScrollView_equipped/ScrollContent");
            if (cont == null) return;
            Transform molde = null; int n = 0;
            foreach (Transform t in cont) if (t.name == "Button_equipped(Clone)") { n++; if (molde == null) molde = t; }
            if (molde == null) return;
            // [nombre del objeto, icono (equip_<icono>.png), lo tiene, libro o null]
            var extras = new List<object[]> { new object[] { "escopeta", "coop_escopeta", Escopeta.Tiene, null }, new object[] { "ballesta", "coop_ballesta", Ballesta.Tiene, null }, new object[] { "bola", "coop_bola", Aliados.TieneBola, null } };
            foreach (var l in LibrosTienda.Lista) if (SinLibro(l.Var)) extras.Add(new object[] { l.Var, l.Var, LibrosTienda.Tiene(l.Var), l });
            var desc = TextoGlobal("equipped_description");
            int pos = n;
            foreach (var e in extras)
            {
                string nom = "coop_equipado_" + e[0];
                var ya = cont.Find(nom);
                bool tiene = (bool)e[2];
                if (!tiene) { if (ya != null) UnityEngine.Object.Destroy(ya.gameObject); continue; }
                if (ya != null) { var riYa = ya.GetComponent<RawImage>(); if (riYa != null) equipadosMod.Add(riYa); ya.SetAsLastSibling(); ((RectTransform)ya).anchoredPosition = ((RectTransform)molde).anchoredPosition + new Vector2(68f * pos, 0f); pos++; continue; }
                var c = (GameObject)UnityEngine.Object.Instantiate(molde.gameObject, cont, false); c.name = nom; c.SetActive(true);
                foreach (var f in c.GetComponents<PlayMakerFSM>()) f.enabled = false;
                var ri = c.GetComponent<RawImage>(); var tex = Icono("equip", (string)e[1]);
                if (ri != null && tex != null) ri.texture = tex;
                if (ri != null) equipadosMod.Add(ri);
                var lib = e[3] as LibrosTienda.Libro;
                if (lib != null) AlEntrar(c, () => { if (desc != null) desc.text = lib.Nombre + " - " + lib.Efecto; }, null);
                var rt = (RectTransform)c.transform; rt.anchoredPosition = ((RectTransform)molde).anchoredPosition + new Vector2(68f * pos, 0f);
                pos++;
            }
            UnaFilaEquipado(cont);
        }

        Vector2? celdaEquipado, espacioEquipado;
        readonly HashSet<RawImage> equipadosMod = new HashSet<RawImage>();
        const int FilasVistaEq = 2;
        RectTransform vistaEq, contenidoEq; float baseEqY, desplazoEq, maxDesplazoEq, altoVistaEq;

        /// Una vez: la vista de Equipado (ScrollView_equipped) pasa a medir 2 filas con el borde de arriba
        /// donde estaba, recorta lo que sobra (RectMask2D) y su ScrollRect se apaga (la rueda la lleva el mod).
        void PrepararVistaEquipado(Transform cont, RectTransform vista, float lado, float esp)
        {
            if (vista == null || vistaEq == vista) return;
            vistaEq = vista; contenidoEq = (RectTransform)cont;
            var sr = vista.GetComponent<ScrollRect>(); if (sr != null) sr.enabled = false;
            foreach (var sb in vista.GetComponentsInChildren<Scrollbar>(true)) sb.gameObject.SetActive(false);
            float alto0 = vista.rect.height;
            altoVistaEq = FilasVistaEq * (lado + esp) - esp + 4f;
            vista.sizeDelta += new Vector2(0f, altoVistaEq - alto0);
            vista.anchoredPosition += new Vector2(0f, (1f - vista.pivot.y) * (alto0 - altoVistaEq));
            if (vista.GetComponent<RectMask2D>() == null && vista.GetComponent<Mask>() == null) vista.gameObject.AddComponent<RectMask2D>();
            // el ancla del contenido se movió con la vista (su borde de arriba quedó fijo): se compensa
            // centrado en horizontal (venía corrido 4 a la izquierda y la 1.ª columna salía cortada)
            contenidoEq.anchoredPosition = new Vector2(0f, contenidoEq.anchoredPosition.y);
            float anclaY = contenidoEq.anchorMin.y;
            contenidoEq.anchoredPosition -= new Vector2(0f, (1f - anclaY) * (alto0 - altoVistaEq));
            baseEqY = contenidoEq.anchoredPosition.y; desplazoEq = 0f;
            // Fondo negro de Equipado ("Black"): asomaba bajo el marco del inventario (la X de inventario.xcf).
            // Se ajusta para que llegue justo al borde de abajo del marco (ni asoma fuera ni deja hueco claro).
            var cvEq = vista.GetComponentInParent<Canvas>(); float esc = cvEq != null && cvEq.rootCanvas.scaleFactor > 0f ? cvEq.rootCanvas.scaleFactor : 1f;
            var ev = new Vector3[4]; vista.GetWorldCorners(ev);
            var fondo = vista.parent.Find("Black") as RectTransform;
            if (fondo != null)
            {
                // Borde de abajo del marco del inventario: la imagen más grande (fuera de Equipado) que cubre la vista.
                float marcoAbajo = ev[0].y - 16f * esc; float area = 0f; string marco = "(estimado)";
                foreach (var im in invGo.GetComponentsInChildren<Image>(true))
                {
                    if (im.rectTransform == fondo || im.transform.IsChildOf(vista) || !im.gameObject.activeInHierarchy) continue;
                    var e = new Vector3[4]; im.rectTransform.GetWorldCorners(e);
                    if (e[0].x > ev[0].x + 2f || e[2].x < ev[2].x - 2f || e[0].y > ev[0].y || e[1].y < ev[1].y) continue;
                    float a = (e[2].x - e[0].x) * (e[1].y - e[0].y);
                    if (a > area && e[0].y > -5f) { area = a; marcoAbajo = e[0].y; marco = im.name; }
                }
                float objetivo = marcoAbajo + 4f;   // justo dentro del borde del marco (px de pantalla)
                for (int k = 0; k < 2; k++)   // se mide y se corrige (las anclas del fondo pueden ser cualquiera)
                {
                    var ef = new Vector3[4]; fondo.GetWorldCorners(ef);
                    float sube = objetivo - ef[0].y;
                    if (Mathf.Abs(sube) < 0.5f) break;
                    fondo.offsetMin += new Vector2(0f, sube / Mathf.Max(0.0001f, fondo.parent.lossyScale.y));   // mueve solo el borde de abajo (escala real: aquí es 1, no la del lienzo)
                    var efk = new Vector3[4]; fondo.GetWorldCorners(efk);
                    plugin.Log("Equipado: fondo paso " + k + " abajo " + ef[0].y.ToString("0") + " → " + efk[0].y.ToString("0") + " (objetivo " + objetivo.ToString("0") + ", esc " + esc + ", lossy " + fondo.lossyScale.y + ", offsetMin " + fondo.offsetMin + ")");
                }
                var ef2 = new Vector3[4]; fondo.GetWorldCorners(ef2);
                plugin.Log("Equipado: fondo Black hasta y " + ef2[0].y.ToString("0") + " (marco " + marco + " abajo " + marcoAbajo.ToString("0") + ", vista abajo " + ev[0].y.ToString("0") + ")");
            }
            plugin.Log("Equipado: vista " + vista.name + " de " + alto0 + " a " + altoVistaEq + " (ScrollRect " + (sr != null) + "), contenido pivote " + contenidoEq.pivot + " pos " + contenidoEq.anchoredPosition);
            // fondos oscuros de la zona (para encontrar la franja negra que asoma bajo el marco)
            var raiz = vista.parent;
            foreach (var im in raiz.GetComponentsInChildren<Image>(true))
            {
                if (im.transform.IsChildOf(cont)) continue;
                var esq = new Vector3[4]; im.rectTransform.GetWorldCorners(esq);
                plugin.Log("Equipado: imagen " + im.name + " color " + im.color + " de y " + esq[0].y.ToString("0") + " a " + esq[1].y.ToString("0") + " x " + esq[0].x.ToString("0") + "-" + esq[2].x.ToString("0") + " activa " + im.gameObject.activeInHierarchy);
            }
        }

        bool RatonEnEquipado()
        {
            if (vistaEq == null || !vistaEq.gameObject.activeInHierarchy) return false;
            var cv = vistaEq.GetComponentInParent<Canvas>(); Camera cam = cv != null && cv.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? cv.rootCanvas.worldCamera : null;
            return RectTransformUtility.RectangleContainsScreenPoint(vistaEq, Input.mousePosition, cam);
        }

        /// d > 0 baja (se ve lo de más abajo).
        public void DesplazarEq(float d)
        {
            if (contenidoEq == null) return;
            desplazoEq = Mathf.Clamp(desplazoEq + d, 0f, maxDesplazoEq);
            contenidoEq.anchoredPosition = new Vector2(contenidoEq.anchoredPosition.x, baseEqY + desplazoEq);
        }
        public string InfoEq() { return "equipado desplazo " + desplazoEq + "/" + maxDesplazoEq + " vista " + altoVistaEq; }

        /// Como los del juego (FSM "description": GraphicSetColor rojo 1,0,0 al entrar, blanco al salir):
        /// el objeto del mod bajo el ratón se pinta rojo. Se mira cada cuadro (no con PointerExit, que se pierde).
        void ResaltarEquipados()
        {
            equipadosMod.RemoveWhere(r => r == null);
            if (equipadosMod.Count == 0) return;
            Camera cam = null; Vector2 raton = Input.mousePosition;
            foreach (var r in equipadosMod)
            {
                if (cam == null && r.canvas != null && r.canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay) cam = r.canvas.rootCanvas.worldCamera;
                bool encima = InventarioAbierto && RatonEnEquipado() && r.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(r.rectTransform, raton, cam);
                var c = encima ? Color.red : Color.white;
                if (r.color != c) r.color = c;
            }
        }

        /// Todo lo equipado en UNA fila (pedido 2026-11-17): con todo comprado eran 9 iconos y la
        /// 2.ª fila se salía del marco; los iconos se achican lo justo para caber en el ancho.
        void UnaFilaEquipado(Transform cont)
        {
            var grid = cont.GetComponent<GridLayoutGroup>();
            int n = 0; foreach (Transform t in cont) if (t.gameObject.activeSelf) n++;
            if (n == 0) return;
            var vista = cont.parent as RectTransform;
            float ancho = vista != null ? vista.rect.width : ((RectTransform)cont).rect.width;
            if (grid != null)
            {
                if (celdaEquipado == null) { celdaEquipado = grid.cellSize; espacioEquipado = grid.spacing; plugin.Log("Equipado: celda " + grid.cellSize + " espacio " + grid.spacing + " ancho " + ancho + " relleno " + grid.padding.left + "/" + grid.padding.right); }
                // Pedido 2026-11-17 (inventario.xcf): iconos del tamaño original (60), los que quepan por fila,
                // a la vista solo 2 filas (dentro del marco) y el resto con la rueda, sin barra (como Habilidades).
                float esp = espacioEquipado.Value.x - 1f;   // 7 en vez de 8: deja margen a los lados
                float lado = celdaEquipado.Value.x;
                if (grid.spacing.x != esp) grid.spacing = new Vector2(esp, esp);
                int columnas = Mathf.Max(1, Mathf.FloorToInt((ancho - grid.padding.left - grid.padding.right + esp) / (lado + esp)));
                if (grid.cellSize != celdaEquipado.Value) grid.cellSize = celdaEquipado.Value;
                if (grid.constraint != GridLayoutGroup.Constraint.FixedColumnCount || grid.constraintCount != columnas) { grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = columnas; }
                PrepararVistaEquipado(cont, vista, lado, esp);
                int filas = Mathf.CeilToInt(n / (float)columnas);
                maxDesplazoEq = Mathf.Max(0f, filas * (lado + esp) - esp - altoVistaEq);
                if (desplazoEq > maxDesplazoEq) DesplazarEq(0f);
            }
            else
            {
                // sin cuadrícula: se colocan a mano desde el primero
                RectTransform primero = null; int i = 0;
                foreach (Transform t in cont)
                {
                    if (!t.gameObject.activeSelf) continue;
                    var r = (RectTransform)t;
                    if (primero == null) { primero = r; if (celdaEquipado == null) { celdaEquipado = r.sizeDelta; plugin.Log("Equipado: sin cuadrícula, celda " + r.sizeDelta + " ancho " + ancho); } }
                    float paso = Mathf.Min(68f, (ancho - 4f) / n);
                    float lado = Mathf.Min(celdaEquipado.Value.x, paso - 8f);
                    r.sizeDelta = new Vector2(lado, lado);
                    r.anchoredPosition = new Vector2(primero.anchoredPosition.x + paso * i, primero.anchoredPosition.y);
                    i++;
                }
            }
        }

        /// Mete TODAS las habilidades (las del juego y las del mod) en un contenido que se mueve con la rueda del ratón
        /// dentro de una ventana de 5 filas, entre el título «Habilidades» y la sección «Equipado» (que queda fija).
        void ArmarDesplazamiento(GameObject inv, List<RectTransform> botones, List<float> cols, List<float> filas, float pasoX, float pasoY, int filasTotales)
        {
            float ancho = pasoX * 5f, alto = pasoY * FilasVentanaHab;
            float cx = (cols[0] + cols[cols.Count - 1]) / 2f, cy = filas[0] + pasoY * 0.5f - alto / 2f;   // arriba donde siempre
            var vg = new GameObject("coop_habilidades", typeof(RectTransform), typeof(RectMask2D));
            var vr = (RectTransform)vg.transform; vr.SetParent(inv.transform, false);
            vr.anchorMin = vr.anchorMax = new Vector2(0.5f, 0.5f); vr.pivot = new Vector2(0.5f, 0.5f);
            vr.anchoredPosition = new Vector2(cx, cy); vr.sizeDelta = new Vector2(ancho, alto);
            var cg = new GameObject("contenido", typeof(RectTransform));
            var cr = (RectTransform)cg.transform; cr.SetParent(vr, false);
            cr.anchorMin = cr.anchorMax = new Vector2(0.5f, 0.5f); cr.pivot = new Vector2(0.5f, 1f);
            cr.sizeDelta = new Vector2(ancho, filasTotales * pasoY);
            cr.anchoredPosition = new Vector2(0f, alto / 2f);
            // todas las habilidades (del juego y del mod) pasan al contenido, sin moverse de donde están
            var todas = new List<Transform>();
            foreach (Transform t in inv.transform) if (t.name.StartsWith("skill_") || t.name.StartsWith("coop_habilidad_")) todas.Add(t);
            // filas ocupadas: se cuentan ANTES de pasarlas al contenido (después sus coordenadas son otras y
            // salía una fila de menos: la Llave maestra, en la 7.ª, no se podía ver; 2026-11-17)
            int filasReales = 1;
            foreach (var t in todas) { if (!t.gameObject.activeSelf) continue; int fr = Mathf.RoundToInt((filas[0] - ((RectTransform)t).anchoredPosition.y) / pasoY) + 1; if (fr > filasReales) filasReales = fr; }
            foreach (var t in todas) t.SetParent(cr, true);
            filasTotales = filasReales;
            invGo = inv; contenidoHab = cr; viewportHab = vr;
            baseContenidoY = cr.anchoredPosition.y; altoFila = pasoY; desplazo = 0f;
            maxDesplazo = Mathf.Max(0f, filasTotales * pasoY - alto);
            plugin.Log("Inventario: habilidades con desplazamiento (" + filasTotales + " filas, ventana de 5, recorrido " + Mathf.RoundToInt(maxDesplazo) + ")");
        }

        /// Rueda del ratón sobre el inventario: mueve la lista de habilidades (unos 2/3 de fila por muesca).
        void RuedaInventario()
        {
            InventarioAbierto = invGo != null && invGo.activeInHierarchy;
            if (!InventarioAbierto || contenidoHab == null) return;
            float w = LeerRueda();
            if (w == 0f) return;
            if (RatonEnEquipado()) { DesplazarEq(-Mathf.Clamp(w, -3f, 3f) * 45f); return; }   // ~2/3 de fila por muesca
            DesplazarHab(-Mathf.Clamp(w, -3f, 3f) * altoFila * 0.66f);
        }

        /// d > 0 baja la lista (se ven habilidades de más abajo), d < 0 la sube.
        public void DesplazarHab(float d)
        {
            if (contenidoHab == null) return;
            desplazo = Mathf.Clamp(desplazo + d, 0f, maxDesplazo);
            contenidoHab.anchoredPosition = new Vector2(contenidoHab.anchoredPosition.x, baseContenidoY + desplazo);
            ActualizarClics();
        }

        /// Lo que queda fuera de la ventana no recibe el ratón (las máscaras de la interfaz solo recortan el dibujo).
        void ActualizarClics()
        {
            var vc = new Vector3[4]; viewportHab.GetWorldCorners(vc);
            foreach (Transform t in contenidoHab)
            {
                var g = t.GetComponent<Graphic>(); if (g == null) continue;
                float y = t.position.y;
                g.raycastTarget = y > vc[0].y && y < vc[1].y;
            }
        }

        public string InfoHab() { return contenidoHab == null ? "sin desplazamiento" : "desplazo " + desplazo.ToString("0") + "/" + maxDesplazo.ToString("0") + " fila " + altoFila.ToString("0") + " hijos " + contenidoHab.childCount; }

        static void Agregar(List<float> l, float v) { foreach (var x in l) if (Mathf.Abs(x - v) < 5f) return; l.Add(v); }
    }

    /// Pasa la rueda del ratón de un artículo a la lista que lo contiene.
    public class ReenviarRueda : MonoBehaviour, IScrollHandler
    {
        public ScrollRect Lista;
        public void OnScroll(PointerEventData datos) { if (Lista != null) Lista.OnScroll(datos); }
    }

    /// Precio de artículos del juego (2026-11-19): cambiar el script por adelantado no basta (al abrir la tienda el juego
    /// vuelve a cargar sus acciones y quedaba en 20). Se corrige justo cuando el botón fija price_int, según su item_name.
    [HarmonyLib.HarmonyPatch(typeof(HutongGames.PlayMaker.Actions.SetIntValue), "OnEnter")]
    static class ParchePrecioJuego
    {
        static void Postfix(HutongGames.PlayMaker.Actions.SetIntValue __instance)
        {
            try
            {
                var v = __instance.intVariable;
                if (v == null || v.Name != "price_int" || __instance.Fsm == null) return;
                var nombre = __instance.Fsm.Variables.GetFsmString("item_name");
                int nuevo;
                if (nombre != null && nombre.Value != null && TiendaUI.PreciosJuego.TryGetValue(nombre.Value, out nuevo) && v.Value != nuevo) v.Value = nuevo;
            }
            catch { }
        }
    }
}

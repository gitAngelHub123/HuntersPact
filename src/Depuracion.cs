using System.Collections.Generic;
using HutongGames.PlayMaker;
// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using System;
using System.IO;
using BepInEx;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace WitchHuntCoop
{
    /// Herramientas de prueba: el mod lee órdenes de BepInEx/coop-ordenes.txt (una por línea)
    /// y deja las respuestas en el registro. Permite probar sin tocar el teclado.
    ///   anfitrion | unirse <ip> | nombre <texto> | desconectar
    ///   botones               lista los botones visibles
    ///   boton <texto>         pulsa el botón cuyo texto contiene <texto>
    ///   captura <nombre>      guarda BepInEx/capturas/<nombre>.png
    ///   bot on|off            camina en círculos sola (aunque la ventana no esté activa)
    ///   junto                 teletransporta al jugador al lado del compañero
    ///   estado                escribe posición propia, del compañero y escena
    public class Depuracion
    {
        readonly Plugin plugin;
        readonly string archivo;
        readonly string carpetaCapturas;
        float proximaLectura;

        public static bool Bot;
        public static float BotHasta;   // 0 = sin límite
        public static bool BotRecto;    // "bot recto N": camina en línea recta
        string escenaPendiente;         // escena cargada con la orden "escena": permitir armas al llegar

        public Depuracion(Plugin p)
        {
            plugin = p;
            archivo = Path.Combine(Paths.BepInExRootPath, "coop-ordenes.txt");
            carpetaCapturas = Path.Combine(Paths.BepInExRootPath, "capturas");
        }

        static float AlturaMax(Vector2 xz)
        {
            float m = 0f;
            foreach (var t in Terrain.activeTerrains)
            {
                if (t == null || t.terrainData == null) continue;
                Vector3 o = t.transform.position, sz = t.terrainData.size;
                if (xz.x < o.x || xz.y < o.z || xz.x > o.x + sz.x || xz.y > o.z + sz.z) continue;
                m = Mathf.Max(m, t.SampleHeight(new Vector3(xz.x, 0f, xz.y)) + o.y);
            }
            return m;
        }

        static readonly Dictionary<string, int> danoOriginal = new Dictionary<string, int>();   // orden 'danoarma'
        public static float VigilarRecargaHasta; string vrUltimo = "";
        void VigilarRecarga()
        {
            if (Time.time > VigilarRecargaHasta) return;
            var pw = Rendimiento.Armas; if (pw == null || pw.aSources == null || pw.aSources.Length < 2 || pw.aSources[1] == null) return;
            var a = pw.aSources[1];
            string e = (a.clip != null ? a.clip.name : "-") + " suena=" + a.isPlaying + " virtual=" + a.isVirtual + " prio=" + a.priority + " vol=" + a.volume.ToString("0.00");
            if (e != vrUltimo) { vrUltimo = e; plugin.Log("  recarga t=" + Time.time.ToString("0.00") + ": " + e); }
        }

        public void Actualizar()
        {
            VigilarRecarga();
            if (Bot && BotHasta > 0f && Time.time > BotHasta) { Bot = false; BotHasta = 0f; plugin.Log("  bot terminado"); }
            if (escenaPendiente != null && SceneManager.GetActiveScene().name == escenaPendiente && Time.timeSinceLevelLoad > 3f)
            {
                // El pueblo prohíbe las armas (zona segura) y solo su salida normal las vuelve a permitir.
                // Al saltar de escena con la orden de prueba hay que permitirlas a mano.
                var pw = UnityEngine.Object.FindObjectOfType<PlayerWeapons>();
                if (pw != null && !escenaPendiente.StartsWith("Bellville")) { pw.NoWeaponsAllowed = false; plugin.Log("  armas permitidas en " + escenaPendiente); }
                escenaPendiente = null;
            }
            if (Time.realtimeSinceStartup < proximaLectura) return;
            proximaLectura = Time.realtimeSinceStartup + 0.5f;
            if (!File.Exists(archivo)) return;

            string[] lineas;
            try
            {
                lineas = File.ReadAllLines(archivo);
                File.Delete(archivo);
            }
            catch { return; }

            foreach (string l in lineas)
            {
                string linea = l.Trim();
                if (linea.Length == 0) continue;
                plugin.Log("Orden: " + linea);
                try { Ejecutar(linea); }
                catch (Exception e) { plugin.Log("  falló: " + e.Message); plugin.Log("  traza: " + e.ToString().Replace("\n", " ‖ ")); }
            }
        }

        void Ejecutar(string linea)
        {
            int esp = linea.IndexOf(' ');
            string orden = (esp < 0 ? linea : linea.Substring(0, esp)).ToLower();
            string arg = esp < 0 ? "" : linea.Substring(esp + 1).Trim();

            switch (orden)
            {
                case "anfitrion": plugin.OrdenAnfitrion(); break;
                case "red":   // red steam|ip: qué conexión usan Crear/Unirse sala (pruebas: IP por defecto) (2026-11-20)
                    if (arg == "steam") Plugin.ForzarIp = false; else if (arg == "ip") Plugin.ForzarIp = true;
                    plugin.Log("  conexión: " + (plugin.UsarSteam ? "Steam" : "IP") + " (Steam disponible: " + Red.SteamDisponible + ", forzar IP: " + Plugin.ForzarIp + ")");
                    break;
                case "elegir":   // elegir continuar | elegir nueva [0|1|2 = normal|difícil|pesadilla] (anfitrión)
                {
                    var pa = arg.Split(' ');
                    if (pa[0] == "nueva") Opciones.FuegoAmigoElegido = System.Array.IndexOf(pa, "fa") > 0 ? 1 : 0;   // elegir nueva <dif> fa = con fuego amigo (2026-11-20)
                    plugin.MenuSalaActual.OrdenElegir(pa[0] == "nueva" ? 1 : 0, pa.Length > 1 && pa[1] != "fa" ? int.Parse(pa[1]) : 0);
                    break;
                }
                case "armacomp":   // armacomp | armacomp ver <n>|off | armacomp ajuste <nombre> rx ry rz px py pz | armacomp rehacer (2026-11-20)
                {
                    var pa = arg.Split(' '); var ci = System.Globalization.CultureInfo.InvariantCulture;
                    if (pa[0] == "ver") ArmaCompanero.Forzado = pa.Length > 1 && pa[1] != "off" ? int.Parse(pa[1]) : -1;
                    else if (pa[0] == "rehacer") ArmaCompanero.Limpiar();
                    else if (pa[0] == "pose" && pa.Length > 1) { ArmaCompanero.ForzadoBanderas = (byte)(pa[1] == "apuntar" ? 1 : pa[1] == "recargar" ? 2 : 0); if (pa[1] == "tiro") ArmaCompanero.ForzadoTiro = true; }   // con 'ver n': apuntar|tiro|nada|recargar
                    else if (pa[0] == "ajuste" && pa.Length >= 8)
                        ArmaCompanero.Ajustes[pa[1]] = new[] { new Vector3(float.Parse(pa[2], ci), float.Parse(pa[3], ci), float.Parse(pa[4], ci)), new Vector3(float.Parse(pa[5], ci), float.Parse(pa[6], ci), float.Parse(pa[7], ci)) };
                    plugin.Log("  " + ArmaCompanero.Informe(plugin));
                    break;
                }
                case "musica":   // musica [on|off]: estado de la música del juego (SoundManager) y quién la silenció; on/off la cambia (2026-11-20)
                    if ((arg == "on" || arg == "off") && RegistroMusica.Existente() != null) SoundManager.MuteMusic(arg == "off");   // (nunca SoundManager.Instance sin que exista)
                    if (arg == "creditos" && RegistroMusica.Existente() != null) SoundManager.PlayConnection("final_ambient");   // prueba del fin del juego (FinalCoop): la música del final
                    plugin.Log("  " + RegistroMusica.Informe());
                    break;
                case "fuentesarmas":   // fuentesarmas: las fuentes de audio del jugador para disparo (0) y recarga (1) (2026-11-20)
                {
                    var pw = Rendimiento.Armas;
                    if (pw == null || pw.aSources == null) { plugin.Log("  sin PlayerWeapons"); break; }
                    for (int k = 0; k < pw.aSources.Length; k++) { var a = pw.aSources[k]; if (a == null) continue;
                        plugin.Log("  fuente " + k + ": clip=" + (a.clip != null ? a.clip.name : "-") + " suena=" + a.isPlaying + " virtual=" + a.isVirtual + " prio=" + a.priority + " vol=" + a.volume.ToString("0.00") + " mute=" + a.mute + " activa=" + a.isActiveAndEnabled + " 3D=" + a.spatialBlend.ToString("0.00")); }
                    int sonando = 0, reales = 0; foreach (var a in UnityEngine.Object.FindObjectsOfType<AudioSource>()) if (a.isPlaying) { sonando++; if (!a.isVirtual) reales++; }
                    plugin.Log("  fuentes sonando en la escena: " + sonando + " (reales " + reales + ")");
                    break;
                }
                case "vigilarrecarga": VigilarRecargaHasta = Time.time + (arg.Length > 0 ? float.Parse(arg, System.Globalization.CultureInfo.InvariantCulture) : 8f); break;   // anota cada cambio de la fuente de recarga
                case "historia":   // historia [vars <filtro>]: misiones del juego (estado y descripción) o variables del sistema de diálogos (2026-11-20)
                {
                    var db = PixelCrushers.DialogueSystem.DialogueManager.MasterDatabase;
                    if (db == null) { plugin.Log("  sin base de diálogos"); break; }
                    var pa = arg.Split(new[] { ' ' }, 2);
                    if (pa[0] == "vars")
                    {
                        string fil = pa.Length > 1 ? pa[1].ToLowerInvariant() : ""; var sb = new System.Text.StringBuilder(); int n = 0;
                        foreach (var v in db.variables)
                        {
                            if (v == null || (fil.Length > 0 && !v.Name.ToLowerInvariant().Contains(fil))) continue;
                            string val; try { val = PixelCrushers.DialogueSystem.DialogueLua.GetVariable(v.Name).AsString; } catch { val = "?"; }
                            sb.Append(v.Name).Append('=').Append(val).Append("; "); if (++n % 12 == 0) { plugin.Log("  " + sb); sb.Length = 0; }
                        }
                        if (sb.Length > 0) plugin.Log("  " + sb);
                        plugin.Log("  (" + n + " variables)");
                        break;
                    }
                    if (pa[0] == "dialogos")   // historia dialogos <filtro>: entradas de conversación cuyo guion o condición contengan el texto
                    {
                        string fil = pa.Length > 1 ? pa[1] : ""; int n = 0;
                        foreach (var cv in db.conversations)
                            foreach (var de in cv.dialogueEntries)
                            {
                                string sc = de.userScript ?? "", co = de.conditionsString ?? "";
                                if (fil.Length > 0 && sc.IndexOf(fil, StringComparison.OrdinalIgnoreCase) < 0 && co.IndexOf(fil, StringComparison.OrdinalIgnoreCase) < 0) continue;
                                string txt = de.DialogueText ?? ""; if (txt.Length > 60) txt = txt.Substring(0, 60) + "…";
                                plugin.Log("  conv " + cv.id + " '" + cv.Title + "' #" + de.id + " cond[" + co + "] guion[" + sc + "] texto[" + txt + "]");
                                if (++n > 60) break;
                            }
                        plugin.Log("  (" + n + " entradas)");
                        break;
                    }
                    int k = 0;
                    foreach (var it in db.items)
                    {
                        if (it == null || it.IsItem) continue;
                        string nom = it.Name, est = PixelCrushers.DialogueSystem.QuestLog.GetQuestState(nom).ToString();
                        string desc = PixelCrushers.DialogueSystem.QuestLog.GetQuestDescription(nom) ?? "";
                        if (desc.Length > 140) desc = desc.Substring(0, 140) + "…";
                        plugin.Log("  misión " + (k++) + " [" + est + "] '" + nom + "': " + desc.Replace("\n", " ").Replace("\r", ""));
                    }
                    break;
                }
                case "danoarma":   // danoarma <nombre> <factor>: multiplica el daño de esa arma en vivo (p. ej. danoarma Pistol 20); factor 1 = original (2026-11-20)
                {
                    var pa = arg.Split(' '); var pw = Rendimiento.Armas;
                    if (pa.Length < 2 || pw == null || pw.weaponOrder == null) { plugin.Log("  uso: danoarma <nombre> <factor>"); break; }
                    float fac = float.Parse(pa[1], System.Globalization.CultureInfo.InvariantCulture);
                    foreach (var w in pw.weaponOrder)
                    {
                        if (w == null || w.name.IndexOf(pa[0], StringComparison.OrdinalIgnoreCase) < 0) continue;
                        var wb = w.GetComponent<WeaponBehavior>(); if (wb == null) continue;
                        int orig; if (!danoOriginal.TryGetValue(w.name, out orig)) { orig = wb.damage; danoOriginal[w.name] = orig; }
                        wb.damage = Mathf.RoundToInt(orig * fac);   // LibrosTienda toma este valor como base nueva (no lo pisa)
                        plugin.Log("  " + w.name + ": daño " + orig + " × " + fac + " = " + wb.damage);
                    }
                    break;
                }
                case "fuegoamigo":   // fuegoamigo [1|0]: activa/apaga en la partida (anfitrión) y dice el estado (2026-11-20)
                    if (arg == "1" || arg == "0") PixelCrushers.DialogueSystem.DialogueLua.SetVariable(Opciones.VarFuegoAmigo, arg == "1");
                    plugin.Log("  " + FuegoAmigo.Informe(plugin));
                    break;
                case "sala":   // sala crear|unirse|aceptar|cerrar: el cuadro del menú principal
                    if (arg == "aceptar") plugin.MenuSalaActual.OrdenAceptar();
                    else if (arg == "cerrar") plugin.MenuSalaActual.OrdenCerrar();
                    else plugin.MenuSalaActual.OrdenAbrir(arg != "unirse");
                    break;
                case "unirse": plugin.OrdenUnirse(arg); break;
                case "nombre": plugin.OrdenNombre(arg); break;
                case "desconectar": plugin.OrdenDesconectar(); break;
                case "botones": ListarBotones(); break;
                case "boton": Pulsar(arg); break;
                case "captura": Captura(arg); break;
                case "fotolimpia": plugin.StartCoroutine(Foto(arg)); break;
                case "fotodesde": plugin.StartCoroutine(FotoDesde(arg)); break;   // fotodesde x y z tx ty tz [zoom fov] [luz] [nombre]: la cámara solo para la foto (no despierta al jefe)   // fotolimpia <nombre>: sin interfaz ni arma, 2× resolución (2026-11-20)
                case "catalogo": Catalogo.Hacer(plugin, arg); break;
                case "lugares": plugin.Log("  " + plugin.mundo.DescribirLugares()); break;
                case "mapa": plugin.Log("  " + plugin.mapa.Describir()); break;
                case "mapapng":
                {
                    // mapapng [todo]: guarda lo que dibuja la cámara del mapa (con "todo": todas las capas)
                    foreach (var c in Resources.FindObjectsOfTypeAll<Camera>())
                    {
                        if (c.name != "Map_Camera" || !c.gameObject.scene.IsValid() || c.targetTexture == null) continue;
                        int capas = c.cullingMask;
                        if (arg == "todo") c.cullingMask = ~0;
                        c.Render();
                        c.cullingMask = capas;
                        var rt = c.targetTexture;
                        RenderTexture.active = rt;
                        var tx = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                        tx.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tx.Apply();
                        RenderTexture.active = null;
                        string ruta = Path.Combine(carpetaCapturas, "mapa_" + (arg == "todo" ? "todo" : "normal") + ".png");
                        File.WriteAllBytes(ruta, tx.EncodeToPNG());
                        UnityEngine.Object.Destroy(tx);
                        plugin.Log("  mapa en " + ruta);
                        int n = 0;
                        foreach (var g in UnityEngine.Object.FindObjectsOfType<Renderer>())
                            if (g.gameObject.layer == 23 && n++ < 15) plugin.Log("  capa 23: " + RutaDe(g.transform, null) + " pos=" + g.transform.position + " tam=" + g.bounds.size);
                    }
                    break;
                }
                case "balas": plugin.Log("  " + plugin.municion.Describir()); break;
                case "disparar": Entrada.Disparar = arg.Length > 0 ? int.Parse(arg) : 6; break;
                case "recargar": Entrada.Recargar = 2; break;
                case "zoom": Entrada.Apuntar = arg.Length > 0 ? int.Parse(arg) : 300; break;   // zoom <fotogramas>: mantiene apuntar (clic derecho)
                case "golpes": plugin.Log("  golpes=" + ParcheMuerteEnemigo.Golpes + " daño=" + ParcheMuerteEnemigo.Dano.ToString("0")); ParcheMuerteEnemigo.Golpes = 0; ParcheMuerteEnemigo.Dano = 0f; break;
                case "elemental":
                    // elemental [hielo|veneno]: lista los elementales o prueba el efecto en el jugador
                    if (arg.StartsWith("forzar"))
                    {
                        var tdm = MasCercano();
                        if (tdm != null) { var u0 = Combate.RaizDe(tdm); Elemental.Aplicar(u0.gameObject, arg.Contains("radi") ? Elemental.Tipo.Radiactivo : arg.Contains("verde") ? Elemental.Tipo.Veneno : Elemental.Tipo.Hielo); plugin.Log("  elemental en " + u0.name); }
                    }
                    else if (arg.StartsWith("hielo")) Elemental.Probar(Elemental.Tipo.Hielo);
                    else if (arg.StartsWith("veneno")) Elemental.Probar(Elemental.Tipo.Veneno);
                    else if (arg.StartsWith("radi")) Elemental.Probar(Elemental.Tipo.Radiactivo);
                    plugin.Log("  " + Elemental.Describir());
                    break;
                case "campana":
                    // campana | campana wav: toca la campanada de la luna de sangre o la guarda como WAV
                    if (arg == "secuencia") { Campana.TocarEntrada(1f); float de = Campana.DuracionEntrada(); Campana.ProgramarFondo(de, 0.8f); plugin.Log("  entrada (" + de.ToString("0.0") + " s) y después la campana sola en bucle"); }
                    else if (arg == "estado") plugin.Log("  " + Campana.Estado());
                    else if (arg == "parar") { Campana.Detener(); plugin.Log("  campanas detenidas"); }
                    else if (arg == "unica") { Campana.TocarUnica(); var cu = Campana.ClipUnica(); plugin.Log("  campana sola " + (cu != null ? cu.length.ToString("0.0") + " s" : "(aún no cargada)")); }
                    else if (arg == "natural" || arg == "oscura") { Campana.CambiarVariante(plugin, arg); plugin.Log("  variante " + arg); }
                    else if (arg == "wav") plugin.Log("  " + Campana.GuardarWav()); else { Campana.Tocar(); var cc = Campana.Clip(); plugin.Log("  sonó '" + cc.name + "' (" + cc.length.ToString("0.0") + " s)"); }
                    break;
                case "suceso":
                {
                    // suceso <luna|tranquila|niebla|oscura|ruidosa> [segundos] | suceso fin | suceso
                    var pa = arg.Split(' ');
                    if (pa[0] == "fin") plugin.eventos.OrdenTerminar();
                    else if (pa[0] == "rayo") plugin.eventos.OrdenRayo(pa.Length > 1 ? pa[1] : null);   // suceso rayo [foto]
                    else if (pa[0].Length > 0) plugin.eventos.OrdenIniciar(Eventos.TipoPorNombre(pa[0]), pa.Length > 1 ? float.Parse(pa[1], System.Globalization.CultureInfo.InvariantCulture) : 0f);
                    plugin.Log("  " + plugin.eventos.Describir());
                    break;
                }
                case "mana": if (arg.Length > 0) Muerte.PonerMana(float.Parse(arg, System.Globalization.CultureInfo.InvariantCulture)); plugin.Log("  maná " + Muerte.LeerMana()); break;   // mana [n]: ver o poner el maná (ManaCurrent)
                case "palancaojo":   // palancaojo: hace lo mismo que usar la palanca de la terraza con E (baja la palanca y mata al Ojo)
                {
                    var po = UnityEngine.Object.FindObjectOfType<PalancaOjo>();
                    if (po == null) { plugin.Log("  no hay palanca del Ojo en la escena"); break; }
                    po.Bajar(); plugin.mundo.MatarOjo(true);
                    plugin.Log("  palanca accionada en " + (po.transform.position - Anclaje.Origen()).ToString("F1"));
                    break;
                }
                case "rayo":   // rayo: objetos que atraviesa el rayo del centro de la cámara (30 m), con su caja
                {
                    var cm = Camera.main; if (cm == null) { plugin.Log("  sin cámara"); break; }
                    var hits = Physics.RaycastAll(cm.transform.position, cm.transform.forward, 30f, ~0, QueryTriggerInteraction.Ignore);
                    System.Array.Sort(hits, (h1, h2) => h1.distance.CompareTo(h2.distance));
                    int nh = 0;
                    foreach (var h in hits)
                    {
                        if (h.collider.GetComponentInParent<FPSPlayer>() != null) continue;
                        var rr = h.collider.GetComponent<Renderer>();
                        plugin.Log("  " + h.distance.ToString("0.0") + " m: " + h.collider.name + " (raíz " + h.collider.transform.root.name + ") punto " + (h.point - Anclaje.Origen()).ToString("F1") + (rr != null ? " caja " + (rr.bounds.min - Anclaje.Origen()).ToString("F2") + "–" + (rr.bounds.max - Anclaje.Origen()).ToString("F2") : ""));
                        if (++nh >= 6) break;
                    }
                    if (nh == 0) plugin.Log("  nada en 30 m");
                    break;
                }
                case "levantar": Muerte.Instancia.OrdenLevantar(); break;   // levanta al compañero derribado
                case "bola":
                {
                    // bola | bola dar|quitar|comprar|invocar | bola alma <tipo>
                    var pa = arg.Split(new[] { ' ' }, 2);
                    if (pa[0] == "dar") PixelCrushers.DialogueSystem.DialogueLua.SetVariable(Aliados.VarBola, true);
                    else if (pa[0] == "quitar") PixelCrushers.DialogueSystem.DialogueLua.SetVariable(Aliados.VarBola, false);
                    else if (pa[0] == "comprar") plugin.Log("  " + plugin.aliados.Comprar());
                    else if (pa[0] == "invocar") plugin.aliados.OrdenInvocar();
                    else if (pa[0] == "junto") plugin.aliados.OrdenJunto();
                    else if (pa[0] == "alma" && pa.Length > 1) { var l = Aliados.Almas(); l.Add(pa[1]); PixelCrushers.DialogueSystem.DialogueLua.SetVariable(Aliados.VarAlmas, string.Join("|", l.ToArray())); }
                    plugin.Log("  " + plugin.aliados.Describir());
                    break;
                }
                case "ballesta":
                {
                    // ballesta | ballesta dar|quitar|fotos | ballesta ajuste px py pz rx ry rz escala | ballesta flechas n
                    var pa = arg.Split(' ');
                    var ci = System.Globalization.CultureInfo.InvariantCulture;
                    if (pa[0] == "dar") Ballesta.Dar(true);
                    else if (pa[0] == "quitar") Ballesta.Dar(false);
                    else if (pa[0] == "fotos") Ballesta.Copia.Fotos("bal");
                    else if (pa[0] == "tex" && pa.Length >= 3 && Ballesta.Copia.MaterialExtra != null)
                    {
                        // ballesta tex <sesgo mip> <punto|bilineal|trilineal>: prueba en vivo de la textura propia
                        var tx = Ballesta.Copia.MaterialExtra.mainTexture; tx.mipMapBias = float.Parse(pa[1], ci);
                        tx.filterMode = pa[2] == "punto" ? FilterMode.Point : (pa[2] == "bilineal" ? FilterMode.Bilinear : FilterMode.Trilinear);
                        plugin.Log("  textura: sesgo " + tx.mipMapBias + " filtro " + tx.filterMode);
                    }
                    else if (pa[0] == "pintar" && Ballesta.Copia.Modelo != null)
                    {
                        // ballesta pintar original|madera|hierro|mate: tiñe esa parte de magenta (para saber qué es cada pieza)
                        var rm = Ballesta.Copia.Modelo.GetComponent<Renderer>(); var mm = rm.sharedMaterials; int nOrig = mm.Length - 3;
                        var tinte = new Material(Shader.Find("Unlit/Color")) { color = Color.magenta };
                        int idx = pa.Length < 2 ? -1 : (pa[1] == "original" ? 0 : (pa[1] == "madera" ? nOrig : (pa[1] == "hierro" ? nOrig + 1 : nOrig + 2)));
                        if (idx >= 0) { if (pa[1] == "original") for (int q = 0; q < nOrig; q++) mm[q] = tinte; else mm[idx] = tinte; rm.sharedMaterials = mm; }
                        plugin.Log("  pintadas: " + (pa.Length < 2 ? "-" : pa[1]));
                    }
                    else if (pa[0] == "pantalla" && Ballesta.Copia.Modelo != null && Camera.main != null)
                    {
                        var rd = Ballesta.Copia.Modelo.GetComponent<Renderer>();
                        Vector3 sp = Camera.main.WorldToScreenPoint(rd.bounds.center);
                        plugin.Log("  ballesta en pantalla: (" + sp.x.ToString("F0") + ", " + sp.y.ToString("F0") + ") profundidad " + sp.z.ToString("F2") + " · ajuste " + Ballesta.Copia.AjustePos.ToString("F3"));
                    }
                    else if (pa[0] == "animbal") plugin.Log("  " + AnimBallesta.Parametro(arg.Length > 8 ? arg.Substring(8) : ""));
                    else if (pa[0] == "giro") plugin.Log("  " + Ballesta.Giro(pa.Length > 1 ? pa[1] : "x", pa.Length > 2 ? pa[2] : ""));
                    else if (pa[0] == "grabar") { Ballesta.EmpezarGrabar(); plugin.Log("  grabando cuadros del disparo y la recarga"); }
                    else if (pa[0] == "pose") plugin.Log("  " + Ballesta.Pose(pa.Length > 1 ? pa[1] : "x", pa.Length > 2 ? pa[2] : "x", pa.Length > 3 ? pa[3] : "x"));
                    else if (pa[0] == "sombras" && pa.Length >= 2 && Ballesta.Copia.Modelo != null)
                    {
                        var rm = Ballesta.Copia.Modelo.GetComponent<Renderer>(); rm.receiveShadows = pa[1] != "0";
                        plugin.Log("  la ballesta " + (rm.receiveShadows ? "recibe" : "no recibe") + " sombras");
                    }
                    else if (pa[0] == "mat" && pa.Length >= 2 && Ballesta.Copia.MaterialExtra != null)
                    {
                        // ballesta mat <brillo 0-1>: brillo del material propio
                        var mx = Ballesta.Copia.MaterialExtra; if (mx.HasProperty("_Glossiness")) mx.SetFloat("_Glossiness", float.Parse(pa[1], ci));
                        plugin.Log("  material: " + mx.shader.name);
                    }
                    else if (pa[0] == "flechas" && pa.Length > 1) plugin.Log("  +" + plugin.municion.SumarFlechas(int.Parse(pa[1])) + " flechas");
                    else if (pa[0] == "comprar") plugin.Log("  " + plugin.tienda.ComprarFlechas());
                    else if (pa[0] == "ajuste" && pa.Length >= 8)
                    {
                        Ballesta.Copia.AjustePos = new Vector3(float.Parse(pa[1], ci), float.Parse(pa[2], ci), float.Parse(pa[3], ci));
                        Ballesta.Copia.AjusteRot = new Vector3(float.Parse(pa[4], ci), float.Parse(pa[5], ci), float.Parse(pa[6], ci));
                        Ballesta.Copia.AjusteEscala = float.Parse(pa[7], ci);
                        Ballesta.Copia.Colocar();
                    }
                    plugin.Log("  " + Ballesta.Describir() + " | flechas=" + plugin.municion.FlechasQueTengo);
                    break;
                }
                case "rueda_arma": SelectorArmas.RuedaPrueba = float.Parse(arg); break;
                case "recfotos":
                {
                    // recfotos <prefijo> <t1> <t2> …: recarga y captura en esos instantes (s reales)
                    var pa = arg.Split(' ');
                    plugin.StartCoroutine(RecFotos(pa));
                    break;
                }
                case "mano":
                {
                    // mano lista | mano reset | mano <hueso> <ex> <ey> <ez> : gira un hueso de la mano izquierda (prueba)
                    var rc0 = Escopeta.Copia.Arma != null ? Escopeta.Copia.Arma.GetComponent<WeaponBehavior>().weaponMesh.GetComponent<RecargaEscopeta>() : null;
                    if (rc0 == null) { plugin.Log("  sin RecargaEscopeta"); break; }
                    var pa = arg.Split(' ');
                    if (pa[0] == "lista") plugin.Log("  huesos: " + rc0.ListaHuesos());
                    else if (pa[0] == "reset") { rc0.ReiniciarPrueba(); plugin.Log("  mano restaurada"); }
                    else if (pa[0] == "modelo") { if (Escopeta.Copia.Modelo != null) Escopeta.Copia.Modelo.SetActive(pa.Length > 1 && pa[1] == "on"); plugin.Log("  modelo del arma " + (pa.Length > 1 ? pa[1] : "?")); }
                    else
                    {
                        // el nombre puede llevar espacios: los 3 últimos números son los ángulos
                        int n = pa.Length; string nom = string.Join(" ", pa, 0, n - 3);
                        var ci = System.Globalization.CultureInfo.InvariantCulture;
                        RecargaEscopeta.PruebaHuesos[nom] = new Vector3(float.Parse(pa[n - 3], ci), float.Parse(pa[n - 2], ci), float.Parse(pa[n - 1], ci));
                        plugin.Log("  " + nom + " = " + RecargaEscopeta.PruebaHuesos[nom]);
                    }
                    break;
                }
                case "caminos":
                {
                    // caminos <filtro>: objetos del bosque cuyo nombre contenga el texto (con tamaño y capa)
                    int nn = 0;
                    foreach (var g in Resources.FindObjectsOfTypeAll<GameObject>())
                    {
                        if (!g.scene.IsValid() || g.name.IndexOf(arg, System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                        var rr = g.GetComponent<Renderer>(); var cc = g.GetComponent<Collider>();
                        plugin.Log("  " + g.name + " pos=" + g.transform.position.ToString("F0") + (rr != null ? " tam=" + rr.bounds.size.ToString("F0") : "") + (cc != null ? " col=" + cc.GetType().Name : "") + " capa=" + g.layer + " padre=" + (g.transform.parent != null ? g.transform.parent.name : "-"));
                        if (++nn > 25) break;
                    }
                    plugin.Log("  encontrados (máx. 25): " + nn);
                    break;
                }
                case "capaspng":
                {
                    var tt = Anclaje.Principal(); if (tt == null) break;
                    var tdd = tt.terrainData; int W = tdd.alphamapWidth, H = tdd.alphamapHeight;
                    var al = tdd.GetAlphamaps(0, 0, W, H);
                    int nc = al.GetLength(2); int paso = Mathf.Max(1, W / 512), N = W / paso;
                    for (int c = 0; c < nc; c++)
                    {
                        var tex = new Texture2D(N, N, TextureFormat.RGB24, false);
                        for (int y = 0; y < N; y++) for (int x = 0; x < N; x++) { float v = al[y * paso, x * paso, c]; tex.SetPixel(x, y, new Color(v, v, v)); }
                        tex.Apply();
                        System.IO.File.WriteAllBytes(System.IO.Path.Combine(BepInEx.Paths.BepInExRootPath, "capturas/capa_" + c + ".png"), tex.EncodeToPNG());
                    }
                    plugin.Log("  " + nc + " capas guardadas (" + N + "x" + N + ")");
                    break;
                }
                case "luces":
                {
                    int nn = 0;
                    foreach (var l in Resources.FindObjectsOfTypeAll<Light>())
                    {
                        if (!l.gameObject.scene.IsValid() || l.type == LightType.Directional) continue;
                        bool naranja = l.color.r > 0.7f && l.color.g < 0.75f && l.color.b < 0.5f;
                        if (!naranja || !l.enabled || !l.gameObject.activeInHierarchy) continue;
                        var cols = Physics.OverlapSphere(l.transform.position, 4f, ~0, QueryTriggerInteraction.Ignore);
                        var ps = l.GetComponentInParent<ParticleSystem>();
                        string cerca = ""; int k = 0;
                        foreach (var c in cols) { if (c is TerrainCollider) continue; cerca += c.name + ","; if (++k >= 4) break; }
                        plugin.Log("  luz '" + l.name + "' raíz='" + l.transform.root.name + "' pos=" + l.transform.position.ToString("F0") + " rango=" + l.range.ToString("0") + " int=" + l.intensity.ToString("0.0") + " cerca[" + cols.Length + "]=" + cerca);
                        if (++nn >= 40) break;
                    }
                    plugin.Log("  luces naranjas: " + nn);
                    break;
                }
                case "cofrepiezas": plugin.Log("  " + plugin.mundo.PiezasCofre()); break;
                case "textos2":
                {
                    int nn = 0;
                    foreach (var tm in Resources.FindObjectsOfTypeAll<TextMesh>())
                        if (tm.gameObject.scene.IsValid() && tm.text != null && tm.text.IndexOf(arg, System.StringComparison.OrdinalIgnoreCase) >= 0 && ++nn < 20)
                            plugin.Log("  TextMesh '" + tm.text.Replace("\n", "/") + "' en " + tm.name + " raíz=" + tm.transform.root.name + " pos=" + tm.transform.position.ToString("F0") + " activo=" + tm.gameObject.activeInHierarchy);
                    foreach (var tx in Resources.FindObjectsOfTypeAll<UnityEngine.UI.Text>())
                        if (tx.gameObject.scene.IsValid() && tx.text != null && tx.text.IndexOf(arg, System.StringComparison.OrdinalIgnoreCase) >= 0 && ++nn < 20)
                            plugin.Log("  Text '" + tx.text.Replace("\n", "/") + "' en " + tx.name + " raíz=" + tx.transform.root.name + " activo=" + tx.gameObject.activeInHierarchy);
                    plugin.Log("  coincidencias: " + nn);
                    break;
                }
                case "caminopct":
                {
                    var tt = Anclaje.Principal(); if (tt == null) break;
                    var tdd = tt.terrainData; var rg = new System.Random(5); int n = 6000;
                    int[] c0 = new int[4], c4 = new int[4], ambos = new int[4]; float[] u0 = { 0.5f, 0.7f, 0.85f, 0.95f }, u4 = { 0.45f, 0.6f, 0.75f, 0.9f };
                    for (int i = 0; i < n; i++)
                    {
                        int ax = rg.Next(40, tdd.alphamapWidth - 40), az = rg.Next(40, tdd.alphamapHeight - 40);
                        var al = tdd.GetAlphamaps(ax, az, 1, 1);
                        for (int k = 0; k < 4; k++) { if (al[0, 0, 0] > u0[k]) c0[k]++; if (al[0, 0, 4] > u4[k]) c4[k]++; if (al[0, 0, 0] > u0[k] || al[0, 0, 4] > u4[k]) ambos[k]++; }
                    }
                    for (int k = 0; k < 4; k++) plugin.Log("  umbral capa0>" + u0[k] + " : " + (100f * c0[k] / n).ToString("0.0") + " %   capa4>" + u4[k] + " : " + (100f * c4[k] / n).ToString("0.0") + " %   cualquiera: " + (100f * ambos[k] / n).ToString("0.0") + " %");
                    break;
                }
                case "capas":
                {
                    var tt = Anclaje.Principal(); if (tt == null) break;
                    var tdd = tt.terrainData; var pr = tdd.splatPrototypes;
                    for (int i = 0; i < pr.Length; i++) plugin.Log("  capa " + i + ": " + (pr[i].texture != null ? pr[i].texture.name : "-"));
                    if (plugin.Jugador != null)
                    {
                        var pp = plugin.Jugador.transform.position; var oo = tt.transform.position; var ss = tdd.size;
                        int ax = Mathf.Clamp((int)((pp.x - oo.x) / ss.x * tdd.alphamapWidth), 0, tdd.alphamapWidth - 1), az = Mathf.Clamp((int)((pp.z - oo.z) / ss.z * tdd.alphamapHeight), 0, tdd.alphamapHeight - 1);
                        var al = tdd.GetAlphamaps(ax, az, 1, 1); var sb2 = new System.Text.StringBuilder("  bajo el jugador:");
                        for (int i = 0; i < pr.Length; i++) sb2.Append(" ").Append(i).Append("=").Append(al[0, 0, i].ToString("0.00"));
                        plugin.Log(sb2.ToString());
                    }
                    break;
                }
                case "animpar": plugin.Log("  " + AnimEscopeta.Parametro(arg)); break;
                case "animacion":
                {
                    // animacion lista | cambios <clip> | pose <clip> <t> | seguir : explora los clips del arma 4 (escopeta)
                    var arma0 = Escopeta.Copia.Arma;
                    var wb0 = arma0 != null ? arma0.GetComponent<WeaponBehavior>() : null;
                    var arma = wb0 != null && wb0.weaponMesh != null ? wb0.weaponMesh : arma0;
                    var an = arma != null ? arma.GetComponent<Animation>() : null;
                    if (an == null) { plugin.Log("  sin arma/Animation"); break; }
                    var pa = arg.Split(' ');
                    if (pa[0] == "lista") { foreach (AnimationState st in an) plugin.Log("  clip " + st.name + " largo=" + st.length.ToString("0.00") + " velocidad=" + st.speed + " capa=" + st.layer + " modo=" + st.wrapMode); }
                    else if (pa[0] == "cambios")
                    {
                        var st = an[pa[1]]; if (st == null) { plugin.Log("  no existe"); break; }
                        var ts = arma.GetComponentsInChildren<Transform>(true);
                        an.enabled = false;
                        st.clip.SampleAnimation(arma, 0f);
                        var p0 = new Dictionary<Transform, Quaternion>(); var q0 = new Dictionary<Transform, Vector3>();
                        foreach (var t in ts) { p0[t] = t.localRotation; q0[t] = t.localPosition; }
                        var maxR = new Dictionary<Transform, float>(); var maxP = new Dictionary<Transform, float>();
                        for (float tt = 0f; tt <= st.length; tt += st.length / 20f)
                        {
                            st.clip.SampleAnimation(arma, tt);
                            foreach (var t in ts)
                            {
                                float r = Quaternion.Angle(p0[t], t.localRotation), d = (q0[t] - t.localPosition).magnitude;
                                float a; maxR[t] = maxR.TryGetValue(t, out a) ? Mathf.Max(a, r) : r;
                                maxP[t] = maxP.TryGetValue(t, out a) ? Mathf.Max(a, d) : d;
                            }
                        }
                        foreach (var t in ts) if (maxR[t] > 3f || maxP[t] > 0.02f) plugin.Log("  " + RutaDe(t, arma.transform) + " giro máx " + maxR[t].ToString("0") + "° mov máx " + maxP[t].ToString("0.000"));
                        an.enabled = true;
                    }
                    else if (pa[0] == "pose") { an.enabled = false; an[pa[1]].clip.SampleAnimation(arma, float.Parse(pa[2])); plugin.Log("  pose " + pa[1] + " t=" + pa[2]); }
                    else if (pa[0] == "seguir") { an.enabled = true; }
                    break;
                }
                case "escopeta":
                {
                    // escopeta | escopeta dar|quitar | escopeta ajuste px py pz rx ry rz escala
                    var pa = arg.Split(' ');
                    var ci = System.Globalization.CultureInfo.InvariantCulture;
                    if (pa[0] == "dar") Escopeta.Dar(true);
                    else if (pa[0] == "fotos") Escopeta.Fotos();
                    else if (pa[0] == "suelta") Escopeta.FotoSuelta();
                    else if (pa[0] == "quitar") Escopeta.Dar(false);
                    else if (pa[0] == "ajuste" && pa.Length >= 8)
                    {
                        Escopeta.Copia.AjustePos = new Vector3(float.Parse(pa[1], ci), float.Parse(pa[2], ci), float.Parse(pa[3], ci));
                        Escopeta.Copia.AjusteRot = new Vector3(float.Parse(pa[4], ci), float.Parse(pa[5], ci), float.Parse(pa[6], ci));
                        Escopeta.Copia.AjusteEscala = float.Parse(pa[7], ci);
                        Escopeta.Copia.Colocar();
                    }
                    plugin.Log("  " + Escopeta.Describir());
                    break;
                }
                case "destreza": VentanaDestreza.Cerrar(); plugin.Log("  ventana de destreza cerrada (escala de tiempo " + Time.timeScale + ")"); break;
                case "pantalla":
                {
                    // pantalla | pantalla abrir | pantalla <modo 0-2> <ancho>x<alto>
                    var pa = arg.Split(' ');
                    if (pa[0] == "abrir") plugin.pantalla.Abrir();
                    else if (pa[0] == "cerrar") plugin.pantalla.OrdenCerrar();
                    else if (pa.Length == 2) plugin.pantalla.OrdenAplicar(int.Parse(pa[0]), pa[1]);
                    plugin.Log("  " + plugin.pantalla.Describir());
                    break;
                }
                case "padres":
                {
                    // padres <texto>: cadena de padres de los textos de la interfaz que contienen ese texto
                    foreach (var tx in UnityEngine.Object.FindObjectsOfType<UnityEngine.UI.Text>())
                    {
                        if (tx.text.IndexOf(arg, StringComparison.OrdinalIgnoreCase) < 0) continue;
                        var sb = new System.Text.StringBuilder();
                        for (Transform t = tx.transform; t != null; t = t.parent) { var rt = t as RectTransform; sb.Append(t.name).Append(rt != null ? "@" + rt.anchoredPosition.ToString("F0") : "").Append(" < "); }
                        plugin.Log("  '" + tx.text + "': " + sb);
                    }
                    break;
                }
                case "rects":
                {
                    // rects <objeto>: hijos directos con su posición en la interfaz y su texto
                    foreach (var g in Resources.FindObjectsOfTypeAll<GameObject>())
                        if (g.name == arg && g.scene.IsValid())
                        {
                            foreach (Transform t in g.transform)
                            {
                                var rt = t as RectTransform; if (rt == null) continue;
                                var tx = t.GetComponentInChildren<UnityEngine.UI.Text>(true);
                                plugin.Log("  " + t.name + " pos=" + rt.anchoredPosition + " tam=" + rt.rect.size + " activo=" + t.gameObject.activeSelf + (tx != null ? " texto='" + tx.text.Replace("\n", " ") + "'" : ""));
                            }
                            break;
                        }
                    break;
                }
                case "textura":
                {
                    // textura <ruta en Resources>: guarda la textura del juego como PNG (vía RenderTexture)
                    var tx = Resources.Load<Texture2D>(arg);
                    if (tx == null) { plugin.Log("  no existe " + arg); break; }
                    var rt = RenderTexture.GetTemporary(tx.width, tx.height, 0, RenderTextureFormat.ARGB32);
                    Graphics.Blit(tx, rt);
                    RenderTexture.active = rt;
                    var copia = new Texture2D(tx.width, tx.height, TextureFormat.ARGB32, false);
                    copia.ReadPixels(new Rect(0, 0, tx.width, tx.height), 0, 0); copia.Apply();
                    RenderTexture.active = null; RenderTexture.ReleaseTemporary(rt);
                    Directory.CreateDirectory(Path.Combine(carpetaCapturas, "texturas"));
                    string ruta = Path.Combine(Path.Combine(carpetaCapturas, "texturas"), arg.Replace('/', '_') + ".png");
                    File.WriteAllBytes(ruta, copia.EncodeToPNG());
                    plugin.Log("  " + arg + " " + tx.width + "x" + tx.height + " → " + ruta);
                    break;
                }
                case "armainfo":
                {
                    // armainfo <nombre del arma>: valores de su WeaponBehavior y de su malla
                    foreach (var w in Resources.FindObjectsOfTypeAll<WeaponBehavior>())
                    {
                        if (w.name != arg || !w.gameObject.scene.IsValid()) continue;
                        plugin.Log("  daño=" + w.damage + " proyectiles=" + w.projectileCount + " alcance=" + w.range + " dispersión=" + w.shotSpread + " cadencia=" + w.fireRate + " recarga=" + w.reloadTime + " cargador=" + w.bulletsPerClip + " porRecarga=" + w.bulletsToReload + " activo=" + w.gameObject.activeSelf + " capa=" + w.gameObject.layer);
                        foreach (var smr in w.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                            plugin.Log("  malla " + smr.name + " legible=" + (smr.sharedMesh != null && smr.sharedMesh.isReadable) + " vértices=" + (smr.sharedMesh != null ? smr.sharedMesh.vertexCount : 0) + " huesos=" + smr.bones.Length + " material=" + (smr.sharedMaterial != null ? smr.sharedMaterial.name + "/" + smr.sharedMaterial.shader.name : "-") + " capa=" + smr.gameObject.layer);
                        foreach (var an in w.GetComponentsInChildren<Animation>(true)) { var sb = new System.Text.StringBuilder("  animación " + an.name + ":"); foreach (AnimationState st in an) sb.Append(' ').Append(st.name); plugin.Log(sb.ToString()); }
                        break;
                    }
                    break;
                }
                case "lienzo":
                {
                    // lienzo <nombre> on|off: muestra u oculta una pantalla de la interfaz (para capturas)
                    var pa = arg.Split(' ');
                    foreach (var g in Resources.FindObjectsOfTypeAll<GameObject>())
                        if (g.name == pa[0] && g.scene.IsValid()) { g.SetActive(pa.Length < 2 || pa[1] != "off"); plugin.Log("  " + g.name + " activo=" + g.activeSelf); }
                    break;
                }
                case "rueda":
                {
                    // rueda <objeto> <n>: simula girar la rueda del ratón sobre un elemento (n negativo = bajar)
                    var pa = arg.Split(' ');
                    foreach (var g in Resources.FindObjectsOfTypeAll<GameObject>())
                        if (g.name == pa[0] && g.scene.IsValid())
                        {
                            var ev = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current) { scrollDelta = new Vector2(0f, float.Parse(pa[1])) };
                            UnityEngine.EventSystems.ExecuteEvents.ExecuteHierarchy(g, ev, UnityEngine.EventSystems.ExecuteEvents.scrollHandler);
                            var sr = g.GetComponentInParent<UnityEngine.UI.ScrollRect>();
                            plugin.Log("  rueda " + pa[1] + " → posición " + (sr != null ? sr.verticalNormalizedPosition.ToString("0.00") : "?"));
                            break;
                        }
                    break;
                }
                case "pasar":
                {
                    // pasar <objeto>: simula el ratón encima (PointerEnter) de un elemento de la interfaz
                    foreach (var g in Resources.FindObjectsOfTypeAll<GameObject>())
                        if (g.name == arg && g.scene.IsValid())
                        {
                            UnityEngine.EventSystems.ExecuteEvents.Execute(g, new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current), UnityEngine.EventSystems.ExecuteEvents.pointerEnterHandler);
                            plugin.Log("  ratón sobre " + g.name);
                            break;
                        }
                    break;
                }
                case "kit": if (arg == "dar") plugin.kit.OrdenDar(); plugin.Log("  " + plugin.kit.Describir()); break;
                case "perro":
                    if (arg == "libre") plugin.perro.OrdenLiberar();
                    else if (arg == "companero") plugin.perro.OrdenCompanero();
                    plugin.Log("  " + plugin.perro.Describir());
                    break;
                case "libros": plugin.Log("  " + LibrosTienda.Describir()); break;
                case "comprarlibro":
                {
                    // comprarlibro <n>: compra el libro n (0 = curación … 6 = ojo de bruja) como desde la tienda
                    int n = int.Parse(arg);
                    plugin.Log("  " + LibrosTienda.Comprar(plugin, LibrosTienda.Lista[n]));
                    break;
                }
                case "potenciados": plugin.mundo.forzarPotenciado = arg != "off"; plugin.Log("  todos potenciados: " + plugin.mundo.forzarPotenciado); break;
                case "municion":
                {
                    var g = HutongGames.PlayMaker.FsmVariables.GlobalVariables;
                    var pw = UnityEngine.Object.FindObjectOfType<PlayerWeapons>();
                    var sb = new System.Text.StringBuilder("  G=" + g.FindFsmInt("silver_bullets_v2").Value + " bullets_left=" + g.FindFsmInt("bullets_left").Value + " musket=" + g.FindFsmInt("CurrentMusketAmmo").Value);
                    if (pw != null)
                    {
                        sb.Append(" actual=" + pw.currentWeapon);
                        for (int k = 0; k < pw.weaponOrder.Length; k++)
                        {
                            var wb = pw.weaponOrder[k] == null ? null : pw.weaponOrder[k].GetComponent<WeaponBehavior>();
                            if (wb != null) sb.Append(" | " + k + " " + pw.weaponOrder[k].name + " ammo=" + wb.ammo + " cargador=" + wb.bulletsLeft + "/" + wb.bulletsPerClip + " max=" + wb.maxAmmo);
                        }
                    }
                    plugin.Log(sb.ToString());
                    break;
                }
                case "setglobal":
                {
                    var pa = arg.Split(' ');
                    var v = HutongGames.PlayMaker.FsmVariables.GlobalVariables.FindFsmInt(pa[0]);
                    if (v != null) { v.Value = int.Parse(pa[1]); plugin.Log("  " + pa[0] + " = " + v.Value); }
                    else { var bv = HutongGames.PlayMaker.FsmVariables.GlobalVariables.FindFsmBool(pa[0]); if (bv != null) { bv.Value = pa[1] == "1" || pa[1] == "true"; plugin.Log("  " + pa[0] + " = " + bv.Value); } }
                    break;
                }
                case "camara":
                {
                    foreach (var c in Resources.FindObjectsOfTypeAll<Camera>())
                    {
                        if (c.name != arg || !c.gameObject.scene.IsValid()) continue;
                        plugin.Log("  " + c.name + " activa=" + c.isActiveAndEnabled + " orto=" + c.orthographic + " tam=" + c.orthographicSize + " fov=" + c.fieldOfView + " pos=" + c.transform.position + " rot=" + c.transform.eulerAngles + " textura=" + (c.targetTexture != null ? c.targetTexture.name + " " + c.targetTexture.width + "x" + c.targetTexture.height : "-") + " capas=" + c.cullingMask + " lejos=" + c.farClipPlane);
                    }
                    break;
                }
                case "cofres": plugin.Log("  " + plugin.mundo.DescribirCofres()); break;
                case "forzar": plugin.mundo.OrdenForzar(int.Parse(arg)); break;
                case "ganzuas": plugin.Log("  " + plugin.tienda.ComprarGanzuas(int.Parse(arg))); break;
                case "abrircofre": plugin.mundo.OrdenAbrirCofre(int.Parse(arg)); break;
                case "objinfo":
                case "mover":
                case "girarobj":
                {
                    // objinfo <nombre> | mover <nombre> dx dy dz | girarobj <nombre> ex ey ez (se suma al giro)
                    var partes = arg.Split(' ');
                    int nn = orden == "objinfo" ? partes.Length : partes.Length - 3;
                    string nom = string.Join(" ", partes, 0, Mathf.Max(1, nn));
                    GameObject go = GameObject.Find(nom);
                    if (go == null) { plugin.Log("  no existe " + nom); break; }
                    if (orden != "objinfo")
                    {
                        var ci = System.Globalization.CultureInfo.InvariantCulture;
                        var v = new Vector3(float.Parse(partes[nn], ci), float.Parse(partes[nn + 1], ci), float.Parse(partes[nn + 2], ci));
                        if (orden == "mover") go.transform.position += v; else go.transform.rotation = go.transform.rotation * Quaternion.Euler(v);
                    }
                    Bounds bb = new Bounds(go.transform.position, Vector3.zero); bool hay = false;
                    foreach (var r in go.GetComponentsInChildren<Renderer>()) { if (!hay) { bb = r.bounds; hay = true; } else bb.Encapsulate(r.bounds); }
                    plugin.Log("  " + nom + " pos=" + go.transform.position.ToString("F2") + " giro=" + go.transform.eulerAngles.ToString("F0") + " caja min=" + bb.min.ToString("F2") + " max=" + bb.max.ToString("F2"));
                    break;
                }
                case "casapiezas": plugin.Log("  " + plugin.mundo.PiezasCasa()); break;
                case "cabanas": plugin.Log("  " + plugin.mundo.DescribirCabanas()); break;
                case "forzarcabana": plugin.mundo.OrdenForzarCabana(int.Parse(arg)); break;
                case "abrircabana": plugin.mundo.OrdenAbrirCabana(int.Parse(arg)); break;
                case "ircabana":
                {
                    var pc = plugin.mundo.PosCabana(int.Parse(arg));
                    var j = UnityEngine.Object.FindObjectOfType<FPSPlayer>();
                    if (pc != null && j != null) j.transform.position = Anclaje.Origen() + pc.Value + Vector3.up * 0.5f;
                    break;
                }
                case "minas": plugin.Log("  " + plugin.mundo.DescribirMinas()); break;
                case "tomarballesta": plugin.mundo.OrdenTomarBallesta(int.Parse(arg)); break;
                case "minaentrar": { var jm = UnityEngine.Object.FindObjectOfType<FPSPlayer>(); if (jm != null) plugin.mundo.OrdenMinaTeleport(jm, int.Parse(arg), true); break; }
                case "minasalir": { var jm = UnityEngine.Object.FindObjectOfType<FPSPlayer>(); if (jm != null) plugin.mundo.OrdenMinaTeleport(jm, int.Parse(arg), false); break; }
                case "irlaberinto": { var jl = UnityEngine.Object.FindObjectOfType<FPSPlayer>(); var pl = plugin.mundo.PosLaberinto(arg); if (jl != null && pl.HasValue) jl.transform.position = Anclaje.Origen() + pl.Value + Vector3.up * 2.2f; break; }
                case "ircastillo": { var jc = UnityEngine.Object.FindObjectOfType<FPSPlayer>(); var pc2 = plugin.mundo.PosCastillo(arg.Length > 0 ? float.Parse(arg, System.Globalization.CultureInfo.InvariantCulture) : -30f); if (jc != null && pc2.HasValue) jc.transform.position = Anclaje.Origen() + pc2.Value + Vector3.up * 2.2f; break; }
                case "irmina":
                {
                    // irmina n [0 boca | 1 dentro | 2 ballesta]
                    var pa = arg.Split(' ');
                    var pm = plugin.mundo.PosMina(int.Parse(pa[0]), pa.Length > 1 ? int.Parse(pa[1]) : 0);
                    var j = UnityEngine.Object.FindObjectOfType<FPSPlayer>();
                    if (pm != null && j != null) j.transform.position = Anclaje.Origen() + pm.Value + Vector3.up * 1f;   // 1 m arriba: el jugador se ubica por su centro y, a ras de suelo, queda metido en el terreno
                    break;
                }
                case "fps": if (arg.Length > 0) plugin.pantalla.CambiarFps(int.Parse(arg)); plugin.Log("  límite " + Pantalla.Fps + " (target " + Application.targetFrameRate + ", vsync " + QualitySettings.vSyncCount + ")"); break;
                case "arboles": plugin.mundo.OrdenArboles(); break;
                case "opciones":
                {
                    // opciones | opciones <distancia|densidad|sombras|escala|dificultad|eventos|texto|volumen> <n> | opciones contador|nombrecomp 0/1 | opciones abrir [pestaña]
                    var pa = arg.Split(' ');
                    int v = pa.Length > 1 ? int.Parse(pa[1]) : 0;
                    switch (pa[0])
                    {
                        case "distancia": Opciones.Distancia = v; break;
                        case "densidad": Opciones.Densidad = v; break;
                        case "sombras": Opciones.Sombras = v; break;
                        case "escala": Opciones.EscalaPct = v; break;
                        case "dificultad": Opciones.Dificultad = v; break;
                        case "eventos": Opciones.Eventos = v; break;
                        case "texto": Opciones.Texto = v; break;
                        case "volumen": Opciones.VolumenEfectos = v; break;
                        case "contador": Opciones.ContadorFps = v == 1; break;
                        case "nombrecomp": Opciones.NombreCompanero = v == 1; break;
                        case "abrir": plugin.pantalla.Abrir(); plugin.pantalla.Pestana = v; break;
                        case "cerrar": plugin.pantalla.OrdenCerrar(); break;
                    }
                    plugin.Log("  " + Opciones.Describir());
                    break;
                }
                case "probartodos": plugin.StartCoroutine(ProbarTodos(arg.Length > 0 ? int.Parse(arg) : 24)); break;   // probartodos [lote]: suena CADA sonido cargado junto a la cámara y anota los que fallan (2026-11-20)
                case "sonidoscomp": plugin.Log("  sonidos del compañero: " + SonidosCompanero.Informe()); break;   // (2026-11-20)
                case "voces":   // voces <reales> <virtuales>: cambia en vivo el límite de voces de Unity (reinicia el audio) (2026-11-19)
                {
                    var pa = arg.Split(' '); var cfg = AudioSettings.GetConfiguration();
                    if (pa.Length >= 2) { cfg.numRealVoices = int.Parse(pa[0]); cfg.numVirtualVoices = int.Parse(pa[1]); bool ok = AudioSettings.Reset(cfg); plugin.Log("  reinicio del audio " + (ok ? "OK" : "FALLÓ")); }
                    var c2 = AudioSettings.GetConfiguration();
                    plugin.Log("  voces reales " + c2.numRealVoices + ", virtuales " + c2.numVirtualVoices);
                    break;
                }
                case "probarsonido":   // probarsonido: suena el disparo del arma actual junto a la cámara (prioridad 0 y 128) y dice si Unity lo descartó
                {
                    var pw = Rendimiento.Armas; AudioClip clip = null;
                    if (pw != null && pw.currentWeapon >= 0 && pw.currentWeapon < pw.weaponOrder.Length && pw.weaponOrder[pw.currentWeapon] != null)
                    { var wb = pw.weaponOrder[pw.currentWeapon].GetComponent<WeaponBehavior>(); if (wb != null) clip = wb.fireSnd; }
                    if (clip == null) clip = Sonidos.Clip("jefe_roca_impacto");
                    var cam = Camera.main; if (cam == null || clip == null) { plugin.Log("  sin cámara o sin clip"); break; }
                    var fuentes = new List<AudioSource>();
                    foreach (int prio in new[] { 0, 128 })
                    {
                        var g = new GameObject("coop_prueba_sonido_" + prio); g.transform.position = cam.transform.position;
                        var a = g.AddComponent<AudioSource>(); a.clip = clip; a.spatialBlend = 0f; a.priority = prio; a.volume = 1f; a.Play();
                        fuentes.Add(a); UnityEngine.Object.Destroy(g, clip.length + 0.5f);
                    }
                    plugin.StartCoroutine(InformarPrueba(fuentes, clip.name));
                    break;
                }
                case "oyente": plugin.Log("  " + Oyente.Informe()); break;   // oyentes de audio, cámara principal y ajustes de sonido (2026-11-19)
                case "audioinfo":   // audioinfo <filtro raíz> [n]: fuentes de sonido SONANDO de esos objetos, agrupadas por clip, con sus ajustes (2026-11-19)
                {
                    var pa = arg.Split(' '); string fil = pa[0]; int tope = pa.Length > 1 ? int.Parse(pa[1]) : 6;
                    var jj = UnityEngine.Object.FindObjectOfType<FPSPlayer>();
                    var grupos = new Dictionary<string, List<AudioSource>>();
                    foreach (var a in UnityEngine.Object.FindObjectsOfType<AudioSource>())
                    {
                        if (a == null || !a.isPlaying || a.transform.root.name.IndexOf(fil, StringComparison.OrdinalIgnoreCase) < 0) continue;
                        string k = a.clip != null ? a.clip.name : "(sin clip)";
                        List<AudioSource> l; if (!grupos.TryGetValue(k, out l)) grupos[k] = l = new List<AudioSource>(); l.Add(a);
                    }
                    int i = 0;
                    foreach (var kv in grupos)
                    {
                        if (i++ >= tope) break;
                        var a = kv.Value[0]; float d = jj != null ? Vector3.Distance(jj.transform.position, a.transform.position) : -1f;
                        plugin.Log("  clip '" + kv.Key + "' ×" + kv.Value.Count + " | ej. " + RutaDe(a.transform, null) + " | loop=" + a.loop + " playOnAwake=" + a.playOnAwake + " 3D=" + a.spatialBlend.ToString("0.00")
                            + " vol=" + a.volume.ToString("0.00") + " min=" + a.minDistance.ToString("0") + " max=" + a.maxDistance.ToString("0") + " rolloff=" + a.rolloffMode + " prio=" + a.priority
                            + " largo=" + (a.clip != null ? a.clip.length.ToString("0.0") : "-") + " s, dist=" + d.ToString("0") + " m, mute=" + a.mute + " virtual=" + a.isVirtual);
                    }
                    var cfg = AudioSettings.GetConfiguration();
                    plugin.Log("  voces reales " + cfg.numRealVoices + ", virtuales " + cfg.numVirtualVoices + "; grupos " + grupos.Count + "; bucles lejanos gestionados " + SonidosLejanos.Gestionados + ", pausados " + SonidosLejanos.Pausados);
                    break;
                }
                case "desglose":   // desglose [n]: agentes, cuerpos con física, sonidos y máquinas agrupados por objeto raíz (2026-11-19: el invitado tenía 10× agentes)
                {
                    int tope = arg.Length > 0 ? int.Parse(arg) : 10;
                    System.Func<Transform, string> nombre = t =>
                    {
                        string nn = t.root.name.Replace("(Clone)", "");
                        nn = System.Text.RegularExpressions.Regex.Replace(nn, @"\s*\(\d+\)$", "");
                        nn = System.Text.RegularExpressions.Regex.Replace(nn, @"[_ ]?\d+$", "");
                        return nn.Trim();
                    };
                    System.Action<string, IEnumerable<Component>> agrupar = (titulo, comps) =>
                    {
                        var cuenta = new Dictionary<string, int>(); var activos = new Dictionary<string, int>(); int total = 0;
                        foreach (var c in comps)
                        {
                            if (c == null) continue; total++;
                            string k = nombre(c.transform);
                            int v; cuenta.TryGetValue(k, out v); cuenta[k] = v + 1;
                            bool on = c is Behaviour ? ((Behaviour)c).isActiveAndEnabled : c.gameObject.activeInHierarchy;
                            if (c is AudioSource) on = ((AudioSource)c).isPlaying;
                            if (on) { activos.TryGetValue(k, out v); activos[k] = v + 1; }
                        }
                        var lista = new List<KeyValuePair<string, int>>(cuenta); lista.Sort((a, b) => b.Value.CompareTo(a.Value));
                        var sb = new System.Text.StringBuilder("  " + titulo + " " + total + ": ");
                        for (int i = 0; i < lista.Count && i < tope; i++) { int a2; activos.TryGetValue(lista[i].Key, out a2); sb.Append(lista[i].Key).Append(" ").Append(lista[i].Value).Append("/").Append(a2).Append("; "); }
                        plugin.Log(sb.ToString());
                    };
                    agrupar("agentes (total/encendidos)", UnityEngine.Object.FindObjectsOfType<UnityEngine.AI.NavMeshAgent>());
                    agrupar("rigidbodies (total/activos)", UnityEngine.Object.FindObjectsOfType<Rigidbody>());
                    agrupar("audio (total/sonando)", UnityEngine.Object.FindObjectsOfType<AudioSource>());
                    agrupar("FSM (total/encendidas)", UnityEngine.Object.FindObjectsOfType<PlayMakerFSM>());
                    agrupar("MonoBehaviour (total/encendidos)", UnityEngine.Object.FindObjectsOfType<MonoBehaviour>());
                    break;
                }
                case "contar":
                {
                    int rn = 0, rl = 0, lz = 0, lzm = 0, co = 0, rb = 0, na = 0, fsm = 0, fsmAct = 0, au = 0, ps = 0, mb = 0;
                    foreach (var r in UnityEngine.Object.FindObjectsOfType<Renderer>()) { rn++; if (r is ParticleSystemRenderer) ps++; }
                    foreach (var l in UnityEngine.Object.FindObjectsOfType<Light>()) { if (!l.enabled) continue; lz++; if (l.type == LightType.Point || l.type == LightType.Spot) lzm++; }
                    co = UnityEngine.Object.FindObjectsOfType<Collider>().Length; rb = UnityEngine.Object.FindObjectsOfType<Rigidbody>().Length;
                    na = UnityEngine.Object.FindObjectsOfType<UnityEngine.AI.NavMeshAgent>().Length;
                    foreach (var f in UnityEngine.Object.FindObjectsOfType<PlayMakerFSM>()) { fsm++; if (f.Fsm != null && f.Fsm.Active) fsmAct++; }
                    au = UnityEngine.Object.FindObjectsOfType<AudioSource>().Length; mb = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>().Length;
                    plugin.Log("  activos: renderers " + rn + " (partículas " + ps + "), luces " + lz + " (puntuales/focos " + lzm + "), colliders " + co + ", rigidbodies " + rb + ", agentes " + na + ", FSM " + fsm + " (activas " + fsmAct + "), audio " + au + ", MonoBehaviour " + mb + ", timeScale " + Time.timeScale + ", pixelLightCount " + QualitySettings.pixelLightCount);
                    break;
                }
                case "llamas":   // llamas [n]: las llamas más cercanas con sus capas (partículas vivas, reproduciendo, tamaño de inicio)
                {
                    var jl = UnityEngine.Object.FindObjectOfType<FPSPlayer>(); if (jl == null) break;
                    var lista = new List<KeyValuePair<float, Transform>>();
                    foreach (var t in UnityEngine.Object.FindObjectsOfType<Transform>()) if (t.name == "coop_llama") lista.Add(new KeyValuePair<float, Transform>(Vector3.Distance(t.position, jl.transform.position), t));
                    lista.Sort((x, y) => x.Key.CompareTo(y.Key));
                    int nl = arg.Length > 0 ? int.Parse(arg) : 4;
                    for (int i = 0; i < Mathf.Min(nl, lista.Count); i++)
                    {
                        var sb = new System.Text.StringBuilder("  llama a " + lista[i].Key.ToString("0.0") + " m en " + (lista[i].Value.position - Anclaje.Origen()).ToString("F1") + " escala " + lista[i].Value.lossyScale.x.ToString("0.00") + ":");
                        foreach (var ps in lista[i].Value.GetComponentsInChildren<ParticleSystem>(true))
                            sb.Append(" " + ps.name + "(vivas " + ps.particleCount + ", juega " + ps.isPlaying + ", tam " + ps.main.startSize.constantMin.ToString("0.00") + "-" + ps.main.startSize.constantMax.ToString("0.00") + ", activo " + ps.gameObject.activeInHierarchy + ", rend " + ps.GetComponent<ParticleSystemRenderer>().enabled + ")");
                        plugin.Log(sb.ToString());
                        if (i == 0)
                        {
                            var ps0 = lista[i].Value.GetComponentInChildren<ParticleSystem>();
                            var arr = new ParticleSystem.Particle[ps0.main.maxParticles]; int np = ps0.GetParticles(arr);
                            var sp = new System.Text.StringBuilder("   partículas de " + ps0.name + ":");
                            for (int k = 0; k < Mathf.Min(5, np); k++) sp.Append(" (" + (arr[k].position - lista[i].Value.position).ToString("F2") + " tam " + arr[k].GetCurrentSize(ps0).ToString("0.00") + " color " + arr[k].GetCurrentColor(ps0) + ")");
                            var rr = ps0.GetComponent<ParticleSystemRenderer>();
                            sp.Append(" | material " + (rr.sharedMaterial != null ? rr.sharedMaterial.name + "/" + rr.sharedMaterial.shader.name + " tex " + (rr.sharedMaterial.mainTexture != null) : "NINGUNO") + " límites " + rr.bounds + " capa " + ps0.gameObject.layer + " espacio " + ps0.main.simulationSpace);
                            plugin.Log(sp.ToString());
                        }
                    }
                    plugin.Log("  " + lista.Count + " llamas en la escena");
                    break;
                }
                case "perimetro":
                {
                    // perimetro [paso]: recorre el borde del mapa (terreno viejo + terreno nuevo) y busca tramos SIN pared ni terreno que impidan salir
                    float paso = arg.Length > 0 ? float.Parse(arg, System.Globalization.CultureInfo.InvariantCulture) : 40f;
                    Terrain viejo = null, nuevoT = null;
                    foreach (var t0 in Terrain.activeTerrains) { if (t0.name == "Coop Terreno Norte") nuevoT = t0; else if (viejo == null || t0.terrainData.size.x > viejo.terrainData.size.x) viejo = t0; }
                    if (viejo == null) { plugin.Log("  sin terreno"); break; }
                    Vector3 o0 = viejo.transform.position, s0 = viejo.terrainData.size;
                    var poly = new System.Collections.Generic.List<Vector2>();
                    poly.Add(new Vector2(o0.x, o0.z)); poly.Add(new Vector2(o0.x + s0.x, o0.z));
                    if (nuevoT != null)
                    {
                        Vector3 on = nuevoT.transform.position, sn = nuevoT.terrainData.size;
                        poly.Add(new Vector2(o0.x + s0.x, on.z)); poly.Add(new Vector2(on.x + sn.x, on.z)); poly.Add(new Vector2(on.x + sn.x, on.z + sn.z)); poly.Add(new Vector2(on.x, on.z + sn.z)); poly.Add(new Vector2(on.x, o0.z + s0.z)); poly.Add(new Vector2(o0.x, o0.z + s0.z));
                    }
                    else { poly.Add(new Vector2(o0.x + s0.x, o0.z + s0.z)); poly.Add(new Vector2(o0.x, o0.z + s0.z)); }
                    int total = 0, huecos = 0; var sb = new System.Text.StringBuilder(); var limites = new System.Collections.Generic.Dictionary<string, int>();
                    for (int i = 0; i < poly.Count; i++)
                    {
                        Vector2 a2 = poly[i], b2 = poly[(i + 1) % poly.Count]; Vector2 dir = (b2 - a2).normalized; float len = Vector2.Distance(a2, b2);
                        Vector2 fuera = new Vector2(dir.y, -dir.x);   // normal hacia afuera (sentido horario)
                        if (poly.Count > 0) { Vector2 mid = (a2 + b2) / 2f + fuera * 5f; }
                        for (float d = 2f; d < len; d += paso)
                        {
                            Vector2 q = a2 + dir * d; total++;
                            // desde el borde del terreno hacia ADENTRO: la primera pared (o ladera) que se cruza es el límite real; si no hay ninguna, es un hueco
                            Vector2 dentro = q + (-fuera) * 1f;
                            float h = AlturaMax(dentro);
                            var origen = new Vector3(dentro.x, h + 12f, dentro.y);
                            RaycastHit rh;
                            bool golpe = Physics.Raycast(origen, new Vector3(-fuera.x, 0f, -fuera.y), out rh, 1500f, -1, QueryTriggerInteraction.Ignore);
                            if (golpe) { string nm = rh.collider.name + (rh.distance < 60f ? " (a <60 m)" : ""); int cn; limites.TryGetValue(nm, out cn); limites[nm] = cn + 1; }
                            else { huecos++; sb.Append("(" + Mathf.RoundToInt(q.x - Anclaje.Origen().x) + "," + Mathf.RoundToInt(q.y - Anclaje.Origen().z) + ") "); }
                        }
                    }
                    foreach (var kv in limites) plugin.Log("   límite " + kv.Key + ": " + kv.Value + " puntos");
                    plugin.Log("  perímetro: " + total + " puntos revisados, " + huecos + " sin pared/terreno" + (huecos > 0 ? ": " + sb : ""));
                    break;
                }
                case "castillovis": plugin.mundo.ForzarCastillo = arg.Length > 0 ? int.Parse(arg) : -1; break;
                case "experimento":
                {
                    // experimento <fisica|particulas|luces|vista|audio> <0|1>: apaga/enciende algo para ver cuánto pesa (solo pruebas)
                    var pe = arg.Split(' '); bool on = pe.Length > 1 && pe[1] == "1";
                    switch (pe[0])
                    {
                        case "fisica": Time.fixedDeltaTime = on ? 0.02f : 0.5f; break;
                        case "particulas": foreach (var s2 in UnityEngine.Object.FindObjectsOfType<ParticleSystem>()) { var em = s2.emission; em.enabled = on; } break;
                        case "luces": foreach (var l2 in UnityEngine.Object.FindObjectsOfType<Light>()) if (l2.type != LightType.Directional) l2.enabled = on; break;
                        case "vista": if (Camera.main != null) Camera.main.cullingMask = on ? -1 : 0; break;
                        case "audio": foreach (var a2 in UnityEngine.Object.FindObjectsOfType<AudioSource>()) a2.mute = !on; break;
                        case "fsm": foreach (var f2 in UnityEngine.Object.FindObjectsOfType<PlayMakerFSM>()) if (f2.Fsm != null && f2.Fsm.Active && f2.gameObject.GetComponentInChildren<TakeDamage>() == null && f2.name != "Canvas_RFPS(Clone)") f2.enabled = on; break;
                    }
                    plugin.Log("  experimento " + arg);
                    break;
                }
                case "congelar": plugin.mundo.ForzarCongelar = arg.Length > 0 ? int.Parse(arg) : -1; plugin.Log("  congelados ahora: monstruos " + plugin.mundo.CongeladosAhora + ", máquinas " + plugin.mundo.FsmCongeladasAhora + ", dormidas " + plugin.mundo.FsmDormidasAhora); break;
                case "fsmtop":
                {
                    var cuenta = new Dictionary<string, int>(); var lejos = new Dictionary<string, int>();
                    var jj = UnityEngine.Object.FindObjectOfType<FPSPlayer>(); Vector3 pj = jj != null ? jj.transform.position : Vector3.zero;
                    foreach (var f in UnityEngine.Object.FindObjectsOfType<PlayMakerFSM>())
                    {
                        if (f.Fsm == null || !f.Fsm.Active) continue;
                        string k = Catalogo.BaseDe(f.gameObject.name) + " / " + f.FsmName;
                        int n; cuenta.TryGetValue(k, out n); cuenta[k] = n + 1;
                        if (Vector3.Distance(f.transform.position, pj) > 170f) { lejos.TryGetValue(k, out n); lejos[k] = n + 1; }
                    }
                    var l = new List<KeyValuePair<string, int>>(cuenta); l.Sort((x, y) => y.Value.CompareTo(x.Value));
                    int tot = 0, totL = 0; foreach (var kv in cuenta) tot += kv.Value; foreach (var kv in lejos) totL += kv.Value;
                    plugin.Log("  FSM activas " + tot + ", a más de 170 m del jugador " + totL);
                    for (int i = 0; i < Mathf.Min(25, l.Count); i++) { int nl; lejos.TryGetValue(l[i].Key, out nl); plugin.Log("   " + l[i].Value + " (lejos " + nl + ")  " + l[i].Key); }
                    break;
                }
                case "medir": Opciones.Medir(arg.Length > 0 ? float.Parse(arg, System.Globalization.CultureInfo.InvariantCulture) : 10f); break;
                case "rendimiento": plugin.Log("  " + Rendimiento.Informe()); break;   // desde la última vez
                case "huellas": plugin.Log("  " + plugin.mundo.DescribirHuellas()); break;
                case "casas": plugin.Log("  " + plugin.mundo.DescribirCasas()); break;
                case "tomarescopeta": plugin.mundo.OrdenTomarEscopeta(int.Parse(arg)); break;
                case "ircasa":
                {
                    var pc = plugin.mundo.PosCasa(int.Parse(arg));
                    var j = UnityEngine.Object.FindObjectOfType<FPSPlayer>();
                    if (pc != null && j != null) j.transform.position = Anclaje.Origen() + pc.Value + Vector3.up * 1.2f + Vector3.right * 1.2f;
                    break;
                }
                case "poscofre": { var pq = plugin.mundo.PosCofre(int.Parse(arg)); plugin.Log("  cofre en " + (pq.HasValue ? pq.Value.ToString("F1") : "—")); break; }
                case "ircofre":
                {
                    var pc = plugin.mundo.PosCofre(int.Parse(arg));
                    var j = UnityEngine.Object.FindObjectOfType<FPSPlayer>();
                    if (pc != null && j != null) j.transform.position = Anclaje.Origen() + pc.Value + Vector3.up * 1.5f + Vector3.right * 1.5f;
                    break;
                }
                case "oyentes":
                {
                    // oyentes <evento>: FSM que reaccionan a un evento global
                    foreach (var f in Resources.FindObjectsOfTypeAll<PlayMakerFSM>())
                    {
                        if (f.Fsm == null || !f.gameObject.scene.IsValid()) continue;
                        foreach (var tr in f.FsmGlobalTransitions)
                            if (tr.EventName == arg) plugin.Log("  " + RutaDe(f.transform, null) + "/" + f.FsmName + " → " + tr.ToState + " activo=" + f.isActiveAndEnabled);
                    }
                    break;
                }
                case "foto": Catalogo.Foto(plugin, arg); break;   // foto x z alto [nombre]: vista aérea con la cámara de catálogo   // catalogo [filtro]: foto de cada tipo de estructura
                case "bot":
                {
                    float seg;
                    BotRecto = arg.StartsWith("recto");
                    if (BotRecto) arg = arg.Substring(5).Trim();
                    if (float.TryParse(arg, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out seg))
                    { Bot = true; BotHasta = Time.time + seg; plugin.Log("  bot durante " + seg + " s"); }
                    else { Bot = arg != "off"; BotHasta = 0f; plugin.Log("  bot " + (Bot ? "activado" : "desactivado")); }
                    break;
                }
                case "inclinar": plugin.OrdenInclinar(float.Parse(arg, System.Globalization.CultureInfo.InvariantCulture)); break;
                case "seguir": plugin.Seguir = arg != "off"; plugin.Log("  seguir " + plugin.Seguir); break;
                case "junto": plugin.OrdenJunto(); break;
                case "estado": plugin.Log("  " + plugin.Estado()); break;
                case "suelo":
                {
                    var jf = UnityEngine.Object.FindObjectOfType<FPSPlayer>(); if (jf == null) break;
                    Vector3 c0 = jf.transform.position; var cc = jf.GetComponent<CharacterController>(); if (cc != null) plugin.Log("  controlador: pendiente " + cc.slopeLimit + "° escalón " + cc.stepOffset + " m"); var sb = new System.Text.StringBuilder();
                    for (int dz = -40; dz <= 40; dz += 20) { for (int dx = -60; dx <= 60; dx += 20) {
                        RaycastHit[] hs = Physics.RaycastAll(c0 + new Vector3(dx, 300f, dz), Vector3.down, 700f);
                        System.Array.Sort(hs, (a, b) => a.distance.CompareTo(b.distance));
                        sb.Append(" [" + dx + "," + dz + "]"); foreach (var h in hs) sb.Append(" " + h.collider.name + "@" + h.point.y.ToString("F1")); }
                        plugin.Log("  " + sb); sb.Length = 0; }
                    break;
                }
                case "mirar": plugin.OrdenMirar(); break;
                case "fsms": ListarFsms(arg); break;
                case "fsm": DetalleFsm(arg); break;
                case "fsmevento":   // fsmevento <evento>: máquinas (también apagadas) que reciben ese evento
                {
                    foreach (var f in Resources.FindObjectsOfTypeAll<PlayMakerFSM>())
                    {
                        if (!f.gameObject.scene.IsValid() || f.Fsm == null) continue;
                        bool si = false;
                        if (f.Fsm.GlobalTransitions != null) foreach (var t0 in f.Fsm.GlobalTransitions) if (t0 != null && t0.EventName == arg) si = true;
                        foreach (var st in f.FsmStates) foreach (var t1 in st.Transitions) if (t1.EventName == arg) si = true;
                        if (!si) continue;
                        var ruta = f.gameObject.name; for (var x = f.transform.parent; x != null; x = x.parent) ruta = x.name + "/" + ruta;
                        plugin.Log("  " + ruta + " :: " + f.FsmName + " activo=" + f.gameObject.activeInHierarchy + " estado='" + f.ActiveStateName + "'");
                    }
                    break;
                }
                case "sonidosfsm":   // sonidosfsm <raíz>: clips de audio de las acciones de las FSM bajo ese objeto (también ocultas)
                {
                    var vistos = new HashSet<string>();
                    foreach (var f in Resources.FindObjectsOfTypeAll<PlayMakerFSM>())
                    {
                        if (!f.gameObject.scene.IsValid() || f.Fsm == null) continue;
                        bool bajo = false; for (var x = f.transform; x != null; x = x.parent) if (x.name == arg) bajo = true;
                        if (!bajo) continue;
                        foreach (var st in f.FsmStates)
                            foreach (var a in st.Actions)
                                foreach (var c in a.GetType().GetFields())
                                {
                                    object v = c.GetValue(a); UnityEngine.Object o = null;
                                    if (v is HutongGames.PlayMaker.FsmObject) o = ((HutongGames.PlayMaker.FsmObject)v).Value; else if (v is UnityEngine.Object) o = (UnityEngine.Object)v;
                                    if (!(o is AudioClip)) continue;
                                    string k = f.gameObject.name + "/" + f.FsmName + "/" + st.Name + " " + a.GetType().Name + " " + o.name;
                                    if (vistos.Add(k)) plugin.Log("  " + k);
                                }
                    }
                    break;
                }
                case "pulsarui":   // pulsarui <objeto>: hace clic en ese botón de la interfaz (también filas del mod)
                {
                    foreach (var b in Resources.FindObjectsOfTypeAll<UnityEngine.UI.Button>())
                        if (b.name == arg && b.gameObject.scene.IsValid()) { b.onClick.Invoke(); plugin.Log("  pulsado " + arg); break; }
                    break;
                }
                case "manobola": plugin.Log("  " + ManoBola.Orden(arg)); break;
                case "poblacion":   // poblacion: enemigos vivos (a < 300 m y en total), cuenta de Crux y copias guardadas por la bola
                {
                    var jj = UnityEngine.Object.FindObjectOfType<FPSPlayer>(); Vector3 pj = jj != null ? jj.transform.position : Vector3.zero;
                    int vivos = 0, cerca = 0, muertos = 0; var tipos = new Dictionary<string, int>();
                    foreach (var td in UnityEngine.Object.FindObjectsOfType<TakeDamage>())
                    {
                        if (td == null || td.GetComponentInParent<FPSPlayer>() != null) continue;
                        if (Eventos.Muerto(td)) { muertos++; continue; }
                        vivos++; var rz = Combate.RaizDe(td); if (Vector3.Distance(rz.position, pj) < 300f) cerca++;
                        string tp = rz.parent != null ? rz.parent.name : rz.name; int c0; tipos.TryGetValue(tp, out c0); tipos[tp] = c0 + 1;
                    }
                    int plantillas = 0; foreach (var g in Resources.FindObjectsOfTypeAll<GameObject>()) if (g.scene.IsValid() && g.name.StartsWith("coop_plantilla_")) plantillas++;
                    var crux = UnityEngine.Object.FindObjectOfType<Crux>();
                    var sbp = new System.Text.StringBuilder("  vivos=" + vivos + " (a <300 m: " + cerca + ") muertos=" + muertos + " plantillas=" + plantillas + (crux != null ? " crux=" + crux._CurrentSpawnedObjects + "/" + crux._MaxObjectsToSpawn + " lista=" + crux._SpawnedObjects.Count : ""));
                    foreach (var kv in tipos) sbp.Append(" | ").Append(kv.Key).Append("×").Append(kv.Value);
                    plugin.Log(sbp.ToString());
                    break;
                }
                case "matartipo":   // matartipo <texto>: mata (como golpe mío) al enemigo vivo más cercano cuyo tipo contenga el texto
                {
                    var jj = UnityEngine.Object.FindObjectOfType<FPSPlayer>(); Vector3 pj = jj != null ? jj.transform.position : Vector3.zero;
                    TakeDamage mejor = null; float dm = float.MaxValue;
                    foreach (var td in UnityEngine.Object.FindObjectsOfType<TakeDamage>())
                    {
                        if (td == null || td.GetComponentInParent<FPSPlayer>() != null || Eventos.Muerto(td)) continue;
                        if (Dificultad.EsJefe(td) || Aliados.Protegido(td.transform)) continue;   // nunca jefes (su muerte lleva a la aldea) ni el perro/cabra
                        var rz = Combate.RaizDe(td); string tp = (rz.parent != null ? rz.parent.name : "") + "/" + rz.name;
                        if (tp.IndexOf(arg, StringComparison.OrdinalIgnoreCase) < 0) continue;
                        float d = Vector3.Distance(rz.position, pj); if (d < dm) { dm = d; mejor = td; }
                    }
                    if (mejor == null) { plugin.Log("  no hay '" + arg + "' vivo"); break; }
                    plugin.Log("  matando " + Combate.NombreDe(mejor) + " a " + Mathf.RoundToInt(dm) + " m");
                    mejor.Damage(99999f, false);   // directo (la zona de la cabeza busca una FSM que no tiene y falla)
                    // el juego marca "muerto" un cuadro después: se hace a mano lo que hace la muerte mía con la bola
                    if (Aliados.Instancia != null) Aliados.Instancia.Tragar(mejor);
                    break;
                }
                case "textosui":   // textosui <filtro>: textos de la interfaz que contengan el filtro (con ruta, color y si está activo)
                {
                    foreach (var t in Resources.FindObjectsOfTypeAll<UnityEngine.UI.Graphic>())
                    {
                        if (!t.gameObject.scene.IsValid()) continue;
                        var tx = t as UnityEngine.UI.Text; string txt = tx != null ? tx.text : "";
                        string ruta = t.name; for (var x = t.transform.parent; x != null; x = x.parent) ruta = x.name + "/" + ruta;
                        if (ruta.IndexOf(arg, StringComparison.OrdinalIgnoreCase) < 0 && txt.IndexOf(arg, StringComparison.OrdinalIgnoreCase) < 0) continue;
                        var cr = t.GetComponent<CanvasRenderer>();
                        plugin.Log("  " + ruta + " '" + txt + "' color=" + t.color + " alfaCR=" + (cr != null ? cr.GetAlpha().ToString("0.00") : "-") + " activo=" + t.gameObject.activeInHierarchy);
                    }
                    break;
                }
                case "huesos":   // huesos <objeto raíz>: huesos de su malla con piel
                {
                    var go = GameObject.Find(arg); plugin.Log(go != null ? "  " + PoseCuerpo.Huesos(go) : "  no encontré " + arg); break;
                }
                case "sentar":   // sentar <hueso> ex ey ez | sentar limpiar: pose de los mineros de las minas
                {
                    var pa = arg.Split(' '); var ci = System.Globalization.CultureInfo.InvariantCulture;
                    if (pa[0] == "limpiar") PoseCuerpo.Sentado.Clear();
                    else { int n = pa.Length; PoseCuerpo.Sentado[string.Join(" ", pa, 0, n - 3)] = new Vector3(float.Parse(pa[n - 3], ci), float.Parse(pa[n - 2], ci), float.Parse(pa[n - 1], ci)); }
                    foreach (var g in Resources.FindObjectsOfTypeAll<GameObject>()) if (g.scene.IsValid() && g.name.StartsWith("coop_mina_minero_")) PoseCuerpo.Aplicar(g, PoseCuerpo.Sentado);
                    var sb2 = new System.Text.StringBuilder(); foreach (var kv in PoseCuerpo.Sentado) sb2.Append(kv.Key).Append("=").Append(kv.Value.ToString("F0")).Append("; ");
                    plugin.Log("  pose: " + sb2); break;
                }
                case "murcielago": plugin.Log("  " + Murcielago.Orden(arg)); break;
                case "verescalera": plugin.Log("  " + plugin.mundo.OrdenVer("escalera", arg.Length > 0 ? int.Parse(arg) : 0)); break;
                case "verexpositor": plugin.Log("  " + plugin.mundo.OrdenVer("expositor", arg.Length > 0 ? int.Parse(arg) : 0)); break;
                case "jefecueva": plugin.Log("  " + plugin.mundo.OrdenJefeCueva(arg)); break;   // jefecueva | jefecueva despertar | jefecueva danar <n>   // murcielago crear [estado] | murcielago estado <x> | murcielago quitar
                case "botonesui":   // botonesui <raíz>: botones bajo ese objeto (también ocultos) con su texto
                {
                    foreach (var g in Resources.FindObjectsOfTypeAll<GameObject>())
                    {
                        if (g.name != arg || !g.scene.IsValid()) continue;
                        foreach (var b in g.GetComponentsInChildren<UnityEngine.UI.Button>(true))
                        {
                            var tx = b.GetComponentInChildren<UnityEngine.UI.Text>(true);
                            var fs = new System.Text.StringBuilder(); foreach (var f in b.GetComponents<PlayMakerFSM>()) fs.Append(f.FsmName).Append(",");
                            plugin.Log("  " + b.transform.parent.name + "/" + b.name + " texto='" + (tx != null ? tx.text : "") + "' clics=" + b.onClick.GetPersistentEventCount() + " fsm=" + fs);
                        }
                    }
                    break;
                }
                case "modelos": ListarModelos(arg); break;
                case "monstruos": ListarMonstruos(); break;
                case "volar": { var jv = UnityEngine.Object.FindObjectOfType<FPSPlayer>(); if (jv != null && Vuelo.Activo != (arg != "0")) Vuelo.Alternar(jv); plugin.Log("  volar " + Vuelo.Activo); break; }
                case "ira":   // ira <x> <z> [yaw]: al mundo (coordenadas del terreno norte), sobre el suelo, mirando al yaw
                {
                    var ji = UnityEngine.Object.FindObjectOfType<FPSPlayer>(); var ci2 = System.Globalization.CultureInfo.InvariantCulture; var ps2 = arg.Split(' ');
                    if (ji == null || ps2.Length < 2 || plugin.mundo.TerrenoNorte == null) break;
                    float xi2 = float.Parse(ps2[0], ci2), zi2 = float.Parse(ps2[1], ci2);
                    var tn = plugin.mundo.TerrenoNorte; float yy = tn.SampleHeight(tn.transform.position + new Vector3(xi2, 0f, zi2)) + tn.transform.position.y;
                    ji.transform.position = tn.transform.position + new Vector3(xi2, yy - tn.transform.position.y + 2f + (ps2.Length > 3 ? float.Parse(ps2[3], ci2) : 0f), zi2);
                    if (ps2.Length > 2) { var ml2 = UnityEngine.Object.FindObjectOfType<SmoothMouseLook>(); if (ml2 != null) ml2.rotationX = Mathf.DeltaAngle(ml2.originalRotation.eulerAngles.y, float.Parse(ps2[2], ci2)); }
                    break;
                }
                case "tp":   // tp <x> <y> <z> [yaw] [inclinación]: a esa posición del mundo (para fotografiar sitios cerrados como las minas)
                {
                    var jt = UnityEngine.Object.FindObjectOfType<FPSPlayer>(); var cit = System.Globalization.CultureInfo.InvariantCulture; var pt = arg.Split(' ');
                    if (jt == null || pt.Length < 3) break;
                    jt.transform.position = new Vector3(float.Parse(pt[0], cit), float.Parse(pt[1], cit), float.Parse(pt[2], cit));
                    var mlt = UnityEngine.Object.FindObjectOfType<SmoothMouseLook>();
                    if (mlt != null) { if (pt.Length > 3) mlt.rotationX = Mathf.DeltaAngle(mlt.originalRotation.eulerAngles.y, float.Parse(pt[3], cit)); if (pt.Length > 4) mlt.rotationY = float.Parse(pt[4], cit); }
                    break;
                }
                case "grandes":   // grandes [radio] [tamaño]: renderizadores grandes cerca del jugador (para encontrar telones/planos)
                {
                    var jg = UnityEngine.Object.FindObjectOfType<FPSPlayer>(); if (jg == null) break;
                    var ci2 = System.Globalization.CultureInfo.InvariantCulture; var pg = arg.Split(' ');
                    float rg = pg.Length > 0 && pg[0].Length > 0 ? float.Parse(pg[0], ci2) : 400f, mg = pg.Length > 1 ? float.Parse(pg[1], ci2) : 60f;
                    foreach (var rd in UnityEngine.Object.FindObjectsOfType<Renderer>())
                    {
                        if (rd == null || !rd.enabled) continue;
                        var bg = rd.bounds; if (Mathf.Max(bg.size.x, Mathf.Max(bg.size.y, bg.size.z)) < mg) continue;
                        float dg = Vector3.Distance(bg.ClosestPoint(jg.transform.position), jg.transform.position); if (dg > rg) continue;
                        plugin.Log("  " + RutaDe(rd.transform, null) + " [" + rd.GetType().Name + "] a " + dg.ToString("0") + " m centro=" + (bg.center - Anclaje.Origen()) + " tam=" + bg.size);
                    }
                    break;
                }
                case "colliders":   // colliders [radio]: cada colisionador a ese radio del jugador (ruta, tipo, tamaño, activo)
                {
                    var jc2 = UnityEngine.Object.FindObjectOfType<FPSPlayer>(); if (jc2 == null) break;
                    float rr = arg.Length > 0 ? float.Parse(arg, System.Globalization.CultureInfo.InvariantCulture) : 6f;
                    foreach (var c in Physics.OverlapSphere(jc2.transform.position, rr, ~0, QueryTriggerInteraction.Collide))
                    {
                        string ruta = c.name; for (var x = c.transform.parent; x != null; x = x.parent) ruta = x.name + "/" + ruta;
                        var rd = c.GetComponent<Renderer>();
                        plugin.Log("  " + ruta + " [" + c.GetType().Name + (c.isTrigger ? " trigger" : "") + "] capa=" + c.gameObject.layer +
                                   " centro=" + c.bounds.center.ToString("F1") + " tam=" + c.bounds.size.ToString("F1") + " visible=" + (rd != null && rd.enabled));
                    }
                    break;
                }
                case "cerca":   // cerca [radio]: lista los objetos raíz con malla a ese radio del jugador (con su distancia y sus colisionadores)
                {
                    var jc = UnityEngine.Object.FindObjectOfType<FPSPlayer>(); if (jc == null) break;
                    float rc = arg.Length > 0 ? float.Parse(arg, System.Globalization.CultureInfo.InvariantCulture) : 25f;
                    var vistos2 = new HashSet<Transform>();
                    foreach (var r in UnityEngine.Object.FindObjectsOfType<Renderer>())
                    {
                        if (r is ParticleSystemRenderer || !r.enabled) continue;
                        float dd = Vector3.Distance(r.bounds.center, jc.transform.position); if (dd > rc) continue;
                        var raiz = r.transform.root; if (raiz.name.StartsWith("coop_mina_boca") && r.transform.name.StartsWith("Rock")) continue;
                        if (!vistos2.Add(raiz)) continue;
                        plugin.Log("  " + raiz.name + " a " + dd.ToString("F1") + " m, y=" + r.bounds.center.y.ToString("F1") + ", colisionadores=" + raiz.GetComponentsInChildren<Collider>().Length + ", pickup=" + (raiz.GetComponentInChildren<PlayMakerFSM>() != null));
                    }
                    break;
                }
                case "refrescaratg": { int na = 0; foreach (var t in Terrain.activeTerrains) na += Mundo.RefrescarHierbaATG(t); plugin.Log("  GrassManager reiniciados: " + na); break; }
                case "refrescarpasto": { int tot = 0; foreach (var t in Terrain.activeTerrains) tot += Mundo.RefrescarPasto(t.terrainData, 0f, 0f, 1e9f, 1e9f); plugin.Log("  hierba regenerada (" + tot + " celdas por capa)"); break; }
                case "perfil":   // perfil x z rumbo largo [paso]: altura del terreno, primer colisionador desde arriba y hierba de detalle a lo largo de una línea
                {
                    var ci = System.Globalization.CultureInfo.InvariantCulture; var pp = arg.Split(' ');
                    float px = float.Parse(pp[0], ci), pz = float.Parse(pp[1], ci), rumbo = float.Parse(pp[2], ci) * Mathf.Deg2Rad, largo = float.Parse(pp[3], ci), pasoP = pp.Length > 4 ? float.Parse(pp[4], ci) : 2f;
                    for (float d = 0f; d <= largo; d += pasoP)
                    {
                        float x = px + Mathf.Sin(rumbo) * d, z = pz + Mathf.Cos(rumbo) * d;
                        Terrain tt = null; foreach (var t in Terrain.activeTerrains) { var to = t.transform.position; var ts = t.terrainData.size; if (x >= to.x && x <= to.x + ts.x && z >= to.z && z <= to.z + ts.z) { tt = t; break; } }
                        if (tt == null) { plugin.Log("    d=" + d + " fuera"); continue; }
                        var td = tt.terrainData; Vector3 o = tt.transform.position;
                        float hT = tt.SampleHeight(new Vector3(x, 0f, z)) + o.y;
                        int dx = Mathf.Clamp((int)((x - o.x) / td.size.x * td.detailWidth), 0, td.detailWidth - 1), dz = Mathf.Clamp((int)((z - o.z) / td.size.z * td.detailHeight), 0, td.detailHeight - 1);
                        int pasto = 0; for (int i = 0; i < td.detailPrototypes.Length; i++) pasto += td.GetDetailLayer(dx, dz, 1, 1, i)[0, 0];
                        RaycastHit hit; string col = "-";
                        if (Physics.Raycast(new Vector3(x, 300f, z), Vector3.down, out hit, 400f, ~0, QueryTriggerInteraction.Ignore)) col = hit.collider.name + "@" + hit.point.y.ToString("F1");
                        plugin.Log("    d=" + d.ToString("F0") + " (" + x.ToString("F0") + "," + z.ToString("F0") + ") terreno=" + hT.ToString("F1") + " pasto=" + pasto + " choque=" + col);
                    }
                    break;
                }
                case "pixelerror": foreach (var t in Terrain.activeTerrains) { if (arg.Length > 0) t.heightmapPixelError = float.Parse(arg, System.Globalization.CultureInfo.InvariantCulture); plugin.Log("  " + t.name + " error de píxel " + t.heightmapPixelError + ", distancia hierba " + t.detailObjectDistance); } break;
                case "aqui":   // aqui x z radio: todo lo que se dibuja cerca de ese punto (objetos de cualquier tamaño e instancias de árbol del terreno) y su altura respecto al suelo
                {
                    var ci = System.Globalization.CultureInfo.InvariantCulture; var pp = arg.Split(' ');
                    float ax = float.Parse(pp[0], ci), az = float.Parse(pp[1], ci), ar = pp.Length > 2 ? float.Parse(pp[2], ci) : 8f;
                    var ta = Terrain.activeTerrain; foreach (var t in Terrain.activeTerrains) { var to = t.transform.position; var ts = t.terrainData.size; if (ax >= to.x && ax <= to.x + ts.x && az >= to.z && az <= to.z + ts.z) ta = t; }
                    float sueloA = ta.SampleHeight(new Vector3(ax, 0f, az)) + ta.transform.position.y;
                    plugin.Log("  suelo en el punto " + sueloA.ToString("F1"));
                    foreach (var r in UnityEngine.Object.FindObjectsOfType<Renderer>())
                    {
                        if (!r.enabled || r is ParticleSystemRenderer) continue; var b = r.bounds;
                        if (ax < b.min.x - ar || ax > b.max.x + ar || az < b.min.z - ar || az > b.max.z + ar) continue;
                        if (b.size.x > 400f || b.size.z > 400f) continue;
                        plugin.Log("    " + r.GetType().Name + " " + r.transform.root.name + "/" + r.name + " min=" + b.min.ToString("F1") + " max=" + b.max.ToString("F1") + " horneada=" + r.isPartOfStaticBatch + " col=" + r.GetComponentsInChildren<Collider>().Length);
                    }
                    var tdA = ta.terrainData; Vector3 oA = ta.transform.position; int na = 0;
                    foreach (var ti in tdA.treeInstances)
                    {
                        Vector3 w = Vector3.Scale(ti.position, tdA.size) + oA;
                        if (Mathf.Abs(w.x - ax) > ar || Mathf.Abs(w.z - az) > ar) continue;
                        float sueloT = ta.SampleHeight(w) + oA.y;
                        if (na++ < 25) plugin.Log("    árbol-terreno tipo " + ti.prototypeIndex + " (" + (tdA.treePrototypes[ti.prototypeIndex].prefab != null ? tdA.treePrototypes[ti.prototypeIndex].prefab.name : "?") + ") en " + w.ToString("F1") + " suelo " + sueloT.ToString("F1") + " hueco " + (w.y - sueloT).ToString("F1") + " escala " + ti.heightScale.ToString("F2"));
                    }
                    plugin.Log("  instancias de árbol del terreno cerca: " + na);
                    break;
                }
                case "filtros":   // efectos de pantalla (CameraFilterPack…) en todas las cámaras: activo y sus valores numéricos
                {
                    foreach (var c in UnityEngine.Object.FindObjectsOfType<Camera>())
                        foreach (var mb in c.GetComponents<MonoBehaviour>())
                        {
                            if (mb == null) continue; var tipo = mb.GetType(); if (!tipo.Name.StartsWith("CameraFilterPack") && tipo.Name.IndexOf("Blur", StringComparison.OrdinalIgnoreCase) < 0 && tipo.Name.IndexOf("Vignet", StringComparison.OrdinalIgnoreCase) < 0) continue;
                            var sb = new System.Text.StringBuilder();
                            foreach (var fi in tipo.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)) if (fi.FieldType == typeof(float)) sb.Append(fi.Name).Append('=').Append(((float)fi.GetValue(mb)).ToString("0.###")).Append(' ');
                            plugin.Log("    " + c.name + " " + tipo.Name + " activo=" + mb.enabled + " " + sb);
                        }
                    break;
                }
                case "fsmcaida":   // las máquinas a las que avisa el jugador (caída, etc.): dónde están, estado y si el mod las durmió
                {
                    var w = UnityEngine.Object.FindObjectOfType<FPSRigidBodyWalker>();
                    if (w == null || w.FSMs == null) { plugin.Log("  sin lista"); break; }
                    foreach (var f in w.FSMs)
                    {
                        if (f == null) { plugin.Log("    (nula)"); continue; }
                        string ruta = f.name; for (var t = f.transform.parent; t != null; t = t.parent) ruta = t.name + "/" + ruta;
                        plugin.Log("    " + ruta + " [" + f.FsmName + "] estado=" + f.ActiveStateName + " activa=" + f.enabled + " dormida=" + (f.Fsm != null && f.Fsm.ManualUpdate) + " pos=" + f.transform.position.ToString("F0"));
                    }
                    break;
                }
                case "flotantes":   // flotantes [radio]: piezas con malla cuya base queda en el aire sobre el terreno (hierba, matas, rocas)
                {
                    var jf = UnityEngine.Object.FindObjectOfType<FPSPlayer>(); var tf = Terrain.activeTerrain; if (jf == null) break;
                    float rf = arg.Length > 0 ? float.Parse(arg, System.Globalization.CultureInfo.InvariantCulture) : 80f;
                    int nf = 0; var porNombre = new Dictionary<string, int>(); float peor = 0f; string peorTxt = "";
                    foreach (var r in UnityEngine.Object.FindObjectsOfType<MeshRenderer>())
                    {
                        if (!r.enabled || r.name.StartsWith("coop_")) continue;
                        var b = r.bounds; if (Vector2.Distance(new Vector2(b.center.x, b.center.z), new Vector2(jf.transform.position.x, jf.transform.position.z)) > rf) continue;
                        if (Mathf.Max(b.size.x, b.size.z) > 14f) continue;
                        Terrain tt = null; foreach (var t in Terrain.activeTerrains) { var to = t.transform.position; var ts = t.terrainData.size; if (b.center.x >= to.x && b.center.x <= to.x + ts.x && b.center.z >= to.z && b.center.z <= to.z + ts.z) { tt = t; break; } }
                        if (tt == null) continue;
                        float suelo = tt.SampleHeight(b.center) + tt.transform.position.y, hueco = b.min.y - suelo;
                        if (hueco < 0.3f || hueco > 6f) continue;
                        nf++; string k = r.transform.root.name + "/" + r.name; int c; porNombre.TryGetValue(k, out c); porNombre[k] = c + 1;
                        if (hueco > peor) { peor = hueco; peorTxt = r.name + " en " + b.center.ToString("F0") + " horneada=" + r.isPartOfStaticBatch; }
                    }
                    plugin.Log("  flotantes a " + rf + " m: " + nf + " (peor " + peor.ToString("F1") + " m: " + peorTxt + ")");
                    foreach (var kv in porNombre) plugin.Log("    " + kv.Key + " ×" + kv.Value);
                    break;
                }
                case "usar": Entrada.Usar = arg.Length > 0 ? int.Parse(arg) : 20; Entrada.UsarPulso = true; break;   // usar [fotogramas]: mantiene la tecla de usar (E)
                case "apuntarxyz":   // apuntar <x> <y> <z>: gira la cámara hacia ese punto del mundo y dice qué hay justo en la mira (colisionador, distancia)
                {
                    var ml3 = UnityEngine.Object.FindObjectOfType<SmoothMouseLook>(); var ci3 = System.Globalization.CultureInfo.InvariantCulture; var pp = arg.Split(' ');
                    if (ml3 == null || Camera.main == null || pp.Length < 3) break;
                    Vector3 dd = new Vector3(float.Parse(pp[0], ci3), float.Parse(pp[1], ci3), float.Parse(pp[2], ci3)) - Camera.main.transform.position;
                    ml3.rotationX = Mathf.DeltaAngle(ml3.originalRotation.eulerAngles.y, Mathf.Atan2(dd.x, dd.z) * Mathf.Rad2Deg);
                    ml3.rotationY = -Mathf.Atan2(dd.y, new Vector2(dd.x, dd.z).magnitude) * Mathf.Rad2Deg * -1f;
                    break;
                }
                case "alturas":   // alturas <x> <z>: altura del terreno (SampleHeight y rayo) en ese punto
                {
                    var pq = arg.Split(' '); var cq = System.Globalization.CultureInfo.InvariantCulture; float xq = float.Parse(pq[0], cq), zq = float.Parse(pq[1], cq);
                    RaycastHit hq; bool okq = Physics.Raycast(new Vector3(xq, 200f, zq), Vector3.down, out hq, 400f, ~0, QueryTriggerInteraction.Ignore);
                    plugin.Log("  rayo: " + (okq ? hq.point.y.ToString("F2") + " (" + hq.collider.name + ")" : "nada"));
                    break;
                }
                case "mira":
                {
                    RaycastHit[] hs = Physics.RaycastAll(Camera.main.transform.position, Camera.main.transform.forward, 6f, ~0, QueryTriggerInteraction.Collide);
                    System.Array.Sort(hs, (x, y) => x.distance.CompareTo(y.distance));
                    foreach (var h in hs) plugin.Log("  mira: " + h.collider.name + " (" + h.collider.transform.root.name + ") a " + h.distance.ToString("F2") + " m trigger=" + h.collider.isTrigger + " capa=" + h.collider.gameObject.layer);
                    break;
                }
                case "dios":
                {
                    var j = UnityEngine.Object.FindObjectOfType<FPSPlayer>();
                    if (j != null) { j.GodMode = arg != "off"; j.hitPoints = Mathf.Max(j.hitPoints, 100f); plugin.Log("  modo dios " + j.GodMode); }
                    break;
                }
                case "irjefe":   // irjefe [dist]: junto al jefe vivo más cercano (cualquier distancia) (2026-11-20)
                case "danarjefe":   // danarjefe <n>: daño n al jefe vivo más cercano por su zona de daño real (como un balazo) (2026-11-20)
                {
                    var jj = UnityEngine.Object.FindObjectOfType<FPSPlayer>(); if (jj == null) break;
                    TakeDamage jefe = null; float dm = float.MaxValue;
                    foreach (var td in UnityEngine.Object.FindObjectsOfType<TakeDamage>())
                    {
                        if (td == null || td.GetComponentInParent<FPSPlayer>() != null || Eventos.Muerto(td) || !Dificultad.EsJefe(td)) continue;
                        float d = Vector3.Distance(td.transform.position, jj.transform.position); if (d < dm) { dm = d; jefe = td; }
                    }
                    if (jefe == null) { plugin.Log("  no hay jefes vivos"); break; }
                    var rz = Combate.RaizDe(jefe);
                    if (orden == "irjefe")
                    {
                        float dist = arg.Length > 0 ? float.Parse(arg, System.Globalization.CultureInfo.InvariantCulture) : 10f;
                        Vector3 m = jefe.transform.position, dir = jj.transform.position - m; dir.y = 0; if (dir.sqrMagnitude < 0.01f) dir = Vector3.forward; dir.Normalize();
                        Vector3 dest = m + dir * dist; UnityEngine.AI.NavMeshHit nh;
                        if (UnityEngine.AI.NavMesh.SamplePosition(dest, out nh, 15f, UnityEngine.AI.NavMesh.AllAreas)) dest = nh.position;
                        jj.transform.position = dest + Vector3.up * 1.2f; var rb = jj.GetComponent<Rigidbody>(); if (rb != null) { rb.position = jj.transform.position; rb.velocity = Vector3.zero; }
                        plugin.Log("  junto a " + rz.name + " en " + (m - Anclaje.Origen()).ToString("F0") + " (estaba a " + Mathf.RoundToInt(dm) + " m)");
                        break;
                    }
                    CharacterDamagePlaymakerAI zona = null;
                    foreach (var z in rz.GetComponentsInChildren<CharacterDamagePlaymakerAI>()) if (z.ai == jefe) { zona = z; break; }
                    float cant = arg.Length > 0 ? float.Parse(arg, System.Globalization.CultureInfo.InvariantCulture) : 100f;
                    if (zona != null) zona.ApplyDamage(cant, false); else jefe.Damage(cant, false);
                    plugin.Log("  daño " + cant + " a " + rz.name + " (" + (zona != null ? "zona " + zona.name : "directo") + ") a " + Mathf.RoundToInt(dm) + " m");
                    break;
                }
                case "danarnombre":   // danarnombre <texto> [n]: daño n (0 = solo informe) al monstruo cuya raíz contenga el texto, aunque su vida esté en 0 (2026-11-20)
                {
                    var pa = arg.Split(' '); float cant = pa.Length > 1 ? float.Parse(pa[1], System.Globalization.CultureInfo.InvariantCulture) : 0f;
                    int n = 0;
                    foreach (var td in UnityEngine.Object.FindObjectsOfType<TakeDamage>())
                    {
                        if (td == null || td.GetComponentInParent<FPSPlayer>() != null) continue;
                        if (td.transform.root.name.IndexOf(pa[0], StringComparison.OrdinalIgnoreCase) < 0) continue;
                        var f = PlayMakerFSM.FindFsmOnGameObject(td.gameObject, td.NameOfFsm); var h = f != null ? f.FsmVariables.FindFsmFloat("Health") : null;
                        var rz = Combate.RaizDe(td);
                        plugin.Log("  " + td.transform.root.name + "/" + td.name + " td en " + td.transform.position.ToString("F0") + " raíz-unidad " + rz.name + " en " + rz.position.ToString("F0")
                            + " vida=" + (h != null ? h.Value.ToString("0") : "?") + " jefe=" + Dificultad.EsJefe(td) + " sincronizado=" + (plugin.combate != null && plugin.combate.Sincronizado(td)));
                        if (cant > 0f)
                        {
                            CharacterDamagePlaymakerAI zona = null;
                            foreach (var z in rz.GetComponentsInChildren<CharacterDamagePlaymakerAI>()) if (z.ai == td) { zona = z; break; }
                            if (zona != null) zona.ApplyDamage(cant, false); else td.Damage(cant, false);
                            plugin.Log("    daño " + cant + (zona != null ? " por la zona " + zona.name : " directo"));
                        }
                        if (++n >= 4) break;
                    }
                    if (n == 0) plugin.Log("  no hay monstruos '" + pa[0] + "'");
                    break;
                }
                case "irmonstruo": IrMonstruo(arg.Length > 0 ? float.Parse(arg, System.Globalization.CultureInfo.InvariantCulture) : 20f); break;
                case "escena":
                    // Igual que el cambio de nivel del juego: guarda armas/variables antes de salir.
                    plugin.Log("  cargando escena '" + arg + "'");
                    PixelCrushers.DialogueSystem.PersistentDataManager.Record();
                    PixelCrushers.DialogueSystem.PersistentDataManager.LevelWillBeUnloaded();
                    escenaPendiente = arg;
                    SceneManager.LoadScene(arg); break;
                case "danar": Danar(arg.Length > 0 ? float.Parse(arg, System.Globalization.CultureInfo.InvariantCulture) : 10f); break;
                case "vida": Vida(); break;
                case "oro":
                    if (arg.Length > 0)
                    {
                        // oro +N / -N: simula recoger o gastar; oro N: fija la bolsa
                        var g = HutongGames.PlayMaker.FsmVariables.GlobalVariables;
                        int? c = Oro.Leer();
                        if (c != null)
                        {
                            int n = int.Parse(arg.TrimStart('+'));
                            int nuevo = arg[0] == '+' || arg[0] == '-' ? c.Value + n : n;
                            var vi = g.FindFsmInt(Oro.Variable); if (vi != null) vi.Value = nuevo;
                            var vf = g.FindFsmFloat(Oro.Variable); if (vf != null) vf.Value = nuevo;
                        }
                    }
                    plugin.Log("  " + Oro.Describir());
                    break;
                case "globales":
                {
                    var g = HutongGames.PlayMaker.FsmVariables.GlobalVariables;
                    foreach (var v in g.GetAllNamedVariables())
                        if (arg.Length == 0 || v.Name.IndexOf(arg, StringComparison.OrdinalIgnoreCase) >= 0)
                            plugin.Log("  " + v.GetType().Name + " " + v.Name + " = " + v.ToString());
                    break;
                }
                case "campos":
                {
                    // campos <Tipo> [filtro]: campos públicos del primer objeto de ese tipo
                    string[] pa = arg.Split(new[] { ' ' }, 2);
                    Type tipo = typeof(FPSPlayer).Assembly.GetType(pa[0]);
                    if (tipo == null) { plugin.Log("  tipo desconocido"); break; }
                    var o = UnityEngine.Object.FindObjectOfType(tipo);
                    if (o == null) { plugin.Log("  no hay " + pa[0]); break; }
                    foreach (var c in tipo.GetFields())
                    {
                        if (pa.Length > 1 && c.Name.IndexOf(pa[1], StringComparison.OrdinalIgnoreCase) < 0) continue;
                        object v = null; try { v = c.GetValue(o); } catch { }
                        plugin.Log("  " + c.Name + " = " + v);
                    }
                    break;
                }
                case "textos":
                {
                    // textos <objeto>: textos de UI bajo ese objeto (también inactivos)
                    foreach (var g in Resources.FindObjectsOfTypeAll<GameObject>())
                    {
                        if (g.name != arg || !g.scene.IsValid()) continue;
                        plugin.Log("  [" + g.name + "] activo=" + g.activeInHierarchy);
                        foreach (var tx in g.GetComponentsInChildren<UnityEngine.UI.Text>(true))
                            plugin.Log("  " + RutaDe(tx.transform, g.transform) + " : " + tx.text.Replace("\n", " | "));
                        foreach (var b in g.GetComponentsInChildren<UnityEngine.UI.Button>(true))
                            plugin.Log("  botón " + RutaDe(b.transform, g.transform));
                    }
                    break;
                }
                case "libro":
                    if (arg == "comprar") plugin.tienda.Comprar();
                    else if (arg == "quitar") PixelCrushers.DialogueSystem.DialogueLua.SetVariable(Tienda.VarLibro, false);
                    {
                        var jj = UnityEngine.Object.FindObjectOfType<FPSPlayer>();
                        plugin.Log("  libro=" + Tienda.TieneLibro + " regen=" + (jj != null ? jj.healthRegenRate.ToString("0.###") : "?") + " " + Oro.Describir());
                    }
                    break;
                case "hijosinv":
                {
                    foreach (var g in Resources.FindObjectsOfTypeAll<GameObject>())
                        if (g.name == "Canvas_inventory" && g.scene.IsValid())
                            foreach (Transform t in g.transform)
                            {
                                var rt = t as RectTransform;
                                plugin.Log("  " + t.name + " act=" + t.gameObject.activeSelf + " pos=" + (rt != null ? rt.anchoredPosition.ToString() : "-") + " tam=" + (rt != null ? rt.sizeDelta.ToString() : "-") + " hijos=" + t.childCount + " fsm=" + t.GetComponent<PlayMakerFSM>());
                            }
                    break;
                }
                case "bloqueoesc":
                {
                    if (arg == "") Controles.HastaEsc = Time.realtimeSinceStartup + 6f;
                    var j = Rewired.ReInput.players.GetPlayer(0); int on = 0, tot = 0;
                    foreach (var m in j.controllers.maps.GetAllMaps()) { tot++; if (m.enabled) on++; }
                    plugin.Log("  mapas activos " + on + "/" + tot + " (el bloqueo se aplica en el siguiente cuadro)");
                    break;
                }
                case "equipado":
                {
                    foreach (var g in Resources.FindObjectsOfTypeAll<GameObject>())
                        if (g.name == "ScrollContent" && g.transform.parent != null && g.transform.parent.name == "ScrollView_equipped" && g.scene.IsValid())
                            foreach (Transform t in g.transform)
                            {
                                var im = t.GetComponent<UnityEngine.UI.Image>(); var ri = t.GetComponent<UnityEngine.UI.RawImage>();
                                var rt = (RectTransform)t;
                                plugin.Log("  " + t.name + " act=" + t.gameObject.activeSelf + " pos=" + rt.anchoredPosition + " sprite=" + (im != null && im.sprite != null ? im.sprite.name : "-") + " raw=" + (ri != null && ri.texture != null ? ri.texture.name : "-") + " hijos=" + t.childCount);
                                foreach (Transform h in t) { var hi = h.GetComponent<UnityEngine.UI.Image>(); var hr = h.GetComponent<UnityEngine.UI.RawImage>(); plugin.Log("     - " + h.name + " act=" + h.gameObject.activeSelf + " sprite=" + (hi != null && hi.sprite != null ? hi.sprite.name : "-") + " raw=" + (hr != null && hr.texture != null ? hr.texture.name : "-")); }
                            }
                    break;
                }
                case "inventario":
                {
                    foreach (var g in Resources.FindObjectsOfTypeAll<GameObject>())
                        if (g.name == "Canvas_inventory" && g.scene.IsValid()) { g.SetActive(arg != "cerrar"); if (arg != "cerrar") Time.timeScale = 0f; else Time.timeScale = 1f; }
                    break;
                }
                case "tienda":
                {
                    // tienda abrir|cerrar: muestra u oculta el lienzo de la tienda (solo para capturas)
                    foreach (var g in Resources.FindObjectsOfTypeAll<GameObject>())
                        if (g.name == "Canvas_shop" && g.scene.IsValid()) g.SetActive(arg != "cerrar");
                    plugin.Log("  tienda abierta=" + plugin.tienda.Abierta);
                    break;
                }
                case "guardado": plugin.Log("  " + Guardado.Describir()); break;
                case "congelon": System.Threading.Thread.Sleep((int)(float.Parse(arg, System.Globalization.CultureInfo.InvariantCulture) * 1000f)); plugin.Log("  congelón de " + arg + " s terminado"); break;   // congela el hilo principal (prueba de desconexión falsa)
                case "derribotiempo": Muerte.TiempoDerribado = float.Parse(arg, System.Globalization.CultureInfo.InvariantCulture); plugin.Log("  tiempo de derribado " + Muerte.TiempoDerribado + " s"); break;
                case "morir":
                {
                    // morir: mata al jugador como lo haría un monstruo (pasa por la FSM de muerte)
                    var jj = UnityEngine.Object.FindObjectOfType<FPSPlayer>();
                    if (jj != null) { jj.GodMode = false; jj.ApplyDamage(jj.hitPoints + 5f); }
                    plugin.Log("  vida=" + (jj != null ? jj.hitPoints.ToString("0") : "?"));
                    break;
                }
                case "buscarfsm":
                {
                    // buscarfsm <texto>: acciones de PlayMaker con algún campo que contenga el texto
                    var j = UnityEngine.Object.FindObjectOfType<FPSPlayer>();
                    int n = 0;
                    foreach (var f in Resources.FindObjectsOfTypeAll<PlayMakerFSM>())
                    {
                        if (!f.gameObject.scene.IsValid() || f.FsmStates == null) continue;
                        foreach (var st in f.FsmStates)
                        {
                            if (st.Actions == null) continue;
                            foreach (var a in st.Actions)
                            {
                                if (a == null) continue;
                                var sb = new System.Text.StringBuilder(); bool hay = a.GetType().Name.IndexOf(arg, StringComparison.OrdinalIgnoreCase) >= 0;   // también por tipo de acción
                                foreach (var c in a.GetType().GetFields())
                                {
                                    object v = null; try { v = c.GetValue(a); } catch { }
                                    string vs = v == null ? "" : v.ToString();
                                    if (vs.IndexOf(arg, StringComparison.OrdinalIgnoreCase) >= 0) hay = true;
                                    sb.Append(c.Name).Append('=').Append(vs).Append(' ');
                                }
                                if (!hay || n++ > 40) continue;
                                float d = j != null ? Vector3.Distance(j.transform.position, f.transform.position) : -1f;
                                plugin.Log("  " + RutaDe(f.transform, null) + "/" + f.FsmName + " [" + st.Name + "] " + a.GetType().Name + " activo=" + f.gameObject.activeInHierarchy + " pos=" + f.transform.position + " dist=" + d.ToString("0") + " :: " + sb);
                            }
                        }
                    }
                    break;
                }
                case "buscarplantilla":
                {
                    // buscarplantilla <texto>: igual que buscarfsm, pero dentro de las plantillas (RunFSM)
                    int n = 0;
                    foreach (var tp in Resources.FindObjectsOfTypeAll<FsmTemplate>())
                    {
                        if (tp == null || tp.fsm == null) continue;
                        try { tp.fsm.Init((MonoBehaviour)null); } catch { }
                        if (tp.fsm.States == null) continue;
                        foreach (var st in tp.fsm.States)
                        {
                            FsmStateAction[] acts = null;
                            try { acts = st.Actions; } catch { }
                            if (acts == null) continue;
                            foreach (var ac in acts)
                            {
                                if (ac == null) continue;
                                var sb = new System.Text.StringBuilder(); bool hay = false;
                                foreach (var c in ac.GetType().GetFields())
                                {
                                    object v = null; try { v = c.GetValue(ac); } catch { }
                                    string vs = v == null ? "" : v.ToString();
                                    if (vs.IndexOf(arg, StringComparison.OrdinalIgnoreCase) >= 0) hay = true;
                                    sb.Append(c.Name).Append('=').Append(vs).Append(' ');
                                }
                                if (!hay || n++ > 40) continue;
                                plugin.Log("  plantilla '" + tp.name + "' [" + st.Name + "] " + ac.GetType().Name + " :: " + sb);
                            }
                        }
                    }
                    plugin.Log("  (" + n + " coincidencias)");
                    break;
                }
                case "objetos":
                {
                    // objetos <filtro>: objetos de la escena cuyo nombre contiene el filtro
                    var j = UnityEngine.Object.FindObjectOfType<FPSPlayer>();
                    int n = 0;
                    foreach (var g in Resources.FindObjectsOfTypeAll<GameObject>())
                    {
                        if (!g.scene.IsValid() || g.name.IndexOf(arg, StringComparison.OrdinalIgnoreCase) < 0) continue;
                        if (n++ > 40) break;
                        var col = g.GetComponent<Collider>();
                        var fs = new System.Text.StringBuilder();
                        foreach (var f in g.GetComponents<PlayMakerFSM>()) fs.Append(f.FsmName).Append(',');
                        float d = j != null ? Vector3.Distance(j.transform.position, g.transform.position) : -1f;
                        plugin.Log("  " + RutaDe(g.transform, null) + " pos=" + g.transform.position + " dist=" + d.ToString("0") + " activo=" + g.activeInHierarchy + (col != null ? " col" + (col.isTrigger ? "(trigger)" : "") : "") + (fs.Length > 0 ? " fsm=" + fs : ""));
                    }
                    break;
                }
                case "yaw":
                {
                    var jy = UnityEngine.Object.FindObjectOfType<FPSPlayer>();
                    if (jy != null) { jy.transform.rotation = Quaternion.Euler(0f, float.Parse(arg, System.Globalization.CultureInfo.InvariantCulture), 0f); plugin.Log("  yaw " + arg); }
                    break;
                }
                case "ir":
                {
                    // ir x y z: teletransporta al jugador
                    var ps = arg.Split(' ');
                    var ci = System.Globalization.CultureInfo.InvariantCulture;
                    var j = UnityEngine.Object.FindObjectOfType<FPSPlayer>();
                    // Coordenadas relativas al terreno principal (el mundo se recentra al alejarse).
                    if (j != null && ps.Length >= 3) j.transform.position = Anclaje.Origen() + new Vector3(float.Parse(ps[0], ci), float.Parse(ps[1], ci), float.Parse(ps[2], ci));
                    break;
                }
                case "ia":
                {
                    // ia: máquinas de estados del monstruo más cercano: estado, variables de objeto y acciones del estado activo
                    var tdm = MasCercano();
                    if (tdm == null) { plugin.Log("  no hay monstruos"); break; }
                    Transform rz = Combate.RaizDe(tdm); if (rz.parent != null) rz = rz.parent;
                    plugin.Log("  monstruo " + rz.name + " (jefe=" + Dificultad.EsJefe(tdm) + ")");
                    foreach (var f in rz.GetComponentsInChildren<PlayMakerFSM>(true))
                    {
                        var sb = new System.Text.StringBuilder();
                        foreach (var v in f.FsmVariables.GameObjectVariables) sb.Append(v.Name).Append('=').Append(v.Value != null ? v.Value.name : "null").Append("; ");
                        var acc = new System.Text.StringBuilder();
                        if (f.Fsm != null && f.Fsm.ActiveState != null && f.Fsm.ActiveState.Actions != null)
                            foreach (var a in f.Fsm.ActiveState.Actions) if (a != null) acc.Append(a.GetType().Name).Append(' ');
                        plugin.Log("    " + f.gameObject.name + "/" + f.FsmName + " estado='" + f.ActiveStateName + "' objetos[" + sb + "] acciones[" + acc + "]");
                    }
                    break;
                }
                case "cazar":
                {
                    // cazar [d]: se pone a d m (20) del monstruo vivo y activo más cercano; luego "apuntar" lo mira
                    float dd = arg.Length > 0 ? float.Parse(arg, System.Globalization.CultureInfo.InvariantCulture) : 20f;
                    var j = UnityEngine.Object.FindObjectOfType<FPSPlayer>();
                    TakeDamage mejor = null; float dm = 1e9f;
                    foreach (var td in UnityEngine.Object.FindObjectsOfType<TakeDamage>())
                    {
                        if (td == null || !td.gameObject.activeInHierarchy || td.GetComponentInParent<FPSPlayer>() != null) continue;
                        if (Dificultad.EsJefe(td) || td.transform.root.name.Contains("Crux") || Aliados.Protegido(td.transform)) continue;   // jefes y Crux no (matarlos avanza la historia)
                        var f = PlayMakerFSM.FindFsmOnGameObject(td.gameObject, td.NameOfFsm);
                        var h = f != null ? f.FsmVariables.FindFsmFloat("Health") : null;
                        if (h != null && h.Value <= 0f) continue;
                        float d0 = Vector3.Distance(td.transform.position, j.transform.position);
                        if (d0 < dm) { dm = d0; mejor = td; }
                    }
                    if (mejor == null || j == null) { plugin.Log("  no hay monstruos vivos"); break; }
                    presaPrueba = mejor.transform;
                    Vector3 m = mejor.transform.position, dir = j.transform.position - m; dir.y = 0f; if (dir.sqrMagnitude < 0.01f) dir = Vector3.forward; dir.Normalize();
                    Vector3 dest = m + dir * dd;
                    UnityEngine.AI.NavMeshHit nh;
                    if (UnityEngine.AI.NavMesh.SamplePosition(dest, out nh, 10f, UnityEngine.AI.NavMesh.AllAreas)) dest = nh.position;
                    j.transform.position = dest + Vector3.up * 1.2f;
                    var rb = j.GetComponent<Rigidbody>(); if (rb != null) { rb.position = j.transform.position; rb.velocity = Vector3.zero; }
                    plugin.Log("  a " + dd + " m de " + Combate.NombreDe(mejor) + " (estaba a " + dm.ToString("0") + " m)");
                    break;
                }
                case "apuntar":
                {
                    var ml = UnityEngine.Object.FindObjectOfType<SmoothMouseLook>();
                    if (ml == null || Camera.main == null || presaPrueba == null) { plugin.Log("  sin presa"); break; }
                    // al centro de lo que se ve del monstruo (caja de sus mallas)
                    Transform rz = presaPrueba;
                    while (rz.parent != null && rz.GetComponent<UnityEngine.AI.NavMeshAgent>() == null) rz = rz.parent;
                    Bounds bb = new Bounds(presaPrueba.position + Vector3.up, Vector3.zero); bool hayb = false;
                    foreach (var rr in rz.GetComponentsInChildren<Renderer>()) { if (rr is ParticleSystemRenderer || !rr.enabled) continue; if (!hayb) { bb = rr.bounds; hayb = true; } else bb.Encapsulate(rr.bounds); }
                    Vector3 blanco = bb.center;
                    Vector3 d = blanco - Camera.main.transform.position;
                    ml.rotationX = Mathf.DeltaAngle(ml.originalRotation.eulerAngles.y, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg);
                    ml.rotationY = Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;
                    plugin.Log("  apuntando a " + presaPrueba.name + " a " + d.magnitude.ToString("0") + " m (centro " + (blanco - Anclaje.Origen()).ToString("F1") + ", tamaño " + bb.size.ToString("F1") + ")");
                    break;
                }
                case "mirarhacia":
                {
                    // mirarhacia x y z: orienta la cámara hacia ese punto
                    var ps = arg.Split(' ');
                    var ci = System.Globalization.CultureInfo.InvariantCulture;
                    var ml = UnityEngine.Object.FindObjectOfType<SmoothMouseLook>();
                    if (ml == null || Camera.main == null || ps.Length < 3) break;
                    Vector3 d = Anclaje.Origen() + new Vector3(float.Parse(ps[0], ci), float.Parse(ps[1], ci), float.Parse(ps[2], ci)) - Camera.main.transform.position;
                    ml.rotationX = Mathf.DeltaAngle(ml.originalRotation.eulerAngles.y, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg);
                    ml.rotationY = Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;
                    break;
                }
                case "potenciar":
                {
                    // potenciar [tipo]: genera un potenciado a 5 m delante de la cámara (para ver el diseño)
                    var cam = Camera.main; if (cam == null) { plugin.Log("  sin cámara"); break; }
                    Vector3 pt = cam.transform.position + new Vector3(cam.transform.forward.x, 0f, cam.transform.forward.z).normalized * 5f;
                    var tdp = plugin.CombateActual.Generar((arg.Length > 0 ? arg : "Undead_naked") + Potenciado.Marca, pt);
                    plugin.Log("  potenciado " + (tdp != null ? tdp.name + " en " + tdp.transform.position : "no se pudo generar"));
                    break;
                }
                case "texturas":
                {
                    // texturas <texto>: texturas cuyo nombre contiene el texto, con su color medio
                    int n = 0;
                    foreach (var t in Resources.FindObjectsOfTypeAll<Texture>())
                    {
                        if (t == null || t.name.IndexOf(arg, StringComparison.OrdinalIgnoreCase) < 0) continue;
                        if (++n > 40) break;
                        var rt = RenderTexture.GetTemporary(4, 4, 0);
                        Graphics.Blit(t, rt);
                        var prev = RenderTexture.active; RenderTexture.active = rt;
                        var lec = new Texture2D(4, 4, TextureFormat.RGBA32, false); lec.ReadPixels(new Rect(0, 0, 4, 4), 0, 0); lec.Apply();
                        RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
                        Color m = Color.black; foreach (var px in lec.GetPixels()) m += px; m /= 16f; UnityEngine.Object.Destroy(lec);
                        plugin.Log("  " + t.name + " " + t.width + "x" + t.height + " medio=(" + m.r.ToString("0.00") + ", " + m.g.ToString("0.00") + ", " + m.b.ToString("0.00") + ")");
                    }
                    break;
                }
                case "prefabsenemigo":
                {
                    var nombres = new List<string>();
                    foreach (var g in Resources.FindObjectsOfTypeAll<GameObject>())
                    {
                        if (g == null || g.scene.IsValid() || g.transform.parent != null) continue;
                        if (g.GetComponentInChildren<TakeDamage>(true) == null) continue;
                        nombres.Add(g.name);
                    }
                    nombres.Sort();
                    plugin.Log("  prefabs con TakeDamage (" + nombres.Count + "): " + string.Join(", ", nombres.ToArray()));
                    break;
                }
                case "fsmprefab":
                {
                    // fsmprefab <nombre>: máquinas de estados de un prefab (o de una copia de la escena), con el tipo de sus acciones
                    foreach (var g in Resources.FindObjectsOfTypeAll<GameObject>())
                    {
                        if (g.name != arg || g.transform.parent != null) continue;
                        foreach (var f in g.GetComponentsInChildren<PlayMakerFSM>(true))
                        {
                            var sb = new System.Text.StringBuilder("  FSM " + f.FsmName + " en " + f.gameObject.name + ": ");
                            foreach (var st in f.FsmStates) { sb.Append("[").Append(st.Name).Append(": "); foreach (var a in st.Actions) sb.Append(a.GetType().Name).Append(' '); sb.Append("] "); }
                            plugin.Log(sb.ToString().Substring(0, Mathf.Min(sb.Length, 900)));
                        }
                        break;
                    }
                    break;
                }
                case "componentes":
                {
                    // componentes <nombre>: componentes de la primera copia en la escena (y de sus hijos) con campos de texto
                    foreach (var g in Resources.FindObjectsOfTypeAll<GameObject>())
                    {
                        if (!g.scene.IsValid() || !g.name.StartsWith(arg)) continue;
                        foreach (var c in g.GetComponentsInChildren<Component>(true))
                        {
                            if (c == null) continue;
                            string extra = "";
                            foreach (var fi in c.GetType().GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                                if (fi.FieldType == typeof(string)) { var v = fi.GetValue(c) as string; if (!string.IsNullOrEmpty(v)) extra += fi.Name + "='" + v + "' "; }
                            plugin.Log("  " + c.gameObject.name + " :: " + c.GetType().Name + " " + extra);
                        }
                        break;
                    }
                    break;
                }
                case "mallas":
                {
                    // mallas <texto>: todas las mallas en memoria cuyo nombre contiene el texto (también las no legibles)
                    int n = 0;
                    foreach (var mm in Resources.FindObjectsOfTypeAll<Mesh>())
                        if (mm != null && mm.name.IndexOf(arg, StringComparison.OrdinalIgnoreCase) >= 0 && ++n <= 40)
                            plugin.Log("  malla '" + mm.name + "' legible=" + mm.isReadable + " vértices=" + mm.vertexCount + " submallas=" + mm.subMeshCount);
                    plugin.Log("  " + n + " mallas");
                    break;
                }
                case "verprop":
                {
                    // verprop <tipo> [n]: pone al jugador delante del mueble n.º n (del lado del centro del castillo) mirándolo
                    var pv = arg.Split(' '); int nv = pv.Length > 1 ? int.Parse(pv[1]) : 0, iv = 0;
                    GameObject gv = null;
                    foreach (var g in Resources.FindObjectsOfTypeAll<GameObject>())
                        if (g.scene.IsValid() && g.activeInHierarchy && g.name == "coop_prop_" + pv[0]) { if (iv++ == nv) { gv = g; break; } }
                    var jv = UnityEngine.Object.FindObjectOfType<FPSPlayer>();
                    if (gv == null || jv == null) { plugin.Log("  no hay coop_prop_" + pv[0] + " n.º " + nv + " (hay " + iv + ")"); break; }
                    Bounds bv = new Bounds(gv.transform.position, Vector3.zero);
                    foreach (var r in gv.GetComponentsInChildren<Renderer>()) bv.Encapsulate(r.bounds);
                    Vector3 cen = plugin.mundo.CentroCastilloMundo(); Vector3 dv = cen - bv.center; dv.y = 0f;
                    if (pv.Length > 2) { float gy = float.Parse(pv[2], System.Globalization.CultureInfo.InvariantCulture); dv = Quaternion.Euler(0f, gy, 0f) * (-gv.transform.forward); dv.y = 0f; }   // 3.er dato: ángulo respecto al frente del mueble (0 = de frente)
                    if (dv.sqrMagnitude < 0.01f) dv = Vector3.forward;
                    float dist = Mathf.Max(1.6f, bv.extents.magnitude * 2.2f);
                    Vector3 ojo = bv.center + dv.normalized * dist; ojo.y = bv.center.y + 0.4f;
                    jv.transform.position = ojo - Vector3.up * 0.9f;
                    var ml = UnityEngine.Object.FindObjectOfType<SmoothMouseLook>();
                    if (ml != null)
                    {
                        Vector3 a = bv.center - ojo;
                        ml.rotationX = Mathf.DeltaAngle(ml.originalRotation.eulerAngles.y, Mathf.Atan2(a.x, a.z) * Mathf.Rad2Deg);
                        ml.rotationY = Mathf.Atan2(-a.y, new Vector2(a.x, a.z).magnitude) * Mathf.Rad2Deg * -1f;
                    }
                    plugin.Log("  delante de " + gv.name + " en " + (bv.center - Anclaje.Origen()).ToString("F1") + " tamaño " + bv.size.ToString("F2"));
                    break;
                }
                case "probarprops":
                    plugin.mundo.ProbarProps(arg);
                    break;
                case "buscar":
                {
                    // buscar <texto>: objetos (de la escena o prefabs) cuyo nombre contiene el texto: posición y mallas
                    int n = 0; var vistos = new HashSet<string>();
                    foreach (var g in Resources.FindObjectsOfTypeAll<GameObject>())
                    {
                        if (g == null || g.name.IndexOf(arg, StringComparison.OrdinalIgnoreCase) < 0) continue;
                        bool esc = g.scene.IsValid();
                        if (esc && g.transform.parent != null && g.transform.parent.name.IndexOf(arg, StringComparison.OrdinalIgnoreCase) >= 0) continue;
                        string clave = (esc ? "E:" : "P:") + g.name + (esc ? g.transform.position.ToString("F0") : "");
                        if (!vistos.Add(clave)) continue;
                        if (++n > 40) break;
                        int mallas = 0, act = 0; foreach (var r in g.GetComponentsInChildren<Renderer>(true)) { mallas++; if (r.enabled && r.gameObject.activeInHierarchy) act++; }
                        string mats = ""; foreach (var r in g.GetComponentsInChildren<Renderer>(true)) { foreach (var m in r.sharedMaterials) if (m != null) mats += m.name + (m.mainTexture != null ? "[" + m.mainTexture.name + "] " : " "); break; }
                        plugin.Log("  " + (esc ? "ESCENA " : "prefab ") + g.name + (esc ? " en " + (g.transform.position - Anclaje.Origen()).ToString("F0") + " activo=" + g.activeInHierarchy : "") + " mallas=" + mallas + " visibles=" + act + " mats=" + mats);
                    }
                    break;
                }
                case "nortealturas":
                {
                    Terrain tn = null; foreach (var t0 in Terrain.activeTerrains) if (t0.name == "Coop Terreno Norte") tn = t0;
                    if (tn == null) { plugin.Log("  sin terreno norte"); break; }
                    var tc = tn.GetComponent<TerrainCollider>();
                    plugin.Log("  collider=" + (tc != null ? "sí enabled=" + tc.enabled + " datos=" + (tc.terrainData == tn.terrainData) : "NO") + " activo=" + tn.gameObject.activeInHierarchy + " capa=" + tn.gameObject.layer + " tamaño=" + tn.terrainData.size);
                    var sbx = new System.Text.StringBuilder("  alturas (m): ");
                    foreach (float zz in new[] { 100f, 300f, 500f, 700f, 900f }) { foreach (float xx in new[] { 150f, 500f, 1000f, 1500f, 2000f, 2500f, 2850f }) sbx.Append(tn.terrainData.GetInterpolatedHeight(xx / 3000f, zz / 1000f).ToString("F0")).Append(' '); sbx.Append("| "); }
                    plugin.Log(sbx.ToString());
                    break;
                }
                case "mirarpunto":
                {
                    // mirarpunto x y z (relativo al ancla): gira la cámara hacia ese punto
                    var ml = UnityEngine.Object.FindObjectOfType<SmoothMouseLook>(); var pa = arg.Split(' ');
                    if (ml == null || Camera.main == null || pa.Length < 3) { plugin.Log("  no se puede"); break; }
                    var ci = System.Globalization.CultureInfo.InvariantCulture;
                    Vector3 obj = Anclaje.Origen() + new Vector3(float.Parse(pa[0], ci), float.Parse(pa[1], ci), float.Parse(pa[2], ci));
                    Vector3 dd = obj - Camera.main.transform.position;
                    ml.rotationX = Mathf.DeltaAngle(ml.originalRotation.eulerAngles.y, Mathf.Atan2(dd.x, dd.z) * Mathf.Rad2Deg);
                    ml.rotationY = Mathf.Atan2(dd.y, new Vector2(dd.x, dd.z).magnitude) * Mathf.Rad2Deg;
                    break;
                }
                case "sincolision":
                {
                    // sincolision: objetos grandes con malla visible y sin colisión (se atraviesan) en la zona norte, agrupados por nombre
                    var cuenta = new Dictionary<string, int>(); var ejemplo = new Dictionary<string, string>();
                    foreach (var r in UnityEngine.Object.FindObjectsOfType<MeshRenderer>())
                    {
                        if (!r.enabled || r.isPartOfStaticBatch) continue;
                        Vector3 rel = r.bounds.center - Anclaje.Origen();
                        if (rel.z < 1900f || rel.x < -50f || r.bounds.size.magnitude < 1.5f) continue;
                        bool col = false;
                        for (var t = r.transform; t != null && !col; t = t.parent) { var c0 = t.GetComponent<Collider>(); if (c0 != null && c0.enabled && !c0.isTrigger) col = true; }
                        if (!col) foreach (var c1 in r.GetComponentsInChildren<Collider>()) if (c1.enabled && !c1.isTrigger) { col = true; break; }
                        if (col) continue;
                        string n = Catalogo.BaseDe(r.transform.root.name); int v; cuenta.TryGetValue(n, out v); cuenta[n] = v + 1;
                        if (!ejemplo.ContainsKey(n)) ejemplo[n] = rel.ToString("F0") + " tam " + r.bounds.size.ToString("F1");
                    }
                    foreach (var kv in cuenta) plugin.Log("  SIN colisión: " + kv.Value + " × " + kv.Key + " (ej. " + ejemplo[kv.Key] + ")");
                    if (cuenta.Count == 0) plugin.Log("  ningún objeto grande sin colisión en la zona norte");
                    break;
                }
                case "yo":
                {
                    var jy = UnityEngine.Object.FindObjectOfType<FPSPlayer>();
                    if (jy == null) { plugin.Log("  sin jugador"); break; }
                    Vector3 pr = jy.transform.position - Anclaje.Origen();
                    string suelo = "sin suelo debajo"; foreach (var hy in Physics.RaycastAll(jy.transform.position + Vector3.up * 2f, Vector3.down, 400f, ~0, QueryTriggerInteraction.Ignore)) if (hy.collider.GetComponentInParent<FPSPlayer>() == null) { suelo = hy.collider.name + " a " + (hy.point.y - Anclaje.Origen().y).ToString("F1"); break; }
                    Terrain tt = null; foreach (var t0 in Terrain.activeTerrains) { var o = t0.transform.position; var sz = t0.terrainData.size; if (jy.transform.position.x >= o.x && jy.transform.position.x <= o.x + sz.x && jy.transform.position.z >= o.z && jy.transform.position.z <= o.z + sz.z) tt = t0; }
                    plugin.Log("  yo=" + pr.ToString("F1") + " suelo=" + suelo + " terreno=" + (tt != null ? tt.name + " alturaTerreno=" + (tt.SampleHeight(jy.transform.position) + tt.transform.position.y - Anclaje.Origen().y).ToString("F1") : "ninguno") + " agua=" + plugin.mundo.NivelAguaActual.ToString("F1"));
                    break;
                }
                case "refuerzo": plugin.mundo.ForzarRefuerzo(); break;
                case "auras":
                {
                    foreach (var ps in Resources.FindObjectsOfTypeAll<ParticleSystem>())
                    {
                        if (!ps.gameObject.scene.IsValid() || (ps.name != "coop_humo" && ps.name != "coop_gotas")) continue;
                        var rr = ps.GetComponent<ParticleSystemRenderer>();
                        plugin.Log("  " + ps.name + " reproduciendo=" + ps.isPlaying + " partículas=" + ps.particleCount + " en " + ps.transform.position + " material=" + (rr.sharedMaterial != null ? rr.sharedMaterial.shader.name : "?") + " activo=" + ps.gameObject.activeInHierarchy);
                    }
                    break;
                }
                case "persistentes":
                {
                    // persistentes: recogibles del bosque con registro de "destruido" y cuántos se reiniciaron al entrar
                    int total = 0, marcados = 0; string ej = "";
                    foreach (var pd in Resources.FindObjectsOfTypeAll<PixelCrushers.DialogueSystem.PersistentDestructible>())
                    {
                        if (!pd.gameObject.scene.IsValid() || !RegenerarBotin.EsBotin(pd.gameObject.name)) continue;
                        total++;
                        string v = string.IsNullOrEmpty(pd.variableName) ? PixelCrushers.DialogueSystem.OverrideActorName.GetInternalName(pd.transform) : pd.variableName;
                        if (PixelCrushers.DialogueSystem.DialogueLua.GetVariable(v).AsBool) marcados++;
                        if (ej == "") ej = pd.gameObject.name + " → variable '" + v + "'";
                    }
                    plugin.Log("  recogibles con registro: " + total + " en escena, " + marcados + " marcados como destruidos; reiniciados al entrar: " + RegenerarBotin.Reiniciados + "; ej. " + ej);
                    break;
                }
                case "destruir":
                {
                    // destruir <nombre>: destruye el recogible más cercano de ese tipo (como si lo hubieran recogido)
                    var jj = UnityEngine.Object.FindObjectOfType<FPSPlayer>(); GameObject mejor = null; float md = 1e9f;
                    foreach (var pd in Resources.FindObjectsOfTypeAll<PixelCrushers.DialogueSystem.PersistentDestructible>())
                    {
                        if (!pd.gameObject.scene.IsValid() || !pd.gameObject.activeInHierarchy || Catalogo.BaseDe(pd.gameObject.name) != arg) continue;
                        float dd = jj != null ? (pd.transform.position - jj.transform.position).sqrMagnitude : 0f;
                        if (dd < md) { md = dd; mejor = pd.gameObject; }
                    }
                    if (mejor == null) plugin.Log("  no hay '" + arg + "' con registro"); else { plugin.Log("  destruyendo " + mejor.name + " a " + Mathf.Sqrt(md).ToString("0") + " m"); UnityEngine.Object.Destroy(mejor); }
                    break;
                }
                case "recogibles":
                {
                    // recogibles: objetos interactuables/recogibles de la escena agrupados por tipo
                    var cuenta = new Dictionary<string, int>();
                    var ejemplo = new Dictionary<string, string>();
                    foreach (var c in Resources.FindObjectsOfTypeAll<Collider>())
                    {
                        if (!c.gameObject.scene.IsValid() || c.GetComponent("PickupButton") == null && c.GetComponent<HealthPickup>() == null) continue;
                        Transform t = c.transform;
                        // Nombre del "objeto" = el padre del interactuable si este se llama !Interactable...
                        string n = t.name.StartsWith("!") && t.parent != null ? t.parent.name : t.name;
                        if (n == "interact" && t.parent != null) n = t.parent.name;
                        int i = n.IndexOf(" ("); if (i > 0) n = n.Substring(0, i);
                        int v; cuenta.TryGetValue(n, out v); cuenta[n] = v + 1;
                        if (!ejemplo.ContainsKey(n)) ejemplo[n] = t.position + (c.gameObject.activeInHierarchy ? "" : " inactivo");
                    }
                    var claves = new List<string>(cuenta.Keys); claves.Sort();
                    foreach (var k in claves) plugin.Log("  " + cuenta[k] + " × " + k + "  ej. " + ejemplo[k]);
                    break;
                }
                case "mundo": plugin.Log("  " + plugin.mundo.Describir()); break;
                case "piedras": plugin.Log("  " + plugin.mundo.Piedras()); break;
                case "terrenos":
                {
                    // terrenos: piezas de terreno, tamaño, resolución y alturas de cada borde
                    foreach (var t in Terrain.activeTerrains)
                    {
                        var d = t.terrainData;
                        plugin.Log("  " + t.name + " pos=" + t.transform.position + " tamaño=" + d.size + " altura res=" + d.heightmapResolution + " splat res=" + d.alphamapResolution + " capas=" + d.splatPrototypes.Length + " árboles=" + d.treeInstanceCount + " tipos árbol=" + d.treePrototypes.Length + " detalle res=" + d.detailResolution + " tipos pasto=" + d.detailPrototypes.Length);
                        int r = d.heightmapResolution;
                        float[,] h = d.GetHeights(0, 0, r, r);
                        string[] nom = { "sur(z=0)", "norte(z=max)", "oeste(x=0)", "este(x=max)" };
                        for (int e = 0; e < 4; e++)
                        {
                            float mn = 1, mx = 0, sum = 0;
                            for (int k = 0; k < r; k++)
                            {
                                float v = e == 0 ? h[0, k] : e == 1 ? h[r - 1, k] : e == 2 ? h[k, 0] : h[k, r - 1];
                                mn = Mathf.Min(mn, v); mx = Mathf.Max(mx, v); sum += v;
                            }
                            plugin.Log("    borde " + nom[e] + ": min=" + (mn * d.size.y).ToString("0") + " máx=" + (mx * d.size.y).ToString("0") + " media=" + (sum / r * d.size.y).ToString("0") + " m");
                        }
                        // perfil a 100 m hacia dentro de cada borde (para ver si hay cordillera)
                        int dentro = Mathf.RoundToInt(100f / d.size.x * (r - 1));
                        float s0 = 0, s1 = 0, s2 = 0, s3 = 0;
                        for (int k = 0; k < r; k++) { s0 += h[dentro, k]; s1 += h[r - 1 - dentro, k]; s2 += h[k, dentro]; s3 += h[k, r - 1 - dentro]; }
                        plugin.Log("    a 100 m dentro: sur=" + (s0 / r * d.size.y).ToString("0") + " norte=" + (s1 / r * d.size.y).ToString("0") + " oeste=" + (s2 / r * d.size.y).ToString("0") + " este=" + (s3 / r * d.size.y).ToString("0") + " m");
                    }
                    // Colliders grandes no-trigger que no son terreno (posibles muros invisibles)
                    foreach (var c in UnityEngine.Object.FindObjectsOfType<BoxCollider>())
                    {
                        if (c.isTrigger || c.GetComponent<Renderer>() != null) continue;
                        var sz = c.bounds.size;
                        if (Mathf.Max(sz.x, sz.z) < 150f) continue;
                        plugin.Log("  muro? " + RutaDe(c.transform, null) + " centro=" + c.bounds.center + " tam=" + sz);
                    }
                    break;
                }
                case "perfilnorte":
                {
                    // perfilnorte: alturas a lo largo del borde norte (z=máx) cada 100 m, y a 30/60/100/150 m dentro; y si hay NavMesh
                    var t = Anclaje.Principal(); if (t == null) break;
                    var d = t.terrainData; Vector3 o = t.transform.position;
                    for (float x = 50; x < d.size.x; x += 100)
                    {
                        var sb = new System.Text.StringBuilder("  x=" + x + ":");
                        foreach (float den in new float[] { 0, 30, 60, 100, 150, 250 })
                        {
                            Vector3 p = o + new Vector3(x, 0, d.size.z - 1 - den);
                            float h = t.SampleHeight(p);
                            UnityEngine.AI.NavMeshHit nh;
                            bool nav = UnityEngine.AI.NavMesh.SamplePosition(p + Vector3.up * h, out nh, 4f, UnityEngine.AI.NavMesh.AllAreas);
                            sb.Append(" ").Append(den).Append("m→").Append(h.ToString("0")).Append(nav ? "N" : "");
                        }
                        plugin.Log(sb.ToString());
                    }
                    break;
                }
                case "agua":
                {
                    // agua: objetos de agua (renderers o zonas) con su altura y extensión
                    foreach (var g in UnityEngine.Object.FindObjectsOfType<GameObject>())
                    {
                        if (g.name.IndexOf("water", StringComparison.OrdinalIgnoreCase) < 0 && g.name.IndexOf("aquas", StringComparison.OrdinalIgnoreCase) < 0 && g.name.IndexOf("river", StringComparison.OrdinalIgnoreCase) < 0 && g.name.IndexOf("lake", StringComparison.OrdinalIgnoreCase) < 0) continue;
                        var r = g.GetComponent<Renderer>(); var c = g.GetComponent<Collider>();
                        if (r == null && c == null) continue;
                        Bounds b = r != null ? r.bounds : c.bounds;
                        if (Mathf.Max(b.size.x, b.size.z) < 30f) continue;
                        plugin.Log("  " + RutaDe(g.transform, null) + " y=" + b.center.y.ToString("0.0") + " x " + b.min.x.ToString("0") + "–" + b.max.x.ToString("0") + " z " + b.min.z.ToString("0") + "–" + b.max.z.ToString("0") + (c != null ? " col" + (c.isTrigger ? "(trigger)" : "") : "") + (r != null ? " visible" : ""));
                    }
                    break;
                }
                case "prefabs":
                {
                    // prefabs: modelos de enemigos (con TakeDamage) cargados en memoria que se pueden generar
                    foreach (var g in Resources.FindObjectsOfTypeAll<GameObject>())
                        if (!g.scene.IsValid() && g.transform.parent == null && g.GetComponentInChildren<TakeDamage>(true) != null)
                            plugin.Log("  " + g.name);
                    break;
                }
                case "expansion": plugin.Log("  " + plugin.mundo.DescribirExpansion()); plugin.Log("  " + Mundo.DiagnosticoRespaldo()); break;
                case "colisiones":
                {
                    // colisiones <radio>: colliders sólidos (no trigger) a ese radio del jugador
                    var j = UnityEngine.Object.FindObjectOfType<FPSPlayer>();
                    float r = arg.Length > 0 ? float.Parse(arg, System.Globalization.CultureInfo.InvariantCulture) : 5f;
                    foreach (var c in Physics.OverlapSphere(j.transform.position + Vector3.forward * 1f, r))
                    {
                        if (c.isTrigger || c.transform.IsChildOf(j.transform)) continue;
                        plugin.Log("  " + RutaDe(c.transform, null) + " [" + c.GetType().Name + "] centro=" + (c.bounds.center - Anclaje.Origen()) + " tam=" + c.bounds.size + " capa=" + LayerMask.LayerToName(c.gameObject.layer));
                    }
                    break;
                }
                case "camino2":
                {
                    // camino2 x1 z1 x2 z2: camino entre dos puntos (relativos) sobre el NavMesh
                    var ps = arg.Split(' '); var ci = System.Globalization.CultureInfo.InvariantCulture;
                    Vector3 o = Anclaje.Origen();
                    Vector3[] pt = new Vector3[2];
                    for (int k = 0; k < 2; k++)
                    {
                        Vector3 p = o + new Vector3(float.Parse(ps[k * 2], ci), 0f, float.Parse(ps[k * 2 + 1], ci));
                        foreach (var t in Terrain.activeTerrains) { var s0 = t.terrainData.size; var to = t.transform.position; if (p.x >= to.x && p.z >= to.z && p.x <= to.x + s0.x && p.z <= to.z + s0.z) p.y = t.SampleHeight(p) + to.y; }
                        UnityEngine.AI.NavMeshHit nh;
                        if (!UnityEngine.AI.NavMesh.SamplePosition(p, out nh, 12f, UnityEngine.AI.NavMesh.AllAreas)) { plugin.Log("  sin navmesh en punto " + (k + 1)); goto fin2; }
                        pt[k] = nh.position;
                    }
                    var path = new UnityEngine.AI.NavMeshPath();
                    UnityEngine.AI.NavMesh.CalculatePath(pt[0], pt[1], UnityEngine.AI.NavMesh.AllAreas, path);
                    plugin.Log("  " + path.status + " esquinas=" + path.corners.Length + (path.corners.Length > 0 ? " termina en " + (path.corners[path.corners.Length - 1] - o) : ""));
                    fin2:
                    break;
                }
                case "camino":
                {
                    // camino x z: ¿se llega a pie desde la llegada del bosque a ese punto (relativo)?
                    var ps = arg.Split(' ');
                    var ci = System.Globalization.CultureInfo.InvariantCulture;
                    Vector3 o = Anclaje.Origen();
                    Vector3 p = o + new Vector3(float.Parse(ps[0], ci), 0f, float.Parse(ps[1], ci));
                    float y = float.NaN;
                    foreach (var t in Terrain.activeTerrains) { var s0 = t.terrainData.size; var to = t.transform.position; if (p.x >= to.x && p.z >= to.z && p.x <= to.x + s0.x && p.z <= to.z + s0.z) y = t.SampleHeight(p) + to.y; }
                    p.y = y;
                    UnityEngine.AI.NavMeshHit a, b = new UnityEngine.AI.NavMeshHit();
                    bool ha = UnityEngine.AI.NavMesh.SamplePosition(o + Mundo.LlegadaRel, out a, 20f, UnityEngine.AI.NavMesh.AllAreas);
                    bool hb = !float.IsNaN(y) && UnityEngine.AI.NavMesh.SamplePosition(p, out b, 12f, UnityEngine.AI.NavMesh.AllAreas);
                    var path = new UnityEngine.AI.NavMeshPath();
                    string r = "altura=" + y.ToString("0.0") + " navmesh origen=" + ha + " destino=" + hb;
                    if (ha && hb) { UnityEngine.AI.NavMesh.CalculatePath(a.position, b.position, UnityEngine.AI.NavMesh.AllAreas, path); r += " camino=" + path.status + " esquinas=" + path.corners.Length; }
                    plugin.Log("  " + r);
                    break;
                }
                case "puestos": plugin.Log("  " + plugin.mundo.DescribirPuestos()); break;
                case "irpuesto":
                {
                    // irpuesto <n>: a 15 m del puesto n, mirándolo
                    GameObject g = null;
                    foreach (var x in UnityEngine.Object.FindObjectsOfType<GameObject>()) if (x.name.StartsWith("coop_puesto_" + arg + "_")) { g = x; break; }
                    var j = UnityEngine.Object.FindObjectOfType<FPSPlayer>();
                    if (g == null || j == null) { plugin.Log("  no hay puesto " + arg); break; }
                    Bounds b = new Bounds(g.transform.position, Vector3.one); bool pr = true;
                    foreach (var r in g.GetComponentsInChildren<MeshRenderer>()) if (r.enabled) { if (pr) { b = r.bounds; pr = false; } else b.Encapsulate(r.bounds); }
                    Vector3 p = b.center + new Vector3(10f + b.extents.x, 0f, 10f + b.extents.z);
                    UnityEngine.AI.NavMeshHit nh;
                    if (UnityEngine.AI.NavMesh.SamplePosition(p, out nh, 15f, UnityEngine.AI.NavMesh.AllAreas)) p = nh.position;
                    j.transform.position = p + Vector3.up * 1.2f;
                    var ml = UnityEngine.Object.FindObjectOfType<SmoothMouseLook>();
                    if (ml != null) { Vector3 d = b.center - (p + Vector3.up * 1.6f); ml.rotationX = Mathf.DeltaAngle(ml.originalRotation.eulerAngles.y, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg); ml.rotationY = Mathf.Clamp(Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg, -30f, 30f); }
                    plugin.Log("  junto al puesto " + g.name);
                    break;
                }
                case "irpiedra":
                {
                    // irpiedra <n>: se pone a 4 m de la piedra de guardado n, mirándola
                    var g = GameObject.Find("coop_save_stone_" + arg);
                    var j = UnityEngine.Object.FindObjectOfType<FPSPlayer>();
                    if (g == null || j == null) { plugin.Log("  no hay piedra " + arg); break; }
                    Vector3 p = g.transform.position + new Vector3(3f, 1.5f, 3f);
                    UnityEngine.AI.NavMeshHit nh;
                    if (UnityEngine.AI.NavMesh.SamplePosition(p, out nh, 5f, UnityEngine.AI.NavMesh.AllAreas)) p = nh.position + Vector3.up * 1.2f;
                    j.transform.position = p;
                    var ml = UnityEngine.Object.FindObjectOfType<SmoothMouseLook>();
                    if (ml != null) { Vector3 d = g.transform.position + Vector3.up - p; ml.rotationX = Mathf.DeltaAngle(ml.originalRotation.eulerAngles.y, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg); ml.rotationY = -10f; }
                    plugin.Log("  junto a la piedra " + arg);
                    break;
                }
                case "donde": plugin.Log("  " + plugin.mundo.Donde(arg)); break;
                case "recoger":
                {
                    // recoger <tipo>: va al objeto más cercano de ese tipo y lo "recoge" (lo desactiva)
                    var j = UnityEngine.Object.FindObjectOfType<FPSPlayer>();
                    GameObject mejor = null; float dm = 1e9f;
                    foreach (var g in UnityEngine.Object.FindObjectsOfType<GameObject>())
                    {
                        if (!g.name.StartsWith(arg) || j == null) continue;
                        float d = Vector3.Distance(g.transform.position, j.transform.position);
                        if (d < dm) { dm = d; mejor = g; }
                    }
                    if (mejor == null) { plugin.Log("  no hay " + arg); break; }
                    j.transform.position = mejor.transform.position + Vector3.up * 1.5f;
                    plugin.Log("  recogiendo " + mejor.name + " en " + mejor.transform.position);
                    mejor.SetActive(false);
                    break;
                }
                case "hijos":
                {
                    // hijos <objeto>: jerarquía con componentes
                    GameObject raiz = null;
                    foreach (var g in Resources.FindObjectsOfTypeAll<GameObject>())
                        if (g.name == arg && g.scene.IsValid()) { raiz = g; break; }
                    if (raiz == null) { plugin.Log("  no existe"); break; }
                    foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
                    {
                        var sb = new System.Text.StringBuilder();
                        foreach (var c in t.GetComponents<Component>())
                        {
                            if (c == null || c is Transform) continue;
                            sb.Append(c is PlayMakerFSM ? "FSM:" + ((PlayMakerFSM)c).FsmName : c.GetType().Name).Append(' ');
                        }
                        plugin.Log("  " + RutaDe(t, raiz.transform) + (t.gameObject.activeSelf ? "" : " (inactivo)") + " [" + sb + "]");
                    }
                    break;
                }
                case "pulsar":
                {
                    // pulsar <raíz>: envía el evento global del primer FSM "Button" bajo ese objeto raíz
                    foreach (var f in UnityEngine.Object.FindObjectsOfType<PlayMakerFSM>())
                    {
                        if (f.FsmName != "Button" || f.transform.root.name != arg) continue;
                        var gt = f.Fsm.GlobalTransitions;
                        if (gt == null || gt.Length == 0) continue;
                        plugin.Log("  pulsando " + RutaDe(f.transform, null) + " ('" + gt[0].EventName + "', estado '" + f.ActiveStateName + "')");
                        f.Fsm.Event(gt[0].FsmEvent);
                        break;
                    }
                    break;
                }
                case "evraiz":
                {
                    // evraiz <objeto raíz> <fsm> <evento>: manda el evento a la máquina cuyo objeto raíz se llame así
                    var pa = arg.Contains("|") ? arg.Split('|') : arg.Split(' ');
                    foreach (var f in Resources.FindObjectsOfTypeAll<PlayMakerFSM>())
                        if (f.gameObject.scene.IsValid() && f.FsmName == pa[1] && f.transform.root.name == pa[0])
                        {
                            plugin.Log("  evento " + pa[2] + " a " + f.transform.root.name + "/" + f.gameObject.name + "/" + f.FsmName);
                            f.Fsm.Event(HutongGames.PlayMaker.FsmEvent.GetFsmEvent(pa[2]));
                            break;
                        }
                    break;
                }
                case "ctrl":
                {
                    // ctrl <id> <KeyCode|restablecer>: cambia una tecla (prueba)
                    var pa = arg.Split(' ');
                    Controles.Iniciar(plugin);
                    if (pa[0] == "restablecer") { Controles.Restablecer(); plugin.Log("  restablecido"); break; }
                    foreach (var a0 in Controles.Lista) if (a0.Id == pa[0]) { Controles.Asignar(a0, (KeyCode)System.Enum.Parse(typeof(KeyCode), pa[1])); plugin.Log("  " + a0.Nombre + " = " + Controles.Texto(a0) + " " + Controles.Aviso); }
                    break;
                }
                case "controles":
                {
                    var pl = Rewired.ReInput.players.GetPlayer(0);
                    foreach (var a in Rewired.ReInput.mapping.Actions)
                    {
                        var sbc = new System.Text.StringBuilder();
                        foreach (var ct in new[] { Rewired.ControllerType.Keyboard, Rewired.ControllerType.Mouse })
                            foreach (var m in pl.controllers.maps.GetMaps(ct, 0))
                                foreach (var em in m.GetElementMapsWithAction(a.id))
                                    sbc.Append(ct.ToString()[0]).Append(':').Append(em.elementIdentifierName).Append(em.axisContribution == Rewired.Pole.Negative ? "(-)" : "").Append(m.enabled ? "" : "[map off]").Append(' ');
                        plugin.Log("  #" + a.id + " " + a.name + " | " + a.descriptiveName + " | " + a.type + " | cat " + a.categoryId + " | " + sbc);
                    }
                    break;
                }
                case "escopetamodo": Escopeta.Doble = arg != "uno"; plugin.Log("  escopeta: " + (Escopeta.Doble ? "disparo doble" : "un cañón")); break;
                case "ping":
                    if (arg.Length > 0) plugin.PingFalsoMs = arg == "real" ? -1f : float.Parse(arg, System.Globalization.CultureInfo.InvariantCulture);
                    plugin.Log("  retardo " + plugin.RetardoMs.ToString("0") + " ms" + (plugin.PingFalsoMs >= 0f ? " (fijado para la prueba)" : ""));
                    break;
                case "tiendarueda": plugin.Log("  " + plugin.tiendaUI.Desplazar(float.Parse(arg, System.Globalization.CultureInfo.InvariantCulture)) + " → " + plugin.tiendaUI.InfoLista()); break;
                case "eqrueda": plugin.tiendaUI.DesplazarEq(float.Parse(arg, System.Globalization.CultureInfo.InvariantCulture)); plugin.Log("  " + plugin.tiendaUI.InfoEq()); break;
                case "invrueda": plugin.tiendaUI.DesplazarHab(float.Parse(arg, System.Globalization.CultureInfo.InvariantCulture)); plugin.Log("  " + plugin.tiendaUI.InfoHab()); break;
                case "tiendainfo": plugin.Log("  " + plugin.tiendaUI.InfoLista()); break;
                case "tiendafilas": plugin.Log("  " + plugin.tiendaUI.FilasMod()); break;   // filas del mod: posición en pantalla, visibles, rojas
                case "tiendaresaltar": plugin.Log("  " + plugin.tiendaUI.ProbarResaltado(arg.Length > 0 ? int.Parse(arg) : -1)); break;   // tiendaresaltar [n]: ratón simulado por cada fila del mod (deja el ratón en la fila n)
                case "raton":   // raton x y: ratón simulado para el resaltado de la tienda | raton: vuelve al ratón real
                {
                    var pr = arg.Split(' ');
                    var ci = System.Globalization.CultureInfo.InvariantCulture;
                    TiendaUI.RatonPrueba = pr.Length >= 2 ? (Vector2?)new Vector2(float.Parse(pr[0], ci), float.Parse(pr[1], ci)) : null;
                    plugin.Log("  ratón de prueba " + (TiendaUI.RatonPrueba.HasValue ? TiendaUI.RatonPrueba.Value.ToString("F0") : "apagado (ratón real)"));
                    break;
                }
                case "pausa": plugin.Log("  " + plugin.pantalla.OrdenPausa(arg)); break;
                case "votar": plugin.Log("  " + (arg == "no" ? Viaje.ResponderPrueba(false) : Viaje.ResponderPrueba(true))); plugin.Log("  " + Viaje.EstadoVoto()); break;
                case "votoestado": plugin.Log("  " + Viaje.EstadoVoto()); break;
                case "getglobal":
                {
                    // getglobal <filtro>: variables globales de PlayMaker cuyo nombre contenga el texto, con su valor
                    var gv = HutongGames.PlayMaker.FsmVariables.GlobalVariables; int nn = 0;
                    foreach (var v in gv.BoolVariables) if (v.Name.IndexOf(arg, System.StringComparison.OrdinalIgnoreCase) >= 0) { plugin.Log("  bool " + v.Name + " = " + v.Value); nn++; }
                    foreach (var v in gv.IntVariables) if (v.Name.IndexOf(arg, System.StringComparison.OrdinalIgnoreCase) >= 0) { plugin.Log("  int " + v.Name + " = " + v.Value); nn++; }
                    foreach (var v in gv.StringVariables) if (v.Name.IndexOf(arg, System.StringComparison.OrdinalIgnoreCase) >= 0) { plugin.Log("  string " + v.Name + " = " + v.Value); nn++; }
                    plugin.Log("  coincidencias: " + nn);
                    break;
                }
                case "arbol":
                {
                    // arbol <nombre>: jerarquía (hasta 4 niveles) con componentes de cada objeto
                    foreach (var g0 in Resources.FindObjectsOfTypeAll<GameObject>())
                    {
                        if (g0.name != arg || !g0.scene.IsValid()) continue;
                        System.Action<Transform, int> rec = null;
                        rec = (t, d) =>
                        {
                            var cs = new System.Text.StringBuilder();
                            foreach (var c in t.GetComponents<Component>()) { if (c == null || c is Transform) continue; cs.Append(c.GetType().Name).Append(' '); var fs = c as PlayMakerFSM; if (fs != null) cs.Append("[").Append(fs.FsmName).Append("] "); }
                            plugin.Log("  " + new string(' ', d * 2) + t.name + " activo=" + t.gameObject.activeSelf + " pos=" + t.position.ToString("F0") + " : " + cs);
                            if (d < 4) foreach (Transform h in t) rec(h, d + 1);
                        };
                        rec(g0.transform, 0);
                        break;
                    }
                    break;
                }
                case "global":
                {
                    // global <nombre> <valor>: cambia una variable global de PlayMaker
                    var ps = arg.Split(' ');
                    var g = HutongGames.PlayMaker.FsmVariables.GlobalVariables;
                    var ci = System.Globalization.CultureInfo.InvariantCulture;
                    var vf = g.FindFsmFloat(ps[0]); if (vf != null) vf.Value = float.Parse(ps[1], ci);
                    var vi = g.FindFsmInt(ps[0]); if (vi != null) vi.Value = int.Parse(ps[1]);
                    var vb = g.FindFsmBool(ps[0]); if (vb != null) vb.Value = ps[1] == "true";
                    plugin.Log("  " + ps[0] + " = " + ps[1]);
                    break;
                }
                case "estructuras":
                {
                    // estructuras: objetos raíz con varias mallas (edificios, ruinas...), con horneado y posición
                    var cuenta = new Dictionary<Transform, int[]>();
                    foreach (var r in UnityEngine.Object.FindObjectsOfType<MeshRenderer>())
                    {
                        Transform raiz = r.transform.root;
                        int[] c; if (!cuenta.TryGetValue(raiz, out c)) cuenta[raiz] = c = new int[3];
                        c[0]++; if (r.isPartOfStaticBatch) c[1]++;
                        if (r.GetComponent<Collider>() != null) c[2]++;
                    }
                    var l = new List<KeyValuePair<Transform, int[]>>(cuenta);
                    l.Sort((a, b) => b.Value[0].CompareTo(a.Value[0]));
                    Vector3 an = Anclaje.Origen();
                    int n = 0;
                    foreach (var kv in l)
                    {
                        if (kv.Value[0] < (arg.Length > 0 ? int.Parse(arg) : 3) || n++ > 70) continue;
                        plugin.Log("  " + kv.Key.name + " mallas=" + kv.Value[0] + " horneadas=" + kv.Value[1] + " colisión=" + kv.Value[2] + " pos=" + (kv.Key.position - an) + " hijos=" + kv.Key.childCount);
                    }
                    break;
                }
                case "clonar":
                {
                    // clonar <raíz> [distancia]: copia reconstruida delante del jugador
                    string[] pa = arg.Split('|');
                    var g = GameObject.Find(pa[0]);
                    var cam = Camera.main;
                    if (g == null || cam == null) { plugin.Log("  no existe " + pa[0]); break; }
                    float dist = pa.Length > 1 ? float.Parse(pa[1], System.Globalization.CultureInfo.InvariantCulture) : 12f;
                    Vector3 fw = cam.transform.forward; fw.y = 0; fw.Normalize();
                    Vector3 p = cam.transform.position + fw * dist;
                    if (Anclaje.Principal() != null) p.y = Anclaje.Principal().SampleHeight(p) + Anclaje.Origen().y;
                    // Mantener la altura de la raíz respecto al suelo del original
                    float off = 0f;
                    if (Anclaje.Principal() != null) off = g.transform.position.y - (Anclaje.Principal().SampleHeight(g.transform.position) + Anclaje.Origen().y);
                    int faltan;
                    var c = Coop.Piezas.Clonar(g, p + Vector3.up * off, g.transform.rotation, out faltan);
                    c.name = "clon_" + g.name;
                    plugin.Log("  clonado " + g.name + " en " + p + " (faltan " + faltan + " piezas)");
                    break;
                }
                case "borrarclones":
                    foreach (var g in UnityEngine.Object.FindObjectsOfType<GameObject>()) if (g.name.StartsWith("clon_") && g.transform.parent == null) UnityEngine.Object.Destroy(g);
                    break;
                case "clonables":
                {
                    // clonables [mín. mallas]: % de mallas de cada estructura que existen sueltas y legibles en memoria
                    var sueltas = new Dictionary<string, Mesh>();
                    foreach (var mm in Resources.FindObjectsOfTypeAll<Mesh>())
                        if (mm.isReadable && !mm.name.StartsWith("Combined Mesh") && !sueltas.ContainsKey(mm.name)) sueltas[mm.name] = mm;
                    plugin.Log("  mallas sueltas legibles: " + sueltas.Count);
                    var cuenta = new Dictionary<Transform, int[]>();
                    foreach (var r in UnityEngine.Object.FindObjectsOfType<MeshRenderer>())
                    {
                        Transform raiz = r.transform.root;
                        int[] c; if (!cuenta.TryGetValue(raiz, out c)) cuenta[raiz] = c = new int[2];
                        c[0]++;
                        if (Coop.Piezas.MallaDe(r, sueltas) != null) c[1]++;
                    }
                    var l = new List<KeyValuePair<Transform, int[]>>(cuenta);
                    l.Sort((a, b) => b.Value[0].CompareTo(a.Value[0]));
                    int min = arg.Length > 0 ? int.Parse(arg) : 4, n = 0;
                    foreach (var kv in l)
                        if (kv.Value[0] >= min && n++ < 60)
                            plugin.Log("  " + kv.Key.name + ": " + kv.Value[1] + "/" + kv.Value[0] + " (" + (100 * kv.Value[1] / kv.Value[0]) + " %)");
                    break;
                }
                case "malla":
                {
                    // malla <raíz>: datos de la primera malla horneada bajo ese objeto (¿se puede recortar?)
                    var g = GameObject.Find(arg);
                    if (g == null) { plugin.Log("  no existe"); break; }
                    int n = 0;
                    foreach (var r in g.GetComponentsInChildren<MeshRenderer>())
                    {
                        if (n++ > 2) break;
                        var mf = r.GetComponent<MeshFilter>();
                        var m = mf != null ? mf.sharedMesh : null;
                        var sb = new System.Text.StringBuilder();
                        foreach (var pr in typeof(Renderer).GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic))
                            if (pr.Name.IndexOf("ubMesh", StringComparison.OrdinalIgnoreCase) >= 0 || pr.Name.IndexOf("atch", StringComparison.OrdinalIgnoreCase) >= 0)
                            { object v = null; try { v = pr.GetValue(r, null); } catch (Exception e) { v = "error " + e.GetType().Name; } sb.Append(pr.Name).Append('=').Append(v).Append(' '); }
                        foreach (var pr in typeof(MeshRenderer).GetProperties())
                            if (pr.Name.IndexOf("ubMesh", StringComparison.OrdinalIgnoreCase) >= 0)
                            { object v = null; try { v = pr.GetValue(r, null); } catch { } sb.Append("MR.").Append(pr.Name).Append('=').Append(v).Append(' '); }
                        plugin.Log("  " + r.name + " horneada=" + r.isPartOfStaticBatch + " malla=" + (m != null ? m.name + " legible=" + m.isReadable + " vértices=" + m.vertexCount + " submallas=" + m.subMeshCount : "null") + " materiales=" + r.sharedMaterials.Length + " | " + sb);
                    }
                    int mallasSueltas = 0;
                    foreach (var mm in Resources.FindObjectsOfTypeAll<Mesh>()) if (mm.name.StartsWith("cabin")) { mallasSueltas++; if (mallasSueltas < 4) plugin.Log("  malla en memoria: " + mm.name + " legible=" + mm.isReadable + " v=" + mm.vertexCount); }
                    plugin.Log("  mallas 'cabin*' en memoria: " + mallasSueltas);
                    break;
                }
                case "estatico":
                {
                    // estatico <filtro>: para objetos raíz con ese nombre, cuántos renderers están horneados (static batching)
                    var vistos = new Dictionary<string, int[]>();
                    foreach (var r in UnityEngine.Object.FindObjectsOfType<MeshRenderer>())
                    {
                        Transform raiz = r.transform.root;
                        if (raiz.name.IndexOf(arg, StringComparison.OrdinalIgnoreCase) < 0) continue;
                        int[] c; if (!vistos.TryGetValue(raiz.name, out c)) vistos[raiz.name] = c = new int[2];
                        c[0]++; if (r.isPartOfStaticBatch) c[1]++;
                    }
                    foreach (var kv in vistos) plugin.Log("  " + kv.Key + ": " + kv.Value[0] + " mallas, " + kv.Value[1] + " horneadas, estático=" + GameObject.Find(kv.Key).isStatic);
                    break;
                }
                case "triggers":
                {
                    // triggers <radio>: colliders de tipo trigger cerca del jugador
                    var j = UnityEngine.Object.FindObjectOfType<FPSPlayer>();
                    float r = arg.Length > 0 ? float.Parse(arg, System.Globalization.CultureInfo.InvariantCulture) : 30f;
                    foreach (var c in UnityEngine.Object.FindObjectsOfType<Collider>())
                    {
                        if (!c.isTrigger || j == null) continue;
                        float d = Vector3.Distance(c.bounds.center, j.transform.position);
                        if (d > r) continue;
                        var fs = new System.Text.StringBuilder();
                        foreach (var f in c.GetComponents<PlayMakerFSM>()) fs.Append(f.FsmName).Append(',');
                        foreach (var m in c.GetComponents<MonoBehaviour>()) if (!(m is PlayMakerFSM)) fs.Append(m.GetType().Name).Append(',');
                        plugin.Log("  " + RutaDe(c.transform, null) + " centro=" + c.bounds.center + " tam=" + c.bounds.size + " dist=" + d.ToString("0") + " [" + fs + "]");
                    }
                    break;
                }
                case "viaje":
                {
                    // viaje: dice si se permitiría; viaje bosque: activa el botón real del puente
                    plugin.Log("  " + Viaje.Probar());
                    if (arg == "lista") { foreach (var x in Viaje.Listar()) plugin.Log("  " + x); break; }
                    if (arg == "pulsar")
                    {
                        // Simula pulsar el botón del puente: envía su evento global (como el juego).
                        // el botón de viaje más cercano (puente del pueblo o del bosque, cartel…)
                        PlayMakerFSM mejorF = null; float mejorD = 1e9f; var jp = UnityEngine.Object.FindObjectOfType<FPSPlayer>();
                        foreach (var f0 in UnityEngine.Object.FindObjectsOfType<PlayMakerFSM>())
                        {
                            if (f0.FsmName != "Button" || Viaje.DestinoDe(f0.Fsm) == null || f0.Fsm.GlobalTransitions == null || f0.Fsm.GlobalTransitions.Length == 0) continue;
                            float d0 = jp != null ? Vector3.Distance(jp.transform.position, f0.transform.position) : 0f;
                            if (d0 < mejorD) { mejorD = d0; mejorF = f0; }
                        }
                        if (mejorF != null) plugin.Log("  botón " + RutaDe(mejorF.transform, null) + " → " + Viaje.DestinoDe(mejorF.Fsm) + " a " + mejorD.ToString("0") + " m");
                        foreach (var f in mejorF != null ? new[] { mejorF } : new PlayMakerFSM[0])
                        {
                            var gt = f.Fsm.GlobalTransitions;
                            plugin.Log("  estado antes='" + f.ActiveStateName + "' globales=" + (gt == null ? 0 : gt.Length));
                            if (gt != null && gt.Length > 0) { plugin.Log("  enviando '" + gt[0].EventName + "'"); f.Fsm.Event(gt[0].FsmEvent); }
                            plugin.Log("  estado después='" + f.ActiveStateName + "'");
                        }
                        break;
                    }
                    if (arg != "bosque") break;
                    plugin.Log("  botón activado=" + Viaje.Activar(Viaje.Bosque, false));
                    break;
                }
                case "buscarvar":   // buscarvar <nombre>: qué FSM (estado y acción) usan esa variable (global o local)
                {
                    foreach (var f in Resources.FindObjectsOfTypeAll<PlayMakerFSM>())
                    {
                        if (!f.gameObject.scene.IsValid() || f.FsmStates == null) continue;
                        foreach (var st in f.FsmStates)
                        {
                            if (st.Actions == null) continue;
                            foreach (var a in st.Actions)
                            {
                                if (a == null) continue;
                                foreach (var c in a.GetType().GetFields())
                                {
                                    var nv = c.GetValue(a) as HutongGames.PlayMaker.NamedVariable;
                                    if (nv == null || nv.Name != arg) continue;
                                    plugin.Log("  " + RutaDe(f.transform, null) + "/" + f.FsmName + " [" + st.Name + "] " + a.GetType().Name + "." + c.Name + " activo=" + f.gameObject.activeInHierarchy + " estado=" + f.ActiveStateName);
                                }
                            }
                        }
                    }
                    break;
                }
                case "cargas":
                {
                    // cargas: acciones de PlayMaker que cargan otro nivel, y componentes ChangeLevel
                    var j = UnityEngine.Object.FindObjectOfType<FPSPlayer>();
                    foreach (var f in Resources.FindObjectsOfTypeAll<PlayMakerFSM>())
                    {
                        if (!f.gameObject.scene.IsValid() || f.FsmStates == null) continue;
                        foreach (var st in f.FsmStates)
                        {
                            if (st.Actions == null) continue;
                            foreach (var a in st.Actions)
                            {
                                if (a == null) continue;
                                string tn = a.GetType().Name;
                                if (tn.IndexOf("Load", StringComparison.OrdinalIgnoreCase) < 0 || (tn.IndexOf("Level", StringComparison.OrdinalIgnoreCase) < 0 && tn.IndexOf("Scene", StringComparison.OrdinalIgnoreCase) < 0)) continue;
                                var sb = new System.Text.StringBuilder();
                                foreach (var c in a.GetType().GetFields()) { object v = null; try { v = c.GetValue(a); } catch { } sb.Append(c.Name).Append('=').Append(v).Append(' '); }
                                float d = j != null ? Vector3.Distance(j.transform.position, f.transform.position) : -1f;
                                plugin.Log("  " + RutaDe(f.transform, null) + "/" + f.FsmName + " [" + st.Name + "] " + tn + " activo=" + f.gameObject.activeInHierarchy + " pos=" + f.transform.position + " dist=" + d.ToString("0") + " :: " + sb);
                            }
                        }
                    }
                    foreach (var cl in Resources.FindObjectsOfTypeAll<PixelCrushers.DialogueSystem.RealisticFPSPrefab.ChangeLevel>())
                        if (cl.gameObject.scene.IsValid()) plugin.Log("  ChangeLevel " + RutaDe(cl.transform, null) + " → " + cl.newLevel + " pos=" + cl.transform.position);
                    break;
                }
                case "objetivo": objetivo = arg; plugin.Log("  objetivo '" + arg + "'"); break;
                case "jefe":
                {
                    var td = MasCercano();
                    plugin.Log(td == null ? "  no hay monstruos" : "  " + td.transform.root.name + " jefe=" + Dificultad.EsJefe(td) + " coop=" + Dificultad.Activa);
                    break;
                }
                case "arma":
                {
                    var pw = UnityEngine.Object.FindObjectOfType<PlayerWeapons>();
                    if (pw == null) break;
                    plugin.Log("  cambiando a " + arg + " (switching=" + pw.switching + ", desde nivel " + Time.timeSinceLevelLoad.ToString("0") + " s)");
                    pw.StartCoroutine(pw.SelectWeapon(int.Parse(arg)));
                    break;
                }
                case "armas":
                {
                    var pw = UnityEngine.Object.FindObjectOfType<PlayerWeapons>();
                    var wk = UnityEngine.Object.FindObjectOfType<FPSRigidBodyWalker>();
                    if (pw == null) { plugin.Log("  sin PlayerWeapons"); break; }
                    if (arg == "permitir") pw.NoWeaponsAllowed = false;
                    plugin.Log("  prohibidas=" + pw.NoWeaponsAllowed + " actual=" + pw.currentWeapon + " ocultar=" + (wk != null && wk.hideWeapon));
                    for (int k = 0; k < pw.weaponOrder.Length; k++)
                    {
                        var wb = pw.weaponOrder[k] == null ? null : pw.weaponOrder[k].GetComponent<WeaponBehavior>();
                        if (wb == null) continue;
                        if (arg == "todas" && k > 0) wb.haveWeapon = true;
                        plugin.Log("  " + k + " " + pw.weaponOrder[k].name + " tiene=" + wb.haveWeapon);
                    }
                    break;
                }
                case "generar":
                {
                    var j = UnityEngine.Object.FindObjectOfType<FPSPlayer>();
                    if (j == null || Combate.Instancia == null || Camera.main == null) { plugin.Log("  sin jugador"); break; }
                    string[] a = arg.Split(' ');
                    float dist = a.Length > 1 ? float.Parse(a[1], System.Globalization.CultureInfo.InvariantCulture) : 10f;
                    Vector3 f = Camera.main.transform.forward; f.y = 0; f.Normalize();
                    Combate.Instancia.Generar(a[0], j.transform.position + f * dist + Vector3.down * 0.9f);
                    break;
                }
                case "pielesperro":
                {
                    var sbp = new System.Text.StringBuilder();
                    foreach (var t in Resources.FindObjectsOfTypeAll<Texture2D>()) if (t != null && (t.name.StartsWith("zd_") || t.name.ToLowerInvariant().Contains("dog"))) sbp.Append(t.name).Append(' ').Append(t.width).Append("x").Append(t.height).Append("; ");
                    plugin.Log("  texturas de perro: " + sbp);
                    break;
                }
                case "perrofoto":
                {
                    // perrofoto <prefab> <n>: un perro enemigo quieto delante de la cámara con la piel zd_0<n> (para fotografiarlo)
                    var j = UnityEngine.Object.FindObjectOfType<FPSPlayer>(); var a = arg.Split(' ');
                    if (j == null || Camera.main == null || a.Length < 2) break;
                    foreach (var viejo in GameObject.FindObjectsOfType<Transform>()) if (viejo != null && viejo.name == "coop_perrofoto") UnityEngine.Object.Destroy(viejo.gameObject);
                    Vector3 f = Camera.main.transform.forward; f.y = 0; f.Normalize();
                    var td = Combate.Instancia.Generar(a[0], j.transform.position + f * 3.2f + Vector3.down * 0.9f);
                    if (td == null) break;
                    var raiz = Combate.RaizDe(td).gameObject; raiz.name = "coop_perrofoto";
                    foreach (var fsm in raiz.GetComponentsInChildren<PlayMakerFSM>(true)) fsm.enabled = false;
                    foreach (var ag in raiz.GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>(true)) ag.enabled = false;
                    raiz.transform.rotation = Quaternion.LookRotation(-f) * Quaternion.Euler(0f, 35f, 0f);
                    string tn = "zd_0" + a[1]; Texture2D tex = null;
                    foreach (var t in Resources.FindObjectsOfTypeAll<Texture2D>()) if (t != null && t.name == tn) { tex = t; break; }
                    foreach (var r in raiz.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                        foreach (var m in r.materials) { if (m == null || !m.name.StartsWith("zd_")) continue; if (tex != null) m.mainTexture = tex; if (m.HasProperty("_Color")) m.color = Color.white; }
                    { var pp = raiz.transform.position; var dirc = Quaternion.Euler(0f, 25f, 0f) * f;
                      Catalogo.Fotografiar(pp - dirc * 2.4f + Vector3.up * 1.0f, pp + Vector3.up * 0.55f, System.IO.Path.Combine(System.IO.Path.Combine(BepInEx.Paths.BepInExRootPath, "capturas"), "perro_" + a[0] + "_" + a[1] + ".png"), 900, 600, false); }
                    plugin.Log("  perrofoto " + a[0] + " piel " + tn + (tex == null ? " (NO existe la textura)" : ""));
                    break;
                }
                case "ver": VerModelo(arg); break;
                case "audios":
                {
                    // audios <filtro>: nombres de los AudioClip del juego que contienen el texto
                    var sb = new System.Text.StringBuilder(); int n = 0;
                    foreach (var c in Resources.FindObjectsOfTypeAll<AudioClip>()) if (c.name.ToLowerInvariant().Contains(arg.ToLowerInvariant())) { sb.Append(c.name).Append('(').Append(c.length.ToString("0.0")).Append(") "); n++; }
                    plugin.Log("  audios '" + arg + "' (" + n + "): " + sb);
                    break;
                }
                case "clips":
                {
                    // clips <objeto>: animaciones y parámetros de sus Animator / Animation (también inactivos)
                    foreach (var g in Resources.FindObjectsOfTypeAll<GameObject>())
                    {
                        if (g.name != arg || !g.scene.IsValid()) continue;
                        foreach (var an in g.GetComponentsInChildren<Animator>(true))
                        {
                            var sb = new System.Text.StringBuilder("  Animator " + an.name + " ctrl=" + (an.runtimeAnimatorController != null ? an.runtimeAnimatorController.name : "-") + " clips:");
                            if (an.runtimeAnimatorController != null) foreach (var c in an.runtimeAnimatorController.animationClips) sb.Append(' ').Append(c.name).Append('(').Append(c.length.ToString("0.0")).Append(')');
                            if (an.isActiveAndEnabled) { sb.Append(" params:"); foreach (var pa in an.parameters) sb.Append(' ').Append(pa.name).Append(':').Append(pa.type); }
                            plugin.Log(sb.ToString());
                        }
                        foreach (var am in g.GetComponentsInChildren<Animation>(true))
                        {
                            var sb = new System.Text.StringBuilder("  Animation " + am.name + ":");
                            foreach (AnimationState st in am) sb.Append(' ').Append(st.name);
                            plugin.Log(sb.ToString());
                        }
                        break;
                    }
                    break;
                }
                case "anim": Animar(arg); break;
                case "quitar": if (muestra != null) UnityEngine.Object.Destroy(muestra); muestra = null; break;
                case "evento": EnviarEvento(arg); break;
                default: plugin.Log("  orden desconocida"); break;
            }
        }

        static string TextoDe(Button b)
        {
            var t = b.GetComponentInChildren<Text>();
            return t != null ? t.text : b.name;
        }

        void ListarBotones()
        {
            plugin.Log("  escena " + SceneManager.GetActiveScene().buildIndex + " '" + SceneManager.GetActiveScene().name + "'");
            foreach (var b in UnityEngine.Object.FindObjectsOfType<Button>())
            {
                if (!b.isActiveAndEnabled) continue;
                plugin.Log("  botón: '" + TextoDe(b).Replace("\n", " ") + "' (" + b.name + ", interactuable=" + b.interactable + ")");
            }
        }

        void Pulsar(string texto)
        {
            foreach (var b in UnityEngine.Object.FindObjectsOfType<Button>())
            {
                if (!b.isActiveAndEnabled || !b.interactable) continue;
                string t = TextoDe(b);
                if (t.IndexOf(texto, StringComparison.OrdinalIgnoreCase) >= 0 || b.name.IndexOf(texto, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    plugin.Log("  pulsando '" + t + "'");
                    b.onClick.Invoke();
                    return;
                }
            }
            plugin.Log("  no hay botón con '" + texto + "'");
        }

        /// Lista las máquinas de estado de PlayMaker activas: objeto/fsm, estado actual
        /// y los eventos que lo hacen avanzar. Con filtro, solo las que lo contienen.
        void ListarFsms(string filtro)
        {
            int n = 0;
            foreach (var f in UnityEngine.Object.FindObjectsOfType<PlayMakerFSM>())
            {
                if (!f.isActiveAndEnabled || f.Fsm == null || f.Fsm.ActiveState == null) continue;
                string id = f.gameObject.name + "/" + f.FsmName;
                string estado = f.ActiveStateName;
                var eventos = new System.Text.StringBuilder();
                foreach (var t in f.Fsm.ActiveState.Transitions) eventos.Append(t.EventName).Append(", ");
                string linea = id + " estado='" + estado + "' eventos=[" + eventos.ToString().TrimEnd(',', ' ') + "]";
                if (filtro.Length > 0 && linea.IndexOf(filtro, StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (filtro.Length == 0 && eventos.Length == 0) continue;   // sin filtro, solo las que esperan algo
                plugin.Log("  " + linea);
                if (++n >= 80) { plugin.Log("  ... (más)"); break; }
            }
        }

        /// fsm <prefab_o_objeto>/<fsm>: estados, acciones (con campos) y transiciones de una FSM,
        /// buscándola también en prefabs en memoria.
        void DetalleFsm(string id)
        {
            int barra = id.LastIndexOf('/');
            string obj = barra < 0 ? id : id.Substring(0, barra), nombre = barra < 0 ? "" : id.Substring(barra + 1);
            foreach (var f in Resources.FindObjectsOfTypeAll<PlayMakerFSM>())
            {
                if (f.FsmName != nombre) continue;
                if (f.gameObject.name != obj && f.transform.root.name != obj) continue;
                plugin.Log("  FSM " + f.transform.root.name + "/" + f.gameObject.name + "/" + f.FsmName + " inicio='" + f.Fsm.StartState + "'");
                { var gt = new System.Text.StringBuilder(); if (f.Fsm.GlobalTransitions != null) foreach (var t0 in f.Fsm.GlobalTransitions) if (t0 != null && t0.FsmEvent != null) gt.Append(t0.FsmEvent.Name).Append("→").Append(t0.ToState).Append(", "); plugin.Log("   globales: " + gt); }
                foreach (var st in f.FsmStates)
                {
                    var tr = new System.Text.StringBuilder();
                    foreach (var t in st.Transitions) tr.Append(t.EventName).Append("→").Append(t.ToState).Append(", ");
                    plugin.Log("   [" + st.Name + "] → " + tr);
                    foreach (var a in st.Actions)
                    {
                        var campos = new System.Text.StringBuilder();
                        foreach (var c in a.GetType().GetFields())
                        {
                            object v = c.GetValue(a);
                            if (v == null) continue;
                            string sv = v.ToString();
                            var ne = v as HutongGames.PlayMaker.NamedVariable;
                            if (ne != null) sv = (ne.UseVariable ? "$" + ne.Name : "") + "=" + ne.ToString();
                            var ev = v as HutongGames.PlayMaker.FsmEvent;
                            if (ev != null) sv = "ev:" + ev.Name;
                            if (sv.Length > 40) sv = sv.Substring(0, 40);
                            campos.Append(c.Name).Append(':').Append(sv).Append(' ');
                        }
                        plugin.Log("      " + a.GetType().Name + " " + campos);
                    }
                }
                return;
            }
            plugin.Log("  no encontré " + id);
        }

        /// evento <objeto/fsm> <EVENTO>   envía el evento a esa máquina de estados.
        void EnviarEvento(string arg)
        {
            // evento <objeto/fsm> <EVENTO>  o  evento <objeto/fsm>|<EVENTO con espacios> (2026-11-20)
            int esp = arg.IndexOf('|') >= 0 ? arg.IndexOf('|') : arg.LastIndexOf(' ');
            if (esp < 0) { plugin.Log("  uso: evento <objeto/fsm> <EVENTO>  o  evento <objeto/fsm>|<EVENTO>"); return; }
            string id = arg.Substring(0, esp).Trim();
            string ev = arg.Substring(esp + 1).Trim();
            foreach (var f in UnityEngine.Object.FindObjectsOfType<PlayMakerFSM>())
            {
                if ((f.gameObject.name + "/" + f.FsmName) == id || f.gameObject.name == id)
                {
                    plugin.Log("  enviando " + ev + " a " + f.gameObject.name + "/" + f.FsmName + " (estado '" + f.ActiveStateName + "')");
                    f.SendEvent(ev);
                    return;
                }
            }
            plugin.Log("  no encontré la máquina '" + id + "'");
        }

        /// Lista personajes con esqueleto (SkinnedMeshRenderer) agrupados por su raíz: en escena
        /// (con distancia al jugador) y cargados en memoria sin estar en escena (prefabs).
        /// Muestra el Animator/Animation y sus clips para saber si sirven de compañero.
        void ListarModelos(string filtro)
        {
            var jugador = UnityEngine.Object.FindObjectOfType<FPSPlayer>();
            Vector3 pj = jugador != null ? jugador.transform.position : Vector3.zero;

            // Cuerpo del propio jugador
            if (jugador != null)
            {
                foreach (var s in jugador.transform.root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    plugin.Log("  [jugador] " + Ruta(s.transform) + " huesos=" + s.bones.Length + " activo=" + s.gameObject.activeInHierarchy);
                var cam = Camera.main;
                if (cam != null)
                    foreach (var s in cam.transform.root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                        plugin.Log("  [cámara] " + Ruta(s.transform) + " huesos=" + s.bones.Length);
            }

            var vistos = new System.Collections.Generic.Dictionary<GameObject, int>();
            foreach (var s in Resources.FindObjectsOfTypeAll<SkinnedMeshRenderer>())
            {
                if (s.bones == null || s.bones.Length < 10) continue;      // solo personajes con esqueleto
                GameObject raiz = RaizPersonaje(s.transform);
                if (vistos.ContainsKey(raiz)) { vistos[raiz]++; continue; }
                vistos[raiz] = 1;
            }

            int n = 0;
            foreach (var par in vistos)
            {
                GameObject r = par.Key;
                bool enEscena = r.scene.IsValid();
                string linea = (enEscena ? "[escena] " : "[memoria] ") + Ruta(r.transform)
                    + " mallas=" + par.Value
                    + (enEscena ? " dist=" + Mathf.RoundToInt(Vector3.Distance(pj, r.transform.position)) + " activo=" + r.activeInHierarchy : "")
                    + " " + Animaciones(r);
                if (filtro.Length > 0 && linea.IndexOf(filtro, StringComparison.OrdinalIgnoreCase) < 0) continue;
                plugin.Log("  " + linea);
                if (++n >= 150) { plugin.Log("  ... (más)"); break; }
            }
            plugin.Log("  total personajes: " + vistos.Count);
        }

        /// Lista los monstruos (TakeDamage) en escena: ruta, distancia, y qué componentes tienen
        /// sus ancestros e hijos (NavMeshAgent, Animator, FSMs, CharacterDamagePlaymakerAI).
        void ListarMonstruos()
        {
            var j = UnityEngine.Object.FindObjectOfType<FPSPlayer>();
            Vector3 pj = j != null ? j.transform.position : Vector3.zero;
            int n = 0;
            foreach (var td in Resources.FindObjectsOfTypeAll<TakeDamage>())
            {
                var t = td.transform;
                if (++n > 12) { plugin.Log("  ..."); break; }
                plugin.Log("  TD " + (td.gameObject.scene.IsValid() ? "[escena] " : "[memoria] ") + Ruta(t) + " dist=" + Mathf.RoundToInt(Vector3.Distance(pj, t.position)) + " activo=" + td.gameObject.activeInHierarchy
                    + " fsm=" + td.NameOfFsm + " var=" + td.NameOfVariableThatReceiveTheDamage + " ev=" + td.NameOfEventThatRunOnReceiveDamage);
                Transform x = t; int nivel = 0;
                while (x != null && nivel < 6)
                {
                    plugin.Log("    ↑" + nivel + " " + x.name + " [" + Componentes(x.gameObject) + "]");
                    x = x.parent; nivel++;
                }
                var root = t.root;
                int col = 0;
                foreach (var c in root.GetComponentsInChildren<CharacterDamagePlaymakerAI>(true)) col++;
                var ag = root.GetComponentInChildren<UnityEngine.AI.NavMeshAgent>(true);
                var an = root.GetComponentInChildren<Animator>(true);
                plugin.Log("    raíz=" + root.name + " colisionesDaño=" + col + " agente=" + (ag != null ? ag.name : "-") + " animator=" + (an != null ? an.name : "-"));
            }
        }

        static string Componentes(GameObject g)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var c in g.GetComponents<Component>())
            {
                if (c == null || c is Transform) continue;
                string n = c.GetType().Name;
                if (c is PlayMakerFSM) n = "FSM:" + ((PlayMakerFSM)c).FsmName;
                sb.Append(n).Append(' ');
            }
            return sb.ToString().TrimEnd();
        }

        System.Collections.IEnumerator RecFotos(string[] pa)
        {
            Entrada.Recargar = 2;
            float t0 = Time.realtimeSinceStartup;
            for (int i = 1; i < pa.Length; i++)
            {
                float t = float.Parse(pa[i], System.Globalization.CultureInfo.InvariantCulture);
                while (Time.realtimeSinceStartup - t0 < t) yield return null;
                Captura(pa[0] + "_" + pa[i]);
            }
        }

        static string RutaDe(Transform t, Transform raiz)
        {
            string r = t.name;
            while (t.parent != null && t != raiz) { t = t.parent; r = t.name + "/" + r; }
            return r;
        }

        static string objetivo = "";   // orden "objetivo <texto>": filtra por nombre de la raíz

        Transform presaPrueba;

        static TakeDamage MasCercano()
        {
            var j = UnityEngine.Object.FindObjectOfType<FPSPlayer>();
            if (j == null) return null;
            TakeDamage mejor = null; float dm = 1e9f;
            foreach (var td in UnityEngine.Object.FindObjectsOfType<TakeDamage>())
            {
                if (objetivo.Length > 0 && td.transform.root.name.IndexOf(objetivo, StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (objetivo.Length == 0 && (Dificultad.EsJefe(td) || !td.gameObject.activeInHierarchy || td.transform.root.name.Contains("Crux") || Aliados.Protegido(td.transform))) continue;   // sin pedirlo, nunca un jefe (matarlo avanza la historia)
                float d = Vector3.Distance(td.transform.position, j.transform.position);
                if (d < dm) { dm = d; mejor = td; }
            }
            return mejor;
        }

        /// danar <cantidad>: daña al monstruo más cercano como si le hubiera disparado este jugador.
        void Danar(float cant)
        {
            var td = MasCercano();
            if (td == null) { plugin.Log("  no hay monstruos"); return; }
            CharacterDamagePlaymakerAI zona = null;
            foreach (var z in Combate.RaizDe(td).GetComponentsInChildren<CharacterDamagePlaymakerAI>())
                if (z.ai == td) { zona = z; break; }
            if (zona == null) { plugin.Log("  sin zona de daño"); return; }
            plugin.Log("  dañando " + td.transform.root.name + " con " + cant + " (zona " + zona.name + ")");
            zona.ApplyDamage(cant, false);
        }

        /// vida: variables numéricas de la FSM de vida del monstruo más cercano.
        void Vida()
        {
            var td = MasCercano();
            if (td == null) { plugin.Log("  no hay monstruos"); return; }
            var f = PlayMakerFSM.FindFsmOnGameObject(td.gameObject, td.NameOfFsm);
            if (f == null) { plugin.Log("  sin FSM " + td.NameOfFsm); return; }
            var sb = new System.Text.StringBuilder();
            foreach (var v in f.FsmVariables.FloatVariables) sb.Append(v.Name).Append('=').Append(v.Value.ToString("0.#")).Append(' ');
            foreach (var v in f.FsmVariables.IntVariables) sb.Append(v.Name).Append('=').Append(v.Value).Append(' ');
            foreach (var v in f.FsmVariables.BoolVariables) sb.Append(v.Name).Append('=').Append(v.Value).Append(' ');
            plugin.Log("  " + td.transform.root.name + " estado='" + f.ActiveStateName + "' " + sb);
        }

        /// irmonstruo [distancia]: teletransporta al jugador a esa distancia del monstruo activo más cercano.
        void IrMonstruo(float dist)
        {
            var j = UnityEngine.Object.FindObjectOfType<FPSPlayer>();
            var td = MasCercano();
            if (j == null || td == null) { plugin.Log("  no hay monstruos"); return; }
            Vector3 m = td.transform.position;
            Vector3 dir = j.transform.position - m; dir.y = 0; dir.Normalize();
            Vector3 destino = m + dir * dist;
            UnityEngine.AI.NavMeshHit nh;
            if (UnityEngine.AI.NavMesh.SamplePosition(destino, out nh, 10f, UnityEngine.AI.NavMesh.AllAreas)) destino = nh.position;
            j.transform.position = destino + Vector3.up * 1.2f;
            var rb = j.GetComponent<Rigidbody>();
            if (rb != null) { rb.position = j.transform.position; rb.velocity = Vector3.zero; }
            plugin.Log("  junto a " + Combate.NombreDe(td) + " a " + dist + " m, en " + destino);
        }

        GameObject muestra;

        static GameObject Buscar(string nombre) { return Modelos.Buscar(nombre); }

        /// ver <modelo>   pone una copia inmóvil del modelo 3 m delante del jugador, mirándolo.
        void VerModelo(string nombre)
        {
            var orig = Buscar(nombre);
            if (orig == null) { plugin.Log("  no existe '" + nombre + "'"); return; }
            var jugador = UnityEngine.Object.FindObjectOfType<FPSPlayer>();
            var cam = Camera.main;
            if (jugador == null || cam == null) { plugin.Log("  sin jugador"); return; }
            if (muestra != null) UnityEngine.Object.Destroy(muestra);

            Vector3 adelante = cam.transform.forward; adelante.y = 0; adelante.Normalize();
            Vector3 pos = jugador.transform.position + adelante * 3f;
            RaycastHit hit;
            if (Physics.Raycast(pos + Vector3.up * 3f, Vector3.down, out hit, 10f)) pos = hit.point;

            muestra = (GameObject)UnityEngine.Object.Instantiate(orig, pos, Quaternion.LookRotation(-adelante));
            muestra.name = "WitchHuntCoop_Muestra";
            Desactivar(muestra);
            muestra.SetActive(true);
            foreach (var t in muestra.GetComponentsInChildren<Transform>(true)) t.gameObject.SetActive(true);
            plugin.Log("  muestra de '" + nombre + "' en " + pos + " " + Animaciones(muestra));
        }

        static void Desactivar(GameObject go) { Modelos.Desactivar(go); }

        /// anim <controlador_de_modelo> <estado>   usa el Animator de otro modelo y reproduce un estado.
        /// anim - <estado>                          reproduce un estado con el controlador actual.
        void Animar(string arg)
        {
            if (muestra == null) { plugin.Log("  primero: ver <modelo>"); return; }
            var an = muestra.GetComponentInChildren<Animator>();
            if (an == null) { plugin.Log("  la muestra no tiene Animator"); return; }
            int esp = arg.IndexOf(' ');
            string de = esp < 0 ? arg : arg.Substring(0, esp);
            string estado = esp < 0 ? "" : arg.Substring(esp + 1).Trim();
            if (de != "-")
            {
                var otro = Buscar(de);
                var anOtro = otro != null ? otro.GetComponentInChildren<Animator>(true) : null;
                if (anOtro == null || anOtro.runtimeAnimatorController == null) { plugin.Log("  '" + de + "' no tiene controlador"); return; }
                an.runtimeAnimatorController = anOtro.runtimeAnimatorController;
                an.applyRootMotion = false;
                plugin.Log("  controlador de '" + de + "': " + Animaciones(muestra));
            }
            if (estado.Length > 0)
            {
                bool hay = an.HasState(0, Animator.StringToHash(estado)) || an.HasState(0, Animator.StringToHash("Base Layer." + estado));
                plugin.Log("  estado '" + estado + "' existe=" + hay);
                if (hay) an.Play(estado, 0, 0f);
            }
        }

        /// Sube hasta el objeto que tiene el Animator/Animation (o la raíz).
        static GameObject RaizPersonaje(Transform t)
        {
            Transform x = t;
            while (x != null)
            {
                if (x.GetComponent<Animator>() != null || x.GetComponent<Animation>() != null) return x.gameObject;
                x = x.parent;
            }
            return t.root.gameObject;
        }

        static string Animaciones(GameObject r)
        {
            var sb = new System.Text.StringBuilder();
            var an = r.GetComponent<Animator>();
            if (an != null)
            {
                sb.Append("animator=").Append(an.runtimeAnimatorController != null ? an.runtimeAnimatorController.name : "ninguno");
                sb.Append(" humanoide=").Append(an.avatar != null && an.avatar.isHuman);
                if (an.runtimeAnimatorController != null)
                {
                    sb.Append(" clips=[");
                    var nombres = new System.Collections.Generic.List<string>();
                    foreach (var c in an.runtimeAnimatorController.animationClips)
                        if (c != null && !nombres.Contains(c.name)) nombres.Add(c.name);
                    sb.Append(string.Join(", ", nombres.ToArray())).Append("]");
                }
            }
            var leg = r.GetComponent<Animation>();
            if (leg != null)
            {
                sb.Append(" legacy=[");
                foreach (AnimationState st in leg) sb.Append(st.name).Append(", ");
                sb.Append("]");
            }
            return sb.ToString();
        }

        static string Ruta(Transform t)
        {
            string r = t.name;
            int i = 0;
            while (t.parent != null && i++ < 4) { t = t.parent; r = t.name + "/" + r; }
            return r;
        }

        /// Foto para publicar: oculta la interfaz (juego y mod), el arma y las manos, saca la captura al doble de resolución y lo devuelve todo.
        public static bool ModoFoto;
        System.Collections.IEnumerator Foto(string nombre)
        {
            Directory.CreateDirectory(carpetaCapturas);
            if (nombre.Length == 0) nombre = "foto_" + DateTime.Now.ToString("HHmmss");
            string ruta = Path.Combine(carpetaCapturas, nombre + ".png");
            ModoFoto = true;
            var apagados = new List<Behaviour>(); var renders = new List<Renderer>();
            foreach (var c in UnityEngine.Object.FindObjectsOfType<Canvas>()) if (c.enabled) { c.enabled = false; apagados.Add(c); }
            foreach (var g in UnityEngine.Object.FindObjectsOfType<GUIText>()) if (g.enabled) { g.enabled = false; apagados.Add(g); }
            foreach (var g in UnityEngine.Object.FindObjectsOfType<GUITexture>()) if (g.enabled) { g.enabled = false; apagados.Add(g); }
            var pw = UnityEngine.Object.FindObjectOfType<PlayerWeapons>();
            if (pw != null) foreach (var r in pw.GetComponentsInChildren<Renderer>()) if (r.enabled) { r.enabled = false; renders.Add(r); }
            yield return null; yield return null;
            Application.CaptureScreenshot(ruta, 2);
            for (int i = 0; i < 4; i++) yield return null;
            foreach (var b in apagados) if (b != null) b.enabled = true;
            foreach (var r in renders) if (r != null) r.enabled = true;
            ModoFoto = false;
            plugin.Log("  foto en " + ruta);
        }

        // ---- fotodesde: la cámara del juego se mueve SOLO al dibujar la foto (el jugador no se mueve) ----
        static bool fdActivo; static Vector3 fdPos, fdGuardPos; static Quaternion fdRot, fdGuardRot; static float fdFov, fdGuardFov;
        static void FdAntes(Camera c)
        {
            if (!fdActivo || c != Camera.main) return;
            fdGuardPos = c.transform.position; fdGuardRot = c.transform.rotation; fdGuardFov = c.fieldOfView;
            c.transform.position = fdPos; c.transform.rotation = fdRot; c.fieldOfView = fdFov;
        }
        static void FdDespues(Camera c)
        {
            if (!fdActivo || c != Camera.main) return;
            c.transform.position = fdGuardPos; c.transform.rotation = fdGuardRot; c.fieldOfView = fdGuardFov;
        }

        System.Collections.IEnumerator FotoDesde(string arg)
        {
            var ps = arg.Split(' '); var ci = System.Globalization.CultureInfo.InvariantCulture;
            if (ps.Length < 6) { plugin.Log("  uso: fotodesde x y z tx ty tz [fov] [luz] [nombre]"); yield break; }
            Vector3 o = Anclaje.Origen();
            fdPos = o + new Vector3(float.Parse(ps[0], ci), float.Parse(ps[1], ci), float.Parse(ps[2], ci));
            Vector3 t = o + new Vector3(float.Parse(ps[3], ci), float.Parse(ps[4], ci), float.Parse(ps[5], ci));
            fdRot = Quaternion.LookRotation(t - fdPos, Vector3.up);
            fdFov = ps.Length > 6 ? float.Parse(ps[6], ci) : 45f;
            float luz = ps.Length > 7 ? float.Parse(ps[7], ci) : 0f;
            string nombre = ps.Length > 8 ? ps[8] : "fotodesde_" + DateTime.Now.ToString("HHmmss");
            Directory.CreateDirectory(carpetaCapturas);
            string ruta = Path.Combine(carpetaCapturas, nombre + ".png");
            GameObject goLuz = null;
            if (luz > 0f)
            {
                goLuz = new GameObject("coop_foto_luz"); goLuz.transform.position = t + (fdPos - t).normalized * 14f + Vector3.down * 6f;
                var l = goLuz.AddComponent<Light>(); l.type = LightType.Point; l.range = 60f; l.intensity = luz; l.color = new Color(1f, 0.82f, 0.68f); l.shadows = LightShadows.None;
            }
            ModoFoto = true;
            var apagados = new List<Behaviour>(); var renders = new List<Renderer>();
            foreach (var c in UnityEngine.Object.FindObjectsOfType<Canvas>()) if (c.enabled) { c.enabled = false; apagados.Add(c); }
            foreach (var g in UnityEngine.Object.FindObjectsOfType<GUIText>()) if (g.enabled) { g.enabled = false; apagados.Add(g); }
            foreach (var g in UnityEngine.Object.FindObjectsOfType<GUITexture>()) if (g.enabled) { g.enabled = false; apagados.Add(g); }
            var pw = UnityEngine.Object.FindObjectOfType<PlayerWeapons>();
            if (pw != null) foreach (var r in pw.GetComponentsInChildren<Renderer>()) if (r.enabled) { r.enabled = false; renders.Add(r); }
            Camera.onPreCull += FdAntes; Camera.onPostRender += FdDespues; fdActivo = true;
            for (int i = 0; i < 3; i++) yield return null;
            Application.CaptureScreenshot(ruta, 2);
            for (int i = 0; i < 4; i++) yield return null;
            fdActivo = false; Camera.onPreCull -= FdAntes; Camera.onPostRender -= FdDespues;
            if (goLuz != null) UnityEngine.Object.Destroy(goLuz);
            foreach (var b in apagados) if (b != null) b.enabled = true;
            foreach (var r in renders) if (r != null) r.enabled = true;
            ModoFoto = false;
            plugin.Log("  foto desde " + (fdPos - o).ToString("F0") + " en " + ruta);
        }

        void Captura(string nombre)
        {
            Directory.CreateDirectory(carpetaCapturas);
            if (nombre.Length == 0) nombre = DateTime.Now.ToString("HHmmss");
            string ruta = Path.Combine(carpetaCapturas, nombre + ".png");
            Application.CaptureScreenshot(ruta);
            plugin.Log("  captura en " + ruta);
        }
    
        static System.Collections.IEnumerator InformarPrueba(List<AudioSource> fuentes, string nombre)
        {
            for (int i = 0; i < 4; i++) yield return null;
            var sb = new System.Text.StringBuilder("  prueba '" + nombre + "':");
            foreach (var a in fuentes) if (a != null) sb.Append(" prioridad ").Append(a.priority).Append(" → sonando=").Append(a.isPlaying).Append(" virtual=").Append(a.isVirtual).Append(";");
            int sonando = 0, virtuales = 0;
            foreach (var a in UnityEngine.Object.FindObjectsOfType<AudioSource>()) if (a.isPlaying) { sonando++; if (a.isVirtual) virtuales++; }
            sb.Append(" | fuentes sonando en la escena ").Append(sonando).Append(" (virtuales ").Append(virtuales).Append(")");
            Escopeta.plugin.Log(sb.ToString());
        }
    
        static System.Collections.IEnumerator ProbarTodos(int lote)
        {
            var pl = Escopeta.plugin;
            var clips = new Dictionary<AudioClip, string>(); var fallos = new List<string>();
            int fuentes = 0, mudas = 0, vol0 = 0; var sbMudas = new System.Text.StringBuilder();
            foreach (var a in Resources.FindObjectsOfTypeAll<AudioSource>())
            {
                if (a == null || !a.gameObject.scene.IsValid()) continue; fuentes++;
                if (a.clip != null && !clips.ContainsKey(a.clip)) clips[a.clip] = "objeto " + a.transform.root.name;
                if (a.mute) { mudas++; if (sbMudas.Length < 500) sbMudas.Append(a.transform.root.name).Append('/').Append(a.name).Append("; "); }
                if (a.enabled && a.gameObject.activeInHierarchy && a.volume <= 0.001f) vol0++;
            }
            var pw = Rendimiento.Armas;
            if (pw != null && pw.weaponOrder != null)
            {
                foreach (var w in pw.weaponOrder)
                {
                    if (w == null) continue; var wb = w.GetComponent<WeaponBehavior>(); if (wb == null) continue;
                    foreach (var c in new[] { wb.fireSnd, wb.reloadSnd, wb.reloadLastSnd, wb.noammoSnd, wb.readySnd }) if (c != null) clips[c] = "arma " + w.name;
                }
                if (pw.changesnd != null) clips[pw.changesnd] = "cambio de arma";
            }
            var f = UnityEngine.Object.FindObjectOfType<Footsteps>();
            if (f != null)
            {
                foreach (var arr in new[] { f.dirtSteps, f.woodSteps, f.metalSteps, f.waterSounds, f.climbSounds, f.stoneSteps, f.proneSteps }) if (arr != null) foreach (var c in arr) if (c != null) clips[c] = "pisadas";
                foreach (var c in new[] { f.dirtLand, f.metalLand, f.woodLand, f.waterLand, f.stoneLand }) if (c != null) clips[c] = "caída";
            }
            var delMod = new List<string>();
            foreach (var n in typeof(Sonidos).Assembly.GetManifestResourceNames())
                if (n.StartsWith("sonidos.") && n.EndsWith(".ogg")) { string nm = n.Substring(8, n.Length - 12); if (nm.IndexOf('.') >= 0) continue; delMod.Add(nm); Sonidos.Preparar(nm); }
            float hasta = Time.realtimeSinceStartup + 20f;
            while (Time.realtimeSinceStartup < hasta) { bool todos = true; foreach (var nm in delMod) if (Sonidos.Clip(nm) == null) { todos = false; break; } if (todos) break; yield return null; }
            foreach (var nm in delMod) { var c = Sonidos.Clip(nm); if (c == null) fallos.Add("mod " + nm + ": no cargó"); else clips[c] = "mod " + nm; }
            foreach (var c in Resources.FindObjectsOfTypeAll<AudioClip>()) if (c != null && !clips.ContainsKey(c)) clips[c] = "cargado en memoria";
            var lista = new List<KeyValuePair<AudioClip, string>>(clips);
            int ok = 0, cortos = 0;
            for (int i = 0; i < lista.Count; i += lote)
            {
                var cam = Camera.main; if (cam == null) { fallos.Add("sin cámara: prueba cortada"); break; }
                var grupo = new List<KeyValuePair<AudioSource, KeyValuePair<AudioClip, string>>>();
                for (int j = i; j < lista.Count && j < i + lote; j++) { var c = lista[j].Key; if (c.loadState == AudioDataLoadState.Unloaded) c.LoadAudioData(); }
                float espera = Time.realtimeSinceStartup + 3f;
                while (Time.realtimeSinceStartup < espera) { bool listos = true; for (int j = i; j < lista.Count && j < i + lote; j++) if (lista[j].Key.loadState == AudioDataLoadState.Loading) listos = false; if (listos) break; yield return null; }
                for (int j = i; j < lista.Count && j < i + lote; j++)
                {
                    var kv = lista[j];
                    if (kv.Key.loadState != AudioDataLoadState.Loaded) { fallos.Add(kv.Value + " '" + kv.Key.name + "': no carga (" + kv.Key.loadState + ")"); continue; }
                    var g = new GameObject("coop_prueba_todos"); g.transform.position = cam.transform.position;
                    var a = g.AddComponent<AudioSource>(); a.clip = kv.Key; a.spatialBlend = 0f; a.priority = 128; a.volume = 1f; a.Play();
                    grupo.Add(new KeyValuePair<AudioSource, KeyValuePair<AudioClip, string>>(a, kv));
                }
                for (int k = 0; k < 4; k++) yield return null;
                foreach (var e in grupo)
                {
                    var a = e.Key; var kv = e.Value;
                    if (a == null) continue;
                    if (kv.Key.length < 0.25f && !a.isPlaying) { cortos++; ok++; }
                    else if (!a.isPlaying) fallos.Add(kv.Value + " '" + kv.Key.name + "': no suena");
                    else if (a.isVirtual) fallos.Add(kv.Value + " '" + kv.Key.name + "': Unity lo silencia (virtual)");
                    else ok++;
                    UnityEngine.Object.Destroy(a.gameObject);
                }
                yield return null;
            }
            pl.Log("  PRUEBA DE SONIDOS: " + lista.Count + " sonidos distintos (" + delMod.Count + " del mod); bien " + ok + " (" + cortos + " muy cortos), con problema " + fallos.Count
                + " | fuentes en la escena " + fuentes + ", silenciadas a mano (mute) " + mudas + ", encendidas con volumen 0: " + vol0);
            if (mudas > 0) pl.Log("  silenciadas: " + sbMudas);
            for (int i = 0; i < fallos.Count; i += 8) pl.Log("  problemas: " + string.Join(" | ", fallos.GetRange(i, Mathf.Min(8, fallos.Count - i)).ToArray()));
        }
    }
}

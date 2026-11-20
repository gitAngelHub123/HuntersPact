// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using System;
using HarmonyLib;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;

namespace WitchHuntCoop
{
    /// Para salir de cacería (pueblo → bosque) los dos tienen que estar juntos en el punto de
    /// viaje. El viaje lo hace el botón "Travel to woods" del puente: ejecuta una plantilla de
    /// PlayMaker (RunFSM) con la variable LevelName = nivel de destino. Si el compañero está
    /// lejos (o en otra escena), se cancela y se avisa.
    [HarmonyPatch(typeof(RunFSM), "OnEnter")]
    static class Viaje
    {
        public static Plugin plugin;
        public const float Distancia = 15f;
        public const string Bosque = "Forest_SceneGua_fff";

        public const string Texto = "deben estar cerca para viajar al bosque";
        static string mensaje; static float mensajeHasta;
        static float ultimoRecibido = -100f;
        static bool siguiendo;   // este viaje lo inició el compañero: no se revisa ni se reenvía

        static bool Prefix(RunFSM __instance)
        {
            try
            {
                string destino = Destino(__instance);
                if (destino == null) return true;
                if (plugin == null || !plugin.Conectado) return true;   // jugando solo: como siempre
                if (siguiendo) return true;
                // Al morir un jefe el juego manda de vuelta al pueblo (parte de la historia): se permite siempre, pero viajan LOS DOS (2026-11-18)
                if (!EsCaceria(destino) && __instance.Owner != null && __instance.Owner.name == "Boss_killed")
                {
                    plugin.EnviarViaje(DestinoVolverPueblo); ultimoEnviado = Time.realtimeSinceStartup; permisoCarga = Time.realtimeSinceStartup + 20f;
                    plugin.Log("Viaje a '" + destino + "' por la muerte de un jefe: viajan los dos.");
                    return true;
                }
                if (!EsCaceria(destino) && SoloQuedoYo())
                {
                    plugin.Log("Viaje a '" + destino + "': " + plugin.NombreCompanero + " no está vivo; viajo solo.");
                    return true;
                }
                if (!EsCaceria(destino))
                {
                    // volver al pueblo (cartel de la cabaña): si se votó que sí, el compañero viaja con nosotros
                    if (Time.time < autorizadoHasta) { plugin.EnviarViaje(ConMarca(destino)); plugin.Log("Viaje a '" + destino + "': viajan los dos (votado)."); return true; }
                    // salida normal del bosque (sin votación): viajan los dos juntos; el menú principal no arrastra al compañero
                    if (destino.IndexOf("Menu", StringComparison.OrdinalIgnoreCase) >= 0 || plugin.Escena != Mundo.EscenaBosque) return true;   // solo la salida del bosque
                    // 2026-11-18: volver al pueblo desde el bosque también exige estar a menos de 15 m (antes este camino lo dejaba pasar siempre)
                    string porqueP;
                    if (!JuntosParaViajar(out porqueP))
                    {
                        Avisar("deben estar cerca para volver al pueblo");
                        plugin.Log("Viaje a '" + destino + "' bloqueado (en RunFSM): " + porqueP);
                        __instance.Finish();
                        try { __instance.Fsm.SetState("State 1"); } catch { }
                        return false;
                    }
                    plugin.EnviarViaje(destino);
                    plugin.Log("Viaje a '" + destino + "': viajan los dos.");
                    return true;
                }
                string porque;
                if (JuntosParaViajar(out porque))
                {
                    // Viajan los dos a la vez: el compañero activa su propio botón de viaje.
                    plugin.EnviarViaje(ConMarca(destino));
                    plugin.Log("Viaje a '" + destino + "': viajan los dos.");
                    return true;
                }
                // Respaldo (normalmente ya se bloqueó al pulsar): no dejar el botón atascado.
                Avisar(Texto);
                plugin.Log("Viaje al bosque bloqueado (en RunFSM): " + porque);
                __instance.Finish();
                try { __instance.Fsm.SetState("State 1"); } catch { }
                return false;
            }
            catch (Exception e) { Debug.LogError("[WitchHuntCoop] viaje: " + e); return true; }
        }

        // ---------- Bloqueo en la pulsación ----------
        // El botón del puente, al pulsarse, pasa por varios estados que quitan cosas del puente
        // (y dejan el botón en un estado sin salida) antes de llegar al RunFSM del viaje. Por eso
        // se bloquea el evento de la pulsación misma: la FSM no se mueve y todo queda intacto.
        static readonly System.Collections.Generic.Dictionary<Fsm, string> destinoDe = new System.Collections.Generic.Dictionary<Fsm, string>();

        /// Destino de caza del botón de esta FSM (o null si no es un botón de viaje a cazar).
        static string DestinoDeFsm(Fsm f)
        {
            string d;
            if (destinoDe.TryGetValue(f, out d)) return d;
            d = null;
            if (f.States != null)
                foreach (var st in f.States)
                {
                    FsmStateAction[] acts = null;
                    try { acts = st.Actions; } catch { }
                    if (acts == null) continue;
                    foreach (var a in acts)
                    {
                        var rf = a as RunFSM;
                        string x = rf != null ? Destino(rf) : null;
                        if (x != null && x.IndexOf("Menu", StringComparison.OrdinalIgnoreCase) < 0) { d = x; break; }   // el botón "Menú principal" de la pausa no se vota
                        var se = a as SendEventByName;   // el cartel de la cabaña del bosque: lanza "fast_travel" (vuelta al pueblo)
                        if (se != null && se.sendEvent != null && se.sendEvent.Value == "fast_travel") { d = DestinoVolverPueblo; break; }
                        // el botón del puente del bosque (2026-11-18): escribe el destino en la global LevelToLoad y llama a la pantalla de carga
                        var ss = a as SetStringValue;
                        if (ss != null && ss.stringVariable != null && ss.stringVariable.Name == "LevelToLoad" && ss.stringValue != null && !string.IsNullOrEmpty(ss.stringValue.Value)
                            && ss.stringValue.Value.IndexOf("Menu", StringComparison.OrdinalIgnoreCase) < 0) { d = ss.stringValue.Value; break; }
                        var sb = a as SetBoolValue;   // el cartel "Cabin" de la aldea: marca to_cabin y lanza el viaje al bosque
                        if (sb != null && sb.boolVariable != null && sb.boolVariable.Name == "to_cabin") { d = DestinoCartelCabana; break; }
                    }
                    if (d != null) break;
                }
            destinoDe[f] = d;
            return d;
        }

        public const string DestinoCartelCabana = "CABIN", DestinoVolverPueblo = "VILLAGE";
        public static string DestinoDe(Fsm f) { return DestinoDeFsm(f); }
        static float autorizadoHasta;   // tras un voto aprobado, los botones del viaje pasan sin revisar

        class Pedido { public Fsm F; public FsmEvent Ev; public string Destino; public float Hasta; }
        static Pedido pedido;           // mi petición, esperando la respuesta
        class Voto { public string Destino, Texto; public float Hasta; }
        static Voto recibido;           // petición del compañero, esperando mi respuesta

        static string TextoDe(string destino)
        {
            if (destino == DestinoCartelCabana) return "volver a la cabaña del lago";
            if (destino == DestinoVolverPueblo) return "volver al pueblo";
            if (EsCaceria(destino)) return "salir al bosque";
            return "volver al pueblo";
        }

        /// true = dejar pasar el evento.
        public static bool AlEvento(Fsm f, FsmEvent ev)
        {
            if (siguiendo || plugin == null || !plugin.Conectado || ev == null) return true;
            if (Time.time < autorizadoHasta) return true;
            if (f.GlobalTransitions == null || f.GlobalTransitions.Length == 0) return true;
            bool global = false;
            foreach (var t in f.GlobalTransitions)
                if (t != null && t.FsmEvent != null && t.FsmEvent.Name == ev.Name) { global = true; break; }
            if (!global) return true;
            string destino = DestinoDeFsm(f);
            if (destino == null) return true;
            if (SoloQuedoYo()) { plugin.Log("Viaje '" + destino + "': " + plugin.NombreCompanero + " no está vivo; sin votación ni distancia."); return true; }
            string porque;
            bool volverAlPueblo = destino == DestinoVolverPueblo || (!EsCaceria(destino) && destino != DestinoCartelCabana);
            // 2026-11-18: volver al pueblo desde el bosque también exige estar a menos de 15 m (antes uno solo podía irse lejos del otro)
            bool exigir = !volverAlPueblo || plugin.Escena == Mundo.EscenaBosque;
            if (exigir && !JuntosParaViajar(out porque))
            {
                Avisar(destino == DestinoVolverPueblo ? "deben estar cerca para volver al pueblo" : (destino == DestinoCartelCabana || EsCaceria(destino) ? Texto : "deben estar cerca para volver al pueblo"));
                plugin.Log("Viaje '" + destino + "' bloqueado al pulsar (" + ev.Name + "): " + porque);
                return false;
            }
            // Solo el viaje rápido de la cabaña del lago se vota (ida: cartel "Cabin" de la aldea; vuelta: cartel de la cabaña).
            // Entrar al bosque por el puente y salir por los demás botones: sin votación (siguen exigiendo estar cerca).
            if (destino != DestinoCartelCabana && destino != DestinoVolverPueblo) return true;
            if (pedido != null && Time.realtimeSinceStartup < pedido.Hasta) return false;   // ya hay una votación en curso
            // están juntos: se pregunta al compañero (el que pulsa ya está a favor)
            pedido = new Pedido { F = f, Ev = ev, Destino = destino, Hasta = Time.realtimeSinceStartup + 25f };
            EnviarVoto(0, destino);
            Avisar("Esperando a que " + plugin.NombreCompanero + " acepte " + TextoDe(destino) + "...");
            plugin.Log("Viaje: votación pedida para '" + destino + "'");
            return false;
        }

        static void EnviarVoto(byte codigo, string destino)
        {
            var datos = Red.Armar(w => { w.Write(codigo); w.Write(destino ?? ""); });
            for (int i = 0; i < 3; i++) plugin.EnviarVotoViaje(datos);
        }

        public static void Cancelar() { pedido = null; recibido = null; }

        static float ultimoRecibidoVoto = -100f; static byte ultimoCodigo; static string ultimoDestinoVoto;

        public static void RecibirVoto(System.IO.BinaryReader r)
        {
            byte codigo = r.ReadByte(); string destino = r.ReadString();
            // UDP: el mensaje llega repetido
            if (codigo == ultimoCodigo && destino == ultimoDestinoVoto && Time.realtimeSinceStartup - ultimoRecibidoVoto < 2f) return;
            ultimoCodigo = codigo; ultimoDestinoVoto = destino; ultimoRecibidoVoto = Time.realtimeSinceStartup;
            switch (codigo)
            {
                case 0:   // el compañero pide viajar: pregunto
                    recibido = new Voto { Destino = destino, Texto = TextoDe(destino), Hasta = Time.realtimeSinceStartup + 25f };
                    plugin.Log("Viaje: " + plugin.NombreCompanero + " pide " + recibido.Texto);
                    break;
                case 1:   // aceptó
                    if (pedido != null && pedido.Destino == destino)
                    {
                        string porque;
                        if ((pedido.Destino != DestinoVolverPueblo || plugin.Escena == Mundo.EscenaBosque) && !JuntosParaViajar(out porque)) { Avisar("deben estar cerca para viajar"); pedido = null; return; }
                        var p = pedido; pedido = null;
                        autorizadoHasta = Time.time + 8f;
                        plugin.Log("Viaje: " + plugin.NombreCompanero + " aceptó; viajando.");
                        p.F.Event(p.Ev);   // se repite la pulsación, ya autorizada
                        if (p.Destino == DestinoVolverPueblo) plugin.EnviarViaje(DestinoVolverPueblo);   // este viaje no pasa por un RunFSM: se avisa aquí
                    }
                    break;
                case 2:   // rechazó
                    if (pedido != null) { Avisar(plugin.NombreCompanero + " no quiso viajar."); pedido = null; }
                    break;
                case 3:   // el compañero canceló su petición
                    recibido = null;
                    break;
                case 4:   // el cartel de la cabaña ya está descubierto
                    var hd = FsmVariables.GlobalVariables.FindFsmBool("house_discovered");
                    if (hd != null && !hd.Value) { hd.Value = true; proximoRevisarCartel = 0f; plugin.Log("Viaje: el cartel de la cabaña queda desbloqueado también para mí."); }
                    break;
            }
        }

        static bool cartelEnviado;

        /// Para probar: contesta la votación pendiente sin pulsar la tecla.
        public static string ResponderPrueba(bool si)
        {
            if (recibido == null) return "no hay votación pendiente";
            var d = recibido.Destino; recibido = null; EnviarVoto((byte)(si ? 1 : 2), d); return si ? "aceptado" : "rechazado";
        }
        public static string EstadoVoto() { return "pedido=" + (pedido != null ? pedido.Destino : "-") + " recibido=" + (recibido != null ? recibido.Destino : "-") + " autorizado=" + (Time.time < autorizadoHasta); }

        /// Cada cuadro: teclas de la votación, tiempos y el cartel de la cabaña compartido.
        public static void Actualizar()
        {
            if (plugin == null) return;
            if (!plugin.Conectado) { pedido = null; recibido = null; cartelEnviado = false; return; }
            float ahora = Time.realtimeSinceStartup;
            if (pedido != null && ahora > pedido.Hasta) { Avisar(plugin.NombreCompanero + " no respondió."); pedido = null; }
            if (recibido != null)
            {
                if (ahora > recibido.Hasta) { EnviarVoto(2, recibido.Destino); recibido = null; }
                else if (Entrada.Enfocado && !Entrada.PanelAbierto)
                {
                    if (Controles.Pulsada("voto_si")) { var d = recibido.Destino; recibido = null; EnviarVoto(1, d); Avisar("Aceptaste."); }
                    else if (Controles.Pulsada("voto_no")) { var d = recibido.Destino; recibido = null; EnviarVoto(2, d); Avisar("Rechazaste el viaje."); }
                }
            }
            try { RevisarCartel(); } catch (Exception e) { plugin.Log("Viaje: cartel " + e.Message); }
            // el cartel "Cabin" de la aldea se desbloquea para los dos
            var hd = FsmVariables.GlobalVariables.FindFsmBool("house_discovered");
            // se repite cada 6 s (antes una sola vez: al cargar un nivel el juego vuelve a poner el valor de su partida guardada
            // y el compañero se quedaba sin cartel)
            if (hd != null && hd.Value && ahora > proximoCartel)
            {
                proximoCartel = ahora + 6f;
                EnviarVoto(4, "");
                if (!cartelEnviado) plugin.Log("Viaje: aviso al compañero del cartel de la cabaña.");
                cartelEnviado = true;
            }
        }
        static float proximoCartel, proximoRevisarCartel;

        /// El cartel "Cabin" del pueblo lo decide una FSM del juego (sign_parrent/FSM) UNA sola vez al cargar el pueblo: si en ese
        /// momento house_discovered era falso, deja la antorcha y no vuelve a mirar. Si después se descubre (o llega del compañero),
        /// se la hace evaluar de nuevo para que aparezca el cartel (2026-11-19: uno lo veía y el otro no).
        static void RevisarCartel()
        {
            if (plugin.Escena != 7 || Time.realtimeSinceStartup < proximoRevisarCartel) return;
            proximoRevisarCartel = Time.realtimeSinceStartup + 2f;
            var hd = FsmVariables.GlobalVariables.FindFsmBool("house_discovered");
            if (hd == null || !hd.Value) return;
            foreach (var f in UnityEngine.Object.FindObjectsOfType<PlayMakerFSM>())
            {
                if (f.gameObject.name != "sign_parrent" || f.FsmName != "FSM") continue;
                if (f.ActiveStateName != "State 4") continue;
                f.Fsm.SetState("State 2");
                plugin.Log("Viaje: el cartel de la cabaña se vuelve a mostrar (estaba la antorcha)");
            }
        }

        /// Pregunta de la votación, en el centro de la pantalla.
        public static void DibujarVoto()
        {
            if (recibido == null) return;
            if (estilo == null) { Dibujar(); }
            var s = new GUIStyle(GUI.skin.label) { fontSize = Opciones.T(24), alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, wordWrap = true };
            var cuadro = new Rect(Screen.width / 2f - 380f, Screen.height * 0.4f, 760f, 130f);
            var prev = GUI.color; GUI.color = new Color(0f, 0f, 0f, 0.7f); GUI.DrawTexture(cuadro, Texture2D.whiteTexture); GUI.color = prev;
            s.normal.textColor = new Color(1f, 0.9f, 0.6f);
            GUI.Label(new Rect(cuadro.x, cuadro.y + 10f, cuadro.width, 60f), plugin.NombreCompanero + " quiere " + recibido.Texto + ".", s);
            s.fontSize = Opciones.T(22); s.normal.textColor = Color.white;
            GUI.Label(new Rect(cuadro.x, cuadro.y + 70f, cuadro.width, 40f), "[" + Controles.NombreTecla(Controles.Tecla("voto_si")) + "] Sí      [" + Controles.NombreTecla(Controles.Tecla("voto_no")) + "] No      (" + Mathf.CeilToInt(recibido.Hasta - Time.realtimeSinceStartup) + " s)", s);
        }

        /// Nivel al que viaja este RunFSM (sustitución de la variable LevelName), o null.
        static string Destino(RunFSM r)
        {
            var ctl = r.fsmTemplateControl;
            if (ctl == null || ctl.fsmVarOverrides == null) return null;
            foreach (var o in ctl.fsmVarOverrides)
            {
                if (o == null || o.variable == null || o.variable.Name != "LevelName") continue;
                string v = o.fsmVar != null ? o.fsmVar.stringValue : null;
                if (string.IsNullOrEmpty(v) && o.variable is FsmString) v = ((FsmString)o.variable).Value;
                return v;
            }
            return null;
        }

        /// Salir a cazar: cualquier nivel que no sea la aldea ni el menú.
        static bool EsCaceria(string nivel)
        {
            return nivel.IndexOf("Bellville", StringComparison.OrdinalIgnoreCase) < 0 &&
                   nivel.IndexOf("Menu", StringComparison.OrdinalIgnoreCase) < 0;
        }

        /// Pedido 2026-11-19: si el compañero está muerto (espectando o muerte de verdad) y yo soy el único vivo, viajo solo
        /// (sin votación ni distancia). Él vuelve a la aldea solo cuando yo llego (Muerte: Revivir).
        public static bool SoloQuedoYo()
        {
            var m = Muerte.Instancia;
            // caído (Derribado) cuenta como vivo: solo viajo solo si está muerto (espectando o muerte de verdad) — pedido 2026-11-19
            return plugin != null && plugin.Conectado && m != null && m.Mio == Muerte.Estado.Vivo && (m.DelOtro == Muerte.Estado.Espectador || m.DelOtro == Muerte.Estado.Caido);
        }

        static bool JuntosParaViajar(out string porque)
        {
            if (SoloQuedoYo()) { porque = ""; return true; }
            var c = plugin.CompaneroActual;
            var j = plugin.Jugador;
            if (c == null || c.Escena != plugin.Escena || !c.Visible || j == null)
            {
                porque = plugin.NombreCompanero + (plugin.Escena == Mundo.EscenaBosque ? " no está en el bosque contigo." : " no está en la aldea contigo.");
                return false;
            }
            float d = Vector3.Distance(j.transform.position, c.PosicionMundo);
            if (d > Distancia)
            {
                porque = plugin.NombreCompanero + " está a " + Mathf.RoundToInt(d) + " m (acércate a menos de " + (int)Distancia + " m).";
                return false;
            }
            porque = "";
            return true;
        }

        /// El compañero inició el viaje: se activa el mismo botón en este juego.
        /// Viaje a la cabaña del lago (cartel "Cabin"): el juego de quien lo usó marca la global to_cabin y viaja al bosque; el
        /// compañero solo recibía "al bosque" y aparecía en el puente (2026-11-19). Ahora el aviso lleva la marca "|cabin".
        static string ConMarca(string destino)
        {
            var tc = FsmVariables.GlobalVariables.FindFsmBool("to_cabin");
            if (tc != null && tc.Value && EsCaceria(destino)) { haciaCabana = Time.realtimeSinceStartup; return destino + "|cabin"; }
            return destino;
        }

        // ---------- Llegada a la cabaña del lago ----------
        // El juego decide dónde aparece el jugador al cargar el bosque UNA vez, a los 0,3 s (FSM !!!scene_settings/OutOfLevel:
        // OutOfCave y to_cabin), y luego borra to_cabin y guarda. Ese momento es demasiado justo para el cooperativo (a veces uno o
        // los dos aparecían en el puente). Por eso, en un viaje a la cabaña, al terminar de cargar el bosque se lleva al jugador al
        // mismo punto que usa el juego si no quedó allí.
        static float haciaCabana = -1000f;
        static readonly Vector3 CabanaRespaldo = new Vector3(1096.6f, 13.7f, 988.6f);

        static Vector3 PuntoCabana()
        {
            foreach (var f in UnityEngine.Object.FindObjectsOfType<PlayMakerFSM>())
            {
                if (f.FsmName != "OutOfLevel" || f.FsmStates == null) continue;
                foreach (var st in f.FsmStates)
                {
                    if (st.Name != "State 8" || st.Actions == null) continue;
                    foreach (var a in st.Actions)
                    {
                        if (a == null || a.GetType().Name != "GetPosition") continue;
                        var campo = a.GetType().GetField("gameObject"); var od = campo != null ? campo.GetValue(a) as FsmOwnerDefault : null;
                        var go = od != null ? (od.OwnerOption == OwnerDefaultOption.UseOwner ? f.gameObject : od.GameObject.Value) : null;
                        if (go != null) return go.transform.position;
                    }
                }
            }
            return Anclaje.Origen() + CabanaRespaldo;
        }

        public static System.Collections.IEnumerator LlegadaCabana()
        {
            if (Time.realtimeSinceStartup - haciaCabana > 120f) yield break;
            haciaCabana = -1000f;
            // esperar a que el mundo esté armado y el juego haya hecho su propia colocación
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 60f && (plugin.Jugador == null || Time.timeSinceLevelLoad < 4f)) yield return new WaitForSeconds(0.5f);
            yield return new WaitForSeconds(1.5f);
            var j = plugin.Jugador; if (j == null || plugin.Escena != Mundo.EscenaBosque) yield break;
            Vector3 c = PuntoCabana();
            if (Vector3.Distance(j.transform.position, c) < 40f) { plugin.Log("Viaje: llegaste a la cabaña del lago"); yield break; }
            Vector3 destino = c + (plugin.EsAnfitrion ? Vector3.zero : new Vector3(1.5f, 0f, 1.5f)) + Vector3.up * 1f;
            RaycastHit h; if (Physics.Raycast(destino + Vector3.up * 20f, Vector3.down, out h, 60f, ~((1 << 2) | (1 << 11)), QueryTriggerInteraction.Ignore)) destino = h.point + Vector3.up * 1f;
            j.transform.position = destino;
            var rb = j.GetComponent<Rigidbody>(); if (rb != null) rb.velocity = Vector3.zero;
            plugin.Log("Viaje: aparecías en " + j.transform.position.ToString("F0") + "; llevado a la cabaña del lago " + destino.ToString("F0"));
        }

        public static void Recibir(string destino)
        {
            // caído o espectando: no viajo por mi cuenta; Muerte me lleva a la aldea cuando el compañero llega (2026-11-19)
            var mm = Muerte.Instancia;
            if (mm != null && (mm.Mio == Muerte.Estado.Derribado || mm.Mio == Muerte.Estado.Espectador)) { plugin.Log("Viaje: orden de viajar a '" + destino + "' ignorada (estoy " + mm.Mio + "); Muerte me lleva."); return; }
            if (plugin == null || plugin.Jugador == null) return;
            if (destino != null && destino.EndsWith("|cabin"))
            {
                destino = destino.Substring(0, destino.Length - 6);
                var tc = FsmVariables.GlobalVariables.FindFsmBool("to_cabin");
                if (tc != null && !tc.Value) { tc.Value = true; plugin.Log("Viaje: a la cabaña del lago, como el compañero (to_cabin)"); }
                var oc = FsmVariables.GlobalVariables.FindFsmBool("OutOfCave"); if (oc != null) oc.Value = true;
                haciaCabana = Time.realtimeSinceStartup;
            }
            if (destino == DestinoVolverPueblo && plugin.Escena != Mundo.EscenaBosque) return;   // ya estoy fuera del bosque: nada que seguir
            if (Time.realtimeSinceStartup - ultimoRecibido < 10f) return;   // el aviso llega repetido
            ultimoRecibido = Time.realtimeSinceStartup;
            plugin.Log("Viaje: " + plugin.NombreCompanero + " partió a '" + destino + "', te vas con él.");
            if (destino == DestinoVolverPueblo) { Avisar("Volviendo al pueblo con " + plugin.NombreCompanero + "..."); autorizadoHasta = Time.time + 8f; PlayMakerFSM.BroadcastEvent("fast_travel"); return; }
            Avisar((EsCaceria(destino) ? "Partiendo al bosque con " : "Volviendo al pueblo con ") + plugin.NombreCompanero + "...");
            if (!Activar(destino, true)) plugin.Log("Viaje: no encontré el botón de viaje a '" + destino + "' en esta escena.");
        }

        static float ultimoEnviado = -100f;
        static float permisoCarga = -100f;   // un viaje ya autorizado (muerte de un jefe…): su carga de nivel pasa sin revisar

        /// Control final (2026-11-18): TODA carga de nivel que saque a un jugador del bosque o del pueblo pasa por aquí (zonas de
        /// salida que se cruzan caminando, acciones de PlayMaker que cargan nivel, ChangeLevel del juego). Si el compañero está a
        /// más de 15 m y el viaje no fue autorizado (votación, seguir al compañero, muerte de un jefe), no se viaja.
        public static bool PermitirCarga(string destino, string via)
        {
            if (plugin == null || !plugin.Conectado || siguiendo) return true;
            int esc = plugin.Escena; if (esc != Mundo.EscenaBosque && esc != 7) return true;
            if (!string.IsNullOrEmpty(destino) && destino.IndexOf("Menu", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            float t = Time.realtimeSinceStartup;
            if (Time.time < autorizadoHasta || t < permisoCarga || t - ultimoRecibido < 25f) return true;
            string porque;
            if (JuntosParaViajar(out porque)) { permisoCarga = t + 20f; return true; }
            Avisar(esc == Mundo.EscenaBosque ? "deben estar cerca para volver al pueblo" : Texto);
            plugin.Log("Viaje a '" + destino + "' bloqueado (" + via + "): " + porque);
            return false;
        }

        /// Red de seguridad (2026-11-18): si llegué al pueblo desde el bosque por cualquier camino (muerte de un jefe, un botón que no
        /// pasó por el control...), el compañero que sigue en el bosque viaja conmigo. Así nunca queda uno solo en el pueblo.
        public static void LlegueAlPuebloDesdeBosque()
        {
            if (plugin == null || !plugin.Conectado) return;
            float t = Time.realtimeSinceStartup;
            if (t - ultimoRecibido < 60f) return;   // fui yo el que siguió al compañero
            var c = plugin.CompaneroActual;
            if (c != null && c.Escena != Mundo.EscenaBosque) return;   // el compañero ya no está en el bosque
            if (Muerte.Instancia != null && Muerte.Instancia.DelOtro != Muerte.Estado.Vivo) return;   // no está vivo: Muerte lo lleva a la aldea
            for (int i = 0; i < 3; i++) plugin.EnviarViaje(DestinoVolverPueblo);
            ultimoEnviado = t;
            plugin.Log("Viaje: llegué al pueblo desde el bosque; " + plugin.NombreCompanero + " viene conmigo.");
        }

        /// Busca el RunFSM que viaja a ese destino y entra en su estado (como pulsar el botón).
        public static bool Activar(string destino, bool comoSeguidor)
        {
            foreach (var f in UnityEngine.Object.FindObjectsOfType<PlayMakerFSM>())
            {
                if (f.FsmStates == null) continue;
                foreach (var st in f.FsmStates)
                    foreach (var a in st.Actions)
                    {
                        var rf = a as RunFSM;
                        if (rf == null || Destino(rf) != destino) continue;
                        siguiendo = comoSeguidor;
                        try { f.Fsm.SetState(st.Name); }
                        finally { siguiendo = false; }
                        return true;
                    }
            }
            return false;
        }

        /// Todos los botones de viaje de la escena: "destino @ posición (ruta)".
        public static System.Collections.Generic.List<string> Listar()
        {
            var l = new System.Collections.Generic.List<string>();
            foreach (var f in Resources.FindObjectsOfTypeAll<PlayMakerFSM>())
            {
                if (!f.gameObject.scene.IsValid() || f.FsmStates == null) continue;
                foreach (var st in f.FsmStates)
                {
                    FsmStateAction[] acts = null;
                    try { acts = st.Actions; } catch { }
                    if (acts == null) continue;
                    foreach (var a in acts)
                    {
                        var rf = a as RunFSM;
                        string d = rf != null ? Destino(rf) : null;
                        if (d == null) continue;
                        string ruta = f.name; var t = f.transform.parent;
                        while (t != null) { ruta = t.name + "/" + ruta; t = t.parent; }
                        l.Add(d + " @ " + f.transform.position + " (" + ruta + "/" + f.FsmName + " [" + st.Name + "], activo=" + f.gameObject.activeInHierarchy + ")");
                    }
                }
            }
            // botones de cualquier tipo que el control reconoce (puente del bosque, carteles…)
            foreach (var f in Resources.FindObjectsOfTypeAll<PlayMakerFSM>())
            {
                if (!f.gameObject.scene.IsValid() || f.FsmName != "Button" || f.Fsm == null) continue;
                string d = null; try { d = DestinoDeFsm(f.Fsm); } catch { }
                if (d == null) continue;
                string ruta = f.name; var t = f.transform.parent; while (t != null) { ruta = t.name + "/" + ruta; t = t.parent; }
                l.Add("BOTÓN " + d + " @ " + f.transform.position + " (" + ruta + ", estado " + f.ActiveStateName + ", activo=" + f.gameObject.activeInHierarchy + ")");
            }
            return l;
        }

        /// Para probar sin pulsar el botón del puente.
        public static string Probar()
        {
            if (plugin == null || !plugin.Conectado) return "sin compañero: viaje libre";
            string porque;
            return JuntosParaViajar(out porque) ? "permitido" : "bloqueado: " + porque;
        }

        public static void Avisar(string m, float segundos = 5f) { mensaje = m; mensajeHasta = Time.realtimeSinceStartup + segundos; }

        static GUIStyle estilo, sombra;

        public static void Dibujar()
        {
            if (mensaje == null || Time.realtimeSinceStartup > mensajeHasta) return;
            if (estilo == null)
            {
                estilo = new GUIStyle(GUI.skin.label) { fontSize = Opciones.T(22), alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, wordWrap = true };
                estilo.normal.textColor = new Color(1f, 0.85f, 0.5f);
                sombra = new GUIStyle(estilo);
                sombra.normal.textColor = new Color(0f, 0f, 0f, 0.85f);
            }
            var r = new Rect(Screen.width / 2f - 360f, Screen.height * 0.32f, 720f, 90f);
            GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), mensaje, sombra);
            GUI.Label(r, mensaje, estilo);
        }
    }

    [HarmonyPatch(typeof(Fsm), "ProcessEvent")]
    static class ParcheViajePulsar
    {
        static bool Prefix(Fsm __instance, FsmEvent fsmEvent)
        {
            if (__instance == null) return true;
            // la pantalla de carga del juego: TODO viaje entre niveles pasa por su evento global "LoadLevel" (destino en la global
            // LevelToLoad). Bloquearlo aquí no deja la pantalla de carga trabada: ni siquiera aparece.
            if (fsmEvent != null && fsmEvent.Name == "LoadLevel" && __instance.Name == "LoadingScreen")
            {
                try
                {
                    var lv = FsmVariables.GlobalVariables.FindFsmString("LevelToLoad");
                    if (Viaje.PermitirCarga(lv != null ? lv.Value : "", "pantalla de carga")) return true;
                    // bloqueado: el botón que lo pidió ya avanzó (y se escondió); se lo devuelve a su estado inicial para poder volver a usarlo
                    var b = EspiaNivel.Ultimo;
                    if (b != null && Time.realtimeSinceStartup - EspiaNivel.UltimoHora < 10f && b != __instance && !string.IsNullOrEmpty(b.StartState))
                    {
                        try
                        {
                            if (b.GameObject != null && !b.GameObject.activeSelf) b.GameObject.SetActive(true);
                            b.SetState(b.StartState);
                            Debug.Log("[WitchHuntCoop] viaje bloqueado: " + (b.GameObject != null ? b.GameObject.name : "?") + "/" + b.Name + " vuelve a '" + b.StartState + "'");
                        }
                        catch (Exception e) { Debug.LogError("[WitchHuntCoop] viaje: no se pudo devolver el botón: " + e.Message); }
                    }
                    return false;
                }
                catch (Exception e) { Debug.LogError("[WitchHuntCoop] viaje (pantalla de carga): " + e); return true; }
            }
            if (__instance.Name != "Button") return true;
            try { return Viaje.AlEvento(__instance, fsmEvent); }
            catch (Exception e) { Debug.LogError("[WitchHuntCoop] viaje (pulsar): " + e); return true; }
        }
    }

    /// Zonas de salida del juego (ChangeLevel: al entrar en el gatillo carga otro nivel).
    [HarmonyPatch(typeof(PixelCrushers.DialogueSystem.RealisticFPSPrefab.ChangeLevel), "OnTriggerEnter")]
    static class ParcheViajeZona
    {
        static bool Prefix(PixelCrushers.DialogueSystem.RealisticFPSPrefab.ChangeLevel __instance, Collider other)
        {
            try
            {
                if (other == null || other.GetComponentInParent<FPSPlayer>() == null) return true;
                return Viaje.PermitirCarga(__instance.newLevel, "zona " + __instance.name);
            }
            catch (Exception e) { Debug.LogError("[WitchHuntCoop] viaje (zona): " + e); return true; }
        }
    }

    /// Acciones de PlayMaker que cargan un nivel (LoadLevel, LoadLevelNum y parecidas): se buscan por nombre al arrancar.
    [HarmonyPatch]
    static class ParcheViajeCargarNivel
    {
        static System.Collections.Generic.IEnumerable<System.Reflection.MethodBase> TargetMethods()
        {
            var lista = new System.Collections.Generic.List<System.Reflection.MethodBase>();
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] tipos; try { tipos = asm.GetTypes(); } catch { continue; }
                foreach (var t in tipos)
                {
                    if (t == null || !typeof(FsmStateAction).IsAssignableFrom(t) || t.IsAbstract) continue;
                    string n = t.Name;
                    if (n.IndexOf("Load", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    if (n.IndexOf("Level", StringComparison.OrdinalIgnoreCase) < 0 && n.IndexOf("Scene", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    var m = t.GetMethod("OnEnter", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.DeclaredOnly);
                    if (m != null) lista.Add(m);
                }
            }
            Debug.Log("[WitchHuntCoop] viaje: vigilando " + lista.Count + " acciones de carga de nivel");
            return lista;
        }

        static bool Prefix(FsmStateAction __instance)
        {
            try
            {
                string destino = "";
                foreach (var f in __instance.GetType().GetFields())
                {
                    object v = f.GetValue(__instance);
                    var fs = v as FsmString; if (fs != null && !string.IsNullOrEmpty(fs.Value)) { destino = fs.Value; break; }
                    var fi = v as FsmInt; if (fi != null && f.Name.IndexOf("level", StringComparison.OrdinalIgnoreCase) >= 0) { destino = "#" + fi.Value; break; }
                }
                if (__instance.Fsm != null && __instance.Fsm.Name == "LoadingScreen") return true;   // ya revisado en su evento "LoadLevel"
                if (Viaje.PermitirCarga(destino, "acción " + __instance.GetType().Name + " en " + (__instance.Fsm != null ? __instance.Fsm.GameObjectName : "?"))) return true;
                __instance.Finish();
                return false;
            }
            catch (Exception e) { Debug.LogError("[WitchHuntCoop] viaje (cargar nivel): " + e); return true; }
        }
    }
}

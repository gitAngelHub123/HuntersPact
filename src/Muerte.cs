// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using System;
using System.Collections.Generic;
using HarmonyLib;
using HutongGames.PlayMaker;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WitchHuntCoop
{
    /// Muerte en cooperativo:
    ///  - Si muere uno y el otro sigue vivo en el mismo mapa, el muerto no pierde: pasa a
    ///    espectador (cámara detrás del compañero) hasta que el vivo vuelve a la aldea; entonces
    ///    reaparece en la aldea con la vida llena.
    ///  - Si muere el vivo también, los dos pasan por la muerte normal del juego (recarga el
    ///    último guardado: se pierde lo de la salida) y después se devuelve el 15 % de lo que
    ///    se había conseguido en la salida (oro, balas, ganzúas, tótems).
    /// La muerte del juego la dispara un evento a la FSM "DeathScript" del jugador: ese evento
    /// se retiene mientras se es espectador y se suelta si hay que morir de verdad.
    public class Muerte
    {
        public enum Estado : byte { Vivo = 0, Espectador = 1, Caido = 2, Derribado = 3 }
        public static float TiempoDerribado = 180f;   // (estático para poder acortarlo en las pruebas: orden "derribotiempo")
        public const float TiempoLevantar = 6f, DistanciaLevantar = 2.6f, VidaAlLevantar = 0.3f;

        public const int EscenaAldea = 7;
        public const float Recupera = 0.15f;
        static readonly string[] Botin = { "Coins", "silver_bullets_v2", "Lockpicks", "watcher_totems", "inventory_eggs" };

        public static Muerte Instancia;
        public Estado Mio = Estado.Vivo;
        /// Invencible por el sistema de muerte (derribado o espectador): el modo vuelo respeta esto al devolver el GodMode.
        public bool Invencible { get { return Mio == Estado.Derribado || Mio == Estado.Espectador || Time.time < invulnerableHasta; } }
        public Estado DelOtro = Estado.Vivo;

        readonly Plugin plugin;
        Fsm fsmRetenida; FsmEvent eventoRetenido;
        static bool soltando;
        float desde;

        // Salida: lo que había al dejar la aldea.
        readonly Dictionary<string, int> alSalir = new Dictionary<string, int>();
        int municionAlSalir = -1;
        int escenaAnterior = -1;
        Dictionary<string, int> bonoPendiente;
        int bonoMunicion;

        public Muerte(Plugin p) { plugin = p; Instancia = this; }

        /// Caídas en esta salida al bosque (pedido 2026-11-19): la segunda deja fuera al jugador (espectador directo).
        /// Además el juego solo manda "Death" UNA vez: tras levantarte, la segunda caída te dejaba con 0 de vida y de pie.
        public int Caidas;
        public const int CaidasMax = 2;
        float sinVidaDesde = -1f;

        // ---------------- Segunda posibilidad (habilidad del juego, pedido 2026-11-19) ----------------
        // El juego: "si tu vida llega a 0 y tienes 45 de maná, te teletransporta a un lugar al azar y recuperas 15 de vida".
        // En coop: si la tienes, te levantas SOLO cuando pasan los 180 s en el suelo sin que nadie te levante, o al caer por
        // segunda vez. Mismos números (45 de maná, 15 de vida), pero en el sitio (para seguir cerca del compañero) y con
        // 3 s de invulnerabilidad para no volver a caer al instante.
        public const float ManaSegunda = 45f, VidaSegunda = 15f, InvulnerableSegunda = 3f;
        float invulnerableHasta = -1f;

        static bool TieneSegundaPosibilidad()
        {
            try { var b = FsmVariables.GlobalVariables.FindFsmBool("skill_secondchance"); return b != null && b.Value; } catch { return false; }
        }

        /// El maná actual del juego es la variable de diálogo (Lua) "ManaCurrent", no una global de PlayMaker
        /// (comprobado 2026-11-19 en la partida: ManaCurrent=80; las globales solo tienen MaxMana y Mana_Changing).
        public const string VarMana = "ManaCurrent";
        public static float LeerMana() { try { return PixelCrushers.DialogueSystem.DialogueLua.GetVariable(VarMana).AsFloat; } catch { return 0f; } }
        public static void PonerMana(float v) { try { PixelCrushers.DialogueSystem.DialogueLua.SetVariable(VarMana, Mathf.RoundToInt(v)); } catch { } }

        bool PuedeSegundaPosibilidad()
        {
            return TieneSegundaPosibilidad() && LeerMana() >= ManaSegunda;
        }

        /// Si tiene la habilidad y el maná alcanza, lo levanta en el sitio. true = se usó.
        bool UsarSegundaPosibilidad(string cuando)
        {
            if (!TieneSegundaPosibilidad()) return false;
            float mana = LeerMana();
            if (mana < ManaSegunda) { plugin.Log("Muerte: Segunda posibilidad sin maná suficiente (" + mana + " de " + ManaSegunda + ") " + cuando); return false; }
            PonerMana(mana - ManaSegunda);
            var j = plugin.Jugador;
            if (j != null)
            {
                j.hitPoints = Mathf.Max(VidaSegunda, Mathf.Min(j.hitPoints, j.maximumHitPoints));
                j.GodMode = true;   // se apaga a los 3 s en Actualizar
                Fisica(j, true);
                var pw = UnityEngine.Object.FindObjectOfType<PlayerWeapons>();
                if (pw != null) pw.NoWeaponsAllowed = false;
            }
            invulnerableHasta = Time.time + InvulnerableSegunda;
            Entrada.Espectando = false;
            Mio = Estado.Vivo;
            fsmRetenida = null; eventoRetenido = null;
            Viaje.Avisar("Segunda posibilidad: te levantaste solo (−" + (int)ManaSegunda + " de maná).");
            plugin.Log("Muerte: Segunda posibilidad usada " + cuando + " (maná " + mana + " → " + (mana - ManaSegunda) + ", vida " + VidaSegunda + ")");
            return true;
        }

        /// La FSM "DeathScript" del jugador y su evento de muerte (para dispararla cuando el juego no lo hace).
        bool BuscarDeathScript(out Fsm fsm, out FsmEvent ev)
        {
            fsm = null; ev = null;
            var j = plugin.Jugador;
            foreach (var f in UnityEngine.Object.FindObjectsOfType<PlayMakerFSM>())
            {
                if (f == null || f.Fsm == null || f.Fsm.Name != "DeathScript") continue;
                foreach (var t in f.Fsm.GlobalTransitions)
                    if (t != null && t.FsmEvent != null) { fsm = f.Fsm; ev = t.FsmEvent; if (t.FsmEvent.Name == "Death") return true; }
                if (fsm != null && (j == null || f.transform.IsChildOf(j.transform.root))) return true;
            }
            return fsm != null;
        }

        /// Red de seguridad: vivo, conectado y sin vida durante 0,5 s sin que el juego avisara la muerte.
        void VigilarSinVida(FPSPlayer j)
        {
            if (!plugin.Conectado || j.GodMode || j.hitPoints >= 1f) { sinVidaDesde = -1f; return; }
            if (sinVidaDesde < 0f) { sinVidaDesde = Time.time; return; }
            if (Time.time - sinVidaDesde < 0.5f) return;
            sinVidaDesde = -1f;
            Fsm fsm; FsmEvent ev;
            BuscarDeathScript(out fsm, out ev);
            plugin.Log("Muerte: vida " + j.hitPoints.ToString("0.0") + " sin aviso del juego (caída " + (Caidas + 1) + "); la manejo yo" + (fsm == null ? " (sin DeathScript)" : ""));
            var c = plugin.CompaneroActual;
            bool otroVivoAqui = c != null && c.Escena == plugin.Escena && DelOtro == Estado.Vivo;
            if (!otroVivoAqui)
            {
                if (UsarSegundaPosibilidad("sin aviso del juego")) return;
                fsmRetenida = fsm; eventoRetenido = ev;
                MorirDeVerdad();
                return;
            }
            fsmRetenida = fsm; eventoRetenido = ev;
            Caer();
        }

        /// Primera caída: derribado (te pueden levantar). Segunda: fuera, a espectar al compañero vivo.
        void Caer()
        {
            Caidas++;
            if (Caidas >= CaidasMax)
            {
                // con Segunda posibilidad y maná suficiente: otra vez 180 s en el suelo (te pueden levantar gratis);
                // al terminar, la habilidad te levanta (pedido 2026-11-19: los 180 s en TODAS las caídas)
                if (PuedeSegundaPosibilidad()) { EntrarDerribado(); return; }
                plugin.Log("Muerte: segunda caída en esta salida; quedo fuera (espectador).");
                EntrarEspectador();
                return;
            }
            EntrarDerribado();
        }

        // ---------------- Decisión ----------------

        /// Llamado por el parche cuando el juego quiere matar al jugador. true = retener (espectar).
        public bool AlMorir(Fsm fsm, FsmEvent ev)
        {
            if (soltando) return false;
            plugin.Log("Muerte: evento '" + ev.Name + "' (yo=" + Mio + ", compañero=" + DelOtro + ")");
            if (Mio == Estado.Espectador) { fsmRetenida = fsm; eventoRetenido = ev; return true; } // ya muerto: se ignora
            var c = plugin.CompaneroActual;
            bool otroVivoAqui = plugin.Conectado && c != null && c.Escena == plugin.Escena && DelOtro == Estado.Vivo;
            if (!otroVivoAqui)
            {
                // Último en caer (o jugando solo): muerte normal del juego.
                if (plugin.Conectado) { Mio = Estado.Caido; PrepararBono(); plugin.Log("Muerte: cayó el último jugador; se pierde la salida (se recupera el 15 %)."); }
                return false;
            }
            fsmRetenida = fsm; eventoRetenido = ev;
            Caer();
            return true;
        }

        // ---------------- Derribado: el compañero puede levantarte ----------------

        Vector3 dondeCai; float derribadoDesde, progreso; bool levantando;

        /// Caes al suelo (90 s). Si el compañero mantiene E 6 s a tu lado, te levanta; si no, pasas a espectador.
        void EntrarDerribado()
        {
            Mio = Estado.Derribado;
            derribadoDesde = Time.time;
            var j = plugin.Jugador;
            if (j != null)
            {
                dondeCai = j.transform.position;
                j.GodMode = true;
                j.hitPoints = Mathf.Max(j.hitPoints, 1f);
                var pw = UnityEngine.Object.FindObjectOfType<PlayerWeapons>();
                if (pw != null) { pw.StartCoroutine(pw.SelectWeapon(0)); pw.NoWeaponsAllowed = true; }
                Fisica(j, false);
            }
            Entrada.Espectando = true;   // sin moverse ni disparar
            Viaje.Avisar("¡Caíste! " + plugin.NombreCompanero + " puede levantarte en " + (int)TiempoDerribado + " s.");
            plugin.Log("Muerte: derribado; " + plugin.NombreCompanero + " tiene " + TiempoDerribado + " s para levantarte.");
        }

        /// Me levantaron: el compañero (manteniendo E) o el perro (porPerro = true).
        public void Levantado(bool porPerro = false)
        {
            if (Mio != Estado.Derribado) return;
            var j = plugin.Jugador;
            if (j != null)
            {
                j.GodMode = false;
                j.hitPoints = Mathf.Max(1f, j.maximumHitPoints * VidaAlLevantar);
                Fisica(j, true);
                var pw = UnityEngine.Object.FindObjectOfType<PlayerWeapons>();
                if (pw != null) pw.NoWeaponsAllowed = false;
            }
            Entrada.Espectando = false;
            Mio = Estado.Vivo;
            fsmRetenida = null; eventoRetenido = null;
            string quien = porPerro ? "El perro" : plugin.NombreCompanero;
            Viaje.Avisar(quien + " te levantó.");
            plugin.Log("Muerte: " + quien + " me levantó (vida " + (j != null ? j.hitPoints.ToString("0") : "?") + ")");
        }

        /// Yo vivo y el compañero derribado a mi lado: mantener E 6 s lo levanta (soltar reinicia).
        void IntentarLevantar(FPSPlayer j)
        {
            var c = plugin.CompaneroActual;
            levantando = false;
            if (DelOtro != Estado.Derribado || c == null || !c.Visible || Mio != Estado.Vivo) { progreso = 0f; return; }
            if (Vector3.Distance(j.transform.position, c.PosicionMundo) > DistanciaLevantar) { progreso = 0f; return; }
            if (Controles.InteractuarMantenida() && Entrada.Enfocado)
            {
                levantando = true;
                progreso += Time.deltaTime;
                if (progreso >= TiempoLevantar)
                {
                    progreso = 0f;
                    plugin.EnviarLevantar();
                    plugin.Log("Muerte: levanté a " + plugin.NombreCompanero);
                }
            }
            else progreso = 0f;
        }

        /// Para las pruebas: simula mantener E el tiempo necesario.
        public void OrdenLevantar() { plugin.EnviarLevantar(); }

        /// (2026-11-19) Aplastado por la roca de la guarida: muerte en el acto, sin que te puedan levantar.
        /// En coop con el compañero vivo pasas a espectador; solo (o si eras el último) muerte normal del juego.
        public void Aplastado()
        {
            var j = plugin.Jugador; if (j == null || Mio == Estado.Espectador || Mio == Estado.Caido) return;
            var c = plugin.CompaneroActual;
            bool otroVivoAqui = plugin.Conectado && c != null && c.Escena == plugin.Escena && DelOtro == Estado.Vivo;
            Viaje.Avisar("¡La roca te aplastó!", 5f);
            if (otroVivoAqui) { Caidas = CaidasMax; EntrarEspectador(); return; }
            j.GodMode = false; j.ApplyDamage(j.hitPoints + 5f);
        }

        void EntrarEspectador()
        {
            Mio = Estado.Espectador;
            desde = Time.time;
            var j = plugin.Jugador;
            if (j != null)
            {
                j.GodMode = true;
                j.hitPoints = Mathf.Max(j.hitPoints, 1f);
                var pw = UnityEngine.Object.FindObjectOfType<PlayerWeapons>();
                if (pw != null) { pw.StartCoroutine(pw.SelectWeapon(0)); pw.NoWeaponsAllowed = true; }
                Fisica(j, false);
            }
            Entrada.Espectando = true;
            plugin.Log("Muerte: espectador hasta que " + plugin.NombreCompanero + " vuelva a la aldea.");
        }

        static void Fisica(FPSPlayer j, bool activa)
        {
            foreach (var col in j.GetComponentsInChildren<Collider>()) col.enabled = activa;
            var rb = j.GetComponent<Rigidbody>();
            if (rb != null) { rb.isKinematic = !activa; if (!activa) rb.velocity = Vector3.zero; }
        }

        void Revivir(bool cargarAldea)
        {
            Caidas = 0;
            var j = plugin.Jugador;
            if (j != null)
            {
                j.GodMode = false;
                j.hitPoints = j.maximumHitPoints;
                Fisica(j, true);
            }
            Entrada.Espectando = false;
            Mio = Estado.Vivo;
            fsmRetenida = null; eventoRetenido = null;
            if (cargarAldea)
            {
                plugin.Log("Muerte: " + plugin.NombreCompanero + " volvió a la aldea; reapareces allí.");
                try
                {
                    PixelCrushers.DialogueSystem.PersistentDataManager.Record();
                    PixelCrushers.DialogueSystem.PersistentDataManager.LevelWillBeUnloaded();
                }
                catch (Exception e) { plugin.Log("Muerte: no se pudo registrar antes de cargar: " + e.Message); }
                SceneManager.LoadScene(EscenaAldea);
            }
        }

        /// Se perdió la conexión mientras estaba caído o espectando (pedido 2026-11-17: la pantalla de muerte solo sale si caen los dos).
        /// El anfitrión reaparece en la aldea con lo que lleva; el invitado ya vuelve al menú por su cuenta (la sala se cerró).
        void SinCompanero()
        {
            plugin.Log("Muerte: se perdió la conexión con el compañero mientras estaba " + (Mio == Estado.Derribado ? "caído" : "espectando") + "; no es una muerte.");
            if (plugin.EsAnfitrion) { Revivir(true); return; }
            var j = plugin.Jugador;
            if (j != null) { j.GodMode = false; Fisica(j, true); }
            Entrada.Espectando = false; Mio = Estado.Vivo; fsmRetenida = null; eventoRetenido = null;
        }

        /// Murió también el compañero: muerte normal del juego para este jugador.
        void MorirDeVerdad()
        {
            plugin.Log("Muerte: cayeron los dos; se pierde la salida (se recupera el 15 %).");
            var fsm = fsmRetenida; var ev = eventoRetenido;
            var j = plugin.Jugador;
            if (j != null) { j.GodMode = false; Fisica(j, true); }
            Entrada.Espectando = false;
            Mio = Estado.Caido;
            PrepararBono();
            fsmRetenida = null; eventoRetenido = null;
            if (fsm != null && ev != null)
            {
                soltando = true;
                try { fsm.ProcessEvent(ev); } finally { soltando = false; }
            }
        }

        // ---------------- Bucle ----------------

        public void Actualizar()
        {
            int escena = plugin.Escena;
            var j = plugin.Jugador;

            // Al dejar la aldea empieza una salida: se anota el inventario.
            if (escena != escenaAnterior)
            {
                if (escenaAnterior == EscenaAldea && escena >= 0 && escena != EscenaAldea && j != null) AnotarSalida();
                if (escena >= 0 && j != null) escenaAnterior = escena;
                if (Mio == Estado.Caido && j != null) { Mio = Estado.Vivo; AplicarBono(); }
                if (escena == EscenaAldea) Caidas = 0;   // nueva salida: vuelve a tener una caída con rescate
            }

            if (invulnerableHasta > 0f && Time.time >= invulnerableHasta)
            {
                invulnerableHasta = -1f;
                if (j != null && Mio == Estado.Vivo) j.GodMode = false;
            }
            if (j != null && Mio == Estado.Vivo) { IntentarLevantar(j); VigilarSinVida(j); }
            if (Mio == Estado.Derribado)
            {
                if (j == null) { Entrada.Espectando = false; Mio = Estado.Vivo; return; }
                if (!plugin.Conectado) { SinCompanero(); return; }   // se cortó la conexión: nunca "has muerto" (solo si caen los dos)
                if (DelOtro == Estado.Caido || DelOtro == Estado.Espectador) { MorirDeVerdad(); return; }
                var co = plugin.CompaneroActual;
                if (co != null && co.Escena == EscenaAldea && escena != EscenaAldea) { Revivir(true); return; }
                if (Time.time - derribadoDesde > TiempoDerribado)
                {
                    plugin.Log("Muerte: nadie me levantó a tiempo.");
                    if (UsarSegundaPosibilidad("tras " + (int)TiempoDerribado + " s en el suelo")) return;
                    EntrarEspectador(); return;
                }
                j.transform.position = dondeCai;
                j.hitPoints = Mathf.Max(j.hitPoints, 1f);
                return;
            }
            if (Mio != Estado.Espectador) return;
            if (j == null) { Entrada.Espectando = false; Mio = Estado.Vivo; return; }   // cambió la escena
            var c = plugin.CompaneroActual;
            if (!plugin.Conectado) { SinCompanero(); return; }   // el compañero se fue o se cortó la conexión: no es una muerte
            if (DelOtro == Estado.Caido || DelOtro == Estado.Espectador && Time.time - desde > 1f) { MorirDeVerdad(); return; }
            if (c != null && c.Escena == EscenaAldea && escena != EscenaAldea) { Revivir(true); return; }

            // El cuerpo invisible acompaña al vivo (así los monstruos se quedan con él).
            if (c != null && c.Visible)
            {
                j.transform.position = c.PosicionMundo + Vector3.up * 1.0f;
                j.hitPoints = Mathf.Max(j.hitPoints, 1f);
            }
        }

        /// Cámara detrás del compañero. Se aplica justo antes de dibujar (después de los scripts
        /// del juego que mueven la cámara del jugador).
        public void Camara(Camera cam)
        {
            if (Mio == Estado.Derribado && cam != null && cam == Camera.main)
            {
                // en el suelo: la cámara baja, mirando hacia donde quiera
                cam.transform.position = dondeCai + Vector3.up * 0.35f;
                return;
            }
            if (Mio != Estado.Espectador || cam == null || cam != Camera.main) return;
            var c = plugin.CompaneroActual;
            if (c == null || !c.Visible) return;
            Vector3 foco = c.PosicionMundo + Vector3.up * 1.6f;
            Vector3 atras = -c.Adelante;
            Vector3 pos = foco + atras * 3.6f + Vector3.up * 1.2f;
            RaycastHit hit;
            if (Physics.Linecast(foco, pos, out hit, ~0, QueryTriggerInteraction.Ignore)) pos = hit.point + (foco - pos).normalized * 0.3f;
            cam.transform.position = pos;
            cam.transform.rotation = Quaternion.LookRotation(foco - pos);
        }

        // ---------------- Botín de la salida ----------------

        void AnotarSalida()
        {
            alSalir.Clear();
            foreach (var n in Botin) { int? v = Global(n); if (v != null) alSalir[n] = v.Value; }
            municionAlSalir = Municion();
            plugin.Log("Salida: inventario anotado (" + string.Join(", ", Describir(alSalir)) + ", munición " + municionAlSalir + ")");
        }

        void PrepararBono()
        {
            if (alSalir.Count == 0) return;
            bonoPendiente = new Dictionary<string, int>();
            foreach (var kv in alSalir)
            {
                // El oro es compartido: solo el anfitrión lo devuelve (el invitado lo recibe sincronizado).
                if (kv.Key == "Coins" && !plugin.EsAnfitrion) continue;
                int? v = Global(kv.Key);
                if (v == null) continue;
                int ganado = v.Value - kv.Value;
                if (ganado > 0) bonoPendiente[kv.Key] = Mathf.FloorToInt(ganado * Recupera);
            }
            int m = Municion();
            bonoMunicion = municionAlSalir >= 0 && m > municionAlSalir ? Mathf.FloorToInt((m - municionAlSalir) * Recupera) : 0;
            plugin.Log("Salida perdida. Se recuperará: " + string.Join(", ", Describir(bonoPendiente)) + ", munición +" + bonoMunicion);
        }

        void AplicarBono()
        {
            if (bonoPendiente == null) return;
            var g = FsmVariables.GlobalVariables;
            foreach (var kv in bonoPendiente)
            {
                var v = g.FindFsmInt(kv.Key);
                if (v != null && kv.Value > 0) v.Value += kv.Value;
            }
            if (bonoMunicion > 0) SumarMunicion(bonoMunicion);
            plugin.Log("Recuperado el 15 %: " + string.Join(", ", Describir(bonoPendiente)) + ", munición +" + bonoMunicion);
            bonoPendiente = null; bonoMunicion = 0;
            alSalir.Clear();
        }

        static int? Global(string n)
        {
            var v = FsmVariables.GlobalVariables.FindFsmInt(n);
            return v == null ? (int?)null : v.Value;
        }

        /// Munición de reserva de las armas de fuego (pistola, mosquete).
        static int Municion()
        {
            var pw = UnityEngine.Object.FindObjectOfType<PlayerWeapons>();
            if (pw == null) return -1;
            int t = 0;
            foreach (var w in pw.weaponOrder)
            {
                var wb = w != null ? w.GetComponent<WeaponBehavior>() : null;
                if (wb != null && wb.haveWeapon && !wb.unarmed && wb.meleeSwingDelay == 0f) t += wb.ammo;
            }
            return t;
        }

        static void SumarMunicion(int n)
        {
            var pw = UnityEngine.Object.FindObjectOfType<PlayerWeapons>();
            if (pw == null) return;
            var armas = new List<WeaponBehavior>();
            foreach (var w in pw.weaponOrder)
            {
                var wb = w != null ? w.GetComponent<WeaponBehavior>() : null;
                if (wb != null && wb.haveWeapon && !wb.unarmed && wb.meleeSwingDelay == 0f) armas.Add(wb);
            }
            for (int i = 0; i < armas.Count; i++) armas[i].ammo += n / armas.Count + (i < n % armas.Count ? 1 : 0);
        }

        static string[] Describir(Dictionary<string, int> d)
        {
            var l = new List<string>();
            foreach (var kv in d) l.Add(kv.Key + "=" + kv.Value);
            return l.ToArray();
        }

        // ---------------- Marcador ----------------
        static GUIStyle estilo;

        public void Dibujar()
        {
            if (estilo == null)
            {
                estilo = new GUIStyle(GUI.skin.label) { fontSize = Opciones.T(20), alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, wordWrap = true };
                estilo.normal.textColor = new Color(0.95f, 0.35f, 0.3f);
            }
            if (Mio == Estado.Derribado)
            {
                int quedan = Mathf.Max(0, Mathf.CeilToInt(TiempoDerribado - (Time.time - derribadoDesde)));
                GUI.Label(new Rect(Screen.width / 2f - 320f, 40f, 640f, 90f), "¡Estás caído! " + quedan + " s\n" + plugin.NombreCompanero + " puede levantarte manteniendo E a tu lado." + (PuedeSegundaPosibilidad() ? "\nSi nadie lo hace, Segunda posibilidad te levantará (−45 de maná)." : ""), estilo);
                // tinte rojo en la pantalla
                var prev = GUI.color; GUI.color = new Color(0.5f, 0f, 0f, 0.25f);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
                GUI.color = prev;
                return;
            }
            if (Mio == Estado.Vivo && DelOtro == Estado.Derribado)
            {
                var c = plugin.CompaneroActual;
                bool cerca = c != null && c.Visible && plugin.Jugador != null && Vector3.Distance(plugin.Jugador.transform.position, c.PosicionMundo) <= DistanciaLevantar;
                string t = cerca ? (levantando ? "Levantando a " + plugin.NombreCompanero + "..." : "Mantén E para levantar a " + plugin.NombreCompanero)
                                 : plugin.NombreCompanero + " está caído: ve a levantarlo.";
                GUI.Label(new Rect(Screen.width / 2f - 320f, Screen.height * 0.6f, 640f, 30f), t, estilo);
                if (cerca && progreso > 0f)
                {
                    var r0 = new Rect(Screen.width / 2f - 150f, Screen.height * 0.6f + 34f, 300f, 14f);
                    var prev = GUI.color;
                    GUI.color = new Color(0f, 0f, 0f, 0.6f); GUI.DrawTexture(r0, Texture2D.whiteTexture);
                    GUI.color = new Color(0.9f, 0.8f, 0.3f, 0.95f); GUI.DrawTexture(new Rect(r0.x, r0.y, r0.width * Mathf.Clamp01(progreso / TiempoLevantar), r0.height), Texture2D.whiteTexture);
                    GUI.color = prev;
                }
                return;
            }
            if (Mio != Estado.Espectador) return;
            var r = new Rect(Screen.width / 2f - 300f, 40f, 600f, 60f);
            GUI.Label(r, "Has caído. Observando a " + plugin.NombreCompanero + ".\nReaparecerás cuando vuelva a la aldea.", estilo);
        }
    }

    /// Retiene el evento que dispara la muerte del jugador (FSM "DeathScript").
    [HarmonyPatch(typeof(Fsm), "ProcessEvent")]
    static class ParcheMuerte
    {
        static bool Prefix(Fsm __instance, FsmEvent fsmEvent)
        {
            if (Muerte.Instancia == null || fsmEvent == null || __instance.Name != "DeathScript") return true;
            if (__instance.ActiveStateName != "State 1") return true;   // ya está muriendo
            // Solo el evento que de verdad dispara la muerte (tiene transición global en esta FSM);
            // DeathScript recibe otros eventos (p. ej. al cargar escena) que no hacen nada.
            bool dispara = false;
            foreach (var t in __instance.GlobalTransitions)
                if (t != null && t.FsmEvent != null && t.FsmEvent.Name == fsmEvent.Name) { dispara = true; break; }
            if (!dispara) return true;
            try { return !Muerte.Instancia.AlMorir(__instance, fsmEvent); }
            catch (Exception e) { Debug.LogError("[WitchHuntCoop] muerte: " + e); return true; }
        }
    }
}

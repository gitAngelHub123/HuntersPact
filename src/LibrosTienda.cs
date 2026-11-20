// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using System.Collections.Generic;
using System.IO;
using PixelCrushers.DialogueSystem;
using UnityEngine;
using UnityEngine.Rendering;

namespace WitchHuntCoop
{
    /// Libros del cooperativo que vende el comerciante del pueblo. Cada jugador compra el suyo (el
    /// libro es solo de quien lo compra), pero se paga con la bolsa de oro compartida: si los dos
    /// quieren el mismo libro, la bolsa paga dos veces.
    public static class LibrosTienda
    {
        public const string Filo = "coop_libro_filo", Pistolero = "coop_libro_pistolero", Mosquetero = "coop_libro_mosquetero",
                            SegundaBala = "coop_libro_segunda_bala", Herbolario = "coop_libro_herbolario", OjoDeBruja = "coop_libro_ojo_bruja",
                            CargamentoPesado = "coop_libro_cargamento", RobinHood = "coop_libro_robin_hood", Vampiro = "coop_libro_vampiro",
                            MercaderSoberano = "coop_libro_mercader", BotasLigeras = "coop_libro_botas", ArmaduraTitanio = "coop_libro_armadura",
                            EscopetaRecarga = "coop_libro_escopeta_recarga", EscopetaDano = "coop_libro_escopeta_dano",
                            BolsaAmpliada = "coop_libro_bolsa_ampliada", CargadorRifle = "coop_libro_cargador_rifle", BallestaDano = "coop_libro_ballesta_dano", BallestaTriple = "coop_libro_ballesta_triple",
                            LlaveMaestra = "coop_libro_llave_maestra";
        public const float FactorRecargaEscopeta = 0.6f;
        public const float FactorEstamina = 1.25f, VidaArmadura = 150f;   // vida máxima con la armadura (no se suma: es el nuevo máximo)
        public const int BalasCargamento = 120;

        public class Libro { public string Var, Nombre, Efecto; public int Precio; }

        public static readonly Libro[] Lista = {
            new Libro { Var = Tienda.VarLibro, Nombre = "Libro de curación", Efecto = "Tu vida se regenera el triple de rápido.", Precio = Tienda.PrecioLibro },
            new Libro { Var = Filo, Nombre = "Filo", Efecto = "+50 % de daño con el estoque.", Precio = 200 },
            new Libro { Var = Pistolero, Nombre = "Pistolero", Efecto = "+25 % de daño con la pistola.", Precio = 250 },
            new Libro { Var = Mosquetero, Nombre = "Mosquetero", Efecto = "+25 % de daño con el mosquete.", Precio = 250 },
            new Libro { Var = SegundaBala, Nombre = "Segunda bala", Efecto = "30 % de que un disparo no gaste la bala.", Precio = 180 },
            new Libro { Var = Herbolario, Nombre = "Herbolario", Efecto = "Las plantas curativas aparecen el doble de veces esparcidas por todo el mapa.", Precio = 120 },
            new Libro { Var = OjoDeBruja, Nombre = "Ojo de bruja", Efecto = "Ves en rojo a los enemigos a menos de 100 m.", Precio = 400 },
            new Libro { Var = CargamentoPesado, Nombre = "Cargamento pesado", Efecto = "Puedes llevar hasta 120 balas en cada arma de fuego.", Precio = 300 },
            new Libro { Var = RobinHood, Nombre = "Robin Hood", Efecto = "Cada enemigo que mates te da una pequeña recompensa.", Precio = 150 },
            new Libro { Var = Vampiro, Nombre = "Vampiro", Efecto = "Al matar a un enemigo tienes un 40 % de probabilidad de curarte vida.", Precio = 175 },
            new Libro { Var = MercaderSoberano, Nombre = "Mercader soberano", Efecto = "Consigues un 45 % más de monedas por los objetos valiosos.", Precio = 250 },
            new Libro { Var = BotasLigeras, Nombre = "Botas súper ligeras", Efecto = "Aumenta tu estamina para correr un 25 %.", Precio = 110 },
            new Libro { Var = ArmaduraTitanio, Nombre = "Armadura de titanio", Efecto = "Tu vida máxima sube a 150.", Precio = 150 },
            new Libro { Var = EscopetaRecarga, Nombre = "Mano rápida", Efecto = "Recargas la escopeta recortada mucho más rápido.", Precio = 250 },
            new Libro { Var = EscopetaDano, Nombre = "Perdigones afilados", Efecto = "+15 % de daño con la escopeta recortada.", Precio = 250 },
            new Libro { Var = CargadorRifle, Nombre = "Cargador ampliado", Efecto = "El rifle dispara 4 veces antes de tener que recargar.", Precio = 300 },
            new Libro { Var = BolsaAmpliada, Nombre = "Bolsa ampliada", Efecto = "Puedes llevar hasta 60 flechas para la ballesta.", Precio = 200 },
            new Libro { Var = BallestaDano, Nombre = "Puntas de acero", Efecto = "+50 % de daño con la ballesta.", Precio = 500 },
            new Libro { Var = BallestaTriple, Nombre = "Disparo triple", Efecto = "La ballesta dispara 3 flechas juntas (gasta 3). Tecla X: 1 o 3 flechas.", Precio = 1000 },
            new Libro { Var = LlaveMaestra, Nombre = "Llave maestra", Efecto = "Tus ganzúas tienen un 100 % más de probabilidad de abrir una cerradura.", Precio = 350 },
        };

        public static bool Tiene(string v)
        {
            try { return DialogueLua.GetVariable(v).AsBool; } catch { return false; }
        }

        public static string Comprar(Plugin plugin, Libro l)
        {
            if (Tiene(l.Var)) return "Ya tienes " + l.Nombre + ".";
            int? oro = Oro.Leer();
            if (oro == null || oro.Value < l.Precio) return "No tienen oro suficiente (" + l.Precio + ").";
            Oro.Gastar(l.Precio);
            DialogueLua.SetVariable(l.Var, true);
            plugin.Log("Tienda: comprado " + l.Nombre + " por " + l.Precio + " oro");
            // Solo los libros abren la ventana "NUEVA DESTREZA CONSEGUIDA", y como en el juego: se cierra
            // la tienda y luego sale la ventana (antes salía encima de la tienda). Lo que no es libro
            // (Filo, Pistolero, Botas…) se compra sin ventana (pedido 2026-11-17).
            if (!TiendaUI.SinLibro(l.Var)) VentanaDestreza.MostrarTrasCerrarTienda(plugin, l);   // solo a quien compra
            return "Compraste " + l.Nombre + ".";
        }

        /// Versiones anteriores del mod avisaban la compra al compañero; ahora cada uno compra el suyo
        /// y el aviso se ignora.
        public static void Recibir(Plugin plugin, BinaryReader r) { r.ReadString(); }

        // ---------------- Efectos ----------------

        static readonly Dictionary<WeaponBehavior, int[]> danoBase = new Dictionary<WeaponBehavior, int[]>();   // [base, puesto]
        static readonly Dictionary<WeaponBehavior, int[]> clipBase = new Dictionary<WeaponBehavior, int[]>();
        static readonly Dictionary<HealthPickup, float[]> curaBase = new Dictionary<HealthPickup, float[]>();
        static float proxima;
        static float estaminaBase = -1f, estaminaPuesta = -1f, vidaBase = -1f, vidaPuesta = -1f;

        static float desgasteAcum, desgasteUlt = -1f;

        /// Pesadilla: cada 2 minutos de juego EN EL BOSQUE se pierde 1 de vida (nunca por debajo de 1: el desgaste
        /// no mata). Fuera del bosque el contador se detiene y sigue al volver. Pedido 2026-11-17.
        static void DesgastePesadilla(FPSPlayer j)
        {
            float t = Time.time;
            bool corre = Opciones.Dificultad >= 2 && Guardado.ModoCoop && Escopeta.plugin != null && Escopeta.plugin.Escena == Mundo.EscenaBosque;
            if (!corre || desgasteUlt < 0f) { desgasteUlt = t; return; }
            desgasteAcum += Mathf.Clamp(t - desgasteUlt, 0f, 2f); desgasteUlt = t;   // (saltos grandes —cargas— no cuentan)
            if (desgasteAcum < 120f) return;
            desgasteAcum = 0f;
            if (j.hitPoints > 1f) { j.hitPoints = Mathf.Max(1f, j.hitPoints - 1f); Escopeta.plugin.Log("Pesadilla: −1 de vida (queda " + j.hitPoints.ToString("0") + ")"); }
        }

        static int fallarJuegoBase = -1;

        /// Cerraduras del propio juego (rejas, puertas…): su variable global UnlockPercentage es la
        /// probabilidad de ABRIR (35; con la habilidad del juego de forzar cerraduras, 65 → «Desbloquear (65%)»).
        /// Con la Llave maestra sube un 100 % (×2, tope 100 %), como en las cerraduras del mod (pedido 2026-11-17).
        static void LlaveMaestraJuego()
        {
            var v = HutongGames.PlayMaker.FsmVariables.GlobalVariables.FindFsmInt("UnlockPercentage");
            if (v == null) return;
            bool llave = Tiene(LlaveMaestra);
            // Con la llave maestra todo abre siempre y sin gastar ganzúas, tenga o no Maestro con ganzúas: la llave manda
            // (el juego pone 65 con su habilidad; se vuelve a poner 100 cada medio segundo). Pedido 2026-11-17.
            if (!llave) { if (fallarJuegoBase >= 0 && v.Value == 100 && llavePuesta) v.Value = fallarJuegoBase; llavePuesta = false; fallarJuegoBase = v.Value; }
            else { if (fallarJuegoBase < 0) fallarJuegoBase = v.Value; llavePuesta = true; if (v.Value != 100) v.Value = 100; }
            SinGanzuasJuego(llave);
            // Los letreros «Desbloquear (X%)» se arman al cargar el mapa (FSM "text": IntToString de UnlockPercentage
            // en number_string → final_text) y solo algunos escuchan UpdateUnlockPercentage: se avisa y se corrigen
            // los ya armados cuando cambia el valor y unos segundos después de cada carga de mapa.
            int escena = UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex;
            if (escena != escenaLetreros) { escenaLetreros = escena; letrerosEn = Time.realtimeSinceStartup + 3f; letrerosHechos = -1; }
            if (v.Value != letrerosHechos && Time.realtimeSinceStartup >= letrerosEn) { letrerosHechos = v.Value; ActualizarLetreros(v.Value); }
        }
        static int escenaLetreros = -2, letrerosHechos = -1; static float letrerosEn; static bool llavePuesta;
        static int escenaSinGanzuas = -2; static float proxSinGanzuas;

        /// Cerraduras del juego: su FSM "Interact" compara Lockpicks con 0 (IntCompare → NoLockpicks) antes de tirar.
        /// Con la llave maestra ese límite pasa a −1 (nunca hace falta ganzúa) y vuelve a 0 sin ella; como abre al 100 %
        /// tampoco se gasta ninguna (el juego solo resta al fallar).
        static void SinGanzuasJuego(bool llave)
        {
            int escena = UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex;
            if (escena == escenaSinGanzuas && Time.realtimeSinceStartup < proxSinGanzuas) return;
            escenaSinGanzuas = escena; proxSinGanzuas = Time.realtimeSinceStartup + 5f;
            int limite = llave ? -1 : 0;
            foreach (var f in Resources.FindObjectsOfTypeAll<PlayMakerFSM>())
            {
                if (f == null || f.Fsm == null || f.FsmName != "Interact" || !f.gameObject.scene.IsValid()) continue;
                foreach (var st in f.FsmStates)
                    foreach (var a in st.Actions)
                    {
                        var ic = a as HutongGames.PlayMaker.Actions.IntCompare;
                        if (ic == null || ic.integer1 == null || ic.integer1.Name != "Lockpicks" || ic.integer2 == null || ic.integer2.UsesVariable) continue;
                        if (ic.integer2.Value != limite) ic.integer2.Value = limite;
                    }
            }
        }

        static void ActualizarLetreros(int pct)
        {
            PlayMakerFSM.BroadcastEvent("UpdateUnlockPercentage");
            string nuevo = pct.ToString(); int n = 0;
            foreach (var f in Resources.FindObjectsOfTypeAll<PlayMakerFSM>())
            {
                if (f.Fsm == null || !f.gameObject.scene.IsValid()) continue;
                var num = f.FsmVariables.FindFsmString("number_string"); var fin = f.FsmVariables.FindFsmString("final_text");
                if (num == null || fin == null || string.IsNullOrEmpty(fin.Value) || num.Value == nuevo) continue;
                string viejo = "(" + num.Value + "%)";
                if (!fin.Value.Contains(viejo)) continue;
                fin.Value = fin.Value.Replace(viejo, "(" + nuevo + "%)"); num.Value = nuevo; n++;
            }
            Escopeta.plugin.Log("Cerraduras del juego: " + pct + " % de abrir, " + n + " letreros corregidos");
        }
        static int ConLlave(int abrir) { return 100; }

        /// Daño de las armas y curación de las plantas según los libros (cada medio segundo).
        public static void Actualizar(FPSPlayer j)
        {
            if (j == null || Time.realtimeSinceStartup < proxima) return;   // tiempo real: la ventana de destreza pausa el juego
            proxima = Time.realtimeSinceStartup + 0.5f;
            LlaveMaestraJuego();
            DesgastePesadilla(j);
            var pw = Rendimiento.Armas;
            if (pw != null && pw.weaponOrder != null)
                foreach (var w in pw.weaponOrder)
                {
                    if (w == null) continue;
                    var wb = w.GetComponent<WeaponBehavior>();
                    if (wb == null) continue;
                    float f = 1f;
                    if (w.name.StartsWith("Hatchet") && Tiene(Filo)) f = 1.5f;
                    else if (w.name.StartsWith("Pistol") && Tiene(Pistolero)) f = 1.25f;
                    else if (w.name.StartsWith("Musket") && Tiene(Mosquetero)) f = 1.25f;
                    else if (w.name == Escopeta.Nombre && Tiene(EscopetaDano)) f = 1.15f;
                    if (w.name.StartsWith("Musket"))   // Cargador ampliado: 4 disparos (3 más) y se recarga todo de una vez
                    {
                        int[] cb;
                        if (!clipBase.TryGetValue(wb, out cb)) { cb = new[] { wb.bulletsPerClip, wb.bulletsToReload }; clipBase[wb] = cb; }
                        bool amp = Tiene(CargadorRifle);
                        int clip = amp ? 4 : cb[0], rec = amp ? 4 : cb[1];
                        if (wb.bulletsPerClip != clip) { wb.bulletsPerClip = clip; }
                        if (wb.bulletsToReload != rec) wb.bulletsToReload = rec;
                    }
                    int[] d;
                    if (!danoBase.TryGetValue(wb, out d)) { d = new[] { wb.damage, wb.damage }; danoBase[wb] = d; }
                    if (wb.damage != d[1]) d[0] = wb.damage;   // lo cambió el juego: nueva base
                    int nuevo = Mathf.RoundToInt(d[0] * f);
                    if (wb.damage != nuevo) wb.damage = nuevo;
                    d[1] = nuevo;
                }
            // Cargamento pesado: el juego limita las balas con MaxAmmo_Current (20, o 60 con su bolsa).
            if (Tiene(CargamentoPesado))
            {
                // El juego copia MaxAmmo_Default (o MaxAmmo_Increased con su bolsa) en MaxAmmo_Current: las tres.
                foreach (var n in new[] { "MaxAmmo_Current", "MaxAmmo_Default", "MaxAmmo_Increased" })
                {
                    var mx = HutongGames.PlayMaker.FsmVariables.GlobalVariables.FindFsmInt(n);
                    if (mx != null && mx.Value < BalasCargamento) mx.Value = BalasCargamento;
                }
            }
            // Botas súper ligeras: más segundos de carrera (si el juego cambia la base, se respeta).
            var andar = Object.FindObjectOfType<FPSRigidBodyWalker>();
            if (andar != null)
            {
                float f = Tiene(BotasLigeras) ? FactorEstamina : 1f;
                if (estaminaPuesta < 0f || Mathf.Abs(andar.staminaForSprint - estaminaPuesta) > 0.001f) estaminaBase = andar.staminaForSprint;
                estaminaPuesta = estaminaBase * f;
                andar.staminaForSprint = estaminaPuesta;
            }
            // Armadura de titanio: la vida máxima pasa a ser 150 (si ya era mayor, se queda).
            var maxG = HutongGames.PlayMaker.FsmVariables.GlobalVariables.FindFsmFloat("Player_Max_Health");
            if (maxG != null)
            {
                bool arm = Tiene(ArmaduraTitanio);
                if (vidaPuesta < 0f || Mathf.Abs(maxG.Value - vidaPuesta) > 0.01f) vidaBase = maxG.Value;
                float nueva = arm ? Mathf.Max(vidaBase, VidaArmadura) : vidaBase;
                maxG.Value = nueva; vidaPuesta = nueva;
                j.maximumHitPoints = Mathf.Max(j.maximumHitPoints, nueva);
            }
        }

        public static string Describir()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var l in Lista) sb.Append(l.Nombre).Append("=").Append(Tiene(l.Var) ? "sí" : "no").Append(" ");
            var pw = Rendimiento.Armas;
            if (pw != null) foreach (var w in pw.weaponOrder) { var wb = w != null ? w.GetComponent<WeaponBehavior>() : null; if (wb != null) sb.Append("| ").Append(w.name).Append(" daño=").Append(wb.damage).Append(" "); }
            foreach (var h in Object.FindObjectsOfType<HealthPickup>()) { sb.Append("| planta cura ").Append(h.healthToAdd); break; }
            return sb.ToString();
        }

        // ---------------- Ojo de bruja ----------------

        static Material silueta;
        static Mesh horneada;
        static readonly List<Renderer> cercanos = new List<Renderer>();
        static float proximaBusqueda;

        /// Mallas que no son el cuerpo del enemigo sino un efecto: el cilindro de distorsión del Deadblind (la vista borrosa),
        /// brillos, auras… Pintadas de rojo salían como un cilindro macizo (2026-11-18).
        static bool EsMallaEfecto(Renderer r)
        {
            if (r is SkinnedMeshRenderer) return false;
            var mf = r.GetComponent<MeshFilter>(); string malla = mf != null && mf.sharedMesh != null ? mf.sharedMesh.name.ToLowerInvariant() : "";
            if (malla.StartsWith("cylinder") || malla.StartsWith("capsule") || malla.StartsWith("sphere") || malla.StartsWith("quad")) return true;
            foreach (var m in r.sharedMaterials)
            {
                if (m == null || m.shader == null) continue;
                string sh = m.shader.name.ToLowerInvariant();
                if (sh.Contains("distort") || sh.Contains("refract") || sh.Contains("heat") || sh.Contains("particle") || sh.Contains("glass") || sh.Contains("fx") || sh.Contains("additive")) return true;
            }
            return false;
        }

        /// Dibuja en rojo, siempre (tapados o no), a los enemigos a menos de 100 m.
        public static void Silueta(Camera cam)
        {
            if (cam == null || cam != Camera.main || !Tiene(OjoDeBruja)) return;
            var j = Object.FindObjectOfType<FPSPlayer>();
            if (j == null) return;
            if (silueta == null)
            {
                var sh = Shader.Find("Hidden/Internal-Colored");
                if (sh == null) return;
                silueta = new Material(sh) { hideFlags = HideFlags.HideAndDontSave, color = new Color(1f, 0.08f, 0.04f, 0.6f) };
                silueta.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                silueta.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                silueta.SetInt("_Cull", (int)CullMode.Off);
                silueta.SetInt("_ZWrite", 0);
                silueta.SetInt("_ZTest", (int)CompareFunction.Always);   // siempre entero en rojo (con Greater solo salía donde algo lo tapaba y parpadeaba al moverse; 2026-11-17)
                horneada = new Mesh();
            }
            if (Time.time > proximaBusqueda)
            {
                proximaBusqueda = Time.time + 0.5f;
                cercanos.Clear();
                foreach (var td in Object.FindObjectsOfType<TakeDamage>())
                {
                    if (td == null || (td.transform.position - j.transform.position).sqrMagnitude > 100f * 100f) continue;
                    if (td.GetComponentInParent<FPSPlayer>() != null) continue;
                    // no son enemigos: el perro de la historia (encerrado tras la reja), el perro compañero, la cabra
                    // negra y los aliados de la bola de cristal (el perro salía rojo detrás de la reja; 2026-11-17)
                    if (Aliados.Protegido(td.transform)) continue;
                    if (Eventos.Muerto(td)) continue;   // los cadáveres no (salían rojos tirados en el suelo)
                    bool aliado = false; for (var x = td.transform; x != null; x = x.parent) if (x.name.StartsWith("coop_aliado_")) { aliado = true; break; }
                    if (aliado) continue;
                    Transform u = td.transform;
                    while (u.parent != null && u.GetComponent<UnityEngine.AI.NavMeshAgent>() == null) u = u.parent;
                    foreach (var r in u.GetComponentsInChildren<Renderer>()) if ((r is SkinnedMeshRenderer || r is MeshRenderer) && !EsMallaEfecto(r)) cercanos.Add(r);
                }
            }
            silueta.SetPass(0);
            foreach (var r in cercanos)
            {
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;   // solo lo que de verdad se dibuja (un enemigo invisible salía como una mancha borrosa)
                if ((r.transform.position - j.transform.position).sqrMagnitude > 100f * 100f) continue;
                var sm = r as SkinnedMeshRenderer;
                if (sm != null)
                {
                    if (sm.sharedMesh == null) continue;
                    sm.BakeMesh(horneada);
                    Graphics.DrawMeshNow(horneada, Matrix4x4.TRS(sm.transform.position, sm.transform.rotation, Vector3.one));
                }
                else
                {
                    var mf = r.GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null) Graphics.DrawMeshNow(mf.sharedMesh, r.transform.localToWorldMatrix);
                }
            }
        }
    }
}

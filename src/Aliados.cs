// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using System.Collections.Generic;
using System.IO;
using PixelCrushers.DialogueSystem;
using UnityEngine;
using UnityEngine.AI;

namespace WitchHuntCoop
{
    /// Bola de cristal de magia oscura (tecla 5). Solo la vende el comerciante (750).
    ///  - Cada enemigo o guardián que matas mientras tienes la bola queda tragado: su alma se guarda
    ///    (hasta 10, se conservan en la partida).
    ///  - Con la bola en la mano, clic izquierdo invoca un alma como aliado: el mismo monstruo, con un
    ///    tinte violeta, que te sigue y ataca a los enemigos cercanos. Máximo 2 aliados por jugador;
    ///    cada uno dura 3 minutos (o hasta salir del bosque).
    ///  - Los aliados los mueve su dueño; el compañero los ve con lo que le manda (mensaje Aliado).
    public class Aliados
    {
        public const string VarBola = "coop_tiene_bola", VarAlmas = "coop_almas";
        public const string NombreArma = "CrystalBall_coop";
        public const int Precio = 750, MaxAliados = 2, MaxAlmas = 10, Indice = 5;
        const float Duracion = 180f, RadioCaza = 25f, Alcance = 2.6f, Cadencia = 1.2f;
        public const float FactorVida = 0.5f, FactorDano = 0.5f;   // los aliados nacen con la mitad de la vida y del daño del original
        static readonly Color AuraPropia = new Color(1f, 1f, 1f), AuraCompanero = new Color(0.45f, 0.85f, 1f);

        public static Aliados Instancia;
        readonly Plugin plugin;
        public Aliados(Plugin p) { plugin = p; Instancia = this; }

        class Aliado
        {
            public int Slot; public string Tipo; public GameObject Go; public NavMeshAgent Ag; public Animator An;
            public float Fin, ProxGolpe, ProxBusqueda; public Transform Presa; public bool Golpeando;
            public string Estado; public int[] Hashes;   // 0 quieto, 1 andar, 2 correr, 3 atacar
            public AnimationClip[] Clips, Originales; public AnimationClip Base, ClipActual; public AnimatorOverrideController Override;
            // remoto
            public Vector3 Pos; public float Yaw; public int AnimHash; public float Visto;
            public float Vida, VidaMax, Dano; public VidaAliado Cuerpo;
        }

        /// Recibe los golpes de los enemigos (el juego se los manda con SendMessage "ApplyDamage").
        public class VidaAliado : MonoBehaviour
        {
            public float Vida = 100f, VidaMax = 100f;
            public System.Action<float> Remoto;   // aliado del compañero: el golpe se le manda a su dueño
            public void ApplyDamage(float d)
            {
                if (d <= 0f) return;
                if (Remoto != null) { Remoto(d); return; }
                Vida = Mathf.Max(0f, Vida - d);
            }
            public void Damage(float d) { ApplyDamage(d); }
        }
        readonly List<Aliado> mios = new List<Aliado>();
        readonly Dictionary<int, Aliado> remotos = new Dictionary<int, Aliado>();
        readonly Dictionary<string, GameObject> plantillas = new Dictionary<string, GameObject>();
        GameObject deposito;   // padre apagado: lo que se copia ahí dentro no se despierta
        GameObject arma, bola;
        int escenaVista = -1;
        float proximoEnvio;

        public static bool TieneBola { get { try { return DialogueLua.GetVariable(VarBola).AsBool; } catch { return false; } } }

        // ---------------- Almas ----------------

        public static List<string> Almas()
        {
            var l = new List<string>(); string s = "";
            try { s = DialogueLua.GetVariable(VarAlmas).AsString; } catch { }
            if (!string.IsNullOrEmpty(s) && s != "nil") foreach (var x in s.Split('|')) if (x.Length > 0) l.Add(x);
            return l;
        }
        static void GuardarAlmas(List<string> l) { DialogueLua.SetVariable(VarAlmas, string.Join("|", l.ToArray())); }

        /// El perro de la historia (GoodDog), nuestro perro compañero y la cabra negra no se tocan (ni ojos rojos, ni elementales, ni dificultad: se quedan como vienen).
        public static bool Protegido(Transform t)
        {
            for (var x = t; x != null; x = x.parent) if (x.name.StartsWith("GoodDog") || x.name.StartsWith("coop_perro") || x.name.StartsWith("German_Shepherd") || x.name.StartsWith("Goat") || x.name.StartsWith("Snake")) return true;   // las serpientes, como la cabra negra: sin cambios
            return false;
        }

        public void OrdenJunto()
        {
            var td = Object.FindObjectOfType<FPSPlayer>();
            foreach (var t in Object.FindObjectsOfType<TakeDamage>())
            {
                if (t == null || !t.gameObject.activeInHierarchy || t.GetComponentInParent<FPSPlayer>() != null || Protegido(t.transform) || Dificultad.EsJefe(t)) continue;
                var u = Combate.RaizDe(t);
                if (Vector3.Distance(u.position, td.transform.position) > 60f) continue;
                foreach (var a in mios) if (a.Ag != null && a.Ag.isActiveAndEnabled) { NavMeshHit nh; if (NavMesh.SamplePosition(u.position + u.forward * 2f, out nh, 5f, NavMesh.AllAreas)) a.Ag.Warp(nh.position); }
                plugin.Log("  aliados junto a " + u.name);
                return;
            }
        }

        static string TipoDe(Transform unidad)
        {
            string n = unidad.name.Replace("(Clone)", "").Replace(" +P", "").Replace(" [coop]", "").Trim();
            int i = n.IndexOf(" ("); if (i > 0) n = n.Substring(0, i);
            return n;
        }

        /// Lo llama Habilidades.Mate: un enemigo que maté queda dentro de la bola.
        public void Tragar(TakeDamage td)
        {
            if (!TieneBola || td == null) return;
            if (Dificultad.EsJefe(td)) return;   // los jefes no caben en la bola
            if (Protegido(td.transform)) return;
            Transform u = Combate.RaizDe(td);
            if (u.parent != null) u = u.parent;   // el monstruo entero (lo que camina es un hijo)
            string tipo = TipoDe(u);
            var almas = Almas();
            if (almas.Count >= MaxAlmas) { Viaje.Avisar("La bola de cristal está llena (" + MaxAlmas + " almas)."); return; }
            almas.Add(tipo); GuardarAlmas(almas);
            if (!plantillas.ContainsKey(tipo) || plantillas[tipo] == null) plantillas[tipo] = Plantilla(u.gameObject);
            Destello(u.position + Vector3.up, new Color(0.6f, 0.2f, 1f));
            Viaje.Avisar("La bola de cristal se tragó un alma (" + almas.Count + ").");
            plugin.Log("Bola: alma de " + tipo + " (" + almas.Count + " almas)");
        }

        GameObject Deposito()
        {
            if (deposito == null) { deposito = new GameObject("coop_almas"); deposito.SetActive(false); }
            return deposito;
        }

        /// Copia apagada y sin comportamiento de un monstruo (para invocarlo o mostrarlo).
        GameObject Plantilla(GameObject original)
        {
            var c = (GameObject)Object.Instantiate(original, Deposito().transform, false);
            c.name = "coop_plantilla_" + TipoDe(original.transform);
            // Fuera ya (no al final del cuadro): si se copia en este mismo cuadro, no debe llevarse la IA ni la vida del juego.
            for (int vuelta = 0; vuelta < 2; vuelta++)   // dos vueltas: algunos dependen de otros
                foreach (var mb in c.GetComponentsInChildren<MonoBehaviour>(true)) if (mb != null) try { Object.DestroyImmediate(mb); } catch { }
            Modelos.Desactivar(c);
            return c;
        }

        /// Plantilla de un tipo: la guardada o, si no hay (otra entrada al bosque), un monstruo vivo de ese tipo.
        GameObject PlantillaDe(string tipo)
        {
            GameObject p;
            if (plantillas.TryGetValue(tipo, out p) && p != null) return p;
            foreach (var td in Object.FindObjectsOfType<TakeDamage>())
            {
                if (td == null || td.GetComponentInParent<FPSPlayer>() != null) continue;
                Transform u = Combate.RaizDe(td);
                if (u.parent != null) u = u.parent;
                if (TipoDe(u) == tipo) { p = Plantilla(u.gameObject); plantillas[tipo] = p; return p; }
            }
            var m = Modelos.Buscar(tipo);
            if (m != null) { p = Plantilla(m); plantillas[tipo] = p; }
            return p;
        }

        static readonly Dictionary<string, float[]> estadisticas = new Dictionary<string, float[]>();

        /// Vida y daño del monstruo original (de las variables de sus máquinas de estados en el prefab del juego).
        static void Estadisticas(string tipo, out float vida, out float dano)
        {
            float[] e;
            if (!estadisticas.TryGetValue(tipo, out e))
            {
                e = new[] { 100f, 30f };
                GameObject prefab = null;
                foreach (var g in Resources.FindObjectsOfTypeAll<GameObject>())
                    if (g.name == tipo && !g.scene.IsValid() && g.transform.parent == null && g.GetComponentInChildren<TakeDamage>(true) != null) { prefab = g; break; }
                if (prefab == null)
                    foreach (var td in Object.FindObjectsOfType<TakeDamage>())
                    {
                        Transform u = Combate.RaizDe(td); if (u.parent != null) u = u.parent;
                        if (TipoDe(u) == tipo) { prefab = u.gameObject; break; }
                    }
                if (prefab != null)
                    foreach (var f in prefab.GetComponentsInChildren<PlayMakerFSM>(true))
                        foreach (var v in f.FsmVariables.FloatVariables)
                        {
                            string n = v.Name.ToLowerInvariant();
                            if (n == "health" || n == "maxhealth" || n == "max_health") e[0] = Mathf.Max(e[0] == 100f ? 0f : e[0], v.Value);
                            else if ((n.Contains("attack") && n.Contains("damage")) || n == "damage_to_player" || n == "playerdamage" || n == "hitdamage") e[1] = v.Value;
                        }
                if (e[0] <= 0f) e[0] = 100f;
                estadisticas[tipo] = e;
                Debug.Log("[WitchHuntCoop] estadísticas de " + tipo + ": vida " + e[0] + ", daño " + e[1] + (prefab == null ? " (por defecto)" : ""));
            }
            vida = e[0]; dano = e[1];
        }

        // ---------------- Bucle ----------------

        public void Actualizar(bool conectado)
        {
            int esc = plugin.Escena;
            if (esc != escenaVista)
            {
                escenaVista = esc; mios.Clear(); remotos.Clear(); plantillas.Clear(); arma = null; bola = null; deposito = null;
                originales.Clear(); variablesDe.Clear(); ultimoGolpe.Clear();
            }
            var j = plugin.Jugador;
            if (j == null || !Guardado.ModoCoop) return;
            var pw = Rendimiento.Armas;
            ArmaBola(pw);
            bool enMano = arma != null && pw != null && pw.currentWeapon >= 0 && pw.currentWeapon < pw.weaponOrder.Length && pw.weaponOrder[pw.currentWeapon] == arma;
            VerBola(enMano);
            if (enMano && Input.GetMouseButtonDown(0) && Entrada.Enfocado) Invocar(j);
            float t = Time.time;
            for (int i = mios.Count - 1; i >= 0; i--)
            {
                var a = mios[i];
                if (a.Cuerpo != null) a.Vida = a.Cuerpo.Vida;
                if (a.Go == null || t > a.Fin || a.Vida <= 0f) { if (a.Vida <= 0f) Viaje.Avisar("Tu aliado cayó."); Despedir(a); mios.RemoveAt(i); continue; }
                Mover(a, j, t);
            }
            foreach (var a in remotos.Values) MoverRemoto(a);
            if (plugin.Escena == Mundo.EscenaBosque) { try { ActualizarEnemistad(j); } catch (System.Exception e) { plugin.Log("Bola: error de enemistad " + e.Message); proximaEnemistad = Time.time + 5f; } }
            if (conectado && Time.realtimeSinceStartup > proximoEnvio)
            {
                proximoEnvio = Time.realtimeSinceStartup + 0.1f;
                foreach (var a in mios) Enviar(a, true);
            }
            // remotos que dejaron de llegar
            List<int> fuera = null;
            foreach (var kv in remotos) if (Time.realtimeSinceStartup - kv.Value.Visto > 3f) (fuera ?? (fuera = new List<int>())).Add(kv.Key);
            if (fuera != null) foreach (var k in fuera) { if (remotos[k].Go != null) Object.Destroy(remotos[k].Go); remotos.Remove(k); }
        }

        // ---------------- Arma 5: la bola en la mano ----------------

        void ArmaBola(PlayerWeapons pw)
        {
            if (pw == null || pw.weaponOrder == null || pw.weaponOrder.Length == 0 || pw.weaponOrder[0] == null) return;
            if (arma == null || pw.weaponOrder.Length <= Indice || pw.weaponOrder[Indice] != arma)
            {
                var vacio = pw.weaponOrder[0];
                Transform ya = vacio.transform.parent != null ? vacio.transform.parent.Find(NombreArma) : null;
                arma = ya != null ? ya.gameObject : null;
                if (arma == null)
                {
                    bool act = vacio.activeSelf; vacio.SetActive(false);
                    arma = (GameObject)Object.Instantiate(vacio, vacio.transform.parent, false);
                    vacio.SetActive(act);
                    arma.name = NombreArma;
                }
                var wb0 = arma.GetComponent<WeaponBehavior>();
                if (wb0 != null) wb0.weaponNumber = Indice;
                var lista = new List<GameObject>(pw.weaponOrder);
                while (lista.Count <= Indice) lista.Add(null);
                lista[Indice] = arma;
                for (int i = 0; i < lista.Count; i++) if (lista[i] == null) lista[i] = pw.weaponOrder[0];
                pw.weaponOrder = lista.ToArray();
                plugin.Log("Bola: arma " + Indice + " lista (tiene=" + TieneBola + ")");
            }
            var wb = arma.GetComponent<WeaponBehavior>();
            if (wb != null && wb.haveWeapon != TieneBola) wb.haveWeapon = TieneBola;
        }

        void VerBola(bool enMano)
        {
            var cam = Camera.main;
            if (bola == null && cam != null)
            {
                bola = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Object.Destroy(bola.GetComponent<Collider>());
                bola.name = "coop_bola_en_mano";
                bola.transform.SetParent(cam.transform, false);
                bola.transform.localPosition = new Vector3(0.2f, -0.19f, 0.48f);
                bola.transform.localScale = Vector3.one * 0.15f;
                var m = new Material(Shader.Find("Standard")) { name = "coop_bola" };
                m.mainTexture = TexturaBola(); m.color = new Color(0.35f, 0.15f, 0.55f);
                m.SetFloat("_Glossiness", 0.95f); m.SetFloat("_Metallic", 0.1f);
                m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", new Color(0.35f, 0.08f, 0.6f));
                m.SetTexture("_EmissionMap", m.mainTexture);
                // sin el brillo de la luz (ese punto celeste ahora es el radar de ManoBola)
                m.SetFloat("_SpecularHighlights", 0f); m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
                m.SetFloat("_GlossyReflections", 0f); m.EnableKeyword("_GLOSSYREFLECTIONS_OFF");
                bola.GetComponent<Renderer>().sharedMaterial = m;
                bola.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var luz = new GameObject("luz"); luz.transform.SetParent(bola.transform, false);
                var l = luz.AddComponent<Light>(); l.type = LightType.Point; l.color = new Color(0.6f, 0.25f, 1f); l.range = 2.5f; l.intensity = 1.2f;
            }
            if (bola == null) return;
            if (bola.activeSelf != enMano) bola.SetActive(enMano);
            ManoBola.plugin = plugin;
            Vector3? sobrePalma = null;
            try { sobrePalma = ManoBola.Actualizar(arma, enMano, cam); } catch (System.Exception e) { plugin.Log("ManoBola: " + e.Message); }
            if (enMano)
            {
                if (sobrePalma.HasValue)
                {
                    bola.transform.position = sobrePalma.Value;
                    int capa = ManoBola.Capa;
                    if (capa >= 0 && bola.layer != capa)
                    {
                        foreach (var t in bola.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = capa;
                        // la luz violeta no tiñe la mano (piel normal, como en estudio_mano_izquierda.png)
                        foreach (var lz in bola.GetComponentsInChildren<Light>(true)) lz.cullingMask = ~(1 << capa);
                    }
                }
                bola.transform.Rotate(0f, 40f * Time.deltaTime, 15f * Time.deltaTime, Space.Self);
                float p = 1f + 0.04f * Mathf.Sin(Time.time * 3f);
                bola.transform.localScale = Vector3.one * 0.15f * p;
                ManoBola.OcultarRadar();   // radar quitado (pedido 2026-11-18): la bola ya no rastrea enemigos
            }
        }

        static Texture2D texBola;
        static Texture2D TexturaBola()
        {
            if (texBola != null) return texBola;
            texBola = new Texture2D(128, 64, TextureFormat.RGB24, false) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Repeat };
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 128; x++)
                {
                    float u = x / 128f * Mathf.PI * 2f, v = y / 64f * Mathf.PI;
                    float remolino = 0.5f + 0.5f * Mathf.Sin(u * 3f + Mathf.Sin(v * 4f) * 2.5f + v * 5f);
                    float k = Mathf.Pow(remolino, 3f);
                    texBola.SetPixel(x, y, new Color(0.25f + 0.6f * k, 0.05f + 0.15f * k, 0.4f + 0.6f * k));
                }
            texBola.Apply();
            return texBola;
        }

        // ---------------- Invocar ----------------

        void Invocar(FPSPlayer j)
        {
            if (mios.Count >= MaxAliados) { Viaje.Avisar("Ya tienes " + MaxAliados + " aliados invocados."); return; }
            var almas = Almas();
            if (almas.Count == 0) { Viaje.Avisar("La bola de cristal está vacía: mata enemigos para llenarla."); return; }
            string tipo = almas[almas.Count - 1];
            var p = PlantillaDe(tipo);
            if (p == null) { Viaje.Avisar("Esa alma no puede salir aquí."); plugin.Log("Bola: sin modelo para " + tipo); return; }
            Vector3 donde = j.transform.position + j.transform.forward * 3f;
            NavMeshHit nh;
            if (NavMesh.SamplePosition(donde, out nh, 6f, NavMesh.AllAreas)) donde = nh.position;
            almas.RemoveAt(almas.Count - 1); GuardarAlmas(almas);
            int slot = 0; while (mios.Exists(x => x.Slot == slot)) slot++;
            var a = Crear(p, tipo, donde, Quaternion.LookRotation(j.transform.forward), true);
            a.Slot = slot; a.Fin = Time.time + Duracion;
            mios.Add(a);
            Destello(donde + Vector3.up, new Color(0.6f, 0.2f, 1f));
            Viaje.Avisar("Invocaste un aliado (" + almas.Count + " almas en la bola).");
            plugin.Log("Bola: aliado " + tipo + " invocado en " + (donde - Anclaje.Origen()).ToString("F0"));
        }

        Aliado Crear(GameObject plantilla, string tipo, Vector3 donde, Quaternion giro, bool propio)
        {
            var go = (GameObject)Object.Instantiate(plantilla, donde, giro);
            go.name = "coop_aliado_" + tipo;
            go.transform.SetParent(null, true);
            go.transform.localScale = plantilla.transform.lossyScale;
            // Lo que camina es un hijo con un desfase grande respecto de la raíz (donde nació el monstruo): a cero.
            foreach (var ag0 in go.GetComponentsInChildren<NavMeshAgent>(true))
                if (ag0.transform != go.transform) { ag0.transform.localPosition = Vector3.zero; ag0.transform.localRotation = Quaternion.identity; }
            go.SetActive(true);
            // Aura: blanca para quien lo invocó, celeste para el compañero (un brillo suave del mismo color en el cuerpo).
            Color aura = propio ? AuraPropia : AuraCompanero;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var ms = r.materials;
                foreach (var m in ms)
                    if (m.HasProperty("_EmissionColor")) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", aura * 0.12f); }
                r.materials = ms;
            }
            var luz = new GameObject("coop_luz_aliado"); luz.transform.SetParent(go.transform, false); luz.transform.localPosition = Vector3.up * 1.4f;
            var l = luz.AddComponent<Light>(); l.type = LightType.Point; l.color = aura; l.range = 5f; l.intensity = 1.6f; l.shadows = LightShadows.None;
            var luzCaminante = go.GetComponentInChildren<NavMeshAgent>(true);
            if (luzCaminante != null && luzCaminante.transform != go.transform) luz.transform.SetParent(luzCaminante.transform, false);
            var a = new Aliado { Tipo = tipo, Go = go };
            float vb, db; Estadisticas(tipo, out vb, out db);
            a.VidaMax = a.Vida = Mathf.Max(10f, vb * FactorVida); a.Dano = Mathf.Max(5f, db * FactorDano);
            a.Cuerpo = go.AddComponent<VidaAliado>(); a.Cuerpo.Vida = a.Cuerpo.VidaMax = a.VidaMax;
            a.An = go.GetComponentInChildren<Animator>(true);
            if (a.An != null)
            {
                for (Transform x = a.An.transform; x != null && x != go.transform.parent; x = x.parent) x.gameObject.SetActive(true);
                a.An.enabled = true; a.An.applyRootMotion = false; a.An.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                var fe = typeof(Animator).GetProperty("fireEvents");   // sin eventos de animación (llaman a la IA que ya no tiene)
                if (fe != null) try { fe.SetValue(a.An, false, null); } catch { }
                a.Hashes = Estados(a.An);
                PrepararClips(a);
            }
            a.Ag = go.GetComponent<NavMeshAgent>();
            foreach (var ag0 in go.GetComponentsInChildren<NavMeshAgent>(true)) if (ag0 != a.Ag) ag0.enabled = false;
            if (propio)
            {
                if (a.Ag == null) a.Ag = go.AddComponent<NavMeshAgent>();
                if (a.Ag != null)
                {
                    a.Ag.enabled = true; a.Ag.speed = 6f; a.Ag.acceleration = 20f; a.Ag.angularSpeed = 480f; a.Ag.stoppingDistance = 2f;
                    a.Ag.updatePosition = true; a.Ag.updateRotation = true;
                    a.Ag.Warp(donde);
                }
            }
            else if (a.Ag != null) a.Ag.enabled = false;
            return a;
        }

        /// Estados del Animator para quieto, andar, correr y atacar (por el nombre de sus animaciones).
        static int[] Estados(Animator an)
        {
            var r = new int[4];
            if (an.runtimeAnimatorController == null) return r;
            string[][] claves = { new[] { "idle", "stand" }, new[] { "walk" }, new[] { "run", "chase", "walk" }, new[] { "attack", "hit", "bite" } };
            for (int k = 0; k < 4; k++)
                foreach (var cl in an.runtimeAnimatorController.animationClips)
                {
                    if (cl == null) continue;
                    string n = cl.name.ToLowerInvariant(); bool ok = false;
                    foreach (var c in claves[k]) if (n.Contains(c)) { ok = true; break; }
                    if (!ok) continue;
                    int h = Animator.StringToHash(cl.name);
                    if (an.HasState(0, h)) { r[k] = h; break; }
                    h = Animator.StringToHash("Base Layer." + cl.name);
                    if (an.HasState(0, h)) { r[k] = h; break; }
                }
            return r;
        }

        void Mover(Aliado a, FPSPlayer j, float t)
        {
            if (a.Ag == null || !a.Ag.isActiveAndEnabled) return;
            if (!a.Ag.isOnNavMesh)
            {
                NavMeshHit nh;
                if (NavMesh.SamplePosition(j.transform.position - j.transform.forward * 2f, out nh, 12f, NavMesh.AllAreas)) a.Ag.Warp(nh.position);
                return;
            }
            Vector3 yo = j.transform.position;
            if (t > a.ProxBusqueda) { a.ProxBusqueda = t + 0.4f; a.Presa = BuscarPresa(a, yo); }
            a.Golpeando = false;
            if (a.Presa != null)
            {
                a.Ag.stoppingDistance = 1.6f;
                a.Ag.SetDestination(a.Presa.position);
                if (Vector3.Distance(a.Go.transform.position, a.Presa.position) < Alcance)
                {
                    a.Golpeando = true;
                    Vector3 d = a.Presa.position - a.Go.transform.position; d.y = 0f;
                    if (d.sqrMagnitude > 0.01f) a.Go.transform.rotation = Quaternion.Slerp(a.Go.transform.rotation, Quaternion.LookRotation(d), 0.25f);
                    if (t > a.ProxGolpe)
                    {
                        a.ProxGolpe = t + Cadencia;
                        var zona = a.Presa.GetComponentInChildren<CharacterDamagePlaymakerAI>();
                        if (zona != null) zona.ApplyDamage(a.Dano, false);   // el combate compartido lo pasa al dueño del monstruo
                    }
                }
            }
            else
            {
                a.Ag.stoppingDistance = 2.5f;
                float dd = Vector3.Distance(a.Go.transform.position, yo);
                if (dd > 3.5f) a.Ag.SetDestination(yo);
                if (dd > 60f)
                {
                    NavMeshHit nh;
                    if (NavMesh.SamplePosition(yo - j.transform.forward * 2f, out nh, 10f, NavMesh.AllAreas)) a.Ag.Warp(nh.position);
                }
            }
            Animar(a, a.Ag.velocity.magnitude, a.Golpeando);
        }

        Transform BuscarPresa(Aliado a, Vector3 dueno)
        {
            Transform mejor = null; float dm = RadioCaza;
            foreach (var td in Object.FindObjectsOfType<TakeDamage>())
            {
                if (td == null || td.GetComponentInParent<FPSPlayer>() != null || Protegido(td.transform)) continue;
                Transform u = Combate.RaizDe(td);
                if (u == null || u.name.StartsWith("coop_") || u.root.name.StartsWith("coop_aliado") || !u.gameObject.activeInHierarchy || u.GetComponentInChildren<CharacterDamagePlaymakerAI>() == null) continue;
                var f = PlayMakerFSM.FindFsmOnGameObject(td.gameObject, td.NameOfFsm);
                var h = f != null ? f.FsmVariables.FindFsmFloat("Health") : null;
                if (h != null && h.Value <= 0f) continue;   // ya muerto
                if (Vector3.Distance(u.position, dueno) > 40f) continue;
                float d = Vector3.Distance(u.position, a.Go.transform.position);
                if (d < dm) { dm = d; mejor = u; }
            }
            return mejor;
        }

        /// Como en el compañero: el estado que el Animator reproduce por defecto se queda, y se le cambia
        /// la animación (quieto, andar, correr, atacar) con un AnimatorOverrideController.
        void PrepararClips(Aliado a)
        {
            var ctrl = a.An.runtimeAnimatorController;
            if (ctrl == null) return;
            a.Clips = new AnimationClip[4];
            string[][] claves = { new[] { "idle", "stand", "breath" }, new[] { "walk" }, new[] { "run", "chase", "sprint" }, new[] { "attack", "hit", "bite", "punch" } };
            for (int k = 0; k < 4; k++)
                foreach (var cl in ctrl.animationClips)
                {
                    if (cl == null) continue;
                    string n = cl.name.ToLowerInvariant(); bool ok = false;
                    foreach (var c in claves[k]) if (n.Contains(c)) { ok = true; break; }
                    if (ok && !n.Contains("death") && !n.Contains("die")) { a.Clips[k] = cl; break; }
                }
            if (a.Clips[2] == null) a.Clips[2] = a.Clips[1];
            if (a.Clips[1] == null) a.Clips[1] = a.Clips[2];
        }

        void AnimarConClips(Aliado a, int k)
        {
            if (a.Clips == null || a.An == null || a.An.runtimeAnimatorController == null) return;
            var c = a.Clips[k] ?? a.Clips[0];
            if (c == null || c == a.ClipActual) return;
            if (a.Override == null)
            {
                a.Originales = a.An.runtimeAnimatorController.animationClips;
                a.Override = new AnimatorOverrideController(a.An.runtimeAnimatorController) { name = "coop_aliado" };
                a.An.runtimeAnimatorController = a.Override;
            }
            // Todas las animaciones del controlador pasan a ser la que toca: esté en el estado que esté, se ve esa.
            var limpia = Modelos.SinEventos(c);
            foreach (var o in a.Originales) if (o != null) a.Override[o.name] = limpia;
            a.ClipActual = c;
        }

        void Animar(Aliado a, float vel, bool golpe)
        {
            if (a.An != null && a.Clips != null && (a.Hashes == null || a.Hashes[1] == 0))
            {
                int kk = golpe ? 3 : vel > 3.5f ? 2 : vel > 0.3f ? 1 : 0;
                AnimarConClips(a, kk);
                a.AnimHash = kk + 1;   // para el compañero: 1..4 = quieto, andar, correr, atacar
                a.An.speed = kk == 1 || kk == 2 ? Mathf.Clamp(vel / (kk == 2 ? 5f : 2f), 0.7f, 1.8f) : 1f;
                return;
            }
            if (a.An == null || a.Hashes == null) return;
            int k = golpe ? 3 : vel > 3.5f ? 2 : vel > 0.3f ? 1 : 0;
            int h = a.Hashes[k]; if (h == 0) h = a.Hashes[k == 2 ? 1 : 0];
            if (h == 0) return;
            if (a.AnimHash != h) { a.An.CrossFade(h, 0.2f, 0); a.AnimHash = h; }
            a.An.speed = k == 1 || k == 2 ? Mathf.Clamp(vel / (k == 2 ? 5f : 2f), 0.7f, 1.8f) : 1f;
        }

        void Despedir(Aliado a)
        {
            if (a.Go != null) { Destello(a.Go.transform.position + Vector3.up, new Color(0.5f, 0.15f, 0.9f)); Object.Destroy(a.Go); }
            Enviar(a, false);
            plugin.Log("Bola: el aliado " + a.Tipo + " se desvaneció");
        }

        static void Destello(Vector3 p, Color c)
        {
            var go = new GameObject("coop_destello");
            go.transform.position = p;
            var l = go.AddComponent<Light>(); l.type = LightType.Point; l.color = c; l.range = 8f; l.intensity = 4f;
            Object.Destroy(go, 0.35f);
        }

        // ---------------- Red ----------------

        void Enviar(Aliado a, bool vivo)
        {
            if (!plugin.Conectado) return;
            int slot = a.Slot; string tipo = a.Tipo;
            Vector3 p = a.Go != null ? a.Go.transform.position - Anclaje.Origen() : Vector3.zero;
            float yaw = a.Go != null ? a.Go.transform.eulerAngles.y : 0f; int h = a.AnimHash;
            float fr = a.VidaMax > 0f ? a.Vida / a.VidaMax : 1f;
            plugin.EnviarAliado(Red.Armar(w => { w.Write((byte)slot); w.Write(vivo); w.Write(tipo); w.Write(p.x); w.Write(p.y); w.Write(p.z); w.Write(yaw); w.Write(h); w.Write(fr); }));
        }

        public void Recibir(BinaryReader r)
        {
            int slot = r.ReadByte(); bool vivo = r.ReadBoolean(); string tipo = r.ReadString();
            Vector3 p = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle()) + Anclaje.Origen();
            float yaw = r.ReadSingle(); int h = r.ReadInt32();
            float fr = r.BaseStream.Position < r.BaseStream.Length ? r.ReadSingle() : 1f;
            Aliado a;
            remotos.TryGetValue(slot, out a);
            if (!vivo) { if (a != null) { if (a.Go != null) { Destello(a.Go.transform.position + Vector3.up, new Color(0.5f, 0.15f, 0.9f)); Object.Destroy(a.Go); } remotos.Remove(slot); } return; }
            if (plugin.Escena != Mundo.EscenaBosque) return;
            if (a == null || a.Tipo != tipo || a.Go == null)
            {
                if (a != null && a.Go != null) Object.Destroy(a.Go);
                var pl = PlantillaDe(tipo);
                if (pl == null) return;
                a = Crear(pl, tipo, p, Quaternion.Euler(0f, yaw, 0f), false);
                a.Slot = slot; remotos[slot] = a;
                int sl = slot;
                if (a.Cuerpo != null) a.Cuerpo.Remoto = d => plugin.EnviarAliadoDano(Red.Armar(w => { w.Write((byte)sl); w.Write(d); }));
                plugin.Log("Bola: aliado de " + plugin.NombreCompanero + " (" + tipo + ")");
            }
            a.Pos = p; a.Yaw = yaw; a.Visto = Time.realtimeSinceStartup;
            a.VidaMax = 1f; a.Vida = fr;
            if (h >= 1 && h <= 4) { AnimarConClips(a, h - 1); a.AnimHash = h; }
            else if (h != 0 && h != a.AnimHash && a.An != null && a.An.HasState(0, h)) { a.An.CrossFade(h, 0.2f, 0); a.AnimHash = h; }
        }

        /// Un monstruo del juego del compañero golpeó a uno de mis aliados.
        public void RecibirDano(BinaryReader r)
        {
            int slot = r.ReadByte(); float d = r.ReadSingle();
            foreach (var a in mios) if (a.Slot == slot && a.Cuerpo != null) a.Cuerpo.ApplyDamage(d);
        }

        // ---------------- Los enemigos también pelean con los aliados ----------------

        // Cada monstruo tiene en sus máquinas de estados "Target" (a quién persigue y mira) y "HitObject"
        // (a quién golpea; normalmente el jugador). Si un aliado está más cerca que los jugadores, se
        // apuntan a él; cuando ya no, se devuelven. Solo en el juego que simula a ese monstruo.
        readonly Dictionary<HutongGames.PlayMaker.FsmGameObject, GameObject> originales = new Dictionary<HutongGames.PlayMaker.FsmGameObject, GameObject>();
        readonly Dictionary<Combate.Unidad, List<HutongGames.PlayMaker.FsmGameObject>> variablesDe = new Dictionary<Combate.Unidad, List<HutongGames.PlayMaker.FsmGameObject>>();
        float proximaEnemistad;
        readonly Dictionary<Combate.Unidad, float> ultimoGolpe = new Dictionary<Combate.Unidad, float>();
        public const float RadioEnemistad = 18f;

        void ActualizarEnemistad(FPSPlayer j)
        {
            if (Time.time < proximaEnemistad) return;
            proximaEnemistad = Time.time + 0.2f;
            var c = plugin.CombateActual;
            if (c == null) return;
            var objetivos = new List<GameObject>();
            foreach (var a in mios) if (a.Go != null && a.Vida > 0f) objetivos.Add(a.Go);
            foreach (var a in remotos.Values) if (a.Go != null) objetivos.Add(a.Go);
            var comp = plugin.CompaneroActual;
            foreach (var u in c.Unidades)
            {
                if (!u.Viva || u.Remota || u.Raiz == null || Protegido(u.Raiz)) continue;
                Vector3 m = (u.Mover != null ? u.Mover : u.Raiz).position;
                float dj = Vector3.Distance(m, j.transform.position);
                if (comp != null && comp.Visible) dj = Mathf.Min(dj, Vector3.Distance(m, comp.PosicionMundo));
                GameObject mejor = null; float dm = RadioEnemistad;
                foreach (var o in objetivos) { float d = Vector3.Distance(m, o.transform.position); if (d < dm) { dm = d; mejor = o; } }
                if (mejor != null && dm >= dj - 1f) mejor = null;   // un jugador está igual de cerca: va por el jugador
                List<HutongGames.PlayMaker.FsmGameObject> vs;
                if (!variablesDe.TryGetValue(u, out vs))
                {
                    vs = new List<HutongGames.PlayMaker.FsmGameObject>();
                    Transform raiz = u.Raiz.parent != null ? u.Raiz.parent : u.Raiz;
                    foreach (var f in raiz.GetComponentsInChildren<PlayMakerFSM>(true))
                        foreach (var v in f.FsmVariables.GameObjectVariables)
                            if (v.Name == "Target") vs.Add(v);
                    variablesDe[u] = vs;
                }
                // Su golpe: el mordisco del juego solo daña al jugador (lo mira con un rayo), así que a un
                // aliado se lo aplica el mod cuando está a distancia de golpe, con el daño de ese monstruo.
                if (mejor != null && u.Td != null)
                {
                    bool jefe = Dificultad.EsJefe(u.Td);
                    float alcance = jefe ? 4f : 2.4f, ult;
                    ultimoGolpe.TryGetValue(u, out ult);
                    if (dm <= alcance && Time.time - ult > 1.4f)
                    {
                        ultimoGolpe[u] = Time.time;
                        Transform rz = u.Raiz.parent != null ? u.Raiz.parent : u.Raiz;
                        float vb, db; Estadisticas(TipoDe(rz), out vb, out db);
                        if (jefe && Eventos.Luna) db *= 2f;
                        var cuerpo = mejor.GetComponent<VidaAliado>();
                        if (cuerpo != null) cuerpo.ApplyDamage(db);
                    }
                }
                foreach (var v in vs)
                {
                    if (mejor != null)
                    {
                        if (!originales.ContainsKey(v)) originales[v] = v.Value;
                        if (v.Value != mejor) v.Value = mejor;
                    }
                    else
                    {
                        GameObject orig;
                        if (originales.TryGetValue(v, out orig)) { v.Value = orig; originales.Remove(v); }
                    }
                }
            }
        }

        void MoverRemoto(Aliado a)
        {
            if (a.Go == null) return;
            if (Vector3.Distance(a.Go.transform.position, a.Pos) > 20f) a.Go.transform.position = a.Pos;
            float k = Mathf.Clamp01(Time.deltaTime * 10f);
            a.Go.transform.position = Vector3.Lerp(a.Go.transform.position, a.Pos, k);
            a.Go.transform.rotation = Quaternion.Slerp(a.Go.transform.rotation, Quaternion.Euler(0f, a.Yaw, 0f), k);
        }

        // ---------------- Tienda ----------------

        public string Comprar()
        {
            if (TieneBola) return "Ya tienes la bola de cristal.";
            int? oro = Oro.Leer();
            if (oro == null || oro.Value < Precio) return "No tienen oro suficiente (" + Precio + ").";
            Oro.Gastar(Precio);
            DialogueLua.SetVariable(VarBola, true);
            plugin.Log("Tienda: bola de cristal comprada por " + Precio + " oro");
            return "Compraste la bola de cristal oscura (tecla 5).";
        }

        public void Dibujar()
        {
            var cam = Camera.main;
            if (cam != null)
            {
                foreach (var a in mios) Barra(cam, a, AuraPropia);
                foreach (var a in remotos.Values) Barra(cam, a, AuraCompanero);
            }
            var pw = arma != null ? Rendimiento.Armas : null;
            bool enMano = pw != null && pw.currentWeapon >= 0 && pw.currentWeapon < pw.weaponOrder.Length && pw.weaponOrder[pw.currentWeapon] == arma;
            if (!enMano) return;
            if (estilo == null)
            {
                estilo = new GUIStyle(GUI.skin.label) { fontSize = Opciones.T(18), alignment = TextAnchor.MiddleRight, fontStyle = FontStyle.Bold };
                estilo.normal.textColor = new Color(0.8f, 0.6f, 1f);
            }
            GUI.Label(new Rect(Screen.width - 520, Screen.height - 90, 500, 28), "Almas: " + Almas().Count + " · Aliados: " + mios.Count + "/" + MaxAliados + " · Clic: invocar", estilo);
        }
        GUIStyle estilo;

        static void Barra(Camera cam, Aliado a, Color c)
        {
            if (a.Go == null || a.VidaMax <= 0f) return;
            Bounds b = new Bounds(a.Go.transform.position, Vector3.zero); bool hay = false;
            foreach (var r in a.Go.GetComponentsInChildren<Renderer>()) { if (r is ParticleSystemRenderer) continue; if (!hay) { b = r.bounds; hay = true; } else b.Encapsulate(r.bounds); }
            Vector3 p = EscalaRender.APantalla(cam, new Vector3(b.center.x, b.max.y + 0.3f, b.center.z));
            if (p.z <= 0f || p.z > 40f) return;
            float w = 60f, y = Screen.height - p.y;
            var prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.6f); GUI.DrawTexture(new Rect(p.x - w / 2f, y, w, 6f), Texture2D.whiteTexture);
            GUI.color = c; GUI.DrawTexture(new Rect(p.x - w / 2f, y, w * Mathf.Clamp01(a.Vida / a.VidaMax), 6f), Texture2D.whiteTexture);
            GUI.color = prev;
        }

        public string Describir()
        {
            var sb = new System.Text.StringBuilder("bola=" + TieneBola + " almas=" + string.Join(",", Almas().ToArray()) + " aliados=" + mios.Count);
            foreach (var a in mios) sb.Append(" | ").Append(a.Tipo).Append(" en ").Append(a.Go != null ? (a.Go.transform.position - Anclaje.Origen()).ToString("F0") : "—").Append(" vida=").Append(a.Vida.ToString("0")).Append("/").Append(a.VidaMax.ToString("0")).Append(" daño=").Append(a.Dano.ToString("0")).Append(" presa=").Append(a.Presa != null ? a.Presa.name : "—").Append(" anim=").Append(a.Clips != null ? string.Join(",", System.Array.ConvertAll(a.Clips, c => c != null ? c.name : "-")) : "—").Append(" actual=").Append(a.ClipActual != null ? a.ClipActual.name : "—");
            sb.Append(" remotos=").Append(remotos.Count).Append(" variables de enemigos apuntando a aliados=").Append(originales.Count);
            return sb.ToString();
        }

        public void OrdenInvocar() { if (plugin.Jugador != null) Invocar(plugin.Jugador); }
    }
}

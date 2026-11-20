// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using UnityEngine;

namespace WitchHuntCoop
{
    /// Enemigos elementales: algunos enemigos comunes salen de un color con un aura del mismo color.
    ///  - Azul (helado): sus golpes te ralentizan a la mitad durante 4 s.
    ///  - Verde (venenoso): sus golpes te envenenan: 2 de daño por segundo durante 6 s.
    /// Cuál es cuál se decide con el número del monstruo que comparten los dos juegos (catálogo del
    /// combate), así ambos ven el mismo enemigo del mismo color. 6 % azules y 6 % verdes; nunca jefes
    /// ni potenciados.
    public class Elemental : MonoBehaviour
    {
        public enum Tipo { Ninguno, Hielo, Veneno, Radiactivo }
        public const float ProbHielo = 0.05f, ProbVeneno = 0.05f, ProbRadiactivo = 0.05f;
        public const float Lentitud = 0.5f, DuracionHielo = 4f;
        // Veneno: te quita 2 de vida cada 10 s hasta que te cures (planta curativa o entrar al agua).
        public const float DanoVeneno = 2f, CadaVeneno = 10f;
        // Radiactivo: tras el primer golpe, 1 de vida por segundo durante 10 s (como mucho 10 puntos).
        public const float DanoRadiacion = 1f, DuracionRadiacion = 10f;

        public Tipo Clase;
        Light aura; float fase;

        // ---------------- Piel de los perros (pedido 2026-11-11) ----------------
        // El juego reparte al azar 9 pieles (zd_01…zd_09). Los perros envenenados llevan una piel verdosa; los normales y los
        // congelados, siempre la oscura (zd_03).
        static readonly System.Collections.Generic.HashSet<GameObject> pielHecha = new System.Collections.Generic.HashSet<GameObject>();
        static Texture2D pielOscura, pielClara;

        static Texture2D Piel(string nombre)
        {
            foreach (var t in Resources.FindObjectsOfTypeAll<Texture2D>()) if (t != null && t.name == nombre) return t;
            return null;
        }

        /// Perros: el elemento lo da el número del monstruo (igual en los dos juegos) y la piel sigue al elemento:
        /// veneno = zd_07 (cabeza negra, manchas amarillas), hielo = zd_02 (marrón oscuro con sangre), normal = zd_01 (la de siempre).
        /// Si esa textura no está cargada en este momento, veneno se tiñe de verde e hielo de azul.
        static Tipo TipoPerro(int id) { var t = TipoDeId(id); return t == Tipo.Radiactivo ? Tipo.Ninguno : t; }

        static void PielPerro(GameObject go, Tipo t)
        {
            if (go == null || !pielHecha.Add(go)) return;
            string nombre = t == Tipo.Veneno ? "zd_07" : t == Tipo.Hielo ? "zd_02" : "zd_01";
            var tex = Piel(nombre);
            foreach (var r in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                foreach (var m in r.materials)
                {
                    if (m == null || !m.name.StartsWith("zd_")) continue;
                    if (tex != null) { m.mainTexture = tex; if (m.HasProperty("_Color")) m.color = Color.white; }
                    else if (m.HasProperty("_Color")) m.color = t == Tipo.Veneno ? new Color(0.6f, 1f, 0.5f, 1f) : t == Tipo.Hielo ? new Color(0.6f, 0.8f, 1f, 1f) : Color.white;
                }
        }

        public static Tipo TipoDeId(int id)
        {
            unchecked
            {
                uint h = (uint)id * 2654435761u; h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
                float r = (h % 10000) / 10000f;
                return r < ProbHielo ? Tipo.Hielo : r < ProbHielo + ProbVeneno ? Tipo.Veneno : r < ProbHielo + ProbVeneno + ProbRadiactivo ? Tipo.Radiactivo : Tipo.Ninguno;
            }
        }

        public static Color ColorDe(Tipo t) { return t == Tipo.Hielo ? new Color(0.25f, 0.6f, 1f) : t == Tipo.Veneno ? new Color(0.55f, 0.85f, 0.15f) : new Color(0.25f, 1f, 0.3f); }

        public static void Aplicar(GameObject unidad, Tipo t)
        {
            if (unidad == null || t == Tipo.Ninguno || unidad.GetComponent<Elemental>() != null) return;
            var e = unidad.AddComponent<Elemental>();
            e.Clase = t;
            e.Montar();
        }

        /// Aura de color alrededor del cuerpo (bruma que sube + luz), sin esferas ni sangre: verde para el veneno, azul para el hielo,
        /// verde brillante para el radiactivo. El resto del enemigo queda como es.
        void Montar()
        {
            Color c = ColorDe(Clase);
            Bounds b = new Bounds(transform.position + Vector3.up, Vector3.one); bool hay = false;
            foreach (var r in GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer) continue;
                Vector3 d = r.bounds.center - transform.position; d.y = 0f;
                if (d.sqrMagnitude > 36f) continue;   // mallas sueltas en el origen del mundo no cuentan
                if (!hay) { b = r.bounds; hay = true; } else b.Encapsulate(r.bounds);
            }
            float tam = Mathf.Clamp(b.size.magnitude, 1f, 8f);
            Color cb = Clase == Tipo.Hielo ? new Color(0.2f, 0.55f, 1f) : Clase == Tipo.Veneno ? new Color(0.25f, 0.9f, 0.1f) : new Color(0.3f, 1f, 0.45f);
            Potenciado.CrearAura(transform, b, tam, cb, false);
            var g = new GameObject("coop_aura_elemental");
            g.transform.SetParent(transform, false);
            g.transform.position = b.center;
            aura = g.AddComponent<Light>();
            aura.type = LightType.Point; aura.color = cb; aura.range = tam * 1.6f; aura.intensity = Clase == Tipo.Radiactivo ? 1.6f : 1.2f; aura.shadows = LightShadows.None;
        }

        float proxMuerte; TakeDamage tdPropio; bool muerto;

        void Update()
        {
            if (!muerto && Time.time >= proxMuerte)
            {
                proxMuerte = Time.time + 0.4f;
                if (tdPropio == null) tdPropio = GetComponentInChildren<TakeDamage>(true);
                if (tdPropio != null && Eventos.Muerto(tdPropio))
                {
                    muerto = true;   // el cadáver ya no brilla
                    foreach (var t in GetComponentsInChildren<Transform>(true))
                        if (t != null && (t.name == "coop_humo" || t.name == "coop_aura_elemental")) { var ps = t.GetComponent<ParticleSystem>(); if (ps != null) { ps.Stop(); Destroy(t.gameObject, 2.5f); } else Destroy(t.gameObject); }
                    aura = null;
                }
            }
            fase += Time.deltaTime * 2.5f;
            if (aura != null) aura.intensity = 1.0f + Mathf.Sin(fase) * 0.4f;
        }

        /// Los azules (congelan) y los envenenadores tienen 1,25 veces más vida y daño; los radiactivos no.
        public const float FactorFuerte = 1.25f;
        public static bool Fuerte(Tipo t) { return t == Tipo.Hielo || t == Tipo.Veneno; }

        /// Multiplicador de vida de este enemigo (se divide el daño que recibe): 1,25 si es azul o envenenador.
        public static float FactorVida(TakeDamage td)
        {
            if (td == null) return 1f;
            var e = td.GetComponentInParent<Elemental>();
            if (e == null)
            {
                Transform r = Combate.RaizDe(td);
                if (r != null) { r = r.parent != null ? r.parent : r; e = r.GetComponentInChildren<Elemental>(true); }
            }
            return e != null && Fuerte(e.Clase) ? FactorFuerte : 1f;
        }

        /// El elemental más cercano al jugador (los golpes llegan sin decir quién atacó).
        public static Tipo Cerca(Vector3 p, float radio)
        {
            Tipo mejor = Tipo.Ninguno; float dm = radio * radio;
            foreach (var x in FindObjectsOfType<Elemental>())
            {
                float d = (x.transform.position - p).sqrMagnitude;
                if (d < dm) { dm = d; mejor = x.Clase; }
            }
            return mejor;
        }

        // ---------------- Efectos en el jugador ----------------

        static float lentoHasta, proximoVeneno, radiacionHasta, proximaRadiacion;
        static bool envenenado; static float vidaPrev = -1f;
        static float walkBase = -1f, sprintBase = -1f;
        static FPSRigidBodyWalker andar;

        /// Lo llama el parche del daño al jugador cuando un enemigo lo golpea.
        public static void Golpe(Vector3 donde)
        {
            var t = Cerca(donde, 5f);
            if (t == Tipo.Hielo) { lentoHasta = Time.time + DuracionHielo; }
            else if (t == Tipo.Veneno && !envenenado) { envenenado = true; proximoVeneno = Time.time + CadaVeneno; Viaje.Avisar("Te han envenenado: tírate al agua o usa una planta para curarte."); }
            else if (t == Tipo.Radiactivo) { if (Time.time > radiacionHasta) proximaRadiacion = Time.time + 1f; radiacionHasta = Time.time + DuracionRadiacion; }
        }

        /// Cada cuadro: el número de cada monstruo decide si es elemental, y los efectos sobre el jugador.
        public static void Actualizar(Plugin plugin)
        {
            var j = plugin.Jugador;
            var c = plugin.CombateActual;
            if (c != null && plugin.Conectado && Time.frameCount % 30 == 0)
                foreach (var u in c.Unidades)
                {
                    if (!u.Viva || u.Raiz == null || u.Td == null) continue;
                    var go = u.Mover != null ? u.Mover.gameObject : u.Raiz.gameObject;
                    if (u.Nombre != null && u.Nombre.IndexOf("Dog", System.StringComparison.OrdinalIgnoreCase) >= 0) PielPerro(go, TipoPerro(u.Id));
                    if (go.GetComponent<Elemental>() != null) continue;
                    if (Dificultad.EsJefe(u.Td) || Potenciado.Es(u.Td) || Aliados.Protegido(u.Raiz)) continue;
                    var t = u.Nombre != null && u.Nombre.IndexOf("Dog", System.StringComparison.OrdinalIgnoreCase) >= 0 ? TipoPerro(u.Id) : TipoDeId(u.Id);
                    if (t != Tipo.Ninguno) Aplicar(go, t);
                }
            if (j == null) { walkBase = -1f; andar = null; envenenado = false; return; }
            if (andar == null) { andar = Object.FindObjectOfType<FPSRigidBodyWalker>(); walkBase = -1f; }
            if (andar != null)
            {
                bool lento = Time.time < lentoHasta;
                if (lento && walkBase < 0f) { walkBase = andar.walkSpeed; sprintBase = andar.sprintSpeed; andar.walkSpeed *= Lentitud; andar.sprintSpeed *= Lentitud; }
                else if (!lento && walkBase >= 0f) { andar.walkSpeed = walkBase; andar.sprintSpeed = sprintBase; walkBase = -1f; }
            }
            // Veneno: 2 de vida cada 10 s hasta curarse; el veneno no mata (te deja con poca vida)
            if (envenenado)
            {
                // cura: una planta curativa (la vida sube de golpe) o estar en el agua
                bool enAgua = j.transform.position.y < plugin.mundo.NivelAguaActual + 0.4f && plugin.mundo.NivelAguaActual > -500f;
                bool planta = vidaPrev >= 0f && j.hitPoints > vidaPrev + 6f;
                if (enAgua || planta) { envenenado = false; Viaje.Avisar("Ya no estás envenenado."); }
                else if (Time.time > proximoVeneno)
                {
                    proximoVeneno = Time.time + CadaVeneno;
                    if (!j.GodMode && j.hitPoints > DanoVeneno + 1f) j.ApplyDamage(DanoVeneno);
                }
            }
            // Radiactivo: 1 de vida por segundo durante 10 s tras el golpe (10 puntos como máximo)
            if (Time.time < radiacionHasta && Time.time > proximaRadiacion)
            {
                proximaRadiacion = Time.time + 1f;
                if (!j.GodMode && j.hitPoints > DanoRadiacion + 1f) j.ApplyDamage(DanoRadiacion);
            }
            vidaPrev = j.hitPoints;
        }

        public static void Dibujar()
        {
            bool lento = Time.time < lentoHasta, veneno = envenenado, rad = Time.time < radiacionHasta;
            if (!lento && !veneno && !rad) return;
            var prev = GUI.color;
            GUI.color = lento ? new Color(0.2f, 0.5f, 1f, 0.15f) : rad ? new Color(0.2f, 1f, 0.3f, 0.12f) : new Color(0.45f, 0.8f, 0.1f, 0.12f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = prev;
        }

        public static string Describir()
        {
            int h = 0, v = 0;
            int rd = 0;
            foreach (var x in FindObjectsOfType<Elemental>()) if (x.Clase == Tipo.Hielo) h++; else if (x.Clase == Tipo.Veneno) v++; else rd++;
            return "elementales: " + h + " azules, " + v + " venenosos, " + rd + " radiactivos; lento " + Mathf.Max(0f, lentoHasta - Time.time).ToString("0.0") + " s, envenenado=" + envenenado + ", radiación " + Mathf.Max(0f, radiacionHasta - Time.time).ToString("0.0") + " s";
        }
        public static void Probar(Tipo t) { if (t == Tipo.Hielo) lentoHasta = Time.time + DuracionHielo; else if (t == Tipo.Veneno) { envenenado = true; proximoVeneno = Time.time + 1f; } else { proximaRadiacion = Time.time + 1f; radiacionHasta = Time.time + DuracionRadiacion; } }
    }
}

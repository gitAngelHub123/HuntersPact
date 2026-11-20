// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace WitchHuntCoop
{
    /// Con dos jugadores los monstruos mueren el doble de rápido. En vez de tocar su vida
    /// (los jefes la guardan en variables globales que se conservan entre encuentros y la
    /// barra de jefe depende de ella), se reduce el daño que reciben: daño / multiplicador
    /// equivale exactamente a vida × multiplicador, y la barra sigue siendo correcta.
    /// Se aplica en ambos juegos por igual: el daño que viaja por la red es el original.
    [HarmonyPatch(typeof(TakeDamage), "Damage")]
    static class Dificultad
    {
        public static bool Activa;                 // solo con el compañero conectado
        public static float VidaJefes = 2f;
        public static float VidaNormales = 1f;

        const string EventoJefe = "Show_Boss_Healthbar";
        static readonly Dictionary<int, bool> esJefe = new Dictionary<int, bool>();

        static void Prefix(TakeDamage __instance, ref float damage)
        {
            if (!Activa || __instance == null || damage <= 0f) return;
            if (Aliados.Protegido(__instance.transform)) return;   // la cabra negra y el perro: sin cambios
            try
            {
                bool jefe = EsJefe(__instance);
                float m = jefe ? VidaJefes : VidaNormales;
                if (jefe && Eventos.Luna) m *= 2f;   // luna de sangre: jefes con el doble de vida
                if (Potenciado.Es(__instance)) m *= Potenciado.FactorVida;
                if (Mundo.JefeMinaActual != null && __instance == Mundo.JefeMinaActual) m *= 12f;   // (2026-11-19: ×6 → ×12)   // el jefe de la mina: mucha vida (la llave de la bóveda)
                m *= Elemental.FactorVida(__instance);   // azules y envenenadores: +25 % de vida
                if (m > 0f && m != 1f) damage /= m;
            }
            catch (Exception e) { Debug.LogError("[WitchHuntCoop] dificultad: " + e); }
        }

        /// Un jefe es lo que muestra la barra de vida de jefe en alguna de sus FSM.
        public static bool EsJefe(TakeDamage td)
        {
            int id = td.GetInstanceID();
            bool r;
            if (esJefe.TryGetValue(id, out r)) return r;
            r = false;
            // Subir hasta el objeto con NavMeshAgent (la unidad) o la raíz, y buscar en sus hijos.
            Transform u = td.transform;
            while (u.parent != null && u.GetComponent<UnityEngine.AI.NavMeshAgent>() == null) u = u.parent;
            Transform raiz = u.parent != null ? u.parent : u;
            foreach (var f in raiz.GetComponentsInChildren<PlayMakerFSM>(true))
            {
                if (f == null || f.Fsm == null || f.FsmStates == null) continue;
                foreach (var st in f.FsmStates)
                {
                    if (st.Actions == null) continue;
                    foreach (var a in st.Actions)
                    {
                        if (a == null || a.GetType().Name != "SendEventByName") continue;
                        var campo = a.GetType().GetField("sendEvent");
                        var v = campo != null ? campo.GetValue(a) as HutongGames.PlayMaker.FsmString : null;
                        if (v != null && v.Value == EventoJefe) { r = true; break; }
                    }
                    if (r) break;
                }
                if (r) break;
            }
            esJefe[id] = r;
            if (r) Debug.Log("[WitchHuntCoop] jefe detectado: " + raiz.name + " (vida ×" + VidaJefes + " en coop)");
            return r;
        }

        public static void Olvidar() { esJefe.Clear(); }

        // ---------------- Enemigos más rápidos ----------------
        public static float Velocidad = 1.35f;

        /// La FSM del monstruo cambia la velocidad del agente y del Animator según lo que hace
        /// (caminar, correr, atacar). Se recuerda lo último que pusimos: si cambió, lo cambió
        /// el juego, y ese es el nuevo valor base a multiplicar.
        public static void Acelerar(Combate.Unidad u)
        {
            if (!Activa || Velocidad == 1f) return;
            if (u.Raiz != null && Aliados.Protegido(u.Raiz)) return;   // la cabra negra y el perro van a su velocidad
            // Luna de sangre: los jefes van al doble (se aplica cuando el juego vuelve a poner su velocidad).
            float f = Velocidad * (Eventos.Luna && u.Td != null && EsJefe(u.Td) ? 2f : 1f);
            if (u.Agente != null && u.Agente.isActiveAndEnabled)
            {
                if (Mathf.Abs(u.Agente.speed - u.VelPuesta) > 0.001f)
                {
                    u.VelPuesta = u.Agente.speed * f;
                    u.Agente.speed = u.VelPuesta;
                    u.Agente.acceleration = Mathf.Max(u.Agente.acceleration, u.VelPuesta * 4f);
                }
            }
            if (u.Anim != null && u.Anim.isActiveAndEnabled)
            {
                if (Mathf.Abs(u.Anim.speed - u.AnimPuesta) > 0.001f)
                {
                    u.AnimPuesta = u.Anim.speed * f;
                    u.Anim.speed = u.AnimPuesta;
                }
            }
        }
    }

    /// Daño de los enemigos al jugador: poco por golpe (2–3) pero más seguido.
    /// Solo cambia golpes de enemigos (PlayMaker/NPCAttack), no caídas, ahogo ni muerte instantánea.
    [HarmonyPatch(typeof(FPSPlayer), "ApplyDamage")]
    static class DanoAlJugador
    {
        public static float Min = 2f, Max = 3f;
        public static float JefeMin = 10f, JefeMax = 15f;

        /// El daño llega por un SendMessage de PlayMaker sin decir quién atacó: se considera golpe
        /// de jefe si hay un jefe vivo a menos de 6 m del jugador (los mordiscos llegan a ~2,6 m).
        static bool JefeCerca()
        {
            var j = UnityEngine.Object.FindObjectOfType<FPSPlayer>();
            if (j == null) return false;
            foreach (var td in UnityEngine.Object.FindObjectsOfType<TakeDamage>())
                if ((td.transform.position - j.transform.position).sqrMagnitude < 36f && Dificultad.EsJefe(td)) return true;
            return false;
        }

        static void Prefix(ref float damage)
        {
            if (!Dificultad.Activa || damage <= 0f || Max <= 0f) return;
            if (damage > 200f) return; // muerte instantánea (aplastado, caída al vacío)
            try
            {
                bool enemigo = false;
                foreach (var f in new System.Diagnostics.StackTrace(1, false).GetFrames())
                {
                    var m = f.GetMethod();
                    if (m == null || m.DeclaringType == null) continue;
                    string t = m.DeclaringType.FullName;
                    if (t.StartsWith("HutongGames.PlayMaker") || t == "NPCAttack") { enemigo = true; break; }
                    if (t == "FPSRigidBodyWalker" || t == "DamageZone" || t == "ExplosionGua") return;
                }
                if (enemigo)
                {
                    float antes = damage;
                    bool jefe = JefeCerca();
                    damage = jefe ? Mathf.Round(UnityEngine.Random.Range(JefeMin, JefeMax))
                                  : Mathf.Round(UnityEngine.Random.Range(Min, Max));
                    if (!jefe)
                    {
                        var jj = UnityEngine.Object.FindObjectOfType<FPSPlayer>();
                        if (jj != null && Potenciado.Cerca(jj.transform.position, 5f)) damage *= Potenciado.FactorDano;
                    }
                    var jx = UnityEngine.Object.FindObjectOfType<FPSPlayer>();
                    if (jx != null)
                    {
                        if (Elemental.Fuerte(Elemental.Cerca(jx.transform.position, 5f))) damage = Mathf.Round(damage * Elemental.FactorFuerte);   // +25 % de daño
                        Elemental.Golpe(jx.transform.position);   // azul ralentiza, verde envenena
                    }
                    if (jefe && Eventos.Luna) damage *= 2f;   // luna de sangre: jefes con el doble de daño
                    if (jefe) antes = -antes;   // negativo en el registro = golpe de jefe
                    Debug.Log("[WitchHuntCoop] golpe enemigo " + antes.ToString("0.#") + " -> " + damage);
                }
            }
            catch (Exception e) { Debug.LogError("[WitchHuntCoop] daño al jugador: " + e); }
        }
    }
}

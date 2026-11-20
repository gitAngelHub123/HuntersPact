// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using System.IO;
using UnityEngine;

namespace WitchHuntCoop
{
    /// (2026-11-20) Sonidos del compañero: antes cada juego solo sonaba lo de SU jugador; del compañero se veía el muñeco pero no se oía nada.
    /// - Disparos, inicio de recarga y cambio de arma: cada juego los detecta en su propio jugador y manda (tipo, número de arma); el otro
    ///   toma el clip de ESA arma de su propio inventario (mismo orden en los dos juegos) y lo toca en 3D en la posición del compañero.
    /// - Pasos: no van por la red; cada juego los genera según la velocidad del muñeco del compañero, con las pisadas del juego (Footsteps).
    public static class SonidosCompanero
    {
        const byte Disparo = 0, Recarga = 1, CambioArma = 2;
        static WeaponBehavior armaVista; static int indiceVisto = -1, balasVistas = -1; static float recargaVista = -1f, golpeVisto = -1f;
        /// Armas cuerpo a cuerpo (estoque, sin arma): no gastan munición, el golpe se ve en shootStartTime (2026-11-20).
        static bool CuerpoACuerpo(WeaponBehavior wb) { return wb != null && wb.range < 5f; }

        /// (2026-11-20) Hora en que empezó la última recarga del arma. El juego declara WeaponBehavior.reloadState pero NUNCA lo cambia
        /// (por eso la recarga del compañero no sonaba nunca); Reload() sí guarda la hora en el campo privado reloadStartTime.
        static readonly System.Reflection.FieldInfo campoInicioRecarga = HarmonyLib.AccessTools.Field(typeof(WeaponBehavior), "reloadStartTime");
        public static float InicioRecarga(WeaponBehavior wb)
        {
            try { return wb != null && campoInicioRecarga != null ? (float)campoInicioRecarga.GetValue(wb) : -1f; } catch { return -1f; }
        }
        /// ¿Está recargando ahora? (para bajar el arma del muñeco del compañero)
        public static bool Recargando(WeaponBehavior wb)
        {
            float t = InicioRecarga(wb);
            return wb != null && t > 0f && Time.time - t < Mathf.Max(0.5f, wb.reloadTime);
        }
        static AudioSource voz, pies; static float proxPaso; static int pasoN;
        // contadores para comprobar que de verdad suena (orden "sonidoscomp")
        static int enviados, recibidos, sonaron, silenciados, sinClip, pasosN;
        static string ultimo = "-";
        public static string Informe()
        {
            return "enviados " + enviados + ", recibidos " + recibidos + " (sonaron " + sonaron + ", silenciados por Unity " + silenciados + ", sin clip " + sinClip + "), pasos del compañero " + pasosN + ", último: " + ultimo;
        }

        /// Otro módulo cambió la munición del arma a propósito (no es un disparo).
        public static void Ajustar(WeaponBehavior wb, int balas) { if (armaVista == wb) balasVistas = balas; }

        public static void Actualizar(Plugin plugin)
        {
            DetectarMios(plugin);
            Pasos(plugin);
        }

        // ---------------- lo mío → al compañero ----------------

        static void DetectarMios(Plugin plugin)
        {
            var pw = Rendimiento.Armas;
            if (pw == null || pw.weaponOrder == null || pw.currentWeapon < 0 || pw.currentWeapon >= pw.weaponOrder.Length || pw.weaponOrder[pw.currentWeapon] == null) { armaVista = null; return; }
            int i = pw.currentWeapon;
            var wb = pw.weaponOrder[i].GetComponent<WeaponBehavior>();
            if (wb == null) { armaVista = null; return; }
            if (i != indiceVisto)
            {
                if (indiceVisto >= 0) Mandar(plugin, CambioArma, i);
                indiceVisto = i; armaVista = wb; balasVistas = wb.bulletsLeft; recargaVista = InicioRecarga(wb); golpeVisto = wb.shootStartTime;
                return;
            }
            if (wb != armaVista) { armaVista = wb; balasVistas = wb.bulletsLeft; recargaVista = InicioRecarga(wb); golpeVisto = wb.shootStartTime; return; }
            if (CuerpoACuerpo(wb) && wb.shootStartTime != golpeVisto) { golpeVisto = wb.shootStartTime; if (wb.shootStartTime > 0f) Mandar(plugin, Disparo, i); }
            if (wb.bulletsLeft < balasVistas && wb.bulletsPerClip > 0) Mandar(plugin, Disparo, i);
            float inicio = InicioRecarga(wb);
            if (inicio > 0f && inicio != recargaVista) Mandar(plugin, Recarga, i);   // empezó una recarga nueva
            balasVistas = wb.bulletsLeft; recargaVista = inicio;
        }

        static void Mandar(Plugin plugin, byte tipo, int arma)
        {
            if (!plugin.Conectado) return;
            plugin.EnviarSonidoCompanero(Red.Armar(w => { w.Write(tipo); w.Write((byte)arma); })); enviados++;
        }

        // ---------------- lo del compañero → suena aquí ----------------

        public static void Recibir(Plugin plugin, BinaryReader r)
        {
            byte tipo = r.ReadByte(); int arma = r.ReadByte(); recibidos++;
            var c = plugin.CompaneroObj;
            if (c == null || !c.Visible || c.Cuerpo == null) return;
            var pw = Rendimiento.Armas;
            if (pw == null || pw.weaponOrder == null || arma >= pw.weaponOrder.Length || pw.weaponOrder[arma] == null) return;
            var wb = pw.weaponOrder[arma].GetComponent<WeaponBehavior>();
            AudioClip clip = null; float vol = 1f, min = 2f, max = 35f;
            if (tipo == Disparo)
            {
                clip = wb != null ? wb.fireSnd : null;
                if (pw.weaponOrder[arma].name == Escopeta.Nombre) clip = Sonidos.Clip("esc_disparo") ?? clip;
                if (CuerpoACuerpo(wb)) { vol = 0.8f; min = 2f; max = 30f; }   // golpe del estoque: se oye de cerca
                else { vol = 1f; min = 10f; max = 260f; }   // un disparo se oye lejos
            }
            else if (tipo == Recarga)
            {
                var pasos = SecuenciaRecarga.De(pw.weaponOrder[arma].name);   // escopeta y ballesta: su recarga propia (el reloadLastSnd del juego es "silence")
                if (pasos != null) { plugin.StartCoroutine(Secuencia(plugin, c.Cuerpo.transform, pasos, "recarga " + pw.weaponOrder[arma].name)); return; }
                clip = wb != null ? (wb.reloadSnd ?? wb.reloadLastSnd) : null;
                if (clip == null && pw.weaponOrder[arma].name == Escopeta.Nombre) clip = Sonidos.Clip("esc_meter1");
                vol = 0.8f; min = 2f; max = 30f;
            }
            else if (tipo == CambioArma)
            {
                clip = wb != null && wb.readySnd != null ? wb.readySnd : pw.changesnd;
                vol = 0.7f; min = 2f; max = 25f;
            }
            if (clip == null) { sinClip++; return; }
            // una fuente nueva por sonido (no PlayOneShot): así se puede comprobar si Unity la tocó o la silenció
            var g = new GameObject("coop_companero_sonido"); g.transform.SetParent(c.Cuerpo.transform, false); g.transform.localPosition = Vector3.up * 1.4f;
            var v = g.AddComponent<AudioSource>();
            v.playOnAwake = false; v.spatialBlend = 1f; v.rolloffMode = AudioRolloffMode.Logarithmic; v.dopplerLevel = 0f; v.priority = 64;
            v.minDistance = min; v.maxDistance = max; v.clip = clip; v.volume = vol * Opciones.FactorVolumen; v.Play();
            Object.Destroy(g, clip.length + 0.3f);
            plugin.StartCoroutine(Comprobar(v, (tipo == Disparo ? "disparo " : tipo == Recarga ? "recarga " : "cambio ") + clip.name));
        }

        /// Recarga por pasos en 3D sobre el compañero (escopeta, ballesta). Comprueba el primer paso como los demás sonidos.
        static System.Collections.IEnumerator Secuencia(Plugin plugin, Transform cuerpo, SecuenciaRecarga.Paso[] pasos, string que)
        {
            float t0 = Time.time;
            for (int i = 0; i < pasos.Length; i++)
            {
                while (Time.time - t0 < pasos[i].T) { if (cuerpo == null) yield break; yield return null; }
                if (cuerpo == null) yield break;
                var clip = pasos[i].Clip(); if (clip == null) { if (i == 0) sinClip++; continue; }
                var g = new GameObject("coop_companero_sonido"); g.transform.SetParent(cuerpo, false); g.transform.localPosition = Vector3.up * 1.4f;
                var v = g.AddComponent<AudioSource>();
                v.playOnAwake = false; v.spatialBlend = 1f; v.rolloffMode = AudioRolloffMode.Logarithmic; v.dopplerLevel = 0f; v.priority = 64;
                v.minDistance = 2f; v.maxDistance = 30f; v.clip = clip; v.volume = 0.9f * pasos[i].Vol * Opciones.FactorVolumen; v.Play();
                Object.Destroy(g, clip.length + 0.3f);
                if (i == 0) plugin.StartCoroutine(Comprobar(v, que + " (" + pasos.Length + " pasos, " + clip.name + ")"));
            }
        }

        static System.Collections.IEnumerator Comprobar(AudioSource v, string que)
        {
            yield return null; yield return null; yield return null;
            if (v == null) yield break;
            if (v.isPlaying && !v.isVirtual) sonaron++; else silenciados++;
            ultimo = que + (v.isPlaying && !v.isVirtual ? " (sonó)" : " (silenciado)") + " a " + (Camera.main != null ? Vector3.Distance(Camera.main.transform.position, v.transform.position).ToString("0") : "?") + " m";
        }

        static AudioSource Voz(Transform cuerpo)
        {
            if (voz != null && voz.transform.parent == cuerpo) return voz;
            voz = Fuente(cuerpo, "coop_companero_voz", 1.4f);
            return voz;
        }

        static AudioSource Fuente(Transform cuerpo, string nombre, float alto)
        {
            var t = cuerpo.Find(nombre);
            var g = t != null ? t.gameObject : new GameObject(nombre);
            g.transform.SetParent(cuerpo, false); g.transform.localPosition = Vector3.up * alto;
            var a = g.GetComponent<AudioSource>() ?? g.AddComponent<AudioSource>();
            a.playOnAwake = false; a.loop = false; a.spatialBlend = 1f; a.rolloffMode = AudioRolloffMode.Logarithmic; a.dopplerLevel = 0f; a.priority = 64;
            return a;
        }

        /// Pisadas del compañero según la velocidad de su muñeco (caminar ~0,5 s entre pasos, correr ~0,3 s).
        static void Pasos(Plugin plugin)
        {
            var c = plugin.CompaneroObj;
            if (c == null || !c.Visible || c.Cuerpo == null || !plugin.Conectado) return;
            float vel = c.Velocidad;
            if (vel < 0.6f) { proxPaso = Time.time + 0.15f; return; }
            if (Time.time < proxPaso) return;
            proxPaso = Time.time + Mathf.Clamp(0.95f / Mathf.Max(vel, 0.1f), 0.28f, 0.6f);
            var f = Object.FindObjectOfType<Footsteps>();
            if (f == null) return;
            var lista = f.dirtSteps != null && f.dirtSteps.Length > 0 ? f.dirtSteps : f.stoneSteps;
            if (lista == null || lista.Length == 0) return;
            if (pies == null || pies.transform.parent != c.Cuerpo.transform) { pies = Fuente(c.Cuerpo.transform, "coop_companero_pies", 0.1f); pies.minDistance = 1.5f; pies.maxDistance = 22f; }
            var clip = lista[(pasoN++ + Random.Range(0, lista.Length)) % lista.Length];
            pies.pitch = Random.Range(0.92f, 1.06f);
            pies.PlayOneShot(clip, (vel > 4f ? 0.9f : 0.65f) * f.dirtStepVol * Opciones.FactorVolumen); pasosN++;
        }
    }
}

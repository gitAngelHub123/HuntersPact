// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using System.Collections.Generic;
using UnityEngine;

namespace WitchHuntCoop
{
    /// La mano izquierda bajo la bola de cristal (pedido 2026-11-17, documentacion/estudio_mano_izquierda.png):
    /// copia de los brazos del mosquete del juego (los mismos de la escopeta), sin el arma ni el brazo derecho,
    /// con la palma hacia arriba y un sube y baja suave en bucle. La bola se apoya sobre la palma.
    /// También el radar: un punto celeste en la cara de la bola que señala al enemigo más cercano (a menos de
    /// 100 m) según hacia dónde miras: arriba = delante, abajo = detrás, a los lados = izquierda/derecha;
    /// cerca del centro si está cerca, hacia el borde si está lejos.
    public static class ManoBola
    {
        public static Plugin plugin;
        static GameObject mano;          // copia de los brazos
        static Transform manoIzq, medio;
        static readonly Dictionary<string, Quaternion> baseHuesos = new Dictionary<string, Quaternion>();
        static Vector3 baseRaizPos; static Quaternion baseRaizRot;
        static GameObject armaVista;

        // Pose (ajustable en vivo con la orden "manobola"; los valores buenos quedan aquí escritos).
        public static Vector3 RaizPos = Vector3.zero, RaizRot = Vector3.zero;
        /// Toda la mano con la bola: ~20 px a la izquierda y un poco más adelante (pedido 2026-11-17), en ejes de la cámara.
        public static Vector3 CorrimientoCam = new Vector3(-0.017f, 0.006f, 0.05f);
        public static readonly Dictionary<string, Vector3> Pose = PoseAbierta();
        public static Vector3 BolaOff = new Vector3(0f, 0.072f, 0f);
        /// Palma abierta (sobre el primer cuadro del clip Fire del mosquete): cada dedo se abre en su eje Z local
        /// (base −20°, 2.ª falange −35°, punta −25°); ajustado con capturas 2026-11-17.
        static Dictionary<string, Vector3> PoseAbierta()
        {
            var d = new Dictionary<string, Vector3>();
            d["L Hand"] = new Vector3(15f, 0f, 35f);   // mano recta en línea con el antebrazo, palma arriba (pedido 2026-11-18, elegido con capturas en vivo)
            for (int f = 1; f <= 4; f++) { d["L Finger " + f + " 0"] = new Vector3(0f, 0f, -20f); d["L Finger " + f + " 1"] = new Vector3(0f, 0f, -35f); d["L Finger " + f + " 2"] = new Vector3(0f, 0f, -25f); }
            return d;
        }   // de la palma a la bola (derecha, arriba, adelante de la cámara)
        public const float Vaiven = 0.008f, VaivenVel = 1.8f;

        static GameObject punto; static Material matPunto;
        static Vector3 objetivo; static bool hayObjetivo; static float proxRadar;

        public static int Capa { get { return mano != null ? mano.layer : -1; } }

        /// Cada cuadro desde Aliados.VerBola. Devuelve el punto donde va la bola (o null si no hay mano).
        public static Vector3? Actualizar(GameObject arma, bool enMano, Camera cam)
        {
            if (arma == null || cam == null) { Quitar(); return null; }
            if (armaVista != arma) { Quitar(); armaVista = arma; }
            if (mano == null) Crear(arma);
            if (mano == null) return null;
            if (mano.activeSelf != enMano) mano.SetActive(enMano);
            if (!enMano) { if (punto != null) punto.SetActive(false); return null; }
            // pose
            mano.transform.localPosition = baseRaizPos + RaizPos + Vector3.up * Vaiven * Mathf.Sin(Time.time * VaivenVel);
            if (mano.transform.parent != null)
                mano.transform.localPosition += mano.transform.parent.InverseTransformVector(cam.transform.right * CorrimientoCam.x + cam.transform.up * CorrimientoCam.y + cam.transform.forward * CorrimientoCam.z);
            mano.transform.localRotation = baseRaizRot * Quaternion.Euler(RaizRot);
            foreach (var t in mano.GetComponentsInChildren<Transform>(true))
            {
                Quaternion b; if (!baseHuesos.TryGetValue(t.name, out b)) continue;
                Vector3 e; t.localRotation = Pose.TryGetValue(t.name, out e) ? b * Quaternion.Euler(e) : b;
            }
            if (manoIzq == null) return null;
            Vector3 palma = medio != null ? Vector3.Lerp(manoIzq.position, medio.position, 0.5f) : manoIzq.position;
            return palma + cam.transform.right * BolaOff.x + cam.transform.up * BolaOff.y + cam.transform.forward * BolaOff.z;
        }

        static void Crear(GameObject arma)
        {
            var pw = Rendimiento.Armas; if (pw == null || pw.weaponOrder == null) return;
            WeaponBehavior mosquete = null;
            foreach (var w in pw.weaponOrder) if (w != null && w.name.StartsWith("Musket")) { mosquete = w.GetComponent<WeaponBehavior>(); break; }
            if (mosquete == null || mosquete.weaponMesh == null) return;
            var wm = mosquete.weaponMesh;
            mano = (GameObject)Object.Instantiate(wm, arma.transform, false);
            mano.name = "coop_mano_bola";
            // mismo sitio relativo que en el mosquete (las armas son hermanas bajo el mismo pivote)
            baseRaizPos = wm.transform.parent == mosquete.transform ? wm.transform.localPosition : arma.transform.InverseTransformPoint(wm.transform.position);
            baseRaizRot = wm.transform.parent == mosquete.transform ? wm.transform.localRotation : Quaternion.Inverse(arma.transform.rotation) * wm.transform.rotation;
            // fuera todo lo que no sea la malla de los brazos
            var an = mano.GetComponent<Animation>();
            AnimationClip quieto = null;
            if (an != null) { foreach (AnimationState s in an) { if (quieto == null || s.name.ToLower().Contains("idle")) quieto = s.clip; } }
            if (quieto != null) quieto.SampleAnimation(mano, 0f);
            if (an != null) an.enabled = false;
            foreach (var mb in mano.GetComponentsInChildren<MonoBehaviour>(true)) if (mb != null) Object.Destroy(mb);
            foreach (var ps in mano.GetComponentsInChildren<ParticleSystem>(true)) ps.gameObject.SetActive(false);
            foreach (var l in mano.GetComponentsInChildren<Light>(true)) l.enabled = false;
            foreach (var a in mano.GetComponentsInChildren<AudioSource>(true)) a.enabled = false;
            SkinnedMeshRenderer smr = null;
            foreach (var r in mano.GetComponentsInChildren<Renderer>(true))
            {
                var s = r as SkinnedMeshRenderer;
                if (s != null && s.name == "smdimport") smr = s; else r.enabled = false;
            }
            if (smr == null) { plugin.Log("ManoBola: sin la malla de los brazos"); Object.Destroy(mano); mano = null; return; }
            SoloManoIzquierda(smr);
            smr.updateWhenOffscreen = true;
            var bIzq = mano.transform.Find("Base/L Arm");
            manoIzq = bIzq != null ? bIzq.Find("L Hand") : null;
            if (manoIzq == null) foreach (var t in mano.GetComponentsInChildren<Transform>(true)) if (t.name == "L Hand") { manoIzq = t; break; }
            medio = null; if (manoIzq != null) foreach (var t in manoIzq.GetComponentsInChildren<Transform>(true)) if (t.name == "L Finger 2 1") { medio = t; break; }
            baseHuesos.Clear();
            foreach (var t in mano.GetComponentsInChildren<Transform>(true)) if (t != mano.transform && !baseHuesos.ContainsKey(t.name)) baseHuesos[t.name] = t.localRotation;
            plugin.Log("ManoBola: mano creada desde " + mosquete.name + " (clip " + (quieto != null ? quieto.name : "-") + ", capa " + mano.layer + ", mano " + (manoIzq != null) + ")");
        }

        /// Quita de la malla los triángulos del mosquete (hueso Rifle) y los del brazo derecho (R Arm).
        static void SoloManoIzquierda(SkinnedMeshRenderer smr)
        {
            var huesos = smr.bones; var fuera = new bool[huesos.Length];
            for (int i = 0; i < huesos.Length; i++)
                for (var x = huesos[i]; x != null; x = x.parent) if (x.name == "Rifle" || x.name == "R Arm") { fuera[i] = true; break; }
            var orig = smr.sharedMesh; var bw = orig.boneWeights;
            var quitar = new bool[bw.Length];
            for (int v = 0; v < bw.Length; v++) quitar[v] = fuera[bw[v].boneIndex0] && bw[v].weight0 >= 0.5f;
            var m = Object.Instantiate(orig); m.name = "coop_mano_bola";
            for (int sm = 0; sm < m.subMeshCount; sm++)
            {
                var tris = m.GetTriangles(sm); var quedan = new List<int>(tris.Length);
                for (int k = 0; k < tris.Length; k += 3)
                    if (!quitar[tris[k]] && !quitar[tris[k + 1]] && !quitar[tris[k + 2]]) { quedan.Add(tris[k]); quedan.Add(tris[k + 1]); quedan.Add(tris[k + 2]); }
                m.SetTriangles(quedan, sm);
            }
            smr.sharedMesh = m;
        }

        public static void Quitar()
        {
            if (mano != null) Object.Destroy(mano);
            mano = null; manoIzq = medio = null; armaVista = null;
            if (punto != null) punto.SetActive(false);
        }

        // ---------------- Radar ----------------

        public static void OcultarRadar() { if (punto != null && punto.activeSelf) punto.SetActive(false); }

        /// Pone el punto celeste en la cara de la bola que mira a la cámara.
        public static void Radar(GameObject bola, Camera cam, float radio)
        {
            if (bola == null || cam == null) return;
            if (punto == null)
            {
                punto = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Object.Destroy(punto.GetComponent<Collider>());
                punto.name = "coop_radar_bola";
                var sh = Shader.Find("Hidden/Internal-Colored");
                matPunto = new Material(sh) { hideFlags = HideFlags.HideAndDontSave, color = new Color(0.6f, 0.92f, 1f, 1f) };
                var r = punto.GetComponent<Renderer>(); r.sharedMaterial = matPunto; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            }
            punto.layer = bola.layer;
            if (Time.time > proxRadar) { proxRadar = Time.time + 0.25f; Buscar(cam.transform.position); }
            if (!hayObjetivo || !bola.activeInHierarchy) { if (punto.activeSelf) punto.SetActive(false); return; }
            if (!punto.activeSelf) punto.SetActive(true);
            Vector3 v = objetivo - cam.transform.position; v.y = 0f;
            float d = v.magnitude;
            // brújula fija (pedido 2026-11-17): arriba = norte (+z del mapa), abajo = sur, derecha = este (+x), izquierda = oeste
            Vector2 dir = new Vector2(v.x, v.z);
            dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.up;
            float k = Mathf.Lerp(0.12f, 0.8f, Mathf.Clamp01(d / 100f));
            float ox = dir.x * k * radio, oy = dir.y * k * radio;
            float oz = Mathf.Sqrt(Mathf.Max(0f, radio * radio - ox * ox - oy * oy));
            punto.transform.position = bola.transform.position + cam.transform.right * ox + cam.transform.up * oy - cam.transform.forward * oz * 1.02f;
            // más grande y brillante cuanto más cerca (late más rápido)
            // pequeño, como el punto de luz que ya tenía la bola (esfera.xcf): late más rápido cuanto más cerca
            float late = 0.8f + 0.2f * Mathf.Sin(Time.time * Mathf.Lerp(9f, 3f, d / 100f));
            punto.transform.localScale = Vector3.one * radio * 0.075f;
            matPunto.color = new Color(0.78f * late, 0.93f * late, 1f * late, 1f);
        }

        static void Buscar(Vector3 desde)
        {
            hayObjetivo = false; float mejor = 100f * 100f;
            foreach (var td in Object.FindObjectsOfType<TakeDamage>())
            {
                if (td == null || td.GetComponentInParent<FPSPlayer>() != null) continue;
                if (Aliados.Protegido(td.transform) || Eventos.Muerto(td)) continue;
                bool fuera = false; for (var x = td.transform; x != null; x = x.parent) if (x.name.StartsWith("coop_aliado_") || x.name.StartsWith("coop_plantilla_")) { fuera = true; break; }
                if (fuera) continue;
                var p = Combate.RaizDe(td).position;
                float d2 = (p - desde).sqrMagnitude;
                if (d2 < mejor) { mejor = d2; objetivo = p; hayObjetivo = true; }
            }
        }

        // ---------------- Orden de prueba ----------------

        /// manobola lista | info | reset | raiz px py pz rx ry rz | hueso <nombre> ex ey ez | bola dx dy dz
        public static string Orden(string arg)
        {
            var pa = arg.Split(' '); var ci = System.Globalization.CultureInfo.InvariantCulture;
            switch (pa[0])
            {
                case "lista":
                {
                    if (mano == null) return "sin mano (equipa la bola)";
                    var sb = new System.Text.StringBuilder();
                    var izq = mano.transform.Find("Base/L Arm");
                    foreach (var t in (izq != null ? izq : mano.transform).GetComponentsInChildren<Transform>(true)) sb.Append(t.name).Append(" | ");
                    return sb.ToString();
                }
                case "info":
                    return "mano=" + (mano != null) + " activa=" + (mano != null && mano.activeInHierarchy) + " raiz=" + RaizPos.ToString("F3") + "/" + RaizRot.ToString("F0") + " bola=" + BolaOff.ToString("F3") + " pose=" + Pose.Count
                        + (manoIzq != null ? " palma=" + manoIzq.position.ToString("F3") : "") + " objetivo=" + (hayObjetivo ? objetivo.ToString("F0") : "-");
                case "reset": Pose.Clear(); foreach (var kv in PoseAbierta()) Pose[kv.Key] = kv.Value; RaizPos = RaizRot = Vector3.zero; return "pose abierta";
                case "raiz":
                    RaizPos = new Vector3(float.Parse(pa[1], ci), float.Parse(pa[2], ci), float.Parse(pa[3], ci));
                    if (pa.Length >= 7) RaizRot = new Vector3(float.Parse(pa[4], ci), float.Parse(pa[5], ci), float.Parse(pa[6], ci));
                    return "raiz " + RaizPos.ToString("F3") + " " + RaizRot.ToString("F0");
                case "bola": BolaOff = new Vector3(float.Parse(pa[1], ci), float.Parse(pa[2], ci), float.Parse(pa[3], ci)); return "bola " + BolaOff.ToString("F3");
                case "correr": CorrimientoCam = new Vector3(float.Parse(pa[1], ci), float.Parse(pa[2], ci), float.Parse(pa[3], ci)); return "corrimiento " + CorrimientoCam.ToString("F3");
                case "hueso":
                {
                    int n = pa.Length; string nom = string.Join(" ", pa, 1, n - 4);
                    Pose[nom] = new Vector3(float.Parse(pa[n - 3], ci), float.Parse(pa[n - 2], ci), float.Parse(pa[n - 1], ci));
                    return nom + " = " + Pose[nom];
                }
                case "pose":   // pose: todo lo puesto, para copiarlo al código
                {
                    var sb = new System.Text.StringBuilder("RaizPos=" + RaizPos.ToString("F3") + " RaizRot=" + RaizRot.ToString("F1") + " BolaOff=" + BolaOff.ToString("F3"));
                    foreach (var kv in Pose) sb.Append(" | ").Append(kv.Key).Append("=").Append(kv.Value.ToString("F0"));
                    return sb.ToString();
                }
            }
            return "uso: manobola lista|info|reset|pose|raiz px py pz [rx ry rz]|hueso <nombre> ex ey ez|bola dx dy dz";
        }
    }
}

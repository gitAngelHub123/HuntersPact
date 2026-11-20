// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace WitchHuntCoop
{
    /// (2026-11-20) El arma que el compañero tiene en la mano, visible en su muñeco.
    /// - Cada juego manda el número del arma de SU jugador (al cambiar y cada segundo, por si se pierde un mensaje).
    /// - El otro toma ESA arma de su propio inventario (mismo orden en los dos juegos), separa la malla del arma de la de los
    ///   brazos de primera persona (como ArmaCopia con el mosquete) y la pone en la mano derecha del cazador, con la misma
    ///   orientación que tiene respecto de la cámara en primera persona, girada hacia donde mira el compañero.
    public static class ArmaCompanero
    {
        // ---------------- lo mío → al compañero ----------------
        static int indiceMio = -1; static float proxEnvio;
        static byte banderasMias, contadorMio; static float ultimoTiroVisto = -1f; static WeaponBehavior armaTiro;
        const byte Apuntando = 1, Recargando = 2, Corriendo = 4;

        /// (2026-11-20, segunda parte) Además del arma: si apunta, recarga o corre, y un contador que sube con cada disparo o golpe
        /// (shootStartTime del arma cambia también con el estoque, que no gasta munición).
        public static void Actualizar(Plugin plugin)
        {
            var pw = Rendimiento.Armas;
            int i = pw != null ? pw.currentWeapon : -1;
            WeaponBehavior wb = null;
            if (pw != null && pw.weaponOrder != null && i >= 0 && i < pw.weaponOrder.Length && pw.weaponOrder[i] != null) wb = pw.weaponOrder[i].GetComponent<WeaponBehavior>();
            byte banderas = 0;
            var j = plugin.Jugador;
            if (j != null && j.zoomed) banderas |= Apuntando;
            if (wb != null && SonidosCompanero.Recargando(wb)) banderas |= Recargando;   // (reloadState del juego nunca cambia)
            if (wb != null && wb.sprintState) banderas |= Corriendo;
            bool tiro = false;
            if (wb != null)
            {
                if (wb != armaTiro) { armaTiro = wb; ultimoTiroVisto = wb.shootStartTime; }
                else if (wb.shootStartTime != ultimoTiroVisto) { ultimoTiroVisto = wb.shootStartTime; if (wb.shootStartTime > 0f) { contadorMio++; tiro = true; } }
            }
            if (i == indiceMio && banderas == banderasMias && !tiro && Time.realtimeSinceStartup < proxEnvio) return;
            indiceMio = i; banderasMias = banderas; proxEnvio = Time.realtimeSinceStartup + 1f;
            byte b = (byte)(i < 0 || i > 254 ? 255 : i), f = banderas, n = contadorMio;
            plugin.EnviarArmaCompanero(Red.Armar(w => { w.Write(b); w.Write(f); w.Write(n); }));
        }

        // ---------------- lo del compañero → visible aquí ----------------
        static int indiceComp = 255;
        public static int Forzado = -1;   // orden 'armacomp ver <n>' (pruebas)

        static byte banderasComp, contadorComp; static bool contadorVisto;
        static float tiroDesde = -10f;   // hora del último disparo/golpe del compañero
        public static byte ForzadoBanderas; public static bool ForzadoTiro;   // orden 'armacomp pose ...'

        public static void Recibir(BinaryReader r)
        {
            indiceComp = r.ReadByte();
            if (r.BaseStream.Position >= r.BaseStream.Length) { banderasComp = 0; return; }
            banderasComp = r.ReadByte();
            byte n = r.ReadByte();
            if (contadorVisto && n != contadorComp) tiroDesde = Time.time;
            contadorComp = n; contadorVisto = true;
        }

        class Plantilla
        {
            public string Nombre; public Mesh Malla; public Material[] Mats;
            public Quaternion RotCam;   // giro del arma respecto de la cámara en primera persona
            public Vector3 Agarre;      // dónde la toma la mano derecha (espacio de la malla)
            public Vector3 Boca;        // boca del cañón (espacio de la malla), solo para el informe
            public float Escala;        // de la malla al mundo
            public Vector3 Eje;         // hacia dónde apunta (espacio de la malla): del agarre a la boca
            public float LargoMundo;
            public string Origen;
        }

        static readonly Dictionary<int, Plantilla> plantillas = new Dictionary<int, Plantilla>();
        static readonly HashSet<int> fallidas = new HashSet<int>();
        static PlayerWeapons pwPlantillas;
        static GameObject visible; static int indiceVisible = -1;

        /// Ajuste fino a ojo por arma (orden 'armacomp ajuste <nombre> rx ry rz px py pz'): giro extra (local del arma) y corrimiento (local de la mira, m).
        /// Valores puestos a ojo con capturas el 2026-11-20: la pistola baja a la palma y apunta al suelo; el estoque con la hoja hacia adelante y abajo
        /// (en primera persona la hoja va hacia arriba). Mosquete, escopeta y ballesta quedan bien tal cual (a la cadera, cañón adelante).
        public static readonly Dictionary<string, Vector3[]> Ajustes = new Dictionary<string, Vector3[]>
        {
            { "Pistol_new", new[] { new Vector3(64f, 0f, 0f), new Vector3(0f, -0.07f, 0.02f) } },
            { "Hatchet", new[] { new Vector3(100f, 0f, 0f), Vector3.zero } },
            { Aliados.NombreArma, new[] { Vector3.zero, new Vector3(0f, -0.09f, 0.04f) } },   // la bola en la palma, no en la muñeca
        };

        /// Largo en el mundo que debe tener cada arma en la mano del cazador (m). Si no está, se usa su tamaño en primera persona.
        static float LargoObjetivo(string nombre)
        {
            if (nombre.StartsWith("Pistol")) return 0.48f;
            if (nombre.StartsWith("Musket")) return 1.45f;
            if (nombre == "Hatchet") return 1.0f;            // el estoque
            if (nombre == Escopeta.Nombre) return 0.95f;
            if (nombre == Ballesta.Nombre) return 0.95f;
            return -1f;
        }

        public static void Colocar(Plugin plugin)
        {
            var c = plugin.CompaneroObj;
            int idx = Forzado >= 0 ? Forzado : indiceComp;
            bool mostrar = plugin.Conectado || Forzado >= 0;
            Transform mano = c != null ? c.ManoDerecha : null;
            if (!mostrar || c == null || !c.Visible || mano == null || c.EstadoVida != Muerte.Estado.Vivo || idx == 255) { Ocultar(); return; }

            var pw = Rendimiento.Armas;
            if (pw == null || pw.weaponOrder == null || idx < 0 || idx >= pw.weaponOrder.Length || pw.weaponOrder[idx] == null) { Ocultar(); return; }
            if (pw != pwPlantillas) { Limpiar(); pwPlantillas = pw; }

            Plantilla p;
            if (!plantillas.TryGetValue(idx, out p) || p.Mats == null || System.Array.Exists(p.Mats, m => m == null))
            {
                if (fallidas.Contains(idx)) { Ocultar(); return; }
                p = Construir(plugin, pw.weaponOrder[idx]);
                if (p == null) { fallidas.Add(idx); Ocultar(); return; }
                plantillas[idx] = p; indiceVisible = -1;
            }

            if (visible == null)
            {
                visible = new GameObject("coop_arma_companero");
                visible.AddComponent<MeshFilter>();
                var mr = visible.AddComponent<MeshRenderer>();
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On; mr.receiveShadows = true;
                indiceVisible = -1;
            }
            if (indiceVisible != idx)
            {
                visible.GetComponent<MeshFilter>().sharedMesh = p.Malla;
                visible.GetComponent<MeshRenderer>().sharedMaterials = p.Mats;
                visible.layer = c.Cuerpo.layer;
                indiceVisible = idx;
            }
            if (!visible.activeSelf) visible.SetActive(true);

            Vector3[] aj; Quaternion extra = Quaternion.identity; Vector3 corr = Vector3.zero;
            if (Ajustes.TryGetValue(p.Nombre, out aj)) { extra = Quaternion.Euler(aj[0]); corr = aj[1]; }
            Quaternion mira = c.Mirada;
            // 1) arma colgando de la mano (animaciones de quieto/caminar/correr)
            Vector3 manoAntes = mano.position;
            Quaternion rotCuelga = mira * p.RotCam * extra;
            Vector3 agarreCuelga = manoAntes + mira * corr;

            // 2) arma arriba: apuntando, recién disparada o (estoque) estocada
            byte ban = Forzado >= 0 ? ForzadoBanderas : banderasComp;
            if (Forzado >= 0 && ForzadoTiro) { tiroDesde = Time.time; ForzadoTiro = false; }
            float desdeTiro = Time.time - tiroDesde;
            bool estoque = p.Nombre == "Hatchet", bola = p.Nombre == Aliados.NombreArma;
            bool larga = !estoque && !bola && !p.Nombre.StartsWith("Pistol");
            bool arriba = (ban & Apuntando) != 0 || desdeTiro < (estoque ? 0.5f : 1.6f);
            // corriendo: por la velocidad del muñeco (sprintState del juego queda encendido aunque el jugador esté quieto)
            if (((ban & Recargando) != 0 || c.Velocidad > 4.5f) && desdeTiro > 0.3f) arriba = false;
            float dt = Time.deltaTime;
            peso = Mathf.MoveTowards(peso, arriba ? 1f : 0f, dt * 6f);
            retroceso = desdeTiro < 0.05f && !estoque ? 1f : retroceso * Mathf.Exp(-dt * 12f);

            Transform cabeza = c.Hueso(HumanBodyBones.Head);
            Vector3 ojos = (cabeza != null ? cabeza.position : c.Cuerpo.transform.position + Vector3.up * 1.9f) + Vector3.up * 0.06f;
            Vector3 lugar = larga ? new Vector3(0.13f, -0.21f, 0.30f) : bola ? new Vector3(0.12f, -0.30f, 0.40f) : estoque ? new Vector3(0.20f, -0.32f, 0.42f) : new Vector3(0.16f, -0.17f, 0.52f);
            Quaternion giroArriba = Quaternion.FromToRotation(p.RotCam * p.Eje, Vector3.forward);   // el cañón justo hacia la mira
            if (estoque)
            {
                float e = desdeTiro < 0.5f ? Mathf.Sin(desdeTiro / 0.5f * Mathf.PI) : 0f;          // estocada: ida y vuelta en medio segundo
                lugar += new Vector3(-0.04f, 0.06f, 0.28f) * e;
                giroArriba = Quaternion.Euler(-12f * e, 0f, 0f) * giroArriba;
            }
            Quaternion rotArriba = mira * Quaternion.Euler(-9f * retroceso, 0f, 0f) * giroArriba * p.RotCam;
            Vector3 agarreArriba = ojos + mira * (lugar + new Vector3(0f, 0f, -0.05f * retroceso));

            Quaternion rot = Quaternion.Slerp(rotCuelga, rotArriba, peso);
            Vector3 agarre = Vector3.Lerp(agarreCuelga, agarreArriba, peso);
            visible.transform.rotation = rot;
            visible.transform.localScale = Vector3.one * p.Escala;
            visible.transform.position = agarre - rot * (p.Agarre * p.Escala) - mira * Vector3.up * ((larga ? 0.06f : bola || estoque ? 0.02f : 0.06f) * peso);   // la mano envuelve la empuñadura, no queda encima

            // 3) brazos hacia el arma (solo cuando sube)
            if (peso > 0.001f)
            {
                Vector3 der = c.Cuerpo.transform.right, abajo = -c.Cuerpo.transform.up, adelante = c.Cuerpo.transform.forward;
                var hd = c.Hueso(HumanBodyBones.RightUpperArm); var cd = c.Hueso(HumanBodyBones.RightLowerArm);
                if (hd != null && cd != null)
                {
                    BrazoIK(hd, cd, mano, Vector3.Lerp(manoAntes, agarreArriba, peso), hd.position + abajo * 0.4f + der * 0.25f - adelante * 0.1f);
                    // dedos alrededor de la empuñadura: hacia abajo y un poco adelante
                    Dedos(mano, c.Hueso(HumanBodyBones.RightMiddleProximal), mira * new Vector3(-0.15f, -0.85f, 0.4f), peso);
                }
                if (larga)
                {
                    var hi = c.Hueso(HumanBodyBones.LeftUpperArm); var ci = c.Hueso(HumanBodyBones.LeftLowerArm); var mi = c.Hueso(HumanBodyBones.LeftHand);
                    if (hi != null && ci != null && mi != null)
                    {
                        Vector3 cano = rot * p.Eje;
                        Vector3 apoyo = agarre + cano * Mathf.Min(0.36f * p.LargoMundo, 0.45f) - mira * Vector3.up * 0.09f;
                        BrazoIK(hi, ci, mi, Vector3.Lerp(mi.position, apoyo, peso), hi.position + abajo * 0.4f - der * 0.2f - adelante * 0.1f);
                        // palma bajo el cañón, dedos hacia la derecha envolviéndolo
                        Dedos(mi, c.Hueso(HumanBodyBones.LeftMiddleProximal), mira * new Vector3(0.85f, 0.35f, 0.3f), peso);
                    }
                }
            }
        }

        static float peso, retroceso;

        /// Gira la mano para que los dedos (muñeca → nudillo del medio) vayan hacia esa dirección.
        static void Dedos(Transform mano, Transform dedo, Vector3 hacia, float peso)
        {
            if (mano == null || dedo == null) return;
            Vector3 ahora = dedo.position - mano.position;
            if (ahora.sqrMagnitude < 1e-8f) return;
            mano.rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(ahora, hacia.normalized), peso) * mano.rotation;
        }

        /// IK de dos huesos (hombro-codo-mano): lleva la mano al objetivo con el codo hacia el polo.
        static void BrazoIK(Transform brazo, Transform antebrazo, Transform mano, Vector3 objetivo, Vector3 polo)
        {
            Vector3 a = brazo.position;
            float la = (antebrazo.position - a).magnitude, lb = (mano.position - antebrazo.position).magnitude;
            if (la < 1e-4f || lb < 1e-4f) return;
            Vector3 hacia = objetivo - a;
            float d = Mathf.Clamp(hacia.magnitude, Mathf.Abs(la - lb) + 0.001f, la + lb - 0.001f);
            Vector3 dir = hacia.sqrMagnitude > 1e-8f ? hacia.normalized : brazo.forward;
            float cosA = Mathf.Clamp((la * la + d * d - lb * lb) / (2f * la * d), -1f, 1f);
            Vector3 dobla = polo - a; dobla -= Vector3.Project(dobla, dir);
            if (dobla.sqrMagnitude < 1e-8f) dobla = Vector3.down;
            dobla.Normalize();
            Vector3 codo = a + dir * (la * cosA) + dobla * (la * Mathf.Sqrt(1f - cosA * cosA));
            brazo.rotation = Quaternion.FromToRotation(antebrazo.position - a, codo - a) * brazo.rotation;
            Vector3 fin = a + dir * d;
            antebrazo.rotation = Quaternion.FromToRotation(mano.position - antebrazo.position, fin - antebrazo.position) * antebrazo.rotation;
        }

        static void Ocultar() { if (visible != null && visible.activeSelf) visible.SetActive(false); }

        public static void Limpiar()
        {
            foreach (var p in plantillas.Values) if (p.Malla != null) Object.Destroy(p.Malla);
            plantillas.Clear(); fallidas.Clear(); indiceVisible = -1;
            if (visible != null) Object.Destroy(visible);
            visible = null;
        }

        // ---------------- armar la malla del arma ----------------

        static bool EsBrazo(string n)
        {
            n = n.ToLowerInvariant();
            return n.Contains("hand") || n.Contains("arm") || n.Contains("finger") || n.Contains("thumb") || n.Contains("ruk") || n.Contains("ladon");
        }

        static Plantilla Construir(Plugin plugin, GameObject arma)
        {
            var cam = Camera.main;
            if (cam == null) return null;
            if (arma.name == Aliados.NombreArma) return PlantillaBola(plugin);
            Transform marco = null;                    // espacio en el que queda la malla
            var verts = new List<Vector3>(); var norms = new List<Vector3>(); var uvs = new List<Vector2>();
            var subs = new List<List<int>>(); var mats = new List<Material>();
            string origen;

            // 1) Armas del mod hechas desde el mosquete (escopeta, ballesta): su modelo propio.
            GameObject modeloMod = null;
            if (arma.name == Escopeta.Nombre && Escopeta.Copia != null) modeloMod = Escopeta.Copia.Modelo;
            if (arma.name == Ballesta.Nombre && Ballesta.Copia != null) modeloMod = Ballesta.Copia.Modelo;
            SkinnedMeshRenderer smr = null;
            if (modeloMod == null) foreach (var s in arma.GetComponentsInChildren<SkinnedMeshRenderer>(true)) if (s.name == "smdimport" && s.sharedMesh != null) { smr = s; break; }

            if (modeloMod != null)
            {
                marco = modeloMod.transform; origen = "modelo del mod";
                foreach (var mf in modeloMod.GetComponentsInChildren<MeshFilter>(true)) Agregar(mf, marco, verts, norms, uvs, subs, mats);
            }
            else if (smr != null)
            {
                // 2) Pistola y mosquete: brazos y arma en una sola malla con piel. El arma = vértices movidos por el hueso del arma y sus hijos.
                Transform hueso = null;
                foreach (var t in arma.GetComponentsInChildren<Transform>(true)) if (t.name == "psitol" || t.name == "Rifle") { hueso = t; break; }
                if (hueso == null)
                    foreach (var t in arma.GetComponentsInChildren<Transform>(true))
                        if (t.name == "Fire_03" && t.parent != null) { hueso = t.parent; break; }
                if (hueso == null) { plugin.Log("Arma del compañero: " + arma.name + " sin hueso del arma"); return null; }
                var huesos = smr.bones; var deArma = new bool[huesos.Length]; int idxH = -1;
                for (int i = 0; i < huesos.Length; i++)
                {
                    if (huesos[i] == null) continue;
                    if (huesos[i] == hueso) idxH = i;
                    for (var x = huesos[i]; x != null; x = x.parent) if (x == hueso) { deArma[i] = true; break; }
                }
                if (idxH < 0) { plugin.Log("Arma del compañero: " + arma.name + " el hueso del arma no mueve la malla"); return null; }
                var orig = smr.sharedMesh;
                if (!orig.isReadable) { plugin.Log("Arma del compañero: malla de " + arma.name + " no legible"); return null; }
                var bw = orig.boneWeights; var ov = orig.vertices; var on = orig.normals; var ou = orig.uv;
                var bp = orig.bindposes[idxH];
                var nuevo = new int[ov.Length];
                for (int v = 0; v < ov.Length; v++) nuevo[v] = -1;
                var sm = smr.sharedMaterials;
                for (int s = 0; s < orig.subMeshCount; s++)
                {
                    var tris = orig.GetTriangles(s); var lista = new List<int>();
                    for (int k = 0; k < tris.Length; k += 3)
                    {
                        bool ok = true;
                        for (int j = 0; j < 3 && ok; j++) { var w = bw[tris[k + j]]; ok = deArma[w.boneIndex0] && w.weight0 >= 0.5f; }
                        if (!ok) continue;
                        for (int j = 0; j < 3; j++)
                        {
                            int v = tris[k + j];
                            if (nuevo[v] < 0)
                            {
                                nuevo[v] = verts.Count;
                                verts.Add(bp.MultiplyPoint3x4(ov[v]));
                                norms.Add(on != null && on.Length == ov.Length ? bp.MultiplyVector(on[v]).normalized : Vector3.up);
                                uvs.Add(ou != null && ou.Length == ov.Length ? ou[v] : Vector2.zero);
                            }
                            lista.Add(nuevo[v]);
                        }
                    }
                    if (lista.Count == 0) continue;
                    subs.Add(lista); mats.Add(s < sm.Length ? sm[s] : sm[sm.Length - 1]);
                }
                marco = hueso; origen = "malla con piel, hueso " + hueso.name;
            }
            else
            {
                // 3) Estoque y otras: mallas sueltas (sin las manos ni las partículas).
                marco = arma.transform; origen = "mallas sueltas";
                foreach (var mf in arma.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (mf.sharedMesh == null || mf.name == "null" || EsBrazo(mf.name) || mf.name.ToLowerInvariant().Contains("muzzle")) continue;
                    var r = mf.GetComponent<MeshRenderer>(); if (r == null) continue;
                    Agregar(mf, marco, verts, norms, uvs, subs, mats);
                }
            }
            if (verts.Count == 0) { plugin.Log("Arma del compañero: " + arma.name + " sin malla de arma (" + origen + ")"); return null; }

            var malla = new Mesh { name = "coop_arma_comp_" + arma.name };
            malla.SetVertices(verts); malla.SetNormals(norms); malla.SetUVs(0, uvs);
            malla.subMeshCount = subs.Count;
            for (int s = 0; s < subs.Count; s++) malla.SetTriangles(subs[s], s);
            malla.RecalculateBounds();

            var p = new Plantilla { Nombre = arma.name, Malla = malla, Mats = mats.ToArray(), Origen = origen };
            p.RotCam = Quaternion.Inverse(cam.transform.rotation) * marco.rotation;

            // Agarre: donde está la mano derecha de primera persona; si no hay, a un 30 % desde la culata por el lado largo.
            Transform manoFP = null;
            foreach (var t in arma.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name;
                if (n == "R Hand" || n == "pravladon" || n == "right hand") { manoFP = t; break; }
            }
            var caja = malla.bounds;
            Vector3 boca = caja.center;
            foreach (var t in arma.GetComponentsInChildren<Transform>(true)) if (t.name == "Fire_03") { boca = marco.InverseTransformPoint(t.position); break; }
            p.Boca = boca;
            if (manoFP != null)
            {
                Vector3 pos = manoFP.position;
                var r = manoFP.GetComponent<Renderer>(); if (r != null) pos = r.bounds.center;
                p.Agarre = marco.InverseTransformPoint(pos);
                if (!caja.Contains(p.Agarre)) p.Agarre = caja.ClosestPoint(p.Agarre);   // la mano en primera persona puede estar un poco fuera
            }
            else
            {
                Vector3 s3 = caja.size; int eje = s3.x >= s3.y && s3.x >= s3.z ? 0 : (s3.y >= s3.z ? 1 : 2);
                Vector3 dir = Vector3.zero; dir[eje] = Mathf.Sign(boca[eje] - caja.center[eje]); if (dir == Vector3.zero) dir[eje] = 1f;
                p.Agarre = caja.center - dir * s3[eje] * 0.2f;
            }

            // Escala: el largo que debe tener en el mundo (o su tamaño en primera persona).
            float escalaFP = marco.lossyScale.x;
            float largoMalla = Mathf.Max(caja.size.x, Mathf.Max(caja.size.y, caja.size.z));
            float objetivo = LargoObjetivo(arma.name);
            p.Escala = objetivo > 0f && largoMalla > 1e-4f ? objetivo / largoMalla : escalaFP;

            {   // eje del cañón = el lado más largo de la caja (la muñeca queda por encima del arma: muñeca→boca sale inclinado),
                // hacia la boca; sin boca (estoque), hacia el extremo más lejano del agarre
                Vector3 s3 = caja.size; int eje = s3.x >= s3.y && s3.x >= s3.z ? 0 : (s3.y >= s3.z ? 1 : 2);
                // Sentido: en primera persona toda arma de fuego apunta hacia adelante de la cámara (las chispas Fire_03 NO están en la boca:
                // en el mosquete quedan del lado contrario). El estoque va hacia arriba en primera persona: hacia el extremo lejano del agarre.
                Vector3 e = Vector3.zero; e[eje] = 1f;
                if (arma.name == "Hatchet") { if (caja.center[eje] - p.Agarre[eje] < 0f) e = -e; }
                else if ((p.RotCam * e).z < 0f) e = -e;
                p.Eje = e;
            }
            p.LargoMundo = largoMalla * p.Escala;
            plugin.Log("Arma del compañero: " + arma.name + " lista (" + origen + ", " + verts.Count + " vértices, " + subs.Count + " materiales, largo "
                + (largoMalla * p.Escala).ToString("0.00") + " m; boca respecto de la mira " + (p.RotCam * (boca - p.Agarre)).normalized.ToString("F2") + ")");
            return p;
        }

        /// La bola de cristal no es una malla del juego (Aliados la dibuja aparte): esfera violeta con la misma textura y brillo, en la palma.
        static Plantilla PlantillaBola(Plugin plugin)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            var malla = Object.Instantiate(g.GetComponent<MeshFilter>().sharedMesh); malla.name = "coop_arma_comp_bola";
            Object.Destroy(g);
            var m = new Material(Shader.Find("Standard")) { name = "coop_bola_companero" };
            m.mainTexture = Aliados.TexturaBola(); m.color = new Color(0.35f, 0.15f, 0.55f);
            m.SetFloat("_Glossiness", 0.95f); m.SetFloat("_Metallic", 0.1f);
            m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", new Color(0.35f, 0.08f, 0.6f)); m.SetTexture("_EmissionMap", m.mainTexture);
            plugin.Log("Arma del compañero: bola de cristal lista");
            // agarre por encima del centro: la bola cuelga dentro de la mano (que el cazador lleva caída)
            return new Plantilla { Nombre = Aliados.NombreArma, Malla = malla, Mats = new[] { m }, RotCam = Quaternion.identity, Agarre = new Vector3(0f, 0.4f, 0f), Boca = Vector3.forward, Eje = Vector3.forward, LargoMundo = 0.15f, Escala = 0.15f, Origen = "bola del mod" };
        }

        static void Agregar(MeshFilter mf, Transform marco, List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs, List<List<int>> subs, List<Material> mats)
        {
            var m = mf.sharedMesh; if (m == null || !m.isReadable) return;
            var r = mf.GetComponent<Renderer>(); var sm = r != null ? r.sharedMaterials : new Material[0];
            if (sm.Length == 0) return;
            Matrix4x4 a = marco.worldToLocalMatrix * mf.transform.localToWorldMatrix;
            var mv = m.vertices; var mn = m.normals; var mu = m.uv;
            int base0 = verts.Count;
            for (int v = 0; v < mv.Length; v++)
            {
                verts.Add(a.MultiplyPoint3x4(mv[v]));
                norms.Add(mn != null && mn.Length == mv.Length ? a.MultiplyVector(mn[v]).normalized : Vector3.up);
                uvs.Add(mu != null && mu.Length == mv.Length ? mu[v] : Vector2.zero);
            }
            for (int s = 0; s < m.subMeshCount; s++)
            {
                var tris = m.GetTriangles(s); var lista = new List<int>(tris.Length);
                for (int k = 0; k < tris.Length; k++) lista.Add(tris[k] + base0);
                subs.Add(lista); mats.Add(s < sm.Length ? sm[s] : sm[sm.Length - 1]);
            }
        }

        /// Para la orden 'armacomp'.
        public static string Informe(Plugin plugin)
        {
            var sb = new System.Text.StringBuilder("arma del compañero: banderas " + banderasComp + " disparos " + contadorComp + " peso " + peso.ToString("0.00") + ", recibida " + indiceComp + (Forzado >= 0 ? " (forzada " + Forzado + ")" : "") + ", mía " + indiceMio
                + ", visible " + (visible != null && visible.activeSelf ? visible.transform.position.ToString("F2") : "no"));
            var c = plugin.CompaneroObj;
            sb.Append(", mano " + (c != null && c.ManoDerecha != null ? c.ManoDerecha.position.ToString("F2") : "sin mano"));
            foreach (var kv in plantillas)
                sb.Append("\n    " + kv.Key + " " + kv.Value.Nombre + ": " + kv.Value.Origen + ", giro " + kv.Value.RotCam.eulerAngles.ToString("F0") + ", agarre " + kv.Value.Agarre.ToString("F3")
                    + ", escala " + kv.Value.Escala.ToString("F3") + ", boca " + (kv.Value.RotCam * (kv.Value.Boca - kv.Value.Agarre)).normalized.ToString("F2"));
            foreach (var f in fallidas) sb.Append("\n    " + f + ": no se pudo armar");
            return sb.ToString();
        }
    }
}

// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using System;
using System.Collections.Generic;
using PixelCrushers.DialogueSystem;
using UnityEngine;

namespace WitchHuntCoop
{
    /// Arma nueva hecha como copia del mosquete del juego (mismas animaciones de apuntar, disparar y
    /// recargar). Del modelo del mosquete se dejan solo los brazos (se quitan los triángulos que mueven
    /// los huesos del arma) y en el hueso del arma se pone un modelo hecho por el mod.
    /// La usan la escopeta recortada (4) y la ballesta (6).
    public class ArmaCopia
    {
        public readonly string Nombre, VarTiene, Etiqueta;
        public readonly int Indice;
        readonly Func<Mesh> malla;
        readonly Func<Texture2D> textura;
        readonly float largoMalla, fraccion;   // largo del modelo (m) y fracción del largo del mosquete que debe ocupar
        readonly Action<WeaponBehavior> configurar;

        public GameObject Arma;
        public GameObject Modelo;
        public Vector3 AjustePos, AjusteRot; public float AjusteEscala = 1f;
        /// Modelo sacado del propio mosquete (misma textura): se acorta y se duplica el cañón (escopeta recortada).
        public bool DelMosquete, Bisagra; public float PosBisagra = 0.27f, AjusteAltoBisagra = 0f; public Mesh MallaCanones; public Vector3 PivoteHueso, EjeBisagra; public Transform Pivote; public Vector3 EjeLargoHueso, EjeAltoHueso; /* hacia la boca y hacia arriba, en el espacio del hueso */ public float Transicion = 0.08f, SubirArma = 0.05f; public float CorteCanon = 0.30f, LargoCanon = 0.55f, SeparacionCanon = 0.42f; public bool ConArco;   // ballesta: el cañón acortado hace de riel y se le agrega el arco
        Vector3 basePos; Quaternion baseRot; float baseEscala = 1f;
        /// Piezas móviles de la ballesta (brazos, cuerdas y flecha), separadas de la malla fija para poder animarlas.
        public class DinBallesta
        {
            public Vector3 L, Up, D, Nuez, Frente; public float U;
            public Vector3[] Base = new Vector3[2], PuntaOff = new Vector3[2];
            public Mesh[] MallaBrazo = new Mesh[2]; public Mesh MallaCuerda, MallaFlecha; public Vector3 PivoteFlecha;
            public Transform[] Brazo = new Transform[2], Cuerda = new Transform[2]; public Transform Flecha;
        }
        public DinBallesta Din;
        public class GeomDin { public List<Vector3> V = new List<Vector3>(); public List<Vector2> U = new List<Vector2>(); public List<Vector3> N = new List<Vector3>(); public List<int>[] S = { new List<int>(), new List<int>(), new List<int>() }; }
        static Mesh MallaDin(GeomDin g, Vector3 origen, string nombre)
        {
            var m = new Mesh { name = nombre }; var v = new List<Vector3>(g.V.Count);
            foreach (var p in g.V) v.Add(p - origen);
            m.SetVertices(v); m.SetUVs(0, g.U); m.SetNormals(g.N); m.subMeshCount = 3;
            for (int i = 0; i < 3; i++) m.SetTriangles(g.S[i].ToArray(), i);
            m.RecalculateBounds(); return m;
        }
        public Material MaterialExtra { get { return materialesExtra != null ? materialesExtra[0] : null; } }
        delegate void CajaWDel(Vector3 a, Vector3 b, Vector3 c, float d, float e, Vector2 f);
        delegate void CajaDel(Vector3 a, Vector3 b, Vector3 c, float d, float e, int f);
        List<int>[] submallasExtra; Material[] materialesExtra; Material[] matsOrig;   // ballesta: submallas (madera, hierro, mate) con textura propia

        public ArmaCopia(string nombre, string varTiene, string etiqueta, int indice, Func<Mesh> malla, Func<Texture2D> textura,
                         float largoMalla, float fraccion, Vector3 ajustePos, Action<WeaponBehavior> configurar)
        {
            Nombre = nombre; VarTiene = varTiene; Etiqueta = etiqueta; Indice = indice;
            this.malla = malla; this.textura = textura; this.largoMalla = largoMalla; this.fraccion = fraccion;
            AjustePos = ajustePos; this.configurar = configurar;
        }

        static Plugin plugin { get { return Escopeta.plugin; } }

        public bool Tiene
        {
            get { try { return DialogueLua.GetVariable(VarTiene).AsBool; } catch { return false; } }
        }

        public void Dar(bool si)
        {
            DialogueLua.SetVariable(VarTiene, si);
            var wb = Arma != null ? Arma.GetComponent<WeaponBehavior>() : null;
            if (wb != null) wb.haveWeapon = si;
        }

        /// Crea el arma si falta (o tras cambiar de escena) y la mantiene en su puesto. Devuelve su
        /// WeaponBehavior (o null).
        public WeaponBehavior Actualizar()
        {
            var pw = Rendimiento.Armas;
            if (pw == null || pw.weaponOrder == null || !Guardado.ModoCoop) return null;
            if (Arma == null || pw.weaponOrder.Length <= Indice || pw.weaponOrder[Indice] != Arma)
            {
                try { Crear(pw); } catch (Exception e) { plugin.Log(Etiqueta + ": error al crear " + e); Arma = null; }
            }
            if (Arma == null) return null;
            var w = Arma.GetComponent<WeaponBehavior>();
            if (w != null && w.haveWeapon != Tiene) w.haveWeapon = Tiene;
            return w;
        }

        void Crear(PlayerWeapons pw)
        {
            GameObject musket = null;
            foreach (var g in pw.weaponOrder) if (g != null && g.name.StartsWith("Musket")) musket = g;
            if (musket == null) return;
            // Si ya existe (la escena no cambió), solo reponerla en la lista.
            Transform ya = musket.transform.parent.Find(Nombre);
            Arma = ya != null ? ya.gameObject : null;
            if (Arma == null)
            {
                bool activo = musket.activeSelf;
                musket.SetActive(false);   // se copia apagada: nada se ejecuta al nacer
                Arma = (GameObject)UnityEngine.Object.Instantiate(musket, musket.transform.parent, false);
                musket.SetActive(activo);
                Arma.name = Nombre;
                PrepararModelo();
            }
            var wb = Arma.GetComponent<WeaponBehavior>();
            wb.weaponNumber = Indice;
            configurar(wb);
            wb.haveWeapon = Tiene;
            // En la lista del juego, en su puesto (los huecos que falten se rellenan con el desarmado).
            var lista = new List<GameObject>(pw.weaponOrder);
            while (lista.Count <= Indice) lista.Add(null);
            lista[Indice] = Arma;
            for (int i = 0; i < lista.Count; i++) if (lista[i] == null) lista[i] = pw.weaponOrder[0];
            pw.weaponOrder = lista.ToArray();
            plugin.Log(Etiqueta + ": arma " + Indice + " lista (tiene=" + wb.haveWeapon + ")");
        }

        /// Brazos sin el mosquete + modelo propio en el hueso del arma.
        void PrepararModelo()
        {
            SkinnedMeshRenderer smr = null;
            foreach (var s in Arma.GetComponentsInChildren<SkinnedMeshRenderer>(true)) if (s.name == "smdimport") smr = s;
            if (smr == null) { plugin.Log(Etiqueta + ": no encontré la malla del mosquete"); return; }
            Transform rifle = null;
            foreach (var t in Arma.GetComponentsInChildren<Transform>(true)) if (t.name == "Rifle") { rifle = t; break; }
            if (rifle == null) { plugin.Log(Etiqueta + ": no encontré el hueso del arma"); return; }
            var huesos = smr.bones;
            var deArma = new bool[huesos.Length];
            int idxRifle = -1;
            for (int i = 0; i < huesos.Length; i++)
            {
                if (huesos[i] == null) continue;
                if (huesos[i] == rifle) idxRifle = i;
                for (var x = huesos[i]; x != null; x = x.parent) if (x == rifle) { deArma[i] = true; break; }
            }
            var orig = smr.sharedMesh;
            var bw = orig.boneWeights;
            var verts = orig.vertices;
            var esArma = new bool[bw.Length];
            for (int v = 0; v < bw.Length; v++) esArma[v] = deArma[bw[v].boneIndex0] && bw[v].weight0 >= 0.5f;
            // Caja del mosquete en el espacio del hueso (para colocar el modelo nuevo).
            var bp = orig.bindposes;
            Bounds caja = new Bounds(); bool hay = false;
            if (idxRifle >= 0)
                for (int v = 0; v < verts.Length; v++)
                {
                    if (!esArma[v]) continue;
                    Vector3 p = bp[idxRifle].MultiplyPoint3x4(verts[v]);
                    if (!hay) { caja = new Bounds(p, Vector3.zero); hay = true; } else caja.Encapsulate(p);
                }
            var m = UnityEngine.Object.Instantiate(orig);
            m.name = "coop_brazos_" + Nombre;
            int quitados = 0;
            for (int sm = 0; sm < m.subMeshCount; sm++)
            {
                var tris = m.GetTriangles(sm);
                var quedan = new List<int>(tris.Length);
                for (int k = 0; k < tris.Length; k += 3)
                {
                    if (esArma[tris[k]] || esArma[tris[k + 1]] || esArma[tris[k + 2]]) { quitados++; continue; }
                    quedan.Add(tris[k]); quedan.Add(tris[k + 1]); quedan.Add(tris[k + 2]);
                }
                m.SetTriangles(quedan, sm);
            }
            smr.sharedMesh = m;
            Vector3 boca = caja.max;
            foreach (var t in Arma.GetComponentsInChildren<Transform>(true)) if (t.name == "Fire_03") { boca = rifle.InverseTransformPoint(t.position); break; }
            plugin.Log(Etiqueta + ": brazos sin mosquete (" + quitados + " triángulos fuera); mosquete en el hueso: centro " + caja.center.ToString("F3") + " tamaño " + caja.size.ToString("F3"));

            // Eje del cañón: el lado más largo de la caja (las partículas del disparo no están en la
            // boca: comprobado a ojo que el cañón va hacia el lado contrario).
            Vector3 s3 = caja.size; int eje = s3.x >= s3.y && s3.x >= s3.z ? 0 : (s3.y >= s3.z ? 1 : 2);
            Vector3 dir = Vector3.zero; dir[eje] = -Mathf.Sign(boca[eje] - caja.center[eje]); if (dir == Vector3.zero) dir[eje] = -1f;
            float largo = s3[eje];
            // Empuñadura del modelo donde estaba la del mosquete (a ~28 % desde la culata).
            Vector3 culata = caja.center - dir * largo * 0.5f;
            Vector3 agarre = culata + dir * largo * 0.28f;
            int eje2 = -1; float m2 = -1f;
            for (int k = 0; k < 3; k++) if (k != eje && s3[k] > m2) { m2 = s3[k]; eje2 = k; }
            Vector3 arriba = Vector3.zero; arriba[eje2] = 1f;

            Modelo = new GameObject("coop_modelo_" + Nombre);
            Modelo.layer = smr.gameObject.layer;
            Modelo.transform.SetParent(rifle, false);
            if (DelMosquete && idxRifle >= 0)
            {
                int eje3 = 3 - eje - eje2;
                EjeLargoHueso = dir; EjeAltoHueso = arriba;   // (los usa el arco de la ballesta)
                matsOrig = smr.sharedMaterials; submallasExtra = null; materialesExtra = null;
                var mm = MallaDelMosquete(orig, verts, esArma, bp[idxRifle], culata, dir, largo, eje3, eje2);
                Modelo.AddComponent<MeshFilter>().sharedMesh = mm;
                var mr2 = Modelo.AddComponent<MeshRenderer>();
                var mats = smr.sharedMaterials; var copia = new Material[mats.Length + (materialesExtra != null ? materialesExtra.Length : 0)];
                for (int i = 0; i < mats.Length; i++)
                {
                    copia[i] = mats[i];
                    if (ConArco && mats[i] != null && mats[i].HasProperty("_EmissionMap") && mats[i].mainTexture != null)
                    {
                        // copia propia de la culata con un poco de luz propia (igual que el resto de la ballesta): sus lados en sombra ya no salen negros
                        var mc = new Material(mats[i]) { name = mats[i].name + "_ballesta" };
                        mc.EnableKeyword("_EMISSION"); mc.SetTexture("_EmissionMap", mats[i].mainTexture); mc.SetColor("_EmissionColor", new Color(0.16f, 0.16f, 0.16f, 1f));
                        mc.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                        copia[i] = mc;
                    }
                }
                if (materialesExtra != null) for (int i = 0; i < materialesExtra.Length; i++) copia[mats.Length + i] = materialesExtra[i];
                mr2.sharedMaterials = copia;
                mr2.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                if (Din != null && Din.MallaFlecha != null)
                {
                    Func<string, Mesh, Vector3, Transform> pieza = (nom, malla, pos) =>
                    {
                        var go = new GameObject(nom); go.layer = Modelo.layer; go.transform.SetParent(Modelo.transform, false); go.transform.localPosition = pos;
                        go.AddComponent<MeshFilter>().sharedMesh = malla; var rr = go.AddComponent<MeshRenderer>();
                        rr.sharedMaterials = materialesExtra; rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; rr.receiveShadows = false;
                        return go.transform;
                    };
                    for (int i = 0; i < 2; i++) { Din.Brazo[i] = pieza("coop_brazo" + i, Din.MallaBrazo[i], Din.Base[i]); Din.Cuerda[i] = pieza("coop_cuerda" + i, Din.MallaCuerda, Din.Base[i] + Din.PuntaOff[i]); }
                    Din.Flecha = pieza("coop_flecha_obj", Din.MallaFlecha, Vector3.zero);
                    plugin.Log(Etiqueta + ": piezas móviles creadas (2 brazos, 2 cuerdas, flecha)");
                }
                if (ConArco) mr2.receiveShadows = false;   // las sombras de las hojas manchaban la madera de la ballesta
                EjeLargoHueso = dir; EjeAltoHueso = arriba;
                if (Bisagra && MallaCanones != null)
                {
                    var pv = new GameObject("coop_pivote"); pv.layer = Modelo.layer;
                    pv.transform.SetParent(Modelo.transform, false);
                    pv.transform.localPosition = PivoteHueso;
                    pv.AddComponent<MeshFilter>().sharedMesh = MallaCanones;
                    var mr3 = pv.AddComponent<MeshRenderer>(); mr3.sharedMaterials = copia;
                    mr3.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    Pivote = pv.transform;
                }
                baseEscala = fraccion; basePos = agarre * (1f - fraccion) + arriba * SubirArma; baseRot = Quaternion.identity;
                Colocar();
                return;
            }
            Modelo.AddComponent<MeshFilter>().sharedMesh = malla();
            var mr = Modelo.AddComponent<MeshRenderer>();
            var mat = new Material(smr.sharedMaterial) { name = "coop_" + Nombre };
            mat.mainTexture = textura(); mat.color = Color.white;
            if (mat.HasProperty("_BumpMap")) mat.SetTexture("_BumpMap", null);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.35f);
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // Modelo: X = cañón, Y = arriba, origen en la empuñadura.
            baseEscala = (largo * fraccion) / largoMalla;
            basePos = agarre;
            baseRot = Quaternion.LookRotation(Vector3.Cross(dir, arriba), arriba);
            Colocar();
        }

        /// Malla del arma del mosquete en el espacio de su hueso: cañón acortado y duplicado a los lados.
        Mesh MallaDelMosquete(Mesh orig, Vector3[] verts, bool[] esArma, Matrix4x4 bind, Vector3 culata, Vector3 dir, float largo, int eje3, int eje2)
        {
            var uvs = orig.uv; var nor = orig.normals;
            int n = verts.Length;
            var pos = new Vector3[n]; var frac = new float[n];
            float ancho0 = float.MaxValue, ancho1 = float.MinValue;
            for (int i = 0; i < n; i++)
            {
                pos[i] = bind.MultiplyPoint3x4(verts[i]);
                frac[i] = Vector3.Dot(pos[i] - culata, dir) / largo;
                if (esArma[i] && frac[i] > CorteCanon) { ancho0 = Mathf.Min(ancho0, pos[i][eje3]); ancho1 = Mathf.Max(ancho1, pos[i][eje3]); }
            }
            // piezas sueltas del arma (componentes conexas) para saber qué es cada una
            var padre = new int[n]; for (int i = 0; i < n; i++) padre[i] = i;
            Func<int, int> raiz = null; raiz = x => { while (padre[x] != x) { padre[x] = padre[padre[x]]; x = padre[x]; } return x; };
            for (int sm = 0; sm < orig.subMeshCount; sm++)
            {
                var t = orig.GetTriangles(sm);
                for (int k = 0; k < t.Length; k += 3)
                {
                    if (!(esArma[t[k]] && esArma[t[k + 1]] && esArma[t[k + 2]])) continue;
                    padre[raiz(t[k + 1])] = raiz(t[k]); padre[raiz(t[k + 2])] = raiz(t[k]);
                }
            }
            var info = new Dictionary<int, float[]>();
            for (int i = 0; i < n; i++)
            {
                if (!esArma[i]) continue;
                int r = raiz(i); float[] d;
                if (!info.TryGetValue(r, out d)) { d = new[] { 0f, 9e9f, -9e9f, 9e9f, -9e9f, 9e9f, -9e9f }; info[r] = d; }
                d[0]++; d[1] = Mathf.Min(d[1], frac[i]); d[2] = Mathf.Max(d[2], frac[i]);
                d[3] = Mathf.Min(d[3], pos[i][eje2]); d[4] = Mathf.Max(d[4], pos[i][eje2]);
                d[5] = Mathf.Min(d[5], pos[i][eje3]); d[6] = Mathf.Max(d[6], pos[i][eje3]);
            }
            // eje entre los dos cañones: mediana lateral de lo que va delante
            var laterales = new List<float>();
            for (int i = 0; i < n; i++) if (esArma[i] && frac[i] > CorteCanon) laterales.Add(pos[i][eje3]);
            laterales.Sort();
            float medio = laterales.Count > 0 ? laterales[laterales.Count / 2] : 0f;
            // piezas que se quitan (pedido del usuario): muescas de la mira (largo 0,53–0,60), aletas altas del seguro
            // (0,26–0,36, alto > 0,1), el resto del mecanismo del seguro (placas) y anillos/bandas de los cañones (largo > 0,88)
            var ocultas = new HashSet<int>();
            foreach (var kv in info)
            {
                var d = kv.Value;
                if (d[1] >= 0.53f && d[2] <= 0.60f) ocultas.Add(kv.Key);
                else if (d[1] >= 0.26f && d[2] <= 0.36f && d[4] > 0.1f) ocultas.Add(kv.Key);
                else if (d[1] >= 0.25f && d[2] <= 0.36f && d[0] < 300 && d[4] > 0.04f && Mathf.Abs((d[5] + d[6]) * 0.5f - medio) > 0.035f) ocultas.Add(kv.Key);   // placas laterales del seguro (no la recámara central)
                else if (d[1] >= 0.88f && d[0] < 300) ocultas.Add(kv.Key);
            }
            float centro3 = (ancho0 + ancho1) * 0.5f, off = (ancho1 - ancho0) * 0.5f * SeparacionCanon;
            var V = new List<Vector3>(); var U = new List<Vector2>(); var N = new List<Vector3>(); var Fr = new List<float>();
            var m = new Mesh { name = "coop_mosquete_" + Nombre };
            var mapa = new Dictionary<long, int>();   // (vértice original, copia) → índice nuevo: se comparten (límite de 65 535)
            var tris = new List<int>[orig.subMeshCount];
            for (int sm = 0; sm < orig.subMeshCount; sm++)
            {
                tris[sm] = new List<int>();
                var t = orig.GetTriangles(sm);
                for (int k = 0; k < t.Length; k += 3)
                {
                    int a = t[k], b = t[k + 1], c = t[k + 2];
                    if (!(esArma[a] && esArma[b] && esArma[c])) continue;
                    if (ocultas.Contains(raiz(a))) continue;
                    // desde un poco antes del corte los triángulos se duplican y se separan poco a poco: sin grietas en la caña
                    bool delante = Mathf.Max(frac[a], Mathf.Max(frac[b], frac[c])) > CorteCanon - Transicion;
                    int copias = delante ? 2 : 1;
                    float fc = (frac[a] + frac[b] + frac[c]) / 3f, lc = (pos[a][eje3] + pos[b][eje3] + pos[c][eje3]) / 3f;
                    float wc = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((fc - (CorteCanon - Transicion)) / (2f * Transicion)));
                    for (int q = 0; q < copias; q++)
                    {
                        float lado = delante ? (q == 0 ? -1f : 1f) : 0f;
                        // cada copia se queda solo con su mitad: sin superficies solapadas (se veían dos texturas chocando)
                        if (delante && lado < 0f && lc + lado * off * wc >= medio) continue;
                        if (delante && lado > 0f && lc + lado * off * wc < medio) continue;
                        foreach (int idx in new[] { a, b, c })
                        {
                            long clave = (long)idx * 4 + q;
                            int nuevo;
                            if (!mapa.TryGetValue(clave, out nuevo))
                            {
                                Vector3 p = pos[idx];
                                if (delante)
                                {
                                    float f = frac[idx] > CorteCanon ? CorteCanon + (frac[idx] - CorteCanon) * LargoCanon : frac[idx];   // cañón acortado
                                    p += dir * ((f - frac[idx]) * largo);
                                    float w = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((frac[idx] - (CorteCanon - Transicion)) / (2f * Transicion)));
                                    p[eje3] += lado * off * w;
                                }
                                nuevo = V.Count; mapa[clave] = nuevo;
                                V.Add(p); U.Add(uvs.Length > idx ? uvs[idx] : Vector2.zero); N.Add(nor.Length > idx ? nor[idx] : Vector3.up); Fr.Add(frac[idx]);
                            }
                            tris[sm].Add(nuevo);
                        }
                        // cara trasera: lo que era hueco (cáscaras abiertas del mosquete) queda cerrado por dentro
                        int n0 = tris[sm].Count - 3;
                        int[] orden = { tris[sm][n0], tris[sm][n0 + 2], tris[sm][n0 + 1] };
                        for (int z = 0; z < 3; z++)
                        {
                            long clave2 = -1 - ((long)(z == 0 ? a : (z == 1 ? c : b)) * 4 + q);
                            int inv;
                            if (!mapa.TryGetValue(clave2, out inv))
                            {
                                int vi = orden[z];
                                inv = V.Count; mapa[clave2] = inv;
                                V.Add(V[vi]); U.Add(U[vi]); N.Add(-N[vi]); Fr.Add(Fr[vi]);
                            }
                            tris[sm].Add(inv);
                        }
                    }
                }
            }
            if (V.Count > 65000) plugin.Log(Etiqueta + ": ¡demasiados vértices (" + V.Count + ")!");
            if (Bisagra)
            {
                // Se parte en culata y cañones por la bisagra (largo = PosBisagra): los cañones giran en un hijo.
                Vector3 piv = culata + dir * (largo * PosBisagra);
                piv[eje3] = medio;
                float minAlto = 9e9f;
                for (int i = 0; i < V.Count; i++) if (Fr[i] >= PosBisagra && Fr[i] <= PosBisagra + 0.12f) minAlto = Mathf.Min(minAlto, V[i][eje2]);
                piv[eje2] = minAlto + AjusteAltoBisagra;
                PivoteHueso = piv;
                EjeBisagra = Vector3.zero; EjeBisagra[eje3] = 1f;
                m = ParteMalla(V, U, N, Fr, tris, orig.subMeshCount, false, Vector3.zero, "coop_culata_" + Nombre);
                MallaCanones = ParteMalla(V, U, N, Fr, tris, orig.subMeshCount, true, piv, "coop_canones_" + Nombre);
                plugin.Log(Etiqueta + ": bisagra en " + piv.ToString("F3") + " (eje " + EjeBisagra + ")");
                return m;
            }
            if (ConArco) AgregarArco(V, U, N, tris, Fr, culata, dir, largo, eje3, eje2);
            m.SetVertices(V); m.SetUVs(0, U); m.SetNormals(N);
            m.subMeshCount = orig.subMeshCount + (submallasExtra != null ? submallasExtra.Length : 0);
            for (int sm = 0; sm < orig.subMeshCount; sm++) m.SetTriangles(tris[sm].ToArray(), sm);
            if (submallasExtra != null) for (int i = 0; i < submallasExtra.Length; i++) m.SetTriangles(submallasExtra[i].ToArray(), orig.subMeshCount + i);
            m.RecalculateBounds();
            plugin.Log(Etiqueta + ": malla del mosquete " + V.Count + " vértices, cañón desde " + CorteCanon + ", límites " + m.bounds.center.ToString("F3") + " " + m.bounds.size.ToString("F3") + ", ancho cañón " + (ancho1 - ancho0).ToString("F3"));
            return m;
        }

        // ---------------- Arco de la ballesta, con las piezas y la textura del propio mosquete ----------------

        void CajaBarra(List<Vector3> V, List<Vector2> U, List<Vector3> N, List<int> tri, Vector3 a, Vector3 b, Vector3 arriba, float alto, float ancho, Vector2 uv)
        {
            Vector3 d = b - a; if (d.sqrMagnitude < 1e-10f) return;
            Vector3 dn = d.normalized, lado = Vector3.Cross(arriba, dn); if (lado.sqrMagnitude < 1e-8f) lado = Vector3.Cross(Vector3.right, dn); lado = lado.normalized * ancho * 0.5f;
            Vector3 up = Vector3.Cross(dn, lado.normalized).normalized * alto * 0.5f;
            Vector3[] A = { a - lado - up, a + lado - up, a + lado + up, a - lado + up }, B = { b - lado - up, b + lado - up, b + lado + up, b - lado + up };
            System.Action<Vector3, Vector3, Vector3, Vector3> cara = (p0, p1, p2, p3) =>
            {
                Vector3 n = Vector3.Cross(p1 - p0, p3 - p0).normalized; int i = V.Count;
                foreach (var p in new[] { p0, p1, p2, p3 }) { V.Add(p); U.Add(uv); N.Add(n); }
                tri.Add(i); tri.Add(i + 1); tri.Add(i + 2); tri.Add(i); tri.Add(i + 2); tri.Add(i + 3);
            };
            cara(A[0], A[3], A[2], A[1]); cara(B[0], B[1], B[2], B[3]);
            cara(A[0], A[1], B[1], B[0]); cara(A[1], A[2], B[2], B[1]); cara(A[2], A[3], B[3], B[2]); cara(A[3], A[0], B[0], B[3]);
        }

        /// Bloque recto texturado con un parche de la textura: la veta corre a lo largo (en espejo cada "repite" metros) y el parche se estira a lo ancho de cada cara.
        void CajaTextura(List<Vector3> V, List<Vector2> U, List<Vector3> N, List<int> tri, Vector3 a, Vector3 b, Vector3 arriba, float alto, float ancho,
                         Vector3 culata, Vector3 D, float repite, Vector2 uvLo, Vector2 uvHi, bool vetaEnU)
        {
            Vector3 d = b - a; float len = d.magnitude; if (len < 1e-6f) return;
            Vector3 dn = d / len, lado = Vector3.Cross(arriba, dn); if (lado.sqrMagnitude < 1e-8f) lado = Vector3.Cross(Vector3.right, dn); lado = lado.normalized * ancho * 0.5f;
            Vector3 up = Vector3.Cross(dn, lado.normalized).normalized * alto * 0.5f;
            var ts = new List<float> { 0f };
            for (int k = 1; k * repite < len; k++) ts.Add(k * repite / len);
            ts.Add(1f);
            Func<float, Vector3[]> anillo = t => { Vector3 c = a + d * t; return new[] { c - lado - up, c + lado - up, c + lado + up, c - lado + up }; };
            Func<float, float, Vector2> mapa = (g, c) => vetaEnU ? new Vector2(Mathf.Lerp(uvLo.x, uvHi.x, g), Mathf.Lerp(uvLo.y, uvHi.y, c))
                                                                  : new Vector2(Mathf.Lerp(uvLo.x, uvHi.x, c), Mathf.Lerp(uvLo.y, uvHi.y, g));
            Action<Vector3[], Vector2[]> cara = (pp, uu) =>
            {
                Vector3 n = Vector3.Cross(pp[1] - pp[0], pp[3] - pp[0]).normalized; int i = V.Count;
                for (int z = 0; z < 4; z++) { V.Add(pp[z]); U.Add(uu[z]); N.Add(n); }
                tri.Add(i); tri.Add(i + 1); tri.Add(i + 2); tri.Add(i); tri.Add(i + 2); tri.Add(i + 3);
            };
            var A0 = anillo(0f);
            cara(new[] { A0[0], A0[3], A0[2], A0[1] }, new[] { mapa(0f, 0f), mapa(0f, 1f), mapa(1f, 1f), mapa(1f, 0f) });
            for (int i = 0; i + 1 < ts.Count; i++)
            {
                var P = anillo(ts[i]); var Q = anillo(ts[i + 1]);
                float x0 = ts[i] * len / repite, x1 = ts[i + 1] * len / repite; int k = Mathf.FloorToInt((x0 + x1) * 0.5f);
                float g0 = (k % 2 == 0) ? x0 - k : 1f - (x0 - k), g1 = (k % 2 == 0) ? x1 - k : 1f - (x1 - k);
                for (int e = 0; e < 4; e++)
                {
                    int f = (e + 1) % 4;
                    cara(new[] { P[e], P[f], Q[f], Q[e] }, new[] { mapa(g0, 0f), mapa(g0, 1f), mapa(g1, 1f), mapa(g1, 0f) });
                }
            }
            var B0 = anillo(1f);
            cara(new[] { B0[0], B0[1], B0[2], B0[3] }, new[] { mapa(0f, 0f), mapa(0f, 1f), mapa(1f, 1f), mapa(1f, 0f) });
        }

        /// Bloque recto de madera con mapeado de densidad uniforme: la veta corre a lo largo de U sin repetirse y cada cara usa su propia franja de la textura
        /// (k = UV por unidad del hueso, igual en las dos direcciones: sin estiramientos). org = dónde empieza en la textura de madera.
        void CajaMadera(List<Vector3> V, List<Vector2> U, List<Vector3> N, List<int> tri, Vector3 a, Vector3 b, Vector3 arriba, float alto, float ancho, Vector2 org, float k)
        {
            Vector3 d = b - a; float len = d.magnitude; if (len < 1e-6f) return;
            Vector3 dn = d / len, lado = Vector3.Cross(arriba, dn); if (lado.sqrMagnitude < 1e-8f) lado = Vector3.Cross(Vector3.right, dn); lado = lado.normalized * ancho * 0.5f;
            Vector3 up = Vector3.Cross(dn, lado.normalized).normalized * alto * 0.5f;
            Vector3[] A = { a - lado - up, a + lado - up, a + lado + up, a - lado + up }, B = { b - lado - up, b + lado - up, b + lado + up, b - lado + up };
            Action<Vector3[], Vector2[]> cara = (pp, uu) =>
            {
                Vector3 n = Vector3.Cross(pp[1] - pp[0], pp[3] - pp[0]).normalized; int i = V.Count;
                for (int z = 0; z < 4; z++) { V.Add(pp[z]); U.Add(uu[z]); N.Add(n); }
                tri.Add(i); tri.Add(i + 1); tri.Add(i + 2); tri.Add(i); tri.Add(i + 2); tri.Add(i + 3);
            };
            float u0 = org.x, u1 = org.x + len * k, ca = ancho * k, cl = alto * k;
            cara(new[] { A[0], A[3], A[2], A[1] }, new[] { new Vector2(u0, org.y), new Vector2(u0, org.y + ca), new Vector2(u0 + cl, org.y + ca), new Vector2(u0 + cl, org.y) });
            for (int e = 0; e < 4; e++)
            {
                int f = (e + 1) % 4; float cw = (e % 2 == 0 ? ancho : alto) * k, v0 = org.y + 0.012f + e * 0.065f;
                cara(new[] { A[e], A[f], B[f], B[e] }, new[] { new Vector2(u0, v0), new Vector2(u0, v0 + cw), new Vector2(u1, v0 + cw), new Vector2(u1, v0) });
            }
            cara(new[] { B[0], B[1], B[2], B[3] }, new[] { new Vector2(u1 - cl, org.y), new Vector2(u1 - cl, org.y + ca), new Vector2(u1, org.y + ca), new Vector2(u1, org.y) });
        }

        /// Barra de sección superelíptica (|x/a|^n + |y/b|^n = 1: n = 2 círculo, n = 4 rectángulo de esquinas redondeadas) con NS lados, tapas planas y normales suaves.
        /// Los puntos del anillo salen a los mismos ángulos que la transición de la culata (para empalmar sin escalón). org/k: UV de la madera (veta a lo largo de U).
        void CajaAnillo(List<Vector3> V, List<Vector2> U, List<Vector3> N, List<int> tri, Vector3 a, Vector3 b, Vector3 ejeX, Vector3 ejeY, float ax, float ay, float n, int NS, Vector2 org, float k, bool tapas)
        {
            Vector3 d = b - a; float len = d.magnitude; if (len < 1e-6f) return;
            var pa = new Vector3[NS + 1]; var dir = new Vector3[NS + 1];
            float per = 0f;
            for (int i = 0; i <= NS; i++)
            {
                float ang = (i % NS + 0.5f) / NS * 2f * Mathf.PI - Mathf.PI, cx = Mathf.Cos(ang), sy = Mathf.Sin(ang), ex = 2f / n;
                float px = ax * Mathf.Sign(cx) * Mathf.Pow(Mathf.Abs(cx), ex), py = ay * Mathf.Sign(sy) * Mathf.Pow(Mathf.Abs(sy), ex);
                pa[i] = ejeX * px + ejeY * py; dir[i] = (ejeX * (px / (ax * ax)) + ejeY * (py / (ay * ay))).normalized;
                if (i > 0) per += (pa[i] - pa[i - 1]).magnitude;
            }
            float u0 = org.x, u1 = org.x + len * k, acum = 0f;
            int ini = V.Count;
            for (int i = 0; i <= NS; i++)
            {
                if (i > 0) acum += (pa[i] - pa[i - 1]).magnitude;
                float v = org.y + acum * k;
                V.Add(a + pa[i]); U.Add(new Vector2(u0, v)); N.Add(dir[i]);
                V.Add(b + pa[i]); U.Add(new Vector2(u1, v)); N.Add(dir[i]);
            }
            for (int i = 0; i < NS; i++)
            {
                int q = ini + i * 2;   // (a_i, b_i, a_i+1, b_i+1)
                Vector3 fn = Vector3.Cross(V[q + 1] - V[q], V[q + 2] - V[q]);
                if (Vector3.Dot(fn, dir[i] + dir[i + 1]) >= 0f) { tri.Add(q); tri.Add(q + 1); tri.Add(q + 2); tri.Add(q + 2); tri.Add(q + 1); tri.Add(q + 3); }
                else { tri.Add(q); tri.Add(q + 2); tri.Add(q + 1); tri.Add(q + 2); tri.Add(q + 3); tri.Add(q + 1); }
            }
            if (!tapas) return;
            for (int e = 0; e < 2; e++)
            {
                Vector3 c = e == 0 ? a : b, nn = e == 0 ? -d.normalized : d.normalized; int ci = V.Count;
                V.Add(c); U.Add(new Vector2(e == 0 ? u0 : u1, org.y)); N.Add(nn);
                int r0 = V.Count;
                for (int i = 0; i < NS; i++) { V.Add(c + pa[i]); U.Add(new Vector2(e == 0 ? u0 : u1, org.y + 0.02f)); N.Add(nn); }
                for (int i = 0; i < NS; i++)
                {
                    int i2 = (i + 1) % NS;
                    Vector3 fn = Vector3.Cross(V[r0 + i] - V[ci], V[r0 + i2] - V[ci]);
                    if (Vector3.Dot(fn, nn) >= 0f) { tri.Add(ci); tri.Add(r0 + i); tri.Add(r0 + i2); } else { tri.Add(ci); tri.Add(r0 + i2); tri.Add(r0 + i); }
                }
            }
        }

        /// Pone el arco de acero (dos brazos curvados hacia atrás), la cuerda, el estribo y una flecha sobre el riel (cañón acortado del mosquete).
        void AgregarArco(List<Vector3> V, List<Vector2> U, List<Vector3> N, List<int>[] tris, List<float> Fr, Vector3 culata, Vector3 dir, float largo, int eje3, int eje2)
        {
            // de dónde sacar la textura: metal = la boca del cañón; madera = la culata
            int iMet = 0, iMad = 0; float mx = -9e9f, mn = 9e9f;
            for (int i = 0; i < Fr.Count; i++) { if (Fr[i] > mx) { mx = Fr[i]; iMet = i; } if (Fr[i] < mn) { mn = Fr[i]; iMad = i; } }
            Vector2 uvMet = U[iMet], uvMad = U[iMad];
            int smMet = 0, smMad = 0;
            for (int sm = 0; sm < tris.Length; sm++) for (int k = 0; k < tris[sm].Count; k++) { if (tris[sm][k] == iMet) smMet = sm; if (tris[sm][k] == iMad) smMad = sm; }
            // la boca (centro de los vértices más adelantados) y la altura del riel
            Vector3 boca = Vector3.zero; int nb = 0; float techo = -9e9f;
            for (int i = 0; i < V.Count; i++)
            {
                if (Fr[i] > mx - 0.03f) { boca += V[i]; nb++; }
                float al = Vector3.Dot(V[i], EjeAltoHueso);
                if (Fr[i] > 0.40f && Fr[i] < 0.6f) techo = Mathf.Max(techo, al);
            }
            if (nb > 0) boca /= nb;
            Vector3 D = dir.normalized, Up = EjeAltoHueso.normalized, L = Vector3.Cross(D, Up).normalized;
            float u = largo;   // unidad: el largo del mosquete en el espacio del hueso
            // se ancla al eje del riel (la boca) y a la altura de su techo
            Vector3 M = boca; M += Up * (techo - Vector3.Dot(M, Up)) * 0.5f;
            Vector3 centroRiel = M;
            var triM = tris[smMet]; var triA = tris[smMad];
            float gr = 0.018f * u;
            // PRUEBA: se quita lo que se ve del mosquete (cañón y mecanismo delante del corte) y en su lugar va un trozo de madera liso
            const float FCorteCulata = 0.17f;
            int quitadosCanon = 0; string modoPuas = "mejorar";
            try { string fp = System.IO.Path.Combine(BepInEx.Paths.BepInExRootPath, "coop-puas.txt"); if (System.IO.File.Exists(fp)) modoPuas = System.IO.File.ReadAllText(fp).Trim(); } catch (Exception) { }
            plugin.Log(Etiqueta + ": modo de puntas de la culata = " + modoPuas);
            for (int sm = 0; sm < tris.Length; sm++)
            {
                var t = tris[sm]; var quedan = new List<int>(t.Count);
                for (int k = 0; k < t.Count; k += 3)
                {
                    float fc = (Fr[t[k]] + Fr[t[k + 1]] + Fr[t[k + 2]]) / 3f;
                    if (fc > CorteCanon - 0.02f) { quitadosCanon++; continue; }
                    if (ConArco && Mathf.Max(Fr[t[k]], Mathf.Max(Fr[t[k + 1]], Fr[t[k + 2]])) > FCorteCulata + 0.025f) { quitadosCanon++; continue; }   // la zona de la llave (agujeros, puntas) se sustituye por un tramo liso
                    if (modoPuas != "ninguno")
                    {
                        float fmax = Mathf.Max(Fr[t[k]], Mathf.Max(Fr[t[k + 1]], Fr[t[k + 2]]));
                        if (modoPuas == "quitar" && fmax > 0.285f) { quitadosCanon++; continue; }
                        if (modoPuas == "mejorar" && fc > 0.15f)
                        {
                            // puntas: triángulos largos y finos del extremo de la culata (aristas de más de ~4 % del largo del arma)
                            float e1 = (V[t[k]] - V[t[k + 1]]).magnitude, e2 = (V[t[k + 1]] - V[t[k + 2]]).magnitude, e3 = (V[t[k + 2]] - V[t[k]]).magnitude;
                            float emax = Mathf.Max(e1, Mathf.Max(e2, e3)), emin = Mathf.Min(e1, Mathf.Min(e2, e3));
                            if (emax > 0.04f * largo && emax > 5f * emin && fmax > 0.22f) { quitadosCanon++; continue; }
                        }
                    }
                    quedan.Add(t[k]); quedan.Add(t[k + 1]); quedan.Add(t[k + 2]);
                }
                tris[sm] = quedan;
            }
            triM = tris[smMet]; triA = tris[smMad];
            Vector3 iniRiel = culata + D * (largo * (CorteCanon - 0.06f)); iniRiel += Up * (Vector3.Dot(M, Up) - Vector3.Dot(iniRiel, Up));
            iniRiel += L * Vector3.Dot(M - iniRiel, L);
            // Parches reales de la textura del mosquete: madera (caña de la culata) y hierro (cañón). Se repiten en espejo en la textura propia.
            Vector3 Lat = Vector3.Cross(D, Up).normalized;
            Vector2 mLo = uvMad - new Vector2(0.01f, 0.01f), mHi = uvMad + new Vector2(0.01f, 0.01f), hLo = mLo, hHi = mHi; bool mVetaU = true, hVetaU = true;
            Func<Func<int, bool>, Vector2[]> buscar = filtro =>
            {
                var cand = new List<int>();
                for (int i = 0; i < V.Count; i++) if (filtro(i)) cand.Add(i);
                if (cand.Count < 8) return null;
                var us = new List<float>(); var vs = new List<float>();
                foreach (int i in cand) { us.Add(U[i].x); vs.Add(U[i].y); }
                us.Sort(); vs.Sort();
                Vector2 med = new Vector2(us[us.Count / 2], vs[vs.Count / 2]);
                var bu = new List<float>(); var bv = new List<float>(); var bi = new List<int>();
                foreach (int i in cand) if (Mathf.Abs(U[i].x - med.x) < 0.08f && Mathf.Abs(U[i].y - med.y) < 0.08f) { bu.Add(U[i].x); bv.Add(U[i].y); bi.Add(i); }
                if (bu.Count < 8) return null;
                float mu = 0f, mv = 0f, ms = 0f;
                foreach (int i in bi) { mu += U[i].x; mv += U[i].y; ms += Vector3.Dot(V[i] - culata, D); }
                mu /= bi.Count; mv /= bi.Count; ms /= bi.Count;
                float cuv = 0f, cvv = 0f;
                foreach (int i in bi) { float ds = Vector3.Dot(V[i] - culata, D) - ms; cuv += (U[i].x - mu) * ds; cvv += (U[i].y - mv) * ds; }
                bu.Sort(); bv.Sort();
                int i10 = (int)(bu.Count * 0.1f), i90 = Mathf.Min(bu.Count - 1, (int)(bu.Count * 0.9f));
                return new[] { new Vector2(bu[i10], bv[i10]), new Vector2(bu[i90], bv[i90]), new Vector2(Mathf.Abs(cuv) >= Mathf.Abs(cvv) ? 1f : 0f, 0f) };
            };
            var pm = buscar(i => Fr[i] > 0.03f && Fr[i] < 0.20f && Mathf.Abs(Vector3.Dot(N[i], Lat)) > 0.6f);
            if (pm != null) { mLo = pm[0]; mHi = pm[1]; mVetaU = pm[2].x > 0.5f; }
            plugin.Log(Etiqueta + ": parche de madera de la culata " + mLo.ToString("F3") + "–" + mHi.ToString("F3") + (pm == null ? " (por defecto)" : ""));
            // Un solo juego de texturas (color, relieve y metal/brillo) propio, sintetizado con la madera y el hierro REALES del mosquete.
            var matBase = matsOrig[smMad];
            var juego = TexturaBallesta.Generar(matBase.mainTexture, mLo, mHi, msg => plugin.Log(Etiqueta + ": " + msg));
            plugin.Log(Etiqueta + ": material del mosquete " + matBase.shader.name);
            float emisionExtra = 0.22f;
            Func<string, Material> material = nom =>
            {
                var mt = new Material(matBase) { name = nom }; mt.mainTexture = juego.Color; mt.color = Color.white;
                foreach (var nm in new[] { "_SpecGlossMap", "_OcclusionMap", "_ParallaxMap", "_DetailAlbedoMap", "_DetailNormalMap", "_DetailMask" })
                    if (mt.HasProperty(nm)) mt.SetTexture(nm, null);
                // relieve (normales) y metal/brillo propios: la madera tiene profundidad y brillo irregular, el hierro es metálico
                mt.SetTexture("_BumpMap", juego.Normal); if (mt.HasProperty("_BumpScale")) mt.SetFloat("_BumpScale", 1f); mt.EnableKeyword("_NORMALMAP");
                mt.SetTexture("_MetallicGlossMap", juego.MetalBrillo); mt.EnableKeyword("_METALLICGLOSSMAP");
                if (mt.HasProperty("_GlossMapScale")) mt.SetFloat("_GlossMapScale", 1f);
                if (mt.HasProperty("_Metallic")) mt.SetFloat("_Metallic", 1f);
                if (mt.HasProperty("_Glossiness")) mt.SetFloat("_Glossiness", 1f);
                if (mt.HasProperty("_OcclusionStrength")) mt.SetFloat("_OcclusionStrength", 0f);
                mt.DisableKeyword("_SPECGLOSSMAP"); mt.DisableKeyword("_DETAIL_MULX2"); mt.DisableKeyword("_PARALLAXMAP");
                // en primera persona la luz del jugador no llega a las partes lejanas (brazos, soporte): se les da un poco de luz propia con la misma textura
                if (mt.HasProperty("_EmissionMap"))
                {
                    mt.EnableKeyword("_EMISSION"); mt.SetTexture("_EmissionMap", juego.Color);
                    mt.SetColor("_EmissionColor", new Color(emisionExtra, emisionExtra, emisionExtra, 1f));
                    mt.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                }
                return mt;
            };
            materialesExtra = new[] { material("coop_ballesta_madera"), material("coop_ballesta_hierro"), material("coop_ballesta_mate") };
            submallasExtra = new[] { new List<int>(), new List<int>(), new List<int>() };
            float kUV = 1.5f / u;   // UV por unidad del hueso (la misma en todas las piezas de madera)
            var gV = V; var gU = U; var gN = N; var gS = submallasExtra;   // destino de la geometría (la malla fija, o una pieza móvil)
            CajaWDel cjW = (pa, pb, arr, al, an, org) => CajaMadera(gV, gU, gN, gS[0], pa, pb, arr, al, an, org, kUV);
            CajaDel cj = (pa, pb, arr, al, an, mt) =>
                CajaTextura(gV, gU, gN, gS[mt == 0 ? 0 : (mt == 1 ? 1 : 2)], pa, pb, arr, al, an, culata, D, 0.10f * u,
                            mt == 0 ? TexturaBallesta.MaderaLo : (mt == 1 ? TexturaBallesta.HierroLo : (mt == 2 ? TexturaBallesta.CuerdaLo : (mt == 3 ? TexturaBallesta.CanalLo : (mt == 5 ? TexturaBallesta.CinchaLo : TexturaBallesta.PlumaLo)))),
                            mt == 0 ? TexturaBallesta.MaderaHi : (mt == 1 ? TexturaBallesta.HierroHi : (mt == 2 ? TexturaBallesta.CuerdaHi : (mt == 3 ? TexturaBallesta.CanalHi : (mt == 5 ? TexturaBallesta.CinchaHi : TexturaBallesta.PlumaHi)))), true);
            const int MAD = 0, FE = 1, CUE = 2, CAN = 3, PLU = 4, CIN = 5;
            // ---- Fusión culata → ballesta: desde la sección limpia de la culata (antes de la llave) un tramo liso de madera, que pasa a forma cuadrada y se afina hasta el mango ----
            float fEnd = CorteCanon + 0.06f; bool fusion = false;
            {
                var srcO = TexturaBallesta.LeerOrigen(matsOrig[smMad].mainTexture);
                float fA = FCorteCulata;
                var cands = new List<int>();
                for (int i = 0; i < V.Count; i++) if (Fr[i] > fA - 0.035f && Fr[i] < fA + 0.03f) cands.Add(i);
                if (srcO != null && cands.Count > 20)
                {
                    var xs = new List<float>(); var ys = new List<float>();
                    foreach (int i in cands) { xs.Add(Vector3.Dot(V[i], L)); ys.Add(Vector3.Dot(V[i], Up)); }
                    xs.Sort(); ys.Sort();
                    float cL = (xs[(int)(xs.Count * 0.04f)] + xs[(int)(xs.Count * 0.96f)]) * 0.5f, cU = (ys[(int)(ys.Count * 0.04f)] + ys[(int)(ys.Count * 0.96f)]) * 0.5f;
                    const int NS = 28; var elegido = new int[NS]; var mejorR = new float[NS]; for (int k = 0; k < NS; k++) elegido[k] = -1;
                    foreach (int pasada in new[] { 1, 0 })
                    {
                        float fMin = fA - (pasada == 1 ? 0.012f : 0.035f);
                        foreach (int i in cands)
                        {
                            if (Fr[i] < fMin) continue;
                            float dx = Vector3.Dot(V[i], L) - cL, dy = Vector3.Dot(V[i], Up) - cU; float r = Mathf.Sqrt(dx * dx + dy * dy); if (r < 1e-5f) continue;
                            int sec = Mathf.Clamp((int)((Mathf.Atan2(dy, dx) + Mathf.PI) / (2f * Mathf.PI) * NS), 0, NS - 1);
                            if (elegido[sec] >= 0 && pasada == 0) continue;
                            if (!TexturaBallesta.EsMadera(srcO.GetPixelBilinear(U[i].x, U[i].y))) continue;
                            if (r > mejorR[sec]) { mejorR[sec] = r; elegido[sec] = i; }
                        }
                    }
                    int hay = 0; for (int k = 0; k < NS; k++) if (elegido[k] >= 0) hay++;
                    plugin.Log(Etiqueta + ": fusión: " + cands.Count + " vértices de la culata en el corte, " + hay + "/" + NS + " sectores con madera, centro (" + cL.ToString("F3") + ", " + cU.ToString("F3") + ")");
                    if (hay >= NS * 2 / 3)
                    {
                        // todo lo de arriba (mango, guías, canal, flecha, soporte, brazos) se alinea con el eje lateral de la culata (cL), no con el del antiguo cañón
                        centroRiel += L * (cL - Vector3.Dot(centroRiel, L));
                        Vector3 cc1 = culata + D * (largo * fEnd); cc1 += Up * (Vector3.Dot(M, Up) - Vector3.Dot(cc1, Up)); cc1 += L * (cL - Vector3.Dot(cc1, L));
                        float a1 = gr * 0.6f, b1 = gr * 0.6f;
                        var P0 = new Vector3[NS]; var uv0 = new Vector2[NS];
                        for (int k = 0; k < NS; k++)
                        {
                            if (elegido[k] >= 0) { P0[k] = V[elegido[k]]; uv0[k] = U[elegido[k]]; continue; }
                            int pa = k, qa = k, da = 0, dq = 0;
                            while (elegido[pa] < 0 && da < NS) { pa = (pa + NS - 1) % NS; da++; }
                            while (elegido[qa] < 0 && dq < NS) { qa = (qa + 1) % NS; dq++; }
                            float tt = da / (float)Mathf.Max(1, da + dq);
                            P0[k] = Vector3.Lerp(V[elegido[pa]], V[elegido[qa]], tt); uv0[k] = tt < 0.5f ? U[elegido[pa]] : U[elegido[qa]];
                        }
                        // centro y tamaño de la sección de partida, y sus tres formas: real, cuadrada (mismo tamaño) y la del mango
                        Vector3 cIni = Vector3.zero; foreach (var q in P0) cIni += q; cIni /= NS;
                        float mnL = 9e9f, mxL = -9e9f, mnU = 9e9f, mxU = -9e9f;
                        foreach (var q in P0) { float l = Vector3.Dot(q - cIni, L), uu = Vector3.Dot(q - cIni, Up); mnL = Mathf.Min(mnL, l); mxL = Mathf.Max(mxL, l); mnU = Mathf.Min(mnU, uu); mxU = Mathf.Max(mxU, uu); }
                        float a0 = (mxL - mnL) * 0.5f, b0 = (mxU - mnU) * 0.5f; Vector3 cSq = cIni + L * ((mxL + mnL) * 0.5f) + Up * ((mxU + mnU) * 0.5f);
                        var offReal = new Vector3[NS]; var offSq = new Vector3[NS]; var offRail = new Vector3[NS];
                        for (int k = 0; k < NS; k++)
                        {
                            float ang = (k + 0.5f) / NS * 2f * Mathf.PI - Mathf.PI, cx = Mathf.Cos(ang), sy = Mathf.Sin(ang);
                            float sx = Mathf.Sign(cx) * Mathf.Pow(Mathf.Abs(cx), 0.5f), sz = Mathf.Sign(sy) * Mathf.Pow(Mathf.Abs(sy), 0.5f);
                            offReal[k] = P0[k] - cIni; offSq[k] = (cSq - cIni) + L * (a0 * sx) + Up * (b0 * sz); offRail[k] = L * (a1 * sx) + Up * (b1 * sz);
                        }
                        Vector3 cEnd = cc1;
                        const int ST = 8;
                        var anillos = new Vector3[ST + 1][]; var normales = new Vector3[ST + 1][];
                        for (int j = 0; j <= ST; j++)
                        {
                            float t = j / (float)ST, e = Mathf.SmoothStep(0f, 1f, t), cen = Mathf.SmoothStep(0f, 1f, t);
                            Vector3 cj0 = Vector3.Lerp(cIni, cEnd, cen);
                            anillos[j] = new Vector3[NS]; normales[j] = new Vector3[NS];
                            for (int k = 0; k < NS; k++)
                            {
                                Vector3 off = t < 0.3f ? Vector3.Lerp(offReal[k], offSq[k], Mathf.SmoothStep(0f, 1f, t / 0.3f)) : Vector3.Lerp(offSq[k], offRail[k], Mathf.SmoothStep(0f, 1f, (t - 0.3f) / 0.7f));
                                anillos[j][k] = cj0 + off;
                                Vector3 rad = L * Vector3.Dot(off, L) + Up * Vector3.Dot(off, Up); normales[j][k] = rad.sqrMagnitude > 1e-12f ? rad.normalized : Up;
                            }
                        }
                        var pa2 = TexturaBallesta.ParcheA;
                        Func<int, float, Vector2> uvMad2 = (kk, g) =>
                        {
                            if (!pa2.Ok) return uv0[kk % NS];
                            float t = (kk % NS + 0.5f) / NS, cc = 1f - Mathf.Abs(2f * t - 1f);
                            return pa2.VetaU ? new Vector2(Mathf.Lerp(pa2.Lo.x, pa2.Hi.x, g), Mathf.Lerp(pa2.Lo.y, pa2.Hi.y, cc)) : new Vector2(Mathf.Lerp(pa2.Lo.x, pa2.Hi.x, cc), Mathf.Lerp(pa2.Lo.y, pa2.Hi.y, g));
                        };
                        for (int j = 0; j < ST; j++)
                            for (int k = 0; k < NS; k++)
                            {
                                int kk = (k + 1) % NS; int baseI = V.Count;
                                Vector3[] pp = { anillos[j][k], anillos[j][kk], anillos[j + 1][kk], anillos[j + 1][k] };
                                Vector3[] nn = { normales[j][k], normales[j][kk], normales[j + 1][kk], normales[j + 1][k] };
                                float g0 = j / (float)ST, g1 = (j + 1) / (float)ST;
                                Vector2[] uu = { uvMad2(k, g0), uvMad2(kk, g0), uvMad2(kk, g1), uvMad2(k, g1) };
                                for (int z = 0; z < 4; z++) { V.Add(pp[z]); U.Add(uu[z]); N.Add(nn[z]); Fr.Add(0.2f); }
                                Vector3 fn = Vector3.Cross(pp[1] - pp[0], pp[3] - pp[0]); bool fuera = Vector3.Dot(fn, nn[0] + nn[1] + nn[2] + nn[3]) >= 0f;
                                if (fuera) { triA.Add(baseI); triA.Add(baseI + 1); triA.Add(baseI + 2); triA.Add(baseI); triA.Add(baseI + 2); triA.Add(baseI + 3); }
                                else { triA.Add(baseI); triA.Add(baseI + 2); triA.Add(baseI + 1); triA.Add(baseI); triA.Add(baseI + 3); triA.Add(baseI + 2); }
                                // cara trasera (sin huecos en ningún ángulo de cámara)
                                int bk = V.Count;
                                for (int z = 0; z < 4; z++) { V.Add(pp[z]); U.Add(uu[z]); N.Add(-nn[z]); Fr.Add(0.2f); }
                                if (fuera) { triA.Add(bk); triA.Add(bk + 2); triA.Add(bk + 1); triA.Add(bk); triA.Add(bk + 3); triA.Add(bk + 2); }
                                else { triA.Add(bk); triA.Add(bk + 1); triA.Add(bk + 2); triA.Add(bk); triA.Add(bk + 2); triA.Add(bk + 3); }
                            }
                        iniRiel = cc1; fusion = true;
                        plugin.Log(Etiqueta + ": tramo liso de la culata (" + NS + " sectores × " + ST + " tramos) desde " + fA.ToString("F2") + " hasta " + fEnd.ToString("F2") + ", sección " + (a0 * 2f).ToString("F3") + "×" + (b0 * 2f).ToString("F3"));
                    }
                }
            }
            CajaAnillo(V, U, N, submallasExtra[0], iniRiel, centroRiel + D * 0.02f * u, L, Up, gr * 0.6f, gr * 0.6f, 4f, 28, new Vector2(0.02f, 0.02f), kUV, true);   // mango: solo las esquinas redondeadas
            plugin.Log(Etiqueta + ": cañón del mosquete quitado (" + quitadosCanon + " triángulos), bloque de madera puesto");
            // ---- Ballesta nueva: soporte de hierro, dos brazos de madera en V (60° y 120°), cuerda, guías del riel y flecha ----
            Vector3 nuez = culata + D * (largo * (CorteCanon + 0.10f)); nuez += Up * (Vector3.Dot(centroRiel, Up) - Vector3.Dot(nuez, Up)); nuez += L * (Vector3.Dot(centroRiel, L) - Vector3.Dot(nuez, L));
            Vector3 sop = centroRiel - D * 0.005f * u;
            // (soporte de hierro y cinchas quitados a pedido del usuario; los brazos conservan su pivote en el mismo punto)
            if (false) cj(sop - D * 0.0115f * u, sop + D * 0.0115f * u, Up, 0.025f * u, 0.037f * u, FE);                                    // soporte de hierro
            for (int c = 0; c < 0; c++)                                                                                          // cinchas de cuerda (quitadas)
            {
                Vector3 pc = sop - D * 0.0105f * u + D * (c * 0.0105f * u);
                cj(pc - D * 0.0016f * u, pc + D * 0.0016f * u, Up, 0.029f * u, 0.041f * u, CIN);
            }
            Din = new DinBallesta { L = L, Up = Up, D = D, Nuez = nuez, U = u, Frente = sop + D * 0.020f * u + Up * 0.006f * u };
            for (int s = -1; s <= 1; s += 2)
            {
                int ib = s > 0 ? 0 : 1;
                // en el plano lateral/arriba: derecho (s = +1) a 60° y izquierdo (s = -1) a 120° desde la horizontal derecha
                Vector3 dirB = (L * (s * 0.5f) + Up * 0.8660254f).normalized;
                dirB = (dirB * 0.9848f - D * 0.1736f).normalized;   // se inclinan 10° hacia atrás (hacia el jugador), como en el render
                Vector3 baseB = sop + Up * 0.011f * u + L * (s * 0.0115f * u);
                float largoB = 0.125f * u; Vector3 tip = baseB + dirB * largoB;
                var gb = new GeomDin(); gV = gb.V; gU = gb.U; gN = gb.N; gS = gb.S;   // el brazo es una pieza aparte que gira en su base
                var orgB = s > 0 ? new Vector2(0.04f, 0.04f) : new Vector2(0.30f, 0.35f);
                cjW(baseB, baseB + dirB * (largoB * 0.6f), D, 0.020f * u, 0.020f * u, orgB);
                cjW(baseB + dirB * (largoB * 0.6f), tip, D, 0.016f * u, 0.016f * u, orgB + new Vector2(largoB * 0.6f * kUV, 0.004f));
                foreach (float kb in new[] { 0.12f, 0.45f, 0.80f })                                                              // bandas de hierro
                {
                    Vector3 cb = baseB + dirB * (largoB * kb);
                    cj(cb - dirB * 0.005f * u, cb + dirB * 0.005f * u, D, 0.025f * u, 0.025f * u, FE);
                }
                cj(tip, tip + dirB * 0.012f * u, D, 0.022f * u, 0.022f * u, FE);                                                  // remate de hierro
                Din.MallaBrazo[ib] = MallaDin(gb, baseB, "coop_brazo" + ib); Din.Base[ib] = baseB; Din.PuntaOff[ib] = tip - baseB;
                gV = V; gU = U; gN = N; gS = submallasExtra;
            }
            {
                // cuerda: una barra de longitud 1 a lo largo de +Z que se orienta y se estira cada cuadro entre la punta del brazo y la nuez
                var gc = new GeomDin(); gV = gc.V; gU = gc.U; gN = gc.N; gS = gc.S;
                cj(Vector3.zero, new Vector3(0f, 0f, 1f), Vector3.up, gr * 0.16f, gr * 0.16f, CUE);
                Din.MallaCuerda = MallaDin(gc, Vector3.zero, "coop_cuerda");
                gV = V; gU = U; gN = N; gS = submallasExtra;
            }
            // estribo delante: un aro de hierro
            Vector3 e0 = centroRiel + D * 0.02f * u;
            // (estribo quitado a pedido del usuario)
            // guías del riel (dejan un canal en el medio) y flecha más gruesa apoyada en él
            float cima = Vector3.Dot(centroRiel, Up) + gr * 0.6f;
            Vector3 cola = culata + D * (largo * (fusion ? fEnd + 0.02f : CorteCanon + 0.04f)); cola += Up * (cima + 0.004f * u - Vector3.Dot(cola, Up)); cola += L * (Vector3.Dot(centroRiel, L) - Vector3.Dot(cola, L));
            Vector3 punta = centroRiel + D * 0.07f * u; punta += Up * (cima + 0.004f * u - Vector3.Dot(punta, Up));
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 g0 = cola + L * (s * 0.0085f * u) - Up * 0.004f * u, g1 = sop - D * 0.0115f * u + L * (s * 0.0085f * u); g1 += Up * (Vector3.Dot(g0, Up) - Vector3.Dot(g1, Up));
                cjW(g0, g1, Up, 0.009f * u, 0.006f * u, new Vector2(0.05f + 0.1f * (s + 1), 0.10f + 0.07f * (s + 1)));
            }
            Vector3 c0 = cola + Up * (-0.0035f * u), c1 = sop - D * 0.016f * u; c1 += Up * (Vector3.Dot(c0, Up) - Vector3.Dot(c1, Up));
            cj(c0, c1, Up, 0.0026f * u, 0.0105f * u, CAN);                                   // canal oscuro entre las guías
            var ga = new GeomDin(); gV = ga.V; gU = ga.U; gN = ga.N; gS = ga.S;   // la flecha es una pieza aparte (se oculta al disparar y entra al canal al recargar)
            // Flecha realista: asta de madera con la veta, muesca trasera, tres plumas inclinadas con su ligadura y punta de hierro con casquillo y hoja en rombo
            float rA = 0.0034f * u;
            Vector2 orgFl = new Vector2(0.06f, 0.24f);
            CajaAnillo(gV, gU, gN, gS[0], cola + D * 0.006f * u, punta + D * 0.002f * u, L, Up, rA, rA, 2f, 16, orgFl, kUV, true);                      // asta
            CajaAnillo(gV, gU, gN, gS[2], cola, cola + D * 0.006f * u, L, Up, rA * 1.45f, rA * 1.45f, 2f, 16, new Vector2(0.78f, 0.52f), 0.6f / u, true);   // muesca oscura
            CajaAnillo(gV, gU, gN, gS[2], cola + D * 0.0035f * u, cola + D * 0.0075f * u, L, Up, rA * 1.7f, rA * 1.7f, 2f, 16, new Vector2(0.78f, 0.52f), 0.6f / u, true);   // ligadura bajo las plumas
            for (int k = 0; k < 3; k++)
            {
                float an = (k * 120f + 90f) * Mathf.Deg2Rad; Vector3 r = Up * Mathf.Cos(an) + L * Mathf.Sin(an);
                Vector3 e = cola + r * (rA + 0.0035f * u);
                // perfil suave de la pluma: sube rápido desde la muesca, se mantiene y baja al final (8 tramos)
                const int NP = 8; float x0 = 0.009f, x1 = 0.058f;
                for (int q = 0; q < NP; q++)
                {
                    float ta = q / (float)NP, tb2 = (q + 1) / (float)NP, tm = (ta + tb2) * 0.5f;
                    float h = 0.0112f * Mathf.Pow(Mathf.Sin(Mathf.Clamp01(tm) * Mathf.PI), 0.55f) + 0.0016f;
                    cj(e + D * (Mathf.Lerp(x0, x1, ta) * u), e + D * (Mathf.Lerp(x0, x1, tb2) * u), r, h * u, 0.0013f * u, PLU);
                }
            }
            // punta: casquillo de hierro y hoja en rombo (pirámide aplanada)
            Vector3 pcp = punta + D * 0.002f * u;
            CajaAnillo(gV, gU, gN, gS[1], pcp, pcp + D * 0.014f * u, L, Up, rA * 1.35f, rA * 1.35f, 2f, 16, new Vector2(0.80f, 0.12f), 0.6f / u, true);
            {
                Vector3 bc = pcp + D * 0.014f * u, ap = pcp + D * 0.058f * u; float wL = 0.0085f * u, wU = 0.0022f * u;
                Vector3[] bs = { bc + L * wL, bc + Up * wU, bc - L * wL, bc - Up * wU };
                Vector2 uvH = new Vector2(0.88f, 0.30f);
                for (int q = 0; q < 4; q++)
                {
                    Vector3 p1 = bs[q], p2 = bs[(q + 1) % 4]; Vector3 nn2 = Vector3.Cross(p1 - ap, p2 - ap).normalized;
                    if (Vector3.Dot(nn2, (p1 + p2) * 0.5f - (bc + ap) * 0.5f) < 0f) { var t = p1; p1 = p2; p2 = t; nn2 = -nn2; }
                    int i0 = gV.Count; gV.Add(ap); gV.Add(p1); gV.Add(p2); for (int z = 0; z < 3; z++) { gU.Add(uvH); gN.Add(nn2); }
                    gS[1].Add(i0); gS[1].Add(i0 + 1); gS[1].Add(i0 + 2);
                }
            }
            Din.MallaFlecha = MallaDin(ga, Vector3.zero, "coop_flecha_modelo");
            gV = V; gU = U; gN = N; gS = submallasExtra;
            plugin.Log(Etiqueta + ": arco agregado (boca " + boca.ToString("F3") + ", techo " + techo.ToString("F3") + ", submallas metal " + smMet + " madera " + smMad + ")");
        }

        /// Una de las dos partes de la malla: los triángulos cuyo largo medio está (o no) más allá de la bisagra.
        Mesh ParteMalla(List<Vector3> V, List<Vector2> U, List<Vector3> N, List<float> Fr, List<int>[] tris, int subs, bool delante, Vector3 origen, string nombre)
        {
            var nv = new List<Vector3>(); var nu = new List<Vector2>(); var nn = new List<Vector3>();
            var mapa = new Dictionary<int, int>(); var nt = new List<int>[subs];
            for (int sm = 0; sm < subs; sm++)
            {
                nt[sm] = new List<int>();
                var t = tris[sm];
                for (int k = 0; k < t.Count; k += 3)
                {
                    float f = (Fr[t[k]] + Fr[t[k + 1]] + Fr[t[k + 2]]) / 3f;
                    if ((f >= PosBisagra) != delante) continue;
                    for (int z = 0; z < 3; z++)
                    {
                        int vi = t[k + z]; int idx;
                        if (!mapa.TryGetValue(vi, out idx)) { idx = nv.Count; mapa[vi] = idx; nv.Add(V[vi] - origen); nu.Add(U[vi]); nn.Add(N[vi]); }
                        nt[sm].Add(idx);
                    }
                }
            }
            var m = new Mesh { name = nombre };
            m.SetVertices(nv); m.SetUVs(0, nu); m.SetNormals(nn);
            m.subMeshCount = subs;
            for (int sm = 0; sm < subs; sm++) m.SetTriangles(nt[sm].ToArray(), sm);
            m.RecalculateBounds();
            return m;
        }

        public void Colocar()
        {
            if (Modelo == null) return;
            Modelo.transform.localPosition = basePos + AjustePos;
            Modelo.transform.localRotation = baseRot * Quaternion.Euler(AjusteRot);
            Modelo.transform.localScale = Vector3.one * baseEscala * AjusteEscala;
        }

        /// Para ajustar a ojo: fotos del modelo con los brazos, de costado y desde arriba.
        public void Fotos(string prefijo)
        {
            if (Modelo == null || Camera.main == null) return;
            var cam = Camera.main.transform;
            Vector3 c = Modelo.GetComponent<Renderer>().bounds.center;
            string dir = System.IO.Path.Combine(BepInEx.Paths.BepInExRootPath, "capturas");
            Catalogo.Fotografiar(c + cam.right * 1.7f + cam.up * 0.15f, c, System.IO.Path.Combine(dir, prefijo + "_lado.png"), 800, 450, true);
            Catalogo.Fotografiar(c + cam.up * 1.7f - cam.forward * 0.05f, c, System.IO.Path.Combine(dir, prefijo + "_arriba.png"), 800, 450, true);
            Catalogo.Fotografiar(c - cam.right * 1.7f + cam.up * 0.15f, c, System.IO.Path.Combine(dir, prefijo + "_otro_lado.png"), 800, 450, true);
            Catalogo.Fotografiar(c + cam.forward * 1.7f + cam.up * 0.1f, c, System.IO.Path.Combine(dir, prefijo + "_frente.png"), 800, 450, true);
        }

        public string Describir()
        {
            var w = Arma != null ? Arma.GetComponent<WeaponBehavior>() : null;
            return Etiqueta + "=" + (Arma != null) + " tiene=" + Tiene + (w != null ? " daño=" + w.damage + " proyectiles=" + w.projectileCount + " alcance=" + w.range + " recarga=" + w.reloadTime + " cargador=" + w.bulletsLeft + " activa=" + Arma.activeSelf : "") + " ajuste pos=" + AjustePos + " rot=" + AjusteRot + " escala=" + AjusteEscala;
        }

        /// ¿Es el arma que el jugador tiene en la mano?
        public bool EnLaMano(PlayerWeapons pw)
        {
            return Arma != null && pw != null && pw.weaponOrder != null && pw.currentWeapon >= 0 && pw.currentWeapon < pw.weaponOrder.Length && pw.weaponOrder[pw.currentWeapon] == Arma;
        }
    }
}

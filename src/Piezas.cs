// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using System.Collections.Generic;
using UnityEngine;

namespace WitchHuntCoop.Coop
{
    /// Las estructuras del bosque están horneadas (static batching): su malla visible es un trozo
    /// de una malla combinada ilegible y no se pueden mover ni clonar tal cual. Pero muchas mallas
    /// originales siguen en memoria (las usan las colisiones o los niveles de detalle): con ellas
    /// se reconstruyen copias que sí se pueden colocar en otro sitio.
    public static class Piezas
    {
        /// Malla original de un renderer: la suya si no está horneado; si no, una malla suelta
        /// con su nombre (o la de su colisión, o la variante LOD más cercana).
        public static Mesh MallaDe(MeshRenderer r, Dictionary<string, Mesh> sueltas)
        {
            var mf = r.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null && !r.isPartOfStaticBatch) return mf.sharedMesh;
            var mc = r.GetComponent<MeshCollider>();
            Mesh m;
            string n = r.name;
            int i = n.IndexOf(" ("); if (i > 0) n = n.Substring(0, i);
            if (sueltas.TryGetValue(n, out m)) return m;
            if (Todas().TryGetValue(n, out m)) return m;   // no legible: no sirve para medir, pero sí para dibujar
            if (mc != null && mc.sharedMesh != null && mc.sharedMesh.isReadable && !mc.sharedMesh.name.StartsWith("Combined")) return mc.sharedMesh;
            // cabinBase_01_LOD0 → cabinBase_01_LOD1 / LOD2
            int lod = n.LastIndexOf("_LOD");
            if (lod > 0)
                for (int k = 0; k <= 3; k++)
                    if (sueltas.TryGetValue(n.Substring(0, lod) + "_LOD" + k, out m)) return m;
            return null;
        }

        /// Cuántas piezas horneadas de una estructura se pueden rearmar bien: con una malla suelta que
        /// tenga coordenadas de textura (las de colisión simplificadas no tienen y salen blancas) y
        /// que ocupe lo mismo que la pieza dibujada.
        public static void Calidad(GameObject original, out int horneadas, out int buenas)
        {
            horneadas = buenas = 0;
            var s = Sueltas();
            foreach (var r in original.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (!r.isPartOfStaticBatch) continue;
                horneadas++;
                var m = MallaDe(r, s);
                if (m == null || m.uv == null || m.uv.Length == 0) continue;
                // Caja de la malla llevada al mundo, comparada con la de la pieza dibujada.
                var b = m.bounds; var M = r.transform.localToWorldMatrix;
                var w = new Bounds(M.MultiplyPoint3x4(b.min), Vector3.zero);
                for (int k = 0; k < 8; k++)
                    w.Encapsulate(M.MultiplyPoint3x4(new Vector3((k & 1) == 0 ? b.min.x : b.max.x, (k & 2) == 0 ? b.min.y : b.max.y, (k & 4) == 0 ? b.min.z : b.max.z)));
                float t = Mathf.Max(r.bounds.size.magnitude, 0.2f);
                if (Vector3.Distance(w.center, r.bounds.center) > 0.15f * t) continue;
                if (Mathf.Abs(w.size.magnitude - r.bounds.size.magnitude) > 0.2f * t) continue;
                buenas++;
            }
        }

        static Dictionary<string, Mesh> sueltas;

        public static Dictionary<string, Mesh> Sueltas()
        {
            if (sueltas != null) return sueltas;
            sueltas = new Dictionary<string, Mesh>();
            foreach (var mm in Resources.FindObjectsOfTypeAll<Mesh>())
                if (mm.isReadable && !mm.name.StartsWith("Combined Mesh") && !sueltas.ContainsKey(mm.name)) sueltas[mm.name] = mm;
            return sueltas;
        }

        static Dictionary<string, Mesh> todas;

        /// Todas las mallas sueltas en memoria, también las no legibles (Unity las dibuja igual).
        public static Dictionary<string, Mesh> Todas()
        {
            if (todas != null) return todas;
            todas = new Dictionary<string, Mesh>();
            foreach (var mm in Resources.FindObjectsOfTypeAll<Mesh>())
                if (mm != null && !mm.name.StartsWith("Combined Mesh") && mm.name.Length > 0 && !todas.ContainsKey(mm.name)) todas[mm.name] = mm;
            return todas;
        }

        public static void Olvidar() { sueltas = null; todas = null; }

        /// Copia de una estructura que sí se puede colocar: Instantiate y, en cada pieza horneada,
        /// la malla combinada se cambia por la original suelta (o se apaga si no existe).
        /// Devuelve la copia y cuántas piezas faltaron.
        public static GameObject Clonar(GameObject original, Vector3 pos, Quaternion rot, out int faltan)
        {
            faltan = 0;
            var s = Sueltas();
            var copia = (GameObject)Object.Instantiate(original, pos, rot);
            var orig = original.GetComponentsInChildren<MeshRenderer>(true);
            var nuevos = copia.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < orig.Length && i < nuevos.Length; i++)
            {
                if (!orig[i].isPartOfStaticBatch) continue;
                var m = MallaDe(orig[i], s);
                var mf = nuevos[i].GetComponent<MeshFilter>();
                if (m == null || mf == null) { nuevos[i].enabled = false; faltan++; continue; }
                mf.sharedMesh = m;
            }
            // Sin comportamiento del juego (persistencia, eventos de historia): solo forma y colisión.
            foreach (var mb in copia.GetComponentsInChildren<MonoBehaviour>(true)) Object.Destroy(mb);
            return copia;
        }
    }
}

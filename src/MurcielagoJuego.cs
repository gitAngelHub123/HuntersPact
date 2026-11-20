// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace WitchHuntCoop
{
    /// Jefe opcional de las minas con el modelo del propio juego: la criatura voladora «Night_Flyer»,
    /// que en el juego solo se ve de cerca al derribarla (logro «Cielo limpio»). El juego solo trae su
    /// cadáver (prefab Night_Flyer_Ragdoll2: malla con piel GIANT_BAT y esqueleto de 3ds Max "GIANT_BAT_ …").
    /// Se clona, se le quita la física de muñeco de trapo y el mod anima los huesos.
    public static class MurcielagoJuego
    {
        public const string Prefab = "Night_Flyer_Ragdoll2";

        public static GameObject BuscarPrefab()
        {
            foreach (var g in Resources.FindObjectsOfTypeAll<GameObject>())
                if (g.name == Prefab && g.transform.parent == null && !g.scene.IsValid()) return g;
            foreach (var g in Resources.FindObjectsOfTypeAll<GameObject>())
                if (g.name == Prefab && g.transform.parent == null) return g;
            return null;
        }

        /// Copia del cadáver sin física (sin Rigidbody, articulaciones, colisores ni scripts del juego).
        public static GameObject Clonar(Vector3 pos, Quaternion rot)
        {
            var pre = BuscarPrefab();
            if (pre == null) return null;
            var go = Object.Instantiate(pre, pos, rot);
            go.name = "coop_jefe_nightflyer";
            foreach (var j in go.GetComponentsInChildren<Joint>(true)) Object.DestroyImmediate(j);
            foreach (var r in go.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(r);
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true)) if (mb != null) Object.DestroyImmediate(mb);
            foreach (var a in go.GetComponentsInChildren<Animator>(true)) a.enabled = false;
            foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true)) smr.updateWhenOffscreen = true;
            go.SetActive(true);
            return go;
        }

        static GameObject puesto;

        /// Orden de prueba: una copia delante de la cámara, apoyada en el suelo, a la escala pedida; devuelve sus medidas.
        public static string Poner(Camera cam, float escala, float dist)
        {
            if (puesto != null) Object.Destroy(puesto);
            Vector3 f = cam.transform.forward; f.y = 0f; f.Normalize();
            Vector3 p = cam.transform.position + f * dist;
            RaycastHit h;
            if (Physics.Raycast(p + Vector3.up * 3f, Vector3.down, out h, 30f, ~0, QueryTriggerInteraction.Ignore)) p = h.point;
            var go = Clonar(p, Quaternion.LookRotation(-f, Vector3.up));
            if (go == null) return "no encontré " + Prefab;
            go.transform.localScale = go.transform.localScale * escala;
            puesto = go;
            var b = Medir(go);
            // apoyarlo: que lo más bajo quede en el suelo
            go.transform.position += Vector3.up * (p.y - b.min.y);
            b = Medir(go);
            return "Night_Flyer ×" + escala + " en " + go.transform.position.ToString("F1") + " medidas " + b.size.ToString("F1") + " m (ancho × alto × largo)";
        }

        public static Bounds Medir(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); return b;
        }

        /// Informe de la jerarquía del prefab: nombre, componentes, rotación local y posición local.
        public static string Jerarquia()
        {
            var pre = BuscarPrefab();
            if (pre == null) return "no encontré " + Prefab;
            var sb = new StringBuilder();
            sb.Append("escala=" + pre.transform.localScale.ToString("F2"));
            foreach (var smr in pre.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var b = smr.sharedMesh != null ? smr.sharedMesh.bounds : new Bounds();
                sb.Append("\n  PIEL " + smr.name + " malla=" + (smr.sharedMesh != null ? smr.sharedMesh.name : "-") + " huesos=" + smr.bones.Length +
                          " raíz=" + (smr.rootBone != null ? smr.rootBone.name : "-") + " límites=" + b.center.ToString("F2") + "±" + b.extents.ToString("F2"));
            }
            Volcar(pre.transform, 0, sb);
            return sb.ToString();
        }

        static void Volcar(Transform t, int nivel, StringBuilder sb)
        {
            sb.Append("\n").Append(' ', nivel * 2).Append(t.name)
              .Append(" p=").Append(t.localPosition.ToString("F3"))
              .Append(" r=").Append(t.localEulerAngles.ToString("F0"));
            var comps = t.GetComponents<Component>();
            foreach (var c in comps) if (c != null && !(c is Transform)) sb.Append(" [").Append(c.GetType().Name).Append("]");
            for (int i = 0; i < t.childCount; i++) Volcar(t.GetChild(i), nivel + 1, sb);
        }
    }
}

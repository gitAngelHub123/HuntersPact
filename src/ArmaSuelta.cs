// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using System.Collections.Generic;
using UnityEngine;

namespace WitchHuntCoop
{
    /// Arma suelta en el mundo con el MISMO modelo que la del jugador (escopeta de las casas, ballesta de las minas),
    /// apoyada DERECHA en el suelo (pedido 2026-11-17): con la misma orientación que tiene en la mano respecto a la
    /// cámara (arriba hacia arriba, apuntando hacia +X del sitio), no de costado, y más grande para que se vea.
    /// combinar = true: las mallas se combinan tal como se ven en la mano (la escopeta: copiar su jerarquía la dejaba
    /// aplastada). combinar = false: se copia la jerarquía (la ballesta: así se veía bien).
    public static class ArmaSuelta
    {
        public static bool Poner(GameObject destino, GameObject modelo, float largo, bool combinar)
        {
            if (destino == null || modelo == null || destino.transform.Find("coop_arma_nueva") != null) return false;
            var cam = Camera.main; if (cam == null) return false;
            foreach (Transform t in destino.transform) if (t.name == "malla" || t.name == "malla_nueva") t.gameObject.SetActive(false);   // el modelo viejo (diminuto)
            Quaternion haciaX = Quaternion.Euler(0f, 90f, 0f);   // lo que mira hacia adelante en la mano (z) queda a lo largo de +X
            GameObject c;
            if (combinar)
            {
                var piezas = new List<CombineInstance>(); var mats = new List<Material>();
                Matrix4x4 refCam = Matrix4x4.TRS(cam.transform.position, cam.transform.rotation, Vector3.one).inverse;
                foreach (var mf in modelo.GetComponentsInChildren<MeshFilter>(true))
                {
                    var mr = mf.GetComponent<MeshRenderer>(); if (mr == null || mf.sharedMesh == null) continue;
                    var m = mf.sharedMesh;
                    for (int s = 0; s < m.subMeshCount; s++)
                    {
                        piezas.Add(new CombineInstance { mesh = m, subMeshIndex = s, transform = refCam * mf.transform.localToWorldMatrix });
                        var ms = mr.sharedMaterials; mats.Add(ms.Length > s ? ms[s] : (ms.Length > 0 ? ms[0] : null));
                    }
                }
                if (piezas.Count == 0) return false;
                var malla = new Mesh { name = "coop_arma_suelta" };
                malla.CombineMeshes(piezas.ToArray(), false, true); malla.RecalculateBounds();
                c = new GameObject("coop_arma_nueva");
                c.AddComponent<MeshFilter>().sharedMesh = malla;
                c.AddComponent<MeshRenderer>().sharedMaterials = mats.ToArray();
                c.transform.SetParent(destino.transform, false);
                c.transform.localRotation = haciaX; c.transform.localScale = Vector3.one; c.transform.localPosition = Vector3.zero;
            }
            else
            {
                c = (GameObject)Object.Instantiate(modelo);
                c.name = "coop_arma_nueva";
                foreach (var mb in c.GetComponentsInChildren<MonoBehaviour>(true)) Object.Destroy(mb);
                foreach (var col in c.GetComponentsInChildren<Collider>(true)) Object.Destroy(col);
                foreach (var t in c.GetComponentsInChildren<Transform>(true)) { t.gameObject.layer = 0; t.gameObject.SetActive(true); }
                c.transform.SetParent(destino.transform, false);
                c.transform.localScale = modelo.transform.lossyScale;
                c.transform.localRotation = haciaX * (Quaternion.Inverse(cam.transform.rotation) * modelo.transform.rotation);
                c.transform.localPosition = Vector3.zero;
            }
            foreach (var t in c.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 0;
            // tamaño: lo largo (en el eje X del sitio) pasa a 'largo'; luego se centra y se apoya en el suelo
            Bounds b; if (!Caja(c, out b)) { Object.Destroy(c); return false; }
            Vector3 eX = destino.transform.right;
            float actual = Mathf.Abs(Vector3.Dot(b.size, new Vector3(Mathf.Abs(eX.x), Mathf.Abs(eX.y), Mathf.Abs(eX.z))));
            actual = Mathf.Max(actual, Mathf.Max(b.size.x, b.size.z) * 0.5f);
            float k = largo / Mathf.Max(actual, 0.0001f);
            c.transform.localScale *= k;
            Caja(c, out b);
            Vector3 suelo = destino.transform.position;
            // colgada en la pared (casa de la escopeta, 2026-11-18): se queda centrada donde está, sin bajar al piso
            if (destino.name.Contains("_pared")) { c.transform.position += suelo - b.center; return true; }
            // apoyada en el piso de verdad (el sitio de la escopeta está 20 cm sobre el piso)
            float piso = float.NegativeInfinity;   // la superficie MÁS ALTA debajo (el piso de la cabaña, no el terreno de abajo)
            foreach (var h in Physics.RaycastAll(suelo + Vector3.up * 0.4f, Vector3.down, 2f, ~0, QueryTriggerInteraction.Ignore))
                if (!h.collider.transform.IsChildOf(destino.transform) && h.collider.GetComponentInParent<Animator>() == null && h.point.y > piso) piso = h.point.y;
            if (!float.IsNegativeInfinity(piso)) suelo.y = piso;
            c.transform.position += new Vector3(suelo.x - b.center.x, suelo.y - b.min.y + 0.01f, suelo.z - b.center.z);
            return true;
        }

        static bool Caja(GameObject c, out Bounds b)
        {
            b = new Bounds(); bool hay = false;
            foreach (var r in c.GetComponentsInChildren<Renderer>(true)) { if (r is ParticleSystemRenderer) continue; if (!hay) { b = r.bounds; hay = true; } else b.Encapsulate(r.bounds); }
            return hay;
        }
    }
}

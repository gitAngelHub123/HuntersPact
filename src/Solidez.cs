// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using System.Collections.Generic;
using UnityEngine;

namespace WitchHuntCoop
{
    /// Todo lo grande que pone el mod tiene que poder tocarse: lo que se ve pero se atraviesa se vuelve sólido (malla de colisión de
    /// su propia forma) y, si no se puede (la malla no se puede leer), se quita. Pedido 2026-11-11: "si algo no es sólido, que no lo coloques".
    /// Marca: esta malla se atraviesa a propósito (luz, agua, efectos); HacerSolidos no le pone colisión.
    public class NoSolido : MonoBehaviour { }

    public partial class Mundo
    {
        public int HechosSolidos, QuitadosSinSolidez;

        void HacerSolidos()
        {
            HechosSolidos = QuitadosSinSolidez = 0;
            var quitar = new List<GameObject>();
            foreach (var r in Object.FindObjectsOfType<MeshRenderer>())
            {
                if (r == null || !r.enabled || r.isPartOfStaticBatch) continue;
                string raiz = r.transform.root.name;
                if (!raiz.StartsWith("coop_") || raiz.StartsWith("coop_luz") || raiz.StartsWith("coop_aura") || raiz.StartsWith("coop_lluvia") || raiz.StartsWith("coop_humo") || raiz.StartsWith("coop_mapa")) continue;
                if (r.bounds.size.magnitude < 2.5f) continue;
                if (r.GetComponentInParent<NoSolido>() != null) continue;   // atravesable a propósito (rayo de luz, charcos, grieta…)
                // ¿ya tiene colisión (ella, sus padres o sus hijos)?
                bool col = false;
                for (var t = r.transform; t != null && !col; t = t.parent) { var c0 = t.GetComponent<Collider>(); if (c0 != null && c0.enabled && !c0.isTrigger) col = true; }
                if (!col) foreach (var c1 in r.GetComponentsInChildren<Collider>()) if (c1.enabled && !c1.isTrigger) { col = true; break; }
                if (col) continue;
                var mf = r.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null && mf.sharedMesh.isReadable && mf.sharedMesh.vertexCount < 60000)
                {
                    var mc = r.gameObject.AddComponent<MeshCollider>(); mc.sharedMesh = mf.sharedMesh; HechosSolidos++;
                }
                else { quitar.Add(r.gameObject); }
            }
            foreach (var g in quitar) if (g != null) { g.SetActive(false); QuitadosSinSolidez++; }
            plugin.Log("Solidez: " + HechosSolidos + " piezas grandes del mod ahora son sólidas, " + QuitadosSinSolidez + " sin colisión posible se quitaron");
        }
    }
}

// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using System.Collections.Generic;
using UnityEngine;

namespace WitchHuntCoop
{
    /// Muebles del juego cuya malla ya no existe en memoria (el juego la horneó y Unity borró la original):
    /// se rehacen con piezas simples y el MATERIAL original del juego (ese sí sigue disponible).
    /// Medidas en metros reales; ColocarDecoracionCastillo los apoya en el suelo y les aplica la escala del castillo.
    public partial class Mundo
    {
        static readonly Dictionary<string, Material> matProp = new Dictionary<string, Material>();

        /// Material de cualquier objeto de la escena cuyo nombre base sea 'tipo' (o el de respaldo).
        Material MatJuego(string tipo, Material respaldo)
        {
            Material m;
            if (matProp.TryGetValue(tipo, out m) && m != null) return m;
            m = null;
            foreach (var g in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (g == null || !g.scene.IsValid() || Catalogo.BaseDe(g.name) != tipo) continue;
                foreach (var r in g.GetComponentsInChildren<MeshRenderer>(true))
                    if (r.sharedMaterial != null && r.sharedMaterial.mainTexture != null) { m = r.sharedMaterial; break; }
                if (m != null) break;
            }
            if (m == null) m = respaldo;
            matProp[tipo] = m;
            return m;
        }

        public Vector3 CentroCastilloMundo() { return castilloRaiz != null ? castilloRaiz.transform.position : Vector3.zero; }

        Material MatHierroProp() { return matHierroC ?? matHierro ?? MatJuego("Hammer_small", null); }
        Material MatMaderaProp() { return MatJuego("Shelf_wooden", matMadera2 ?? matMadera); }

        /// Pieza de un mueble hecho a mano: sin colisión, textura repetida una vez por cada 'tile' metros.
        GameObject Pp(Transform pa, PrimitiveType t, Vector3 c, Vector3 tam, Material m, float giroX = 0f, float giroY = 0f, float giroZ = 0f, float tile = 1f)
        {
            if (m == null) m = matPiedra;
            var g = Pz(pa, "coop_pieza", t, c, tam, 0f, m, false, tile);
            g.transform.localRotation = Quaternion.Euler(giroX, giroY, giroZ);
            return g;
        }

        /// Devuelve el mueble hecho a mano (en el origen, sin girar) o null si no hay receta para ese tipo.
        GameObject PropHecho(string tipo)
        {
            var raiz = new GameObject("coop_prophecho_" + tipo);
            var r = raiz.transform;
            bool hecho = true;
            switch (tipo)
            {
                case "CandleStand1":
                case "CandleStand1_nolight":
                case "CandleStand2_nolight":
                {
                    // candelabro de pie de hierro: base de tres patas, vara, platillo y 1 o 3 velas encendidas
                    var hi = MatHierroProp(); var cera = matVelaCem();
                    for (int i = 0; i < 3; i++) Pp(r, PrimitiveType.Cube, new Vector3(Mathf.Sin(i * 2.094f) * 0.13f, 0.03f, Mathf.Cos(i * 2.094f) * 0.13f), new Vector3(0.04f, 0.05f, 0.3f), hi, 0f, i * 120f);
                    Pp(r, PrimitiveType.Cylinder, new Vector3(0f, 0.75f, 0f), new Vector3(0.035f, 0.72f, 0.035f), hi);
                    Pp(r, PrimitiveType.Cylinder, new Vector3(0f, 1.48f, 0f), new Vector3(0.16f, 0.015f, 0.16f), hi);
                    var velas = tipo.StartsWith("CandleStand2") ? new[] { -0.19f, 0f, 0.19f } : new[] { 0f };
                    if (velas.Length > 1) Pp(r, PrimitiveType.Cube, new Vector3(0f, 1.5f, 0f), new Vector3(0.42f, 0.025f, 0.03f), hi);
                    foreach (var x in velas)
                    {
                        Pp(r, PrimitiveType.Cylinder, new Vector3(x, 1.52f, 0f), new Vector3(0.07f, 0.01f, 0.07f), hi);
                        Pp(r, PrimitiveType.Cylinder, new Vector3(x, 1.62f, 0f), new Vector3(0.045f, 0.1f, 0.045f), cera);
                        Llama.Crear(new Vector3(x, 1.75f, 0f), r, 0.12f, false, false);
                    }
                    break;
                }
                case "Shelf_wooden":
                {
                    // estantería de pared: dos laterales, tres baldas y fondo
                    var ma = MatMaderaProp();
                    foreach (var x in new[] { -0.58f, 0.58f }) Pp(r, PrimitiveType.Cube, new Vector3(x, 0.8f, 0f), new Vector3(0.04f, 1.6f, 0.34f), ma);
                    foreach (var y in new[] { 0.06f, 0.6f, 1.12f, 1.58f }) Pp(r, PrimitiveType.Cube, new Vector3(0f, y, 0f), new Vector3(1.2f, 0.035f, 0.34f), ma);
                    Pp(r, PrimitiveType.Cube, new Vector3(0f, 0.8f, 0.16f), new Vector3(1.16f, 1.6f, 0.015f), ma);
                    break;
                }
                case "Book_parrent":
                {
                    // libro cerrado: tapas con la textura del tomo y hojas claras
                    var tapa = MatJuego("Book_parrent", matTela);
                    Pp(r, PrimitiveType.Cube, new Vector3(0f, 0.035f, 0f), new Vector3(0.2f, 0.06f, 0.27f), tapa, 0f, 0f, 0f, 0.3f);
                    Pp(r, PrimitiveType.Cube, new Vector3(0.006f, 0.035f, 0f), new Vector3(0.195f, 0.045f, 0.26f), matVelaCem());
                    break;
                }
                case "MortarAndPestle01":
                {
                    var pi = MatJuego("MortarAndPestle01", matPiedra);
                    Pp(r, PrimitiveType.Cylinder, new Vector3(0f, 0.06f, 0f), new Vector3(0.16f, 0.06f, 0.16f), pi);
                    Pp(r, PrimitiveType.Cylinder, new Vector3(0f, 0.115f, 0f), new Vector3(0.12f, 0.005f, 0.12f), matNegroFosa());
                    Pp(r, PrimitiveType.Cylinder, new Vector3(0.03f, 0.17f, 0f), new Vector3(0.03f, 0.09f, 0.03f), pi, 0f, 0f, 25f);
                    break;
                }
                case "SK_BackPack":
                {
                    var tela = MatJuego("SK_BackPack", matTela);
                    Pp(r, PrimitiveType.Cube, new Vector3(0f, 0.25f, 0f), new Vector3(0.36f, 0.48f, 0.2f), tela, 0f, 0f, 0f, 0.5f);
                    Pp(r, PrimitiveType.Sphere, new Vector3(0f, 0.5f, 0f), new Vector3(0.36f, 0.12f, 0.2f), tela);
                    Pp(r, PrimitiveType.Cube, new Vector3(0f, 0.2f, -0.12f), new Vector3(0.28f, 0.22f, 0.06f), tela, 0f, 0f, 0f, 0.5f);
                    foreach (var x in new[] { -0.1f, 0.1f }) Pp(r, PrimitiveType.Cube, new Vector3(x, 0.28f, 0.11f), new Vector3(0.04f, 0.42f, 0.015f), matNegroFosa());
                    break;
                }
                case "smallFrame_a":
                {
                    // cuadro pequeño de pared: marco con la textura del juego y lienzo oscuro
                    var marco = MatJuego("smallFrame_a 1", MatMaderaProp());
                    Pp(r, PrimitiveType.Cube, new Vector3(0f, 0.25f, 0f), new Vector3(0.42f, 0.5f, 0.035f), marco, 0f, 0f, 0f, 0.5f);
                    Pp(r, PrimitiveType.Cube, new Vector3(0f, 0.25f, -0.012f), new Vector3(0.32f, 0.4f, 0.02f), MatJuego("Deer_Troffy", matTela), 0f, 0f, 0f, 0.4f);
                    break;
                }
                case "barrel01_pile":
                {
                    // tres barriles: dos acostados y uno encima
                    var ba = MatJuego("barrel01_LOD0", MatMaderaProp()); var hi = MatHierroProp();
                    foreach (var x in new[] { -0.33f, 0.33f })
                    {
                        Pp(r, PrimitiveType.Cylinder, new Vector3(x, 0.3f, 0f), new Vector3(0.6f, 0.45f, 0.6f), ba, 90f);
                        foreach (var z in new[] { -0.3f, 0.3f }) Pp(r, PrimitiveType.Cylinder, new Vector3(x, 0.3f, z), new Vector3(0.62f, 0.025f, 0.62f), hi, 90f);
                    }
                    Pp(r, PrimitiveType.Cylinder, new Vector3(0f, 0.85f, 0f), new Vector3(0.6f, 0.45f, 0.6f), ba, 90f);
                    break;
                }
                case "Basket_gua":
                {
                    var ce = MatJuego("Basket_gua", MatMaderaProp());
                    Pp(r, PrimitiveType.Cylinder, new Vector3(0f, 0.15f, 0f), new Vector3(0.42f, 0.15f, 0.42f), ce, 0f, 0f, 0f, 0.4f);
                    Pp(r, PrimitiveType.Cylinder, new Vector3(0f, 0.3f, 0f), new Vector3(0.46f, 0.02f, 0.46f), ce);
                    Pp(r, PrimitiveType.Cylinder, new Vector3(0f, 0.285f, 0f), new Vector3(0.38f, 0.005f, 0.38f), matNegroFosa());
                    break;
                }
                case "metal_pitcher01":
                {
                    var me = MatJuego("metal_pitcher01", MatHierroProp());
                    Pp(r, PrimitiveType.Cylinder, new Vector3(0f, 0.11f, 0f), new Vector3(0.13f, 0.11f, 0.13f), me);
                    Pp(r, PrimitiveType.Cylinder, new Vector3(0f, 0.25f, 0f), new Vector3(0.09f, 0.04f, 0.09f), me);
                    Pp(r, PrimitiveType.Cube, new Vector3(0.085f, 0.15f, 0f), new Vector3(0.015f, 0.14f, 0.02f), me);
                    break;
                }
                case "metal_bowl01":
                case "terracotta_plate01":
                {
                    var me = MatJuego(tipo == "metal_bowl01" ? "metal_bowl01" : "terracotta_pitcher01", MatJuego("metal_bowl01", MatHierroProp()));
                    bool cuenco = tipo == "metal_bowl01";
                    Pp(r, PrimitiveType.Cylinder, new Vector3(0f, cuenco ? 0.035f : 0.008f, 0f), new Vector3(cuenco ? 0.17f : 0.24f, cuenco ? 0.035f : 0.008f, cuenco ? 0.17f : 0.24f), me);
                    if (cuenco) Pp(r, PrimitiveType.Cylinder, new Vector3(0f, 0.069f, 0f), new Vector3(0.14f, 0.002f, 0.14f), matNegroFosa());
                    break;
                }
                case "Deer_Troffy":
                {
                    // trofeo de ciervo: escudo de madera, cuello, cabeza, hocico y astas ramificadas
                    var ma = MatMaderaProp(); var pelo = MatJuego("Deer_Troffy", matTela); var hueso = matVelaCem();
                    Pp(r, PrimitiveType.Cylinder, new Vector3(0f, 0.3f, 0.04f), new Vector3(0.36f, 0.025f, 0.46f), ma, 90f);
                    Pp(r, PrimitiveType.Sphere, new Vector3(0f, 0.27f, -0.08f), new Vector3(0.2f, 0.26f, 0.2f), pelo);
                    Pp(r, PrimitiveType.Sphere, new Vector3(0f, 0.38f, -0.2f), new Vector3(0.16f, 0.16f, 0.24f), pelo);
                    Pp(r, PrimitiveType.Sphere, new Vector3(0f, 0.34f, -0.33f), new Vector3(0.08f, 0.08f, 0.12f), pelo);
                    foreach (var s in new[] { -1f, 1f })
                    {
                        Pp(r, PrimitiveType.Cylinder, new Vector3(s * 0.08f, 0.55f, -0.18f), new Vector3(0.025f, 0.13f, 0.025f), hueso, 0f, 0f, -s * 30f);
                        Pp(r, PrimitiveType.Cylinder, new Vector3(s * 0.2f, 0.72f, -0.18f), new Vector3(0.02f, 0.1f, 0.02f), hueso, 0f, 0f, -s * 10f);
                        Pp(r, PrimitiveType.Cylinder, new Vector3(s * 0.12f, 0.72f, -0.2f), new Vector3(0.018f, 0.07f, 0.018f), hueso, 0f, 0f, s * 20f);
                        Pp(r, PrimitiveType.Sphere, new Vector3(s * 0.09f, 0.42f, -0.29f), new Vector3(0.025f, 0.025f, 0.025f), matNegroFosa());
                    }
                    break;
                }
                case "hammer":
                {
                    var ma = MatMaderaProp(); var hi = MatJuego("Hammer_small", MatHierroProp());
                    Pp(r, PrimitiveType.Cylinder, new Vector3(0f, 0.02f, 0f), new Vector3(0.03f, 0.17f, 0.03f), ma, 90f);
                    Pp(r, PrimitiveType.Cube, new Vector3(0f, 0.03f, -0.17f), new Vector3(0.05f, 0.05f, 0.13f), hi, 0f, 90f);
                    break;
                }
                case "Witch_WooDoo":
                {
                    // muñeco de trapo: cuerpo, cabeza, brazos y piernas, con alfileres de hierro
                    var tela = MatJuego("Witch_WooDoo", matTela); var hi = MatHierroProp();
                    Pp(r, PrimitiveType.Capsule, new Vector3(0f, 0.16f, 0f), new Vector3(0.1f, 0.08f, 0.07f), tela);
                    Pp(r, PrimitiveType.Sphere, new Vector3(0f, 0.28f, 0f), new Vector3(0.1f, 0.1f, 0.09f), tela);
                    foreach (var s in new[] { -1f, 1f })
                    {
                        Pp(r, PrimitiveType.Capsule, new Vector3(s * 0.08f, 0.18f, 0f), new Vector3(0.03f, 0.05f, 0.03f), tela, 0f, 0f, s * 60f);
                        Pp(r, PrimitiveType.Capsule, new Vector3(s * 0.03f, 0.05f, 0f), new Vector3(0.035f, 0.05f, 0.035f), tela);
                    }
                    Pp(r, PrimitiveType.Cylinder, new Vector3(0.01f, 0.2f, -0.05f), new Vector3(0.005f, 0.05f, 0.005f), hi, 70f);
                    Pp(r, PrimitiveType.Cylinder, new Vector3(-0.02f, 0.3f, -0.04f), new Vector3(0.005f, 0.05f, 0.005f), hi, 60f, 30f);
                    break;
                }
                case "magic_totem":
                {
                    // tótem de piedras ensangrentadas apiladas
                    var pi = MatJuego("magic_totem", matPiedra);
                    Pp(r, PrimitiveType.Sphere, new Vector3(0f, 0.16f, 0f), new Vector3(0.5f, 0.32f, 0.45f), pi);
                    Pp(r, PrimitiveType.Sphere, new Vector3(0.03f, 0.43f, 0f), new Vector3(0.36f, 0.26f, 0.33f), pi, 0f, 30f);
                    Pp(r, PrimitiveType.Sphere, new Vector3(-0.01f, 0.63f, 0.01f), new Vector3(0.24f, 0.2f, 0.22f), pi, 0f, 70f);
                    Pp(r, PrimitiveType.Sphere, new Vector3(0f, 0.77f, 0f), new Vector3(0.13f, 0.12f, 0.12f), pi);
                    break;
                }
                default: hecho = false; break;
            }
            if (!hecho) { Object.Destroy(raiz); return null; }
            return raiz;
        }
    }
}

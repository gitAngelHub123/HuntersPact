// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using UnityEngine;

namespace WitchHuntCoop
{
    /// Opción de administrador (provisional, para ver el mundo): modo vuelo. F9 (o el botón del panel F8) lo activa.
    /// WASD mueve hacia donde miras, Espacio sube, Ctrl baja, Shift acelera. Atraviesa todo y no hay daño de caída.
    public static class Vuelo
    {
        public static bool Activo;
        public static KeyCode Tecla = KeyCode.F9;
        static Behaviour caminante; static Rigidbody cuerpo; static Collider[] colisiones; static bool[] colAntes;
        static bool diosAntes; static float diosHasta = -1f; static FPSPlayer jug;

        public static void Alternar(FPSPlayer j)
        {
            if (j == null) return;
            if (!Activo) Encender(j); else Apagar();
        }

        static void Encender(FPSPlayer j)
        {
            jug = j; Activo = true;
            caminante = j.GetComponent<FPSRigidBodyWalker>(); if (caminante != null) caminante.enabled = false;
            cuerpo = j.GetComponent<Rigidbody>(); if (cuerpo != null) { cuerpo.velocity = Vector3.zero; cuerpo.isKinematic = true; }
            colisiones = j.GetComponentsInChildren<Collider>(true); colAntes = new bool[colisiones.Length];
            for (int i = 0; i < colisiones.Length; i++) { colAntes[i] = colisiones[i].enabled; colisiones[i].enabled = false; }
            // 2026-11-18: si se vuelve a volar durante los 5 s de gracia, el GodMode actual es el de la gracia (no el real):
            // antes se guardaba ese y el jugador quedaba invencible para siempre (el Ojo "te veía" pero no hacía daño)
            if (diosHasta < 0f) diosAntes = j.GodMode && Muerte.Instancia != null && Muerte.Instancia.Invencible;
            j.GodMode = true; diosHasta = -1f;
        }

        static void Apagar()
        {
            Activo = false;
            if (colisiones != null) for (int i = 0; i < colisiones.Length; i++) if (colisiones[i] != null) colisiones[i].enabled = colAntes[i];
            if (cuerpo != null) { cuerpo.isKinematic = false; cuerpo.velocity = Vector3.zero; }
            if (caminante != null) caminante.enabled = true;
            diosHasta = Time.time + 5f;   // unos segundos sin daño por si caes de golpe
        }

        public static void Procesar(Plugin plugin)
        {
            if (diosHasta > 0f && Time.time >= diosHasta) { diosHasta = -1f; if (jug != null) jug.GodMode = diosAntes; }
            var j = plugin.Jugador;
            if (j == null) { Activo = false; return; }
            if (Entrada.Enfocado && !Entrada.PanelAbierto && Input.GetKeyDown(Tecla)) Alternar(j);
            if (!Activo) return;
            if (jug != j) { Activo = false; diosHasta = -1f; if (j != null) j.GodMode = Muerte.Instancia != null && Muerte.Instancia.Invencible; return; }   // cambió de escena
            if (!Entrada.Enfocado || Entrada.PanelAbierto || Camera.main == null) return;
            Vector3 d = Vector3.zero; var cam = Camera.main.transform;
            if (Input.GetKey(KeyCode.W)) d += cam.forward;
            if (Input.GetKey(KeyCode.S)) d -= cam.forward;
            if (Input.GetKey(KeyCode.D)) d += cam.right;
            if (Input.GetKey(KeyCode.A)) d -= cam.right;
            if (Input.GetKey(KeyCode.Space)) d += Vector3.up;
            if (Input.GetKey(KeyCode.LeftControl)) d -= Vector3.up;
            float v = Input.GetKey(KeyCode.LeftShift) ? 120f : 25f;
            j.transform.position += d.normalized * v * Time.deltaTime;
        }
    }
}

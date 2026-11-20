// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using UnityEngine;

namespace WitchHuntCoop
{
    /// Cambio de arma en coop (pedido 2026-11-10): teclas 1 pistola, 2 mosquete, 3 escopeta, 4 ballesta,
    /// 5 bola de cristal, F estoque. La rueda del ratón recorre las ranuras 1→5 (arriba) y 5→1 (abajo),
    /// dando la vuelta. El juego solo recorría hacia abajo, en orden de índice y saltándose el estoque.
    static class SelectorArmas
    {
        // ranura (tecla) → índice de arma del juego
        static readonly int[] Indices = { 1, 2, Escopeta.Indice, Ballesta.Indice, Aliados.Indice };
        const int Estoque = 3;
        public static float RuedaPrueba;   // orden de prueba 'rueda_arma 1|-1'

        public static float UltimaRueda;   // última rueda leída por el juego (para las listas del mod)

        public static void Procesar(InputControl i)
        {
            if (!Guardado.ModoCoop) return;
            if (i.mouseWheel != 0f) UltimaRueda = i.mouseWheel;
            if (Escopeta.plugin != null && Escopeta.plugin.tienda != null && (Escopeta.plugin.tienda.Abierta || TiendaUI.InventarioAbierto)) { i.mouseWheel = 0f; i.selectNextPress = i.selectPrevPress = false; return; }   // con la tienda abierta la rueda es de la lista
            var pw = Rendimiento.Armas;
            if (pw == null || pw.weaponOrder == null) return;
            int pedido = -1;
            bool enfocado = Entrada.Enfocado && !Entrada.PanelAbierto && Controles.Capturando == null;
            if (enfocado)
            {
                if (Controles.Pulsada("arma1")) pedido = 0;
                else if (Controles.Pulsada("arma2")) pedido = 1;
                else if (Controles.Pulsada("arma3")) pedido = 2;
                else if (Controles.Pulsada("arma4")) pedido = 3;
                else if (Controles.Pulsada("arma5")) pedido = 4;
            }
            float rueda = i.mouseWheel + RuedaPrueba; RuedaPrueba = 0f;
            bool arriba = rueda > 0f || i.selectNextPress;
            bool paso = rueda != 0f || i.selectNextPress || i.selectPrevPress;
            bool f = enfocado && Controles.Pulsada("estoque");
            // el juego no debe hacer su propio cambio
            i.selectWeap1Press = i.selectWeap2Press = i.selectWeap3Press = i.selectWeap4Press = i.selectWeap5Press = false;
            i.selectNextPress = i.selectPrevPress = false;
            if (rueda != 0f && PuedeCambiar(pw)) i.mouseWheel = 0f;
            if (Entrada.Espectando || !PuedeCambiar(pw)) return;

            int destino = -1;
            if (f) destino = Estoque;
            else if (pedido >= 0) destino = Indices[pedido];
            else if (paso)
            {
                int actual = System.Array.IndexOf(Indices, pw.currentWeapon);
                int n = Indices.Length;
                for (int k = 1; k <= n; k++)
                {
                    int p;
                    if (actual < 0) p = arriba ? k - 1 : n - k;           // desde estoque o desarmado: primera / última
                    else p = ((actual + (arriba ? k : -k)) % n + n) % n;
                    if (Tiene(pw, Indices[p])) { destino = Indices[p]; break; }
                }
            }
            if (destino < 0 || destino == pw.currentWeapon || !Tiene(pw, destino)) return;
            Escopeta.plugin.Log("Selector de armas: " + pw.currentWeapon + " → " + destino + (rueda != 0f ? (rueda > 0f ? " (rueda arriba)" : " (rueda abajo)") : ""));
            pw.StartCoroutine(pw.SelectWeapon(destino));
        }

        static bool Tiene(PlayerWeapons pw, int idx)
        {
            if (idx < 0 || idx >= pw.weaponOrder.Length || pw.weaponOrder[idx] == null) return false;
            var wb = pw.weaponOrder[idx].GetComponent<WeaponBehavior>();
            return wb != null && wb.haveWeapon;
        }

        // mismas condiciones que el cambio de arma del juego
        static bool PuedeCambiar(PlayerWeapons pw)
        {
            if (Time.timeSinceLevelLoad <= 2f || pw.switching) return false;
            var cur = pw.CurrentWeaponBehaviorComponent;
            if (cur != null && cur.shooting) return false;
            return true;
        }
    }
}

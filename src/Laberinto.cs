// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using System.Collections.Generic;
using UnityEngine;

namespace WitchHuntCoop
{
    /// Montaña del castillo y laberinto de setos (pedido 2026-11-12): el castillo está en la cima de una montaña y la única forma de llegar
    /// a ella es cruzar un laberinto cuadrado de setos que la rodea por completo (entrada al sur, salida al pie de la montaña, frente al castillo).
    /// El laberinto sale de la semilla de la partida (anfitrión e invitado ven el mismo).
    public partial class Mundo
    {
        const float LabPaso = 6f;          // distancia entre ejes de pasillo (pasillo de ~4,4 m + seto de 1,6 m)
        const float LabGrosor = 1.6f, LabAlto = 6f;
        const int LabNx = 26, LabNz = 27;  // celdas del laberinto: ~156 m de ancho (este-oeste) × ~162 m de largo (norte-sur)
        public const float CastilloEscala = 1.6f;   // el castillo se construye a tamaño normal y se agranda en conjunto
        const float MonteBase = 360f, MonteMeseta = 120f, MonteAlto = 120f;   // radio de la falda, de la cima llana (donde está el castillo) y altura
        const float PitOut = MonteBase + LabNz * LabPaso;   // el foso negro rodea la montaña hasta aquí; el laberinto es el único puente

        static Vector2 CentroMonte { get { return new Vector2(CastilloX, CastilloZ + 70f); } }

        bool[,] labAbDer, labAbArr;   // celdas del laberinto y pasos abiertos (a la derecha = +x y hacia arriba = +z)
        int labEi, labSi, labSj, labSvi, labSvj;  // celda de entrada (columna) y celda de salida (+ celda vecina hacia la montaña)
        Vector2[] labRuta;                        // camino de adoquines: salida → pie de la meseta → escalinata
        Vector2 labEntradaPos, labSalidaPos;
        bool labListo;

        static float LabX0 { get { return CastilloX - LabNx * LabPaso / 2f; } }
        static float LabZ0 { get { return CentroMonte.y - PitOut; } }

        /// Montaña + foso + laberinto (en coordenadas del terreno norte), con un margen.
        static bool EnZonaCastillo(float x, float z, float margen)
        {
            float dx = x - CastilloX, dz = z - CentroMonte.y;
            return dx * dx + dz * dz < (PitOut + margen) * (PitOut + margen);
        }

        /// 0..1: cuánto es foso (negro y mortal) en ese punto del terreno norte. El anillo entre la montaña y PitOut es foso, salvo el puente del laberinto.
        static float PesoFoso(float x, float z)
        {
            float dx = x - CastilloX, dz = z - CentroMonte.y; float dc = Mathf.Sqrt(dx * dx + dz * dz);
            float dring = Mathf.Min(dc - MonteBase, PitOut - dc);
            if (dring <= 0f) return 0f;
            float sdx = Mathf.Abs(dx) - LabNx * LabPaso / 2f, sdz = Mathf.Max(CentroMonte.y - PitOut - z, z - (CentroMonte.y - MonteBase));
            float sd = Mathf.Max(sdx, sdz);
            return Mathf.SmoothStep(0f, 1f, Mathf.Min(dring, sd) / 12f);
        }

        /// Altura de la montaña (en metros sobre el llano) a una distancia dada del centro.
        static float AlturaMonte(float dc, float ruido)
        {
            if (dc >= MonteBase) return 0f;
            float t = Mathf.Clamp01((dc - MonteMeseta) / (MonteBase - MonteMeseta));
            float perfil = 1f - Mathf.SmoothStep(0f, 1f, t);
            return MonteAlto * perfil + ruido * 5f * Mathf.Sin(Mathf.PI * t);
        }

        void GenerarLaberinto(System.Random rng)
        {
            labAbDer = new bool[LabNx, LabNz]; labAbArr = new bool[LabNx, LabNz];
            var depth = new int[LabNx, LabNz];
            for (int i = 0; i < LabNx; i++) for (int j = 0; j < LabNz; j++) depth[i, j] = -1;
            labEi = LabNx / 2;
            // recorrido en profundidad (laberinto perfecto: un solo camino entre dos puntos)
            var pila = new Stack<int>(); pila.Push(labEi * 100); depth[labEi, 0] = 0;
            int[] dx = { 1, -1, 0, 0 }, dz = { 0, 0, 1, -1 };
            while (pila.Count > 0)
            {
                int cur = pila.Peek(); int i = cur / 100, j = cur % 100;
                var libres = new List<int>();
                for (int d = 0; d < 4; d++)
                {
                    int ni = i + dx[d], nj = j + dz[d];
                    if (ni < 0 || nj < 0 || ni >= LabNx || nj >= LabNz || depth[ni, nj] >= 0) continue;
                    libres.Add(d);
                }
                if (libres.Count == 0) { pila.Pop(); continue; }
                int e = libres[rng.Next(libres.Count)]; int ti = i + dx[e], tj = j + dz[e];
                if (e == 0) labAbDer[i, j] = true; else if (e == 1) labAbDer[ti, tj] = true; else if (e == 2) labAbArr[i, j] = true; else labAbArr[ti, tj] = true;
                depth[ti, tj] = depth[i, j] + 1; pila.Push(ti * 100 + tj);
            }
            // salida: la celda de la fila norte (pegada a la montaña) más lejos de la entrada; se calcula ANTES de abrir atajos
            // salida en el medio de la fila norte (pedido 2026-11-17): las dos celdas centrales unidas, de frente a la escalinata del castillo
            int mi = LabNx / 2, mejor = depth[mi, LabNz - 1];
            // más caminos: ~14 % de los muros interiores se abren (vueltas y bifurcaciones donde perderse)
            int abiertos = 0;
            for (int i = 0; i < LabNx; i++) for (int j = 0; j < LabNz; j++)
            {
                if (i < LabNx - 1 && !labAbDer[i, j] && rng.NextDouble() < 0.14) { labAbDer[i, j] = true; abiertos++; }
                if (j < LabNz - 1 && !labAbArr[i, j] && rng.NextDouble() < 0.14) { labAbArr[i, j] = true; abiertos++; }
            }
            labAbDer[mi - 1, LabNz - 1] = true;   // sin seto entre las dos celdas de la salida
            labSi = mi; labSj = LabNz - 1; labSvi = mi; labSvj = LabNz;
            labEntradaPos = new Vector2(LabX0 + (labEi + 0.5f) * LabPaso, LabZ0);
            labSalidaPos = new Vector2(LabX0 + mi * LabPaso, LabZ0 + LabNz * LabPaso);   // entre las dos celdas centrales: x = centro del castillo
            labRuta = new[] { labSalidaPos, new Vector2(EscX, EscZ1), new Vector2(CastilloX, CastilloZ - (14f * CastilloEscala + 2f)) };
            labListo = true;
            plugin.Log("Laberinto: " + LabNx + "×" + LabNz + " celdas (" + Mathf.RoundToInt(LabNx * LabPaso) + " × " + Mathf.RoundToInt(LabNz * LabPaso) + " m), salida a " + mejor + " pasos de la entrada con ruta principal, " + abiertos + " atajos; entrada en (" + Mathf.RoundToInt(labEntradaPos.x) + ", " + Mathf.RoundToInt(labEntradaPos.y) + ")");
        }

        bool LabAct(int i, int j) { return i >= 0 && j >= 0 && i < LabNx && j < LabNz; }

        // ¿hay seto en la línea vertical a (entre las celdas a-1 y a) a la altura de la fila j?
        bool SetoVert(int a, int j)
        {
            bool l = LabAct(a - 1, j), r = LabAct(a, j);
            if (!l && !r) return false;
            if (l && r && labAbDer[a - 1, j]) return false;
            if (labSvi >= 0 && labSj == j && ((labSi == a - 1 && labSvi == a) || (labSi == a && labSvi == a - 1))) return false;   // salida
            return true;
        }

        bool SetoHor(int b, int i)
        {
            bool d = LabAct(i, b - 1), u = LabAct(i, b);
            if (!d && !u) return false;
            if (d && u && labAbArr[i, b - 1]) return false;
            if (b == 0 && i == labEi) return false;   // entrada
            if (labSvi >= 0 && labSi == i && ((labSj == b - 1 && labSvj == b) || (labSj == b && labSvj == b - 1))) return false;   // salida
            if (labSvi >= 0 && b == LabNz && i == labSi - 1) return false;   // salida doble (la celda vecina también abre al norte)
            return true;
        }

        // ---- escalinata del laberinto al castillo (pedido 2026-11-17: "como si fueras a entrar al castillo de Drácula") ----
        public const float EscAncho = 9f;
        static float EscX { get { return CastilloX; } }
        static float EscZ0 { get { return LabZ0 + LabNz * LabPaso + 12f; } }        // pie de la escalinata (tras la explanada de la salida)
        static float EscZ1 { get { return CentroMonte.y - MonteMeseta - 60f; } }   // fin de la subida, ya en la cima
        static float EscZ2 { get { return CastilloZ - 29.5f * CastilloEscala; } }  // comienzo de la plaza del castillo

        /// Altura (normalizada) del pasillo de la escalinata: llano hasta su pie, rampa recta hasta la cima y luego llano hasta la plaza.
        /// Corta la loma donde sobra y rellena donde falta, para que la escalera vaya en línea recta de la salida a la puerta.
        static float TerrenoEscalinata(float x, float z, float v, float baseN, float cimaN)
        {
            if (z < EscZ0 - 12f || z > EscZ2 + 8f) return v;
            float lat = Mathf.Abs(x - EscX) - (EscAncho / 2f + 3.5f);
            float w = 1f - Mathf.SmoothStep(0f, 1f, lat / 22f);
            if (w <= 0f) return v;
            float objetivo = z <= EscZ0 ? baseN : z >= EscZ1 ? cimaN : Mathf.Lerp(baseN, cimaN, (z - EscZ0) / (EscZ1 - EscZ0));
            return Mathf.Lerp(v, objetivo, w);
        }

        float SueloT(TerrainData dn, float x, float z) { return dn.GetInterpolatedHeight(x / AnchoNuevo, z / FondoNuevo); }

        static void CajaMalla(List<Vector3> v, List<Vector2> uv, List<int> tr, Vector3 c, Vector3 s, float tile)
        {
            Vector3 h = s / 2f;
            float lu = Mathf.Max(s.x, s.z) / tile, lv = s.y / tile;
            // 6 caras (24 vértices); las laterales llevan la textura por metros
            CaraDel cara = (n, a, b, ua, ub) =>
            {
                Vector3 o = c + Vector3.Scale(n, h);
                int k = v.Count;
                v.Add(o - a - b); v.Add(o + a - b); v.Add(o + a + b); v.Add(o - a + b);
                uv.Add(new Vector2(0f, 0f)); uv.Add(new Vector2(ua, 0f)); uv.Add(new Vector2(ua, ub)); uv.Add(new Vector2(0f, ub));
                // orientación: normal hacia fuera
                Vector3 nn = Vector3.Cross(a, b);
                if (Vector3.Dot(nn, n) >= 0f) { tr.Add(k); tr.Add(k + 2); tr.Add(k + 1); tr.Add(k); tr.Add(k + 3); tr.Add(k + 2); }
                else { tr.Add(k); tr.Add(k + 1); tr.Add(k + 2); tr.Add(k); tr.Add(k + 2); tr.Add(k + 3); }
            };
            cara(Vector3.right, new Vector3(0f, 0f, h.z), new Vector3(0f, h.y, 0f), s.z / tile, lv);
            cara(Vector3.left, new Vector3(0f, 0f, h.z), new Vector3(0f, h.y, 0f), s.z / tile, lv);
            cara(Vector3.forward, new Vector3(h.x, 0f, 0f), new Vector3(0f, h.y, 0f), s.x / tile, lv);
            cara(Vector3.back, new Vector3(h.x, 0f, 0f), new Vector3(0f, h.y, 0f), s.x / tile, lv);
            cara(Vector3.up, new Vector3(h.x, 0f, 0f), new Vector3(0f, 0f, h.z), s.x / tile, s.z / tile);
        }

        /// Seto con volumen (pedido 2026-11-17: "mejorar excesivamente el laberinto"): en vez de una caja lisa, una malla con relieve
        /// de follaje en los costados, copa abombada e irregular arriba y bordes que se ondulan. El desplazamiento sale de la posición en el
        /// mundo, así que los tramos vecinos casan sin grietas y es igual en los dos juegos.
        static float CopaSeto(float x, float z) { return 0.15f + 0.75f * Mathf.PerlinNoise(x * 0.38f + 13.1f, z * 0.38f + 7.7f) + 0.22f * Mathf.PerlinNoise(x * 1.3f, z * 1.3f); }
        static float HojaSeto(float x, float y, float z) { return Mathf.PerlinNoise(x * 0.85f + z * 0.62f + 3.3f, y * 0.9f + 1.7f) * 0.7f + Mathf.PerlinNoise(x * 2.1f - z * 1.7f, y * 2.3f) * 0.3f; }

        static void SetoMalla(List<Vector3> v, List<Vector2> uv, List<int> tr, Vector3 c, Vector3 s, float suelo)
        {
            const float paso = 1.15f, tile = 2.2f;   // 1,15 m: buen relieve con ~40 % menos vértices que a 0,9 m
            Vector3 h = s / 2f;
            float yTop = c.y + h.y, yBot = c.y - h.y;
            // costados: rejilla vertical; desplazamiento hacia afuera (cero en las aristas verticales), la fila de arriba sube con la copa
            System.Action<Vector3, Vector3, float> cara = (o, eje, ancho) =>   // o: esquina inferior; eje: dirección horizontal de la cara; normal = cross(up, eje)
            {
                Vector3 n = Vector3.Cross(eje, Vector3.up).normalized;
                if (Vector3.Dot(n, (o + eje * ancho * 0.5f) - new Vector3(c.x, o.y, c.z)) < 0f) n = -n;
                int nu = Mathf.Max(1, Mathf.CeilToInt(ancho / paso)), nv = Mathf.Max(1, Mathf.CeilToInt((yTop - yBot) / paso));
                int k0 = v.Count;
                for (int j = 0; j <= nv; j++)
                    for (int i = 0; i <= nu; i++)
                    {
                        float fu = i / (float)nu, fv = j / (float)nv;
                        Vector3 p = o + eje * (ancho * fu) + Vector3.up * ((yTop - yBot) * fv);
                        float sobreSuelo = Mathf.Clamp01((p.y - suelo) / 1.2f);   // al pie del seto no se abomba (no se mete en el camino)
                        float lado = Mathf.Sin(Mathf.PI * fu);
                        float fuera = (HojaSeto(p.x, p.y, p.z) - 0.3f) * 0.55f * lado * sobreSuelo * (j < nv ? 1f : 0f);
                        float arriba = CopaSeto(p.x, p.z) * fv * fv;
                        v.Add(p + n * fuera + Vector3.up * arriba);
                        uv.Add(new Vector2((Vector3.Dot(p, eje)) / tile, p.y / tile));
                    }
                for (int j = 0; j < nv; j++)
                    for (int i = 0; i < nu; i++)
                    {
                        int a0 = k0 + j * (nu + 1) + i, a1 = a0 + 1, b0 = a0 + nu + 1, b1 = b0 + 1;
                        Vector3 nn = Vector3.Cross(v[a1] - v[a0], v[b0] - v[a0]);
                        // Unity: cara frontal = cross(v1-v0, v2-v0); nn = cross(eje, arriba) apunta hacia afuera si coincide con n
                        if (Vector3.Dot(nn, n) >= 0f) { tr.Add(a0); tr.Add(a1); tr.Add(b0); tr.Add(a1); tr.Add(b1); tr.Add(b0); }
                        else { tr.Add(a0); tr.Add(b0); tr.Add(a1); tr.Add(a1); tr.Add(b0); tr.Add(b1); }
                    }
            };
            float x0 = c.x - h.x, x1 = c.x + h.x, z0 = c.z - h.z, z1 = c.z + h.z;
            cara(new Vector3(x0, yBot, z0), Vector3.right, s.x);     // cara sur
            cara(new Vector3(x0, yBot, z1), Vector3.right, s.x);     // cara norte
            cara(new Vector3(x0, yBot, z0), Vector3.forward, s.z);   // cara oeste
            cara(new Vector3(x1, yBot, z0), Vector3.forward, s.z);   // cara este
            // copa: rejilla horizontal abombada (la misma altura que la fila de arriba de los costados en los bordes)
            {
                int nu = Mathf.Max(1, Mathf.CeilToInt(s.x / paso)), nw = Mathf.Max(1, Mathf.CeilToInt(s.z / paso));
                int k0 = v.Count;
                for (int j = 0; j <= nw; j++)
                    for (int i = 0; i <= nu; i++)
                    {
                        float fu = i / (float)nu, fw = j / (float)nw;
                        float x = x0 + s.x * fu, z = z0 + s.z * fw;
                        float bomba = 0.22f * Mathf.Sin(Mathf.PI * fu) * Mathf.Sin(Mathf.PI * fw);
                        v.Add(new Vector3(x, yTop + CopaSeto(x, z) + bomba, z));
                        uv.Add(new Vector2(x / tile, z / tile));
                    }
                for (int j = 0; j < nw; j++)
                    for (int i = 0; i < nu; i++)
                    {
                        int a0 = k0 + j * (nu + 1) + i, a1 = a0 + 1, b0 = a0 + nu + 1, b1 = b0 + 1;
                        tr.Add(a0); tr.Add(b0); tr.Add(a1); tr.Add(a1); tr.Add(b0); tr.Add(b1);   // normal hacia arriba (x a la derecha, z adelante)
                    }
            }
        }

        delegate void CaraDel(Vector3 n, Vector3 a, Vector3 b, float ua, float ub);
        delegate void CajaDel(float x0, float z0, float x1, float z1);

        static Material matSeto;

        void MaterialSeto()
        {
            if (matSeto != null) return;
            // follaje: grupos de hojas a varias escalas (repetible sin costuras), con relieve (mapa de normales) y motas de luz
            const int N = 512;
            System.Func<float, float, float, float> rep = (x, y, f) =>   // ruido que se repite cada N píxeles
            {
                float u = x / N, v = y / N;
                float a1 = Mathf.PerlinNoise(x * f, y * f), a2 = Mathf.PerlinNoise((x - N) * f, y * f), a3 = Mathf.PerlinNoise(x * f, (y - N) * f), a4 = Mathf.PerlinNoise((x - N) * f, (y - N) * f);
                return Mathf.Lerp(Mathf.Lerp(a1, a2, u), Mathf.Lerp(a3, a4, u), v);
            };
            // HOJAS DIBUJADAS (2026-11-18): miles de hojas con forma, nervio y borde, en capas (las de encima tapan a las de abajo);
            // entre ellas, huecos oscuros. La textura se repite sin costuras (todo se dibuja con vuelta a los bordes).
            var alt = new float[N, N]; var col = new Color[N, N];
            Color hueco = new Color(0.015f, 0.04f, 0.02f);
            for (int y = 0; y < N; y++) for (int x = 0; x < N; x++) { col[x, y] = hueco; alt[x, y] = 0f; }
            var rh = new System.Random(4242);
            for (int k = 0; k < 3600; k++)
            {
                float cx = (float)rh.NextDouble() * N, cy = (float)rh.NextDouble() * N, ang = (float)rh.NextDouble() * Mathf.PI * 2f;
                float L = 5f + (float)rh.NextDouble() * 5f, Wd = L * (0.38f + 0.12f * (float)rh.NextDouble());
                float capa = 0.25f + 0.75f * (k / 3600f) + 0.1f * (float)rh.NextDouble();   // las últimas quedan arriba
                float tono = (float)rh.NextDouble();
                Color baseC = Color.Lerp(new Color(0.07f, 0.2f, 0.07f), new Color(0.24f, 0.44f, 0.14f), tono * 0.8f + capa * 0.2f);
                if (rh.NextDouble() < 0.08) baseC = Color.Lerp(baseC, new Color(0.42f, 0.46f, 0.15f), 0.5f);   // alguna hoja amarillenta
                float ca = Mathf.Cos(ang), sa = Mathf.Sin(ang);
                int r = Mathf.CeilToInt(L) + 1;
                for (int dy = -r; dy <= r; dy++) for (int dx = -r; dx <= r; dx++)
                {
                    float u = dx * ca + dy * sa, w = -dx * sa + dy * ca;          // a lo largo / a lo ancho de la hoja
                    float t = u / L;                                              // −1 … 1
                    if (t < -1f || t > 1f) continue;
                    float anchoAqui = Wd * Mathf.Sqrt(Mathf.Max(0f, 1f - t * t)) * (1f - 0.25f * t);   // punta más fina
                    if (Mathf.Abs(w) > anchoAqui) continue;
                    int px = ((int)(cx + dx) % N + N) % N, py = ((int)(cy + dy) % N + N) % N;
                    float rel = Mathf.Abs(w) / Mathf.Max(0.01f, anchoAqui);
                    float h = capa + 0.25f * (1f - rel * rel) - 0.08f * Mathf.Abs(t);
                    if (h < alt[px, py]) continue;
                    alt[px, py] = h;
                    Color c = baseC * (0.75f + 0.35f * (1f - rel));                  // más claro en el centro de la hoja
                    if (Mathf.Abs(w) < 0.55f && t > -0.85f) c *= 0.72f;              // nervio central
                    if (rel > 0.82f) c *= 0.78f;                                     // borde
                    col[px, py] = c * (0.55f + 0.5f * capa);                         // las de abajo, en sombra
                }
            }
            var tex = new Texture2D(N, N, TextureFormat.RGB24, true) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Repeat };
            var nor = new Texture2D(N, N, TextureFormat.RGB24, true) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Repeat };
            for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
            {
                tex.SetPixel(x, y, col[x, y]);
                float dxh = alt[(x + 1) % N, y] - alt[(x + N - 1) % N, y], dyh = alt[x, (y + 1) % N] - alt[x, (y + N - 1) % N];
                var n = new Vector3(-dxh * 3f, -dyh * 3f, 1f).normalized;
                nor.SetPixel(x, y, new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f));
            }
            // rosas rojo oscuro entre las hojas (pedido 2026-11-17: seguir mejorando las paredes del laberinto): pocas por tesela, con pétalos y brillo
            var rf = new System.Random(7177);
            for (int f = 0; f < 7; f++)
            {
                int fx = rf.Next(N), fy = rf.Next(N), r = 5 + rf.Next(3);
                for (int dy = -r; dy <= r; dy++) for (int dx = -r; dx <= r; dx++)
                {
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / r; if (d > 1f) continue;
                    float ang = Mathf.Atan2(dy, dx), petalo = 0.75f + 0.25f * Mathf.Cos(ang * 5f + d * 6f);
                    if (d > petalo) continue;
                    Color rosa = Color.Lerp(new Color(0.62f, 0.05f, 0.08f), new Color(0.25f, 0.01f, 0.03f), d * d) * (0.85f + 0.3f * Mathf.Abs(Mathf.Sin(ang * 5f + d * 9f)));
                    tex.SetPixel((fx + dx + N) % N, (fy + dy + N) % N, rosa);
                }
            }
            tex.Apply(); nor.Apply();
            matSeto = new Material(Shader.Find("Standard")) { name = "coop_seto", color = Color.white, mainTexture = tex };
            // de noche los costados sin luz de luna quedaban negros: un poco de brillo propio con la misma textura de hojas
            matSeto.EnableKeyword("_EMISSION"); matSeto.SetTexture("_EmissionMap", tex); matSeto.SetColor("_EmissionColor", new Color(0.2f, 0.26f, 0.2f));
            // manchas grandes (zonas más claras, más oscuras o amarillentas): de lejos el seto no se ve de un solo color
            var det = new Texture2D(64, 64, TextureFormat.RGB24, true) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Repeat };
            for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
            {
                float n1 = rep(x * 4f, y * 4f, 0.02f), n2 = rep(x * 4f + 90f, y * 4f + 33f, 0.05f);
                float l = 0.42f + 0.2f * n1 + 0.08f * n2;   // manchas suaves, sin amarillear el verde
                det.SetPixel(x, y, new Color(l * 0.94f, l * 1.04f, l * 0.92f));
            }
            det.Apply();
            matSeto.EnableKeyword("_DETAIL_MULX2"); matSeto.SetTexture("_DetailAlbedoMap", det); matSeto.SetTextureScale("_DetailAlbedoMap", new Vector2(0.07f, 0.07f));
            // mapa de relieve de las hojas (las mallas de setos ya llevan tangentes, 2026-11-18)
            matSeto.SetTexture("_BumpMap", nor); matSeto.SetFloat("_BumpScale", 1.3f); matSeto.EnableKeyword("_NORMALMAP");
            matSeto.SetFloat("_Glossiness", 0.12f);
        }

        void ConstruirLaberinto(GameObject terreno, TerrainData dn)
        {
            if (!labListo) return;
            PrepararMaterialesCastillo(); MaterialSeto();
            var raiz = new GameObject("coop_laberinto"); raiz.transform.SetParent(terreno.transform, false); raiz.transform.localPosition = Vector3.zero;
            var pa = raiz.transform;
            var rng = new System.Random(77177);
            var porChunk = new Dictionary<int, KeyValuePair<List<Vector3>, KeyValuePair<List<Vector2>, List<int>>>>();
            var cajas = new Dictionary<int, List<Bounds>>();
            int total = 0;
            CajaDel caja = (x0, z0, x1, z1) =>
            {
                float cx = (x0 + x1) / 2f, cz = (z0 + z1) / 2f;
                float ya = SueloT(dn, x0, z0), yb = SueloT(dn, x1, z1), ym = SueloT(dn, cx, cz);
                float lo = Mathf.Min(ya, Mathf.Min(yb, ym)) - 1f, hi = Mathf.Max(ya, Mathf.Max(yb, ym)) + LabAlto;
                var c = new Vector3(cx, (lo + hi) / 2f, cz); var s = new Vector3(x1 - x0, hi - lo, z1 - z0);
                int key = Mathf.FloorToInt((cx - LabX0) / 100f) * 100 + Mathf.FloorToInt((cz - LabZ0) / 100f);
                while (porChunk.ContainsKey(key) && porChunk[key].Key.Count > 56000) key += 1000000;   // Unity 5.6: máximo 65 535 vértices por malla
                KeyValuePair<List<Vector3>, KeyValuePair<List<Vector2>, List<int>>> g;
                if (!porChunk.TryGetValue(key, out g)) { g = new KeyValuePair<List<Vector3>, KeyValuePair<List<Vector2>, List<int>>>(new List<Vector3>(), new KeyValuePair<List<Vector2>, List<int>>(new List<Vector2>(), new List<int>())); porChunk[key] = g; cajas[key] = new List<Bounds>(); }
                SetoMalla(g.Key, g.Value.Key, g.Value.Value, c, s, lo + 1f);
                cajas[key].Add(new Bounds(c, s)); total++;
            };
            float G = LabGrosor, P = LabPaso;
            for (int a = 0; a <= LabNx; a++)   // líneas verticales (muros que van a lo largo de z)
            {
                int ini = -1;
                for (int j = 0; j <= LabNz; j++)
                {
                    bool m = j < LabNz && SetoVert(a, j);
                    if (m && ini < 0) ini = j;
                    if (!m && ini >= 0) { float x = LabX0 + a * P; caja(x - G / 2f, LabZ0 + ini * P - G / 2f, x + G / 2f, LabZ0 + j * P + G / 2f); ini = -1; }
                }
            }
            for (int b = 0; b <= LabNz; b++)   // líneas horizontales
            {
                int ini = -1;
                for (int i = 0; i <= LabNx; i++)
                {
                    bool m = i < LabNx && SetoHor(b, i);
                    if (m && ini < 0) ini = i;
                    if (!m && ini >= 0) { float z = LabZ0 + b * P; caja(LabX0 + ini * P - G / 2f, z - G / 2f, LabX0 + i * P + G / 2f, z + G / 2f); ini = -1; }
                }
            }
            int verts = 0;
            foreach (var kv in porChunk)
            {
                var go = new GameObject("coop_setos_" + kv.Key); go.transform.SetParent(pa, false);
                var me = new Mesh { name = "coop_setos" };
                me.SetVertices(kv.Value.Key); me.SetUVs(0, kv.Value.Value.Key); me.SetTriangles(kv.Value.Value.Value, 0);
                me.RecalculateNormals(); me.RecalculateBounds();
                me.tangents = Malla.Tangentes(kv.Value.Key, kv.Value.Value.Key, kv.Value.Value.Value, me.normals);   // para el mapa de relieve de las hojas
                go.AddComponent<MeshFilter>().sharedMesh = me; go.AddComponent<MeshRenderer>().sharedMaterial = matSeto;
                foreach (var b in cajas[kv.Key]) { var bc = go.AddComponent<BoxCollider>(); bc.center = b.center; bc.size = b.size; }
                verts += kv.Value.Key.Count;
            }

            // entrada: portón monumental de piedra, al sur (pedido 2026-11-17)
            float ex = labEntradaPos.x, ez = labEntradaPos.y, ey = SueloT(dn, ex, ez);
            PortonLaberinto(pa, ex, ey, ez, dn);

            // salida monumental (como la entrada, mirando a la montaña) y escalinata recta hasta la plaza del castillo
            try { SalidaLaberinto(pa, dn); EscalinataDracula(pa, dn); } catch (System.Exception e) { plugin.Log("Laberinto: error en la escalinata " + e); }
            try { EntornoCastillo(pa, dn); } catch (System.Exception e) { plugin.Log("Castillo: error en el entorno " + e); }

            // estatuas en algunos callejones sin salida y luces fantasmales entre los setos
            var sinSalida = new List<int>();
            for (int i = 0; i < LabNx; i++) for (int j = 0; j < LabNz; j++)
            {
                if ((i == labEi && j == 0)) continue;
                int ab = 0; if (labAbDer[i, j]) ab++; if (i > 0 && labAbDer[i - 1, j]) ab++; if (labAbArr[i, j]) ab++; if (j > 0 && labAbArr[i, j - 1]) ab++;
                if (ab == 1 && !(i == labSi && j == labSj)) sinSalida.Add(i * 100 + j);
            }
            for (int n = 0; n < 8 && sinSalida.Count > 0; n++)
            {
                int idx = rng.Next(sinSalida.Count); int c = sinSalida[idx]; sinSalida.RemoveAt(idx);
                float x = LabX0 + (c / 100 + 0.5f) * P, z = LabZ0 + (c % 100 + 0.5f) * P;
                Estatua(pa, new Vector3(x, SueloT(dn, x, z), z), (float)rng.NextDouble() * 360f);
            }
            for (int n = 0; n < 12; n++)
            {
                float x = LabX0 + (float)rng.NextDouble() * LabNx * LabPaso, z = LabZ0 + (float)rng.NextDouble() * LabNz * LabPaso;
                var e = new GameObject("coop_espectro"); e.transform.SetParent(pa, false);
                e.transform.localPosition = new Vector3(x, SueloT(dn, x, z) + 2.5f, z);
                var l = e.AddComponent<Light>(); l.type = LightType.Point; l.color = n % 3 == 0 ? new Color(0.75f, 0.2f, 0.9f) : n % 3 == 1 ? new Color(0.3f, 1f, 0.8f) : new Color(0.5f, 0.55f, 1f);
                l.range = 14f; l.intensity = 1.3f; l.shadows = LightShadows.None;
                e.AddComponent<EspectroFlotante>().Fase = n * 1.7f;
            }

            // niebla baja sobre el laberinto
            var gn = new GameObject("coop_niebla_laberinto"); gn.transform.SetParent(pa, false);
            gn.transform.localPosition = new Vector3(CastilloX, SueloT(dn, labEntradaPos.x, labEntradaPos.y) + 1.4f, LabZ0 + LabNz * LabPaso / 2f);
            var ps = gn.AddComponent<ParticleSystem>();
            var main = ps.main; main.loop = true; main.startLifetime = 16f; main.startSpeed = 0.2f; main.startSize = new ParticleSystem.MinMaxCurve(14f, 26f);
            main.startColor = new Color(0.42f, 0.4f, 0.62f, 0.15f); main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = 320; main.prewarm = true;
            var em = ps.emission; em.rateOverTime = 22f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.box = new Vector3(LabNx * LabPaso, 2f, LabNz * LabPaso);
            var cl = ps.colorOverLifetime; cl.enabled = true; var gr = new Gradient();
            gr.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            cl.color = gr;
            var rr = gn.GetComponent<ParticleSystemRenderer>(); rr.material = Potenciado.MaterialGas(); rr.renderMode = ParticleSystemRenderMode.Billboard; rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            try { ConstruirNubesCastillo(pa, dn); } catch (System.Exception e) { plugin.Log("Nubes: error " + e); }
            ConstruirFosoNegro(pa, dn);
            try { ConstruirCaminoZonaNueva(terreno, dn); } catch (System.Exception e) { plugin.Log("Camino a la zona nueva: error " + e); }
            laberintoRaiz = raiz;
            try { var todos = new List<Bounds>(); foreach (var kv in cajas) todos.AddRange(kv.Value); PisoLaberinto(pa, dn, todos); } catch (System.Exception e) { plugin.Log("Laberinto: error en el suelo " + e.Message); }
            SinSombraLoPequeno(raiz);
            plugin.Log("Laberinto: " + total + " tramos de seto en " + porChunk.Count + " bloques (" + verts + " vértices), portón de entrada, camino de adoquines y " + 12 + " luces");
        }

        /// El foso: un disco negro (sin luz) justo sobre su fondo, para que se vea como un abismo sin fin, y dos anillos de paredes invisibles en sus bordes
        /// (los jugadores no pueden caer: pedido 2026-11-13). El puente del laberinto queda libre.
        void ConstruirFosoNegro(Transform pa, TerrainData dn)
        {
            float yFoso = SueloT(dn, CastilloX + (MonteBase + PitOut) / 2f, CentroMonte.y);
            var disco = GameObject.CreatePrimitive(PrimitiveType.Cylinder); Object.Destroy(disco.GetComponent<Collider>());
            disco.name = "coop_foso_negro"; disco.transform.SetParent(pa, false);
            disco.transform.localPosition = new Vector3(CastilloX, yFoso + 0.45f, CentroMonte.y);
            disco.transform.localScale = new Vector3(PitOut * 2f - 6f, 0.01f, PitOut * 2f - 6f);
            var mn = new Material(Shader.Find("Unlit/Color")) { name = "coop_foso_negro", color = Color.black };
            disco.GetComponent<Renderer>().sharedMaterial = mn; disco.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            int cajas = 0;
            foreach (float radio in new[] { MonteBase + 4f, PitOut - 4f })
            {
                int pasos = Mathf.CeilToInt(2f * Mathf.PI * radio / 7f);
                for (int k = 0; k < pasos; k++)
                {
                    float aa = k * 2f * Mathf.PI / pasos;
                    float x = CastilloX + Mathf.Cos(aa) * radio, z = CentroMonte.y + Mathf.Sin(aa) * radio;
                    if (PesoFoso(x, z) < 0.012f && PesoFoso(CastilloX + Mathf.Cos(aa) * (radio + (radio < PitOut - 10f ? 8f : -8f)), CentroMonte.y + Mathf.Sin(aa) * (radio + (radio < PitOut - 10f ? 8f : -8f))) < 0.012f) continue;   // no hay foso aquí (puente)
                    var g = new GameObject("coop_foso_muro"); g.transform.SetParent(pa, false);
                    g.transform.localPosition = new Vector3(x, SueloT(dn, x, z) + 6f, z);
                    g.transform.localRotation = Quaternion.Euler(0f, -aa * Mathf.Rad2Deg + 90f, 0f);
                    var bc = g.AddComponent<BoxCollider>(); bc.size = new Vector3(9f, 40f, 3f);   // 9 m de largo (se solapan), 40 m de alto
                    cajas++;
                }
            }
            plugin.Log("Foso: disco negro de " + Mathf.RoundToInt(PitOut * 2f) + " m y " + cajas + " paredes invisibles en sus bordes");
        }

        GameObject caminoZonaNueva; static Material matCaminoZN;

        /// Camino de tierra a la zona nueva (pedido 2026-11-16): se PINTA en el suelo con las mismas texturas de camino del juego
        /// (capa 0 = tierra, capa 4 = tierra con piedras en los bordes), angosto y con curvas suaves, del paso a la entrada del laberinto.
        void ConstruirCaminoZonaNueva(GameObject terreno, TerrainData dn)
        {
            var orig = Anclaje.Principal(); if (orig == null || labEntradaPos == Vector2.zero) return;
            if (caminoZonaNueva != null) Object.Destroy(caminoZonaNueva);
            Vector3 o = orig.transform.position, on = terreno.transform.position; float zOrigFin = o.z + orig.terrainData.size.z;
            float cx = o.x + (PasoX0 + PasoX1) / 2f;
            var pts = new List<Vector2> { new Vector2(cx, o.z + ZPasoSinArboles), new Vector2(cx, zOrigFin + 60f), new Vector2(on.x + labEntradaPos.x, on.z + labEntradaPos.y - 8f) };
            // ruta con curvas: cada 1,5 m, desplazada de lado por ondas suaves que se apagan en los extremos (empalma con el camino de siempre)
            var ruta = new List<Vector2>(); float total = 0f;
            for (int sg = 0; sg < pts.Count - 1; sg++) total += Vector2.Distance(pts[sg], pts[sg + 1]);
            float recorrido = 0f;
            for (int sg = 0; sg < pts.Count - 1; sg++)
            {
                Vector2 a0 = pts[sg], b0 = pts[sg + 1]; float len = Vector2.Distance(a0, b0); Vector2 dir = (b0 - a0) / len, nor = new Vector2(-dir.y, dir.x);
                for (float t = 0f; t <= len; t += 1.5f)
                {
                    float sAbs = recorrido + t;
                    float taper = Mathf.SmoothStep(0f, 1f, Mathf.Min(sAbs, total - sAbs) / 60f);
                    float lateral = (8f * Mathf.Sin(sAbs / 65f) + 4.5f * Mathf.Sin(sAbs / 27f + 1.3f)) * taper;
                    ruta.Add(a0 + dir * t + nor * lateral);
                }
                recorrido += len;
            }
            int pintadas = PintarCamino(orig.terrainData, o, ruta) + PintarCamino(dn, on, ruta);
            plugin.Log("Camino a la zona nueva: pintado en el suelo (" + ruta.Count + " puntos, " + Mathf.RoundToInt(total) + " m, " + pintadas + " celdas)");
        }

        /// Pinta el camino en las capas del suelo: tierra en el centro (capa 0) y tierra con piedras en el borde (capa 4).
        static int PintarCamino(TerrainData td, Vector3 o, List<Vector2> ruta)
        {
            if (td == null || td.alphamapLayers < 5) return 0;
            int W = td.alphamapWidth, H = td.alphamapHeight, L = td.alphamapLayers; Vector3 sz = td.size;
            float cw = sz.x / W, ch = sz.z / H;
            float nucleo = Mathf.Max(1.2f, cw * 0.7f), borde = nucleo + Mathf.Max(1.4f, cw * 0.9f), R = borde + Mathf.Max(cw, ch);
            float minX = 1e9f, maxX = -1e9f, minZ = 1e9f, maxZ = -1e9f; bool hay = false;
            foreach (var p in ruta)
            {
                if (p.x < o.x - R || p.x > o.x + sz.x + R || p.y < o.z - R || p.y > o.z + sz.z + R) continue;
                hay = true; minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x); minZ = Mathf.Min(minZ, p.y); maxZ = Mathf.Max(maxZ, p.y);
            }
            if (!hay) return 0;
            int x0 = Mathf.Clamp(Mathf.FloorToInt((minX - R - o.x) / cw), 0, W - 1), x1 = Mathf.Clamp(Mathf.CeilToInt((maxX + R - o.x) / cw), 0, W - 1);
            int z0 = Mathf.Clamp(Mathf.FloorToInt((minZ - R - o.z) / ch), 0, H - 1), z1 = Mathf.Clamp(Mathf.CeilToInt((maxZ + R - o.z) / ch), 0, H - 1);
            int w = x1 - x0 + 1, h = z1 - z0 + 1; if (w <= 0 || h <= 0) return 0;
            var a = td.GetAlphamaps(x0, z0, w, h);
            var wc = new float[h, w]; var wb = new float[h, w];
            foreach (var p in ruta)
            {
                int cxi = Mathf.FloorToInt((p.x - o.x) / cw) - x0, czi = Mathf.FloorToInt((p.y - o.z) / ch) - z0;
                int rx = Mathf.CeilToInt(R / cw) + 1, rz = Mathf.CeilToInt(R / ch) + 1;
                for (int j = czi - rz; j <= czi + rz; j++)
                    for (int i = cxi - rx; i <= cxi + rx; i++)
                    {
                        if (i < 0 || j < 0 || i >= w || j >= h) continue;
                        float dx = o.x + (x0 + i + 0.5f) * cw - p.x, dz = o.z + (z0 + j + 0.5f) * ch - p.y, d = Mathf.Sqrt(dx * dx + dz * dz);
                        if (d > borde) continue;
                        float c = d <= nucleo ? 1f : 1f - Mathf.SmoothStep(0f, 1f, (d - nucleo) / (borde - nucleo));
                        if (c > wc[j, i]) wc[j, i] = c;
                        float bd = d <= nucleo ? 0.25f : 0.8f * (1f - Mathf.Abs((d - (nucleo + borde) * 0.5f)) / ((borde - nucleo) * 0.5f + 0.01f));
                        if (bd > wb[j, i]) wb[j, i] = Mathf.Max(0f, bd);
                    }
            }
            int n = 0;
            for (int j = 0; j < h; j++)
                for (int i = 0; i < w; i++)
                {
                    float c = wc[j, i], b = Mathf.Min(wb[j, i] * 0.55f, 1f - c);
                    if (c <= 0.001f && b <= 0.001f) continue;
                    float resto = Mathf.Clamp01(1f - c - b);
                    for (int l = 0; l < L; l++) a[j, i, l] *= resto;
                    a[j, i, 0] += c; a[j, i, 4] += b;
                    float sum = 0f; for (int l = 0; l < L; l++) sum += a[j, i, l];
                    if (sum > 0.0001f) for (int l = 0; l < L; l++) a[j, i, l] /= sum;
                    n++;
                }
            td.SetAlphamaps(x0, z0, a);
            return n;
        }

        GameObject laberintoRaiz;
        public Terrain TerrenoNorte { get { return nuevo; } }

        // ---------------- Foso mortal y enemigos del laberinto ----------------

        float proxFoso, proxEnemigosLab;
        readonly List<TakeDamage> enemigosLab = new List<TakeDamage>();
        public const int EnemigosLaberinto = 18;

        /// ¿Está ese punto del mundo dentro del foso negro?
        bool EnFoso(Vector3 w)
        {
            if (nuevo == null) return false;
            Vector3 l = w - nuevo.transform.position;
            return PesoFoso(l.x, l.z) > 0.02f;
        }

        /// Si caes al foso (bajo el nivel del llano) mueres. No cuenta mientras vuelas (modo admin).
        void ActualizarFoso()
        {
            if (Time.time < proxFoso || nuevo == null) return;
            proxFoso = Time.time + 0.25f;
            var j = plugin.Jugador;
            if (j == null || Vuelo.Activo || plugin.Escena != EscenaBosque) return;
            Vector3 p = j.transform.position, l = p - nuevo.transform.position;
            if (PesoFoso(l.x, l.z) > 0.15f && p.y < sueloLlanoY - 4f)
            {
                plugin.Log("Foso: caíste al vacío (" + p.ToString("F0") + ")");
                j.ApplyDamage(10000f);
            }
        }

        /// El anfitrión llena el laberinto de enemigos cuando alguien se acerca (y los quita cuando los dos se alejan).
        void ActualizarEnemigosLaberinto()
        {
            if (!labListo || nuevo == null || datosNuevos == null || !plugin.EsAnfitrion || !plugin.Conectado || plugin.CombateActual == null || plugin.Escena != EscenaBosque) return;
            if (Time.time < proxEnemigosLab) return;
            proxEnemigosLab = Time.time + 2f;
            enemigosLab.RemoveAll(x => x == null);
            Vector3 centro = nuevo.transform.position + new Vector3(CastilloX, 0f, LabZ0 + LabNz * LabPaso / 2f);
            var j = plugin.Jugador; var c = plugin.CompaneroActual;
            float dj = j != null ? Vector2.Distance(new Vector2(j.transform.position.x, j.transform.position.z), new Vector2(centro.x, centro.z)) : 99999f;
            float dc = c != null && c.Visible ? Vector2.Distance(new Vector2(c.PosicionMundo.x, c.PosicionMundo.z), new Vector2(centro.x, centro.z)) : 99999f;
            float cerca = Mathf.Min(dj, dc);
            if (cerca > 450f)
            {
                foreach (var td in enemigosLab) if (td != null) Object.Destroy(td.transform.root.gameObject);
                enemigosLab.Clear(); return;
            }
            if (cerca > 200f || enemigosLab.Count > 0) return;
            for (int n = 0; n < EnemigosLaberinto; n++)
            {
                int i = Random.Range(0, LabNx), k = Random.Range(5, LabNz);   // no en las primeras filas (la entrada)
                float x = LabX0 + (i + 0.5f) * LabPaso, z = LabZ0 + (k + 0.5f) * LabPaso;
                Vector3 pos = nuevo.transform.position + new Vector3(x, SueloT(datosNuevos, x, z), z);
                string tipo = Guardianes[Random.Range(0, Guardianes.Length)];
                var td = plugin.CombateActual.Generar(tipo, pos);
                if (td != null) enemigosLab.Add(td);
            }
            plugin.Log("Laberinto: " + enemigosLab.Count + " enemigos generados");
        }


        /// Para la orden de prueba "irlaberinto [entrada|salida|cima]": posición en el mundo.
        public Vector3? PosLaberinto(string que)
        {
            if (!labListo || nuevo == null || datosNuevos == null) return null;
            Vector2 p = que == "salida" ? labSalidaPos : que == "cima" ? CentroMonte : labEntradaPos + new Vector2(0f, -6f);
            float y = datosNuevos.GetInterpolatedHeight(p.x / AnchoNuevo, p.y / FondoNuevo);
            return nuevo.transform.position + new Vector3(p.x, y, p.y) - Ancla();
        }
    }
}

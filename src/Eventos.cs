// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace WitchHuntCoop
{
    /// Eventos del bosque. Pueden coincidir varios si hay mala suerte (cada uno con su duración); solo la noche tranquila y la noche ruidosa se excluyen entre sí. El anfitrión tira los dados cada segundo y avisa al
    /// invitado; los dos juegos aplican el efecto.
    ///  - Luna de sangre (0,0035 %/s, 12 min; también al encontrar la escopeta o la ballesta): todo rojo,
    ///    luna roja en el cielo, todos los enemigos potenciados; los jefes además ×2 vida, velocidad y daño.
    ///  - Noche tranquila (0,01 %/s, 6 min): la mitad de los enemigos desaparece.
    ///  - Niebla (0,09 %/s, 2 min): niebla espesa.
    ///  - Noche oscura (0,02 %/s, 3 min): se ve hasta ~50 m; más allá y el cielo, negro.
    ///  - Noche ruidosa (0,008 %/s, 3 min): aparecen más enemigos y los potenciados salen el doble.
    ///  - Tormenta (0,02 %/s, 10 min): lluvia intensa, relámpagos y truenos, se ve hasta ~150 m y hay
    ///    1,5 veces más enemigos (el anfitrión genera la mitad de los que había y los repone cada minuto).
    public class Eventos
    {
        public enum Tipo : byte { Ninguno = 0, LunaDeSangre = 1, NocheTranquila = 2, Niebla = 3, NocheOscura = 4, NocheRuidosa = 5, Tormenta = 6 }
        static readonly float[] Probabilidad = { 0f, 0.000035f, 0.0001f, 0.0009f, 0.0002f, 0f, 0.0002f };   // por segundo (2026-11-19: noche ruidosa QUITADA del juego, prob. 0)
        static readonly float[] Duracion = { 0f, 720f, 360f, 240f, 180f, 180f, 600f };   // niebla: 4 min (pedido 2026-11-13)
        static readonly string[] Titulo = { "", "LUNA DE SANGRE", "NOCHE TRANQUILA", "NIEBLA", "NOCHE OSCURA", "NOCHE RUIDOSA", "TORMENTA" };
        static readonly string[] Subtitulo = { "", "Todos los enemigos están potenciados. Los jefes, más fuertes que nunca.", "El bosque está más tranquilo esta noche...",
                                               "Una niebla espesa cubre el bosque.", "No se ve más allá de unos pasos.", "Algo despertó al bosque: vienen más enemigos.",
                                               "Lluvia, relámpagos y truenos. Cuesta ver a más de 150 m... y hay más enemigos." };

        public static Eventos Instancia;
        public static Tipo Actual = Tipo.Ninguno;   // el último que empezó y sigue activo (para el título)
        static readonly Dictionary<Tipo, float> activos = new Dictionary<Tipo, float>();   // evento → cuándo termina
        static readonly Dictionary<Tipo, float> inicios = new Dictionary<Tipo, float>();   // evento → cuándo empezó
        public static bool Esta(Tipo t) { return activos.ContainsKey(t); }
        public static bool Luna { get { return Esta(Tipo.LunaDeSangre); } }
        public static bool Ruidosa { get { return false; } }   // noche ruidosa quitada (2026-11-19)
        public static int Cuantos { get { return activos.Count; } }

        readonly Plugin plugin;
        float proximaTirada, proximoEnvio, anuncioHasta, proximaHorda, durTitulo = 6f;
        Tipo tituloTipo = Tipo.Ninguno;   // el evento cuyo título se está mostrando
        int escenaVista = -1;
        readonly System.Random azar = new System.Random();

        // lo que se cambia y hay que devolver
        bool guardado; bool fogAntes; FogMode modoAntes; Color colorAntes, ambienteAntes; float densAntes, iniAntes, finAntes; Material cieloAntes;
        readonly Dictionary<Light, Color> lucesAntes = new Dictionary<Light, Color>();
        readonly Dictionary<Camera, KeyValuePair<CameraClearFlags, Color>> camarasAntes = new Dictionary<Camera, KeyValuePair<CameraClearFlags, Color>>();
        readonly List<GameObject> ocultos = new List<GameObject>();
        readonly List<GameObject> potenciadosLuna = new List<GameObject>();
        class GotaLuna { public Transform Cuerpo, Gota; public Material MC, MG; public float X, Y, Ancho, L, Vel, Fase; }
        readonly List<GotaLuna> gotasLuna = new List<GotaLuna>();
        GameObject luna; Vector3 dirLuna = new Vector3(-0.45f, 0.8f, 0.7f).normalized;
        int campanadas; float proximaCampana = float.MaxValue;
        float proximoBarrido;

        public Eventos(Plugin p) { plugin = p; Instancia = this; }

        public void Actualizar(bool conectado)
        {
            int esc = plugin.Escena;
            if (esc != escenaVista)
            {
                TerminarTodos(false);
                Campana.Detener();   // al cambiar de escena (cabaña/puente) no queda ninguna campana sonando
                escenaVista = esc; guardado = false; ocultos.Clear(); potenciadosLuna.Clear(); luna = null;
            }
            if (esc != Mundo.EscenaBosque || plugin.Jugador == null || !conectado || !Guardado.ModoCoop) return;
            float t = Time.time;
            if (activos.Count > 0)
            {
                var vencidos = new List<Tipo>();
                foreach (var kv in activos) if (t > kv.Value) vencidos.Add(kv.Key);
                foreach (var v in vencidos) Terminar(v, true);
                if (activos.Count > 0)
                {
                    Mantener(t);
                    if (plugin.EsAnfitrion && Time.realtimeSinceStartup > proximoEnvio) { proximoEnvio = Time.realtimeSinceStartup + 2f; Enviar(); }
                }
            }
            // Solo el anfitrión tira los dados (una vez por segundo, tras 20 s en el bosque). Se tira aunque ya haya
            // un evento: con mala suerte coinciden varios.
            if (!plugin.EsAnfitrion || Time.timeSinceLevelLoad < 20f || t < proximaTirada) return;
            proximaTirada = t + 1f;
            for (int k = 1; k < Probabilidad.Length; k++)
                if (!Esta((Tipo)k) && Compatible((Tipo)k) && azar.NextDouble() < Probabilidad[k] * Opciones.FactorEventos) { Iniciar((Tipo)k, Duracion[k], true); break; }
        }

        /// La noche tranquila (menos enemigos) y la ruidosa (más enemigos) no pueden coincidir.
        static bool Compatible(Tipo t)
        {
            return !(t == Tipo.NocheTranquila && Esta(Tipo.NocheRuidosa)) && !(t == Tipo.NocheRuidosa && Esta(Tipo.NocheTranquila));
        }

        /// Empieza un evento (se suma a los que ya haya; si ya estaba, se alarga).
        public void Iniciar(Tipo tipo, float segundos, bool avisar)
        {
            if (tipo == Tipo.Ninguno || tipo == Tipo.NocheRuidosa) return;   // (2026-11-19) la noche ruidosa ya no existe: ni al azar, ni por la red, ni por orden
            if (Esta(tipo)) { activos[tipo] = Mathf.Max(activos[tipo], Time.time + segundos); return; }
            if (!Compatible(tipo)) return;
            Guardar();
            activos[tipo] = Time.time + segundos; inicios[tipo] = Time.time;
            Actual = tipo; tituloTipo = tipo;
            if (tipo == Tipo.NocheOscura) cielosNegros = false;
            ultimaTinta = 0f;
            durTitulo = tipo == Tipo.LunaDeSangre ? 7.5f : 6f; anuncioHasta = Time.realtimeSinceStartup + durTitulo;
            proximoBarrido = 0f; proximaHorda = Time.time + 5f;
            Aplicar(tipo);
            plugin.Log("Evento: " + Titulo[(int)tipo] + " durante " + Mathf.RoundToInt(segundos) + " s (activos: " + activos.Count + ")");
            if (avisar) Enviar();
        }

        /// Luna de sangre al encontrar la escopeta o la ballesta (la empieza quien la encontró).
        public void LunaPorArma() { Iniciar(Tipo.LunaDeSangre, Duracion[(int)Tipo.LunaDeSangre], true); }

        /// Tormenta al tomar la ballesta de la mina (pedido 2026-11-12; la escopeta sigue despertando la luna de sangre).
        public void TormentaPorBallesta() { Iniciar(Tipo.Tormenta, Duracion[(int)Tipo.Tormenta], true); }

        void Enviar()
        {
            if (!plugin.Conectado) return;
            var lista = new List<KeyValuePair<byte, float>>();
            foreach (var kv in activos) lista.Add(new KeyValuePair<byte, float>((byte)kv.Key, Mathf.Max(0f, kv.Value - Time.time)));
            plugin.EnviarEvento(Red.Armar(w => { w.Write((byte)lista.Count); foreach (var e in lista) { w.Write(e.Key); w.Write(e.Value); } }));
        }

        public void Recibir(BinaryReader r)
        {
            int n = r.ReadByte();
            var recibidos = new Dictionary<Tipo, float>();
            for (int i = 0; i < n; i++) { var tipo = (Tipo)r.ReadByte(); float quedan = r.ReadSingle(); if (tipo != Tipo.Ninguno) recibidos[tipo] = quedan; }
            if (plugin.Escena != Mundo.EscenaBosque) return;
            foreach (var kv in recibidos)
            {
                if (Esta(kv.Key)) activos[kv.Key] = Time.time + kv.Value;
                else Iniciar(kv.Key, kv.Value, false);
            }
            // lo que el anfitrión ya no tiene, el invitado también lo termina (el anfitrión no se corrige con lo del invitado)
            if (!plugin.EsAnfitrion)
            {
                var sobran = new List<Tipo>();
                foreach (var kv in activos) if (!recibidos.ContainsKey(kv.Key)) sobran.Add(kv.Key);
                foreach (var t0 in sobran) Terminar(t0, false);
            }
        }

        // ---------------- Efectos ----------------

        void Guardar()
        {
            if (guardado) return;
            guardado = true;
            fogAntes = RenderSettings.fog; modoAntes = RenderSettings.fogMode; colorAntes = RenderSettings.fogColor;
            densAntes = RenderSettings.fogDensity; iniAntes = RenderSettings.fogStartDistance; finAntes = RenderSettings.fogEndDistance;
            ambienteAntes = RenderSettings.ambientLight; cieloAntes = RenderSettings.skybox;
        }

        void Aplicar(Tipo tipo)
        {
            switch (tipo)
            {
                case Tipo.LunaDeSangre:
                    CrearLuna();
                    CrearLluvia(true);   // llueve sangre (pedido 2026-11-19)
                    campanadas = 0; proximaCampana = Time.time;   // campanas al empezar
                    break;
                case Tipo.Niebla:
                    CrearHumo();   // volutas de humo gris bajas que se mueven (la neblina leve la pone Componer)
                    break;
                case Tipo.NocheOscura:
                    Ilusiones.Reiniciar();
                    break;
                case Tipo.NocheTranquila:
                    if (plugin.EsAnfitrion) OcultarMitad();
                    break;
                case Tipo.Tormenta:
                    AtenuarLuces(0.45f);
                    if (!Esta(Tipo.NocheOscura)) CamarasGrises(new Color(0.13f, 0.14f, 0.17f));
                    CrearLluvia();
                    flashInicio = Time.realtimeSinceStartup;   // destello blanco al empezar la tormenta
                    destello = 1f; truenoEn = Time.time + 0.5f;
                    proximoRayo = Time.time + 3f;
                    if (plugin.EsAnfitrion) { refuerzosMeta = -1; proximoRefuerzo = Time.time + 4f; }
                    break;
            }
        }

        /// El ambiente (niebla, luz ambiente, cielo y tinte de la luz) se compone cada cuadro con todos los eventos activos:
        /// de claro a oscuro (noche oscura), de natural a rojo (luna), la neblina de la niebla y el aire cerrado de la tormenta.
        /// Las capas lineales (oscura, tormenta) mandan sobre las exponenciales (luna, niebla); donde coinciden se mezclan.
        void Componer(float t)
        {
            bool osc = Esta(Tipo.NocheOscura), tor = Esta(Tipo.Tormenta), lun = Esta(Tipo.LunaDeSangre), nie = Esta(Tipo.Niebla);
            if (!osc && !tor && !lun && !nie) return;
            float ko = osc ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - inicios[Tipo.NocheOscura]) / RampaOscura)) : 0f;
            float kl = lun ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - inicios[Tipo.LunaDeSangre]) / RampaLuna)) : 0f;
            Color fogC = colorAntes, amb = ambienteAntes; float dens = densAntes;
            if (nie) { fogC = new Color(0.36f, 0.38f, 0.41f); }   // gris medio, como la niebla de Silent Hill 2 (no blanca)   // oscuro como el cielo: así los árboles se funden con el gas y no se ven como siluetas claras   // gas azulado suave (antes gris: parecía un filtro blanco y negro)
            if (lun)
            {
                dens = Mathf.Lerp(dens, Mathf.Max(dens, 0.008f), kl);
                fogC = Color.Lerp(fogC, new Color(0.30f, 0.05f, 0.04f), kl);
                amb = Color.Lerp(amb, new Color(0.38f, 0.10f, 0.08f) + amb * 0.25f, kl);
            }
            if (osc || tor)
            {
                float ini = float.MaxValue, fin = float.MaxValue;
                if (tor) { ini = 12f; fin = 150f; fogC = Color.Lerp(fogC, new Color(0.15f, 0.16f, 0.19f), 0.85f); amb = amb * 0.55f; }
                if (osc)
                {
                    ini = Mathf.Min(ini, Mathf.Lerp(150f, 8f, ko)); fin = Mathf.Min(fin, Mathf.Lerp(500f, 50f, ko));
                    fogC = Color.Lerp(fogC, Color.black, ko); amb = Color.Lerp(amb, amb * 0.5f, ko);
                }
                RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear; RenderSettings.fogStartDistance = ini; RenderSettings.fogEndDistance = fin;
            }
            else if (nie)
            {
                // Niebla (pedido 2026-11-11): gas borroso, apenas se ve más allá de ~10 m; entra poco a poco
                float kn = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - inicios[Tipo.Niebla]) / 10f));
                RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear;
                RenderSettings.fogStartDistance = Mathf.Lerp(Mathf.Max(iniAntes, 60f), 0f, kn);   // muy espesa: a partir de ~8 m no se ve nada
                RenderSettings.fogEndDistance = Mathf.Lerp(Mathf.Max(finAntes, 300f), 10f, kn);   // visión de ~10 m
            }
            else { RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared; RenderSettings.fogDensity = dens; }
            RenderSettings.fogColor = fogC; RenderSettings.ambientLight = amb;
            if (nie && !osc && !camarasNiebla && Time.time - inicios[Tipo.Niebla] > 6f) { camarasNiebla = true; CamarasNiebla(fogC); }   // más allá de la niebla no hay nada: ni árboles lejanos ni montañas ni cielo (como la noche oscura, pero gris)
            if (osc && ko > 0.6f && !cielosNegros) { cielosNegros = true; CamarasNegras(true); }   // el cielo se apaga cuando ya está oscuro
            if (lun && Time.realtimeSinceStartup > ultimaTinta + 0.4f)
            {
                ultimaTinta = Time.realtimeSinceStartup;
                foreach (var l in Object.FindObjectsOfType<Light>())
                {
                    if (l.type != LightType.Directional) continue;
                    if (!lucesAntes.ContainsKey(l)) lucesAntes[l] = l.color;
                    l.color = Color.Lerp(lucesAntes[l], new Color(1f, 0.38f, 0.28f), 0.75f * kl);   // rojo, pero sin tapar el bosque
                }
            }
        }

        void Mantener(float t)
        {
            // (las nubes de la niebla se mueven solas en LateUpdate, SeguirCamaraNiebla)
            Componer(t);
            if (Esta(Tipo.LunaDeSangre) && t > proximoBarrido)
            {
                proximoBarrido = t + 2f;
                // Todo enemigo activo, potenciado mientras dure la luna (también los que aparezcan).
                foreach (var td in Object.FindObjectsOfType<TakeDamage>())
                {
                    if (td == null || !td.gameObject.activeInHierarchy || td.GetComponentInParent<FPSPlayer>() != null) continue;
                    if (Potenciado.Es(td) || Aliados.Protegido(td.transform)) continue;
                    Transform u = Combate.RaizDe(td);
                    if (u == null || u.name.StartsWith("coop_aliado")) continue;
                    var raiz = u.parent != null ? u.parent.gameObject : u.gameObject;
                    Potenciado.Aplicar(raiz);
                    potenciadosLuna.Add(raiz);
                }
                if (luna == null) CrearLuna();
            }
            if (Esta(Tipo.LunaDeSangre) && t >= proximaCampana)
            {
                // Al empezar: la entrada (1 s de silencio, campanada fuerte con eco, repique, y empalme);
                // cuando termina la entrada, la campana sola en bucle de fondo hasta que termine la luna.
                Campana.TocarEntrada(1f);
                Campana.ProgramarFondo(Campana.DuracionEntrada(), 0.3f);   // tras la entrada fuerte, la campana queda baja
                campanadas++;
                proximaCampana = float.MaxValue;
            }
            if (Esta(Tipo.LunaDeSangre) && luna != null && Camera.main != null)
            {
                // la luna sigue a la cámara, siempre en el mismo punto del cielo
                // punto FIJO del cielo (dirección elegida al empezar, a 40° de altura): no se mueve con la cámara
                // y queda dentro del alcance de dibujo y por detrás de lo que tenga delante (árboles).
                luna.SetActive(true);
                float dist = Mathf.Min(300f, Camera.main.farClipPlane * 0.8f);
                luna.transform.position = Camera.main.transform.position + dirLuna * dist;
                luna.transform.localScale = Vector3.one * 330f * (dist / 600f);   // grande: la cara se ve bien
                luna.transform.LookAt(Camera.main.transform);
                AnimarSangreLuna();
            }
            if (Esta(Tipo.NocheOscura) && plugin.Jugador != null) Ilusiones.Actualizar(plugin.Jugador.transform);
            // (sin volutas grises: el gas es la propia niebla)
            if (Esta(Tipo.Tormenta)) MantenerTormenta(t); else if (lluvia != null) MoverLluvia();
            if (Esta(Tipo.NocheRuidosa) && plugin.EsAnfitrion && t > proximaHorda)
            {
                proximaHorda = t + 40f;
                Horda();
            }
        }

        void TeñirLuces(Color c)
        {
            foreach (var l in Object.FindObjectsOfType<Light>())
            {
                if (l.type != LightType.Directional) continue;
                if (!lucesAntes.ContainsKey(l)) lucesAntes[l] = l.color;
                l.color = c;
            }
        }

        bool camarasNiebla; readonly Dictionary<Camera, float> lejosAntes = new Dictionary<Camera, float>();
        /// Con la niebla el fondo es del color de la niebla y la cámara no dibuja nada a más de ~14 m (los árboles lejanos se veían como siluetas blancas porque no reciben la niebla).
        void CamarasNiebla(Color c)
        {
            foreach (var cam in Camera.allCameras)
            {
                if (!camarasAntes.ContainsKey(cam)) camarasAntes[cam] = new KeyValuePair<CameraClearFlags, Color>(cam.clearFlags, cam.backgroundColor);
                if (cam.clearFlags == CameraClearFlags.Skybox || cam.clearFlags == CameraClearFlags.SolidColor) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = c; }
                if (!lejosAntes.ContainsKey(cam)) lejosAntes[cam] = cam.farClipPlane;
                cam.farClipPlane = Mathf.Min(cam.farClipPlane, 14f);
            }
            RenderSettings.skybox = null;
        }

        void CamarasNegras(bool si)
        {
            foreach (var cam in Camera.allCameras)
            {
                if (si)
                {
                    if (!camarasAntes.ContainsKey(cam)) camarasAntes[cam] = new KeyValuePair<CameraClearFlags, Color>(cam.clearFlags, cam.backgroundColor);
                    if (cam.clearFlags == CameraClearFlags.Skybox || cam.clearFlags == CameraClearFlags.SolidColor) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black; }
                }
            }
            if (si) RenderSettings.skybox = null;
        }

        // ---------------- Sangre que baja por la pantalla ----------------

        static Texture2D texSangreCuerpo, texSangreGota, texDegradado;

        static void TexturasSangre()
        {
            if (texSangreCuerpo != null) return;
            // cuerpo del chorro: perfil horizontal (borde suave, núcleo oscuro, brillo húmedo en un lado), se estira a lo largo
            texSangreCuerpo = new Texture2D(32, 4, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int x = 0; x < 32; x++)
            {
                float u = (x + 0.5f) / 32f, c = Mathf.Abs(u - 0.5f) * 2f;
                float a = Mathf.Clamp01((1f - c) * 3.2f);
                float oscuro = 0.45f + 0.35f * c;
                float brillo = Mathf.Exp(-Mathf.Pow((u - 0.33f) / 0.08f, 2f)) * 0.55f;
                Color col = new Color(0.36f * oscuro + brillo * 0.55f, 0.012f + brillo * 0.12f, 0.012f + brillo * 0.1f, a * 0.93f);
                for (int y = 0; y < 4; y++) texSangreCuerpo.SetPixel(x, y, col);
            }
            texSangreCuerpo.Apply();
            // gota: bulbo ancho abajo que se afina hacia arriba, con reflejo
            texSangreGota = new Texture2D(32, 48, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 48; y++) for (int x = 0; x < 32; x++)
            {
                float v = y / 47f;   // 0 abajo, 1 arriba
                float ancho = v < 0.45f ? Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow((0.45f - v) / 0.45f, 2f))) : Mathf.Lerp(1f, 0.45f, (v - 0.45f) / 0.55f);
                float u = (x + 0.5f) / 32f, c = Mathf.Abs(u - 0.5f) * 2f / Mathf.Max(0.05f, ancho * 0.95f);
                float a = Mathf.Clamp01((1f - c) * 4f);
                float oscuro = 0.45f + 0.35f * Mathf.Clamp01(c);
                float brillo = Mathf.Exp(-(Mathf.Pow((u - 0.36f) / 0.09f, 2f) + Mathf.Pow((v - 0.3f) / 0.18f, 2f))) * 0.85f;
                texSangreGota.SetPixel(x, y, new Color(0.36f * oscuro + brillo * 0.6f, 0.012f + brillo * 0.16f, 0.012f + brillo * 0.14f, a * 0.95f));
            }
            texSangreGota.Apply();
        }

        /// La sangre cae desde el borde de arriba de la pantalla: una mancha que escurre en el borde y muchos chorros viscosos que bajan frenándose, con su gota al final.
        void DibujarSangrePantalla(float t, float dur, float W, float H)
        {
            TexturasSangre();
            var pc = GUI.color;
            float salida = Mathf.Clamp01((t - (dur - 1.4f)) / 1.4f);
            // (sin charco en el borde de arriba: solo los chorros que bajan; pedido 2026-11-13)
            // chorros
            const int N = 26;
            for (int i = 0; i < N; i++)
            {
                var rg = new System.Random(i * 7919 + 11);
                float x = ((float)rg.NextDouble() * 0.96f + 0.02f) * W;
                float w = 5f + (float)rg.NextDouble() * (i % 5 == 0 ? 16f : 9f);
                float ini = 0.5f + (float)rg.NextDouble() * 3.2f;
                float maxL = H * (0.2f + (float)rg.NextDouble() * 0.6f);
                float tau = 1.3f + (float)rg.NextDouble() * 2.4f;
                float u = t - ini; if (u <= 0f) continue;
                // viscoso: arranca rápido y se frena; deja de bajar casi del todo
                float largo = maxL * (1f - Mathf.Exp(-u / tau)) + u * 3f;
                largo = Mathf.Min(largo, maxL * 1.08f);
                if (largo < 4f) continue;
                float alfa = 1f - salida;
                // cuerpo en tramos de ~14 px: ancho y posición varían despacio (irregular y más grueso arriba)
                float yy = 0f; float desfase = 0f;
                while (yy < largo)
                {
                    float seg = Mathf.Min(14f, largo - yy);
                    float tapiz = Mathf.Lerp(1.25f, 0.8f, yy / Mathf.Max(60f, maxL));
                    float wob = (Mathf.PerlinNoise(i * 3.7f, yy * 0.045f) - 0.5f) * 0.55f;
                    desfase = Mathf.Lerp(desfase, (Mathf.PerlinNoise(i * 1.3f + 9f, yy * 0.012f) - 0.5f) * 10f, 0.3f);
                    float ww = Mathf.Max(3f, w * tapiz * (1f + wob));
                    GUI.color = new Color(1f, 1f, 1f, alfa);
                    GUI.DrawTexture(new Rect(x - ww / 2f + desfase, yy - 1f, ww, seg + 2f), texSangreCuerpo);
                    yy += seg;
                }
                // la gota del final
                float gw = w * 1.55f + 3f, gh = gw * 1.5f;
                GUI.color = new Color(1f, 1f, 1f, alfa);
                GUI.DrawTexture(new Rect(x - gw / 2f + desfase, largo - gh * 0.55f, gw, gh), texSangreGota);
                // de vez en cuando una gotita se desprende y cae sola
                if (i % 3 == 0 && u > 1.5f)
                {
                    float cae = ((u * 0.35f + i * 0.37f) % 1f);
                    float gy = largo + cae * cae * H * 0.35f;
                    if (gy < H) { float g2 = w * 0.7f + 2f; GUI.color = new Color(1f, 1f, 1f, alfa * (1f - cae)); GUI.DrawTexture(new Rect(x - g2 / 2f + desfase, gy, g2, g2 * 1.4f), texSangreGota); }
                }
            }
            GUI.color = pc;
        }

        /// Una luna roja de verdad: disco con mares oscuros, cráteres con borde iluminado, rayos de eyección, oscurecimiento hacia el borde y un halo rojizo difuso.
        static Texture2D TexLuna(int n)
        {
            var rg = new System.Random(2077);
            const int NC = 110;
            var cx = new float[NC]; var cy = new float[NC]; var cr = new float[NC];
            for (int i = 0; i < NC; i++)
            {
                float a = (float)rg.NextDouble() * 6.283f, rr = Mathf.Sqrt((float)rg.NextDouble()) * 0.62f;
                cx[i] = 0.5f + Mathf.Cos(a) * rr; cy[i] = 0.5f + Mathf.Sin(a) * rr;
                float u = (float)rg.NextDouble(); cr[i] = 0.012f + u * u * u * 0.075f;   // muchos pequeños, pocos grandes
            }
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color[n * n];
            Vector2 luz = new Vector2(-0.6f, 0.8f);   // la luz viene de arriba a la izquierda: los bordes de los cráteres de ese lado brillan
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u0 = (x + 0.5f) / n, v0 = (y + 0.5f) / n;
                    float dx = u0 - 0.5f, dy = v0 - 0.5f; float d = Mathf.Sqrt(dx * dx + dy * dy) / 0.5f;   // 0 centro, 1 borde de la textura
                    const float R = 0.6f;   // radio del disco dentro de la textura (el resto es halo)
                    // halo: gaussiana roja que se apaga con la distancia al disco
                    float halo = Mathf.Exp(-Mathf.Pow(Mathf.Max(0f, d - R) / 0.24f, 2f)) * 0.5f;
                    float disco = Mathf.Clamp01((R - d) * 70f);   // borde suave de unos píxeles
                    Color haloC = new Color(1f, 0.16f, 0.07f, halo * halo * (1f - Mathf.SmoothStep(0.78f, 1f, d)));
                    if (disco <= 0f) { px[y * n + x] = haloC; continue; }
                    float r = d / R;                      // 0..1 dentro del disco
                    float esfera = Mathf.Sqrt(Mathf.Max(0f, 1f - r * r));
                    float brillo = 0.62f + 0.38f * esfera;   // más oscuro hacia el borde
                    // mares: manchas grandes y oscuras de baja frecuencia
                    float mar = Mathf.PerlinNoise(u0 * 3.2f + 7f, v0 * 3.2f + 2f) * 0.6f + Mathf.PerlinNoise(u0 * 7f, v0 * 7f + 5f) * 0.4f;
                    float oscuro = Mathf.SmoothStep(0.48f, 0.62f, mar);
                    float grano = 0.9f + 0.2f * Mathf.PerlinNoise(u0 * 60f, v0 * 60f);
                    float tono = brillo * (1f - 0.42f * oscuro) * grano;
                    // cráteres
                    float cra = 0f;
                    for (int i = 0; i < NC; i++)
                    {
                        float ex = u0 - cx[i], ey = v0 - cy[i]; if (Mathf.Abs(ex) > cr[i] * 1.5f || Mathf.Abs(ey) > cr[i] * 1.5f) continue;
                        float dd = Mathf.Sqrt(ex * ex + ey * ey) / cr[i];
                        if (dd > 1.45f) continue;
                        float borde = Mathf.Exp(-Mathf.Pow((dd - 1f) / 0.16f, 2f));
                        float lado = Mathf.Clamp((ex * luz.x + ey * luz.y) / (cr[i] * Mathf.Max(dd, 0.2f)), -1f, 1f);   // 1 si el punto mira a la luz
                        cra += borde * (0.35f + 0.55f * lado);   // reborde claro a un lado, sombra al otro
                        if (dd < 1f) cra -= 0.3f * (1f - dd * dd) * (0.7f - 0.3f * lado);   // el fondo es más oscuro
                    }
                    tono = Mathf.Clamp(tono + cra * 0.45f, 0.08f, 1.25f);
                    // la cara: se oscurece primero el relieve alrededor (ojos hundidos)
                    float fx = u0 - 0.5f, fy = v0 - 0.5f;   // centro de la luna; y hacia arriba
                    Color capaCara = Color.clear;
                    // color: de un rojo ladrillo oscuro a un naranja-rojo vivo según el brillo
                    Color c = Color.Lerp(new Color(0.28f, 0.03f, 0.02f), new Color(1f, 0.42f, 0.2f), Mathf.Clamp01(tono * 0.95f));
                    if (tono > 1f) c = Color.Lerp(c, new Color(1f, 0.7f, 0.5f), Mathf.Clamp01((tono - 1f) * 2f));
                    // ojos: dos óvalos negros enteros, algo inclinados hacia adentro, con un brillo blanco pequeño
                    for (int ojo = -1; ojo <= 1; ojo += 2)
                    {
                        float ox = fx - ojo * 0.098f, oy = fy - 0.052f;
                        float ang = ojo * 0.22f; float rx = ox * Mathf.Cos(ang) + oy * Mathf.Sin(ang), ry = -ox * Mathf.Sin(ang) + oy * Mathf.Cos(ang);
                        float e = Mathf.Sqrt(Mathf.Pow(rx / 0.046f, 2f) + Mathf.Pow(ry / 0.066f, 2f));
                        float negro = Mathf.Clamp01((1f - e) * 12f);
                        if (negro > 0f) c = Color.Lerp(c, new Color(0.01f, 0f, 0f), negro);
                        float gx = (fx - (ojo * 0.098f - ojo * 0.012f)) / 0.0105f, gy = (fy - 0.078f) / 0.0105f;   // brillo en la parte alta de adentro
                        float glint = Mathf.Clamp01((1f - Mathf.Sqrt(gx * gx + gy * gy)) * 6f);
                        if (glint > 0f) c = Color.Lerp(c, new Color(1f, 0.97f, 0.95f), glint);
                    }
                    // nariz: dos fosas y una sombra pequeña
                    {
                        float nx = fx, ny = fy + 0.028f;
                        float sombra = Mathf.Clamp01(1f - Mathf.Sqrt(Mathf.Pow(nx / 0.026f, 2f) + Mathf.Pow((ny + 0.004f) / 0.02f, 2f)));
                        c = Color.Lerp(c, c * 0.45f, sombra * 0.7f);
                        foreach (float sg in new[] { -1f, 1f })
                        {
                            float fe = Mathf.Sqrt(Mathf.Pow((nx - sg * 0.011f) / 0.0085f, 2f) + Mathf.Pow((ny + 0.002f) / 0.0055f, 2f));
                            c = Color.Lerp(c, new Color(0.02f, 0f, 0f), Mathf.Clamp01((1f - fe) * 8f));
                        }
                    }
                    // boca: una sonrisa enorme y desencajada que casi cruza la cara, de comisura a comisura, con dientes afilados arriba y abajo
                    {
                        float mx = fx / 0.225f;
                        if (Mathf.Abs(mx) < 1f)
                        {
                            float arriba = -0.062f + 0.115f * mx * mx;                          // labio de arriba: las comisuras suben casi hasta los ojos
                            float abajo = arriba - 0.125f * (1f - Mathf.Abs(mx * mx * mx));    // la boca se abre mucho hacia abajo en el centro
                            float dentro = Mathf.Clamp01(Mathf.Min(fy - abajo, arriba - fy) * 220f);
                            if (dentro > 0f)
                            {
                                float prof = Mathf.Clamp01((arriba - fy) / Mathf.Max(0.001f, arriba - abajo));
                                Color interior = Color.Lerp(new Color(0.16f, 0f, 0f), new Color(0.015f, 0f, 0f), Mathf.Sin(prof * Mathf.PI));   // negro con un fondo rojo oscuro
                                c = Color.Lerp(c, interior, dentro);
                            }
                            // dientes puntiagudos: triángulos desde el labio de arriba hacia abajo y desde el de abajo hacia arriba (más pequeños hacia las comisuras)
                            float alto = (arriba - abajo) * 0.34f * (1f - Mathf.Abs(mx) * 0.55f);
                            float nDient = 15f;
                            float uu = (mx + 1f) * 0.5f * nDient, fr = uu - Mathf.Floor(uu), tri = 1f - Mathf.Abs(fr * 2f - 1f);   // 0..1..0 por diente
                            float dSup = arriba - fy;   // distancia bajo el labio de arriba
                            if (dentro > 0.3f && dSup > 0f && dSup < alto * tri) c = Color.Lerp(c, new Color(0.9f, 0.84f, 0.7f) * (0.65f + 0.35f * (1f - dSup / Mathf.Max(0.001f, alto * tri))), Mathf.Clamp01((alto * tri - dSup) * 260f));
                            float uu2 = (mx + 1f) * 0.5f * (nDient - 1f) + 0.5f, fr2 = uu2 - Mathf.Floor(uu2), tri2 = 1f - Mathf.Abs(fr2 * 2f - 1f);
                            float dInf = fy - abajo;    // distancia sobre el labio de abajo
                            if (dentro > 0.3f && dInf > 0f && dInf < alto * 0.8f * tri2) c = Color.Lerp(c, new Color(0.86f, 0.8f, 0.66f) * (0.6f + 0.4f * (1f - dInf / Mathf.Max(0.001f, alto * 0.8f * tri2))), Mathf.Clamp01((alto * 0.8f * tri2 - dInf) * 260f));
                            // contorno oscuro de los labios
                            float lab = Mathf.Clamp01((0.0055f - Mathf.Min(Mathf.Abs(fy - arriba), Mathf.Abs(fy - abajo))) * 130f);
                            if (lab > 0f) c = Color.Lerp(c, new Color(0.14f, 0.005f, 0.005f), lab * 0.9f);
                        }
                    }
                    float a = Mathf.Max(disco, haloC.a);
                    c = Color.Lerp(haloC, c, disco);
                    px[y * n + x] = new Color(c.r, c.g, c.b, a);
                }
            tex.SetPixels(px); tex.Apply();
            return tex;
        }

        void CrearLuna()
        {
            if (luna != null) return;
            luna = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.Destroy(luna.GetComponent<Collider>());
            luna.name = "coop_luna_de_sangre";
            if (Camera.main != null) dirLuna = Quaternion.Euler(-40f, Camera.main.transform.eulerAngles.y - 25f, 0f) * Vector3.forward;
            luna.transform.localScale = Vector3.one * 330f;
            var tex = TexLuna(384);
            var m = new Material(Shader.Find("Sprites/Default")) { mainTexture = tex, name = "coop_luna" };
            // cola normal (transparente): se dibuja después del cielo y los árboles que tenga delante la tapan por profundidad
            luna.GetComponent<Renderer>().sharedMaterial = m;
            luna.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            CrearSangreLuna();
        }

        /// Sangre que escurre de los ojos y de la boca de la luna (hijos de la luna; se animan en AnimarSangreLuna).
        void CrearSangreLuna()
        {
            TexturasSangre(); gotasLuna.Clear();
            // x, y (coordenadas de la luna: 1 = ancho de la imagen), ancho, largo máx., velocidad, fase
            float[][] def = {
                new[] { -0.098f, 0.0f, 0.016f, 0.20f, 0.07f, 0.00f }, new[] { -0.114f, 0.012f, 0.008f, 0.12f, 0.1f, 0.37f },
                new[] {  0.098f, 0.0f, 0.016f, 0.24f, 0.06f, 0.55f }, new[] {  0.082f, 0.008f, 0.008f, 0.14f, 0.09f, 0.81f },
                new[] { -0.205f, 0.005f, 0.012f, 0.16f, 0.075f, 0.2f }, new[] { -0.12f, -0.13f, 0.014f, 0.19f, 0.07f, 0.35f },
                new[] { -0.04f, -0.178f, 0.016f, 0.24f, 0.06f, 0.62f }, new[] {  0.05f, -0.175f, 0.014f, 0.2f, 0.08f, 0.9f },
                new[] {  0.13f, -0.119f, 0.013f, 0.17f, 0.085f, 0.5f }, new[] {  0.205f, 0.005f, 0.012f, 0.15f, 0.09f, 0.1f },
            };
            foreach (var d in def)
            {
                var g = new GotaLuna { X = d[0], Y = d[1], Ancho = d[2], L = d[3], Vel = d[4], Fase = d[5] };
                var mc = new Material(Shader.Find("Sprites/Default")) { mainTexture = texSangreCuerpo, name = "coop_luna_sangre" }; mc.renderQueue = 3001;
                var mg = new Material(Shader.Find("Sprites/Default")) { mainTexture = texSangreGota, name = "coop_luna_gota" }; mg.renderQueue = 3002;
                g.MC = mc; g.MG = mg;
                var b = GameObject.CreatePrimitive(PrimitiveType.Quad); Object.Destroy(b.GetComponent<Collider>()); b.transform.SetParent(luna.transform, false); b.GetComponent<Renderer>().sharedMaterial = mc;
                var t = GameObject.CreatePrimitive(PrimitiveType.Quad); Object.Destroy(t.GetComponent<Collider>()); t.transform.SetParent(luna.transform, false); t.GetComponent<Renderer>().sharedMaterial = mg;
                b.GetComponent<Renderer>().shadowCastingMode = t.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                g.Cuerpo = b.transform; g.Gota = t.transform; gotasLuna.Add(g);
            }
        }

        void AnimarSangreLuna()
        {
            foreach (var g in gotasLuna)
            {
                if (g.Cuerpo == null) continue;
                float p = (Time.time * g.Vel + g.Fase) % 1f;
                float crece = Mathf.Min(1f, p / 0.8f);
                float largo = g.L * (1f - Mathf.Exp(-3.4f * crece)) / (1f - Mathf.Exp(-3.4f));   // viscoso: baja rápido y se frena
                float alfa = p < 0.85f ? 1f : Mathf.Clamp01((1f - p) / 0.15f);
                if (p > 0.9f) largo += (p - 0.9f) * g.L * 1.4f;   // al final la gota se desprende y cae
                g.MC.color = new Color(1f, 1f, 1f, alfa); g.MG.color = new Color(1f, 1f, 1f, alfa);
                float z = 0.002f;
                float cuerpoL = Mathf.Max(0.0001f, p > 0.9f ? g.L * (1f - Mathf.Exp(-3.4f * 0.99f)) / (1f - Mathf.Exp(-3.4f)) : largo);
                g.Cuerpo.localScale = new Vector3(g.Ancho, cuerpoL, 1f);
                g.Cuerpo.localPosition = new Vector3(g.X, g.Y - cuerpoL / 2f, z);
                g.Gota.localScale = new Vector3(g.Ancho * 1.8f, g.Ancho * 2.7f, 1f);
                g.Gota.localPosition = new Vector3(g.X, g.Y - largo, z + 0.0005f);
            }
        }

        /// Noche tranquila: el anfitrión apaga la mitad de los enemigos comunes (el invitado oculta los
        /// que el anfitrión no tiene).
        void OcultarMitad()
        {
            int n = 0;
            foreach (var td in Object.FindObjectsOfType<TakeDamage>())
            {
                if (td == null || !td.gameObject.activeInHierarchy || td.GetComponentInParent<FPSPlayer>() != null || Dificultad.EsJefe(td) || Aliados.Protegido(td.transform)) continue;
                Transform u = Combate.RaizDe(td);
                var raiz = u.parent != null ? u.parent.gameObject : u.gameObject;
                if (raiz.name.StartsWith("coop_") || ocultos.Contains(raiz)) continue;
                if ((n++ & 1) == 0) { raiz.SetActive(false); ocultos.Add(raiz); }
            }
            plugin.Log("Evento: " + ocultos.Count + " enemigos ocultos por la noche tranquila");
        }

        /// Noche ruidosa: el anfitrión hace aparecer 3 enemigos más cerca de un jugador al azar.
        void Horda()
        {
            var c = plugin.CombateActual;
            if (c == null) return;
            var tipos = new List<string>(Mundo.Guardianes);   // los que se pueden generar (los de los puestos)
            if (tipos.Count == 0) return;
            var comp = plugin.CompaneroActual;
            Vector3 centro = comp != null && comp.Visible && azar.Next(2) == 0 ? comp.PosicionMundo : plugin.Jugador.transform.position;
            int hechos = 0;
            for (int k = 0; k < 3; k++)
            {
                float ang = (float)azar.NextDouble() * Mathf.PI * 2f, dist = 35f + (float)azar.NextDouble() * 20f;
                Vector3 p = centro + new Vector3(Mathf.Cos(ang) * dist, 0f, Mathf.Sin(ang) * dist);
                bool pot = azar.NextDouble() < Potenciado.ProbabilidadActual;
                var td = c.Generar(tipos[azar.Next(tipos.Count)] + (pot ? Potenciado.Marca : ""), p);
                if (td != null) hechos++;
            }
            plugin.Log("Evento: noche ruidosa, " + hechos + " enemigos más");
        }

        void TerminarTodos(bool avisar)
        {
            foreach (var t in new List<Tipo>(activos.Keys)) Terminar(t, avisar);
        }

        /// Termina un evento y deshace solo lo suyo; el ambiente de los que sigan activos se recompone solo.
        void Terminar(Tipo tipo, bool avisar)
        {
            if (!activos.Remove(tipo)) return;
            inicios.Remove(tipo);
            switch (tipo)
            {
                case Tipo.LunaDeSangre:
                    foreach (var kv in lucesAntes) if (kv.Key != null) kv.Key.color = kv.Value;
                    lucesAntes.Clear();
                    foreach (var g in potenciadosLuna) if (g != null) Potenciado.Quitar(g);
                    potenciadosLuna.Clear();
                    if (luna != null) { Object.Destroy(luna); luna = null; }
                    if (Esta(Tipo.Tormenta)) TeñirLluvia(false); else QuitarLluvia();   // deja de llover sangre
                    Campana.Detener();   // se acaban las campanas con la luna
                    proximaCampana = float.MaxValue;
                    break;
                case Tipo.Tormenta:
                    QuitarTormenta();
                    if (Esta(Tipo.LunaDeSangre)) CrearLluvia(true);   // sigue la luna: sigue lloviendo sangre
                    if (Esta(Tipo.NocheOscura)) { cielosNegros = false; }   // el cielo negro de la noche oscura sigue (Componer lo repone)
                    else RestaurarCamaras();
                    break;
                case Tipo.NocheOscura:
                    cielosNegros = false;
                    RestaurarCamaras();
                    if (Esta(Tipo.Tormenta)) CamarasGrises(new Color(0.13f, 0.14f, 0.17f));   // sigue la tormenta: vuelve su cielo gris
                    else RenderSettings.skybox = cieloAntes;
                    break;
                case Tipo.Niebla:
                    QuitarHumo();
                    break;
                case Tipo.NocheTranquila:
                    foreach (var g in ocultos) if (g != null) g.SetActive(true);
                    ocultos.Clear();
                    break;
            }
            if (activos.Count == 0)
            {
                // se acabó el último: todo vuelve a como estaba
                if (guardado)
                {
                    RenderSettings.fog = fogAntes; RenderSettings.fogMode = modoAntes; RenderSettings.fogColor = colorAntes;
                    RenderSettings.fogDensity = densAntes; RenderSettings.fogStartDistance = iniAntes; RenderSettings.fogEndDistance = finAntes;
                    RenderSettings.ambientLight = ambienteAntes; RenderSettings.skybox = cieloAntes;
                    guardado = false;
                }
                foreach (var kv in lucesAntes) if (kv.Key != null) kv.Key.color = kv.Value;
                lucesAntes.Clear();
                RestaurarCamaras();
                Actual = Tipo.Ninguno;
            }
            else
            {
                // el título/color de pantalla pasa al más reciente de los que quedan
                Tipo mas = Tipo.Ninguno; float t0 = float.MinValue;
                foreach (var kv in inicios) if (kv.Value > t0) { t0 = kv.Value; mas = kv.Key; }
                Actual = mas;
            }
            plugin.Log("Evento: terminó " + Titulo[(int)tipo] + " (quedan " + activos.Count + ")");
            if (avisar && plugin.EsAnfitrion && plugin.Conectado) Enviar();
        }

        void RestaurarCamaras()
        {
            foreach (var kv in camarasAntes) if (kv.Key != null) { kv.Key.clearFlags = kv.Value.Key; kv.Key.backgroundColor = kv.Value.Value; }
            camarasAntes.Clear();
            foreach (var kv in lejosAntes) if (kv.Key != null) kv.Key.farClipPlane = kv.Value;
            lejosAntes.Clear(); camarasNiebla = false;
        }

        // ---------------- Tormenta ----------------

        float ultimaTinta; bool cielosNegros; float flashInicio = -1f;
        public const float RampaOscura = 14f, RampaLuna = 20f;
        Color ambienteTormenta; float proximoRayo, destello, truenoEn = -1f, proximoRefuerzo; int refuerzosMeta = -1;
        /// El sonido de lluvia de fondo está apagado (pedido 2026-11-12: se oía mal): la tormenta suena solo con los truenos.
        const bool SonidoLluvia = false;
        GameObject lluvia; AudioSource sonidoLluvia; float proxTecho; bool cubierto, bajoTechoSonido;
        readonly Dictionary<Light, float> intensidadAntes = new Dictionary<Light, float>();
        readonly List<TakeDamage> refuerzos = new List<TakeDamage>();

        void AtenuarLuces(float f)
        {
            foreach (var l in Object.FindObjectsOfType<Light>())
            {
                if (l.type != LightType.Directional) continue;
                if (!intensidadAntes.ContainsKey(l)) intensidadAntes[l] = l.intensity;
                l.intensity = intensidadAntes[l] * f;
            }
        }

        void CamarasGrises(Color c)
        {
            foreach (var cam in Camera.allCameras)
            {
                if (!camarasAntes.ContainsKey(cam)) camarasAntes[cam] = new KeyValuePair<CameraClearFlags, Color>(cam.clearFlags, cam.backgroundColor);
                if (cam.clearFlags == CameraClearFlags.Skybox || cam.clearFlags == CameraClearFlags.SolidColor) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = c; }
            }
            RenderSettings.skybox = null;
        }

        /// Lluvia: partículas estiradas que caen alrededor de la cámara (se quedan en el mundo al moverse).
        bool lluviaRoja;

        void CrearLluvia(bool sangre = false)
        {
            if (lluvia != null) { if (sangre && !lluviaRoja) TeñirLluvia(true); return; }
            lluviaRoja = false;
            lluvia = new GameObject("coop_lluvia");
            lluvia.transform.rotation = Quaternion.Euler(90f, 6f, 0f);   // la caja emite hacia +Z: hacia abajo (un poco de viento)
            var ps = lluvia.AddComponent<ParticleSystem>();
            ps.Stop();
            var main = ps.main;
            main.loop = true; main.startLifetime = 1.15f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(24f, 34f);   // gotas más variadas (más finas y más gruesas, más rápidas y más lentas)
            main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.055f);
            main.maxParticles = 16000; main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.7f, 0.76f, 0.88f, 0.22f), new Color(0.85f, 0.9f, 1f, 0.6f));
            var em = ps.emission; em.rateOverTime = 7800f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.box = new Vector3(46f, 46f, 0.5f);
            var r = lluvia.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.065f; r.lengthScale = 1.4f;
            // gota afilada: núcleo brillante, bordes suaves y cola que se desvanece (antes era una barra pareja)
            var tex = new Texture2D(8, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < 64; y++) for (int x = 0; x < 8; x++)
            {
                float v = y / 63f, ancho = Mathf.Lerp(0.15f, 1f, Mathf.Pow(v, 0.6f));
                float dx = Mathf.Abs(x - 3.5f) / 3.5f, borde = Mathf.Clamp01(1f - dx / ancho);
                float a = Mathf.Pow(borde, 1.6f) * Mathf.Pow(Mathf.Sin(v * Mathf.PI), 0.7f) * Mathf.Lerp(0.35f, 1f, v);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            tex.Apply();
            var sh2 = Shader.Find("Particles/Alpha Blended") ?? Shader.Find("Sprites/Default");
            var m = new Material(sh2) { mainTexture = tex, name = "coop_lluvia" };
            if (m.HasProperty("_TintColor")) m.SetColor("_TintColor", new Color(0.6f, 0.65f, 0.75f, 0.45f));
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            ps.Play();
            CrearSalpicaduras(m.shader);
            if (sangre) TeñirLluvia(true);
        }

        /// Lluvia de sangre (Luna de sangre, pedido 2026-11-19): las mismas gotas, salpicaduras y bruma, en rojo sangre.
        void TeñirLluvia(bool roja)
        {
            lluviaRoja = roja;
            if (lluvia != null)
            {
                var mm = lluvia.GetComponent<ParticleSystem>().main;
                mm.startColor = roja ? new ParticleSystem.MinMaxGradient(new Color(0.42f, 0.01f, 0.01f, 0.45f), new Color(0.7f, 0.04f, 0.03f, 0.85f))
                                     : new ParticleSystem.MinMaxGradient(new Color(0.7f, 0.76f, 0.88f, 0.22f), new Color(0.85f, 0.9f, 1f, 0.6f));
                var mat = lluvia.GetComponent<ParticleSystemRenderer>().sharedMaterial;
                if (mat != null && mat.HasProperty("_TintColor")) mat.SetColor("_TintColor", roja ? new Color(0.6f, 0.03f, 0.02f, 0.6f) : new Color(0.6f, 0.65f, 0.75f, 0.45f));
            }
            if (salpicaduras != null)
            {
                var ms = salpicaduras.GetComponent<ParticleSystem>().main;
                ms.startColor = roja ? new ParticleSystem.MinMaxGradient(new Color(0.5f, 0.02f, 0.02f, 0.5f), new Color(0.75f, 0.05f, 0.04f, 0.8f))
                                     : new ParticleSystem.MinMaxGradient(new Color(0.8f, 0.85f, 0.95f, 0.35f), new Color(0.9f, 0.93f, 1f, 0.6f));
                var mat = salpicaduras.GetComponent<ParticleSystemRenderer>().sharedMaterial;
                if (mat != null && mat.HasProperty("_TintColor")) mat.SetColor("_TintColor", roja ? new Color(0.55f, 0.03f, 0.02f, 0.55f) : new Color(0.55f, 0.6f, 0.7f, 0.4f));
            }
            if (brumaLluvia != null)
            {
                var mb = brumaLluvia.GetComponent<ParticleSystem>().main;
                mb.startColor = roja ? new Color(0.45f, 0.04f, 0.03f, 0.06f) : new Color(0.6f, 0.64f, 0.7f, 0.05f);
                var mat = brumaLluvia.GetComponent<ParticleSystemRenderer>().sharedMaterial;
                if (mat != null && mat.HasProperty("_TintColor")) mat.SetColor("_TintColor", roja ? new Color(0.45f, 0.05f, 0.04f, 0.25f) : new Color(0.5f, 0.52f, 0.58f, 0.25f));
            }
        }

        void QuitarLluvia()
        {
            if (lluvia != null) { Object.Destroy(lluvia); lluvia = null; }
            if (salpicaduras != null) { Object.Destroy(salpicaduras); salpicaduras = null; }
            if (brumaLluvia != null) { Object.Destroy(brumaLluvia); brumaLluvia = null; }
            lluviaRoja = false;
        }

        GameObject salpicaduras, brumaLluvia; AudioSource bucleLluvia; static AudioClip clipLluvia;

        /// Salpicaduras en el suelo alrededor del jugador (pequeños anillos que se abren y desaparecen) y una bruma de
        /// lluvia baja y tenue: dan cuerpo a la tormenta (pedido 2026-11-17: mejorar la calidad de la lluvia).
        void CrearSalpicaduras(Shader sh)
        {
            if (salpicaduras != null) return;
            salpicaduras = new GameObject("coop_lluvia_salpicaduras");
            salpicaduras.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            var ps = salpicaduras.AddComponent<ParticleSystem>(); ps.Stop();
            var main = ps.main; main.loop = true; main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.32f);
            main.startSpeed = 0f; main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.2f); main.maxParticles = 3000;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.8f, 0.85f, 0.95f, 0.35f), new Color(0.9f, 0.93f, 1f, 0.6f));
            var em = ps.emission; em.rateOverTime = 1400f;
            var shp = ps.shape; shp.shapeType = ParticleSystemShapeType.Box; shp.box = new Vector3(22f, 22f, 0.05f);
            var tam = ps.sizeOverLifetime; tam.enabled = true; tam.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.3f), new Keyframe(1f, 1.4f)));
            var col = ps.colorOverLifetime; col.enabled = true;
            var gr = new Gradient(); gr.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(gr);
            var r = salpicaduras.GetComponent<ParticleSystemRenderer>(); r.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            var t = new Texture2D(32, 32, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
            {
                float d = new Vector2(x - 15.5f, y - 15.5f).magnitude / 15.5f;
                float anillo = Mathf.Clamp01(1f - Mathf.Abs(d - 0.75f) / 0.18f), centro = Mathf.Clamp01(1f - d / 0.25f) * 0.5f;
                t.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Max(anillo, centro)));
            }
            t.Apply();
            var m = new Material(sh) { mainTexture = t, name = "coop_lluvia_salpicadura" };
            if (m.HasProperty("_TintColor")) m.SetColor("_TintColor", new Color(0.55f, 0.6f, 0.7f, 0.4f));
            r.sharedMaterial = m; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            ps.Play();
            // bruma: manchas grandes y casi transparentes a ras de suelo
            brumaLluvia = new GameObject("coop_lluvia_bruma");
            var pb = brumaLluvia.AddComponent<ParticleSystem>(); pb.Stop();
            var mb = pb.main; mb.loop = true; mb.startLifetime = 6f; mb.startSpeed = 0.4f; mb.startSize = new ParticleSystem.MinMaxCurve(6f, 11f);
            mb.maxParticles = 120; mb.simulationSpace = ParticleSystemSimulationSpace.World; mb.startColor = new Color(0.6f, 0.64f, 0.7f, 0.05f);
            var eb = pb.emission; eb.rateOverTime = 18f;
            var sb = pb.shape; sb.shapeType = ParticleSystemShapeType.Box; sb.box = new Vector3(40f, 1.5f, 40f);
            var cb = pb.colorOverLifetime; cb.enabled = true;
            var gb = new Gradient(); gb.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.4f), new GradientAlphaKey(0f, 1f) });
            cb.color = new ParticleSystem.MinMaxGradient(gb);
            var rb = brumaLluvia.GetComponent<ParticleSystemRenderer>();
            var tb = new Texture2D(32, 32, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++) { float d = new Vector2(x - 15.5f, y - 15.5f).magnitude / 15.5f; tb.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Pow(Mathf.Clamp01(1f - d), 2f))); }
            tb.Apply();
            var mbm = new Material(sh) { mainTexture = tb, name = "coop_lluvia_bruma" };
            if (mbm.HasProperty("_TintColor")) mbm.SetColor("_TintColor", new Color(0.5f, 0.52f, 0.58f, 0.25f));
            rb.sharedMaterial = mbm; rb.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; rb.receiveShadows = false;
            pb.Play();
        }

        /// Sonido de lluvia hecho por el mod (sin música ni grabaciones): ruido rosa filtrado (el rumor de fondo), un
        /// siseo agudo suave y miles de gotas sueltas (golpecitos cortos), 8 s que se repiten sin corte.
        static AudioClip ClipLluvia()
        {
            if (clipLluvia != null) return clipLluvia;
            const int f = 44100; int n = f * 8; var d = new float[n];
            var rng = new System.Random(4242);
            float b0 = 0, b1 = 0, b2 = 0, lp = 0, hp = 0, prev = 0;
            for (int i = 0; i < n; i++)
            {
                float w = (float)rng.NextDouble() * 2f - 1f;
                b0 = 0.99765f * b0 + w * 0.0990460f; b1 = 0.96300f * b1 + w * 0.2965164f; b2 = 0.57000f * b2 + w * 1.0526913f;
                float rosa = (b0 + b1 + b2 + w * 0.1848f) * 0.11f;
                lp += (rosa - lp) * 0.18f;                 // rumor grave
                hp = 0.92f * (hp + w - prev); prev = w;    // siseo
                d[i] = lp * 0.55f + hp * 0.035f;
            }
            for (int k = 0; k < 9000; k++)                 // gotas: golpecitos con un leve tono y caída rápida
            {
                int p = rng.Next(n); float amp = 0.04f + (float)rng.NextDouble() * 0.12f, fr = 1800f + (float)rng.NextDouble() * 3500f;
                int largo = 120 + rng.Next(500);
                for (int j = 0; j < largo; j++) { int q = (p + j) % n; d[q] += amp * Mathf.Exp(-j / (largo * 0.22f)) * Mathf.Sin(2f * Mathf.PI * fr * j / f); }
            }
            int cruce = f / 2;                             // fundido para que el bucle no se note
            for (int i = 0; i < cruce; i++) { float a = i / (float)cruce; d[i] = d[i] * a + d[n - cruce + i] * (1f - a); }
            var recorte = new float[n - cruce]; System.Array.Copy(d, recorte, recorte.Length);
            float pico = 0f; foreach (var x in recorte) pico = Mathf.Max(pico, Mathf.Abs(x));
            if (pico > 0f) for (int i = 0; i < recorte.Length; i++) recorte[i] *= 0.7f / pico;
            clipLluvia = AudioClip.Create("coop_lluvia_sintetica", recorte.Length, 1, f, false);
            clipLluvia.SetData(recorte, 0);
            return clipLluvia;
        }

        /// La lluvia (normal o de sangre) sigue a la cámara y no cae bajo techo ni dentro de las minas.
        void MoverLluvia()
        {
            var cam = Camera.main;
            if (lluvia != null && cam != null)
            {
                lluvia.transform.position = cam.transform.position + Vector3.up * 14f + cam.transform.forward * 6f;
                if (salpicaduras != null)
                {
                    Vector3 pie = cam.transform.position + cam.transform.forward * 4f; RaycastHit hs;
                    if (Physics.Raycast(pie + Vector3.up * 3f, Vector3.down, out hs, 12f, ~0, QueryTriggerInteraction.Ignore)) pie.y = hs.point.y + 0.03f; else pie.y = cam.transform.position.y - 1.6f;
                    salpicaduras.transform.position = pie;
                }
                if (brumaLluvia != null) brumaLluvia.transform.position = cam.transform.position + Vector3.down * 1.2f;
                // dentro de las minas (fuera del mapa, al oeste) no llueve
                bool bajoTecho = cam.transform.position.x < Anclaje.Origen().x - 100f;
                // bajo un techo (cabaña, cueva): algo sólido encima, a menos de 12 m; no llueve ahí dentro
                if (!bajoTecho && Time.time >= proxTecho)
                {
                    proxTecho = Time.time + 0.2f;
                    cubierto = false;
                    foreach (var h in Physics.RaycastAll(cam.transform.position + Vector3.up * 0.2f, Vector3.up, 12f, ~0, QueryTriggerInteraction.Ignore))
                        if (h.collider.GetComponentInParent<FPSPlayer>() == null && !(h.collider is TerrainCollider)) { cubierto = true; break; }
                }
                if (!bajoTecho && cubierto) bajoTecho = true;
                var psl = lluvia.GetComponent<ParticleSystem>();
                var em = psl.emission;
                if (em.enabled == bajoTecho) { em.enabled = !bajoTecho; if (bajoTecho) psl.Clear(); }   // al entrar se borran las gotas que ya venían cayendo
                if (salpicaduras != null) { var ps2 = salpicaduras.GetComponent<ParticleSystem>(); var e2 = ps2.emission; if (e2.enabled == bajoTecho) { e2.enabled = !bajoTecho; if (bajoTecho) ps2.Clear(); } }
                bajoTechoSonido = bajoTecho;
            }
        }

        void MantenerTormenta(float t)
        {
            var cam = Camera.main;
            MoverLluvia();
            // lluvia que suena (en bucle) y truenos
            // La fuente de audio se crea siempre (los truenos suenan por ella); el bucle de lluvia solo si SonidoLluvia está activo.
            if (cam != null && sonidoLluvia == null)
            {
                sonidoLluvia = cam.gameObject.AddComponent<AudioSource>();
                sonidoLluvia.loop = true; sonidoLluvia.spatialBlend = 0f; sonidoLluvia.playOnAwake = false;
                if (SonidoLluvia)
                {
                    var clip = Sonidos.Clip("tormenta_lluvia");
                    if (clip != null) { sonidoLluvia.clip = clip; sonidoLluvia.volume = 0.75f * Opciones.FactorVolumen; sonidoLluvia.Play(); }
                }
            }
            // bucle de lluvia propio (fuente aparte de la de los truenos): fuerte fuera, apagado bajo techo
            // grabación real de lluvia («Heavy Rain», Pro Sensory, CC0); el sonido sintetizado sonaba raro (2026-11-18)
            var grabada = Sonidos.Clip("tormenta_lluvia");
            if (cam != null && bucleLluvia == null && grabada != null)
            {
                bucleLluvia = cam.gameObject.AddComponent<AudioSource>();
                bucleLluvia.clip = grabada; bucleLluvia.loop = true; bucleLluvia.spatialBlend = 0f; bucleLluvia.playOnAwake = false; bucleLluvia.volume = 0f; bucleLluvia.Play();
            }
            if (bucleLluvia != null) bucleLluvia.volume = Mathf.MoveTowards(bucleLluvia.volume, (bajoTechoSonido ? 0.18f : 0.55f) * Opciones.FactorVolumen, Time.deltaTime * 0.5f);
            if (sonidoLluvia != null) sonidoLluvia.volume = SonidoLluvia ? (bajoTechoSonido ? 0.22f : 0.75f) * Opciones.FactorVolumen : Opciones.FactorVolumen;   // dentro la lluvia suena lejana; los truenos, a volumen normal
            // relámpago: doble destello, luz de golpe y el trueno un poco después (más tarde = más lejos)
            if (t >= proximoRayo)
            {
                proximoRayo = t + Random.Range(6f, 18f);
                destello = 1f;
                truenoEn = t + Random.Range(0.3f, 2.2f);
                if (cam != null) Rayo(cam);
                if (fotoRayo != null) { Application.CaptureScreenshot(System.IO.Path.Combine(System.IO.Path.Combine(BepInEx.Paths.BepInExRootPath, "capturas"), fotoRayo + ".png")); fotoRayo = null; }
            }
            if (rayo != null)
            {
                // parpadea y se apaga con el destello
                float f2 = Mathf.Clamp01(destello);
                bool visible = f2 > 0.55f || (f2 > 0.12f && f2 < 0.32f);
                foreach (var lr in rayo.GetComponentsInChildren<LineRenderer>()) lr.enabled = visible;
                if (destello <= 0f) { Object.Destroy(rayo); rayo = null; }
            }
            if (destello > 0f)
            {
                destello -= Time.deltaTime * 3.2f;
                float f = Mathf.Clamp01(destello);
                float pulso = f > 0.55f || (f > 0.15f && f < 0.3f) ? f : f * 0.3f;   // dos golpes de luz
                RenderSettings.ambientLight = Color.Lerp(ambienteTormenta, new Color(0.85f, 0.9f, 1f), pulso);
                foreach (var kv in intensidadAntes) if (kv.Key != null) kv.Key.intensity = kv.Value * Mathf.Lerp(0.45f, 2.2f, pulso);
                if (destello <= 0f) { RenderSettings.ambientLight = ambienteTormenta; AtenuarLuces(0.45f); }
            }
            if (truenoEn > 0f && t >= truenoEn)
            {
                truenoEn = -1f;
                var tr = Sonidos.Clip("tormenta_trueno");
                if (tr != null && cam != null && sonidoLluvia != null) sonidoLluvia.PlayOneShot(tr, Random.Range(0.6f, 1f) * Opciones.FactorVolumen);
            }
            // 1,5 veces más enemigos: el anfitrión agrega la mitad de los que había y los repone cada minuto
            if (plugin.EsAnfitrion && t >= proximoRefuerzo)
            {
                proximoRefuerzo = t + 60f;
                Refuerzos();
            }
        }

        GameObject rayo; static Material matRayo;

        /// Rayo visible a lo lejos: una línea quebrada del cielo al suelo con un par de ramas.
        void Rayo(Camera cam)
        {
            if (rayo != null) Object.Destroy(rayo);
            if (cam.transform.position.x < Anclaje.Origen().x - 100f) return;   // dentro de las minas no se ve el cielo
            if (matRayo == null) { matRayo = new Material(Shader.Find("Sprites/Default")) { name = "coop_rayo" }; }   // sin niebla: se ve de lejos
            rayo = new GameObject("coop_rayo");
            Vector3 dir = cam.transform.forward; dir.y = 0f; if (dir.sqrMagnitude < 0.01f) dir = Vector3.forward;
            dir = Quaternion.Euler(0f, Random.Range(-70f, 70f), 0f) * dir.normalized;
            Vector3 suelo = cam.transform.position + dir * Random.Range(150f, 400f);
            suelo.y = cam.transform.position.y - 5f;
            Vector3 cielo = suelo + Vector3.up * Random.Range(160f, 220f) + new Vector3(Random.Range(-30f, 30f), 0f, Random.Range(-30f, 30f));
            Tramo(cielo, suelo, 4.5f, 14);
            for (int k = 0; k < 2; k++)
            {
                float a = Random.Range(0.25f, 0.6f);
                Vector3 desde = Vector3.Lerp(cielo, suelo, a);
                Vector3 hasta = desde + new Vector3(Random.Range(-40f, 40f), -Random.Range(30f, 70f), Random.Range(-40f, 40f));
                Tramo(desde, hasta, 2.2f, 7);
            }
        }

        void Tramo(Vector3 a, Vector3 b, float ancho, int puntos)
        {
            var go = new GameObject("tramo"); go.transform.SetParent(rayo.transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = matRayo; lr.useWorldSpace = true;
            lr.startWidth = ancho; lr.endWidth = ancho * 0.5f;
            lr.startColor = new Color(0.85f, 0.9f, 1f, 1f); lr.endColor = new Color(0.75f, 0.85f, 1f, 0.9f);
            lr.positionCount = puntos;
            Vector3 lado = Vector3.Cross(b - a, Vector3.up).normalized;
            for (int i = 0; i < puntos; i++)
            {
                float t = i / (float)(puntos - 1);
                Vector3 p = Vector3.Lerp(a, b, t);
                if (i > 0 && i < puntos - 1) p += lado * Random.Range(-12f, 12f) + Vector3.up * Random.Range(-6f, 6f);
                lr.SetPosition(i, p);
            }
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; lr.receiveShadows = false;
        }

        /// Para las pruebas: un relámpago ya.
        public void OrdenRayo(string foto = null) { proximoRayo = 0f; fotoRayo = foto; }
        string fotoRayo;

        void Refuerzos()
        {
            var c = plugin.CombateActual; var j = plugin.Jugador;
            if (c == null || j == null) return;
            refuerzos.RemoveAll(td => td == null || !td.gameObject.activeInHierarchy || Muerto(td));
            var comp = plugin.CompaneroActual;
            if (refuerzosMeta < 0)
            {
                int base0 = 0;
                foreach (var td in Object.FindObjectsOfType<TakeDamage>())
                {
                    if (td == null || !td.gameObject.activeInHierarchy || td.GetComponentInParent<FPSPlayer>() != null || Dificultad.EsJefe(td) || Aliados.Protegido(td.transform) || Muerto(td)) continue;
                    Vector3 p = td.transform.position;
                    if (Vector3.Distance(p, j.transform.position) < 200f || (comp != null && comp.Visible && Vector3.Distance(p, comp.PosicionMundo) < 200f)) base0++;
                }
                refuerzosMeta = Mathf.Max(2, Mathf.CeilToInt(base0 * 0.5f));
                plugin.Log("Evento: tormenta, " + base0 + " enemigos cerca → " + refuerzosMeta + " más (×1,5)");
            }
            int faltan = refuerzosMeta - refuerzos.Count, hechos = 0;
            var tipos = Mundo.Guardianes;
            for (int k = 0; k < faltan; k++)
            {
                Vector3 centro = comp != null && comp.Visible && azar.Next(2) == 0 ? comp.PosicionMundo : j.transform.position;
                float ang = (float)azar.NextDouble() * Mathf.PI * 2f, dist = 40f + (float)azar.NextDouble() * 30f;
                bool pot = azar.NextDouble() < Potenciado.ProbabilidadActual;
                var td = c.Generar(tipos[azar.Next(tipos.Length)] + (pot ? Potenciado.Marca : ""), centro + new Vector3(Mathf.Cos(ang) * dist, 0f, Mathf.Sin(ang) * dist));
                if (td != null) { refuerzos.Add(td); hechos++; }
            }
            if (hechos > 0) plugin.Log("Evento: tormenta, " + hechos + " enemigos más (" + refuerzos.Count + "/" + refuerzosMeta + ")");
        }

        internal static bool Muerto(TakeDamage td)
        {
            var f = PlayMakerFSM.FindFsmOnGameObject(td.gameObject, td.NameOfFsm);
            var h = f != null ? f.FsmVariables.FindFsmFloat("Health") : null;
            return h != null && h.Value <= 0f;
        }

        GameObject humo;

        /// Volutas de humo gris cerca del suelo alrededor del jugador (seguirlo en MantenerHumo).
        void CrearHumo()
        {
            if (humo != null) return;
            humo = new GameObject("coop_humo");
            humo.AddComponent<SeguirCamaraNiebla>();
            humo.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            var ps = humo.AddComponent<ParticleSystem>();
            ps.Stop();
            var main = ps.main;
            main.loop = true; main.prewarm = true; main.startLifetime = 22f; main.startSpeed = 0.15f;
            main.startSize = new ParticleSystem.MinMaxCurve(6f, 10f); main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.maxParticles = 700; main.simulationSpace = ParticleSystemSimulationSpace.Local;   // locales: las nubes acompañan al jugador hasta que acaba el evento
            // bancos de niebla azulada-blanquecina pegados al suelo, como el claro del bosque de la captura de referencia
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.52f, 0.54f, 0.58f, 0.2f), new Color(0.7f, 0.72f, 0.76f, 0.42f));   // nubes medias, blancas
            var em = ps.emission; em.rateOverTime = 34f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.box = new Vector3(32f, 32f, 5f);   // más juntas, más cerca del jugador
            var col = ps.colorOverLifetime; col.enabled = true;
            var gr = new Gradient(); gr.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            col.color = gr;
            var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);
            var r = humo.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            const int N = 64;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
            {
                float dx = (x - N / 2f + 0.5f) / (N / 2f), dy = (y - N / 2f + 0.5f) / (N / 2f), d = Mathf.Sqrt(dx * dx + dy * dy);
                float ruido = Mathf.PerlinNoise(x * 0.11f, y * 0.11f) * 0.6f + 0.4f;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(1f - d) * Mathf.Clamp01(1f - d) * ruido));
            }
            tex.Apply();
            var sh2 = Shader.Find("Particles/Alpha Blended") ?? Shader.Find("Sprites/Default");
            var m = new Material(sh2) { mainTexture = tex, name = "coop_humo" };
            if (m.HasProperty("_TintColor")) m.SetColor("_TintColor", new Color(0.66f, 0.68f, 0.72f, 0.5f));
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            ps.Play();
        }

        float giroHumo;
        void MantenerHumo()
        {
            var cam = Camera.main;
            if (humo == null || cam == null || plugin.Jugador == null) return;
            RaycastHit h; float y = plugin.Jugador.transform.position.y;
            if (Physics.Raycast(plugin.Jugador.transform.position + Vector3.up * 3f, Vector3.down, out h, 12f)) y = h.point.y;
            humo.transform.position = new Vector3(cam.transform.position.x, cam.transform.position.y - 0.9f, cam.transform.position.z);
            giroHumo += Time.deltaTime * 7f;   // las nubes giran despacio alrededor del jugador (el sistema es local: gira entero)
            humo.transform.rotation = Quaternion.Euler(0f, giroHumo, 0f) * Quaternion.Euler(-90f, 0f, 0f);
        }

        void QuitarHumo() { if (humo != null) { Object.Destroy(humo); humo = null; } }

        void QuitarTormenta()
        {
            QuitarLluvia();
            if (bucleLluvia != null) { bucleLluvia.Stop(); Object.Destroy(bucleLluvia); bucleLluvia = null; }
            if (rayo != null) { Object.Destroy(rayo); rayo = null; }
            if (sonidoLluvia != null) { sonidoLluvia.Stop(); Object.Destroy(sonidoLluvia); sonidoLluvia = null; }
            foreach (var kv in intensidadAntes) if (kv.Key != null) kv.Key.intensity = kv.Value;
            intensidadAntes.Clear();
            destello = 0f; truenoEn = -1f; refuerzosMeta = -1; refuerzos.Clear();
        }

        // ---------------- Pantalla ----------------

        GUIStyle titulo, sub, reloj;

        public void Dibujar()
        {
            if (activos.Count == 0) return;
            if (titulo == null)
            {
                titulo = new GUIStyle(GUI.skin.label) { fontSize = Opciones.T(46), alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
                sub = new GUIStyle(GUI.skin.label) { fontSize = Opciones.T(20), alignment = TextAnchor.MiddleCenter, wordWrap = true };
                reloj = new GUIStyle(GUI.skin.label) { fontSize = Opciones.T(16), alignment = TextAnchor.MiddleLeft, fontStyle = FontStyle.Bold };
            }
            Color col = tituloTipo == Tipo.Tormenta ? new Color(0.65f, 0.75f, 1f) : tituloTipo == Tipo.LunaDeSangre ? new Color(1f, 0.15f, 0.1f) : tituloTipo == Tipo.NocheTranquila ? new Color(0.6f, 0.85f, 1f)
                      : tituloTipo == Tipo.NocheRuidosa ? new Color(1f, 0.6f, 0.2f) : new Color(0.85f, 0.85f, 0.9f);
            if (Esta(Tipo.LunaDeSangre))
            {
                // un velo rojo muy suave que crece con la transición (antes era un rojo fuerte y fijo)
                float k = Mathf.Clamp01((Time.time - inicios[Tipo.LunaDeSangre]) / RampaLuna);
                var prev = GUI.color; GUI.color = new Color(0.45f, 0f, 0f, 0.09f * k);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
                GUI.color = prev;
            }
            if (Esta(Tipo.Tormenta))
            {
                if (flashInicio >= 0f)
                {
                    float a2 = 1f - (Time.realtimeSinceStartup - flashInicio) / 1.3f;   // destello blanco de transición
                    if (a2 > 0f) { var p0 = GUI.color; GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(a2)); GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture); GUI.color = p0; }
                    else flashInicio = -1f;
                }
                if (destello > 0f)
                {
                    var prev2 = GUI.color; GUI.color = new Color(0.85f, 0.9f, 1f, Mathf.Clamp01(destello) * 0.55f);
                    GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
                    GUI.color = prev2;
                }
            }
            float dentro = durTitulo - (anuncioHasta - Time.realtimeSinceStartup);   // segundos desde que salió el título (dura 6; la luna de sangre 7,5)
            if (dentro >= 0f && dentro <= durTitulo)
            {
                string txt = Titulo[(int)tituloTipo];
                float alfa = Mathf.Clamp01(Mathf.Min(dentro / 1.2f, (durTitulo - dentro) / 1.4f));
                float escala = 1f + 0.35f * (1f - Mathf.Clamp01(dentro / 1.6f));
                var rect = new Rect(0, Screen.height * 0.28f, Screen.width, 60f);
                var oldM = GUI.matrix;
                GUIUtility.ScaleAroundPivot(new Vector2(escala, escala), new Vector2(Screen.width / 2f, rect.y + 30f));
                if (tituloTipo == Tipo.NocheOscura)
                {
                    // las letras aparecen una a una, con un parpadeo inquieto
                    int visibles = Mathf.Clamp(Mathf.FloorToInt(dentro / 0.14f), 0, txt.Length);
                    string parcial = txt.Substring(0, visibles);
                    float flick = 0.75f + 0.25f * Mathf.Sin(Time.realtimeSinceStartup * 23f) * Mathf.Sin(Time.realtimeSinceStartup * 7f);
                    var sombra = new Color(0f, 0f, 0f, alfa * 0.8f);
                    titulo.normal.textColor = sombra; GUI.Label(new Rect(rect.x + 3, rect.y + 3, rect.width, rect.height), parcial, titulo);
                    titulo.normal.textColor = new Color(col.r, col.g, col.b, alfa * flick); GUI.Label(rect, parcial, titulo);
                }
                else if (tituloTipo == Tipo.LunaDeSangre)
                {
                    GUI.matrix = oldM;   // esta animación lleva su propia escala
                    DibujarTituloLuna(dentro, txt);
                }
                else
                {
                    titulo.normal.textColor = new Color(col.r, col.g, col.b, alfa); GUI.Label(rect, txt, titulo);
                }
                GUI.matrix = oldM;
            }
            // sin temporizador (pedido 2026-11-10)
        }

        /// Título de la luna de sangre: destello rojo, las letras caen una a una con rebote y salpican, laten como un corazón y chorrean sangre; al final se derriten.
        void DibujarTituloLuna(float t, string txt)
        {
            float W = Screen.width, H = Screen.height;
            float dur = durTitulo;
            var blanca = Texture2D.whiteTexture; var pc = GUI.color;
            float latido = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Time.realtimeSinceStartup * Mathf.PI * 2f * 1.05f)), 8f);   // pulso de corazón
            // destello rojo al empezar y velo que late
            if (t < 0.7f) { GUI.color = new Color(0.55f, 0f, 0f, 0.5f * (1f - t / 0.7f)); GUI.DrawTexture(new Rect(0, 0, W, H), blanca); }
            float velo = Mathf.Clamp01(Mathf.Min(t / 0.6f, (dur - t) / 1.5f));
            GUI.color = new Color(0.4f, 0f, 0f, (0.05f + 0.07f * latido) * velo); GUI.DrawTexture(new Rect(0, 0, W, H), blanca);
            // bordes oscuros-rojos (viñeta) que laten
            float vg = (0.16f + 0.1f * latido) * velo;
            GUI.color = new Color(0.25f, 0f, 0f, vg); float bv = H * 0.09f;
            GUI.DrawTexture(new Rect(0, 0, W, bv), blanca); GUI.DrawTexture(new Rect(0, H - bv, W, bv), blanca);
            GUI.color = pc;

            var estilo = new GUIStyle(titulo) { fontSize = Opciones.T(58), alignment = TextAnchor.UpperLeft };
            float baseY = H * 0.26f;
            var total = estilo.CalcSize(new GUIContent(txt)); float x0 = W / 2f - total.x / 2f;
            float salida = Mathf.Clamp01((t - (dur - 1.6f)) / 1.6f);   // al final el título se derrite hacia abajo
            float escalaLatido = 1f + 0.045f * latido;
            var oldM = GUI.matrix;
            GUIUtility.ScaleAroundPivot(new Vector2(escalaLatido, escalaLatido), new Vector2(W / 2f, baseY + 40f));
            float sacudida = t < 1.0f ? (1f - t) * 4f : 0f;   // un temblor al principio
            for (int i = 0; i < txt.Length; i++)
            {
                if (txt[i] == ' ') continue;
                float ti = t - 0.15f - i * 0.085f;   // cada letra entra con su retraso
                if (ti < 0f) continue;
                float p = Mathf.Clamp01(ti / 0.42f);
                float caida = (1f - p) * (1f - p);
                float rebote = ti > 0.42f && ti < 0.7f ? Mathf.Sin((ti - 0.42f) / 0.28f * Mathf.PI) * 6f : 0f;   // un saltito al caer
                var pre = estilo.CalcSize(new GUIContent(txt.Substring(0, i)));
                float lx = x0 + pre.x + (i == 0 ? 0f : -2f), ly = baseY - 230f * caida - rebote + salida * salida * 40f;
                lx += (Mathf.PerlinNoise(i * 3.1f, Time.realtimeSinceStartup * 30f) - 0.5f) * sacudida; 
                float a = p * (1f - salida);
                float esc = 1f + 1.1f * (1f - p);
                var oM = GUI.matrix; GUIUtility.ScaleAroundPivot(new Vector2(esc, esc), new Vector2(lx + 16f, ly + 30f));
                var r = new Rect(lx, ly, 120f, 90f);
                // contorno de sangre oscura + sombra negra
                estilo.normal.textColor = new Color(0f, 0f, 0f, a * 0.85f); GUI.Label(new Rect(r.x + 4, r.y + 5, r.width, r.height), txt[i].ToString(), estilo);
                estilo.normal.textColor = new Color(0.32f, 0f, 0f, a * 0.9f);
                foreach (var d in new[] { new Vector2(-2, 0), new Vector2(2, 0), new Vector2(0, -2), new Vector2(0, 2) }) GUI.Label(new Rect(r.x + d.x, r.y + d.y, r.width, r.height), txt[i].ToString(), estilo);
                float v = 0.85f + 0.15f * latido;
                estilo.normal.textColor = new Color(0.95f * v, 0.06f + 0.05f * latido, 0.05f, a); GUI.Label(r, txt[i].ToString(), estilo);
                GUI.matrix = oM;
                // salpicadura donde cae
                if (ti > 0.4f && ti < 0.8f)
                {
                    float sp = (ti - 0.4f) / 0.4f;
                    GUI.color = new Color(0.6f, 0.02f, 0.02f, (1f - sp) * a);
                    for (int g = 0; g < 5; g++)
                    {
                        float ang = (g / 5f) * 6.28f + i; float rd = 8f + sp * 34f;
                        GUI.DrawTexture(new Rect(lx + 18f + Mathf.Cos(ang) * rd, ly + 62f + Mathf.Sin(ang) * rd * 0.6f, 5f * (1f - sp) + 2f, 5f * (1f - sp) + 2f), blanca);
                    }
                    GUI.color = pc;
                }
            }
            GUI.matrix = oldM;
            DibujarSangrePantalla(t, dur, W, H);
            // subtítulo que aparece después del golpe
            float sa = Mathf.Clamp01(Mathf.Min((t - 2.1f) / 0.9f, (dur - t) / 1.2f));
            if (sa > 0f)
            {
                var ss = new GUIStyle(sub) { fontSize = Opciones.T(22), alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Italic };
                var rs = new Rect(0, baseY + 105f, W, 40f);
                ss.normal.textColor = new Color(0f, 0f, 0f, sa * 0.8f); GUI.Label(new Rect(rs.x + 2, rs.y + 2, rs.width, rs.height), "Todo lo que vive te busca.", ss);
                ss.normal.textColor = new Color(0.9f, 0.62f, 0.58f, sa); GUI.Label(rs, "Todo lo que vive te busca.", ss);
            }
        }

        public string Describir() { var sd = new System.Text.StringBuilder("eventos=" + activos.Count + " ["); foreach (var kv in activos) sd.Append(kv.Key).Append(" ").Append(Mathf.RoundToInt(kv.Value - Time.time)).Append(" s; "); sd.Append("]"); return sd.ToString() + " ocultos=" + ocultos.Count + " potenciadosLuna=" + potenciadosLuna.Count; }

        public static Tipo TipoPorNombre(string n)
        {
            n = n.ToLowerInvariant();
            if (n.StartsWith("luna")) return Tipo.LunaDeSangre;
            if (n.StartsWith("tranq")) return Tipo.NocheTranquila;
            if (n.StartsWith("nieb")) return Tipo.Niebla;
            if (n.StartsWith("oscu")) return Tipo.NocheOscura;
            if (n.StartsWith("ruid")) return Tipo.NocheRuidosa;
            if (n.StartsWith("torm")) return Tipo.Tormenta;
            return Tipo.Ninguno;
        }
        public void OrdenIniciar(Tipo t, float s) { Iniciar(t, s > 0f ? s : Duracion[(int)t], true); }
        public void OrdenTerminar() { foreach (var k in new List<Tipo>(activos.Keys)) activos[k] = 0f; }
    }

    /// Mueve las nubes de la niebla DESPUÉS de que el jugador y la cámara ya se movieron en este fotograma (si no, iban un cuadro atrasadas y daban tirones al caminar o correr).
    [DefaultExecutionOrder(32000)]
    public class SeguirCamaraNiebla : MonoBehaviour
    {
        float giro;
        void LateUpdate()
        {
            var cam = Camera.main; if (cam == null) return;
            giro += Time.deltaTime * 7f;   // las nubes giran despacio alrededor del jugador
            transform.position = cam.transform.position + Vector3.down * 0.9f;
            transform.rotation = Quaternion.Euler(0f, giro, 0f) * Quaternion.Euler(-90f, 0f, 0f);
        }
    }
}

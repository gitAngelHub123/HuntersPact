// Witch Hunt Coop — © 2026 Angel (Chile). Licencia MIT.
using System.IO;
using UnityEngine;

namespace WitchHuntCoop
{
    /// Representación del otro jugador: el modelo del cazador del juego (Witch_Hunter) con
    /// animaciones de quieto/caminar/correr según su velocidad. Si el modelo no está cargado,
    /// una figura simple de respaldo. Encima, su nombre y distancia.
    public class Companero
    {
        public string Nombre = "";

        int escena = -1;
        Vector3 objetivo;           // relativo al ancla
        float yawObjetivo;
        float pitchObjetivo, pitchVisto;   // grados, positivo = mira hacia abajo
        bool tieneDatos;

        // Huesos que se inclinan al mirar arriba/abajo y cuánto del ángulo se lleva cada uno.
        static readonly HumanBodyBones[] huesosMirada = { HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.Neck, HumanBodyBones.Head };
        static readonly float[] pesosMirada = { 0.15f, 0.25f, 0.25f, 0.35f };
        Transform[] huesos;

        GameObject cuerpo;
        Vector3 posVista;
        float yawVisto;

        // Animación
        Animator animator;
        AnimatorOverrideController anims;
        AnimationClip clipQuieto, clipCaminar, clipCorrer, clipActual;
        float velocidad;            // m/s horizontal, suavizada
        Vector3 posAnterior;
        string pendiente; float pendienteDesde;

        const float VelCaminar = 0.4f;    // por debajo: quieto
        const float VelCorrer = 4.2f;     // por encima: corre
        const float RitmoCaminar = 1.6f;  // m/s a los que el clip de caminar se ve natural
        const float RitmoCorrer = 5f;

        public void Recibir(BinaryReader r)
        {
            escena = r.ReadInt32();
            objetivo = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
            yawObjetivo = r.ReadSingle();
            float p = r.ReadSingle();               // ángulo x de la cámara: 0..360, positivo = abajo
            pitchObjetivo = Mathf.Clamp(p > 180f ? p - 360f : p, -80f, 80f);
            EstadoVida = r.BaseStream.Position < r.BaseStream.Length ? (Muerte.Estado)r.ReadByte() : Muerte.Estado.Vivo;
            tieneDatos = true;
        }

        public Muerte.Estado EstadoVida = Muerte.Estado.Vivo;
        public int Escena { get { return escena; } }
        public bool Visible { get { return cuerpo != null && cuerpo.activeSelf; } }
        public Vector3 PosicionMundo { get { return posVista; } }
        public Vector3 Adelante { get { return Quaternion.Euler(0f, yawVisto, 0f) * Vector3.forward; } }

        public string Describir()
        {
            return "compañero escena=" + escena + " rel=" + objetivo + " yaw=" + Mathf.RoundToInt(yawObjetivo) + " pitch=" + Mathf.RoundToInt(pitchObjetivo) + " visible=" + Visible + (Visible ? " mundo=" + posVista + " " + Modelo : "");
        }

        public void Actualizar(float dt, int escenaLocal, Vector3 ancla)
        {
            // Un compañero caído (espectador) no se dibuja.
            bool visible = tieneDatos && escena >= 0 && escena == escenaLocal && (EstadoVida == Muerte.Estado.Vivo || EstadoVida == Muerte.Estado.Derribado);
            if (!visible)
            {
                if (cuerpo != null) cuerpo.SetActive(false);
                return;
            }

            bool nuevo = cuerpo == null;   // se destruye al cambiar de escena
            if (nuevo) Crear();
            cuerpo.SetActive(true);

            Vector3 destino = objetivo + ancla;
            if (nuevo || (posVista - destino).sqrMagnitude > 20f * 20f)
            {
                posVista = destino;
                yawVisto = yawObjetivo;
            }
            else
            {
                float k = 1f - Mathf.Exp(-15f * dt);
                posVista = Vector3.Lerp(posVista, destino, k);
                yawVisto = Mathf.LerpAngle(yawVisto, yawObjetivo, k);
            }
            cuerpo.transform.position = posVista;
            cuerpo.transform.rotation = Quaternion.Euler(0f, yawVisto, 0f);
            if (EstadoVida == Muerte.Estado.Derribado)
            {
                // tendido de espaldas en el suelo
                cuerpo.transform.rotation = Quaternion.Euler(0f, yawVisto, 0f) * Quaternion.Euler(-85f, 0f, 0f);
                cuerpo.transform.position = posVista + Vector3.up * 0.15f;
            }
            pitchVisto = nuevo ? pitchObjetivo : Mathf.Lerp(pitchVisto, pitchObjetivo, 1f - Mathf.Exp(-12f * dt));

            Animar(dt, nuevo);
        }

        void Animar(float dt, bool nuevo)
        {
            if (nuevo || dt <= 0f) { posAnterior = posVista; velocidad = 0f; }
            Vector3 d = posVista - posAnterior; d.y = 0f;
            posAnterior = posVista;
            if (dt > 0f) velocidad = Mathf.Lerp(velocidad, d.magnitude / dt, 1f - Mathf.Exp(-8f * dt));
            if (animator == null || anims == null) return;

            // Qué clip toca, con un pequeño margen para no parpadear entre dos.
            string quiero = velocidad < VelCaminar ? "quieto" : (velocidad < VelCorrer || clipCorrer == null ? "caminar" : "correr");
            if (quiero != pendiente) { pendiente = quiero; pendienteDesde = Time.time; }
            AnimationClip c = quiero == "quieto" ? clipQuieto : (quiero == "caminar" ? clipCaminar : clipCorrer);
            if (c != null && c != clipActual && (Time.time - pendienteDesde > 0.12f || clipActual == null))
            {
                anims[clipQuieto.name] = c;   // el controlador del cazador tiene un único estado "idle"
                clipActual = c;
            }

            if (clipActual == clipCaminar) animator.speed = Mathf.Clamp(velocidad / RitmoCaminar, 0.6f, 2.2f);
            else if (clipActual == clipCorrer) animator.speed = Mathf.Clamp(velocidad / RitmoCorrer, 0.7f, 1.6f);
            else animator.speed = 1f;
        }

        /// Figura provisional de ~1,8 m con el origen en los pies:
        /// cuerpo (cápsula), cabeza (esfera) y una visera amarilla que indica hacia dónde mira.
        void Crear()
        {
            if (CrearCazador()) return;
            CrearFigura();
        }

        bool CrearCazador()
        {
            GameObject orig = Modelos.Buscar(Modelos.Cazador);
            if (orig == null) return false;
            var anOrig = orig.GetComponentInChildren<Animator>(true);
            if (anOrig == null || anOrig.runtimeAnimatorController == null) return false;

            cuerpo = (GameObject)Object.Instantiate(orig);
            cuerpo.name = "WitchHuntCoop_Companero";
            Modelos.Desactivar(cuerpo);
            cuerpo.SetActive(true);
            foreach (var t in cuerpo.GetComponentsInChildren<Transform>(true)) t.gameObject.SetActive(true);

            animator = cuerpo.GetComponentInChildren<Animator>();
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            var fuente = Modelos.Buscar(Modelos.FuenteAnim);
            clipQuieto = anOrig.runtimeAnimatorController.animationClips.Length > 0 ? anOrig.runtimeAnimatorController.animationClips[0] : null;
            clipCaminar = Modelos.SinEventos(Modelos.Clip(fuente, Modelos.ClipCaminar));
            clipCorrer = Modelos.SinEventos(Modelos.Clip(fuente, Modelos.ClipCorrer));
            if (clipQuieto != null && clipCaminar != null)
            {
                anims = new AnimatorOverrideController();
                anims.runtimeAnimatorController = anOrig.runtimeAnimatorController;
                animator.runtimeAnimatorController = anims;
            }
            clipActual = null;
            pendiente = null;

            huesos = null;
            if (animator.isHuman)
            {
                huesos = new Transform[huesosMirada.Length];
                for (int i = 0; i < huesos.Length; i++) huesos[i] = animator.GetBoneTransform(huesosMirada[i]);
            }
            return true;
        }

        /// Inclina columna, pecho, cuello y cabeza según hacia dónde mira. Se llama en LateUpdate,
        /// después de que el Animator pose el esqueleto (si no, la animación lo sobrescribe).
        public void AplicarMirada()
        {
            if (huesos == null || cuerpo == null || !cuerpo.activeSelf) return;
            Vector3 derecha = cuerpo.transform.right;
            for (int i = 0; i < huesos.Length; i++)
            {
                if (huesos[i] == null) continue;
                huesos[i].rotation = Quaternion.AngleAxis(pitchVisto * pesosMirada[i], derecha) * huesos[i].rotation;
            }
        }

        public string Modelo { get { return animator != null ? "cazador (anim " + (clipActual != null ? clipActual.name : "-") + ", " + velocidad.ToString("0.0") + " m/s)" : "figura"; } }

        void CrearFigura()
        {
            animator = null; anims = null; huesos = null;
            cuerpo = new GameObject("WitchHuntCoop_Companero");
            Parte(PrimitiveType.Capsule, new Vector3(0f, 0.8f, 0f), new Vector3(0.55f, 0.8f, 0.55f), new Color(0.6f, 0.12f, 0.08f));
            Parte(PrimitiveType.Sphere, new Vector3(0f, 1.62f, 0f), new Vector3(0.32f, 0.34f, 0.32f), new Color(0.85f, 0.7f, 0.55f));
            Parte(PrimitiveType.Cube, new Vector3(0f, 1.7f, 0.17f), new Vector3(0.3f, 0.07f, 0.18f), new Color(0.95f, 0.85f, 0.3f));
        }

        void Parte(PrimitiveType tipo, Vector3 pos, Vector3 escala, Color color)
        {
            var go = GameObject.CreatePrimitive(tipo);
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(cuerpo.transform, false);
            go.transform.localPosition = pos;
            go.transform.localScale = escala;
            Pintar(go, color);
        }

        static void Pintar(GameObject go, Color c)
        {
            var rend = go.GetComponent<Renderer>();
            if (rend != null) rend.material.color = c;
        }

        public GameObject Cuerpo { get { return cuerpo; } }
        public float Velocidad { get { return velocidad; } }   // m/s del muñeco (para sus pasos)

        public void DibujarNombre(Camera cam)
        {
            if (cuerpo == null || !cuerpo.activeSelf) return;
            Vector3 p = EscalaRender.APantalla(cam, cuerpo.transform.position + Vector3.up * 2.1f);
            if (p.z <= 0f) return;
            float dist = Vector3.Distance(cam.transform.position, cuerpo.transform.position);
            string texto = Nombre + "  (" + Mathf.RoundToInt(dist) + " m)";
            var tam = GUI.skin.label.CalcSize(new GUIContent(texto));
            GUI.Label(new Rect(p.x - tam.x / 2f, Screen.height - p.y - tam.y, tam.x + 4, tam.y), texto);
        }

        public void Destruir()
        {
            if (cuerpo != null) Object.Destroy(cuerpo);
            cuerpo = null;
            animator = null; anims = null; clipActual = null; huesos = null;
        }
    }
}

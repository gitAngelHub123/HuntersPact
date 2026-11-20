# Ojo: muerte n.º 5 mejorada (coro oscuro que se desploma), más humano y más fuerte. Síntesis original desde cero.
import sys, wave, numpy as np

SR = 44100
rng = np.random.default_rng(55)
D = float(sys.argv[2]) if len(sys.argv) > 2 else 4.0   # duración del coro (más la cola de reverb)
COLA = float(sys.argv[3]) if len(sys.argv) > 3 else 2.0
DEMONIO = len(sys.argv) > 4 and sys.argv[4] == '1'
N = int(SR * D)
tt = np.arange(N) / SR

def ruido_lento(n, paso_s, amp):
    """Deriva suave (interpolación de valores al azar cada paso_s segundos)."""
    k = int(n / (SR * paso_s)) + 3
    pts = rng.standard_normal(k) * amp
    return np.interp(np.arange(n) / (SR * paso_s), np.arange(k), pts)

def pulso_glotal(f):
    """Fuente de voz tipo cuerdas vocales (pulso de Rosenberg) con jitter y shimmer."""
    f = f * (1 + ruido_lento(len(f), 0.05, 0.004))          # jitter
    fase = np.cumsum(f) / SR; c = fase % 1.0
    ab, ci = 0.42, 0.16                                       # apertura y cierre del ciclo
    g = np.where(c < ab, 0.5 * (1 - np.cos(np.pi * c / ab)),
        np.where(c < ab + ci, np.cos(0.5 * np.pi * (c - ab) / ci), 0.0))
    d = np.diff(g, prepend=0.0)                               # derivada: el "golpe" de la glotis
    shimmer = 1 + ruido_lento(len(f), 0.06, 0.06)
    return d * shimmer

def biquad_banda(x, fc, bw):
    """Resonador de formante con frecuencia variable en el tiempo (fc y bw: arrays)."""
    y = np.zeros_like(x); y1 = y2 = 0.0
    r = np.exp(-np.pi * bw / SR); a1 = -2 * r * np.cos(2 * np.pi * fc / SR); a2 = r * r; g = 1 - r
    for i in range(len(x)):
        v = g * x[i] - a1[i] * y1 - a2 * y2 if np.isscalar(a2) else g[i] * x[i] - a1[i] * y1 - a2[i] * y2
        y2, y1 = y1, v; y[i] = v
    return y

# vocales (F1..F5, Hz) de voz masculina oscura: "a" → "o" → "u" mientras el coro se derrumba
A = np.array([700, 1150, 2450, 3300, 4100]); O = np.array([480, 820, 2400, 3200, 4000]); U = np.array([330, 700, 2250, 3100, 3900])
def formantes_en(x_t):
    m1 = np.clip(x_t * 2, 0, 1)[:, None]; m2 = np.clip(x_t * 2 - 1, 0, 1)[:, None]
    return (A * (1 - m1) + O * m1) * (1 - m2) + U * m2

def fuente(base, retraso, desafine):
    """Fuente de una voz del coro (cuerdas vocales + aire): entra, sostiene y cae de tono, con vibrato humano."""
    x_t = np.clip((tt - retraso) / (D - retraso), 0, 1)
    caida = base * (0.30 + 0.70 * (1 - x_t ** 1.8))               # se desploma ~1,7 octavas
    vib = 1 + (0.010 + 0.006 * x_t) * np.sin(2 * np.pi * np.cumsum(5.2 + ruido_lento(N, 0.8, 0.6)) / SR)
    f = caida * vib * (1 + desafine) * (1 + ruido_lento(N, 0.4, 0.006))
    src = pulso_glotal(f) * 3.0 + rng.standard_normal(N) * 0.035
    entrada = np.clip((tt - retraso) / 0.35, 0, 1) ** 1.5
    salida = np.clip((D - tt) / (D * 0.35), 0, 1) ** 1.3
    return src * entrada * salida * (1 + ruido_lento(N, 0.3, 0.12))

def vocal(src, corrimiento):
    """Las vocales del coro (a → o → u) aplicadas a la suma de las fuentes."""
    x_t = tt / D; F = formantes_en(x_t); bw = np.array([90, 110, 160, 200, 250]); out = np.zeros(N)
    for k, ganancia in enumerate([1.0, 0.7, 0.35, 0.2, 0.12]):
        out += ganancia * biquad_banda(src, F[:, k] * (1 + corrimiento), np.full(N, bw[k]) * (1 + x_t * 0.6))
    return out

# 14 voces: acorde menor oscuro con graves, afinaciones y entradas distintas (coro, no máquina), en dos grupos
grupos = [np.zeros(N), np.zeros(N)]
for base in [73.4, 87.3, 110.0, 130.8, 146.8, 174.6, 220.0]:
    for k in range(2):
        grupos[k] += fuente(base, rng.uniform(0.0, 0.45), rng.uniform(-0.012, 0.012))
coro = vocal(grupos[0], -0.02) + vocal(grupos[1], 0.025)
coro /= np.abs(coro).max()

# lamento grave bajo el coro y un retumbo final cuando se apaga
x_t = tt / D
retumbo = np.sin(2 * np.pi * np.cumsum(55 * (1 - 0.6 * x_t)) / SR) * np.clip((tt - D * 0.55) / (D * 0.3), 0, 1) * np.clip((D - tt) / 1.0, 0, 1)
ruido_grave = np.convolve(rng.standard_normal(N), np.ones(400) / 400, mode="same") * 6
mezcla = coro + 0.35 * retumbo + 0.15 * ruido_grave * np.clip((tt - D * 0.5) / D, 0, 1)

def reverb(x, cola, semilla):
    """Reverb de catedral por convolución con ruido que decae (un canal por oído: estéreo)."""
    r = np.random.default_rng(semilla); n = len(x) + int(SR * cola)
    it = np.arange(int(SR * cola)) / SR
    ir = r.standard_normal(len(it)) * np.exp(-it * 6.9 / cola)
    # reflexiones tempranas
    for d, g in ((0.013, 0.6), (0.029, 0.45), (0.047, 0.35), (0.071, 0.3)): ir[int(d * SR)] += g * 8
    ir = np.convolve(ir, np.ones(6) / 6, mode="same")        # suaviza los agudos
    ir /= np.abs(ir).max()
    h = np.fft.irfft(np.fft.rfft(x, 2 * n) * np.fft.rfft(ir, 2 * n))[:n]
    return h / np.abs(h).max()

if DEMONIO:
    # Voz demoníaca (2026-11-19): garganta enorme, ~50 Hz con subarmónico, vocal fry, ronquera y gruñido, vocales "o/u" oscuras
    x_t = tt / D
    f_dem = 52 * (1 + 0.18 * np.sin(2 * np.pi * 0.55 * tt) * (1 - x_t)) * (1 - 0.35 * x_t ** 1.5) * (1 + ruido_lento(N, 0.25, 0.03))
    voz_d = pulso_glotal(f_dem) * 4.0 + 1.6 * pulso_glotal(f_dem * 0.5)            # tono + subarmónico (una octava abajo)
    fry = (rng.random(N) < (f_dem * 0.35 / SR)).astype(float)                         # chasquidos irregulares de garganta
    fry = np.convolve(fry, np.hanning(60), mode="same") * 2.5
    rasp = rng.standard_normal(N) * 0.25
    fuente_d = voz_d + fry + rasp * (0.6 + 0.4 * np.abs(np.sin(2 * np.pi * 27 * tt)))  # gruñido que late a ~27 Hz
    Fd = np.array([300, 620, 1800, 2600]) * 0.72                                       # vocal "o/u" de una garganta gigante
    dem = np.zeros(N)
    for k, (ga, bw) in enumerate(zip([1.0, 0.75, 0.35, 0.2], [80, 110, 170, 220])):
        dem += ga * biquad_banda(fuente_d, np.full(N, Fd[k]) * (1 + 0.08 * np.sin(2 * np.pi * 0.4 * tt + k)), np.full(N, float(bw)))
    # "frases": lamentos que suben y bajan, como si hablara
    silabas = np.zeros(N)
    for c in np.arange(0.3, D * 0.85, 0.9) + rng.uniform(-0.1, 0.1, len(np.arange(0.3, D * 0.85, 0.9))):
        silabas += np.exp(-0.5 * ((tt - c) / 0.28) ** 2)
    env_d = np.clip(tt / 0.25, 0, 1) * np.clip((D - tt) / (D * 0.3), 0, 1) * (0.45 + 0.55 * np.clip(silabas, 0, 1))
    dem = np.tanh(3.0 * dem / np.abs(dem).max())
    # cuerpo grave sin filtrar (tono de 52 Hz + subarmónico de 26 Hz): los formantes casi los borraban y faltaba tamaño
    cuerpo = np.convolve(voz_d, np.ones(150) / 150, mode="same"); cuerpo = np.tanh(2.0 * cuerpo / np.abs(cuerpo).max())
    dem = (dem + 0.9 * cuerpo) * env_d
    mezcla = mezcla / np.abs(mezcla).max() * 0.5 + dem * 1.4

seco = np.concatenate([mezcla, np.zeros(int(SR * COLA))])
L = 0.55 * seco + 0.75 * reverb(mezcla, COLA, 1)
R = 0.55 * seco + 0.75 * reverb(mezcla, COLA, 2)

def fuerte(x):
    """Más fuerte sin saturar: compresión suave + limitador."""
    x = x / np.abs(x).max()
    env = np.convolve(np.abs(x), np.ones(2205) / 2205, mode="same") + 1e-4
    x = x * (0.55 / env) ** 0.45                               # sube lo suave, contiene lo fuerte
    x = np.tanh(1.6 * x / np.abs(x).max()) / np.tanh(1.6)
    return x * 0.97

st = np.stack([L, R], axis=1); st = fuerte(st.reshape(-1)).reshape(st.shape)
fade = int(SR * 0.08); st[-fade:] *= np.linspace(1, 0, fade)[:, None]
st[:int(SR * 0.01)] *= np.linspace(0, 1, int(SR * 0.01))[:, None]

with wave.open(sys.argv[1], "wb") as w:
    w.setnchannels(2); w.setsampwidth(2); w.setframerate(SR)
    w.writeframes((st * 32767).astype(np.int16).tobytes())
print("hecho")

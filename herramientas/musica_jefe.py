# Música original para el jefe opcional de la cueva (La Madre de la Guarida).
# Estilo: orquesta y coro oscuros, épicos, en re menor. Composición propia (progresiones y melodía originales), síntesis aditiva.
import sys, wave, numpy as np

SR = 32000
BPM = 92.0
BEAT = 60.0 / BPM
COMPAS = 4 * BEAT
rng = np.random.default_rng(1993)

def hz(nota):   # nota MIDI → Hz
    return 440.0 * 2 ** ((nota - 69) / 12.0)

# acordes (MIDI de la fundamental + intervalos), progresiones propias
ACORDES = {
    "Dm": (50, (0, 3, 7)), "Bb": (46, (0, 4, 7)), "Gm": (43, (0, 3, 7)), "A": (45, (0, 4, 7)),
    "F": (41, (0, 4, 7)), "C": (48, (0, 4, 7)), "Eb": (51, (0, 4, 7)), "Asus": (45, (0, 5, 7)), "Cm": (48, (0, 3, 7)),
}
SECCIONES = [
    ("intro",   ["Dm", "Dm", "Bb", "Asus"]),
    ("A",       ["Dm", "Bb", "Gm", "A", "Dm", "F", "C", "A"]),
    ("B",       ["Gm", "Eb", "Dm", "A", "Gm", "Eb", "Cm", "A"]),
    ("clímax",  ["Dm", "Bb", "Gm", "A", "Eb", "Cm", "Asus", "A"]),
    ("final",   ["Dm", "Bb", "A", "Dm"]),
]
# melodía propia (grados MIDI) para las secciones B y clímax: (compás relativo, pulso, nota, duración en pulsos)
MELODIA_B = [(0, 0, 62, 2), (0, 2, 65, 2), (1, 0, 63, 3), (1, 3, 62, 1), (2, 0, 62, 2), (2, 2, 60, 2), (3, 0, 61, 4),
             (4, 0, 67, 2), (4, 2, 65, 2), (5, 0, 63, 2), (5, 2, 62, 2), (6, 0, 60, 3), (6, 3, 58, 1), (7, 0, 61, 4)]
MELODIA_C = [(0, 0, 74, 2), (0, 2, 72, 1), (0, 3, 70, 1), (1, 0, 70, 2), (1, 2, 69, 2), (2, 0, 67, 3), (2, 3, 69, 1),
             (3, 0, 69, 4), (4, 0, 70, 2), (4, 2, 72, 2), (5, 0, 67, 3), (5, 3, 65, 1), (6, 0, 69, 2), (6, 2, 74, 2), (7, 0, 73, 4)]

total_compases = sum(len(c) for _, c in SECCIONES)
DUR = total_compases * COMPAS + 6.0
N = int(DUR * SR)
L = np.zeros(N); R = np.zeros(N)

def poner(señal, inicio, pan=0.0, ganancia=1.0):
    i = int(inicio * SR); j = min(N, i + len(señal))
    if j <= i: return
    s = señal[:j - i] * ganancia
    L[i:j] += s * np.sqrt(0.5 * (1 - pan)); R[i:j] += s * np.sqrt(0.5 * (1 + pan))

def envolvente(n, a, r, sostener=1.0):
    e = np.full(n, sostener); na = max(1, int(SR * a)); nr = max(1, min(n - 1, int(SR * r)))
    e[:na] = np.linspace(0, 1, na) ** 1.4; e[-nr:] *= np.linspace(1, 0, nr) ** 1.6
    return e

def aditiva(f0, dur, amp_k, vib=0.0, vib_f=5.0, deriva=0.0, fmax=7000.0):
    """Suma de armónicos (vectorizado). amp_k(k, fk) → amplitud del armónico k."""
    n = int(dur * SR); tt = np.arange(n) / SR
    f = f0 * (1 + vib * np.sin(2 * np.pi * (vib_f + rng.uniform(-0.4, 0.4)) * tt + rng.uniform(0, 6.28)))
    if deriva: f *= 1 + deriva * np.sin(2 * np.pi * rng.uniform(0.1, 0.3) * tt + rng.uniform(0, 6.28))
    p = 2 * np.pi * np.cumsum(f) / SR
    out = np.zeros(n); k = 1
    while k * f0 < fmax:
        a = amp_k(k, k * f0)
        if a > 1e-4: out += a * np.sin(k * p + rng.uniform(0, 6.28))
        k += 1
    return out

def vocal_ah(k, fk):   # envolvente espectral de "ah" coral
    return sum(g * np.exp(-0.5 * ((fk - fc) / bw) ** 2) for fc, bw, g in ((700, 130, 1.0), (1180, 160, 0.55), (2600, 260, 0.22), (3300, 300, 0.12))) / k ** 0.3

def cuerda(k, fk): return (1.0 / k) * (1 / (1 + (fk / 3500.0) ** 2))
def metal(k, fk): return (1.0 / k ** 0.7) * (1 / (1 + (fk / 2600.0) ** 3))
def organo(k, fk): return {1: 1.0, 2: 0.6, 3: 0.3, 4: 0.35, 6: 0.15, 8: 0.12}.get(k, 0.0)

def timbal(f0, dur=2.2, fuerza=1.0):
    n = int(dur * SR); tt = np.arange(n) / SR
    tono = sum(a * np.sin(2 * np.pi * f0 * r * tt) * np.exp(-tt * d) for r, a, d in ((1.0, 1.0, 2.2), (1.5, 0.45, 3.5), (1.98, 0.3, 4.5), (2.44, 0.2, 6.0)))
    golpe = rng.standard_normal(n) * np.exp(-tt * 40) * 0.6
    golpe = np.convolve(golpe, np.ones(12) / 12, "same")
    return (tono + golpe) * fuerza

def campana(f0, dur=6.0):
    n = int(dur * SR); tt = np.arange(n) / SR
    return sum(a * np.sin(2 * np.pi * f0 * r * tt) * np.exp(-tt * d) for r, a, d in ((0.5, 0.6, 0.35), (1.0, 1.0, 0.5), (1.19, 0.5, 0.7), (1.56, 0.4, 0.9), (2.0, 0.35, 1.0), (2.74, 0.25, 1.4), (3.76, 0.15, 2.0)))

# ---------------- composición ----------------
t0 = 0.5
for nombre, compases in SECCIONES:
    for c_i, ac in enumerate(compases):
        base, iv = ACORDES[ac]; ini = t0 + c_i * COMPAS
        intens = {"intro": 0.5, "A": 0.8, "B": 0.85, "clímax": 1.0, "final": 0.7}[nombre]
        # órgano y bajo de fondo
        poner(aditiva(hz(base - 12), COMPAS + 0.3, organo, deriva=0.001) * envolvente(int((COMPAS + 0.3) * SR), 0.3, 0.6), ini, 0.0, 0.10 * intens)
        # coro: 8 voces sobre el acorde (graves y agudas), entradas un poco desfasadas
        for v in range(8):
            nota = base + iv[v % 3] + (12 if v >= 3 else 0) + (12 if v >= 6 else 0)
            d = COMPAS + 0.6
            voz = aditiva(hz(nota) * (1 + rng.uniform(-0.004, 0.004)), d, vocal_ah, vib=0.006, vib_f=5.2, deriva=0.002, fmax=4500)
            poner(voz * envolvente(len(voz), 0.45, 0.8), ini + rng.uniform(0, 0.08), rng.uniform(-0.7, 0.7), 0.035 * intens * (0.6 if nombre == "intro" else 1))
        if nombre != "intro":
            # cuerdas graves: ostinato de corcheas (fundamental y octava), con acento en 1 y 3
            for b in range(8):
                nota = base - 12 + (0 if b % 4 != 2 else iv[2])
                d = BEAT * 0.45
                s = aditiva(hz(nota), d, cuerda, vib=0.002) * envolvente(int(d * SR), 0.01, 0.6)
                s += aditiva(hz(nota + 12), d, cuerda) * envolvente(int(d * SR), 0.01, 0.6) * 0.5
                poner(s, ini + b * BEAT / 2, -0.3 if b % 2 else 0.3, 0.09 * intens * (1.3 if b % 4 == 0 else 1.0))
            # metales: golpe largo en el pulso 1 (y en el 3 en el clímax)
            for pulso in ((0, 2) if nombre == "clímax" else (0,)):
                for nota in (base, base + iv[1], base + iv[2]):
                    d = BEAT * 1.6
                    poner(aditiva(hz(nota), d, metal, vib=0.003) * envolvente(int(d * SR), 0.06, 0.7), ini + pulso * BEAT, rng.uniform(-0.3, 0.3), 0.05 * intens)
            # timbales: fundamental en el 1, quinta en el 3, redoble en el último compás de la sección
            poner(timbal(hz(base - 24 + 12)), ini, 0.0, 0.35 * intens)
            poner(timbal(hz(base - 24 + 19), fuerza=0.7), ini + 2 * BEAT, 0.0, 0.3 * intens)
            if c_i == len(compases) - 1:
                for r in range(8): poner(timbal(hz(base - 12), 0.6, 0.4 + r * 0.08), ini + 2 * BEAT + r * BEAT / 4, 0.0, 0.3)
        else:
            if c_i == 0: poner(campana(hz(50)), ini, 0.0, 0.25)
            poner(timbal(hz(38), 3.0, 0.6), ini, 0.0, 0.3)
        # campana grave al empezar cada sección
        if c_i == 0 and nombre in ("A", "clímax"): poner(campana(hz(38)), ini, -0.2, 0.22)
    # melodía (metales + voces agudas al unísono) en B y clímax
    mel = MELODIA_B if nombre == "B" else MELODIA_C if nombre == "clímax" else None
    if mel:
        for cb, pulso, nota, dur in mel:
            d = dur * BEAT
            s = aditiva(hz(nota), d, metal, vib=0.006, vib_f=5.5) * envolvente(int(d * SR), 0.08, 0.35)
            s += aditiva(hz(nota), d, vocal_ah, vib=0.008, fmax=5000) * envolvente(int(d * SR), 0.1, 0.4) * 1.4
            poner(s, t0 + cb * COMPAS + pulso * BEAT, 0.1, 0.07)
    t0 += len(compases) * COMPAS
# golpe final
poner(timbal(hz(38), 4.0, 1.2), t0, 0.0, 0.5); poner(campana(hz(38), 7.0), t0, 0.0, 0.3)

# ---------------- reverb de catedral (estéreo) y mezcla final ----------------
def reverb(x, cola, semilla):
    r = np.random.default_rng(semilla); it = np.arange(int(SR * cola)) / SR
    ir = r.standard_normal(len(it)) * np.exp(-it * 6.9 / cola); ir = np.convolve(ir, np.ones(5) / 5, "same"); ir /= np.abs(ir).max()
    n = len(x) + len(ir)
    return np.fft.irfft(np.fft.rfft(x, n) * np.fft.rfft(ir, n))[:len(x)]

wl = reverb(L, 3.8, 1); wr = reverb(R, 3.8, 2)
L2 = L / np.abs(L).max() * 0.6 + wl / np.abs(wl).max() * 0.55
R2 = R / np.abs(R).max() * 0.6 + wr / np.abs(wr).max() * 0.55
st = np.stack([L2, R2], 1); st /= np.abs(st).max()
env = np.convolve(np.abs(st).max(1), np.ones(3200) / 3200, "same") + 1e-3
st *= ((0.5 / env) ** 0.35)[:, None]                     # compresión suave
st = np.tanh(1.5 * st / np.abs(st).max()) / np.tanh(1.5) * 0.95
fade = int(SR * 2.0); st[-fade:] *= np.linspace(1, 0, fade)[:, None]

with wave.open(sys.argv[1], "wb") as w:
    w.setnchannels(2); w.setsampwidth(2); w.setframerate(SR); w.writeframes((st * 32767).astype(np.int16).tobytes())
print("hecho", round(DUR, 1), "s")

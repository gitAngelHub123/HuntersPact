# Música original para la FASE 2 del jefe de la cueva: furiosa y agresiva (no triste).
# Uso: musica_jefe_furia.py salida.wav [bucle]
# 138 BPM, acentos 3+3+2, re frigio (choque re–mi bemol), tritonos, cuerdas en semicorcheas, golpes de metales,
# gritos del coro, tambores de guerra, yunque y piano grave. Composición propia, síntesis aditiva.
import sys, wave, numpy as np

SALIDA = sys.argv[1]; BUCLE = len(sys.argv) > 2 and sys.argv[2] == "bucle"
SR = 44100; BPM = 138.0; BEAT = 60.0 / BPM; COMPAS = 4 * BEAT; SEMI = BEAT / 4
rng = np.random.default_rng(666)
def hz(n): return 440.0 * 2 ** ((n - 69) / 12.0)

AC = {"Dm": (50, (0, 3, 7)), "Eb": (51, (0, 4, 7)), "Bb": (46, (0, 4, 7)), "A": (45, (0, 4, 7, 10)), "Gm": (43, (0, 3, 7)),
      "C#o": (49, (0, 3, 6)), "D5": (50, (0, 7)), "Ebo": (51, (0, 3, 6))}
SECCIONES = [("intro", ["D5", "D5"]),
             ("A", ["Dm", "Eb", "Dm", "Eb", "Bb", "A", "Dm", "A"]),
             ("B", ["Gm", "Eb", "Dm", "C#o", "Gm", "Eb", "A", "A"]),
             ("C", ["D5", "Ebo", "D5", "Ebo"]),
             ("clímax", ["Dm", "Eb", "Bb", "A", "Gm", "C#o", "Eb", "A"]),
             ("final", ["Dm", "D5"])]
if BUCLE: SECCIONES = [s for s in SECCIONES if s[0] != "final"]
ACENTOS = (0, 3, 6, 8, 11, 14)          # semicorcheas acentuadas en el compás (3+3+2 | 3+3+2)
# motivo de metales propio: cae cromático y remata en el tritono, como un rugido
RUGIDO = [(0, 74, 2), (2, 73, 2), (4, 72, 2), (6, 71, 1), (7, 68, 5), (12, 69, 4)]
CANTO = [(0, 62, 3), (3, 63, 3), (6, 62, 2), (8, 65, 3), (11, 63, 3), (14, 61, 2)]

total = sum(len(c) for _, c in SECCIONES); DUR = total * COMPAS + 4.0; N = int(DUR * SR)
L = np.zeros(N); R = np.zeros(N)

def poner(s, ini, pan=0.0, g=1.0):
    ini += rng.normal(0, 0.006); i = max(0, int(ini * SR)); j = min(N, i + len(s))
    if j <= i: return
    s = s[:j - i] * g; L[i:j] += s * np.sqrt(0.5 * (1 - pan)); R[i:j] += s * np.sqrt(0.5 * (1 + pan))

def env(n, a, r):
    e = np.ones(n); na = max(1, min(n - 1, int(SR * a))); nr = max(1, min(n - 1, int(SR * r)))
    e[:na] = np.linspace(0, 1, na) ** 1.2; e[-nr:] *= np.linspace(1, 0, nr) ** 1.5
    return e

def aditiva(f0, dur, esp, vib=0.0, fmax=7000.0, brillo_ataque=True):
    n = int(dur * SR); tt = np.arange(n) / SR
    f = f0 * (1 + vib * np.sin(2 * np.pi * rng.uniform(5, 6.5) * tt) + 0.002 * np.sin(2 * np.pi * rng.uniform(0.2, 0.5) * tt))
    p = 2 * np.pi * np.cumsum(f) / SR; out = np.zeros(n); k = 1
    cierre = np.exp(-tt * 6) * 0.6 + 0.4 if brillo_ataque else 1.0       # brillo fuerte al atacar, luego se cierra
    while k * f0 < fmax:
        a = esp(k, k * f0)
        if a > 1e-4: out += a * np.sin(k * p + rng.uniform(0, 6.28)) * (cierre ** (k * 0.08) if brillo_ataque else 1)
        k += 1
    return out

def seccion(f0, dur, esp, copias, des=0.005, **kw):
    n = int(dur * SR); out = np.zeros(n)
    for _ in range(copias):
        s = aditiva(f0 * (1 + rng.uniform(-des, des)), dur, esp, **kw); d = int(rng.uniform(0, 0.012) * SR); out[d:] += s[:n - d]
    return out / copias

def cuerda_aspera(k, fk): return (1.0 / k ** 0.85) / (1 + (fk / 4200.0) ** 2)
def metal_furioso(k, fk): return (1.0 / k ** 0.5) / (1 + (fk / 3200.0) ** 3)
def tuba(k, fk): return (1.0 / k ** 0.8) / (1 + (fk / 900.0) ** 3)
def gauss(fk, picos): return sum(g * np.exp(-0.5 * ((fk - fc) / bw) ** 2) for fc, bw, g in picos)
def grito_ha(k, fk): return gauss(fk, ((800, 160, 1.0), (1250, 200, 0.7), (2700, 320, 0.35), (3500, 380, 0.2))) / k ** 0.2

def ruido_banda(n, fc, ancho):
    X = np.fft.rfft(rng.standard_normal(n)); fr = np.fft.rfftfreq(n, 1 / SR)
    X *= np.exp(-0.5 * ((fr - fc) / ancho) ** 2); return np.fft.irfft(X, n)

def grito(nota, dur=0.32):
    """Grito corto del coro ("¡ha!"): voces con aire y caída rápida."""
    n = int(dur * SR); s = seccion(hz(nota), dur, grito_ha, 4, des=0.012, vib=0.0, fmax=5000)
    s += ruido_banda(n, 1100, 600) / 25.0
    return np.tanh(1.8 * s / np.abs(s).max()) * env(n, 0.012, 0.6)

def tambor(dur=0.9, f0=62.0, fuerza=1.0):
    n = int(dur * SR); tt = np.arange(n) / SR
    cuerpo = np.sin(2 * np.pi * f0 * tt * (1 + 0.8 * np.exp(-tt * 28))) * np.exp(-tt * 7)
    piel = np.convolve(rng.standard_normal(n) * np.exp(-tt * 22), np.ones(10) / 10, "same") * 0.7
    return np.tanh(1.5 * (cuerpo + piel)) * fuerza

def caja(dur=0.25, fuerza=1.0):
    n = int(dur * SR); tt = np.arange(n) / SR
    return (ruido_banda(n, 2500, 1800) * 6 * np.exp(-tt * 25) + np.sin(2 * np.pi * 190 * tt) * np.exp(-tt * 30) * 0.6) * fuerza

def timbal(f0, dur=1.6, fuerza=1.0):
    n = int(dur * SR); tt = np.arange(n) / SR
    return (sum(a * np.sin(2 * np.pi * f0 * r * tt) * np.exp(-tt * d) for r, a, d in ((1, 1, 2.5), (1.5, 0.4, 4), (1.98, 0.25, 5)))
            + np.convolve(rng.standard_normal(n) * np.exp(-tt * 35), np.ones(20) / 20, "same") * 0.6) * fuerza

def yunque(dur=1.6):
    n = int(dur * SR); tt = np.arange(n) / SR
    return sum(a * np.sin(2 * np.pi * f * tt) * np.exp(-tt * d) for f, a, d in ((1180, 1.0, 3.0), (2730, 0.7, 4.5), (4310, 0.45, 6), (6020, 0.3, 8))) + rng.standard_normal(n) * np.exp(-tt * 60) * 0.5

def piano_grave(notas, dur=0.6):
    n = int(dur * SR); tt = np.arange(n) / SR; out = np.zeros(n)
    for nota in notas:
        f0 = hz(nota); k = 1
        while k * f0 < 5000 and k <= 25:
            out += (1 / k ** 1.1) * np.sin(2 * np.pi * k * f0 * np.sqrt(1 + 0.0004 * k * k) * tt) * np.exp(-tt * (1.5 + 0.4 * k)); k += 1
    return np.tanh(1.4 * out / max(1e-9, np.abs(out).max())) * env(n, 0.002, 0.3)

INT = {"intro": 0.8, "A": 0.85, "B": 0.92, "C": 0.8, "clímax": 1.0, "final": 0.9}
t0 = 0.4
for nombre, comp in SECCIONES:
    for ci, ac in enumerate(comp):
        base, iv = AC[ac]; ini = t0 + ci * COMPAS; it = INT[nombre]
        # cuerdas graves: semicorcheas sin pausa, con la nota vecina medio tono arriba (re–mi bemol) y acentos 3+3+2
        if nombre != "intro":
            for s16 in range(16):
                nota = base - 12 + (1 if s16 in (5, 13) else 0) + (12 if s16 in (7, 15) else 0)
                acento = s16 in ACENTOS
                d = SEMI * 0.95
                sn = seccion(hz(nota), d, cuerda_aspera, 3, des=0.004) * env(int(d * SR), 0.004, 0.45)
                poner(np.tanh(1.6 * sn), ini + s16 * SEMI, -0.3 if s16 % 2 else 0.3, (0.11 if acento else 0.065) * it)
        # golpes de metales en los acentos (acorde completo, cortos y con mordida)
        for s16 in (ACENTOS if nombre in ("A", "clímax", "intro") else (0, 6, 11)):
            d = SEMI * (2.2 if s16 in (6, 14) else 1.4)
            for nota in [base + x for x in iv] + [base - 12]:
                sn = seccion(hz(nota), d, metal_furioso, 2, des=0.006) * env(int(d * SR), 0.01, 0.5)
                poner(np.tanh(2.0 * sn), ini + s16 * SEMI, rng.uniform(-0.4, 0.4), 0.045 * it)
        # tuba y piano grave en el 1 y en el 3 (yunque al empezar cada sección)
        poner(seccion(hz(base - 24), BEAT * 1.6, tuba, 2) * env(int(BEAT * 1.6 * SR), 0.01, 0.6), ini, 0.0, 0.10 * it)
        poner(piano_grave([base - 24, base - 12, base - 11 if ac in ("Dm", "D5") else base - 12 + iv[1]]), ini, -0.15, 0.10 * it)
        poner(piano_grave([base - 12, base - 6]), ini + 2 * BEAT + SEMI * 2, 0.15, 0.08 * it)   # tritono golpeado
        if ci == 0 and nombre != "intro": poner(yunque(), ini, 0.2, 0.09)
        # percusión: tambores de guerra en corcheas con acentos, caja en los contratiempos, timbal en el 1
        for s8 in range(8):
            fuerte = s8 in (0, 3, 6)
            poner(tambor(f0=58 if fuerte else 70, fuerza=1.0 if fuerte else 0.55), ini + s8 * BEAT / 2, rng.uniform(-0.25, 0.25), 0.16 * it)
        for s16 in (4, 12, 14) if nombre != "C" else (4, 12):
            poner(caja(fuerza=0.8), ini + s16 * SEMI, 0.1, 0.06 * it)
        poner(timbal(hz(base - 24 + 12)), ini, 0.0, 0.22 * it)
        if ci == len(comp) - 1:
            for r in range(12): poner(caja(0.15, 0.4 + r * 0.05), ini + 2 * BEAT + r * SEMI * 0.66, 0.0, 0.07)   # redoble que empuja
        # gritos del coro en los acentos (B, C y clímax)
        if nombre in ("B", "C", "clímax"):
            for s16 in (0, 6, 11) if nombre != "C" else (0, 3, 6, 8):
                for nota in (base + 12, base + 12 + iv[1], base + 24):
                    poner(grito(nota), ini + s16 * SEMI, rng.uniform(-0.6, 0.6), 0.05 * it)
    # melodía: rugido de metales (A y clímax) y canto del coro (B)
    for ci in range(0, len(comp), 2):
        ini = t0 + ci * COMPAS
        if nombre in ("A", "clímax"):
            for s16, nota, largo in RUGIDO:
                d = largo * SEMI * 1.05
                sn = seccion(hz(nota - (0 if nombre == "clímax" else 12)), d, metal_furioso, 3, des=0.004, vib=0.006) * env(int(d * SR), 0.02, 0.35)
                poner(np.tanh(1.8 * sn), ini + s16 * SEMI, 0.05, 0.075)
                if nombre == "clímax": poner(grito(nota - 12, d), ini + s16 * SEMI, -0.1, 0.05)
        if nombre == "B":
            for s16, nota, largo in CANTO:
                d = largo * SEMI
                poner(grito(nota + 12, d), ini + s16 * SEMI, 0.0, 0.07)
    t0 += len(comp) * COMPAS
FIN = t0
if not BUCLE:
    poner(yunque(2.5), t0, 0.0, 0.15); poner(timbal(hz(26), 3.0, 1.3), t0, 0.0, 0.4); poner(tambor(1.5, 50, 1.4), t0, 0.0, 0.3)

# ---------------- sala corta (pegada) y masterizado con fuerza ----------------
def sala(x, cola, semilla):
    r = np.random.default_rng(semilla); it = np.arange(int(SR * cola)) / SR
    ir = r.standard_normal(len(it)) * np.exp(-it * 6.9 / cola); ir[:int(0.012 * SR)] = 0
    for dly, g in ((0.013, 0.5), (0.027, 0.35), (0.041, 0.25)): ir[int(dly * SR)] += g * 8
    n = len(x) + len(ir); IR = np.fft.rfft(ir, n); fr = np.fft.rfftfreq(n, 1 / SR); IR *= 1 / (1 + (fr / 5000.0) ** 2)
    y = np.fft.irfft(np.fft.rfft(x, n) * IR, n)[:len(x)]; return y / np.abs(y).max()

wl, wr = sala(L, 2.2, 7), sala(R, 2.2, 8)
st = np.stack([L / np.abs(L).max() * 0.8 + wl * 0.35, R / np.abs(R).max() * 0.8 + wr * 0.35], 1); st /= np.abs(st).max()
envl = np.convolve(np.abs(st).max(1), np.ones(1323) / 1323, "same") + 1e-3
st *= ((0.5 / envl) ** 0.35)[:, None]
st = np.tanh(1.8 * st / np.abs(st).max()) / np.tanh(1.8) * 0.95
if BUCLE:
    i0, i1 = int(0.4 * SR), int(FIN * SR); cuerpo = st[i0:i1].copy(); cola = st[i1:]
    m = min(len(cola), len(cuerpo)); cuerpo[:m] += cola[:m]; st = cuerpo / max(1.0, np.abs(cuerpo).max() / 0.95)
else:
    f = int(SR * 1.5); st[-f:] *= np.linspace(1, 0, f)[:, None]
with wave.open(SALIDA, "wb") as w:
    w.setnchannels(2); w.setsampwidth(2); w.setframerate(SR); w.writeframes((st * 32767).astype(np.int16).tobytes())
print("hecho", "bucle" if BUCLE else "escuchar", round(DUR, 1), "s")

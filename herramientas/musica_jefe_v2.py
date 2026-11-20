# Música original para el jefe opcional (La Madre de la Guarida), versión 2: más humana, mejor sonido y más suave.
# Uso: musica_jefe_v2.py salida.wav fase1|fase2
# fase1: solemne y oscura; fase2: la misma música, más rápida e intensa. Composición propia, síntesis aditiva.
import sys, wave, numpy as np

SALIDA, MODO = sys.argv[1], (sys.argv[2] if len(sys.argv) > 2 else "fase1")
F2 = MODO == "fase2"
BUCLE = len(sys.argv) > 3 and sys.argv[3] == "bucle"   # para el juego: sin final y sin cortes al repetir
SR = 44100
BPM = 106.0 if F2 else 88.0
BEAT = 60.0 / BPM; COMPAS = 4 * BEAT
rng = np.random.default_rng(2024 if F2 else 1993)

def hz(n): return 440.0 * 2 ** ((n - 69) / 12.0)

ACORDES = {"Dm": (50, (0, 3, 7)), "Bb": (46, (0, 4, 7)), "Gm": (43, (0, 3, 7)), "A": (45, (0, 4, 7)), "F": (41, (0, 4, 7)),
           "C": (48, (0, 4, 7)), "Eb": (51, (0, 4, 7)), "Asus": (45, (0, 5, 7)), "Cm": (48, (0, 3, 7))}
SECCIONES = [("intro", ["Dm", "Dm", "Bb", "Asus"]),
             ("A", ["Dm", "Bb", "Gm", "A", "Dm", "F", "C", "A"]),
             ("B", ["Gm", "Eb", "Dm", "A", "Gm", "Eb", "Cm", "A"]),
             ("clímax", ["Dm", "Bb", "Gm", "A", "Eb", "Cm", "Asus", "A"]),
             ("final", ["Dm", "Bb", "A", "Dm"])]
if F2: SECCIONES = [("intro", ["Dm", "Asus"])] + SECCIONES[1:4] + [("clímax", ["Gm", "Eb", "Dm", "A", "Gm", "Cm", "Asus", "A"]), ("final", ["Dm", "Bb", "A", "Dm"])]
if BUCLE: SECCIONES = [sc for sc in SECCIONES if sc[0] != "final"]
MELODIA_B = [(0, 0, 62, 2), (0, 2, 65, 2), (1, 0, 63, 3), (1, 3, 62, 1), (2, 0, 62, 2), (2, 2, 60, 2), (3, 0, 61, 4),
             (4, 0, 67, 2), (4, 2, 65, 2), (5, 0, 63, 2), (5, 2, 62, 2), (6, 0, 60, 3), (6, 3, 58, 1), (7, 0, 61, 4)]
MELODIA_C = [(0, 0, 74, 2), (0, 2, 72, 1), (0, 3, 70, 1), (1, 0, 70, 2), (1, 2, 69, 2), (2, 0, 67, 3), (2, 3, 69, 1),
             (3, 0, 69, 4), (4, 0, 70, 2), (4, 2, 72, 2), (5, 0, 67, 3), (5, 3, 65, 1), (6, 0, 69, 2), (6, 2, 74, 2), (7, 0, 73, 4)]

# fase 2: melodía nueva para la sección A y contramelodía aguda para el clímax (composición propia)
MELODIA_A2 = [(0, 0, 69, 1), (0, 1, 70, 1), (0, 2, 69, 2), (1, 0, 65, 2), (1, 2, 67, 1), (1, 3, 69, 1), (2, 0, 70, 3), (2, 3, 72, 1),
              (3, 0, 73, 4), (4, 0, 74, 2), (4, 2, 72, 2), (5, 0, 69, 2), (5, 2, 65, 2), (6, 0, 67, 2), (6, 2, 64, 2), (7, 0, 69, 4)]
CONTRA_C = [(0, 0, 81, 1), (0, 1, 79, 1), (0, 2, 77, 2), (1, 0, 79, 2), (1, 2, 74, 2), (2, 0, 77, 1), (2, 1, 76, 1), (2, 2, 74, 2),
            (3, 0, 76, 4), (4, 0, 79, 2), (4, 2, 82, 2), (5, 0, 79, 3), (5, 3, 75, 1), (6, 0, 76, 2), (6, 2, 81, 2), (7, 0, 81, 4)]
total = sum(len(c) for _, c in SECCIONES)
DUR = total * COMPAS + 7.0; N = int(DUR * SR)
L = np.zeros(N); R = np.zeros(N)

def poner(s, ini, pan=0.0, g=1.0):
    ini += rng.normal(0, 0.012)                       # humano: nada cae exacto en el pulso
    i = max(0, int(ini * SR)); j = min(N, i + len(s))
    if j <= i: return
    s = s[:j - i] * g
    L[i:j] += s * np.sqrt(0.5 * (1 - pan)); R[i:j] += s * np.sqrt(0.5 * (1 + pan))

def env(n, a, r, curva=1.6):
    e = np.ones(n); na = max(1, min(n - 1, int(SR * a))); nr = max(1, min(n - 1, int(SR * r)))
    e[:na] = (np.sin(np.linspace(0, np.pi / 2, na))) ** 2; e[-nr:] *= np.linspace(1, 0, nr) ** curva
    return e

def aditiva(f0, dur, espectro, vib=0.0, vib_ret=0.4, deriva=0.003, fmax=6000.0, brillo=1.0):
    """Armónicos con vibrato que entra de a poco, deriva lenta de afinación y brillo que se abre con la nota."""
    n = int(dur * SR); tt = np.arange(n) / SR
    rv = np.clip(tt / max(vib_ret, 1e-3), 0, 1)
    f = f0 * (1 + vib * rv * np.sin(2 * np.pi * rng.uniform(4.6, 5.8) * tt + rng.uniform(0, 6.28)))
    f *= 1 + deriva * np.sin(2 * np.pi * rng.uniform(0.07, 0.25) * tt + rng.uniform(0, 6.28))
    p = 2 * np.pi * np.cumsum(f) / SR
    abre = np.clip(tt / 0.25, 0.35, 1.0) ** 0.5             # los agudos se abren al atacar (menos "máquina")
    out = np.zeros(n); k = 1
    while k * f0 < fmax:
        a = espectro(k, k * f0)
        if a > 1e-4: out += a * np.sin(k * p + rng.uniform(0, 6.28)) * (abre ** ((k - 1) * 0.15 * brillo))
        k += 1
    return out

def seccion(f0, dur, espectro, copias, des=0.006, **kw):
    """Varias copias levemente desafinadas y desfasadas = una sección, no un instrumento."""
    n = int(dur * SR); out = np.zeros(n)
    for c in range(copias):
        s = aditiva(f0 * (1 + rng.uniform(-des, des)), dur, espectro, **kw)
        d = int(rng.uniform(0, 0.03) * SR); out[d:] += s[:n - d] * rng.uniform(0.75, 1.0)
    return out / copias

def gauss(fk, picos): return sum(g * np.exp(-0.5 * ((fk - fc) / bw) ** 2) for fc, bw, g in picos)
def coro_ah(k, fk): return gauss(fk, ((680, 120, 1.0), (1100, 150, 0.5), (2500, 250, 0.16), (3200, 280, 0.08))) / k ** 0.35
def coro_oh(k, fk): return gauss(fk, ((480, 100, 1.0), (850, 130, 0.45), (2400, 240, 0.12))) / k ** 0.35
def cuerdas(k, fk): return (1.0 / k) / (1 + (fk / 2800.0) ** 2.5)
def cornos(k, fk): return (1.0 / k ** 0.9) / (1 + (fk / 1500.0) ** 3) * (1.0 if k > 1 else 0.8)
def metales_f2(k, fk): return (1.0 / k ** 0.6) / (1 + (fk / 2400.0) ** 3)
def organo(k, fk): return {1: 1.0, 2: 0.55, 3: 0.25, 4: 0.3, 6: 0.1}.get(k, 0.0)

def aire(dur, fc=900.0, q=1.5):
    """Respiración del coro: ruido con forma de vocal (filtrado en frecuencia)."""
    n = int(dur * SR); x = rng.standard_normal(n); X = np.fft.rfft(x); fr = np.fft.rfftfreq(n, 1 / SR)
    X *= np.exp(-0.5 * ((fr - fc) / (fc / q)) ** 2) + 0.4 * np.exp(-0.5 * ((fr - 2400) / 600) ** 2)
    return np.fft.irfft(X, n) / 30.0

def timbal(f0, dur=2.6, fuerza=1.0):
    n = int(dur * SR); tt = np.arange(n) / SR
    tono = sum(a * np.sin(2 * np.pi * f0 * r * tt * (1 + 0.02 * np.exp(-tt * 8))) * np.exp(-tt * d)
               for r, a, d in ((1.0, 1.0, 1.8), (1.5, 0.35, 3.0), (1.98, 0.22, 4.0), (2.44, 0.12, 5.5)))
    piel = np.convolve(rng.standard_normal(n) * np.exp(-tt * 30), np.ones(30) / 30, "same") * 0.5   # golpe blando
    return (tono + piel) * fuerza

def tambor_guerra(dur=1.2, fuerza=1.0):
    n = int(dur * SR); tt = np.arange(n) / SR
    cuerpo = np.sin(2 * np.pi * 58 * tt * (1 + 0.6 * np.exp(-tt * 25))) * np.exp(-tt * 5)
    piel = np.convolve(rng.standard_normal(n) * np.exp(-tt * 18), np.ones(14) / 14, "same") * 0.6
    return (cuerpo + piel) * fuerza

def campana(f0, dur=7.0):
    n = int(dur * SR); tt = np.arange(n) / SR
    return sum(a * np.sin(2 * np.pi * f0 * r * tt) * np.exp(-tt * d)
               for r, a, d in ((0.5, 0.55, 0.3), (1.0, 1.0, 0.45), (1.19, 0.45, 0.65), (1.56, 0.35, 0.85), (2.0, 0.3, 1.0), (2.74, 0.2, 1.4)))

def piano(f0, dur, fuerza=1.0):
    """Piano: parciales levemente estirados (inarmonía de cuerda), cada uno se apaga a su ritmo, golpe de martillo."""
    n = int(dur * SR); tt = np.arange(n) / SR; out = np.zeros(n); B = 0.00035; k = 1
    while k * f0 < 7000 and k <= 30:
        fk = k * f0 * np.sqrt(1 + B * k * k)
        out += (1.0 / k ** 1.15) * np.sin(2 * np.pi * fk * tt + rng.uniform(0, 6.28)) * np.exp(-tt * (0.9 + 0.35 * k) * (1 + f0 / 900))
        k += 1
    martillo = np.convolve(rng.standard_normal(n) * np.exp(-tt * 120), np.ones(6) / 6, "same") * 0.25
    e = np.ones(n); r = int(0.12 * SR); e[-r:] = np.linspace(1, 0, r)
    return (out + martillo) * e * fuerza

def platillo(dur=2.5):
    """Platillo que crece hasta el cambio de sección (ruido metálico filtrado)."""
    n = int(dur * SR); x = rng.standard_normal(n); X = np.fft.rfft(x); fr = np.fft.rfftfreq(n, 1 / SR)
    X *= (fr > 3000) * (1 / (1 + (fr / 12000) ** 2)); y = np.fft.irfft(X, n)
    return y / np.abs(y).max() * np.linspace(0, 1, n) ** 2.2

INT = {"intro": 0.45, "A": 0.7, "B": 0.78, "clímax": 0.95, "final": 0.6}
t0 = 0.6
for nombre, comp in SECCIONES:
    for ci, ac in enumerate(comp):
        base, iv = ACORDES[ac]; ini = t0 + ci * COMPAS; it = INT[nombre] * (1.15 if F2 else 1.0)
        # órgano grave de fondo
        d = COMPAS + 0.5
        poner(aditiva(hz(base - 12), d, organo, deriva=0.001) * env(int(d * SR), 0.6, 0.9), ini, 0.0, 0.07 * it)
        # coro (12 voces en secciones), vocal ah/oh alternada, con aire
        for v in range(6):
            nota = base + iv[v % 3] + (12 if v >= 3 else 0) + (12 if F2 and v >= 4 else 0)
            d = COMPAS + 1.0; esp = coro_ah if (ci + v) % 2 == 0 else coro_oh
            s = seccion(hz(nota), d, esp, 2, des=0.005, vib=0.007, vib_ret=0.6, fmax=4200) * env(int(d * SR), 0.7, 1.1)
            s += aire(d, 750 if esp is coro_oh else 900) * env(int(d * SR), 0.7, 1.1)
            poner(s, ini + rng.uniform(0, 0.12), rng.uniform(-0.75, 0.75), 0.05 * it * (0.7 if nombre == "intro" else 1))
        if nombre != "intro" or F2:
            # cuerdas: pulso legato (fase 1, corcheas suaves) o semicorcheas tensas (fase 2)
            sub = 4 if F2 else 2
            for b in range(4 * sub):
                nota = base - 12 + (iv[2] if b % (2 * sub) == sub else 0)
                d = BEAT / sub * (0.85 if F2 else 1.1)
                acento = 1.25 if b % sub == 0 else 0.8 + rng.uniform(-0.1, 0.1)
                s = seccion(hz(nota), d, cuerdas, 3, des=0.004, vib=0.003, vib_ret=0.2) * env(int(d * SR), 0.035 if F2 else 0.07, 0.5)
                poner(s, ini + b * BEAT / sub, -0.35 if b % 2 else 0.35, (0.07 if F2 else 0.05) * it * acento)
            # cuerdas largas sosteniendo el acorde (colchón)
            d = COMPAS + 0.4
            for nota in (base + iv[1], base + iv[2] + 12):
                poner(seccion(hz(nota), d, cuerdas, 4, des=0.005, vib=0.006, vib_ret=0.8) * env(int(d * SR), 0.5, 0.8), ini, rng.uniform(-0.5, 0.5), 0.035 * it)
            # cornos (fase 1: crescendo suave al inicio del compás; fase 2: metales en 1 y 3)
            for pulso in ((0, 2) if F2 else (0,)):
                for nota in (base, base + iv[1], base + iv[2]):
                    d = BEAT * (1.4 if F2 else 2.6)
                    s = seccion(hz(nota), d, metales_f2 if F2 else cornos, 3, des=0.004, vib=0.004, brillo=1.3 if F2 else 0.8)
                    poner(s * env(int(d * SR), 0.05 if F2 else 0.35, 0.6), ini + pulso * BEAT, rng.uniform(-0.3, 0.3), (0.05 if F2 else 0.035) * it)
            # percusión
            poner(timbal(hz(base - 12)), ini, 0.0, 0.22 * it)
            poner(timbal(hz(base - 5), fuerza=0.6), ini + 2 * BEAT, 0.0, 0.18 * it)
            if F2:
                for b in range(8): poner(tambor_guerra(fuerza=1.0 if b % 2 == 0 else 0.6), ini + b * BEAT / 2, rng.uniform(-0.2, 0.2), 0.16 * it)
            if ci == len(comp) - 1:
                for r in range(8 if F2 else 6): poner(timbal(hz(base - 12), 0.7, 0.3 + r * 0.07), ini + 2 * BEAT + r * BEAT / 4, 0.0, 0.2)
        else:
            poner(timbal(hz(38), 3.2, 0.5), ini, 0.0, 0.2)
            if ci == 0: poner(campana(hz(50)), ini, 0.0, 0.18)
        if ci == 0 and nombre in ("A", "clímax"): poner(campana(hz(38)), ini, -0.2, 0.16)
        if F2 and nombre != "intro":
            # piano: arpegio de corcheas sobre el acorde + octavas graves en el 1
            arp = [base + 12, base + 12 + iv[1], base + 12 + iv[2], base + 24, base + 24 + iv[1], base + 24, base + 12 + iv[2], base + 12 + iv[1]]
            for b, nota in enumerate(arp): poner(piano(hz(nota), BEAT * 0.9, 0.9 if b % 4 == 0 else 0.65), ini + b * BEAT / 2, 0.25, 0.05 * it)
            for nota in (base - 12, base): poner(piano(hz(nota), COMPAS * 0.9, 1.1), ini, -0.1, 0.06 * it)
            # más voces: sopranos en la quinta y la octava del acorde
            for nota in (base + 24 + iv[2], base + 36):
                d = COMPAS + 0.8
                s = seccion(hz(nota), d, coro_ah, 3, des=0.005, vib=0.009, vib_ret=0.5, fmax=5000) * env(int(d * SR), 0.5, 0.9)
                poner(s, ini + rng.uniform(0, 0.1), rng.uniform(-0.6, 0.6), 0.03 * it)
            if ci == len(comp) - 1: poner(platillo(COMPAS * 0.9), ini + COMPAS * 0.1, 0.0, 0.08)
    if F2 and nombre in ("A", "clímax"):
        for cb, pulso, nota, dur in (MELODIA_A2 if nombre == "A" else CONTRA_C):
            d = dur * BEAT + 0.1
            s = seccion(hz(nota), d, cuerdas, 4, des=0.004, vib=0.01, vib_ret=0.3) * env(int(d * SR), 0.06, 0.35)
            s += piano(hz(nota), d, 0.8) * 0.6
            poner(s, t0 + cb * COMPAS + pulso * BEAT, -0.15, 0.06)
    mel = MELODIA_B if nombre == "B" else MELODIA_C if nombre == "clímax" else None
    if mel:
        for cb, pulso, nota, dur in mel:
            d = dur * BEAT + 0.15
            s = seccion(hz(nota), d, cornos if not F2 else metales_f2, 3, des=0.003, vib=0.008, vib_ret=0.5, brillo=0.9) * env(int(d * SR), 0.12, 0.4)
            s += seccion(hz(nota + (12 if F2 else 0)), d, coro_ah, 3, des=0.004, vib=0.01, vib_ret=0.5, fmax=4500) * env(int(d * SR), 0.15, 0.45) * 1.2
            poner(s, t0 + cb * COMPAS + pulso * BEAT, 0.08, 0.075 if F2 else 0.06)
    t0 += len(comp) * COMPAS
if not BUCLE: poner(timbal(hz(38), 4.5, 1.0), t0, 0.0, 0.4); poner(campana(hz(38), 8.0), t0, 0.0, 0.22)
FIN_MUSICA = t0

# ---------------- sala (estéreo, primeras reflexiones + cola), ecualización suave y masterizado tranquilo ----------------
def sala(x, cola, semilla, predelay=0.025):
    r = np.random.default_rng(semilla); it = np.arange(int(SR * cola)) / SR
    ir = r.standard_normal(len(it)) * np.exp(-it * 6.9 / cola)
    ir[:int(predelay * SR)] = 0
    for dly, g in ((0.017, 0.5), (0.031, 0.4), (0.052, 0.32), (0.077, 0.25), (0.11, 0.2)): ir[int(dly * SR)] += g * 10
    n = len(x) + len(ir); IR = np.fft.rfft(ir, n); fr = np.fft.rfftfreq(n, 1 / SR)
    IR *= 1 / (1 + (fr / 4500.0) ** 2)                     # cola oscura, sin brillos ásperos
    y = np.fft.irfft(np.fft.rfft(x, n) * IR, n)[:len(x)]
    return y / np.abs(y).max()

def eq_suave(x):
    X = np.fft.rfft(x); fr = np.fft.rfftfreq(len(x), 1 / SR)
    X *= 1 - 0.35 * np.exp(-0.5 * ((fr - 3800) / 1200) ** 2)   # quita la dureza de 3–5 kHz
    X *= 1 / (1 + (fr / 11000.0) ** 4)
    return np.fft.irfft(X, len(x))

wl, wr = sala(L, 4.2, 1), sala(R, 4.2, 2)
mezcla_seca = 0.55 if F2 else 0.45
Lf = eq_suave(L / np.abs(L).max() * mezcla_seca + wl * 0.6)
Rf = eq_suave(R / np.abs(R).max() * mezcla_seca + wr * 0.6)
st = np.stack([Lf, Rf], 1); st /= np.abs(st).max()
envl = np.convolve(np.abs(st).max(1), np.ones(4410) / 4410, "same") + 1e-3
st *= ((0.45 / envl) ** (0.25 if F2 else 0.15))[:, None]          # compresión leve (más suave en la fase 1)
st = np.tanh(1.2 * st / np.abs(st).max()) / np.tanh(1.2) * 0.92
if BUCLE:
    i0, i1 = int(0.6 * SR), int(FIN_MUSICA * SR)
    cuerpo = st[i0:i1].copy(); cola = st[i1:]
    m = min(len(cola), len(cuerpo)); cuerpo[:m] += cola[:m]          # la cola del final suena sobre el comienzo: sin corte
    st = cuerpo / max(1.0, np.abs(cuerpo).max() / 0.95)
else:
    fade = int(SR * 2.5); st[-fade:] *= np.linspace(1, 0, fade)[:, None]; st[:int(SR * 0.02)] *= np.linspace(0, 1, int(SR * 0.02))[:, None]

with wave.open(SALIDA, "wb") as w:
    w.setnchannels(2); w.setsampwidth(2); w.setframerate(SR); w.writeframes((st * 32767).astype(np.int16).tobytes())
print("hecho", MODO, round(DUR, 1), "s")

# "El último murciélago": música original para la FASE 2 del jefe de la cueva.
# Mezcla tristeza, furia, intensidad y vida o muerte. Parte de la base propia de la fase 2 (acordes y melodías del mod).
# Uso: musica_jefe_ultimo.py salida.wav [bucle]
import sys, wave, numpy as np

SALIDA = sys.argv[1]; MODO = sys.argv[2] if len(sys.argv) > 2 else "escuchar"   # escuchar | bucle | entrada
BUCLE = MODO == "bucle"; SOLO_ENTRADA = MODO == "entrada"
SR = 44100; BPM = 112.0; BEAT = 60.0 / BPM; COMPAS = 4 * BEAT; SEMI = BEAT / 4
rng = np.random.default_rng(1131)
def hz(n): return 440.0 * 2 ** ((n - 69) / 12.0)

AC = {"Dm": (50, (0, 3, 7)), "Bb": (46, (0, 4, 7)), "Gm": (43, (0, 3, 7)), "A": (45, (0, 4, 7)), "F": (41, (0, 4, 7)),
      "C": (48, (0, 4, 7)), "Eb": (51, (0, 4, 7)), "Asus": (45, (0, 5, 7)), "Cm": (48, (0, 3, 7))}
# (nombre, acordes, transposición en semitonos)
SECCIONES = [("soledad", ["Dm", "Bb", "Gm", "A"] if not BUCLE else ["Dm", "A"], 0),
             ("furia", ["Dm", "Bb", "Gm", "A", "Dm", "F", "C", "A"], 0),
             ("tristeza", ["Gm", "Eb", "Dm", "A", "Gm", "Eb", "Cm", "A"], 0),
             ("vida o muerte", ["Dm", "Dm", "Bb", "A"], 0),
             ("clímax", ["Dm", "Bb", "Gm", "A", "Eb", "Cm", "Asus", "A"], 1),
             ("final", ["Dm"], 1)]
if BUCLE: SECCIONES = [s for s in SECCIONES if s[0] != "final"]
# (2026-11-19) entrada épica de jefe: suena una vez al empezar la fase 2 (en el juego va en su propio archivo)
ENTRADA = ("entrada", ["Dm", "Dm", "Bb", "A"], 0)
if SOLO_ENTRADA: SECCIONES = [ENTRADA]
elif not BUCLE: SECCIONES = [ENTRADA] + SECCIONES
MEL_TRISTE = [(0, 0, 62, 2), (0, 2, 65, 2), (1, 0, 63, 3), (1, 3, 62, 1), (2, 0, 62, 2), (2, 2, 60, 2), (3, 0, 61, 4),
              (4, 0, 67, 2), (4, 2, 65, 2), (5, 0, 63, 2), (5, 2, 62, 2), (6, 0, 60, 3), (6, 3, 58, 1), (7, 0, 61, 4)]
MEL_FURIA = [(0, 0, 69, 1), (0, 1, 70, 1), (0, 2, 69, 2), (1, 0, 65, 2), (1, 2, 67, 1), (1, 3, 69, 1), (2, 0, 70, 3), (2, 3, 72, 1),
             (3, 0, 73, 4), (4, 0, 74, 2), (4, 2, 72, 2), (5, 0, 69, 2), (5, 2, 65, 2), (6, 0, 67, 2), (6, 2, 64, 2), (7, 0, 69, 4)]
MEL_CLIMAX = [(0, 0, 74, 2), (0, 2, 72, 1), (0, 3, 70, 1), (1, 0, 70, 2), (1, 2, 69, 2), (2, 0, 67, 3), (2, 3, 69, 1),
              (3, 0, 69, 4), (4, 0, 70, 2), (4, 2, 72, 2), (5, 0, 67, 3), (5, 3, 65, 1), (6, 0, 69, 2), (6, 2, 74, 2), (7, 0, 73, 4)]
CONTRA = [(0, 0, 81, 1), (0, 1, 79, 1), (0, 2, 77, 2), (1, 0, 79, 2), (1, 2, 74, 2), (2, 0, 77, 1), (2, 1, 76, 1), (2, 2, 74, 2),
          (3, 0, 76, 4), (4, 0, 79, 2), (4, 2, 82, 2), (5, 0, 79, 3), (5, 3, 75, 1), (6, 0, 76, 2), (6, 2, 81, 2), (7, 0, 81, 4)]
ACENTOS = (0, 3, 6, 8, 11, 14)

total = sum(len(c) for _, c, _ in SECCIONES); DUR = total * COMPAS + 7.0; N = int(DUR * SR)
L = np.zeros(N); R = np.zeros(N)

def poner(s, ini, pan=0.0, g=1.0, humano=0.008):
    ini += rng.normal(0, humano); i = max(0, int(ini * SR)); j = min(N, i + len(s))
    if j <= i: return
    s = s[:j - i] * g; L[i:j] += s * np.sqrt(0.5 * (1 - pan)); R[i:j] += s * np.sqrt(0.5 * (1 + pan))

def env(n, a, r, curva=1.6):
    e = np.ones(n); na = max(1, min(n - 1, int(SR * a))); nr = max(1, min(n - 1, int(SR * r)))
    e[:na] = np.sin(np.linspace(0, np.pi / 2, na)) ** 2; e[-nr:] *= np.linspace(1, 0, nr) ** curva
    return e

def aditiva(f0, dur, esp, vib=0.0, vib_ret=0.4, deriva=0.003, fmax=6500.0, brillo=1.0):
    n = int(dur * SR); tt = np.arange(n) / SR; rv = np.clip(tt / max(vib_ret, 1e-3), 0, 1)
    f = f0 * (1 + vib * rv * np.sin(2 * np.pi * rng.uniform(4.6, 5.8) * tt + rng.uniform(0, 6.28)))
    f *= 1 + deriva * np.sin(2 * np.pi * rng.uniform(0.07, 0.25) * tt + rng.uniform(0, 6.28))
    p = 2 * np.pi * np.cumsum(f) / SR; abre = np.clip(tt / 0.2, 0.35, 1.0) ** 0.5; out = np.zeros(n); k = 1
    while k * f0 < fmax:
        a = esp(k, k * f0)
        if a > 1e-4: out += a * np.sin(k * p + rng.uniform(0, 6.28)) * (abre ** ((k - 1) * 0.15 * brillo))
        k += 1
    return out

def seccion(f0, dur, esp, copias, des=0.005, **kw):
    n = int(dur * SR); out = np.zeros(n)
    for _ in range(copias):
        s = aditiva(f0 * (1 + rng.uniform(-des, des)), dur, esp, **kw); d = int(rng.uniform(0, 0.02) * SR); out[d:] += s[:n - d] * rng.uniform(0.8, 1.0)
    return out / copias

def gauss(fk, picos): return sum(g * np.exp(-0.5 * ((fk - fc) / bw) ** 2) for fc, bw, g in picos)
def coro_ah(k, fk): return gauss(fk, ((680, 120, 1.0), (1100, 150, 0.5), (2500, 250, 0.16), (3200, 280, 0.08))) / k ** 0.35
def coro_oh(k, fk): return gauss(fk, ((480, 100, 1.0), (850, 130, 0.45), (2400, 240, 0.12))) / k ** 0.35
def cuerdas(k, fk): return (1.0 / k) / (1 + (fk / 2800.0) ** 2.5)
def cuerda_aspera(k, fk): return (1.0 / k ** 0.85) / (1 + (fk / 4000.0) ** 2)
def cornos(k, fk): return (1.0 / k ** 0.9) / (1 + (fk / 1500.0) ** 3)
def metales(k, fk): return (1.0 / k ** 0.55) / (1 + (fk / 2900.0) ** 3)
def organo(k, fk): return {1: 1.0, 2: 0.55, 3: 0.25, 4: 0.3, 6: 0.1}.get(k, 0.0)

def aire(dur, fc=900.0):
    n = int(dur * SR); X = np.fft.rfft(rng.standard_normal(n)); fr = np.fft.rfftfreq(n, 1 / SR)
    X *= np.exp(-0.5 * ((fr - fc) / 500) ** 2) + 0.4 * np.exp(-0.5 * ((fr - 2400) / 600) ** 2); return np.fft.irfft(X, n) / 30.0

def piano(f0, dur, fuerza=1.0):
    n = int(dur * SR); tt = np.arange(n) / SR; out = np.zeros(n); k = 1
    while k * f0 < 7000 and k <= 30:
        out += (1.0 / k ** 1.15) * np.sin(2 * np.pi * k * f0 * np.sqrt(1 + 0.00035 * k * k) * tt + rng.uniform(0, 6.28)) * np.exp(-tt * (0.8 + 0.33 * k) * (1 + f0 / 900)); k += 1
    mart = np.convolve(rng.standard_normal(n) * np.exp(-tt * 120), np.ones(6) / 6, "same") * 0.22
    e = np.ones(n); r = int(min(0.15, dur * 0.3) * SR); e[-r:] = np.linspace(1, 0, r)
    return (out + mart) * e * fuerza

def latido(fuerza=1.0):
    n = int(0.6 * SR); out = np.zeros(n)
    for off, g in ((0.0, 1.0), (0.17, 0.72)):
        i = int(off * SR); t2 = np.arange(n - i) / SR
        out[i:] += g * np.sin(2 * np.pi * 47 * t2 * (1 + 0.5 * np.exp(-t2 * 30))) * np.exp(-t2 * 11)
    return out * fuerza

def tambor(dur=0.9, f0=60.0, fuerza=1.0):
    n = int(dur * SR); tt = np.arange(n) / SR
    c = np.sin(2 * np.pi * f0 * tt * (1 + 0.7 * np.exp(-tt * 26))) * np.exp(-tt * 6.5)
    pz = np.convolve(rng.standard_normal(n) * np.exp(-tt * 22), np.ones(10) / 10, "same") * 0.6
    return np.tanh(1.4 * (c + pz)) * fuerza

def caja(dur=0.22, fuerza=1.0):
    n = int(dur * SR); tt = np.arange(n) / SR; X = np.fft.rfft(rng.standard_normal(n)); fr = np.fft.rfftfreq(n, 1 / SR)
    X *= np.exp(-0.5 * ((fr - 2600) / 1800) ** 2); r = np.fft.irfft(X, n)
    return (r / np.abs(r).max() * np.exp(-tt * 24) + np.sin(2 * np.pi * 190 * tt) * np.exp(-tt * 30) * 0.5) * fuerza

def timbal(f0, dur=2.2, fuerza=1.0):
    n = int(dur * SR); tt = np.arange(n) / SR
    return (sum(a * np.sin(2 * np.pi * f0 * r * tt) * np.exp(-tt * d) for r, a, d in ((1, 1, 1.9), (1.5, 0.35, 3.2), (1.98, 0.22, 4.2)))
            + np.convolve(rng.standard_normal(n) * np.exp(-tt * 30), np.ones(25) / 25, "same") * 0.5) * fuerza

def platillo(dur):
    n = int(dur * SR); X = np.fft.rfft(rng.standard_normal(n)); fr = np.fft.rfftfreq(n, 1 / SR)
    X *= (fr > 3000) / (1 + (fr / 12000) ** 2); y = np.fft.irfft(X, n); return y / np.abs(y).max() * np.linspace(0, 1, n) ** 2.2

def choque_platillo(dur=3.0):
    n = int(dur * SR); tt = np.arange(n) / SR; X = np.fft.rfft(rng.standard_normal(n)); fr = np.fft.rfftfreq(n, 1 / SR)
    X *= (fr > 2500) / (1 + (fr / 13000) ** 2); y = np.fft.irfft(X, n); return y / np.abs(y).max() * np.exp(-tt * 1.6)

def braam(base, dur=2.4):
    """Golpe grave enorme de metales (cluster raíz-octava-quinta), con gruñido y saturación."""
    n = int(dur * SR); tt = np.arange(n) / SR; out = np.zeros(n)
    for nota in (base - 24, base - 12, base - 5, base):
        out += seccion(hz(nota), dur, metales, 4, des=0.007, deriva=0.004)
    out *= 1 + 0.25 * np.sin(2 * np.pi * 31 * tt)
    return np.tanh(2.2 * out / np.abs(out).max()) * env(n, 0.25, 0.8)

def subgrave(base, dur):
    n = int(dur * SR); tt = np.arange(n) / SR
    return np.sin(2 * np.pi * hz(base - 24) * tt) * env(n, 0.03, 0.4) * np.exp(-tt * 0.6)

def arco(dur, fuerza=1.0):
    """Ruido de arco (resina) al atacar y debajo de la nota: quita lo robótico de las cuerdas."""
    n = int(dur * SR); tt = np.arange(n) / SR; X = np.fft.rfft(rng.standard_normal(n)); fr = np.fft.rfftfreq(n, 1 / SR)
    X *= np.exp(-0.5 * ((fr - 2200) / 1400) ** 2); y = np.fft.irfft(X, n); y /= np.abs(y).max()
    return y * (0.6 * np.exp(-tt * 18) + 0.08) * fuerza

def gong(dur=6.0):
    n = int(dur * SR); tt = np.arange(n) / SR
    return sum(a * np.sin(2 * np.pi * f * tt * (1 + 0.004 * np.sin(2 * np.pi * 0.7 * tt))) * np.exp(-tt * d)
               for f, a, d in ((62, 1.0, 0.5), (97, 0.7, 0.6), (141, 0.55, 0.8), (233, 0.4, 1.0), (378, 0.3, 1.4), (611, 0.2, 2.0))) * env(n, 0.04, 0.3)

def melodia(mel, t_ini, tr, voz, esp, g, octava=0, pan=0.0, piano_dob=False):
    for cb, pulso, nota, dur in mel:
        d = dur * BEAT + 0.15; f = hz(nota + tr + octava)
        s = seccion(f, d, esp, voz, des=0.004, vib=0.009, vib_ret=0.45, fmax=5000) * env(int(d * SR), 0.08, 0.4)
        if piano_dob: s += piano(f, d, 0.7) * 0.5
        poner(s, t_ini + cb * COMPAS + pulso * BEAT, pan, g)

t0 = 0.6
for nombre, comp, tr in SECCIONES:
    for ci, ac in enumerate(comp):
        base, iv = AC[ac]; base += tr; ini = t0 + ci * COMPAS
        ultimo = ci == len(comp) - 1
        if nombre == "entrada":
            if ci == 0:   # retumbo, coro que crece, redoble de timbales
                poner(subgrave(base, COMPAS * 1.2) * np.linspace(0.2, 1, int(COMPAS * 1.2 * SR)), ini, 0.0, 0.14)
                for v, nota in enumerate((base, base + 7, base + 12, base + 15)):
                    d = COMPAS + 0.3; sc = seccion(hz(nota), d, coro_oh, 4, des=0.006, vib=0.008, fmax=4200)
                    poner(sc * np.linspace(0, 1, len(sc)) ** 1.5, ini, (-0.6, 0.6, -0.3, 0.3)[v], 0.06)
                for r in range(16): poner(timbal(hz(base - 12), 0.5, 0.25 + r * 0.05), ini + r * SEMI, 0.0, 0.12)
            elif ci == 1:   # braams con tambores
                poner(braam(base), ini, 0.0, 0.16); poner(braam(base + 1, 1.6), ini + 2 * BEAT, 0.0, 0.12)
                for s8 in (0, 3, 4, 6, 7): poner(tambor(1.0, 55, 1.0), ini + s8 * BEAT / 2, rng.uniform(-0.3, 0.3), 0.17)
                poner(choque_platillo(), ini, 0.0, 0.08)
            elif ci == 2:   # metales que suben por quintas, gritos del coro y redoble
                for k, nota in enumerate((base, base + 7, base + 12, base + 19)):
                    d = BEAT * 1.0; mt = seccion(hz(nota), d, metales, 4, des=0.006) * env(int(d * SR), 0.03, 0.4)
                    poner(np.tanh(1.8 * mt), ini + k * BEAT, 0.0, 0.08)
                    poner(seccion(hz(nota + 12), d * 0.6, coro_ah, 4, des=0.01, fmax=5000) * env(int(d * 0.6 * SR), 0.01, 0.5), ini + k * BEAT, rng.uniform(-0.5, 0.5), 0.06)
                for r in range(16): poner(caja(0.14, 0.3 + r * 0.045), ini + r * SEMI, 0.0, 0.07)
                poner(platillo(COMPAS), ini, 0.0, 0.1)
            else:           # golpe gigante de toda la orquesta, silencio y latido
                poner(braam(base, 3.0), ini, 0.0, 0.18); poner(gong(), ini, 0.0, 0.16); poner(choque_platillo(4.0), ini, 0.0, 0.12)
                poner(timbal(hz(base - 12), 3.0, 1.4), ini, 0.0, 0.3); poner(tambor(1.5, 48, 1.4), ini, 0.0, 0.25)
                for nota in (base, base + iv[1], base + iv[2], base + 12, base + 24):
                    d = BEAT * 1.6
                    poner(seccion(hz(nota), d, coro_ah, 4, des=0.008, fmax=5000) * env(int(d * SR), 0.01, 0.7), ini, rng.uniform(-0.6, 0.6), 0.05)
                    poner(np.tanh(1.6 * seccion(hz(nota), d, cuerda_aspera, 5) * env(int(d * SR), 0.005, 0.7)), ini, rng.uniform(-0.6, 0.6), 0.05)
                poner(latido(1.0), ini + 3 * BEAT, 0.0, 0.2)
            continue
        if nombre == "soledad":
            # piano solo en arpegio lento + latido suave + una sola voz
            for b, nota in enumerate([base, base + iv[1] + 12, base + iv[2] + 12, base + 24]):
                poner(piano(hz(nota), BEAT * 1.8, 0.8), ini + b * BEAT, rng.uniform(-0.3, 0.3), 0.07)
            poner(latido(0.8), ini, 0.0, 0.14); poner(latido(0.6), ini + 2 * BEAT, 0.0, 0.12)
            d = COMPAS + 0.6
            poner(seccion(hz(base - 12), d, organo, 1) * env(int(d * SR), 0.8, 0.9), ini, 0.0, 0.05)
            if ultimo: poner(platillo(COMPAS * 0.9), ini + COMPAS * 0.1, 0.0, 0.07); [poner(caja(0.15, 0.3 + r * 0.06), ini + 2 * BEAT + r * SEMI, 0.0, 0.06) for r in range(8)]
            continue
        intens = {"furia": 0.9, "tristeza": 0.85, "vida o muerte": 0.6, "clímax": 1.0, "final": 0.9}[nombre]
        # colchón: coro y cuerdas largas (más presente en tristeza y clímax)
        d = COMPAS + 0.8; g_coro = {"furia": 0.03, "tristeza": 0.05, "vida o muerte": 0.035, "clímax": 0.05, "final": 0.05}[nombre]
        for v in range(6):
            nota = base + iv[v % 3] + (12 if v >= 3 else 0)
            esp = coro_ah if (ci + v) % 2 == 0 else coro_oh
            s = seccion(hz(nota), d, esp, 3, des=0.006, vib=0.007, vib_ret=0.6, fmax=4200) * env(int(d * SR), 0.6, 1.0) + aire(d) * env(int(d * SR), 0.6, 1.0)
            poner(s, ini + rng.uniform(0, 0.1), rng.uniform(-0.75, 0.75), g_coro * intens)
        if nombre in ("clímax", "final"):
            for nota in (base + 24 + iv[2], base + 36):
                s = seccion(hz(nota), d, coro_ah, 3, des=0.005, vib=0.01, vib_ret=0.5, fmax=5000) * env(int(d * SR), 0.5, 0.9)
                poner(s, ini, rng.uniform(-0.6, 0.6), 0.03)
        for nota in (base + iv[1], base + iv[2] + 12):
            poner((seccion(hz(nota), d, cuerdas, 6, des=0.006, vib=0.006, vib_ret=0.8) + arco(d, 0.5)) * env(int(d * SR), 0.5, 0.8), ini, rng.uniform(-0.5, 0.5), 0.04 * intens)
        poner(seccion(hz(base - 12), d, organo, 1) * env(int(d * SR), 0.5, 0.8), ini, 0.0, 0.06 * intens)
        # FURIA: cuerdas en semicorcheas con acentos 3+3+2, golpes de metales, tambores de guerra
        if nombre in ("furia", "clímax"):
            for s16 in range(16):
                nota = base - 12 + (12 if s16 in (7, 15) else 0)
                dd = SEMI * 0.95; acento = s16 in ACENTOS
                sn = (seccion(hz(nota), dd, cuerda_aspera, 5, des=0.005) + arco(dd, 0.9)) * env(int(dd * SR), 0.004, 0.45)
                poner(np.tanh(1.4 * sn), ini + s16 * SEMI, -0.3 if s16 % 2 else 0.3, (0.09 if acento else 0.05) * intens, 0.004)
            for s16 in (0, 6, 11):
                for nota in [base + x for x in iv] + [base - 12]:
                    dd = SEMI * 2.0
                    mt = seccion(hz(nota), dd, metales, 4, des=0.006); mt *= 1 + 0.2 * np.sin(2 * np.pi * 29 * np.arange(len(mt)) / SR)
                    poner(np.tanh(2.0 * mt * env(int(dd * SR), 0.01, 0.5)), ini + s16 * SEMI, rng.uniform(-0.4, 0.4), 0.045 * intens)
            for s8 in range(8):
                fuerte = s8 in (0, 3, 6); poner(tambor(f0=58 if fuerte else 70, fuerza=1.0 if fuerte else 0.55), ini + s8 * BEAT / 2, rng.uniform(-0.25, 0.25), 0.13 * intens)
            poner(caja(fuerza=0.7), ini + 4 * SEMI, 0.1, 0.05); poner(caja(fuerza=0.7), ini + 12 * SEMI, 0.1, 0.05)
        # TRISTEZA intensa: piano en arpegio, percusión a medio tiempo
        if nombre in ("tristeza", "clímax"):
            arp = [base + 12, base + 12 + iv[1], base + 12 + iv[2], base + 24, base + 24 + iv[1], base + 24, base + 12 + iv[2], base + 12 + iv[1]]
            for b, nota in enumerate(arp): poner(piano(hz(nota), BEAT * 0.9, 0.9 if b % 4 == 0 else 0.6), ini + b * BEAT / 2, 0.25, 0.05 * intens)
        if nombre == "tristeza":
            poner(tambor(1.2, 54, 1.0), ini, 0.0, 0.14); poner(tambor(1.0, 60, 0.7), ini + 2 * BEAT, 0.0, 0.11)
            poner(piano(hz(base - 12), COMPAS * 0.9, 1.0), ini, -0.1, 0.06)
        # VIDA O MUERTE: el latido manda y se acelera; piano y cuerdas agudas; redoble que crece
        if nombre == "vida o muerte":
            pulsos = 2 + ci * 2
            for b in range(pulsos): poner(latido(1.0), ini + b * COMPAS / pulsos, 0.0, 0.2)
            poner(piano(hz(base), COMPAS * 0.9, 0.9), ini, -0.1, 0.06); poner(piano(hz(base + iv[1] + 12), COMPAS * 0.9, 0.8), ini, 0.1, 0.05)
            poner(seccion(hz(base + 24 + iv[2]), COMPAS, cuerdas, 4, vib=0.01) * env(int(COMPAS * SR), 0.6, 0.6), ini, 0.0, 0.03 + ci * 0.008)
            if ultimo:
                for r in range(16): poner(caja(0.14, 0.25 + r * 0.05), ini + r * SEMI, 0.0, 0.06 + r * 0.004)
                poner(platillo(COMPAS), ini, 0.0, 0.09)
        # timbales en el 1 (y el 3 en el clímax); choque de platillo y campana al entrar a cada sección
        if nombre != "vida o muerte":
            poner(timbal(hz(base - 12)), ini, 0.0, 0.2 * intens)
            if nombre == "clímax": poner(timbal(hz(base - 5), fuerza=0.7), ini + 2 * BEAT, 0.0, 0.16)
        if ci == 0 and nombre in ("furia", "tristeza", "clímax"): poner(choque_platillo(), ini, 0.0, 0.08)
        if ci in (0, 4) and nombre in ("furia", "clímax"): poner(braam(base), ini, 0.0, 0.11)
        if nombre in ("furia", "clímax", "tristeza"): poner(subgrave(base, COMPAS), ini, 0.0, 0.10 * intens)
        if nombre == "clímax":
            for s16 in (2, 5, 10, 13): poner(tambor(0.6, 85, 0.6), ini + s16 * SEMI, rng.uniform(-0.5, 0.5), 0.09)
        if ultimo and nombre in ("furia", "tristeza"): poner(platillo(COMPAS * 0.9), ini + COMPAS * 0.1, 0.0, 0.07)
    # melodías
    if nombre == "soledad": melodia(MEL_TRISTE[:7] if not BUCLE else MEL_TRISTE[:3], t0, tr, 1, coro_oh, 0.09, pan=0.05)
    if nombre == "furia": melodia(MEL_FURIA, t0, tr, 3, metales, 0.06, octava=-12, pan=0.05)
    if nombre == "tristeza":
        melodia(MEL_TRISTE, t0, tr, 4, coro_ah, 0.075, octava=0, pan=-0.05)
        melodia(MEL_TRISTE, t0, tr, 3, cuerdas, 0.05, octava=-12, pan=0.1)
    if nombre == "clímax":
        melodia(MEL_CLIMAX, t0, tr, 3, metales, 0.065, pan=0.05)
        melodia(MEL_CLIMAX, t0, tr, 4, coro_ah, 0.06, pan=-0.05)
        melodia(CONTRA, t0, tr, 4, cuerdas, 0.045, pan=0.2, piano_dob=True)
    t0 += len(comp) * COMPAS
FIN = t0
if not BUCLE:
    poner(timbal(hz(38 + 1), 4.5, 1.1), t0, 0.0, 0.35); poner(latido(1.0), t0 + 1.0, 0.0, 0.18); poner(latido(0.6), t0 + 2.4, 0.0, 0.12)

# ---------------- sala y masterizado (intenso pero con aire) ----------------
def sala(x, cola, semilla):
    r = np.random.default_rng(semilla); it = np.arange(int(SR * cola)) / SR
    ir = r.standard_normal(len(it)) * np.exp(-it * 6.9 / cola); ir[:int(0.02 * SR)] = 0
    for dly, g in ((0.017, 0.5), (0.031, 0.4), (0.052, 0.3), (0.077, 0.22)): ir[int(dly * SR)] += g * 9
    n = len(x) + len(ir); IR = np.fft.rfft(ir, n); fr = np.fft.rfftfreq(n, 1 / SR); IR *= 1 / (1 + (fr / 4800.0) ** 2)
    y = np.fft.irfft(np.fft.rfft(x, n) * IR, n)[:len(x)]; return y / np.abs(y).max()
def eq(x):
    X = np.fft.rfft(x); fr = np.fft.rfftfreq(len(x), 1 / SR); X *= 1 - 0.3 * np.exp(-0.5 * ((fr - 3800) / 1200) ** 2); return np.fft.irfft(X, len(x))
wl, wr = sala(L, 3.4, 11), sala(R, 3.4, 12)
st = np.stack([eq(L / np.abs(L).max() * 0.6 + wl * 0.5), eq(R / np.abs(R).max() * 0.6 + wr * 0.5)], 1); st /= np.abs(st).max()
envl = np.convolve(np.abs(st).max(1), np.ones(2205) / 2205, "same") + 1e-3
comp = st * ((0.5 / envl) ** 0.55)[:, None]; comp /= np.abs(comp).max()
st = 0.55 * st + 0.65 * comp                                 # compresión en paralelo: pega fuerte sin perder aire
st = np.tanh(1.8 * st / np.abs(st).max()) / np.tanh(1.8) * 0.95
if BUCLE:
    i0, i1 = int(0.6 * SR), int(FIN * SR); cuerpo = st[i0:i1].copy(); cola = st[i1:]
    m = min(len(cola), len(cuerpo)); cuerpo[:m] += cola[:m]; st = cuerpo / max(1.0, np.abs(cuerpo).max() / 0.95)
elif SOLO_ENTRADA:
    st = st[int(0.6 * SR):int((FIN + 1.2) * SR)]; f = int(SR * 0.8); st[-f:] *= np.linspace(1, 0, f)[:, None]
else:
    f = int(SR * 3.0); st[-f:] *= np.linspace(1, 0, f)[:, None]
with wave.open(SALIDA, "wb") as w:
    w.setnchannels(2); w.setsampwidth(2); w.setframerate(SR); w.writeframes((st * 32767).astype(np.int16).tobytes())
print("hecho", MODO, round(DUR, 1), "s")

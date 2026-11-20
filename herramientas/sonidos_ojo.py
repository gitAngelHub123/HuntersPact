# Sintetiza 10 sonidos de muerte "turbios" para el Ojo (originales, desde cero).
import sys, wave, numpy as np

SR = 44100
rng = np.random.default_rng(7)

def t(d): return np.arange(int(SR * d)) / SR

def glide(f0, f1, d, curva=1.0):
    x = np.linspace(0, 1, int(SR * d)) ** curva
    return f0 * (f1 / f0) ** x            # exponencial

def osc(freq, forma="sin"):
    fase = 2 * np.pi * np.cumsum(freq) / SR
    if forma == "sin": return np.sin(fase)
    if forma == "saw": return 2 * ((fase / (2 * np.pi)) % 1.0) - 1
    if forma == "sq": return np.sign(np.sin(fase))
    if forma == "tri": return 2 * np.abs(2 * ((fase / (2 * np.pi)) % 1.0) - 1) - 1

def env(n, a=0.02, r=0.6, pico=0.1):
    e = np.ones(n); na = max(1, int(SR * a)); nr = max(1, int(n * r))
    e[:na] = np.linspace(0, 1, na); e[-nr:] *= np.linspace(1, 0, nr) ** 1.6
    return e

def lp(x, fc):                      # paso bajo de un polo (fc fija o array)
    fc = np.broadcast_to(np.asarray(fc, float), x.shape)
    a = 1 - np.exp(-2 * np.pi * fc / SR); y = np.empty_like(x); s = 0.0
    for i in range(len(x)): s += a[i] * (x[i] - s); y[i] = s
    return y

def hp(x, fc): return x - lp(x, fc)

def banda(x, fc, q=4.0):            # biquad paso banda
    w = 2 * np.pi * fc / SR; al = np.sin(w) / (2 * q)
    b0, b2, a0, a1, a2 = al, -al, 1 + al, -2 * np.cos(w), 1 - al
    b0, b2, a1, a2 = b0 / a0, b2 / a0, a1 / a0, a2 / a0
    y = np.zeros_like(x); x1 = x2 = y1 = y2 = 0.0
    for i in range(len(x)):
        v = b0 * x[i] + b2 * x2 - a1 * y1 - a2 * y2
        x2, x1, y2, y1 = x1, x[i], y1, v; y[i] = v
    return y

def formantes(x, fs, qs=(6, 8, 10)): return sum(banda(x, f, q) for f, q in zip(fs, qs))

def eco(x, retardos=(0.11, 0.23, 0.37, 0.53), ganancia=0.45, cola=2.0):
    n = len(x) + int(SR * cola); y = np.zeros(n); y[:len(x)] += x
    for k, d in enumerate(retardos):
        s = int(SR * d); g = ganancia ** (k + 1)
        for rep in range(1, 6):
            o = s * rep
            if o >= n: break
            m = min(len(x), n - o); y[o:o + m] += x[:m] * g ** rep
    return lp(y, 2500)

def reverb(x, cola=2.5, mezcla=0.45):
    n = len(x) + int(SR * cola); seco = np.zeros(n); seco[:len(x)] = x
    ir_t = np.arange(int(SR * cola)) / SR
    ir = rng.standard_normal(len(ir_t)) * np.exp(-ir_t * 3.2 / cola)
    ir = lp(ir, 1800); ir /= np.abs(ir).max()
    h = np.fft.irfft(np.fft.rfft(seco, 2 * n) * np.fft.rfft(ir, 2 * n))[:n]
    h /= max(1e-9, np.abs(h).max())
    return (1 - mezcla) * seco / max(1e-9, np.abs(seco).max()) + mezcla * h

def satura(x, k=3.0): return np.tanh(k * x) / np.tanh(k)

def ruido(d): return rng.standard_normal(int(SR * d))

def mezclar(*capas):
    n = max(len(c) for c in capas); y = np.zeros(n)
    for c in capas: y[:len(c)] += c
    return y

# ---------------- las diez variantes ----------------
def s01():   # gemido grave que se hunde
    d = 4.0; f = glide(140, 32, d, 0.8)
    v = osc(f, "saw") + 0.5 * osc(f * 1.007, "saw") + 0.3 * osc(f * 0.5, "sin")
    v = formantes(v, (420, 900, 2300)) * env(len(v), 0.05, 0.7)
    return reverb(satura(v * 0.8, 2.0), 3.0, 0.5)

def s02():   # rugido demoníaco con gruñido
    d = 3.2; f = glide(95, 28, d, 0.6); n = len(f)
    tono = osc(f, "saw") + osc(f * 1.5, "saw") * 0.4 + osc(f * 0.501, "saw") * 0.6
    gr = 0.6 + 0.4 * np.sin(2 * np.pi * np.cumsum(np.full(n, 31.0)) / SR) ** 2
    v = (tono * gr + 0.6 * lp(ruido(d), 900)) * env(n, 0.03, 0.65)
    return reverb(satura(lp(v, 1600), 4.0), 2.5, 0.4)

def s03():   # chillido al revés que se corta en un golpe
    d = 2.2; f = glide(2400, 380, d, 1.5); n = len(f)
    ch = formantes(osc(f, "saw") + 0.3 * ruido(d), (900, 1700, 3100), (5, 7, 9))
    ch = ch[::-1] * np.linspace(0, 1, n) ** 2.5
    golpe_t = t(2.5); golpe = np.sin(2 * np.pi * np.cumsum(glide(70, 25, 2.5)) / SR) * np.exp(-golpe_t * 2.2)
    return reverb(mezclar(ch * 0.8, np.concatenate([np.zeros(n), golpe * 1.2])), 3.0, 0.45)

def s04():   # gorgoteo húmedo
    d = 3.8; n = int(SR * d); f = glide(120, 45, d)
    base = osc(f, "tri")
    pulsos = np.zeros(n)
    for c in rng.uniform(0, d * 0.85, 55):
        i = int(c * SR); L = int(SR * rng.uniform(0.02, 0.07))
        if i + L < n: pulsos[i:i + L] += np.hanning(L) * np.sin(2 * np.pi * rng.uniform(180, 520) * np.arange(L) / SR)
    v = formantes(base * 0.6 + ruido(d) * 0.25, (300, 750, 1900)) + pulsos * 0.9
    return reverb(lp(v * env(n, 0.02, 0.6), 2200), 2.2, 0.4)

def s05():   # coro oscuro que colapsa
    d = 5.0; n = int(SR * d); y = np.zeros(n)
    for k, base in enumerate([110, 131, 147, 165, 196]):
        vib = 1 + 0.012 * np.sin(2 * np.pi * (4.5 + k * 0.3) * t(d))
        f = glide(base, base * 0.33, d, 2.2) * vib
        y += formantes(osc(f, "saw"), (500 + k * 40, 1100, 2600), (7, 9, 11))
    return reverb(y * env(n, 0.3, 0.55), 3.5, 0.55)

def s06():   # latido que se apaga bajo un zumbido
    d = 5.0; n = int(SR * d); y = np.zeros(n); tt = 0.0; gap = 0.55
    while tt < d - 0.4:
        for off, g in ((0, 1.0), (0.17, 0.7)):
            i = int((tt + off) * SR); L = int(SR * 0.25)
            if i + L < n: y[i:i + L] += g * np.sin(2 * np.pi * 48 * np.arange(L) / SR) * np.exp(-np.arange(L) / SR * 18)
        tt += gap; gap *= 1.25
    zumbido = lp(osc(glide(62, 30, d), "saw"), 300) * 0.35 * np.linspace(1, 0, n)
    return reverb(satura(y + zumbido, 1.8), 2.0, 0.35)

def s07():   # grito ahogado en una caverna (eco largo)
    d = 1.8; f = glide(520, 160, d, 0.7) * (1 + 0.03 * np.sin(2 * np.pi * 7 * t(d)))
    v = formantes(osc(f, "saw") + 0.2 * ruido(d), (750, 1250, 2700), (8, 10, 12)) * env(len(f), 0.04, 0.6)
    return eco(satura(v, 2.5), (0.19, 0.31, 0.47, 0.71), 0.5, 3.5)

def s08():   # desgarro metálico
    d = 3.5; n = int(SR * d); y = np.zeros(n)
    for r, a in ((1.0, 1.0), (2.76, 0.6), (5.40, 0.45), (8.93, 0.3), (13.3, 0.2)):
        y += a * osc(glide(210 * r, 60 * r, d, 0.9), "sin") * np.exp(-t(d) * (0.8 + r * 0.25))
    rasgado = hp(ruido(d), 1500) * np.exp(-t(d) * 4) * 0.6
    return reverb(satura(y * 0.7 + rasgado, 2.2), 2.8, 0.45)

def s09():   # último aliento con estertor
    d = 4.2; n = int(SR * d)
    aliento = banda(ruido(d), 700, 1.2) * np.sin(np.linspace(0, np.pi, n)) ** 0.7
    estertor = (np.sin(2 * np.pi * np.cumsum(glide(28, 14, d)) / SR) > 0.6) * lp(ruido(d), 500) * 0.8
    gemido = formantes(osc(glide(180, 70, d), "saw"), (450, 900, 2400)) * 0.35
    return reverb((aliento + estertor + gemido) * env(n, 0.2, 0.5), 2.5, 0.4)

def s10():   # implosión: succión al revés y golpe profundo
    d = 2.0; n = int(SR * d)
    succion = reverb(lp(ruido(0.8), 1400) * np.exp(-t(0.8) * 6), 1.6, 0.9)[::-1][:n]
    golpe = np.sin(2 * np.pi * np.cumsum(glide(60, 22, 3.0)) / SR) * np.exp(-t(3.0) * 1.4)
    retumbo = lp(ruido(3.0), 120) * np.exp(-t(3.0) * 1.2) * 2.5
    return reverb(mezclar(succion * 0.9, np.concatenate([np.zeros(len(succion)), satura(golpe + retumbo, 2.0)])), 3.0, 0.4)

def guardar(x, ruta):
    x = x - np.mean(x); x = x / max(1e-9, np.abs(x).max()) * 0.9
    fade = int(SR * 0.05); x[-fade:] *= np.linspace(1, 0, fade)
    with wave.open(ruta, "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes((x * 32767).astype(np.int16).tobytes())

if __name__ == "__main__":
    salida = sys.argv[1]
    for k, fn in enumerate([s01, s02, s03, s04, s05, s06, s07, s08, s09, s10], 1):
        guardar(fn(), f"{salida}/ojo_muerte_{k:02d}.wav"); print("hecho", k, flush=True)

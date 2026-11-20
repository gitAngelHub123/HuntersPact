# Sonidos del jefe de la cueva (La Madre de la Guarida): picada y agarre, 3 opciones cada uno. Síntesis original desde cero.
import sys, wave, numpy as np

SR = 44100
rng = np.random.default_rng(31)

def t(d): return np.arange(int(SR * d)) / SR
def glide(f0, f1, d, c=1.0):
    x = np.linspace(0, 1, int(SR * d)) ** c
    return f0 * (f1 / f0) ** x
def fase(f): return 2 * np.pi * np.cumsum(f) / SR
def saw(f): return 2 * ((np.cumsum(f) / SR) % 1.0) - 1
def lp(x, fc):
    fc = np.broadcast_to(np.asarray(fc, float), x.shape); a = 1 - np.exp(-2 * np.pi * fc / SR)
    y = np.empty_like(x); s = 0.0
    for i in range(len(x)): s += a[i] * (x[i] - s); y[i] = s
    return y
def hp(x, fc): return x - lp(x, fc)
def banda(x, fc, q):
    fc = np.broadcast_to(np.asarray(fc, float), x.shape)
    y = np.zeros_like(x); x1 = x2 = y1 = y2 = 0.0
    for i in range(len(x)):
        w = 2 * np.pi * fc[i] / SR; al = np.sin(w) / (2 * q); a0 = 1 + al
        v = (al * x[i] - al * x2 + 2 * np.cos(w) * y1 - (1 - al) * y2) / a0
        x2, x1, y2, y1 = x1, x[i], y1, v; y[i] = v
    return y
def ruido(d): return rng.standard_normal(int(SR * d))
def env(n, a, r):
    e = np.ones(n); na = max(1, int(SR * a)); nr = max(1, int(n * r))
    e[:na] = np.linspace(0, 1, na) ** 1.5; e[-nr:] *= np.linspace(1, 0, nr) ** 1.8
    return e
def sala(x, cola=0.9, mezcla=0.3):
    n = len(x) + int(SR * cola); it = np.arange(int(SR * cola)) / SR
    ir = rng.standard_normal(len(it)) * np.exp(-it * 6.9 / cola); ir = np.convolve(ir, np.ones(8) / 8, "same"); ir /= np.abs(ir).max()
    h = np.fft.irfft(np.fft.rfft(np.pad(x, (0, n - len(x))), 2 * n) * np.fft.rfft(ir, 2 * n))[:n]
    seco = np.pad(x, (0, n - len(x)))
    return (1 - mezcla) * seco / np.abs(seco).max() + mezcla * h / np.abs(h).max()
def chillido(f0, f1, d, formantes=(1200, 2400, 3600), aspereza=0.3):
    """Chillido de criatura: diente de sierra con vibrato rápido, formantes y aire."""
    f = glide(f0, f1, d, 0.8) * (1 + 0.025 * np.sin(fase(np.full(int(SR * d), 23.0))))
    s = saw(f) + aspereza * ruido(d)
    return sum(banda(s, fc * np.ones(len(s)), q) * g for fc, q, g in zip(formantes, (6, 8, 10), (1, 0.6, 0.35)))

# ---------------- PICADA: se lanza desde el aire ----------------
def picada1():   # chillido que sube con ráfaga de viento que se acerca
    d = 1.8; n = int(SR * d)
    ch = chillido(700, 1900, d) * env(n, 0.25, 0.35)
    viento = banda(ruido(d), glide(300, 1400, d), 1.2) * np.linspace(0.2, 1, n) ** 2 * env(n, 0.05, 0.15)
    return sala(ch * 0.8 + viento * 0.9, 0.8, 0.25)
def picada2():   # rugido grave de criatura grande que se convierte en chillido
    d = 2.0; n = int(SR * d)
    gr = banda(saw(glide(90, 160, d)) + 0.5 * ruido(d), np.full(n, 450.0), 3) * (0.6 + 0.4 * np.abs(np.sin(fase(np.full(n, 26.0)))))
    ch = chillido(500, 1500, d, (900, 1900, 3100), 0.4) * np.linspace(0, 1, n) ** 1.5
    viento = banda(ruido(d), glide(200, 900, d), 1.0) * np.linspace(0.1, 1, n) ** 2
    return sala(np.tanh(2.0 * (gr * 0.7 + ch * 0.8 + viento * 0.6)) * env(n, 0.1, 0.3), 0.9, 0.3)
def picada3():   # alarido agudo y desgarrado, aire que se rasga
    d = 1.5; n = int(SR * d)
    ch = chillido(1400, 2600, d, (1600, 3000, 4400), 0.5) * env(n, 0.08, 0.4)
    ch2 = chillido(1450, 2700, d, (1700, 3100, 4600), 0.5) * env(n, 0.1, 0.4)
    rasgado = hp(ruido(d), 2500) * np.linspace(0.3, 1, n) * env(n, 0.02, 0.2) * 0.5
    return sala(np.tanh(1.6 * (ch + ch2 * 0.7 + rasgado)), 0.7, 0.25)

# ---------------- AGARRE: te atrapa con las garras (sin golpe sordo) ----------------
def aleteo(d, golpes):
    n = int(SR * d); y = np.zeros(n)
    for c in golpes:
        i = int(c * SR); L = int(SR * 0.16)
        if i + L < n: y[i:i + L] += banda(rng.standard_normal(L), np.full(L, 600.0), 0.8) * np.hanning(L) ** 0.7
    return y
def garras(d, n_rasp=4):
    n = int(SR * d); y = np.zeros(n)
    for k in range(n_rasp):
        i = int(rng.uniform(0.0, 0.25) * SR); L = int(SR * rng.uniform(0.12, 0.22))
        if i + L < n:
            r = hp(rng.standard_normal(L), 3000) * (np.sin(np.linspace(0, np.pi, L)) ** 2)
            r *= 1 + 0.8 * np.sin(2 * np.pi * rng.uniform(90, 160) * np.arange(L) / SR)   # chirrido de uña
            y[i:i + L] += r * 0.7
    return y
def agarre1():   # zarpazo: garras que raspan + aleteo fuerte + chillido corto de triunfo
    d = 1.4; n = int(SR * d)
    ch = chillido(1100, 800, 0.6, (1300, 2500, 3700), 0.35) * env(int(SR * 0.6), 0.03, 0.5)
    return sala(garras(d) + aleteo(d, (0.0, 0.32, 0.64)) * 0.9 + np.pad(ch, (int(SR * 0.08), n - int(SR * 0.6) - int(SR * 0.08))) * 0.7, 0.7, 0.25)
def agarre2():   # gruñido feroz + carne apretada (sin golpe)
    d = 1.5; n = int(SR * d)
    gr = banda(saw(glide(120, 90, d)) + 0.6 * ruido(d), np.full(n, 380.0), 2.5) * (0.5 + 0.5 * np.abs(np.sin(fase(np.full(n, 31.0))))) * env(n, 0.04, 0.5)
    humedo = lp(ruido(d), 900) * (rng.random(n) < 0.0015).astype(float).cumsum() % 2 * 0.4 * env(n, 0.01, 0.6)
    return sala(np.tanh(2.2 * (gr + humedo + garras(d, 2) * 0.6)), 0.6, 0.2)
def agarre3():   # chillido que se corta en seco al atrapar + alas que baten
    d = 1.6; n = int(SR * d)
    ch = chillido(1700, 1200, 0.45, (1500, 2800, 4000), 0.3) * env(int(SR * 0.45), 0.02, 0.25)
    return sala(np.pad(ch, (0, n - len(ch))) + aleteo(d, (0.3, 0.6, 0.9, 1.2)) * 0.8 + garras(d, 3) * 0.5, 0.8, 0.3)

def guardar(x, ruta):
    x = x - np.mean(x); x = x / np.abs(x).max() * 0.92
    x[:int(SR * 0.004)] *= np.linspace(0, 1, int(SR * 0.004)); x[-int(SR * 0.04):] *= np.linspace(1, 0, int(SR * 0.04))
    with wave.open(ruta, "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR); w.writeframes((x * 32767).astype(np.int16).tobytes())

if __name__ == "__main__":
    out = sys.argv[1]
    for nombre, fn in [("picada_1", picada1), ("picada_2", picada2), ("picada_3", picada3), ("agarre_1", agarre1), ("agarre_2", agarre2), ("agarre_3", agarre3)]:
        guardar(fn(), f"{out}/jefe_{nombre}.wav"); print("hecho", nombre, flush=True)

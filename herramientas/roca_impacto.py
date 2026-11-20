# roca_03 mejorada: parte fuerte de ~2,8 s, sin cola débil, reverb de cueva por convolución. Solo grabaciones reales (CC0 / dominio público).
import sys, wave, numpy as np
SR = 44100; W = sys.argv[1]; rng = np.random.default_rng(3)
def leer(n):
    with wave.open(f"{W}/{n}.wav") as w: return np.frombuffer(w.readframes(w.getnframes()), np.int16).astype(float) / 32768
golpe, rebote, grave, cruje, grava = (leer(n) for n in ("golpe", "rebote", "grave", "cruje", "grava"))
DUR = 3.4; N = int(DUR * SR); y = np.zeros(N)
def poner(s, t, g):
    i = int(t * SR); j = min(N, i + len(s)); y[i:j] += s[:j - i] * g
def tramo(ini, largo):
    s = grava[int(ini * SR):int((ini + largo) * SR)].copy()
    f = int(0.03 * SR); s[:f] *= np.linspace(0, 1, f); s[-f:] *= np.linspace(1, 0, f); return s
# impacto: golpe + grave + crujido; rebote a los 0,35 s y otro chico a 0,8 s (como una roca real)
poner(golpe, 0.0, 1.0); poner(grave, 0.0, 1.1); poner(cruje, 0.01, 0.75)
poner(rebote, 0.35, 0.55); poner(cruje, 0.36, 0.35); poner(rebote, 0.80, 0.3)
# piedras rodando FUERTES ~2,7 s: los tramos más fuertes de la grabación, encadenados
deb = np.zeros(N)
for ini, t, g in ((16.9, 0.10, 1.0), (42.1, 0.75, 0.95), (48.1, 1.35, 0.9), (54.9, 1.95, 0.8)):
    s = tramo(ini, 0.85); i = int(t * SR); j = min(N, i + len(s)); deb[i:j] += s[:j - i] * g
env = np.convolve(np.abs(deb), np.ones(2205) / 2205, "same") + 1e-4
deb *= np.clip((0.06 / env) ** 0.5, 0.5, 3.0)       # sin huecos débiles en el derrumbe
y += deb * 2.2   # derrumbe fuerte, no solo el primer golpe
corte = int(2.8 * SR); fin = int(3.3 * SR)
y[corte:fin] *= np.linspace(1, 0, fin - corte) ** 1.5; y[fin:] = 0
# reverb de cueva por convolución (no ecos repetidos)
it = np.arange(int(0.9 * SR)) / SR
ir = rng.standard_normal(len(it)) * np.exp(-it * 6.9 / 0.9); ir[:int(0.012 * SR)] = 0
n = len(y) + len(ir); IR = np.fft.rfft(ir, n); fr = np.fft.rfftfreq(n, 1 / SR); IR *= 1 / (1 + (fr / 3500.0) ** 2)
rev = np.fft.irfft(np.fft.rfft(y, n) * IR, n)[:len(y)]; rev /= np.abs(rev).max()
y = y / np.abs(y).max() * 0.8 + rev * 0.28
y[int(3.1 * SR):] *= np.linspace(1, 0, len(y) - int(3.1 * SR))
y = np.tanh(1.3 * y / np.abs(y).max()) / np.tanh(1.3) * 0.92
with wave.open(sys.argv[2], "wb") as w:
    w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR); w.writeframes((y * 32767).astype(np.int16).tobytes())
print("hecho", DUR, "s")

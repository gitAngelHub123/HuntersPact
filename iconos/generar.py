# Iconos de los libros del cooperativo, con el estilo de Witch Hunt:
#  - item_coop_*.png : silueta blanca sobre transparente (como los artículos de la tienda)
#  - skill_coop_*.png: silueta blanca sobre negro con resplandor rojo en rayos (como las habilidades)
import math, random, os
from PIL import Image, ImageDraw, ImageFilter, ImageChops

S = 1024           # se dibuja grande y se reduce a 256 (bordes suaves)
OUT = os.path.dirname(os.path.abspath(__file__))

def lienzo(): return Image.new("L", (S, S), 0)

def rot(pts, ang, c=(S/2, S/2)):
    a = math.radians(ang); ca, sa = math.cos(a), math.sin(a)
    return [(c[0] + (x-c[0])*ca - (y-c[1])*sa, c[1] + (x-c[0])*sa + (y-c[1])*ca) for x, y in pts]

def poly(d, pts, ang=0, fill=255): d.polygon(rot(pts, ang), fill=fill)

# ---------- símbolos ----------
def filo():
    # Espada medieval: hoja recta de doble filo con canal, guarda recta, empuñadura y pomo.
    m = lienzo(); d = ImageDraw.Draw(m); A = 45
    poly(d, [(512,60),(568,170),(562,640),(462,640),(456,170)], A)            # hoja
    poly(d, [(503,190),(521,190),(521,600),(503,600)], A, 0)                   # canal
    poly(d, [(330,640),(694,640),(720,615),(735,665),(694,692),(330,692),(289,665),(304,615)], A)  # guarda
    poly(d, [(484,692),(540,692),(540,860),(484,860)], A)                      # puño
    for y in (720,760,800): poly(d, [(484,y),(540,y+18),(540,y+30),(484,y+12)], A, 0)  # tiras de cuero
    c = rot([(512,905)], A)[0]
    d.ellipse([c[0]-62,c[1]-62,c[0]+62,c[1]+62], fill=255)                     # pomo
    d.ellipse([c[0]-22,c[1]-22,c[0]+22,c[1]+22], fill=0)
    return m

def pistola():
    # Pistola de chispa de perfil: cañón largo con anillas, llave con pie de gato, culata curva.
    m = lienzo(); d = ImageDraw.Draw(m)
    poly(d, [(110,318),(150,318),(150,312),(640,330),(640,410),(150,428),(150,422),(110,422)])   # cañón y boca
    poly(d, [(170,318),(190,290),(210,318)])                                   # mira
    d.line([(165,368),(600,375)], fill=0, width=12)                            # arista del cañón
    poly(d, [(330,410),(640,410),(640,455),(380,448)])                         # caña de madera
    for x in (300,470): d.rectangle([x,312,x+24,452], fill=255); d.line([(x-4,316),(x-4,448)], fill=0, width=8)  # abrazaderas
    poly(d, [(560,300),(790,300),(820,360),(800,470),(560,470)])               # llave
    d.line([(575,455),(790,455)], fill=0, width=10)
    poly(d, [(600,300),(615,215),(650,215),(645,300)])                         # rastrillo
    poly(d, [(700,300),(690,230),(720,170),(780,160),(800,185),(760,200),(740,240),(760,300)])   # pie de gato
    poly(d, [(780,330),(880,350),(950,520),(985,700),(940,790),(840,800),(800,690),(770,560),(720,470)])  # culata
    d.ellipse([860,700,1000,840], fill=255)                                    # cantonera
    d.line([(820,420),(905,700)], fill=0, width=16)                            # veta
    d.arc([590,400,780,590], 0, 180, fill=255, width=30)                       # guardamonte
    poly(d, [(655,470),(690,470),(705,545),(675,548)])                         # gatillo
    return m

def mosquete():
    # Mosquete: cañón largo, caña de madera con abrazaderas, llave, guardamonte y culata ancha.
    m = lienzo(); d = ImageDraw.Draw(m); A = -32
    poly(d, [(40,448),(70,448),(70,452),(640,458),(640,512),(70,518),(70,522),(40,522)], A)   # cañón y boca
    poly(d, [(90,448),(105,420),(120,450)], A)                                  # mira
    poly(d, [(220,512),(640,512),(640,560),(260,552)], A)                       # caña de madera
    d.line(rot([(230,532),(620,540)], A), fill=0, width=12)                    # baqueta
    for x in (230,400,540): poly(d, [(x,446),(x+26,446),(x+26,560),(x,560)], A)   # abrazaderas
    poly(d, [(600,440),(780,440),(800,500),(780,580),(600,580)], A)             # llave
    poly(d, [(640,440),(650,370),(680,370),(680,440)], A)                       # rastrillo
    poly(d, [(715,440),(705,380),(735,330),(790,325),(800,350),(760,360),(752,400),(770,440)], A)  # pie de gato
    poly(d, [(780,455),(1010,500),(1025,720),(960,730),(840,640),(780,600)], A) # culata
    d.line(rot([(980,505),(990,720)], A), fill=0, width=14)                    # cantonera
    d.line(rot([(820,530),(960,620)], A), fill=0, width=14)                    # veta
    d.arc(rot([(650,560)], A)[0][0]-70, 0, 0, fill=0) if False else None
    c = rot([(705,585)], A)[0]
    d.arc([c[0]-75,c[1]-75,c[0]+75,c[1]+75], 0 + A, 180 + A, fill=255, width=26)   # guardamonte
    return m

def segunda_bala():
    m = lienzo(); d = ImageDraw.Draw(m)
    def bala(cx, base, ancho, alto, ang):
        w = ancho/2
        pts = [(cx-w, base), (cx-w, base-alto*0.55)]
        for i in range(21):                                            # punta ojival
            t = i/20; a = math.pi*(1-t)
            pts.append((cx + w*math.cos(a), base-alto*0.55 - math.sin(a)*alto*0.45 * (1 - 0.25*abs(math.cos(a)))))
        pts += [(cx+w, base-alto*0.55), (cx+w, base)]
        poly(d, rot(pts, ang, (cx, base)), 0)
        anillo = [(cx-w, base-alto*0.2), (cx+w, base-alto*0.2), (cx+w, base-alto*0.16), (cx-w, base-alto*0.16)]
        poly(d, rot(anillo, ang, (cx, base)), 0, 0)
        culote = [(cx-w*1.15, base), (cx+w*1.15, base), (cx+w*1.15, base+alto*0.09), (cx-w*1.15, base+alto*0.09)]
        poly(d, rot(culote, ang, (cx, base)), 0)
    bala(400, 820, 170, 560, -12)
    bala(640, 800, 170, 560, 12)
    # destellos de disparo entre las dos
    for a, l in ((-90, 120), (-60, 90), (-120, 90)):
        x1, y1 = 520 + 60*math.cos(math.radians(a)), 230 + 60*math.sin(math.radians(a))
        x2, y2 = 520 + (60+l)*math.cos(math.radians(a)), 230 + (60+l)*math.sin(math.radians(a))
        d.line([(x1, y1), (x2, y2)], fill=255, width=26)
    return m

def herbolario():
    # Manojo de hierbas medicinales: tres tallos curvos con hojas, atados, y raíces abajo.
    m = lienzo(); d = ImageDraw.Draw(m)
    def tallo(pts, ancho=26): d.line(pts, fill=255, width=ancho, joint="curve")
    def hoja(cx, cy, ang, largo, gros=0.34):
        pts = []
        for k in range(31):
            t = k/30; pts.append((cx + largo*t, cy - math.sin(math.pi*t)*largo*gros))
        for k in range(30, -1, -1):
            t = k/30; pts.append((cx + largo*t, cy + math.sin(math.pi*t)*largo*gros))
        d.polygon(rot(pts, ang, (cx, cy)), fill=255)
        d.line(rot([(cx+8, cy), (cx + largo*0.85, cy)], ang, (cx, cy)), fill=0, width=9)
    base = (512, 700)
    for (dx, top, hojas) in ((-230, 200, [(-150, 380), (-30, 300), (-160, 260)]),
                             (0, 120, [(-120, 420), (-60, 330), (-130, 250), (-60, 190)]),
                             (230, 200, [(-30, 380), (-150, 300), (-20, 260)])):
        pts = [(base[0] + dx*t + dx*0.25*math.sin(math.pi*t), base[1] - (base[1]-top)*t) for t in [k/20 for k in range(21)]]
        tallo(pts)
        for idx, (ang, y) in enumerate(hojas):
            t = (base[1] - y) / (base[1] - top); x = base[0] + dx*t + dx*0.25*math.sin(math.pi*t)
            a = ang if idx % 2 == 0 else 180 - (180 + ang) % 360 + 0
            hoja(x, y, -40 if idx % 2 == 0 else -140, 170 - 15*idx)
    d.rounded_rectangle([430, 660, 594, 730], 18, fill=255)                     # atadura
    d.line([(440, 690), (584, 700)], fill=0, width=10)
    for k, dx in enumerate((-90, -40, 0, 40, 90)):                              # raíces
        d.line([(512 + dx*0.4, 730), (512 + dx, 830 + 30*(k % 2)), (512 + dx*1.3, 900)], fill=255, width=18)
    return m

def ojo():
    m = lienzo(); d = ImageDraw.Draw(m)
    pts = []
    for i in range(61):
        t = i/60; x = 130 + 764*t; pts.append((x, 512 - math.sin(math.pi*t)*270))
    for i in range(60, -1, -1):
        t = i/60; x = 130 + 764*t; pts.append((x, 512 + math.sin(math.pi*t)*270))
    d.polygon(pts, fill=255)
    d.ellipse([352,352,672,672], fill=0)                                # iris (hueco)
    d.ellipse([382,382,642,642], fill=255)
    d.ellipse([480,350,544,674], fill=0)                                # pupila de serpiente
    for a in range(200, 341, 28):                                       # pestañas / rayos
        r1, r2 = 300, 420
        x1, y1 = 512 + r1*math.cos(math.radians(a)), 512 + r1*math.sin(math.radians(a)) - 20
        x2, y2 = 512 + r2*math.cos(math.radians(a)), 512 + r2*math.sin(math.radians(a)) - 20
        d.line([(x1,y1),(x2,y2)], fill=255, width=30)
    return m

def curacion():
    m = lienzo(); d = ImageDraw.Draw(m)
    # libro como el del juego (tapa inclinada) con un corazón en la tapa
    poly(d, [(250,170),(760,140),(800,800),(300,860)])
    poly(d, [(300,860),(800,800),(820,850),(320,915)])
    d.line([(300,860),(800,800)], fill=0, width=18)
    d.line([(318,890),(812,828)], fill=0, width=10)
    hx, hy, r = 525, 470, 105
    d.ellipse([hx-2*r+10, hy-r, hx+10, hy+r], fill=0); d.ellipse([hx-10, hy-r, hx+2*r-10, hy+r], fill=0)
    poly(d, [(hx-2*r+18, hy+30), (hx+2*r-18, hy+30), (hx, hy+r*2.4)], 0, 0)
    d.rectangle([hx-14, hy-40, hx+14, hy+120], fill=255); d.rectangle([hx-70, hy+16, hx+70, hy+44], fill=255)
    return m

def cargamento():
    m = lienzo(); d = ImageDraw.Draw(m)
    poly(d, [(170,560),(860,560),(860,900),(170,900)])                     # caja
    poly(d, [(140,520),(890,520),(890,590),(140,590)])                     # borde superior
    d.rectangle([170,650,860,666], fill=0); d.rectangle([170,800,860,816], fill=0)   # tablas
    d.rectangle([475,690,555,780], fill=0); d.rectangle([495,710,535,760], fill=255)  # cerrojo
    for x, alto in ((250,330), (380,380), (510,340), (640,390)):          # balas de punta redonda
        w = 100
        d.rectangle([x, 520-alto*0.62, x+w, 520], fill=255)
        d.ellipse([x, 520-alto, x+w, 520-alto*0.62+w*0.5], fill=255)
        d.rectangle([x, 520-alto*0.62-6, x+w, 520-alto*0.62+6], fill=0)   # unión vaina-bala
    return m

def robin_hood():
    m = lienzo(); d = ImageDraw.Draw(m)
    d.ellipse([250,250,770,770], fill=255); d.ellipse([300,300,720,720], fill=0); d.ellipse([330,330,690,690], fill=255)
    for k in range(12):                                                    # canto de la moneda
        a = k * math.pi / 6
        d.line([(510+175*math.cos(a), 510+175*math.sin(a)), (510+160*math.cos(a), 510+160*math.sin(a))], fill=0, width=10)
    poly(d, [(510,400),(560,500),(510,620),(460,500)], 0, 0)              # rombo grabado
    d.line([(120,900),(900,120)], fill=255, width=32)                      # flecha que la atraviesa
    poly(d, [(910,110),(770,150),(870,250)])                               # punta
    for k in range(3):                                                      # plumas
        o = 55*k
        poly(d, [(160+o,860-o),(95+o,815-o),(125+o,790-o),(190+o,835-o)])
        poly(d, [(160+o,860-o),(205+o,925-o),(180+o,955-o),(135+o,890-o)])
    return m

def vampiro():
    # Murciélago con alas de membrana (dedos marcados y borde festoneado) y una gota de sangre.
    m = lienzo(); d = ImageDraw.Draw(m)
    for lado in (-1, 1):
        cx = 512
        mano = (cx + lado*390, 250)
        dedos = [(cx + lado*420, 470), (cx + lado*300, 520), (cx + lado*170, 500)]
        ala = [(cx + lado*60, 330), mano]
        # borde inferior festoneado entre las puntas de los dedos
        prev = mano
        for p in dedos + [(cx + lado*70, 450)]:
            mx, my = (prev[0]+p[0])/2, (prev[1]+p[1])/2 - 55
            for t in [k/8 for k in range(1, 9)]:
                x = (1-t)**2*prev[0] + 2*(1-t)*t*mx + t*t*p[0]; y = (1-t)**2*prev[1] + 2*(1-t)*t*my + t*t*p[1]
                ala.append((x, y))
            prev = p
        d.polygon(ala, fill=255)
        for p in dedos: d.line([mano, p], fill=0, width=12)                      # huesos de los dedos
        d.line([(cx + lado*60, 330), mano], fill=0, width=12)
        poly(d, [mano, (mano[0] + lado*40, 200), (mano[0] - lado*10, 245)])     # garra del ala
    d.ellipse([430, 300, 594, 520], fill=255)                                   # cuerpo
    d.ellipse([445, 230, 579, 360], fill=255)                                   # cabeza
    poly(d, [(455, 270), (470, 160), (510, 250)]); poly(d, [(514, 250), (554, 160), (569, 270)])   # orejas
    d.ellipse([472, 280, 500, 305], fill=0); d.ellipse([524, 280, 552, 305], fill=0)               # ojos
    poly(d, [(488, 330), (498, 368), (508, 330)], 0, 0); poly(d, [(516, 330), (526, 368), (536, 330)], 0, 0)   # colmillos
    d.ellipse([452, 720, 572, 840], fill=255); poly(d, [(512, 610), (566, 750), (458, 750)])         # gota de sangre
    return m

def mercader():
    # Mercader soberano: una moneda grande con una corona grabada y el borde estriado, y otra moneda detrás.
    m = lienzo(); d = ImageDraw.Draw(m)
    def moneda(cx, cy, r, detalle):
        d.ellipse([cx-r-22, cy-r-22, cx+r+22, cy+r+22], fill=0)          # hueco oscuro que la separa de la de atrás
        d.ellipse([cx-r, cy-r, cx+r, cy+r], fill=255)
        for k in range(40):                                              # borde estriado
            a = math.radians(k*9); ca, sa = math.cos(a), math.sin(a)
            d.line([(cx+ca*(r-46), cy+sa*(r-46)), (cx+ca*(r-8), cy+sa*(r-8))], fill=0, width=10)
        d.ellipse([cx-(r-62), cy-(r-62), cx+(r-62), cy+(r-62)], fill=0)  # aro interior
        d.ellipse([cx-(r-78), cy-(r-78), cx+(r-78), cy+(r-78)], fill=255)
        if detalle: detalle(cx, cy, r)
    def corona(cx, cy, r):
        k = r/300.0
        base = [(cx-150*k, cy+95*k), (cx-150*k, cy-80*k), (cx-75*k, cy-5*k), (cx, cy-120*k), (cx+75*k, cy-5*k), (cx+150*k, cy-80*k), (cx+150*k, cy+95*k)]
        d.polygon(base, fill=0)                                          # corona (hueco oscuro en la moneda)
        for (px, py) in ((cx-150*k, cy-80*k), (cx, cy-120*k), (cx+150*k, cy-80*k)):
            d.ellipse([px-22*k, py-22*k-10*k, px+22*k, py+22*k-10*k], fill=0)   # perlas de las puntas
        d.rectangle([cx-150*k, cy+40*k, cx+150*k, cy+52*k], fill=255)    # banda
        for dx in (-90, 0, 90): d.ellipse([cx+dx*k-16*k, cy+66*k-16*k, cx+dx*k+16*k, cy+66*k+16*k], fill=255)   # joyas
    moneda(640, 400, 290, None)                                          # la de atrás
    moneda(420, 610, 300, corona)                                        # la de delante, con la corona
    return m

def bala1():
    # Una bala de plata: punta redondeada, cuerpo recto, anillo y base.
    m = lienzo(); d = ImageDraw.Draw(m)
    def bala(cx, y0, y1, w):
        d.pieslice([cx-w, y0, cx+w, y0+2*w], 180, 360, fill=255)           # punta
        d.rectangle([cx-w, y0+w, cx+w, y1-70], fill=255)                  # cuerpo
        d.rectangle([cx-w, y1-70, cx+w, y1-30], fill=0); d.rectangle([cx-w-14, y1-58, cx+w+14, y1], fill=255)   # ranura y base
        d.rectangle([cx-w-14, y1-30, cx+w+14, y1-22], fill=0)
        d.line([(cx-w*0.35, y0+w*1.1), (cx-w*0.35, y1-110)], fill=0, width=14)   # brillo
    bala(512, 140, 880, 150)
    return m

def paquete_balas():
    # Un paquete: caja con tapa y tres balas asomando.
    m = lienzo(); d = ImageDraw.Draw(m)
    for cx in (330, 512, 694):
        w = 78
        d.pieslice([cx-w, 150, cx+w, 150+2*w], 180, 360, fill=255)
        d.rectangle([cx-w, 150+w, cx+w, 560], fill=255)
        d.line([(cx-w*0.35, 150+w*1.1), (cx-w*0.35, 520)], fill=0, width=10)
    d.rectangle([230, 480, 794, 860], fill=255)                              # caja
    d.rectangle([230, 480, 794, 540], fill=0); d.rectangle([250, 498, 774, 522], fill=255)   # franja de la tapa
    d.rectangle([420, 640, 604, 700], fill=0)                                # etiqueta
    return m

def botas():
    # Bota con un ala (ligereza).
    m = lienzo(); d = ImageDraw.Draw(m)
    poly(d, [(330,200),(560,200),(560,620),(820,680),(860,760),(860,840),(300,840),(300,560)])   # bota
    d.rectangle([300,780,860,800], fill=0)                                       # suela
    for y in (300, 400, 500): d.line([(345,y),(545,y)], fill=0, width=14)       # cordones
    d.rectangle([320,200,570,250], fill=255); d.line([(320,255),(570,255)], fill=0, width=10)
    # ala en el talón
    for k, (l, a) in enumerate(((330, 200), (280, 215), (230, 230), (180, 245))):
        x0, y0 = 300, 380 + k*40
        pts = [(x0, y0), (x0 - l*math.cos(math.radians(a-180)), y0 - l*math.sin(math.radians(a-180))*-0.35 - 60 + k*10), (x0 - l*0.8, y0 + 40)]
        d.polygon([(x0, y0 - 20), (x0 - l, y0 - 90 + k*25), (x0 - l*0.85, y0 - 40 + k*25), (x0, y0 + 25)], fill=255)
        d.line([(x0 - 10, y0 + 2), (x0 - l*0.85, y0 - 50 + k*25)], fill=0, width=8)
    return m

def armadura():
    # Coraza que se angosta en la cintura, cuello alto, hombreras de placas y escudo en el pecho.
    m = lienzo(); d = ImageDraw.Draw(m)
    cuerpo = [(330,250),(430,215),(470,260),(554,260),(594,215),(694,250),(720,420),(660,600),(690,760),(600,880),(424,880),(334,760),(364,600),(304,420)]
    d.polygon(cuerpo, fill=255)
    d.line([(470,262),(512,320),(554,262)], fill=0, width=12)                   # escote
    for lado in (-1, 1):                                                        # hombreras: 3 placas
        for k in range(3):
            cx, cy = 512 + lado*(250 + k*22), 300 + k*55
            d.pieslice([cx-130, cy-90, cx+130, cy+90], 180 if lado < 0 else 270, 270 if lado < 0 else 360, fill=255)
            d.arc([cx-130, cy-90, cx+130, cy+90], 180 if lado < 0 else 270, 270 if lado < 0 else 360, fill=0, width=10)
    d.line([(512,330),(512,600)], fill=0, width=10)                             # quilla
    d.line([(364,605),(660,605)], fill=0, width=14)                             # cintura
    for y in (690, 780): d.line([(350,y),(674,y)], fill=0, width=12)           # láminas de la falda
    poly(d, [(430,350),(594,350),(594,470),(512,550),(430,470)], 0, 0)          # escudo
    poly(d, [(448,368),(576,368),(576,462),(512,524),(448,462)])
    d.line([(512,378),(512,512)], fill=0, width=10); d.line([(458,425),(566,425)], fill=0, width=10)
    return m

def escopeta_base(d, A=-28):
    # Escopeta recortada de dos cañones: cañones cortos paralelos, báscula, gatillos y culata de pistola.
    poly(d, [(150,395),(560,395),(560,445),(150,445)], A)                      # cañón de arriba
    poly(d, [(150,455),(560,455),(560,505),(150,505)], A)                      # cañón de abajo
    d.line(rot([(160,450),(550,450)], A), fill=0, width=10)                    # separación
    for x in (150,):
        c1 = rot([(x,420)], A)[0]; c2 = rot([(x,480)], A)[0]
        for c in (c1, c2): d.ellipse([c[0]-17,c[1]-17,c[0]+17,c[1]+17], fill=0)   # bocas
    poly(d, [(300,505),(560,505),(560,545),(330,540)], A)                      # guardamano
    poly(d, [(560,385),(680,385),(700,440),(680,545),(560,545)], A)            # báscula
    poly(d, [(600,385),(615,345),(650,345),(655,385)], A)                      # martillos
    poly(d, [(680,400),(790,430),(900,560),(930,700),(860,740),(800,640),(740,560),(690,545)], A)  # culata recortada
    d.line(rot([(720,450),(860,640)], A), fill=0, width=14)                    # veta
    c = rot([(640,560)], A)[0]
    d.arc([c[0]-60,c[1]-60,c[0]+60,c[1]+60], 0 + A, 180 + A, fill=255, width=22)   # guardamonte

def escopeta_recarga():
    # Escopeta y dos cartuchos que entran volando, con líneas de velocidad.
    m = lienzo(); d = ImageDraw.Draw(m)
    escopeta_base(d)
    for (x, y) in ((640, 60), (810, 130)):
        d.rectangle([x, y, x+70, y+150], fill=255)                             # cartucho
        d.rectangle([x-6, y+150, x+76, y+190], fill=255)                       # culote de latón
        d.line([(x-6, y+150), (x+76, y+150)], fill=0, width=10)
        for k in range(3): d.line([(x-40-k*30, y+20+k*45), (x-120-k*30, y+20+k*45)], fill=255, width=16)   # velocidad
    return m

def escopeta_dano():
    # Escopeta disparando: abanico de perdigones saliendo de los cañones.
    m = lienzo(); d = ImageDraw.Draw(m)
    escopeta_base(d)
    o = rot([(150,450)], -28)[0]; q = rot([(560,450)], -28)[0]
    base = math.atan2(o[1]-q[1], o[0]-q[0])
    random.seed(7)
    for k in range(16):
        ang = base + math.radians(random.uniform(-24, 24)); r = random.uniform(110, 480)
        x, y = o[0] + math.cos(ang)*r, o[1] + math.sin(ang)*r
        rr = random.uniform(20, 30); d.ellipse([x-rr, y-rr, x+rr, y+rr], fill=255)
    return m

def flecha(d, x0, y0, x1, y1, grosor=26, punta=70, plumas=True):
    ang = math.atan2(y1-y0, x1-x0); ca, sa = math.cos(ang), math.sin(ang)
    d.line([(x0, y0), (x1 - ca*punta*0.8, y1 - sa*punta*0.8)], fill=255, width=grosor)
    px, py = -sa, ca
    d.polygon([(x1, y1), (x1 - ca*punta + px*punta*0.55, y1 - sa*punta + py*punta*0.55), (x1 - ca*punta*0.7, y1 - sa*punta*0.7),
               (x1 - ca*punta - px*punta*0.55, y1 - sa*punta - py*punta*0.55)], fill=255)
    if plumas:
        for k in range(3):
            bx, by = x0 + ca*k*38, y0 + sa*k*38
            d.polygon([(bx, by), (bx - ca*60 + px*55, by - sa*60 + py*55), (bx + ca*30 + px*45, by + sa*30 + py*45)], fill=255)
            d.polygon([(bx, by), (bx - ca*60 - px*55, by - sa*60 - py*55), (bx + ca*30 - px*45, by + sa*30 - py*45)], fill=255)

def flechas_item():
    m = lienzo(); d = ImageDraw.Draw(m)
    flecha(d, 180, 840, 840, 180); flecha(d, 300, 900, 900, 300)
    return m

def bolsa_ampliada():
    # Aljaba de cuero llena de flechas, con correa.
    m = lienzo(); d = ImageDraw.Draw(m); A = -20
    for k, x in enumerate((420, 500, 580, 660)):
        flecha(d, *rot([(x, 470)], A)[0], *rot([(x - 20, 120 + (k % 2)*40)], A)[0], grosor=22, punta=60, plumas=False)
        c = rot([(x, 470)], A)[0]
    poly(d, [(380,430),(700,430),(680,900),(400,900)], A)                 # cuerpo
    poly(d, [(370,420),(710,420),(710,480),(370,480)], A, 0)
    poly(d, [(370,430),(710,430),(710,470),(370,470)], A)                 # boca reforzada
    for y in (560, 760): d.line(rot([(392,y),(690,y)], A), fill=0, width=16)   # costuras
    d.arc([250, 250, 900, 1000], 200, 330, fill=255, width=26)            # correa
    return m

def ballesta_dano():
    # Punta de flecha de acero grande, con filo marcado.
    m = lienzo(); d = ImageDraw.Draw(m); A = 45
    poly(d, [(512,80),(700,520),(560,470),(560,940),(464,940),(464,470),(324,520)], A)
    d.line(rot([(512,150),(512,460)], A), fill=0, width=14)
    poly(d, [(430,600),(594,600),(594,640),(430,640)], A, 0)
    return m

def ballesta_triple():
    m = lienzo(); d = ImageDraw.Draw(m)
    for y in (300, 512, 724): flecha(d, 130, y, 900, y, grosor=30, punta=110)
    return m

def bola():
    # Bola de cristal sobre su pie, con un remolino y destellos.
    m = lienzo(); d = ImageDraw.Draw(m)
    d.ellipse([222, 110, 802, 690], fill=255)
    for k in range(3):
        d.arc([300 + k*40, 190 + k*40, 724 - k*40, 614 - k*40], 200 + k*50, 340 + k*50, fill=0, width=22)
    d.ellipse([330, 220, 420, 300], fill=0)
    poly(d, [(330,700),(694,700),(760,800),(264,800)])
    poly(d, [(230,800),(794,800),(794,870),(230,870)])
    for (x, y) in ((150, 160), (880, 260), (860, 600)):
        poly(d, [(x, y-50), (x+14, y-14), (x+50, y), (x+14, y+14), (x, y+50), (x-14, y+14), (x-50, y), (x-14, y-14)])
    return m

def arma_ballesta():
    # Ballesta vista de lado: culata, riel con el virote y el arco de la punta.
    m = lienzo(); d = ImageDraw.Draw(m)
    poly(d, [(120,520),(560,490),(760,500),(760,540),(560,550),(300,610),(120,640)])        # culata y caja
    poly(d, [(560,470),(900,478),(900,500),(560,500)])                                      # riel
    poly(d, [(900,470),(980,489),(900,508)])                                                # punta del virote
    d.arc([700,150,1010,830], 270, 90, fill=255, width=34)                                  # arco
    d.line([(855,175),(560,490)], fill=255, width=10); d.line([(855,805),(560,540)], fill=255, width=10)   # cuerda
    poly(d, [(330,590),(380,590),(400,690),(350,690)])                                      # gatillo
    return m

def arma_escopeta():
    m = lienzo(); d = ImageDraw.Draw(m); escopeta_base(d); return m

def cargador_rifle():
    # Cargador de rifle (distinto de la caja de Cargamento): cuerpo curvo vertical con ventanas
    # de conteo, base con talón y 4 cartuchos escalonados que asoman por arriba.
    m = lienzo(); d = ImageDraw.Draw(m); A = 14
    cuerpo = [(330,400),(650,400),(690,620),(730,860),(720,930),(420,930),(390,860),(350,620)]
    poly(d, cuerpo, A)                                                        # cuerpo
    poly(d, [(395,930),(745,930),(760,990),(380,990)], A)                     # base
    poly(d, [(352,440),(368,440),(398,880),(382,880)], A, 0)                  # nervio lateral
    for i, y in enumerate((480,560,640,720)):                                 # ventanas de conteo
        x0 = 470 + (y-480)*0.17
        c = rot([(x0+80, y+22)], A)[0]
        d.ellipse([c[0]-26,c[1]-26,c[0]+26,c[1]+26], fill=0)
    poly(d, [(330,400),(650,400),(640,365),(340,365)], A, 0)                  # labios (separación)
    for i in range(4):                                                        # cartuchos escalonados
        x = 300 + i*95; base = 395 - i*30; w = 82; alto = 260
        poly(d, [(x,base),(x+w,base),(x+w,base-alto*0.6),(x,base-alto*0.6)], A)
        poly(d, [(x,base-alto*0.6-8),(x+w,base-alto*0.6-8),(x+w*0.78,base-alto*0.88),(x+w*0.5,base-alto),(x+w*0.22,base-alto*0.88)], A)
        poly(d, [(x-4,base-alto*0.6-2),(x+w+4,base-alto*0.6-2),(x+w+4,base-alto*0.6+10),(x-4,base-alto*0.6+10)], A, 0)  # unión vaina-bala
    return m

def llave_maestra():
    # Llave maestra antigua en diagonal: aro con calado de cuatro lóbulos, cuello con anillas,
    # caña larga y paletón con varios dientes.
    m = lienzo(); d = ImageDraw.Draw(m); A = -45
    cx, cy = 512, 230
    c = rot([(cx, cy)], A)[0]
    d.ellipse([c[0]-190,c[1]-190,c[0]+190,c[1]+190], fill=255)               # aro
    for ang in (0, 90, 180, 270):                                             # calado de cuatro lóbulos
        o = rot([(cx + 70*math.cos(math.radians(ang)), cy + 70*math.sin(math.radians(ang)))], A)[0]
        d.ellipse([o[0]-58,o[1]-58,o[0]+58,o[1]+58], fill=0)
    d.ellipse([c[0]-30,c[1]-30,c[0]+30,c[1]+30], fill=255)                    # botón central
    poly(d, [(482,400),(542,400),(542,900),(482,900)], A)                     # caña
    for y in (430,480): poly(d, [(452,y),(572,y),(572,y+30),(452,y+30)], A)  # anillas del cuello
    poly(d, [(542,740),(700,740),(700,940),(542,940)], A)                     # paletón
    for y0, y1 in ((770,800),(840,870)): poly(d, [(620,y0),(700,y0),(700,y1),(620,y1)], A, 0)  # muescas
    poly(d, [(600,880),(640,880),(640,940),(600,940)], A, 0)
    return m

EQUIPADO = {"coop_escopeta": arma_escopeta, "coop_ballesta": arma_ballesta, "coop_bola": bola,
    # mejoras sin «(libro)»: van a «Equipado» (pedido 2026-11-17)
    "coop_libro_filo": filo, "coop_libro_pistolero": pistola, "coop_libro_mosquetero": mosquete,
    "coop_libro_cargamento": cargamento, "coop_libro_botas": botas, "coop_libro_armadura": armadura,
    "coop_libro_escopeta_recarga": escopeta_recarga, "coop_libro_escopeta_dano": escopeta_dano,
    "coop_libro_cargador_rifle": cargador_rifle, "coop_libro_ballesta_dano": ballesta_dano,
    "coop_libro_ballesta_triple": ballesta_triple}

SIMBOLOS = {
    "coop_bala_plata": bala1, "coop_paquete_balas": paquete_balas,
    "coop_bola": bola,
    "coop_flechas": flechas_item, "coop_libro_bolsa_ampliada": bolsa_ampliada,
    "coop_libro_ballesta_dano": ballesta_dano, "coop_libro_ballesta_triple": ballesta_triple,
    "coop_libro_escopeta_recarga": escopeta_recarga, "coop_libro_escopeta_dano": escopeta_dano,
    "coop_libro_botas": botas, "coop_libro_armadura": armadura,
    "coop_libro_cargamento": cargamento, "coop_libro_cargador_rifle": cargador_rifle, "coop_libro_llave_maestra": llave_maestra, "coop_libro_robin_hood": robin_hood,
    "coop_libro_vampiro": vampiro, "coop_libro_mercader": mercader,
    "coop_libro_curacion": curacion, "coop_libro_filo": filo, "coop_libro_pistolero": pistola,
    "coop_libro_mosquetero": mosquete, "coop_libro_segunda_bala": segunda_bala,
    "coop_libro_herbolario": herbolario, "coop_libro_ojo_bruja": ojo,
}

def reducir(m):
    # margen como los del juego (el símbolo ocupa ~80 %)
    bb = m.getbbox(); c = m.crop(bb)
    lado = max(c.size); cuadro = Image.new("L", (lado, lado), 0)
    cuadro.paste(c, ((lado - c.size[0])//2, (lado - c.size[1])//2))
    final = Image.new("L", (S, S), 0)
    t = int(S*0.80); cuadro = cuadro.resize((t, t), Image.LANCZOS)
    final.paste(cuadro, ((S-t)//2, (S-t)//2))
    return final.resize((256, 256), Image.LANCZOS)

def salpicadura(semilla, alcance=1.0):
    """Mancha de sangre como la de las habilidades del juego: masa roja en el centro que se
    abre en picos afilados hacia afuera, con vetas. Devuelve la intensidad (0-255)."""
    random.seed(semilla)
    N = 720
    perfil = [0.0]*N
    for _ in range(70):                                   # picos afilados de distinto largo
        c = random.randrange(N); ancho = random.uniform(2, 9); largo = random.uniform(0.45, 1.0)
        for k in range(-30, 31):
            v = largo * math.exp(-(k/ancho)**2)
            perfil[(c+k) % N] = max(perfil[(c+k) % N], v)
    vetas = [random.uniform(0.6, 1.0) for _ in range(N)]
    im = Image.new("L", (256, 256), 0); px = im.load()
    for y in range(256):
        for x in range(256):
            dx, dy = x-128, y-128; r = math.hypot(dx, dy) / 128.0
            a = int((math.atan2(dy, dx) + math.pi) / (2*math.pi) * N) % N
            R = alcance * (0.52 + 0.46*perfil[a])
            if r >= R: continue
            v = 1.0 if r < R*0.55 else (1.0 - (r - R*0.55)/(R*0.45))**0.8
            px[x, y] = int(255 * v * vetas[a])
    return im.filter(ImageFilter.GaussianBlur(0.9))

import sys
SOLO = sys.argv[1:]
for nombre, f in SIMBOLOS.items():
    if SOLO and nombre not in SOLO: continue
    mask = reducir(f())
    # tienda: blanco con una pequeña mancha de sangre detrás (fondo transparente)
    sp = salpicadura(nombre + "_tienda", 0.78)
    rojo_t = Image.new("RGBA", (256, 256), (150, 18, 10, 0)); rojo_t.putalpha(sp.point(lambda v: int(v*0.85)))
    blanco = Image.new("RGBA", (256, 256), (255, 255, 255, 0)); blanco.putalpha(mask)
    Image.alpha_composite(rojo_t, blanco).save(os.path.join(OUT, "item_" + nombre + ".png"))
    # habilidad: fondo negro, sangre detrás (también asoma por los huecos del dibujo) y el símbolo blanco
    gl = salpicadura(nombre, 1.0)
    rojo = Image.merge("RGB", (gl.point(lambda v: int(v*0.55)), gl.point(lambda v: int(v*0.05)), gl.point(lambda v: int(v*0.025))))
    sk = Image.composite(Image.new("RGB", (256, 256), (255, 255, 255)), rojo, mask)
    sk.save(os.path.join(OUT, "skill_" + nombre + ".png"))
for nombre, f in EQUIPADO.items():
    if SOLO and nombre not in SOLO and "equipado" not in SOLO: continue
    # como los botones de arma del juego: marco blanco, fondo negro y el símbolo dentro
    simb = reducir(f()).resize((190, 190), Image.LANCZOS)
    cuadro = Image.new("L", (256, 256), 0); cuadro.paste(simb, (33, 33))
    marco = Image.new("L", (256, 256), 0); dm = ImageDraw.Draw(marco)
    dm.rectangle([0, 0, 255, 255], fill=255); dm.rectangle([10, 10, 245, 245], fill=0)
    mask = Image.composite(Image.new("L", (256, 256), 255), cuadro, marco.point(lambda v: v))
    out = Image.new("RGBA", (256, 256), (0, 0, 0, 255)); out.paste(Image.new("RGBA", (256, 256), (255, 255, 255, 255)), (0, 0), mask)
    out.save(os.path.join(OUT, "equip_" + nombre + ".png"))
print("ok")

"""Square branding image for the GK2 Vanilla+ Steam guide – pixel art in the style of the mod logo.
Drawn on a 128x128 pixel canvas, scaled x8 (nearest) to 1024x1024."""
from PIL import Image
import math, random
random.seed(11)

G = {
'A':[".###.","#...#","#...#","#####","#...#","#...#","#...#"],
'D':["####.","#...#","#...#","#...#","#...#","#...#","####."],
'E':["#####","#....","#....","####.","#....","#....","#####"],
'G':[".####","#....","#....","#.###","#...#","#...#",".####"],
'I':[".###.","..#..","..#..","..#..","..#..","..#..",".###."],
'L':["#....","#....","#....","#....","#....","#....","#####"],
'N':["#...#","##..#","#.#.#","#..##","#...#","#...#","#...#"],
'U':["#...#","#...#","#...#","#...#","#...#","#...#",".###."],
'V':["#...#","#...#","#...#","#...#","#...#",".#.#.","..#.."],
'+':[".....","..#..","..#..","#####","..#..","..#..","....."],
}
def mask(text, s=1, gap=1, bold=0):
    cw = 5*s+bold; w = len(text)*(cw+gap)-gap; h = 7*s
    m = [[0]*w for _ in range(h)]
    for i, ch in enumerate(text):
        g = G[ch]; ox = i*(cw+gap)
        for y in range(7):
            for x in range(5):
                if g[y][x] == '#':
                    for dy in range(s):
                        for dx in range(s+bold): m[y*s+dy][ox+x*s+dx] = 1
    return m
def rgb(h): h = h.lstrip('#'); return tuple(int(h[i:i+2], 16) for i in (0, 2, 4))+(255,)
def lerp(a, b, t): t = max(0, min(1, t)); return tuple(int(a[i]+(b[i]-a[i])*t) for i in range(3))+(255,)
def noise(c, *n): d = random.choice(n or (0, 0, 0, -12, 9)); return tuple(max(0, min(255, v+d)) for v in c[:3])+(255,)

W = H = 128
img = Image.new('RGBA', (W, H)); px = img.load()
OUT = rgb('1a1620'); SH = rgb('0b0a10')
def put(x, y, c):
    if 0 <= x < W and 0 <= y < H: px[x, y] = c
def get(x, y): return px[x, y] if 0 <= x < W and 0 <= y < H else OUT

# ---------- night background ----------
for y in range(H):
    for x in range(W):
        d = math.hypot(x-64, y-60)/90
        put(x, y, lerp(rgb('2b3a6e'), rgb('0e1224'), d*1.25))
for sx, sy in [(9,10),(22,6),(40,12),(92,7),(116,14),(104,26),(12,30),(118,44),(6,52),(30,20),(80,4),(60,8)]:
    put(sx, sy, rgb('fff5d6'))
for sx, sy in [(22,6),(104,26)]:
    put(sx-1, sy, rgb('7d88b8')); put(sx+1, sy, rgb('7d88b8')); put(sx, sy-1, rgb('7d88b8')); put(sx, sy+1, rgb('7d88b8'))
# crescent moon (top right)
for y in range(0, 20):
    for x in range(100, 126):
        if math.hypot(x-116, y-9) < 6.5 and math.hypot(x-119, y-6.8) > 5.4: put(x, y, rgb('f4efd0'))
# ground glow / grass strip at the bottom
for x in range(W):
    top = int(112+2*math.sin(x/9.0))
    for y in range(top, H):
        put(x, y, rgb('3e7a36') if y == top else rgb('2a5226') if y < top+3 else rgb('1c3419'))
    if x % 3 == 0: put(x, top-1, rgb('4f9a44'))

# ---------- open book ----------
BX0, BX1, BY0, BY1 = 12, 116, 44, 100      # outer cover
MID = 64
# cover (leather) with shadow
for y in range(BY0+2, BY1+3):
    for x in range(BX0+2, BX1+3): put(x, y, SH)
for y in range(BY0, BY1+1):
    for x in range(BX0, BX1+1):
        if x in (BX0, BX1) or y in (BY0, BY1): put(x, y, OUT); continue
        put(x, y, noise(lerp(rgb('7a3f24'), rgb('4a2415'), (y-BY0)/(BY1-BY0)), 0, 0, -8, 6))
# pages (slightly curved top), left and right
def page(x0, x1, left):
    for x in range(x0, x1+1):
        t = (x-x0)/(x1-x0)
        curve = int(3*math.sin(math.pi*t))           # pages bulge upward a little
        top = BY0+5-curve
        bot = BY1-3-(curve//2)
        for y in range(top, bot+1):
            if y == top: put(x, y, OUT); continue
            shade = (1-t) if not left else t          # darker toward the spine
            c = lerp(rgb('f6ead0'), rgb('cbb68a'), shade**2*0.9 + (y-top)/(bot-top)*0.15)
            put(x, y, noise(c, 0, 0, 0, -6, 4))
        put(x, bot+1, OUT)
        # page edges (stacked pages)
        if left and x < x0+3 or (not left and x > x1-3):
            pass
    for y in range(BY0+4, BY1-2):
        edge = x0 if left else x1
        put(edge, y, rgb('b59e70'))
page(BX0+4, MID-1, True)
page(MID+1, BX1-4, False)
for y in range(BY0+2, BY1-1): put(MID, y, rgb('5a4430'))   # spine gutter
for y in range(BY0+3, BY1-2): put(MID-1, y, lerp(get(MID-1, y), rgb('8a7550'), .6)); put(MID+1, y, lerp(get(MID+1, y), rgb('8a7550'), .6))

# ---------- emblem on the left page ----------
cx, cy, R = 38, 71, 19
for y in range(cy-R-2, cy+R+3):
    for x in range(cx-R-2, cx+R+3):
        d = math.hypot(x-cx+0.5, y-cy+0.5)
        if d > R+1.2: continue
        if d > R: put(x, y, OUT)
        elif d > R-4:
            t = (y-(cy-R))/(2*R)
            c = noise(lerp(rgb('b8b2a6'), rgb('6d675e'), t), 0, 0, 0, -14, 12)
            if d > R-1: c = lerp(c, rgb('e0dace'), .35) if y < cy else lerp(c, rgb('3a352f'), .4)
            put(x, y, c)
        elif d > R-5: put(x, y, OUT)
        else:
            t = (y-(cy-R+5))/(2*(R-5))
            put(x, y, lerp(rgb('141c38'), rgb('3c4f8c'), t))
for a in range(0, 360, 36):
    for k in range(4):
        r = R-3.6+k
        put(int(cx+r*math.cos(math.radians(a))), int(cy+r*math.sin(math.radians(a))), rgb('4a443c'))
for sx, sy in [(29,62),(33,58),(45,60),(30,68)]: put(sx, sy, rgb('fff5d6'))
for y in range(cy-R, cy+R):
    for x in range(cx-R, cx+R):
        if math.hypot(x-cx+0.5, y-cy+0.5) > R-5: continue
        if math.hypot(x-44, y-62) < 3.6 and math.hypot(x-45.6, y-60.8) > 3.0: put(x, y, rgb('f4efd0'))
for x in range(cx-R+5, cx+R-4):
    top = int(cy+8+2*math.sin((x-cx)/5.0))
    for y in range(top, cy+R-4):
        if math.hypot(x-cx+0.5, y-cy+0.5) <= R-5:
            put(x, y, rgb('3e7a36') if y == top else rgb('2f5f2b') if y < top+2 else rgb('24421f'))
gx, gy, gw, gh = cx-6, cy-7, 12, 16
stone = set()
for y in range(gy, gy+gh):
    for x in range(gx, gx+gw):
        if y < gy+5 and math.hypot(x-(gx+gw/2-0.5), y-(gy+5)) > gw/2: continue
        stone.add((x, y)); put(x, y, noise(lerp(rgb('d3cdc0'), rgb('8c867b'), (x-gx)/gw), 0, 0, -10, 8))
for (x, y) in list(stone):
    for dx, dy in ((1,0),(-1,0),(0,1),(0,-1)):
        if (x+dx, y+dy) not in stone and y+dy < gy+gh: put(x+dx, y+dy, OUT)
pcx, pcy = gx+gw//2, gy+7
for i in range(-3, 3):
    for w in (-1, 0):
        put(pcx+i, pcy+w, rgb('8fe06f')); put(pcx+w, pcy+i, rgb('8fe06f'))
put(pcx-1, pcy-1, rgb('e4ffd2'))
for x in range(gx-2, gx+gw+2):
    if x % 2 == 0: put(x, gy+gh-1, rgb('4f9a44'))

# ---------- checklist on the right page ----------
INK = rgb('6b5236'); INK2 = rgb('9c8360'); GREEN = rgb('3f9a3a'); GREEN2 = rgb('8fe06f')
rows = [54, 64, 74, 84]
lens = [20, 15, 21, 12]
for ry, ln in zip(rows, lens):
    bx = 72
    # small box
    for y in range(ry, ry+6):
        for x in range(bx, bx+6):
            if x in (bx, bx+5) or y in (ry, ry+5): put(x, y, INK)
    # green check mark
    for (x, y) in ((bx+1, ry+3), (bx+2, ry+4), (bx+3, ry+3), (bx+4, ry+2), (bx+5, ry+1), (bx+6, ry)):
        put(x, y, GREEN); put(x, y-1, GREEN2)
    # "text" line
    for x in range(bx+9, bx+9+ln):
        if (x*5+ry) % 13 == 0: continue            # word gaps
        put(x, ry+2, INK); put(x, ry+3, INK2)
# ribbon bookmark (green) hanging over the right page bottom
rx = 106
for y in range(BY0-4, BY1+8):
    for x in range(rx, rx+5):
        if y > BY1+4 and abs(x-(rx+2)) < (y-(BY1+4)): continue   # notched end
        c = rgb('4fb04a') if x < rx+2 else rgb('2f7a2d')
        if x in (rx, rx+4): c = OUT
        put(x, y, c)

# ---------- title VANILLA+ ----------
def draw_title(text, x0, y0, s, top, bot, hl, dark, stone=True, gap=2, bold=1):
    m = mask(text, s, gap, bold); h = len(m); w = len(m[0])
    for y in range(h):
        for x in range(w):
            if m[y][x]:
                for dx, dy in ((2,2),(1,2),(2,1)): put(x0+x+dx, y0+y+dy, SH)
    for y in range(h):
        for x in range(w):
            if m[y][x]:
                for dx in (-1, 0, 1):
                    for dy in (-1, 0, 1):
                        xx, yy = x+dx, y+dy
                        if not (0 <= yy < h and 0 <= xx < w and m[yy][xx]): put(x0+xx, y0+yy, OUT)
    for y in range(h):
        for x in range(w):
            if not m[y][x]: continue
            c = lerp(top, bot, y/(h-1))
            if stone: c = noise(c)
            if y == 0 or not m[y-1][x] or x == 0 or not m[y][x-1]: c = lerp(c, hl, .55)
            if y == h-1 or not m[y+1][x]: c = lerp(c, dark, .5)
            put(x0+x, y0+y, c)
    return w
tw = len(mask('VANILLA', 2, 2, 1)[0]); pw = len(mask('+', 2, 2, 1)[0])
x0 = (W-(tw+3+pw))//2
draw_title('VANILLA', x0, 20, 2, rgb('d8d2c4'), rgb('7f786c'), rgb('fbf6ea'), rgb('3a352f'))
draw_title('+', x0+tw+3, 20, 2, rgb('b8f59a'), rgb('3f9a3a'), rgb('eaffdc'), rgb('1f5a1f'), stone=False)

# ---------- wooden plank "GUIDE" ----------
m = mask('GUIDE', 2, 2, 0); mw, mh = len(m[0]), len(m)
pw_, ph_ = mw+14, mh+8
px0, py0 = (W-pw_)//2, 101
for y in range(py0+2, py0+ph_+2):
    for x in range(px0+2, px0+pw_+2): put(x, y, SH)
for y in range(py0, py0+ph_):
    for x in range(px0, px0+pw_):
        if x in (px0, px0+pw_-1) or y in (py0, py0+ph_-1): put(x, y, OUT); continue
        c = lerp(rgb('c28c55'), rgb('8a5a30'), (y-py0)/ph_)
        if (x*7+y*3) % 11 == 0: c = lerp(c, rgb('5a3a1f'), .5)
        if y == py0+1: c = lerp(c, rgb('d69a5c'), .5)
        put(x, y, c)
for (x, y) in ((px0+2, py0+2), (px0+pw_-3, py0+2), (px0+2, py0+ph_-3), (px0+pw_-3, py0+ph_-3)): put(x, y, rgb('3a2614'))
ox, oy = px0+7, py0+4
for y in range(mh):
    for x in range(mw):
        if m[y][x]:
            put(ox+x+1, oy+y+1, rgb('d69a5c'))  # carved highlight
for y in range(mh):
    for x in range(mw):
        if m[y][x]: put(ox+x, oy+y, rgb('22140a') if y > 0 else rgb('3d2614'))

img.save('base.png')
big = img.resize((1024, 1024), Image.NEAREST).convert('RGB')
big.save('GK2-VanillaPlus-Guide.png')
big.save('GK2-VanillaPlus-Guide.jpg', quality=95)
img.resize((195, 195), Image.LANCZOS).save('preview_195.png')
print('ok')

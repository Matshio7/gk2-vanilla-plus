from PIL import Image
import math, random
random.seed(7)
G = {
'A':[".###.","#...#","#...#","#####","#...#","#...#","#...#"],
'C':[".####","#....","#....","#....","#....","#....",".####"],
'D':["####.","#...#","#...#","#...#","#...#","#...#","####."],
'E':["#####","#....","#....","####.","#....","#....","#####"],
'F':["#####","#....","#....","####.","#....","#....","#...."],
'G':[".####","#....","#....","#.###","#...#","#...#",".####"],
'I':[".###.","..#..","..#..","..#..","..#..","..#..",".###."],
'K':["#...#","#..#.","#.#..","##...","#.#..","#..#.","#...#"],
'L':["#....","#....","#....","#....","#....","#....","#####"],
'M':["#...#","##.##","#.#.#","#.#.#","#...#","#...#","#...#"],
'N':["#...#","##..#","#.#.#","#..##","#...#","#...#","#...#"],
'O':[".###.","#...#","#...#","#...#","#...#","#...#",".###."],
'P':["####.","#...#","#...#","####.","#....","#....","#...."],
'Q':[".###.","#...#","#...#","#...#","#.#.#","#..#.",".##.#"],
'R':["####.","#...#","#...#","####.","#.#..","#..#.","#...#"],
'T':["#####","..#..","..#..","..#..","..#..","..#..","..#.."],
'U':["#...#","#...#","#...#","#...#","#...#","#...#",".###."],
'V':["#...#","#...#","#...#","#...#","#...#",".#.#.","..#.."],
'W':["#...#","#...#","#...#","#.#.#","#.#.#","##.##","#...#"],
'2':[".###.","#...#","....#","...#.","..#..",".#...","#####"],
'+':[".....","..#..","..#..","#####","..#..","..#..","....."],
'.':[".....",".....",".....","..#..",".....",".....","....."],
' ':["....."]*7,
}
def mask(text, s=1, gap=1, bold=0):
    cw=5*s+bold; w = len(text)*(cw+gap)-gap; h=7*s
    m = [[0]*w for _ in range(h)]
    for i,ch in enumerate(text):
        g=G[ch]; ox=i*(cw+gap)
        for y in range(7):
            for x in range(5):
                if g[y][x]=='#':
                    for dy in range(s):
                        for dx in range(s+bold): m[y*s+dy][ox+x*s+dx]=1
    return m
def rgb(h): h=h.lstrip('#'); return tuple(int(h[i:i+2],16) for i in (0,2,4))+(255,)
def lerp(a,b,t): return tuple(int(a[i]+(b[i]-a[i])*t) for i in range(3))+(255,)

W,H=236,92
img=Image.new('RGBA',(W,H),(0,0,0,0)); px=img.load()
OUT=rgb('1a1620'); SH=rgb('0d0b12')
def put(x,y,c):
    if 0<=x<W and 0<=y<H: px[x,y]=c

# ---------- Emblem ----------
cx,cy,R=34,38,30
for y in range(H):
    for x in range(W):
        d=math.hypot(x-cx+0.5,y-cy+0.5)
        if d<=R+1.2: 
            if d>R: put(x,y,OUT)
            elif d>R-5:  # stone ring
                t=(y-(cy-R))/(2*R)
                base=lerp(rgb('b8b2a6'),rgb('6d675e'),t)
                n=random.choice([0,0,0,-14,12])
                c=tuple(max(0,min(255,v+n)) for v in base[:3])+(255,)
                # ring bevel
                if d>R-1: c=lerp(c,rgb('e0dace'),0.35) if y<cy else lerp(c,rgb('3a352f'),0.4)
                if d<=R-4: c=lerp(c,rgb('2a2530'),0.5)
                put(x,y,c)
            elif d>R-6: put(x,y,OUT)
            else:  # night sky
                t=(y-(cy-R+6))/(2*(R-6))
                put(x,y,lerp(rgb('141c38'),rgb('3c4f8c'),max(0,min(1,t))))
# stone blocks seams on ring
for a in range(0,360,30):
    r1,r2=R-4.5,R-0.5
    for k in range(5):
        r=r1+(r2-r1)*k/4
        put(int(cx+r*math.cos(math.radians(a))),int(cy+r*math.sin(math.radians(a))),rgb('4a443c'))
# stars
for sx,sy in [(18,22),(24,15),(44,17),(50,26),(29,20),(39,12),(15,33)]:
    put(sx,sy,rgb('fff5d6'))
put(24,15,rgb('ffe9a8')); put(23,15,rgb('8a95c0')); put(25,15,rgb('8a95c0'))
# crescent moon
for y in range(8,26):
    for x in range(36,56):
        if math.hypot(x-47,y-17)<5.5 and math.hypot(x-49.5,y-15.5)>4.6: put(x,y,rgb('f4efd0'))
# hill / grass
for x in range(cx-R+6,cx+R-5):
    top=int(cy+13+3*math.sin((x-cx)/7.0))
    for y in range(top,cy+R-5):
        if math.hypot(x-cx+0.5,y-cy+0.5)<=R-6:
            c=rgb('3e7a36') if y==top else (rgb('2f5f2b') if y<top+3 else rgb('24421f'))
            if y==top and x%3==0: put(x,top-1,rgb('4f9a44'))
            put(x,y,c)
# gravestone
gx,gy,gw,gh=cx-8,cy-10,17,24
for y in range(gy,gy+gh):
    for x in range(gx,gx+gw):
        top_round = y<gy+6 and math.hypot(x-(gx+gw/2-0.5),(y-(gy+6))*1.0)>gw/2
        if top_round: continue
        edge = (x in (gx,gx+gw-1)) or y==gy+gh-1
        t=(x-gx)/gw
        c=lerp(rgb('d3cdc0'),rgb('8c867b'),t)
        n=random.choice([0,0,-10,8]); c=tuple(max(0,min(255,v+n)) for v in c[:3])+(255,)
        put(x,y,c)
# outline gravestone
pts=[(x,y) for y in range(H) for x in range(W) if px[x,y][:3]!=(0,0,0) or True]
stone=set()
for y in range(gy,gy+gh):
    for x in range(gx,gx+gw):
        if not (y<gy+6 and math.hypot(x-(gx+gw/2-0.5),(y-(gy+6)))>gw/2): stone.add((x,y))
for (x,y) in list(stone):
    for dx,dy in ((1,0),(-1,0),(0,1),(0,-1)):
        if (x+dx,y+dy) not in stone and y+dy<gy+gh: put(x+dx,y+dy,OUT)
# glowing plus on gravestone
pcx,pcy=gx+gw//2,gy+10
for (x,y) in list(stone):
    d=math.hypot(x-pcx,y-pcy)
    if d<7: c=px[x,y]; put(x,y,lerp(c,rgb('b6f59a'),0.25*(1-d/7)))
for i in range(-4,5):
    for w in (-1,0):
        put(pcx+i,pcy+w,rgb('8fe06f')); put(pcx+w,pcy+i,rgb('8fe06f'))
for i in range(-4,5):
    put(pcx+i,pcy-2 if abs(i)>1 else pcy-5 if False else pcy-2,px[pcx+i,pcy-2]) 
for i in (-5,4):
    put(pcx+i,pcy,rgb('3f8a3a')); put(pcx+i,pcy-1,rgb('3f8a3a')); put(pcx,pcy+i,rgb('3f8a3a')); put(pcx-1,pcy+i,rgb('3f8a3a'))
put(pcx-1,pcy-1,rgb('e4ffd2')); put(pcx,pcy-1,rgb('d0ffb8'))
# grass in front of stone
for x in range(gx-3,gx+gw+3):
    if x%2==0: put(x,gy+gh-1,rgb('4f9a44'))
    if x%4==1: put(x,gy+gh-2,rgb('5fb050'))
# rivets
for a in (45,135,225,315):
    rx=int(cx+(R-2.5)*math.cos(math.radians(a))); ry=int(cy+(R-2.5)*math.sin(math.radians(a)))
    put(rx,ry,rgb('2a2530')); put(rx-1,ry-1,rgb('d9d3c7'))

# ---------- Wooden plank "GK2" ----------
px0,py0,pw,ph=132,8,40,13
for y in range(py0,py0+ph):
    for x in range(px0,px0+pw):
        if x in (px0,px0+pw-1) or y in (py0,py0+ph-1): put(x,y,OUT); continue
        c=lerp(rgb('c28c55'),rgb('8a5a30'),(y-py0)/ph)
        if (x*7+y*3)%11==0: c=lerp(c,rgb('5a3a1f'),0.5)
        if y==py0+1: c=lerp(c,rgb('d69a5c'),0.5)
        put(x,y,c)
for (x,y) in ((px0+2,py0+2),(px0+pw-3,py0+2),(px0+2,py0+ph-3),(px0+pw-3,py0+ph-3)): put(x,y,rgb('3a2614'))
m=mask('GK2',1,1); mw=len(m[0])
ox=px0+(pw-mw)//2; oy=py0+3
for y in range(7):
    for x in range(mw):
        if m[y][x]: put(ox+x,oy+y,rgb('22140a')); 
        if m[y][x] and y==0: put(ox+x,oy+y,rgb('3d2614'))

# ---------- Title VANILLA+ ----------
def draw_title(text,x0,y0,s,fill_top,fill_bot,hl,dark,stone=True):
    m=mask(text,s,2,1); h=len(m); w=len(m[0])
    # shadow
    for y in range(h):
        for x in range(w):
            if m[y][x]:
                for dx,dy in ((2,2),(1,2),(2,1)): put(x0+x+dx,y0+y+dy,SH)
    # outline
    for y in range(h):
        for x in range(w):
            if m[y][x]:
                for dx in (-1,0,1):
                    for dy in (-1,0,1):
                        xx,yy=x+dx,y+dy
                        if not(0<=yy<h and 0<=xx<w and m[yy][xx]): put(x0+xx,y0+yy,OUT)
    for y in range(h):
        for x in range(w):
            if not m[y][x]: continue
            c=lerp(fill_top,fill_bot,y/(h-1))
            if stone:
                n=random.choice([0,0,0,-12,9]); c=tuple(max(0,min(255,v+n)) for v in c[:3])+(255,)
            above = y==0 or not m[y-1][x]
            below = y==h-1 or not m[y+1][x]
            left = x==0 or not m[y][x-1]
            if above or left: c=lerp(c,hl,0.55)
            if below: c=lerp(c,dark,0.5)
            put(x0+xx if False else x0+x,y0+y,c)
    return w
tw=draw_title('VANILLA',76,28,3,rgb('d8d2c4'),rgb('7f786c'),rgb('fbf6ea'),rgb('3a352f'))
draw_title('+',76+tw+3,28,3,rgb('b8f59a'),rgb('3f9a3a'),rgb('eaffdc'),rgb('1f5a1f'),stone=False)

# ---------- Subtitle ----------
sub='ULTRAWIDE . PERFORMANCE . QOL'
m=mask(sub,1,1); w=len(m[0]); ox=(W-w)//2; oy=78
for y in range(7):
    for x in range(w):
        if m[y][x]:
            put(ox+x+1,oy+y+1,SH)
for y in range(7):
    for x in range(w):
        if m[y][x]: put(ox+x,oy+y,rgb('ffd68c') if y<4 else rgb('e0a94f'))
img.save('/tmp/logo/base.png')
img.resize((W*6,H*6),Image.NEAREST).save('/tmp/logo/GK2-VanillaPlus-Logo.png')
# icon: emblem only
ic=img.crop((cx-R-2,cy-R-2,cx+R+3,cy+R+3)); ic=ic.resize((ic.width*8,ic.height*8),Image.NEAREST); ic.save('/tmp/logo/GK2-VanillaPlus-Icon.png')
# preview on dark bg
bg=Image.new('RGBA',(W*6,H*6),rgb('24283a')); bg.alpha_composite(Image.open('/tmp/logo/GK2-VanillaPlus-Logo.png')); bg.convert('RGB').save('/tmp/logo/preview.png')
print(img.size)

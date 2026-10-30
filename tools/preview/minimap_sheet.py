# Draws docs/preview.png: the round screen minimap on every base map and weather, on real ProcTrack laps (tools/preview/laps).
#   python3 tools/preview/minimap_sheet.py tools/preview/laps docs/preview.png
# A Python port of TrackImage.Build and PixelMap.Draw (round map) and of Palettes.For, as of 0.2.0: keep it in step with them when
# the round minimap's look changes. (The GPS and the icons are drawn by the C# tool in this folder, from the mod's own code.)
import sys, math, numpy as np
from PIL import Image, ImageDraw, ImageFont
LAPS, OUT = sys.argv[1], sys.argv[2]
def rgb(h): return np.array([(h>>16)&255,(h>>8)&255,h&255],float)/255
P = {'Stadium':(0x8a6e4f,0x3a3a3e,0xc9c7c0,0x2a2724,True),'Forest':(0x4f7d34,0x8c6239,0x4e3420,0x26331c,True),
 'Mountain':(0xeef2f7,0x515a66,0x8fb3d9,0x34404f,True),'Factory':(0x96703f,0x34302d,0xe07b24,0x2b231b,True),'Tokyo':(0x2b2d3c,0x17171d,0xff4fa8,0x121219,False)}
W = {'StadiumAsh':(0,0,1,0),'StadiumRainyNight':(1,0,0,0),'ForestRed':(0,0,1,1),'ForestRainyDay':(0,0,0,0),'ForestRainyNight':(1,0,0,0),
 'MountainDay':(0,1,0,0),'SnowNight':(1,1,0,0),'FactoryFire':(0,0,0,0),'FactoryNight':(1,0,0,0),'TokyoNeon':(0,0,0,0)}
luma=lambda c:0.299*c[0]+0.587*c[1]+0.114*c[2]
def palette(key,w):
    g,r,e,f,dim=P[key]; g,r,e,f=rgb(g),rgb(r),rgb(e),rgb(f)
    night,snow,ash,red=W[w]
    if key=='Forest' and red: g,f=rgb(0x8a3b24),rgb(0x3a1a12)
    if snow and key!='Mountain': g=g+(rgb(0xeef2f7)-g)*0.6
    if ash: g=g+(rgb(0x8c8c8c)-g)*0.3
    if night and dim: g,r,f=g*0.72,r*0.7,f*0.7
    if abs(luma(g)-luma(r))<0.15: r= r*0.55 if luma(g)>0.45 else r+(1-r)*0.35
    c32=lambda c:np.round(np.clip(c,0,1)*255).astype(np.uint8)
    return dict(g=c32(g),r=c32(r),e=c32(e),f=c32(f),rim=c32(f+(1-f)*0.35))
DET=4; RH=1.5; EW=1.0; RING=4
def build(pts,pal,size):
    xz=pts; c=(xz.min(0)+xz.max(0))/2; R=max(1,np.linalg.norm(xz-c,axis=1).max())
    side=size*DET; rh=RH*DET; reach=(RH+EW)*DET
    scale=((size*0.5-RING-RH-EW-1)*DET)/R
    px=(xz-c)*scale+side/2
    yy,xx=np.mgrid[0:side,0:side]+0.5; Q=np.stack([xx,yy],-1); d=np.full((side,side),1e9)
    for i in range(len(px)):
        a,b=px[i],px[(i+1)%len(px)]; ab=b-a; t=np.clip(((Q-a)@ab)/max(ab@ab,1e-9),0,1)
        d=np.minimum(d,np.linalg.norm(Q-(a+t[...,None]*ab),axis=-1))
    img=np.empty((side,side,3),np.uint8); img[:]=pal['g']; img[d<=reach]=pal['e']; img[d<=rh]=pal['r']
    o=px[0]; al=px[1]-px[-1]; al/=np.linalg.norm(al); ac=np.array([-al[1],al[0]])
    m=(abs((Q-o)@ac)<=reach)&(abs((Q-o)@al)<=DET*0.5); img[m]=(240,240,236)
    return img,c,scale
ARROW=["...#...","..#o#..",".#ooo#.",".#ooo#.","#ooooo#","#oo#oo#","##...##"]
DOT=[".###.","#ooo#","#ooo#","#ooo#",".###."]
OUTL=(14,13,16,255)
def draw(src,c,scale,pal,heading,player,cars,size):
    rad=math.radians(heading); co,si=math.cos(rad),math.sin(rad); half=size/2; side=src.shape[0]
    out=np.zeros((size,size,4),np.uint8)
    for y in range(size):
        for x in range(size):
            ox,oy=x+0.5-half,y+0.5-half; d=math.hypot(ox,oy)
            if d>half: continue
            if d>half-RING: out[y,x]=OUTL if d>half-1 else (*pal['f'],255) if d>half-RING+1 else (*pal['rim'],255); continue
            mx=ox*co+oy*si; my=-ox*si+oy*co; sx=math.floor(side/2+mx*DET); sy=math.floor(side/2+my*DET)
            out[y,x]=(*(pal['g'] if not(0<=sx<side and 0<=sy<side) else src[sy,sx]),255)
    def scr(w):
        m=(w-c)*scale/DET; X=m[0]*co-m[1]*si; Y=m[0]*si+m[1]*co; return math.floor(half+X),math.floor(half+Y)
    def stamp(shape,fill,at):
        h=len(shape)
        for row,line in enumerate(shape):
            y=at[1]+h//2-row
            for col,ch in enumerate(line):
                x=at[0]-len(line)//2+col
                if ch=='.' or not(0<=x<size and 0<=y<size): continue
                if (x+0.5-half)**2+(y+0.5-half)**2>(half-RING)**2: continue
                out[y,x]=OUTL if ch=='#' else fill
    for p,kind in cars: stamp(DOT,(63,169,245,255) if kind else (232,67,58,255),scr(p))
    stamp(ARROW,(245,245,240,255),scr(player))
    return Image.fromarray(np.flipud(out),'RGBA')   # texture row 0 is the bottom
def tile(key,w,seed,at,size=100,px=3):
    pts=np.loadtxt(f'{LAPS}/lap_{seed}.csv',delimiter=','); n=len(pts); pal=palette(key,w)
    src,c,scale=build(pts,pal,size)
    f=pts[(at+1)%n]-pts[at-1]; heading=math.degrees(math.atan2(f[0],f[1]))
    cars=[(pts[(at+k)%n],k==25) for k in (9,25,-14,170,330)]
    im=draw(src,c,scale,pal,heading,pts[at],cars,size)
    return im.resize((size*px,size*px),Image.NEAREST)
cases=[('Stadium','StadiumAsh',11),('Forest','ForestRainyDay',23),('Mountain','MountainDay',37),('Factory','FactoryFire',41),('Tokyo','TokyoNeon',58),
 ('Stadium','StadiumRainyNight',64),('Forest','ForestRed',72),('Mountain','SnowNight',89),('Factory','FactoryNight',95),('Forest','ForestRainyNight',101)]
labels={'StadiumAsh':'Stadium','StadiumRainyNight':'Stadium, rainy night','ForestRainyDay':'Forest','ForestRed':'Forest, red','ForestRainyNight':'Forest, rainy night',
 'MountainDay':'Mountain','SnowNight':'Mountain, snowy night','FactoryFire':'Factory','FactoryNight':'Factory, night','TokyoNeon':'Tokyo'}
cols=5; T=300; G=24
sheet=Image.new('RGB',(cols*(T+G)+G,2*(T+G+28)+G),(36,33,38)); d=ImageDraw.Draw(sheet)
try: font=ImageFont.truetype('/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf',17)
except Exception: font=ImageFont.load_default()
for i,(k,w,s) in enumerate(cases):
    im=tile(k,w,s,at=[120,200,300,430,520,80,260,380,470,560][i])
    x=G+(i%cols)*(T+G); y=G+(i//cols)*(T+G+28); sheet.paste(im,(x,y),im)
    tw=d.textlength(labels[w],font=font); d.text((x+(T-tw)/2,y+T+4),labels[w],fill=(220,216,210),font=font)
sheet.save(OUT); print('ok')

"""Render Harbor's geometric identity at every Windows asset size."""
from pathlib import Path
from PIL import Image, ImageDraw

assets = Path(__file__).resolve().parents[1] / 'src' / 'Harbor' / 'Assets'

def draw_icon(size):
    scale = 8
    image = Image.new('RGBA', (size*scale, size*scale))
    draw = ImageDraw.Draw(image)
    def box(values): return tuple(round(v*size*scale/64) for v in values)
    draw.rounded_rectangle(box((1,1,63,63)), radius=round(size*scale*15/64), fill='#123D42')
    draw.rounded_rectangle(box((13,29,51,53)), radius=round(size*scale*12/64), fill='#F4FAF9')
    draw.rectangle(box((13,29,51,39)), fill='#F4FAF9')
    draw.rounded_rectangle(box((22,23,42,45)), radius=round(size*scale*5/64), fill='#123D42')
    draw.rectangle(box((22,20,42,36)), fill='#123D42')
    draw.polygon([box((26,11)),box((38,16)),box((38,35)),box((32,40)),box((26,35))], fill='#64D9BF')
    return image.resize((size,size),Image.Resampling.LANCZOS)

icon = draw_icon(256)
icon.save(assets/'AppIcon.ico',sizes=[(16,16),(24,24),(32,32),(48,48),(64,64),(128,128),(256,256)])
for filename,size in [('Square44x44Logo.scale-200.png',88),('Square44x44Logo.targetsize-24_altform-unplated.png',24),('Square44x44Logo.targetsize-48_altform-lightunplated.png',48),('Square150x150Logo.scale-200.png',300),('StoreLogo.png',50),('LockScreenLogo.scale-200.png',48)]:
    draw_icon(size).save(assets/filename)
for filename,dimensions in [('SplashScreen.scale-200.png',(1240,600)),('Wide310x150Logo.scale-200.png',(620,300))]:
    canvas = Image.new('RGBA', dimensions)
    canvas.alpha_composite(draw_icon(192), ((dimensions[0]-192)//2,(dimensions[1]-192)//2))
    canvas.save(assets/filename)
draw_icon(512).save(assets/'Harbor.png')
print('Generated Harbor icon, tile and splash assets')

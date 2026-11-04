from PIL import Image, ImageDraw
from pathlib import Path

OUT = Path(__file__).resolve().parents[1] / "src" / "AuditaPC.App" / "Assets"
S = 1024
im = Image.new("RGBA", (S, S), (0, 0, 0, 0))
px = im.load()

# Deep navy diagonal gradient inside a rounded app tile.
for y in range(S):
    for x in range(S):
        t = (x + y) / (2 * (S - 1))
        px[x, y] = (int(27 - 12*t), int(62 - 31*t), int(75 - 28*t), 255)

mask = Image.new("L", (S, S), 0)
md = ImageDraw.Draw(mask)
md.rounded_rectangle((32, 32, 992, 992), radius=220, fill=255)
im.putalpha(mask)
d = ImageDraw.Draw(im)

# Monitor body and lower bezel.
d.rounded_rectangle((176, 154, 848, 694), radius=72, fill=(29, 68, 80), outline=(103, 220, 202), width=26)
d.rounded_rectangle((226, 206, 798, 640), radius=34, fill=(20, 46, 57))
d.rounded_rectangle((264, 242, 760, 310), radius=22, fill=(35, 76, 88))
d.ellipse((291, 263, 315, 287), fill=(241, 187, 94))
d.rounded_rectangle((340, 267, 586, 284), radius=8, fill=(219, 237, 232))

# Three clear checklist rows.
for y, width, alpha in [(382, 390, 245), (492, 350, 205), (602, 265, 155)]:
    teal = (104, 222, 204, alpha)
    d.line((286, y+8, 307, y+29, 346, y-20), fill=teal, width=24, joint="curve")
    d.rounded_rectangle((390, y-12, 390+width, y+8), radius=10, fill=(241, 247, 245, alpha))

# Stand and base.
d.rounded_rectangle((466, 694, 558, 782), radius=22, fill=(114, 147, 151))
d.rounded_rectangle((360, 774, 664, 818), radius=18, fill=(221, 235, 231))

# Verification seal overlaps the lower-right edge.
d.ellipse((596, 590, 922, 916), fill=(13, 31, 40), outline=(241, 187, 94), width=28)
d.ellipse((626, 620, 892, 886), fill=(232, 245, 240))
d.line((696, 756, 751, 811, 832, 710), fill=(24, 126, 112), width=35, joint="curve")
d.ellipse((683, 743, 709, 769), fill=(24, 126, 112))
d.ellipse((819, 697, 845, 723), fill=(24, 126, 112))

# Supersampled export keeps the mark crisp at small sizes.
im = im.resize((512, 512), Image.Resampling.LANCZOS)
im.save(OUT / "auditapc-mark.png", optimize=True)
im.save(OUT / "auditapc.ico", format="ICO", sizes=[(16,16),(24,24),(32,32),(48,48),(64,64),(128,128),(256,256)])
print(f"Iconos actualizados en {OUT}")

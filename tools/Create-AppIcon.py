"""Render the original code-defined EffortLess mark; Python standard library only."""
from pathlib import Path
import struct
import zlib

root = Path(__file__).resolve().parents[1] / "src/EffortLess.App/Assets"
root.mkdir(parents=True, exist_ok=True)
svg = '''<svg xmlns="http://www.w3.org/2000/svg" width="256" height="256" viewBox="0 0 256 256">
<rect width="256" height="256" rx="48" fill="#18191c"/>
<circle cx="128" cy="128" r="91" fill="#282b31" stroke="#8596aa" stroke-width="4"/>
<path d="M106 80L184 128L106 176Z" fill="#dadce1"/>
</svg>'''
(root / "EffortLess.svg").write_text(svg, encoding="utf-8")

def color(x, y):
    cx, cy = max(48, min(208, x)), max(48, min(208, y))
    if (x-cx)**2 + (y-cy)**2 > 48**2:
        return (0,0,0,0)
    distance = (x-128)**2 + (y-128)**2
    if 106 <= x <= 184 and abs(y-128) <= 48 * (184-x) / 78:
        return (218,220,225,255)
    if 89**2 <= distance <= 93**2:
        return (133,150,170,255)
    if distance < 89**2:
        return (40,43,49,255)
    return (24,25,28,255)

def png(size):
    raw=bytearray()
    for y in range(size):
        raw.append(0)
        for x in range(size):
            samples=[color((x+(sx+.5)/4)*256/size,(y+(sy+.5)/4)*256/size) for sy in range(4) for sx in range(4)]
            alpha=sum(c[3] for c in samples)
            rgb=[round(sum(c[k]*c[3] for c in samples)/alpha) if alpha else 0 for k in range(3)]
            raw.extend((*rgb, round(alpha/16)))
    def chunk(kind, data):
        return struct.pack('>I',len(data))+kind+data+struct.pack('>I',zlib.crc32(kind+data)&0xffffffff)
    return b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>IIBBBBB',size,size,8,6,0,0,0))+chunk(b'IDAT',zlib.compress(raw,9))+chunk(b'IEND',b'')

sizes=(16,24,32,48,64,128,256)
images=[png(size) for size in sizes]
offset=6+16*len(sizes)
entries=[]
for size,data in zip(sizes,images):
    entries.append(struct.pack('<BBBBHHII',size%256,size%256,0,0,1,32,len(data),offset))
    offset+=len(data)
(root/'EffortLess.ico').write_bytes(struct.pack('<HHH',0,1,len(sizes))+b''.join(entries)+b''.join(images))
(root/'EffortLess.png').write_bytes(images[-1])
print(root/'EffortLess.ico')

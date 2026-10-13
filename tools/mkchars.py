"""
mkchars.py - Il set di caratteri originale di Halls of the Things (1983) in binario.

    python tools/mkchars.py [uscita]    (predefinita: riscos/!Halls/Chars,ffd)

Legge original/halls83_chars.asm (dal RTF di Design Design) e scrive i 2048
byte di chars.bin come li caricava lo Spectrum a #6800: byte 0-255 gli
attributi dei codici #00-#FF, byte 8*c..8*c+7 la bitmap del carattere c
(#20-#FF, riga per riga dall'alto, bit 7 = pixel a sinistra).
"""
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
src = open(os.path.join(ROOT, "original", "halls83_chars.asm"), encoding="latin-1").read().splitlines()

data = bytearray(2048)
attrs = []
code = None
row = 0
for line in src:
    m = re.match(r"^;\s*#([0-9A-Fa-f]{2})\b", line)
    if m:
        code = int(m.group(1), 16)
        row = 0
        continue
    m = re.match(r"^\s*db\s+%([01]{8})", line)
    if m and code is not None:
        data[code * 8 + row] = int(m.group(1), 2)
        row += 1
        continue
    m = re.match(r"^\s*db\s+(#[0-9A-Fa-f]{2}(?:\s*,\s*#[0-9A-Fa-f]{2})*)", line)
    if m and code is None and len(attrs) < 256:
        attrs += [int(x.strip()[1:], 16) for x in m.group(1).split(",")]

assert len(attrs) == 256, len(attrs)
data[0:256] = bytes(attrs)
out = sys.argv[1] if len(sys.argv) > 1 else os.path.join(ROOT, "riscos", "!Halls", "Chars,ffd")
os.makedirs(os.path.dirname(out), exist_ok=True)
open(out, "wb").write(data)
print(out, len(data), "byte")

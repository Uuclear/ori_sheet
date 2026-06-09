import os
import zipfile
from xml.etree import ElementTree as ET

base = r"d:\github\jsscript"
path = None
for f in os.listdir(base):
    if "2" in f and "1" in f and f.endswith(".docx") and not f.startswith("~"):
        path = os.path.join(base, f)
        break

ns = {"w": "http://schemas.openxmlformats.org/wordprocessingml/2006/main"}
W = "{http://schemas.openxmlformats.org/wordprocessingml/2006/main}"


def cell_text(tc):
    texts = []
    for t in tc.iter(f"{W}t"):
        if t.text:
            texts.append(t.text)
        if t.tail:
            texts.append(t.tail)
    return "".join(texts).strip()


with zipfile.ZipFile(path) as z:
    root = ET.fromstring(z.read("word/document.xml"))

tbl = root.find(".//w:tbl", ns)
rows = tbl.findall("w:tr", ns)
print("rows", len(rows))

for ri, tr in enumerate(rows):
    cells = tr.findall("w:tc", ns)
    parts = []
    for ci, tc in enumerate(cells):
        gs = tc.find("w:tcPr/w:gridSpan", ns)
        vm = tc.find("w:tcPr/w:vMerge", ns)
        gs_val = gs.get(f"{W}val") if gs is not None else "1"
        vm_val = vm.get(f"{W}val") if vm is not None else "-"
        parts.append(f'c{ci}(col{gs_val},v{vm_val})="{cell_text(tc)[:50]}"')
    print(f"R{ri+1:02d} [{len(cells)}]: " + " | ".join(parts))

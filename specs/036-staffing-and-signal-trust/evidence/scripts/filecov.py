import glob, sys, xml.etree.ElementTree as ET
d, names = sys.argv[1], sys.argv[2:]
for f in glob.glob(f"{d}/**/coverage.cobertura.xml", recursive=True):
    agg = {}
    for c in ET.parse(f).getroot().iter("class"):
        fn = c.get("filename", "").replace("\\", "/")
        for n in names:
            if fn.endswith(n):
                a = agg.setdefault(n, [0, 0, 0, 0])
                for line in c.iter("line"):
                    a[0] += 1; a[1] += int(line.get("hits", "0")) > 0
                    cond = line.get("condition-coverage")
                    if cond:
                        x, y = cond.split("(")[1].rstrip(")").split("/"); a[2] += int(x); a[3] += int(y)
    for n, (l, lv, bv, br) in agg.items():
        print(f"{n}: lines {lv}/{l} = {lv/l*100:.1f} %, branches {bv}/{br} = {(bv/br*100 if br else 100):.1f} %")

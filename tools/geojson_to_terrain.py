"""
geojson_to_terrain.py
Natural Earth 육지 GeoJSON -> 512x256 바다/육지 격자 PNG (정사각투영).

매핑: lon(-180..180)->x, lat(90..-90)->y  (위가 북쪽)
색상: 바다=파랑, 육지=초록  (1픽셀 = 1셀)

사용: python tools/geojson_to_terrain.py
"""
import json
from pathlib import Path
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parent.parent
SRC = ROOT / "source" / "ne_110m_land.geojson"
OUT = ROOT / "Assets" / "Game" / "Map" / "Authoring" / "world_terrain_512x256.png"

W, H = 512, 256
SEA = (40, 90, 160)    # DeepSea
LAND = (95, 150, 70)   # Land

def lonlat_to_px(lon, lat):
    x = (lon + 180.0) / 360.0 * W
    y = (90.0 - lat) / 180.0 * H
    return (x, y)

def rings(geom):
    """Polygon/MultiPolygon -> [(exterior, [holes...]), ...]"""
    t = geom["type"]
    if t == "Polygon":
        polys = [geom["coordinates"]]
    elif t == "MultiPolygon":
        polys = geom["coordinates"]
    else:
        return []
    out = []
    for poly in polys:
        ext = [lonlat_to_px(x, y) for x, y in poly[0]]
        holes = [[lonlat_to_px(x, y) for x, y in ring] for ring in poly[1:]]
        out.append((ext, holes))
    return out

def main():
    data = json.loads(SRC.read_text(encoding="utf-8"))
    img = Image.new("RGB", (W, H), SEA)
    d = ImageDraw.Draw(img)
    n_poly = 0
    for feat in data["features"]:
        for ext, holes in rings(feat["geometry"]):
            d.polygon(ext, fill=LAND)
            for hole in holes:          # 내해(예: 카스피해) -> 바다로 되돌림
                d.polygon(hole, fill=SEA)
            n_poly += 1
    OUT.parent.mkdir(parents=True, exist_ok=True)
    img.save(OUT)
    # 통계
    px = img.load()
    land = sum(1 for j in range(H) for i in range(W) if px[i, j] == LAND)
    total = W * H
    print(f"polygons drawn : {n_poly}")
    print(f"grid           : {W}x{H} = {total} cells")
    print(f"land cells     : {land} ({land/total*100:.1f}%)")
    print(f"sea cells      : {total-land} ({(total-land)/total*100:.1f}%)")
    print(f"saved          : {OUT.relative_to(ROOT)}")

if __name__ == "__main__":
    main()

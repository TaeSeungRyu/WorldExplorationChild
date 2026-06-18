"""
build_world_data.py
원본 지리 데이터 -> 게임용 격자 이미지 2장 (정사각투영, 1024x512).

입력 (source/):
  - ne_50m_land.geojson           : 정밀 육지/바다 마스크 (해안선)
  - gebco_elev_21600x10800.png    : 통합 고도(해저+육지) 그레이스케일

출력 (Assets/Game/Map/Authoring/):
  - world_terrain_1024x512.png    : 지형 분류 색 (DeepSea/ShallowSea/Land/Mountain)
  - world_height_1024x512.png     : 고도 그레이스케일 (128=해수면, <128 바다깊이, >128 육지높이)

매핑: lon(-180..180)->x, lat(90..-90)->y (행0=북쪽). PNG 좌하단=격자(0,0)=남서.
사용: python tools/build_world_data.py
"""
import json
from pathlib import Path
from PIL import Image, ImageDraw

Image.MAX_IMAGE_PIXELS = None

ROOT = Path(__file__).resolve().parent.parent
LAND_SRC = ROOT / "source" / "ne_50m_land.geojson"
ELEV_SRC = ROOT / "source" / "gebco_elev_21600x10800.png"
OUT_DIR = ROOT / "Assets" / "Game" / "Map" / "Authoring"
OUT_TERRAIN = OUT_DIR / "world_terrain_1024x512.png"
OUT_HEIGHT = OUT_DIR / "world_height_1024x512.png"

W, H = 1024, 512

# 지형 분류 색 (WorldMapImporter의 팔레트와 일치시킬 것)
C_DEEP = (30, 70, 140)
C_SHALLOW = (70, 130, 200)
C_LAND = (95, 150, 70)
C_MOUNTAIN = (120, 110, 95)


def lonlat_to_px(lon, lat):
    return ((lon + 180.0) / 360.0 * W, (90.0 - lat) / 180.0 * H)


def rings(geom):
    t = geom["type"]
    polys = [geom["coordinates"]] if t == "Polygon" else (
        geom["coordinates"] if t == "MultiPolygon" else [])
    out = []
    for poly in polys:
        ext = [lonlat_to_px(x, y) for x, y in poly[0]]
        holes = [[lonlat_to_px(x, y) for x, y in r] for r in poly[1:]]
        out.append((ext, holes))
    return out


def build_land_mask():
    """1=육지, 0=바다 의 1차원 리스트 (index = y*W + x)."""
    data = json.loads(LAND_SRC.read_text(encoding="utf-8"))
    img = Image.new("L", (W, H), 0)
    d = ImageDraw.Draw(img)
    for feat in data["features"]:
        for ext, holes in rings(feat["geometry"]):
            d.polygon(ext, fill=255)
            for hole in holes:
                d.polygon(hole, fill=0)
    px = img.load()
    return [1 if px[i % W, i // W] else 0 for i in range(W * H)]


def load_elevation():
    """GEBCO 고도를 WxH로 리샘플한 0..255 리스트 (index = y*W + x)."""
    im = Image.open(ELEV_SRC).convert("L").resize((W, H), Image.BILINEAR)
    px = im.load()
    return [px[i % W, i // W] for i in range(W * H)]


def pct(sorted_vals, p):
    if not sorted_vals:
        return 0
    return sorted_vals[min(len(sorted_vals) - 1, int(p / 100.0 * (len(sorted_vals) - 1)))]


def build_shallow(land, radius=2):
    """바다 셀 중 육지에서 radius칸 이내인 것 = 얕은바다(대륙붕). index 리스트."""
    shallow = bytearray(W * H)
    for i in range(W * H):
        if not land[i]:
            continue
        x0, y0 = i % W, i // W
        for dy in range(-radius, radius + 1):
            yy = y0 + dy
            if yy < 0 or yy >= H:
                continue
            for dx in range(-radius, radius + 1):
                xx = x0 + dx
                if 0 <= xx < W:
                    j = yy * W + xx
                    if not land[j]:
                        shallow[j] = 1
    return shallow


def main():
    land = build_land_mask()
    elev = load_elevation()  # 육지 고도만(바다=0)
    n = W * H

    # 육지 고도 분포로 정규화 기준 + 산악 임계값 산출
    land_e = sorted(elev[i] for i in range(n) if land[i])
    land_hi = max(1, pct(land_e, 98))      # 최고 고도(이상치 제외)
    mtn_thr = pct(land_e, 82)              # 상위 ~18%를 산악으로
    shallow = build_shallow(land, radius=2)

    terrain_img = Image.new("RGB", (W, H))
    height_img = Image.new("L", (W, H))
    tpx = terrain_img.load()
    hpx = height_img.load()

    counts = {"deep": 0, "shallow": 0, "land": 0, "mountain": 0}
    for i in range(n):
        x, y = i % W, i // W
        e = elev[i]
        if land[i]:
            # 해수면(128) ~ 최고 고도(255)
            t = max(0.0, min(1.0, e / land_hi))
            hpx[x, y] = 128 + int(t * 127)
            if e >= mtn_thr:
                tpx[x, y] = C_MOUNTAIN; counts["mountain"] += 1
            else:
                tpx[x, y] = C_LAND; counts["land"] += 1
        else:
            # 바다는 평평(해수면=128). 깊이 데이터는 없음.
            hpx[x, y] = 128
            if shallow[i]:
                tpx[x, y] = C_SHALLOW; counts["shallow"] += 1
            else:
                tpx[x, y] = C_DEEP; counts["deep"] += 1

    OUT_DIR.mkdir(parents=True, exist_ok=True)
    terrain_img.save(OUT_TERRAIN)
    height_img.save(OUT_HEIGHT)

    print(f"grid            : {W}x{H} = {n} cells")
    print(f"land elev (gray): max(98p)={land_hi}, mountain>= {mtn_thr}")
    print(f"deep sea        : {counts['deep']}")
    print(f"shallow sea     : {counts['shallow']}")
    print(f"land            : {counts['land']}")
    print(f"mountain        : {counts['mountain']}")
    print(f"saved           : {OUT_TERRAIN.relative_to(ROOT)}")
    print(f"saved           : {OUT_HEIGHT.relative_to(ROOT)}")


if __name__ == "__main__":
    main()

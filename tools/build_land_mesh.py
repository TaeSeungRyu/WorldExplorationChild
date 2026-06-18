"""
build_land_mesh.py
ne_50m_land.geojson → 완전 평면 대륙 시트(고도·두께 없음).

해안선 ear-clipping 윗면만. 돌출/옆벽 없음 → 모든 법선 +Y → 균일 음영(둥근 느낌 0).
지형 기복(산 등)은 나중에 별도 프리팹/에셋을 맵 위에 배치하는 방식으로 추가한다.

출력: Assets/Game/Map/land_mesh.bytes
  int32 vCount, int32 triCount, vCount*(float32 x,y,z), triCount*3 int32
사용: python tools/build_land_mesh.py
"""
import json
import struct
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SRC = ROOT / "source" / "ne_50m_land.geojson"
OUT = ROOT / "Assets" / "Game" / "Map" / "land_mesh.bytes"

WORLD_W = 4096.0
WORLD_H = 2048.0
TOP_Y = 0.1       # 육지 시트 높이(바다 -0.05 바로 위, 평면)


def to_world(lon, lat):
    return ((lon + 180.0) / 360.0 * WORLD_W, (lat + 90.0) / 180.0 * WORLD_H)


def signed_area2(pts):
    s = 0.0
    n = len(pts)
    for i in range(n):
        x1, y1 = pts[i]; x2, y2 = pts[(i + 1) % n]
        s += x1 * y2 - x2 * y1
    return s


def cross(ax, ay, bx, by, cx, cy):
    return (bx - ax) * (cy - ay) - (by - ay) * (cx - ax)


def point_in_tri(px, py, ax, ay, bx, by, cx, cy):
    d1 = (px - bx) * (ay - by) - (ax - bx) * (py - by)
    d2 = (px - cx) * (by - cy) - (bx - cx) * (py - cy)
    d3 = (px - ax) * (cy - ay) - (cx - ax) * (py - ay)
    neg = d1 < 0 or d2 < 0 or d3 < 0
    pos = d1 > 0 or d2 > 0 or d3 > 0
    return not (neg and pos)


def ear_clip(pts):
    n = len(pts)
    if n < 3:
        return []
    prev = [(i - 1) % n for i in range(n)]
    nxt = [(i + 1) % n for i in range(n)]
    alive = [True] * n

    def convex(i):
        ax, ay = pts[prev[i]]; bx, by = pts[i]; cx, cy = pts[nxt[i]]
        return cross(ax, ay, bx, by, cx, cy) > 0

    reflex = set(i for i in range(n) if not convex(i))
    tris = []
    remaining = n
    i = 0
    while remaining > 3:
        found = False
        scanned = 0
        while scanned < remaining:
            if alive[i] and i not in reflex:
                pi, ni = prev[i], nxt[i]
                ax, ay = pts[pi]; bx, by = pts[i]; cx, cy = pts[ni]
                ear = True
                for r in reflex:
                    if r == pi or r == i or r == ni:
                        continue
                    rx, ry = pts[r]
                    if point_in_tri(rx, ry, ax, ay, bx, by, cx, cy):
                        ear = False; break
                if ear:
                    tris.append((pi, i, ni))
                    nxt[pi] = ni; prev[ni] = pi; alive[i] = False
                    remaining -= 1
                    for v in (pi, ni):
                        if convex(v): reflex.discard(v)
                        else: reflex.add(v)
                    i = ni; found = True; break
            i = nxt[i]; scanned += 1
        if not found:
            break
    if remaining == 3:
        a = [k for k in range(n) if alive[k]]
        if len(a) == 3:
            tris.append((a[0], a[1], a[2]))
    return tris


def rings_exterior(geom):
    t = geom["type"]
    polys = [geom["coordinates"]] if t == "Polygon" else (
        geom["coordinates"] if t == "MultiPolygon" else [])
    for poly in polys:
        yield poly[0]


def main():
    t0 = time.time()
    data = json.loads(SRC.read_text(encoding="utf-8"))

    verts = []   # (x,y,z)
    tris = []
    poly_count = 0

    for feat in data["features"]:
        for ring in rings_exterior(feat["geometry"]):
            pts = [to_world(c[0], c[1]) for c in ring]
            if len(pts) > 1 and pts[0] == pts[-1]:
                pts = pts[:-1]
            n = len(pts)
            if n < 3:
                continue
            if signed_area2(pts) < 0:
                pts = pts[::-1]
                n = len(pts)

            top_base = len(verts)
            for (x, z) in pts:
                verts.append((x, TOP_Y, z))       # 평면 시트
            for (a, b, c) in ear_clip(pts):       # 법선 +Y: a,c,b
                tris.extend((top_base + a, top_base + c, top_base + b))
            poly_count += 1

    OUT.parent.mkdir(parents=True, exist_ok=True)
    with open(OUT, "wb") as f:
        f.write(struct.pack("<i", len(verts)))
        f.write(struct.pack("<i", len(tris) // 3))
        for (x, y, z) in verts:
            f.write(struct.pack("<fff", x, y, z))
        f.write(struct.pack("<%di" % len(tris), *tris))

    print(f"polygons   : {poly_count}")
    print(f"vertices   : {len(verts)}")
    print(f"triangles  : {len(tris)//3}")
    print(f"(flat sheet, no extrude)")
    print(f"file size  : {OUT.stat().st_size/1024:.0f} KB")
    print(f"elapsed    : {time.time()-t0:.1f}s")
    print(f"saved      : {OUT.relative_to(ROOT)}")


if __name__ == "__main__":
    main()

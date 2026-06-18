"""
build_land_mesh.py
ne_110m_land.geojson → 저폴리(low-poly) 입체 대륙 메시.

방법:
  1) 외곽 링 ear-clipping
  2) longest-edge bisection 으로 청크감 있는 패싯(THRESH)까지 세분 (틈 없음)
  3) world_height 고도 샘플링 → y 솟음 (저폴리 산)
  4) **플랫 셰이딩**: 면마다 정점을 분리해 RecalculateNormals 시 면별 법선 → 각진 패싯
  5) 면 색 = 고도+위도 바이옴 submesh (저지대 초록 / 고지대 갈색 / 봉우리·극지 흰색 / 절벽)

출력: Assets/Game/Map/land_mesh.bytes
  int32 vCount, int32 subCount, subCount*int32 triCount,
  vCount*(float32 x,y,z), 서브메시별 triCount*3 int32
사용: python tools/build_land_mesh.py
"""
import json
import struct
import time
from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
SRC = ROOT / "source" / "ne_110m_land.geojson"
HEIGHT_PNG = ROOT / "Assets" / "Game" / "Map" / "Authoring" / "world_height_4096x2048.png"
OUT = ROOT / "Assets" / "Game" / "Map" / "land_mesh.bytes"

WORLD_W = 4096.0
WORLD_H = 2048.0
THRESH = 42.0        # 패싯 크기(월드 유닛). 클수록 청크한 저폴리, 작을수록 매끈·무거움
HEIGHT_SCALE = 16.0  # 최고 육지 고도가 솟는 높이(완만할수록 면 음영 차분)
BASE_TOP = 1.0       # 해수면 육지 윗면 y
BOTTOM_Y = -2.0      # 옆벽 바닥

_himg = Image.open(HEIGHT_PNG).convert("L")
_hpx = _himg.load()
_HW, _HH = _himg.size


def sample(x, z):
    """world (x,z) → (y, landNorm 0..1)."""
    col = min(_HW - 1, max(0, int(x / WORLD_W * _HW)))
    row = min(_HH - 1, max(0, int((1.0 - z / WORLD_H) * _HH)))
    h = _hpx[col, row]
    norm = max(0.0, (h - 128) / 127.0)
    return BASE_TOP + norm * HEIGHT_SCALE, norm


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


def ekey(a, b):
    return (a, b) if a < b else (b, a)


def refine(verts2d, tris):
    """longest-edge bisection(conforming). verts2d 직접 확장. (tris, boundary_edges) 반환."""
    tri = {i: list(t) for i, t in enumerate(tris)}
    nextid = len(tris)
    edge2 = {}
    for tid, t in tri.items():
        for s in range(3):
            edge2.setdefault(ekey(t[s], t[(s + 1) % 3]), set()).add(tid)
    mid = {}

    def get_mid(a, b):
        k = ekey(a, b)
        if k in mid:
            return mid[k]
        ax, az = verts2d[a]; bx, bz = verts2d[b]
        verts2d.append(((ax + bx) * 0.5, (az + bz) * 0.5))
        mid[k] = len(verts2d) - 1
        return mid[k]

    def add_tri(a, b, c):
        nonlocal nextid
        tid = nextid; nextid += 1
        tri[tid] = [a, b, c]
        for s in range(3):
            edge2.setdefault(ekey([a, b, c][s], [a, b, c][(s + 1) % 3]), set()).add(tid)
        return tid

    def del_tri(tid):
        t = tri.pop(tid)
        for s in range(3):
            edge2[ekey(t[s], t[(s + 1) % 3])].discard(tid)

    def longest(tid):
        t = tri[tid]; best, bl = 0, -1.0
        for s in range(3):
            ax, az = verts2d[t[s]]; bx, bz = verts2d[t[(s + 1) % 3]]
            d = (ax - bx) ** 2 + (az - bz) ** 2
            if d > bl:
                bl = d; best = s
        return best, bl

    def eidx(tid, a, b):
        t = tri[tid]; kk = ekey(a, b)
        for s in range(3):
            if ekey(t[s], t[(s + 1) % 3]) == kk:
                return s
        return -1

    def bisect(tid, s, m):
        t = tri[tid]
        i, j, k = t[s], t[(s + 1) % 3], t[(s + 2) % 3]
        del_tri(tid)
        return [add_tri(i, m, k), add_tri(m, j, k)]

    thr2 = THRESH * THRESH
    queue = list(tri.keys())
    while queue:
        tid = queue.pop()
        if tid not in tri:
            continue
        s, l2 = longest(tid)
        if l2 <= thr2:
            continue
        a, b = tri[tid][s], tri[tid][(s + 1) % 3]
        nbrs = [x for x in edge2.get(ekey(a, b), ()) if x != tid and x in tri]
        n = nbrs[0] if nbrs else None
        if n is not None:
            sn, _ = longest(n)
            an, bn = tri[n][sn], tri[n][(sn + 1) % 3]
            if ekey(an, bn) != ekey(a, b):
                queue.append(tid); queue.append(n); continue
        m = get_mid(a, b)
        queue.extend(bisect(tid, s, m))
        if n is not None:
            queue.extend(bisect(n, eidx(n, a, b), m))

    boundary = [k for k, ts in edge2.items() if len(ts) == 1]
    return list(tri.values()), boundary


def rings_exterior(geom):
    t = geom["type"]
    polys = [geom["coordinates"]] if t == "Polygon" else (
        geom["coordinates"] if t == "MultiPolygon" else [])
    for poly in polys:
        yield poly[0]


def biome_face(lat, norm):
    """면 색 submesh: 0 저지대(초록) 1 고지대(갈색) 2 봉우리·극지(흰색)."""
    if abs(lat) >= 66.0:
        return 2
    if norm >= 0.50:
        return 2
    if norm >= 0.26:
        return 1
    return 0


def main():
    t0 = time.time()
    data = json.loads(SRC.read_text(encoding="utf-8"))

    gverts = []                  # (x,y,z) — 면마다 분리(플랫 셰이딩)
    sub = [[], [], [], []]       # 0 저지대 1 고지대 2 봉우리/극지 3 절벽
    poly_count = 0

    def emit(sm, p0, p1, p2):
        b = len(gverts)
        gverts.append(p0); gverts.append(p1); gverts.append(p2)
        sub[sm].extend((b, b + 1, b + 2))

    for feat in data["features"]:
        for ring in rings_exterior(feat["geometry"]):
            pts = [to_world(c[0], c[1]) for c in ring]
            if len(pts) > 1 and pts[0] == pts[-1]:
                pts = pts[:-1]
            if len(pts) < 3:
                continue
            if signed_area2(pts) < 0:
                pts = pts[::-1]

            verts2d = [(x, z) for (x, z) in pts]
            base_tris = ear_clip(verts2d)
            tris2d, boundary = refine(verts2d, base_tris)

            # 정점별 (y, norm) 캐시
            vy = [sample(x, z) for (x, z) in verts2d]

            # 윗면 — 면마다 정점 분리(플랫), 면 색 = 고도+위도
            for (a, b, c) in tris2d:
                ax, az = verts2d[a]; bx, bz = verts2d[b]; cx, cz = verts2d[c]
                cz_mid = (az + bz + cz) / 3.0
                lat = cz_mid / WORLD_H * 180.0 - 90.0
                norm = (vy[a][1] + vy[b][1] + vy[c][1]) / 3.0
                sm = biome_face(lat, norm)
                # 법선 +Y 와인딩: a,c,b
                emit(sm,
                     (ax, vy[a][0], az),
                     (cx, vy[c][0], cz),
                     (bx, vy[b][0], bz))

            # 옆벽(절벽) — 경계 가장자리마다, 플랫
            for (a, b) in boundary:
                ax, az = verts2d[a]; bx, bz = verts2d[b]
                ya = vy[a][0]; yb = vy[b][0]
                ta = (ax, ya, az); tb = (bx, yb, bz)
                ba = (ax, BOTTOM_Y, az); bb = (bx, BOTTOM_Y, bz)
                emit(3, ba, ta, tb)
                emit(3, ba, tb, bb)
            poly_count += 1

    OUT.parent.mkdir(parents=True, exist_ok=True)
    with open(OUT, "wb") as f:
        f.write(struct.pack("<i", len(gverts)))
        f.write(struct.pack("<i", len(sub)))
        for s in sub:
            f.write(struct.pack("<i", len(s) // 3))
        for (x, y, z) in gverts:
            f.write(struct.pack("<fff", x, y, z))
        for s in sub:
            if s:
                f.write(struct.pack("<%di" % len(s), *s))

    total = sum(len(s) for s in sub) // 3
    names = ["lowland", "highland", "peak/polar", "cliff"]
    print(f"polygons   : {poly_count}")
    print(f"vertices   : {len(gverts)}")
    print(f"triangles  : {total}")
    for nm, s in zip(names, sub):
        print(f"  {nm:11}: {len(s)//3}")
    print(f"THRESH {THRESH}, height {HEIGHT_SCALE}")
    print(f"file size  : {OUT.stat().st_size/1024:.0f} KB")
    print(f"elapsed    : {time.time()-t0:.1f}s")
    print(f"saved      : {OUT.relative_to(ROOT)}")


if __name__ == "__main__":
    main()

"""
world_lowpoly_blender.py  —  Blender에서 실행하는 저폴리 월드맵 생성 스크립트.

입력 : source/blender_heightmask_512x256.png  (바다=0, 육지=고도, 회색조)
출력 : Assets/Game/Map/WorldLowPoly.fbx        (저폴리 대륙 + 바다 평면)

실행 방법 (둘 중 하나):
  A) GUI: Blender 열기 → Scripting 탭 → Open → 이 파일 → ▶ Run Script
  B) 헤드리스(터미널):
     "<Blender경로>/blender" --background --python tools/world_lowpoly_blender.py
     예) "C:/Program Files/Blender Foundation/Blender 4.2/blender.exe" --background --python tools/world_lowpoly_blender.py

흐름: 격자 → 하이트마스크로 디스플레이스 → 바다면 삭제 → Decimate(저폴리) →
      플랫 셰이딩 → 고도색 머티리얼(저/중/고) → 큰 크기 → FBX.
* 정확한 지도가 아니라 "저폴리 월드맵 느낌". 크기(MAP_W)는 넓게.
"""
import bpy, bmesh, math, os

# ===== 설정 (경로는 절대경로로) =====
PROJECT    = os.path.dirname(os.path.dirname(os.path.abspath(__file__))) \
             if "__file__" in globals() else r"d:/unity-editor/workspace/WorldExplorationChild"
HEIGHT_IMG = os.path.join(PROJECT, "source", "blender_heightmask_512x256.png")
OUT_FBX    = os.path.join(PROJECT, "Assets", "Game", "Map", "WorldLowPoly.fbx")

GRID_X, GRID_Y = 240, 120     # 격자 분할(저폴리 베이스)
MAP_W, MAP_H   = 1200.0, 600.0  # 맵 크기(월드유닛) — 크게 = 넓은 플레이 공간
HEIGHT         = 45.0         # 산 높이
SEA_CUT        = 0.06         # 이 값 이하 면 = 바다 → 삭제
DECIMATE_RATIO = 0.30         # 저폴리화(낮을수록 면 적음/청크)

C_LOW  = (0.36, 0.60, 0.31, 1)
C_MID  = (0.47, 0.39, 0.27, 1)
C_HIGH = (0.94, 0.96, 0.98, 1)
C_SEA  = (0.13, 0.34, 0.58, 1)


def mat(name, rgba):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = False
    m.diffuse_color = rgba
    return m


def remove(name):
    o = bpy.data.objects.get(name)
    if o:
        bpy.data.objects.remove(o, do_unlink=True)


def main():
    remove("WorldLowPoly")
    remove("WorldSea")

    img = bpy.data.images.load(HEIGHT_IMG, check_existing=True)
    w, h = img.size
    px = list(img.pixels)  # RGBA flat (작은 이미지라 OK)

    def s(u, v):
        ix = min(w - 1, max(0, int(u * w)))
        iy = min(h - 1, max(0, int(v * h)))
        return px[(iy * w + ix) * 4]

    # 1) 격자 + 디스플레이스
    bpy.ops.mesh.primitive_grid_add(x_subdivisions=GRID_X, y_subdivisions=GRID_Y, size=1.0)
    obj = bpy.context.active_object
    obj.name = "WorldLowPoly"
    me = obj.data
    for vert in me.vertices:
        u = vert.co.x + 0.5
        t = vert.co.y + 0.5
        vert.co.x = u * MAP_W
        vert.co.y = t * MAP_H
        vert.co.z = s(u, t) * HEIGHT

    # 2) 바다면 삭제 + 외톨이 정점 제거
    bm = bmesh.new(); bm.from_mesh(me)
    dead = [f for f in bm.faces
            if s((sum(v.co.x for v in f.verts) / len(f.verts)) / MAP_W,
                 (sum(v.co.y for v in f.verts) / len(f.verts)) / MAP_H) < SEA_CUT]
    bmesh.ops.delete(bm, geom=dead, context='FACES')
    loose = [v for v in bm.verts if not v.link_faces]
    if loose:
        bmesh.ops.delete(bm, geom=loose, context='VERTS')
    bm.to_mesh(me); bm.free()

    # 3) Decimate → 저폴리
    md = obj.modifiers.new("dec", "DECIMATE")
    md.decimate_type = 'COLLAPSE'
    md.ratio = DECIMATE_RATIO
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.modifier_apply(modifier=md.name)

    # 4) 플랫 셰이딩
    bpy.ops.object.shade_flat()

    # 5) 고도색 머티리얼 3개 + 면별 배정
    mats = [mat("LP_Low", C_LOW), mat("LP_Mid", C_MID), mat("LP_High", C_HIGH)]
    me.materials.clear()
    for m in mats:
        me.materials.append(m)
    for poly in me.polygons:
        z = poly.center.z
        poly.material_index = 2 if z > HEIGHT * 0.55 else (1 if z > HEIGHT * 0.22 else 0)
    me.update()

    # 6) 바다 평면
    bpy.ops.mesh.primitive_plane_add(size=1.0)
    sea = bpy.context.active_object
    sea.name = "WorldSea"
    sea.scale = (MAP_W, MAP_H, 1.0)
    sea.location = (MAP_W * 0.5, MAP_H * 0.5, -0.3)
    sea.data.materials.append(mat("LP_Sea", C_SEA))

    # 7) FBX 내보내기
    os.makedirs(os.path.dirname(OUT_FBX), exist_ok=True)
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True); sea.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.fbx(filepath=OUT_FBX, use_selection=True,
                             object_types={'MESH'}, mesh_smooth_type='FACE',
                             apply_unit_scale=True)
    print("[world_lowpoly] Exported:", OUT_FBX,
          "| land tris:", len(me.polygons))


main()

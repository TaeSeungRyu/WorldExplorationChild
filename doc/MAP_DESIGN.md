# 맵 데이터 구조 설계 (World Map Data Design)

> 대항해시대류 세계 여행 게임의 맵 토대 설계.
> **결정된 전제**: 3D 평면 + 탑다운 카메라 / 논리 격자(Grid) 데이터와 3D 비주얼 분리.
>
> 이 문서는 **데이터 구조 설계만** 다룬다. 실제 C# 구현, 씬 배치, 이동 컨트롤러, 도시/전투 시스템은 후속 작업이다.

---

## 0. 핵심 원칙

1. **논리(데이터)와 비주얼(3D)을 분리한다.** 이동/통행/길찾기는 격자 데이터를 조회하고, 3D 지형 메시는 그 격자를 "보여주기만" 한다. 물리 콜라이더에 통행 판정을 의존하지 않는다.
2. **맵 데이터는 직렬화 가능한 단일 소스(Single Source of Truth)다.** `WorldMapData` (ScriptableObject) 하나가 진실이다.
3. **맵 위에 올라가는 것들(도시·발견물·NPC)은 맵을 *참조*만 한다.** 격자 좌표(`GridCoord`)로 위치를 가리키며, 맵 데이터 자체에 섞지 않는다. → 레이어 분리.

---

## 1. 좌표계 (Coordinate System)

3D 평면이므로 **격자는 XZ 평면에 깔린다** (Y는 높이/시각효과용).

- `GridCoord { int x; int y; }` — 논리 격자 좌표. (struct, 값 타입)
- 월드 매핑: `grid.x → world.X`, `grid.y → world.Z`.
- 변환 파라미터: `cellSize` (셀 1칸 = 월드 몇 유닛), `origin` (격자 [0,0]의 월드 위치).

```
worldPos = origin + new Vector3(x * cellSize, 0, y * cellSize) + (cellSize/2 중심 보정)
gridCoord = floor((worldPos - origin) / cellSize)
```

변환 함수는 `WorldMapData`에 두어 맵마다 cellSize/origin이 달라도 일관되게 처리한다.

---

## 2. 지형 타입 (TerrainType)

`byte` 기반 enum. 통행 규칙과 직결.

| 값 | 이름 | 배(Ship) | 도보(Land) | 비고 |
|----|------|:---:|:---:|------|
| 0 | `DeepSea` (심해) | ✅ | ❌ | 기본 바다 |
| 1 | `ShallowSea` (연안) | ✅ | ❌ | 항구 접근/암초 표현 여지 |
| 2 | `Land` (평지) | ❌ | ✅ | 육지 |
| 3 | `Mountain` (산악) | ❌ | ❌ | 통행 불가 육지 |
| 4 | `Port` (항구) | ✅ | ✅ | **육지↔바다 접점.** 도시 진입 지점 |

> 최소 MVP는 `DeepSea / Land / Port` 3종으로도 시작 가능. enum은 확장 여지를 두되 초기엔 위 5종 권장.

핵심 규칙: **배는 바다 계열(+항구)만, 도보는 육지 계열(+항구)만.** 항구만이 두 모드의 환승 지점이다.

---

## 3. 이동 모드 (MoveMode)

```
enum MoveMode { Ship, Land }
```

통행 판정은 `(TerrainType, MoveMode)` 한 쌍으로 결정된다 → `WorldMapData.IsNavigable(GridCoord, MoveMode)`.
이 한 함수가 "배는 육지 못 감 / 육지는 배 못 감"이라는 핵심 요구사항을 책임진다.

---

## 4. 셀 저장 구조 (Cell Storage)

대형 월드(예: 512×256 = 131,072셀)도 가볍게 다루기 위해 **1D 평면 배열 + 레이어 분리**를 쓴다.

```
class WorldMapData : ScriptableObject
{
    int width;
    int height;
    float cellSize;     // 월드 유닛/셀
    Vector3 origin;     // 격자 [0,0]의 월드 위치

    byte[] terrain;     // length = width*height, index = y*width + x  (TerrainType)
    // (확장) byte[] nationOwner;  // 셀별 소속 국가 id, 0=무소속
}
```

- 인덱싱: `index = y * width + x`. 셀당 1바이트 → 13만 셀도 ~128KB.
- "셀별 추가 정보(국가 소속 등)"는 **별도 평행 배열**로 둔다. terrain 배열에 비트를 섞지 않는다(가독성·확장성).
- 도시/발견물/NPC 같은 **희소(sparse) 데이터는 배열이 아니라 별도 리스트/SO**로 (5번 참조).

### 제공 API (설계 시점의 의도)
- `TerrainType GetTerrain(int x, int y)` / `GetTerrain(GridCoord)`
- `bool InBounds(GridCoord)`
- `bool IsNavigable(GridCoord, MoveMode)`
- `Vector3 GridToWorld(GridCoord)` / `GridCoord WorldToGrid(Vector3)`

---

## 5. 맵 위 엔티티 레이어 (이번 범위 밖 — 인터페이스만 예고)

맵 데이터와 **분리된** 별도 에셋으로 관리. 모두 `GridCoord`로 맵을 참조.

- `CityData` (ScriptableObject): 이름, 소속 국가, 항구 `GridCoord`, 특산물 목록, 의뢰 목록 → *무역/의뢰 시스템 토대*
- `NationData`: 국가 이름, 색상, 외교/명성 관계
- `DiscoveryData`: 주요 발견물 이름, 위치/조건 → *발견 시스템 토대*
- NPC 스폰 존(산적/해적): 영역(격자 범위) 정의 → *전투·명성 시스템 토대*

> 이번 작업에선 만들지 않는다. 다만 "맵은 좌표만 제공하고 이들은 맵을 참조한다"는 경계를 확정해 둔다.

---

## 6. 맵 오소링 워크플로우 (Authoring)

대형 월드를 손으로 셀마다 찍는 건 비현실적 → **색상 맵 PNG → `WorldMapData` 변환** 방식 권장.

- 이미지 편집기에서 픽셀 색으로 지형을 칠한다: 파랑=DeepSea, 하늘=ShallowSea, 초록=Land, 회색=Mountain, 노랑=Port.
- 에디터 도구 `WorldMapImporter`가 PNG를 읽어 `terrain[]`을 채운다 (1픽셀 = 1셀).
- 장점: 디자이너가 비주얼하게 작업, 버전 관리 용이, 빠른 반복.

> MVP 단계에선 소형 맵을 코드/인스펙터로 직접 채워 검증한 뒤 임포터를 붙여도 된다.

---

## 7. 폴더 구조 (제안)

워크스페이스를 깔끔하게 유지하기 위한 경계. **맵 관련 모든 코드는 `Assets/Scripts/Map/` 아래로.**

```
doc/                              ← 모든 문서 (Assets 밖)
├─ MAP_DESIGN.md                  ← (현재 문서)
└─ DATA_SOURCES.md

source/                           ← 원본 지리 데이터 (Assets 밖, 빌드 비포함)
└─ ne_110m_*.geojson

tools/                            ← 데이터 변환 스크립트 (Assets 밖)
└─ geojson_to_terrain.py          ← GeoJSON → 512x256 PNG

Assets/Scripts/Map/               ← 코드
├─ Data/                          ← 순수 데이터 타입
│  ├─ TerrainType.cs   [완료]
│  ├─ MoveMode.cs      [완료]
│  ├─ GridCoord.cs     [완료]
│  └─ WorldMapData.cs  [완료]  ← ScriptableObject (맵의 진실)
├─ Runtime/                       ← 런타임 서비스 (후속: 이동/길찾기/렌더)
└─ Editor/
   └─ WorldMapImporter.cs [완료]  ← PNG → WorldMapData.asset

Assets/Game/Map/                  ← 데이터 에셋
├─ Authoring/world_terrain_512x256.png  ← 변환된 칸 이미지 (임포터 입력)
└─ WorldMapData.asset             ← 임포터 실행 시 생성 (게임이 읽는 최종 데이터)
```

> assembly definition(`.asmdef`)으로 `Map` / `Map.Editor`를 분리하면 컴파일 격리·테스트가 깔끔하다. (선택, 후속 판단)

---

## 8. 후속 작업 순서 (참고 — 이번엔 진행 안 함)

1. **(다음)** 위 `Data/` 4개 타입 C# 구현 + 작은 테스트 맵 SO 1개 생성
2. 격자↔3D 비주얼 연결 (탑다운 카메라 + 지형 표시)
3. 배 오브젝트 이동 + `IsNavigable` 통행 판정 (= 첫 "동작하는 맵" MVP)
4. 항구/도시 진입, A* 자동 항해
5. 이후 의뢰·무역·전투·명성·동료 시스템 (각각 맵을 *참조*)

---

### 확인 필요 (구현 착수 전)
- 지형 타입 5종 구성에 동의하는지 (MVP는 3종으로 축소 가능)
- `cellSize` 기본값과 첫 테스트 맵 크기 (예: 64×64로 시작 권장)
- 좌표 매핑을 XZ 평면(권장)으로 확정하는지

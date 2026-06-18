# 맵 원본 데이터 출처 (Data Sources)

세계지도 격자(`WorldMapData`)를 만들기 위한 원본 지리 데이터의 출처·라이선스·위치 기록.

## 보관 위치
- **원본 데이터**: 루트 `source/` (Assets 밖 — 빌드 비포함, 용량 큼)
- **변환 결과(PNG, 로직용)**: `Assets/Game/Map/Authoring/`
- **최종 로직 데이터**: `Assets/Game/Map/WorldMapData.asset`
- **맵 비주얼 이미지**: `Assets/Game/Map/EARTH.jpg` (2048×1024 자연색·음영기복 실사 지도). HorizontalChild 프로젝트에서 가져옴. NASA/Natural Earth 계열 공개 이미지. **화면 표시는 이 이미지**가 담당하고, 격자(WorldMapData)는 로직 전용.

## 다운로드한 원본

| 파일 | 내용 | 용도 |
|------|------|------|
| `source/ne_50m_land.geojson` | 정밀 육지 폴리곤 1420개 (1:50m) | **현재** 바다/육지 마스크 (해안선) |
| `source/gebco_elev_21600x10800.png` | 통합 고도 그레이스케일 (육지 고도, 바다=0) | **현재** 고도(height) 레이어 |
| `source/ne_110m_admin_0_countries.geojson` | 국가 경계 177개 (1:110m) | nationOwner 레이어 (후속) |
| `source/ne_110m_populated_places.geojson` | 주요 도시 243개 (이름·위경도) | CityData 배치 (후속) |
| `source/ne_110m_land.geojson` | 저정밀 육지 (1:110m) | 초기 프로토타입(현재 미사용) |

## 출처 및 라이선스
- **Natural Earth** (naturalearthdata.com, 미러 github.com/nvkelso/natural-earth-vector)
  - 좌표계 CRS84(경위도), 라이선스 **퍼블릭 도메인** — 상업 이용 자유(출처 표기 권장).
- **GEBCO 통합 고도 PNG** — NASA Visible Earth(eoimages.gsfc.nasa.gov) 제공 GEBCO 기반 그레이스케일.
  - 21600×10800, 8-bit. **육지 고도만** 담고 바다는 0(평평). 항해 게임엔 육지 기복만 필요하므로 적합.
  - GEBCO/NASA 데이터, 공개 이용 가능(GEBCO 출처 표기 권장).

## 변환 방식 (요약)
경도(-180~180)→x, 위도(-90~90)→y 선형 매핑 → **4096×2048** 격자로 래스터화.
`tools/build_world_data.py`가 두 장의 PNG를 생성한다:
- `world_terrain_4096x2048.png` — 지형 분류색(심해/얕은바다/육지/산악/빙하). 위도 -60° 이하 육지는 빙하(흰색).
- `world_height_4096x2048.png` — 고도 그레이스케일(**128=해수면**, >128 육지높이)

상세는 [MAP_DESIGN.md](MAP_DESIGN.md) 참조.

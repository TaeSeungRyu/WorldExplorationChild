# 맵 원본 데이터 출처 (Data Sources)

세계지도 격자(`WorldMapData`)를 만들기 위한 원본 지리 데이터의 출처·라이선스·위치 기록.

## 보관 위치
- **원본 데이터**: 루트 `source/` (Assets 밖 — 빌드 비포함, 용량 큼)
- **변환 결과(PNG)**: `Assets/Game/Map/Authoring/`
- **최종 런타임 데이터**: `Assets/Game/Map/WorldMapData.asset`

## 다운로드한 원본

| 파일 | 내용 | 용도 |
|------|------|------|
| `source/ne_110m_land.geojson` | 육지 영역 폴리곤 | 바다/육지 마스크 (terrain 기본) |
| `source/ne_110m_admin_0_countries.geojson` | 국가 경계 177개 | nationOwner 레이어 (후속) |
| `source/ne_110m_populated_places.geojson` | 주요 도시 243개 (이름·위경도) | CityData 배치 (후속) |

## 출처 및 라이선스
- **제공처**: Natural Earth (naturalearthdata.com), 미러: github.com/nvkelso/natural-earth-vector
- **스케일**: 1:110m (저해상, 첫 작업용)
- **좌표계**: CRS84 (경도/위도) — 정사각투영(equirectangular) 변환에 적합
- **라이선스**: **퍼블릭 도메인** — 상업적 이용 포함 자유 (출처 표기 권장, 의무 아님)

## 변환 방식 (요약)
경도(-180~180)→x, 위도(-90~90)→y 선형 매핑 → 512×256 격자로 래스터화 → 셀별 바다/육지 판정.
상세는 [MAP_DESIGN.md](MAP_DESIGN.md) 참조.

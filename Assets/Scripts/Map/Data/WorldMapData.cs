using UnityEngine;

namespace WorldExploration.Map
{
    /// <summary>
    /// 세계 맵의 유일한 진실(Single Source of Truth).
    /// 격자 셀별 지형(<see cref="TerrainType"/>)과 격자↔월드 변환 파라미터만 담는다.
    /// 도시·발견물·NPC 등은 여기에 넣지 않고 GridCoord로 이 데이터를 *참조*만 한다.
    /// </summary>
    [CreateAssetMenu(fileName = "WorldMapData", menuName = "World Exploration/World Map Data")]
    public class WorldMapData : ScriptableObject
    {
        [Header("격자 크기")]
        [SerializeField] private int width;
        [SerializeField] private int height;

        [Header("격자 ↔ 월드 (3D 평면: x→X, y→Z)")]
        [Tooltip("셀 1칸의 월드 유닛 크기")]
        [SerializeField] private float cellSize = 1f;
        [Tooltip("격자 (0,0) 셀의 월드 기준 위치")]
        [SerializeField] private Vector3 origin = Vector3.zero;

        // 셀당 1바이트(TerrainType). index = y * width + x. 인스펙터에서 숨김(대용량).
        [SerializeField, HideInInspector] private byte[] terrain;

        public int Width => width;
        public int Height => height;
        public float CellSize => cellSize;
        public Vector3 Origin => origin;
        public int CellCount => width * height;

        /// <summary>임포터/생성기에서 격자 데이터를 채울 때 호출.</summary>
        public void Initialize(int width, int height, byte[] terrain, float cellSize, Vector3 origin)
        {
            if (terrain == null || terrain.Length != width * height)
                throw new System.ArgumentException(
                    $"terrain 길이({terrain?.Length ?? 0})가 width*height({width * height})와 다릅니다.");

            this.width = width;
            this.height = height;
            this.terrain = terrain;
            this.cellSize = cellSize;
            this.origin = origin;
        }

        public bool InBounds(int x, int y) => x >= 0 && x < width && y >= 0 && y < height;
        public bool InBounds(GridCoord c) => InBounds(c.x, c.y);

        public TerrainType GetTerrain(int x, int y)
        {
            if (!InBounds(x, y))
                throw new System.IndexOutOfRangeException($"격자 범위를 벗어남: ({x}, {y})");
            return (TerrainType)terrain[y * width + x];
        }

        public TerrainType GetTerrain(GridCoord c) => GetTerrain(c.x, c.y);

        /// <summary>
        /// 해당 셀을 주어진 이동 수단으로 통행할 수 있는지. 범위 밖은 false.
        /// 핵심 요구사항("배는 바다만 / 도보는 육지만 / 항구는 둘 다")을 책임지는 단일 함수.
        /// </summary>
        public bool IsNavigable(GridCoord c, MoveMode mode)
        {
            if (!InBounds(c)) return false;
            TerrainType t = GetTerrain(c);
            switch (mode)
            {
                case MoveMode.Ship:
                    return t == TerrainType.DeepSea || t == TerrainType.ShallowSea || t == TerrainType.Port;
                case MoveMode.Land:
                    return t == TerrainType.Land || t == TerrainType.Port;
                default:
                    return false;
            }
        }

        /// <summary>격자 좌표 → 셀 중심의 월드 위치 (XZ 평면).</summary>
        public Vector3 GridToWorld(GridCoord c)
        {
            return origin + new Vector3((c.x + 0.5f) * cellSize, 0f, (c.y + 0.5f) * cellSize);
        }

        /// <summary>월드 위치 → 격자 좌표 (범위 보장 안 함, InBounds로 확인할 것).</summary>
        public GridCoord WorldToGrid(Vector3 world)
        {
            int gx = Mathf.FloorToInt((world.x - origin.x) / cellSize);
            int gy = Mathf.FloorToInt((world.z - origin.z) / cellSize);
            return new GridCoord(gx, gy);
        }
    }
}

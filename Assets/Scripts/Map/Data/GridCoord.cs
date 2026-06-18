using System;

namespace WorldExploration.Map
{
    /// <summary>
    /// 논리 격자 좌표. (0,0)은 맵의 남서쪽(경도 -180, 위도 -90), x는 동쪽·y는 북쪽으로 증가.
    /// 월드 변환은 <see cref="WorldMapData.GridToWorld"/> / <see cref="WorldMapData.WorldToGrid"/> 참조.
    /// </summary>
    [Serializable]
    public struct GridCoord : IEquatable<GridCoord>
    {
        public int x;
        public int y;

        public GridCoord(int x, int y)
        {
            this.x = x;
            this.y = y;
        }

        public bool Equals(GridCoord other) => x == other.x && y == other.y;
        public override bool Equals(object obj) => obj is GridCoord other && Equals(other);
        public override int GetHashCode() => (x * 397) ^ y;
        public override string ToString() => $"({x}, {y})";

        public static bool operator ==(GridCoord a, GridCoord b) => a.Equals(b);
        public static bool operator !=(GridCoord a, GridCoord b) => !a.Equals(b);
    }
}

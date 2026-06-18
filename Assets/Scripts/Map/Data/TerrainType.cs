namespace WorldExploration.Map
{
    /// <summary>
    /// 셀 하나의 지형 타입. byte 기반으로 <see cref="WorldMapData"/>의 terrain 배열에 저장된다.
    /// 통행 규칙: 배는 바다 계열(+항구)만, 도보는 육지 계열(+항구)만. 항구가 유일한 환승 지점.
    /// </summary>
    public enum TerrainType : byte
    {
        DeepSea = 0,    // 심해 — 배만
        ShallowSea = 1, // 연안 — 배만
        Land = 2,       // 평지 — 도보만
        Mountain = 3,   // 산악 — 통행 불가
        Port = 4,       // 항구 — 배/도보 모두 (육지↔바다 접점)
    }
}

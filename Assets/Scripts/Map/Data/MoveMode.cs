namespace WorldExploration.Map
{
    /// <summary>
    /// 이동 수단. 통행 판정(<see cref="WorldMapData.IsNavigable"/>)의 입력.
    /// </summary>
    public enum MoveMode
    {
        Ship, // 배 — 바다 계열 + 항구
        Land, // 도보 — 육지 + 항구
    }
}

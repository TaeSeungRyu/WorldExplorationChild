using UnityEngine;

namespace WorldExploration.Map
{
    /// <summary>
    /// 맵을 비스듬히 내려다보는 카메라 정의. 카메라 GameObject에 붙인다.
    /// tilt(기울기), 투영(직교/원근), 줌을 인스펙터에서 조절하면 에디터에서 즉시 반영된다.
    /// 항상 맵 중심을 바라보고 전체가 화면에 들어오도록 거리/사이즈를 자동 계산한다.
    /// tilt=90이면 수직 탑다운, 낮출수록 입체적인 부감 시점.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Camera))]
    public class MapCameraRig : MonoBehaviour
    {
        [Tooltip("바라볼 맵 데이터(범위 계산용).")]
        [SerializeField] private WorldMapData map;

        [Header("시점")]
        [Tooltip("수평선 기준 내려다보는 각도(도). 90=수직 탑다운, 낮을수록 입체적.")]
        [Range(15f, 90f)] [SerializeField] private float tiltAngle = 55f;

        [Tooltip("원근 투영 사용(입체감↑). 끄면 직교(왜곡 없는 부감).")]
        [SerializeField] private bool perspective = true;

        [Tooltip("원근일 때 시야각(FOV).")]
        [Range(20f, 80f)] [SerializeField] private float fieldOfView = 40f;

        [Tooltip("맵을 화면에 담을 때 여백 배율(1=딱 맞게).")]
        [Range(1f, 2f)] [SerializeField] private float padding = 1.1f;

        public WorldMapData Map { get => map; set { map = value; Apply(); } }

        private void OnEnable() => Apply();
        private void OnValidate() => Apply();

        [ContextMenu("Apply")]
        public void Apply()
        {
            if (map == null || map.Width <= 0 || map.Height <= 0) return;

            var cam = GetComponent<Camera>();
            float worldW = map.Width * map.CellSize;
            float worldH = map.Height * map.CellSize;
            Vector3 center = map.Origin + new Vector3(worldW * 0.5f, 0f, worldH * 0.5f);

            // 맵 평면의 외접원 반지름(여백 포함) — 어떤 각도/화면비에서도 전체가 담기는 보수적 크기
            float radius = 0.5f * Mathf.Sqrt(worldW * worldW + worldH * worldH) * padding;

            Quaternion rot = Quaternion.Euler(Mathf.Clamp(tiltAngle, 15f, 90f), 0f, 0f);
            Vector3 dir = rot * Vector3.forward; // (0, -sin, cos): 아래·북쪽을 향함

            float dist;
            if (perspective)
            {
                cam.orthographic = false;
                cam.fieldOfView = fieldOfView;
                dist = radius / Mathf.Sin(fieldOfView * 0.5f * Mathf.Deg2Rad);
            }
            else
            {
                cam.orthographic = true;
                cam.orthographicSize = radius;
                dist = Mathf.Max(worldW, worldH); // 직교는 거리 무관, 클립 안에만 들도록
            }

            cam.transform.rotation = rot;
            cam.transform.position = center - dir * dist; // 맵 위·남쪽에서 중심을 바라봄
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = dist + radius + 100f;
        }
    }
}

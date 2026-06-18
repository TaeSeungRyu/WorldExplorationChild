using UnityEngine;
using UnityEngine.InputSystem;

namespace WorldExploration.Map
{
    /// <summary>
    /// 맵을 비스듬히 내려다보는 카메라. 카메라 GameObject에 붙인다.
    /// - 시작 시 지정 위경도(기본: 대한민국)를 화면 중심에.
    /// - 드래그(마우스 좌클릭 / 한 손가락)로 팬, 휠 / 핀치로 줌. 둘 다 부드럽게 보간.
    /// - followTarget 지정 시 그 대상(배·캐릭터)을 따라가며 팬 비활성.
    /// - focus는 맵 범위 안으로 자동 클램프.
    /// 입력은 새 Input System(Mouse/Touchscreen low-level).
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Camera))]
    public class MapCameraRig : MonoBehaviour
    {
        [SerializeField] private WorldMapData map;

        [Header("초점 (위경도) — 시작 위치")]
        [SerializeField] private float focusLatitude = 36.3f;   // 대한민국
        [SerializeField] private float focusLongitude = 127.8f;
        [Tooltip("지정 시 이 대상을 따라간다(배·캐릭터). 비우면 자유 팬.")]
        [SerializeField] private Transform followTarget;

        [Header("시점")]
        [Range(20f, 90f)] [SerializeField] private float tiltAngle = 55f;
        [Range(20f, 80f)] [SerializeField] private float fieldOfView = 45f;

        [Header("줌 (세로로 보이는 월드 크기)")]
        [SerializeField] private float viewHeight = 60f;
        [SerializeField] private float minViewHeight = 20f;
        [SerializeField] private float maxViewHeight = 2048f;
        [Range(1.02f, 1.3f)] [SerializeField] private float zoomStep = 1.12f;
        [Range(1f, 20f)] [SerializeField] private float smooth = 10f;

        // 런타임 상태
        private Vector3 _focus;          // 현재 보는 월드 지점(평면상)
        private Vector3 _targetFocus;
        private float _targetViewHeight;
        private bool _initialized;
        private float _pinchPrev;

        public WorldMapData Map { get => map; set { map = value; ResetToFocus(); } }
        public Transform FollowTarget { get => followTarget; set => followTarget = value; }

        private void OnEnable() => ResetToFocus();
        private void OnValidate() { _targetViewHeight = viewHeight; if (_initialized) ApplyImmediate(); else ResetToFocus(); }

        /// <summary>위경도 초점으로 카메라 즉시 배치.</summary>
        public void ResetToFocus()
        {
            if (map == null || map.Width <= 0) return;
            _focus = _targetFocus = ClampToMap(LatLonToWorld(focusLatitude, focusLongitude));
            _targetViewHeight = viewHeight;
            _initialized = true;
            ApplyImmediate();
        }

        private void Update()
        {
            if (map == null || !_initialized) return;

            if (followTarget != null)
            {
                _targetFocus = ClampToMap(followTarget.position);
            }
            else if (Application.isPlaying)
            {
                HandlePan();
            }
            if (Application.isPlaying) HandleZoom();

            // 부드럽게 보간
            float t = 1f - Mathf.Exp(-smooth * Time.unscaledDeltaTime);
            _focus = Vector3.Lerp(_focus, _targetFocus, t);
            viewHeight = Mathf.Lerp(viewHeight, _targetViewHeight, t);
            ApplyImmediate();
        }

        private void HandlePan()
        {
            var mouse = Mouse.current;
            Vector2 dragPixels = Vector2.zero;
            bool dragging = false;

            if (mouse != null && mouse.leftButton.isPressed)
            {
                dragPixels = mouse.delta.ReadValue();
                dragging = true;
            }
            else
            {
                var ts = Touchscreen.current;
                if (ts != null && ts.touches.Count >= 1 && ts.touches[0].press.isPressed)
                {
                    // 두 손가락(핀치)일 땐 팬하지 않음
                    bool twoFingers = ts.touches.Count >= 2 && ts.touches[1].press.isPressed;
                    if (!twoFingers) { dragPixels = ts.touches[0].delta.ReadValue(); dragging = true; }
                }
            }

            if (dragging && dragPixels.sqrMagnitude > 0f)
            {
                // 화면 픽셀 → 월드 이동(드래그 방향과 반대로 맵이 끌려옴)
                float worldPerPixel = viewHeight / Mathf.Max(1, Screen.height);
                _targetFocus = ClampToMap(_targetFocus +
                    new Vector3(-dragPixels.x * worldPerPixel, 0f, -dragPixels.y * worldPerPixel));
            }
        }

        private void HandleZoom()
        {
            var mouse = Mouse.current;
            if (mouse != null)
            {
                float s = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(s) > 0.01f)
                    _targetViewHeight = Mathf.Clamp(_targetViewHeight * (s > 0 ? 1f / zoomStep : zoomStep),
                                                    minViewHeight, maxViewHeight);
            }
            var ts = Touchscreen.current;
            if (ts != null && ts.touches.Count >= 2 && ts.touches[0].press.isPressed && ts.touches[1].press.isPressed)
            {
                float d = Vector2.Distance(ts.touches[0].position.ReadValue(), ts.touches[1].position.ReadValue());
                if (_pinchPrev > 0f && d > 0f)
                    _targetViewHeight = Mathf.Clamp(_targetViewHeight * (_pinchPrev / d), minViewHeight, maxViewHeight);
                _pinchPrev = d;
            }
            else _pinchPrev = 0f;
        }

        private void ApplyImmediate()
        {
            var cam = GetComponent<Camera>();
            cam.orthographic = false;
            cam.fieldOfView = fieldOfView;

            Quaternion rot = Quaternion.Euler(Mathf.Clamp(tiltAngle, 20f, 90f), 0f, 0f);
            Vector3 dir = rot * Vector3.forward;                  // 아래·북쪽
            float dist = (viewHeight * 0.5f) / Mathf.Tan(fieldOfView * 0.5f * Mathf.Deg2Rad);

            cam.transform.rotation = rot;
            cam.transform.position = _focus - dir * dist;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = dist + Mathf.Max(map.Width, map.Height) * map.CellSize + 100f;
        }

        private Vector3 ClampToMap(Vector3 p)
        {
            float w = map.Width * map.CellSize, h = map.Height * map.CellSize;
            return new Vector3(
                Mathf.Clamp(p.x, map.Origin.x, map.Origin.x + w), p.y,
                Mathf.Clamp(p.z, map.Origin.z, map.Origin.z + h));
        }

        /// <summary>위경도 → 월드. (0,0)=남서(경도-180,위도-90), x=동·z=북.</summary>
        public Vector3 LatLonToWorld(float lat, float lon)
        {
            float gx = (lon + 180f) / 360f * map.Width;
            float gy = (lat + 90f) / 180f * map.Height;
            return map.Origin + new Vector3(gx * map.CellSize, 0f, gy * map.CellSize);
        }
    }
}

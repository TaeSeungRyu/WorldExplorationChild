using UnityEngine;
using UnityEngine.InputSystem;

namespace WorldExploration.Map
{
    /// <summary>
    /// 맵 영역(XZ 바운드)을 비스듬히 내려다보며 드래그 팬 + 휠/핀치 줌 하는 카메라.
    /// 카메라 GameObject에 붙인다. 바운드는 Configure()로 주입(예: FBX 맵의 렌더러 바운드).
    /// followTarget 지정 시 그 대상을 따라가며 팬 비활성. focus는 바운드 안으로 클램프.
    /// 입력은 새 Input System(Mouse/Touchscreen).
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Camera))]
    public class MapCameraRig : MonoBehaviour
    {
        [Header("맵 영역 (XZ 월드 바운드)")]
        [SerializeField] private Vector3 boundsCenter = Vector3.zero;
        [SerializeField] private Vector3 boundsSize = new Vector3(1200f, 0f, 600f);

        [Header("시작 초점 (바운드를 정사각투영으로 가정한 위경도)")]
        [SerializeField] private bool startAtLatLon = true;
        [SerializeField] private float focusLatitude = 36.3f;   // 대한민국
        [SerializeField] private float focusLongitude = 127.8f;
        [SerializeField] private Transform followTarget;

        [Header("시점")]
        [Range(20f, 90f)] [SerializeField] private float tiltAngle = 55f;
        [Range(-90f, 90f)] [SerializeField] private float yawAngle = 0f;
        [Range(20f, 80f)] [SerializeField] private float fieldOfView = 45f;

        [Header("줌 (세로로 보이는 월드 크기)")]
        [SerializeField] private float viewHeight = 200f;
        [SerializeField] private float minViewHeight = 5f;
        [SerializeField] private float maxViewHeight = 2000f;
        [Range(1.02f, 1.3f)] [SerializeField] private float zoomStep = 1.12f;
        [Range(1f, 20f)] [SerializeField] private float smooth = 10f;

        private Vector3 _focus, _targetFocus;
        private float _targetViewHeight;
        private bool _initialized;
        private float _pinchPrev;

        public Transform FollowTarget { get => followTarget; set => followTarget = value; }
        public bool StartAtLatLon { get => startAtLatLon; set => startAtLatLon = value; }
        public float ViewHeight
        {
            get => viewHeight;
            set { viewHeight = _targetViewHeight = Mathf.Clamp(value, minViewHeight, maxViewHeight); if (_initialized) ApplyImmediate(); }
        }

        /// <summary>맵 영역(바운드) + 시작 줌 설정 + 시점 값 정상 리셋. 빌더에서 FBX 바운드로 호출.</summary>
        public void Configure(Bounds area, float startViewHeight, float maxView)
        {
            boundsCenter = area.center;
            boundsSize = area.size;
            tiltAngle = 33f;        // 부감 각도(요청값)
            yawAngle = 0f;
            fieldOfView = 45f;
            minViewHeight = Mathf.Max(2f, maxView * 0.01f);
            maxViewHeight = Mathf.Max(maxView, startViewHeight);
            viewHeight = Mathf.Clamp(startViewHeight, minViewHeight, maxViewHeight);
            ResetToFocus();
        }

        private void OnEnable() => ResetToFocus();
        private void OnValidate() { _targetViewHeight = viewHeight; if (_initialized) ApplyImmediate(); else ResetToFocus(); }

        public void ResetToFocus()
        {
            if (boundsSize.x <= 0f || boundsSize.z <= 0f) return;
            Vector3 c = startAtLatLon ? LatLonToWorld(focusLatitude, focusLongitude) : boundsCenter;
            _focus = _targetFocus = ClampToBounds(c);
            _targetViewHeight = viewHeight;
            _initialized = true;
            ApplyImmediate();
        }

        private void Update()
        {
            // 에디트 모드에선 카메라를 매 프레임 덮어쓰지 않음 → 수동 조정 가능(읽기전용처럼 보이던 문제 해결).
            // 위치/줌은 빌더의 Configure()나 인스펙터 필드 변경(OnValidate) 시 1회만 적용. 연속 제어는 플레이에서만.
            if (!Application.isPlaying || !_initialized) return;

            if (followTarget != null) _targetFocus = ClampToBounds(followTarget.position);
            else HandlePan();
            HandleZoom();

            float t = 1f - Mathf.Exp(-smooth * Time.unscaledDeltaTime);
            _focus = Vector3.Lerp(_focus, _targetFocus, t);
            viewHeight = Mathf.Lerp(viewHeight, _targetViewHeight, t);
            ApplyImmediate();
        }

        private void HandlePan()
        {
            Vector2 drag = Vector2.zero; bool dragging = false;
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.isPressed) { drag = mouse.delta.ReadValue(); dragging = true; }
            else
            {
                var ts = Touchscreen.current;
                if (ts != null && ts.touches.Count >= 1 && ts.touches[0].press.isPressed)
                {
                    bool two = ts.touches.Count >= 2 && ts.touches[1].press.isPressed;
                    if (!two) { drag = ts.touches[0].delta.ReadValue(); dragging = true; }
                }
            }
            if (dragging && drag.sqrMagnitude > 0f)
            {
                Quaternion rot = Quaternion.Euler(0f, yawAngle, 0f);
                Vector3 right = rot * Vector3.right;
                Vector3 fwd = rot * Vector3.forward;
                float wpp = viewHeight / Mathf.Max(1, Screen.height);
                _targetFocus = ClampToBounds(_targetFocus + (-drag.x * right - drag.y * fwd) * wpp);
            }
        }

        private void HandleZoom()
        {
            var mouse = Mouse.current;
            if (mouse != null)
            {
                float s = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(s) > 0.01f)
                    _targetViewHeight = Mathf.Clamp(_targetViewHeight * (s > 0 ? 1f / zoomStep : zoomStep), minViewHeight, maxViewHeight);
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
            Quaternion rot = Quaternion.Euler(Mathf.Clamp(tiltAngle, 20f, 90f), yawAngle, 0f);
            Vector3 dir = rot * Vector3.forward;
            float dist = (viewHeight * 0.5f) / Mathf.Tan(fieldOfView * 0.5f * Mathf.Deg2Rad);
            cam.transform.rotation = rot;
            cam.transform.position = _focus - dir * dist;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = dist + Mathf.Max(boundsSize.x, boundsSize.z) + 200f;
        }

        private Vector3 ClampToBounds(Vector3 p)
        {
            float hx = boundsSize.x * 0.5f, hz = boundsSize.z * 0.5f;
            return new Vector3(
                Mathf.Clamp(p.x, boundsCenter.x - hx, boundsCenter.x + hx), p.y,
                Mathf.Clamp(p.z, boundsCenter.z - hz, boundsCenter.z + hz));
        }

        /// <summary>위경도 → 월드. 바운드를 정사각투영(경도=X, 위도=Z)으로 가정.</summary>
        public Vector3 LatLonToWorld(float lat, float lon)
        {
            float fx = (lon + 180f) / 360f;
            float fz = (lat + 90f) / 180f;
            return new Vector3(
                boundsCenter.x - boundsSize.x * 0.5f + fx * boundsSize.x, 0f,
                boundsCenter.z - boundsSize.z * 0.5f + fz * boundsSize.z);
        }
    }
}

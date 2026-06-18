using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace WorldExploration.Map.EditorTools
{
    /// <summary>
    /// 저폴리 월드맵 FBX를 씬에 배치하고, 그 바운드에 맞춰 팬·줌 카메라 + 광원을 설정한다.
    /// 메뉴: Tools ▸ World Exploration ▸ Build Map In Scene
    /// 비주얼 = FBX(WorldMap_LowPoly), 카메라 = MapCameraRig(바운드 기반).
    /// </summary>
    public static class WorldMapSceneBuilder
    {
        private const string FbxPath = "Assets/Game/Art/Models/WorldMap_LowPoly.fbx";
        private const string MapObjectName = "WorldMap";

        [MenuItem("Tools/World Exploration/Build Map In Scene")]
        public static void Build()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            if (model == null)
            {
                EditorUtility.DisplayDialog("Build Map In Scene",
                    $"FBX를 찾을 수 없습니다:\n{FbxPath}", "확인");
                return;
            }

            // 1) 씬에 이미 있는 맵 오브젝트(단독 드롭한 것)를 재사용 — 없을 때만 새로 인스턴스
            var go = GameObject.Find(MapObjectName) ?? GameObject.Find("WorldMap_LowPoly");
            if (go == null)
                go = (GameObject)PrefabUtility.InstantiatePrefab(model);
            go.name = MapObjectName;
            go.transform.position = Vector3.zero;
            // FBX는 수직(위도=Y)으로 임포트됨 → 눕혀야 탑다운 리그가 위도축 기준으로 프레이밍됨.
            // 270,180 = 사용자 검증 방향(북=위, 미러 없음). 색 문제는 아래 양면 렌더로 방지.
            go.transform.rotation = Quaternion.Euler(270f, 180f, 0f);
            MakeDoubleSided(go);  // 눕혔을 때 면 뒤집힘(백페이스)으로 어두워지는 것 방지 — 색 유지

            // 2) 바운드 계산 (실제 렌더러에서 — 스케일/위치 몰라도 자동 프레이밍)
            if (!TryGetBounds(go, out Bounds b))
            {
                EditorUtility.DisplayDialog("Build Map In Scene",
                    "FBX에 렌더러가 없습니다(메시 없음?). FBX 내용을 확인하세요.", "확인");
                return;
            }
            DiagnoseModel(go);   // 렌더러/머티리얼/바운드 로그 (진단)
            // 머티리얼은 FBX 본래 것을 그대로 사용(덮어쓰지 않음 — 그게 색이 정상).

            // 3) 카메라 (바운드 기반 팬·줌)
            Camera cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera", typeof(Camera));
                camGo.tag = "MainCamera";
                cam = camGo.GetComponent<Camera>();
            }
            var rig = GetOrAdd<MapCameraRig>(cam.gameObject);
            rig.StartAtLatLon = false;            // 맵 중앙에서 시작(드래그로 이동)
            float startVH = b.size.z * 0.95f;     // 전체 맵이 거의 다 보이게(파묻힘 방지). 이후 휠로 줌인.
            float maxVH = b.size.z * 1.25f;
            rig.Configure(b, startVH, maxVH);
            cam.clearFlags = CameraClearFlags.Skybox;  // 배경은 기본(Skybox) — 바다는 나중에 직접 추가

            // 4) 광원: 씬에 Directional Light가 없을 때만 추가(기존 조명·환경광은 건드리지 않음
            //    — standalone이 잘 보이는 그 기본 조명을 그대로 유지)
            var sun = Object.FindFirstObjectByType<Light>();
            if (sun == null || sun.type != LightType.Directional)
            {
                var lg = new GameObject("Directional Light", typeof(Light));
                var nl = lg.GetComponent<Light>();
                nl.type = LightType.Directional;
                nl.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                nl.shadows = LightShadows.None;
            }
            // 이전 빌드가 바꿔놨을 수 있는 환경광을 씬 기본(Skybox)으로 되돌림 → standalone과 동일
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;

            Selection.activeGameObject = go;
            EditorSceneManager.MarkSceneDirty(go.scene);
            Debug.Log($"[WorldMapSceneBuilder] FBX 맵 배치 완료.\n" +
                      $"  바운드 center={b.center} size={b.size}\n" +
                      $"  카메라 pos={cam.transform.position} rot={cam.transform.eulerAngles} VH={startVH:F0}\n" +
                      $"  광원={(sun != null ? "있음(intensity " + sun.intensity + ")" : "없음")}. 드래그=팬, 휠/핀치=줌.");
        }

        /// <summary>FBX 머티리얼을 양면 렌더로(색은 그대로 유지). 눕혔을 때 백페이스 컬링으로 어두워짐 방지.</summary>
        private static void MakeDoubleSided(GameObject go)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>())
                foreach (var m in r.sharedMaterials)
                {
                    if (m == null) continue;
                    if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f); // 0 = Off(양면)
                    m.doubleSidedGI = true;
                    EditorUtility.SetDirty(m);
                }
            AssetDatabase.SaveAssets();
        }

        private static bool TryGetBounds(GameObject go, out Bounds b)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) { b = default; return false; }
            b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return true;
        }

        private static void DiagnoseModel(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[Map 진단] 렌더러 {rs.Length}개");
            foreach (var r in rs)
            {
                string mats = "";
                foreach (var m in r.sharedMaterials)
                    mats += (m == null ? "null" : $"{m.name}({(m.shader != null ? m.shader.name : "noShader")})") + " ";
                sb.AppendLine($"  - {r.name}: bounds c={r.bounds.center} s={r.bounds.size} | mats: {mats}");
            }
            Debug.Log(sb.ToString());
        }

        private static T GetOrAdd<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            if (c == null) c = go.AddComponent<T>();
            return c;
        }
    }
}

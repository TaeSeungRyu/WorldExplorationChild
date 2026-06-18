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
            // 머티리얼은 FBX 원본 그대로 사용(건드리지 않음). 270,180이면 윗면이 위를 향해 단면으로도 보임.

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
            cam.clearFlags = CameraClearFlags.Skybox;

            // 3b) 바다 평면 (씬 루트에 평평하게 — 맵 영역을 덮는 파란 면)
            BuildSea(b);

            // 4) 광원: 눕힌 맵 윗면(+Y)을 잘 비추도록 각도/세기 설정 (없으면 생성).
            //    가파른 각도일수록 윗면이 밝고 색이 살아남. 약간의 기울기로 저폴리 패싯 음영도 유지.
            var sun = Object.FindFirstObjectByType<Light>();
            if (sun == null || sun.type != LightType.Directional)
            {
                var lg = new GameObject("Directional Light", typeof(Light));
                sun = lg.GetComponent<Light>();
                sun.type = LightType.Directional;
            }
            sun.transform.rotation = Quaternion.Euler(0f, 0f, 0f); // 라이트 회전 0 (요청)
            sun.intensity = 1.15f;
            sun.shadows = LightShadows.None;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox; // 환경광은 기본(색 자연스럽게)

            Selection.activeGameObject = go;
            EditorSceneManager.MarkSceneDirty(go.scene);
            Debug.Log($"[WorldMapSceneBuilder] FBX 맵 배치 완료.\n" +
                      $"  바운드 center={b.center} size={b.size}\n" +
                      $"  카메라 pos={cam.transform.position} rot={cam.transform.eulerAngles} VH={startVH:F0}\n" +
                      $"  광원={(sun != null ? "있음(intensity " + sun.intensity + ")" : "없음")}. 드래그=팬, 휠/핀치=줌.");
        }

        private const string SeaObjectName = "Sea";
        private const string MatDir = "Assets/Game/Map/Materials";
        private static readonly Color SeaColor = new Color(0.13f, 0.34f, 0.58f);

        /// <summary>맵 영역을 덮는 평평한 바다 평면을 씬 루트에 배치. 해수면 Y는 인스펙터에서 조절.</summary>
        private static void BuildSea(Bounds b)
        {
            var old = GameObject.Find(SeaObjectName);
            if (old != null) Object.DestroyImmediate(old);

            var sea = GameObject.CreatePrimitive(PrimitiveType.Plane); // 내장 Plane(10x10 XZ, 법선 +Y)
            sea.name = SeaObjectName;
            var col = sea.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);

            float seaY = b.min.y - 0.2f;   // 맵 바닥보다 살짝 아래 → 땅이 항상 위로 보임(덮지 않음). 인스펙터에서 Y 올려 수위 조절.
            const float pad = 1.4f;                     // 맵보다 약간 넓게(수평선까지)
            sea.transform.position = new Vector3(b.center.x, seaY, b.center.z);
            sea.transform.localScale = new Vector3(b.size.x * pad / 10f, 1f, b.size.z * pad / 10f);
            sea.GetComponent<MeshRenderer>().sharedMaterial = EnsureSeaMat();
        }

        private static Material EnsureSeaMat()
        {
            if (!AssetDatabase.IsValidFolder(MatDir))
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.GetFullPath(MatDir));
                AssetDatabase.Refresh();
            }
            string path = $"{MatDir}/Sea.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                mat = new Material(sh);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", SeaColor);
                mat.color = SeaColor;
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.4f);
                AssetDatabase.CreateAsset(mat, path);
            }
            return mat;
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

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace WorldExploration.Map.EditorTools
{
    /// <summary>
    /// 현재 씬에 맵 평면(<see cref="WorldMapRenderer"/>)과 탑다운 직교 카메라를 자동 배치한다.
    /// 메뉴: Tools ▸ World Exploration ▸ Build Map In Scene
    /// 클릭 한 번으로 에디터/플레이 모두에서 세계지도를 화면에 띄운다.
    /// </summary>
    public static class WorldMapSceneBuilder
    {
        private const string MapDataPath = "Assets/Game/Map/WorldMapData.asset";
        private const string MapObjectName = "WorldMap";

        [MenuItem("Tools/World Exploration/Build Map In Scene")]
        public static void Build()
        {
            var data = AssetDatabase.LoadAssetAtPath<WorldMapData>(MapDataPath);
            if (data == null)
            {
                EditorUtility.DisplayDialog("Build Map In Scene",
                    $"WorldMapData를 찾을 수 없습니다:\n{MapDataPath}\n\n" +
                    "먼저 'Import World Map (PNG → WorldMapData)'를 실행하세요.", "확인");
                return;
            }

            // 1) 맵 평면 오브젝트
            var go = GameObject.Find(MapObjectName) ?? new GameObject(MapObjectName);
            var renderer = go.GetComponent<WorldMapRenderer>() ?? go.AddComponent<WorldMapRenderer>();
            renderer.MapData = data; // setter가 Rebuild 수행
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            // 2) 탑다운 직교 카메라
            float worldW = data.Width * data.CellSize;
            float worldH = data.Height * data.CellSize;

            // 2) 비스듬히 내려다보는 카메라 (MapCameraRig가 각도·줌·투영을 정의)
            Camera cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera", typeof(Camera));
                camGo.tag = "MainCamera";
                cam = camGo.GetComponent<Camera>();
            }
            var rig = cam.GetComponent<MapCameraRig>() ?? cam.gameObject.AddComponent<MapCameraRig>();
            rig.Map = data; // setter가 Apply 수행 (입체 부감 시점으로 배치)

            // 3) 비스듬한 직사광 — 산맥 음영(기복지도 효과). 그림자는 모바일 위해 off.
            Light sun = Object.FindFirstObjectByType<Light>();
            if (sun == null || sun.type != LightType.Directional)
            {
                var lightGo = new GameObject("Directional Light", typeof(Light));
                sun = lightGo.GetComponent<Light>();
                sun.type = LightType.Directional;
            }
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f); // 낮은 해 → 슬로프 음영
            sun.shadows = LightShadows.None;
            sun.intensity = 1.1f;

            // 4) 선택 & 씬 더티
            Selection.activeGameObject = go;
            EditorSceneManager.MarkSceneDirty(go.scene);

            Debug.Log($"[WorldMapSceneBuilder] 맵 배치 완료. 평면 {worldW}x{worldH} (월드유닛), " +
                      $"카메라=입체 부감(MapCameraRig). 각도/줌은 카메라의 MapCameraRig에서 조절. " +
                      $"Scene/Game 뷰에서 확인하세요.");
        }
    }
}

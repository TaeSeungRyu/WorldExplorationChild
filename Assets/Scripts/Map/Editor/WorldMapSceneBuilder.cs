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
            Vector3 center = data.Origin + new Vector3(worldW * 0.5f, 0f, worldH * 0.5f);
            float span = Mathf.Max(worldW, worldH);

            Camera cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera", typeof(Camera));
                camGo.tag = "MainCamera";
                cam = camGo.GetComponent<Camera>();
            }
            cam.orthographic = true;
            cam.orthographicSize = worldH * 0.5f;        // 세로 전체가 보이도록
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = span * 2f + 10f;
            cam.transform.SetPositionAndRotation(
                center + new Vector3(0f, span, 0f),       // 맵 위에서
                Quaternion.Euler(90f, 0f, 0f));           // 똑바로 내려다봄 (북=+Z=화면 위)

            // 3) 비스듬한 직사광 — 탑다운에서도 산맥 음영이 보이게(기복지도 효과). 그림자는 모바일 위해 off.
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
                      $"카메라 직교 size={cam.orthographicSize}. Scene/Game 뷰에서 확인하세요.");
        }
    }
}

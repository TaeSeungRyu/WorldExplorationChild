using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace WorldExploration.Map.EditorTools
{
    /// <summary>
    /// 현재 씬에 단색 바다 평면 + 단색(녹색) 입체 대륙 메시 + 팬·줌 카메라 + 광원을 배치한다.
    /// 메뉴: Tools ▸ World Exploration ▸ Build Map In Scene
    ///
    /// 단색이라 어느 줌에서도 안 깨짐. 비주얼/로직(WorldMapData) 분리 유지.
    /// </summary>
    public static class WorldMapSceneBuilder
    {
        private const string MapDataPath = "Assets/Game/Map/WorldMapData.asset";
        private const string LandMeshPath = "Assets/Game/Map/LandMesh.asset";
        private const string MatDir = "Assets/Game/Map/Materials";
        private const string MapObjectName = "WorldMap";

        private static readonly Color LandColor = new Color(0.36f, 0.55f, 0.27f); // 녹색
        private static readonly Color SeaColor = new Color(0.13f, 0.30f, 0.52f);  // 파랑

        [MenuItem("Tools/World Exploration/Build Map In Scene")]
        public static void Build()
        {
            var data = AssetDatabase.LoadAssetAtPath<WorldMapData>(MapDataPath);
            if (data == null)
            {
                EditorUtility.DisplayDialog("Build Map In Scene",
                    $"WorldMapData를 찾을 수 없습니다:\n{MapDataPath}\n\n먼저 'Import World Map'을 실행하세요.", "확인");
                return;
            }

            float worldW = data.Width * data.CellSize;
            float worldH = data.Height * data.CellSize;

            // 1) 옛 WorldMap 제거 후 새로 생성
            var old = GameObject.Find(MapObjectName);
            if (old != null) Object.DestroyImmediate(old);
            var root = new GameObject(MapObjectName);
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            // 2) 바다 = 단색 평면 (직접 만든 quad 메시 — 내장 리소스 의존 제거)
            var sea = new GameObject("Sea");
            sea.transform.SetParent(root.transform, false);
            sea.AddComponent<MeshFilter>().sharedMesh = EnsureSeaQuad();
            sea.AddComponent<MeshRenderer>().sharedMaterial = EnsureMaterial("Sea.mat", SeaColor, doubleSided: true);
            sea.transform.localScale = new Vector3(worldW, 1f, worldH);
            sea.transform.localPosition = new Vector3(worldW * 0.5f, -0.05f, worldH * 0.5f);

            // 3) 입체 대륙 메시 = 단색 녹색
            var landMesh = AssetDatabase.LoadAssetAtPath<Mesh>(LandMeshPath);
            if (landMesh != null)
            {
                var land = new GameObject("Land");
                land.transform.SetParent(root.transform, false);
                land.AddComponent<MeshFilter>().sharedMesh = landMesh;
                land.AddComponent<MeshRenderer>().sharedMaterial = EnsureMaterial("Land.mat", LandColor, doubleSided: true);
                land.transform.localPosition = Vector3.zero; // 메시가 이미 월드 좌표
            }
            else
            {
                Debug.LogWarning("[WorldMapSceneBuilder] LandMesh.asset 없음 — 바다 평면만 배치. " +
                                 "입체 대륙을 원하면 'Import Land Mesh'를 실행하세요.");
            }

            // 4) 팬·줌 카메라 (한국 중심 시작)
            Camera cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera", typeof(Camera));
                camGo.tag = "MainCamera";
                cam = camGo.GetComponent<Camera>();
            }
            var rig = GetOrAdd<MapCameraRig>(cam.gameObject);
            rig.Map = data;
            cam.clearFlags = CameraClearFlags.SolidColor; // 바다 평면이 안 보여도 배경은 바다색
            cam.backgroundColor = SeaColor;

            // 해안선 가장자리 부드럽게: URP 에셋 MSAA 4x(지오메트리) + 카메라 FXAA(화면 기반) 둘 다.
            var camData = cam.GetUniversalAdditionalCameraData();
            camData.renderPostProcessing = true;   // FXAA 적용을 위해 후처리 패스 켬
            camData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;

            // 5) 직사광 — 돌출 대륙 옆면 음영(입체감)
            Light sun = Object.FindFirstObjectByType<Light>();
            if (sun == null || sun.type != LightType.Directional)
            {
                var lightGo = new GameObject("Directional Light", typeof(Light));
                sun = lightGo.GetComponent<Light>();
                sun.type = LightType.Directional;
            }
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            sun.shadows = LightShadows.None;

            Selection.activeGameObject = root;
            EditorSceneManager.MarkSceneDirty(root.scene);
            Debug.Log($"[WorldMapSceneBuilder] 단색 맵 배치 완료. 월드 {worldW}x{worldH}. 드래그=팬, 휠/핀치=줌.");
        }

        private static Material EnsureMaterial(string fileName, Color color, bool doubleSided)
        {
            if (!AssetDatabase.IsValidFolder(MatDir))
            {
                Directory.CreateDirectory(Path.GetFullPath(MatDir));
                AssetDatabase.Refresh();
            }
            string path = $"{MatDir}/{fileName}";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool isNew = mat == null;
            if (isNew)
            {
                Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                mat = new Material(sh);
            }
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            mat.color = color;
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", null);
            mat.mainTexture = null;
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0f);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
            if (doubleSided && mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f); // 양면(옆벽)
            if (isNew) AssetDatabase.CreateAsset(mat, path);
            else EditorUtility.SetDirty(mat);
            return mat;
        }

        private const string SeaQuadPath = "Assets/Game/Map/SeaQuad.asset";

        /// <summary>1x1 XZ 평면(법선 +Y) 메시 에셋. 스케일로 크기 조절.</summary>
        private static Mesh EnsureSeaQuad()
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(SeaQuadPath);
            if (mesh != null) return mesh;
            mesh = new Mesh { name = "SeaQuad" };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, -0.5f),
                new Vector3(0.5f, 0f, 0.5f),   new Vector3(-0.5f, 0f, 0.5f),
            };
            mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, SeaQuadPath);
            return mesh;
        }

        private static T GetOrAdd<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            if (c == null) c = go.AddComponent<T>();
            return c;
        }
    }
}

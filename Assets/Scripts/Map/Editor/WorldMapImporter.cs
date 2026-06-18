using System.IO;
using UnityEditor;
using UnityEngine;

namespace WorldExploration.Map.EditorTools
{
    /// <summary>
    /// 색상 코딩 PNG(바다=파랑/육지=초록) → <see cref="WorldMapData"/> 에셋으로 변환한다.
    /// 메뉴: Tools ▸ World Exploration ▸ Import World Map (PNG → WorldMapData)
    ///
    /// PNG 픽셀 (0,0)은 좌하단이며 격자 (0,0)=남서쪽과 일치하므로 행 뒤집기가 필요 없다.
    /// </summary>
    public static class WorldMapImporter
    {
        private const string SourcePng = "Assets/Game/Map/Authoring/world_terrain_512x256.png";
        private const string OutputAsset = "Assets/Game/Map/WorldMapData.asset";

        // 변환 스크립트(tools/geojson_to_terrain.py)가 쓴 색상과 일치해야 한다.
        private static readonly Color32 SeaColor = new Color32(40, 90, 160, 255);
        private static readonly Color32 LandColor = new Color32(95, 150, 70, 255);

        [MenuItem("Tools/World Exploration/Import World Map (PNG → WorldMapData)")]
        public static void Import()
        {
            string absPng = Path.GetFullPath(SourcePng);
            if (!File.Exists(absPng))
            {
                EditorUtility.DisplayDialog("World Map Import",
                    $"PNG를 찾을 수 없습니다:\n{SourcePng}", "확인");
                return;
            }

            // 임포트 설정과 무관하게 원본 픽셀을 읽기 위해 파일 바이트를 직접 디코드.
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(File.ReadAllBytes(absPng)))
            {
                Object.DestroyImmediate(tex);
                EditorUtility.DisplayDialog("World Map Import", "PNG 디코드 실패.", "확인");
                return;
            }

            int w = tex.width, h = tex.height;
            Color32[] pixels = tex.GetPixels32(); // index = y*w + x, (0,0)=좌하단
            Object.DestroyImmediate(tex);

            var terrain = new byte[w * h];
            int landCount = 0;
            for (int i = 0; i < pixels.Length; i++)
            {
                TerrainType t = Classify(pixels[i]);
                terrain[i] = (byte)t;
                if (t == TerrainType.Land) landCount++;
            }

            // 출력 폴더 보장
            string dir = Path.GetDirectoryName(OutputAsset);
            if (!AssetDatabase.IsValidFolder(dir))
            {
                Directory.CreateDirectory(Path.GetFullPath(dir));
                AssetDatabase.Refresh();
            }

            var data = AssetDatabase.LoadAssetAtPath<WorldMapData>(OutputAsset);
            bool isNew = data == null;
            if (isNew) data = ScriptableObject.CreateInstance<WorldMapData>();

            data.Initialize(w, h, terrain, cellSize: 1f, origin: Vector3.zero);

            if (isNew) AssetDatabase.CreateAsset(data, OutputAsset);
            else EditorUtility.SetDirty(data);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = data;
            EditorGUIUtility.PingObject(data);

            Debug.Log($"[WorldMapImporter] {(isNew ? "생성" : "갱신")} 완료: {OutputAsset}\n" +
                      $"격자 {w}x{h} ({w * h}셀), 육지 {landCount} / 바다 {w * h - landCount}");
        }

        /// <summary>픽셀 색을 바다/육지 팔레트 중 더 가까운 쪽으로 분류.</summary>
        private static TerrainType Classify(Color32 c)
        {
            long dSea = SqDist(c, SeaColor);
            long dLand = SqDist(c, LandColor);
            return dSea <= dLand ? TerrainType.DeepSea : TerrainType.Land;
        }

        private static long SqDist(Color32 a, Color32 b)
        {
            long dr = a.r - b.r, dg = a.g - b.g, db = a.b - b.b;
            return dr * dr + dg * dg + db * db;
        }
    }
}

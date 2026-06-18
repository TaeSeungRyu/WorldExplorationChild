using System.IO;
using UnityEditor;
using UnityEngine;

namespace WorldExploration.Map.EditorTools
{
    /// <summary>
    /// 지형 분류 PNG + 고도 PNG → <see cref="WorldMapData"/> 에셋으로 변환한다.
    /// 메뉴: Tools ▸ World Exploration ▸ Import World Map (PNG → WorldMapData)
    ///
    /// PNG 픽셀 (0,0)은 좌하단이며 격자 (0,0)=남서쪽과 일치하므로 행 뒤집기가 필요 없다.
    /// 두 PNG는 tools/build_world_data.py 가 생성하며 색상/인코딩이 여기와 일치해야 한다.
    /// </summary>
    public static class WorldMapImporter
    {
        private const string TerrainPng = "Assets/Game/Map/Authoring/world_terrain_1024x512.png";
        private const string HeightPng = "Assets/Game/Map/Authoring/world_height_1024x512.png";
        private const string OutputAsset = "Assets/Game/Map/WorldMapData.asset";

        // build_world_data.py 의 지형 분류 색과 일치.
        private static readonly (Color32 color, TerrainType type)[] Palette =
        {
            (new Color32(30, 70, 140, 255), TerrainType.DeepSea),
            (new Color32(70, 130, 200, 255), TerrainType.ShallowSea),
            (new Color32(95, 150, 70, 255), TerrainType.Land),
            (new Color32(120, 110, 95, 255), TerrainType.Mountain),
        };

        [MenuItem("Tools/World Exploration/Import World Map (PNG → WorldMapData)")]
        public static void Import()
        {
            if (!TryLoadTexture(TerrainPng, out Texture2D terrainTex)) return;
            if (!TryLoadTexture(HeightPng, out Texture2D heightTex))
            {
                Object.DestroyImmediate(terrainTex);
                return;
            }

            int w = terrainTex.width, h = terrainTex.height;
            if (heightTex.width != w || heightTex.height != h)
            {
                EditorUtility.DisplayDialog("World Map Import",
                    $"지형/고도 PNG 크기가 다릅니다.\n지형 {w}x{h}, 고도 {heightTex.width}x{heightTex.height}", "확인");
                Object.DestroyImmediate(terrainTex);
                Object.DestroyImmediate(heightTex);
                return;
            }

            Color32[] terrainPx = terrainTex.GetPixels32();
            Color32[] heightPx = heightTex.GetPixels32();
            Object.DestroyImmediate(terrainTex);
            Object.DestroyImmediate(heightTex);

            var terrain = new byte[w * h];
            var heights = new byte[w * h];
            var counts = new int[5];
            for (int i = 0; i < terrain.Length; i++)
            {
                TerrainType t = Classify(terrainPx[i]);
                terrain[i] = (byte)t;
                counts[(int)t]++;
                heights[i] = heightPx[i].r; // 그레이스케일이므로 r=g=b
            }

            EnsureFolder(Path.GetDirectoryName(OutputAsset));

            var data = AssetDatabase.LoadAssetAtPath<WorldMapData>(OutputAsset);
            bool isNew = data == null;
            if (isNew) data = ScriptableObject.CreateInstance<WorldMapData>();

            data.Initialize(w, h, terrain, heights, cellSize: 1f, origin: Vector3.zero);

            if (isNew) AssetDatabase.CreateAsset(data, OutputAsset);
            else EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = data;
            EditorGUIUtility.PingObject(data);

            Debug.Log($"[WorldMapImporter] {(isNew ? "생성" : "갱신")} 완료: {OutputAsset}\n" +
                      $"격자 {w}x{h} ({w * h}셀) | 심해 {counts[(int)TerrainType.DeepSea]}, " +
                      $"얕은바다 {counts[(int)TerrainType.ShallowSea]}, 육지 {counts[(int)TerrainType.Land]}, " +
                      $"산악 {counts[(int)TerrainType.Mountain]}");
        }

        private static bool TryLoadTexture(string assetPath, out Texture2D tex)
        {
            tex = null;
            string abs = Path.GetFullPath(assetPath);
            if (!File.Exists(abs))
            {
                EditorUtility.DisplayDialog("World Map Import",
                    $"PNG를 찾을 수 없습니다:\n{assetPath}\n\n먼저 tools/build_world_data.py 를 실행하세요.", "확인");
                return false;
            }
            // 임포트 설정과 무관하게 원본 픽셀을 읽기 위해 파일 바이트를 직접 디코드.
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!t.LoadImage(File.ReadAllBytes(abs)))
            {
                Object.DestroyImmediate(t);
                EditorUtility.DisplayDialog("World Map Import", $"PNG 디코드 실패:\n{assetPath}", "확인");
                return false;
            }
            tex = t;
            return true;
        }

        private static void EnsureFolder(string dir)
        {
            if (!AssetDatabase.IsValidFolder(dir))
            {
                Directory.CreateDirectory(Path.GetFullPath(dir));
                AssetDatabase.Refresh();
            }
        }

        /// <summary>픽셀 색을 팔레트 중 가장 가까운 지형 타입으로 분류.</summary>
        private static TerrainType Classify(Color32 c)
        {
            TerrainType best = TerrainType.DeepSea;
            long bestDist = long.MaxValue;
            foreach (var (color, type) in Palette)
            {
                long dr = c.r - color.r, dg = c.g - color.g, db = c.b - color.b;
                long d = dr * dr + dg * dg + db * db;
                if (d < bestDist) { bestDist = d; best = type; }
            }
            return best;
        }
    }
}

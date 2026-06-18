using System.IO;
using UnityEditor;
using UnityEngine;

namespace WorldExploration.Map.EditorTools
{
    /// <summary>
    /// 돌출 대륙 메시 바이너리(tools/build_land_mesh.py) → Unity Mesh 에셋.
    /// 메뉴: Tools ▸ World Exploration ▸ Import Land Mesh (bytes → Mesh)
    ///
    /// 포맷(LE): int32 vCount, int32 subCount, subCount*int32 triCount,
    /// vCount*(float32 x,y,z), 그다음 서브메시별 triCount*3 int32(순차).
    /// 서브메시 = 위도 기후대 바이옴(열대/아열대/온대/냉대/극지) + 절벽. UV 없음(단색). 법선 자동.
    /// </summary>
    public static class LandMeshImporter
    {
        private const string BytesPath = "Assets/Game/Map/land_mesh.bytes";
        private const string OutputAsset = "Assets/Game/Map/LandMesh.asset";

        [MenuItem("Tools/World Exploration/Import Land Mesh (bytes → Mesh)")]
        public static void Import()
        {
            string abs = Path.GetFullPath(BytesPath);
            if (!File.Exists(abs))
            {
                EditorUtility.DisplayDialog("Import Land Mesh",
                    $"바이너리를 찾을 수 없습니다:\n{BytesPath}\n\n먼저 tools/build_land_mesh.py 를 실행하세요.", "확인");
                return;
            }

            Vector3[] verts;
            int[][] subTris;
            using (var br = new BinaryReader(File.OpenRead(abs)))
            {
                int vCount = br.ReadInt32();
                int subCount = br.ReadInt32();
                var triCounts = new int[subCount];
                for (int s = 0; s < subCount; s++) triCounts[s] = br.ReadInt32();
                verts = new Vector3[vCount];
                for (int i = 0; i < vCount; i++)
                {
                    float x = br.ReadSingle();
                    float y = br.ReadSingle();
                    float z = br.ReadSingle();
                    verts[i] = new Vector3(x, y, z);
                }
                subTris = new int[subCount][];
                for (int s = 0; s < subCount; s++)
                {
                    subTris[s] = new int[triCounts[s] * 3];
                    for (int i = 0; i < subTris[s].Length; i++) subTris[s][i] = br.ReadInt32();
                }
            }

            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(OutputAsset);
            bool isNew = mesh == null;
            if (isNew) mesh = new Mesh();
            mesh.name = "LandMesh";
            mesh.Clear();
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = verts;
            mesh.subMeshCount = subTris.Length;
            for (int s = 0; s < subTris.Length; s++) mesh.SetTriangles(subTris[s], s);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            if (isNew) AssetDatabase.CreateAsset(mesh, OutputAsset);
            else EditorUtility.SetDirty(mesh);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = mesh;
            EditorGUIUtility.PingObject(mesh);
            int total = 0; foreach (var s in subTris) total += s.Length / 3;
            Debug.Log($"[LandMeshImporter] {(isNew ? "생성" : "갱신")} 완료: {OutputAsset}\n" +
                      $"정점 {verts.Length}, 서브메시 {subTris.Length}, 삼각형 {total}");
        }
    }
}

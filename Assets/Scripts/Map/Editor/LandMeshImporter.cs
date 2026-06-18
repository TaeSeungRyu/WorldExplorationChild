using System.IO;
using UnityEditor;
using UnityEngine;

namespace WorldExploration.Map.EditorTools
{
    /// <summary>
    /// 돌출 대륙 메시 바이너리(tools/build_land_mesh.py) → Unity Mesh 에셋.
    /// 메뉴: Tools ▸ World Exploration ▸ Import Land Mesh (bytes → Mesh)
    ///
    /// 포맷(LE): int32 vCount, int32 triCount,
    /// vCount*(float32 x,y,z), triCount*3 int32.
    /// 정점 y는 고도(산). 단색 머티리얼이라 UV 없음. 법선은 임포트 시 계산.
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
            int[] tris;
            using (var br = new BinaryReader(File.OpenRead(abs)))
            {
                int vCount = br.ReadInt32();
                int triCount = br.ReadInt32();
                verts = new Vector3[vCount];
                for (int i = 0; i < vCount; i++)
                {
                    float x = br.ReadSingle();
                    float y = br.ReadSingle();
                    float z = br.ReadSingle();
                    verts[i] = new Vector3(x, y, z);
                }
                tris = new int[triCount * 3];
                for (int i = 0; i < tris.Length; i++) tris[i] = br.ReadInt32();
            }

            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(OutputAsset);
            bool isNew = mesh == null;
            if (isNew) mesh = new Mesh();
            mesh.name = "LandMesh";
            mesh.Clear();
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            if (isNew) AssetDatabase.CreateAsset(mesh, OutputAsset);
            else EditorUtility.SetDirty(mesh);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = mesh;
            EditorGUIUtility.PingObject(mesh);
            Debug.Log($"[LandMeshImporter] {(isNew ? "생성" : "갱신")} 완료: {OutputAsset}\n" +
                      $"정점 {verts.Length}, 삼각형 {tris.Length / 3}");
        }
    }
}

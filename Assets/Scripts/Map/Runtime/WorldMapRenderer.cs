using UnityEngine;

namespace WorldExploration.Map
{
    /// <summary>
    /// <see cref="WorldMapData"/> 격자를 고도가 반영된 3D 지형 메시로 렌더링한다.
    /// 색은 지형 분류 텍스처에서, 입체 기복은 고도(height)에서 온다. 둘 다 데이터에서 생성되므로 어긋나지 않는다.
    /// 라이팅(URP/Lit) + 비스듬한 광원으로 탑다운에서도 산맥 음영이 보인다(기복지도 효과).
    /// 모바일을 위해 메시 밀도는 다운샘플 가능하며(텍스처는 풀해상도 유지), 플레이 시 프레임을 고정한다.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class WorldMapRenderer : MonoBehaviour
    {
        [SerializeField] private WorldMapData mapData;

        [Header("고도")]
        [Tooltip("최고 육지 고도가 솟는 월드 높이(과장). 0이면 평면.")]
        [SerializeField] private float heightScale = 30f;

        [Header("성능")]
        [Tooltip("메시 격자 다운샘플(1=풀, 2=절반…). 텍스처 해상도는 그대로. 모바일은 2~4 권장.")]
        [Range(1, 8)] [SerializeField] private int meshDownsample = 2;
        [Tooltip("플레이 시 목표 FPS(발열 억제). 0이면 미설정.")]
        [SerializeField] private int playTargetFps = 30;

        private Mesh _mesh;
        private Texture2D _tex;
        private Material _mat;

        public WorldMapData MapData
        {
            get => mapData;
            set { mapData = value; Rebuild(); }
        }

        private void Awake()
        {
            if (Application.isPlaying && playTargetFps > 0)
                Application.targetFrameRate = playTargetFps;
        }

        private void OnEnable() => Rebuild();
        private void OnDisable() => CleanupGenerated();

        [ContextMenu("Rebuild")]
        public void Rebuild()
        {
            if (mapData == null || mapData.Width <= 0 || mapData.Height <= 0)
                return;

            BuildTexture();
            BuildMesh();
            BuildMaterial();

            GetComponent<MeshFilter>().sharedMesh = _mesh;
            GetComponent<MeshRenderer>().sharedMaterial = _mat;
        }

        private void BuildTexture()
        {
            int w = mapData.Width, h = mapData.Height;
            if (_tex == null || _tex.width != w || _tex.height != h)
            {
                CleanupTexture();
                _tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
                {
                    name = "WorldMapTex",
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.DontSave,
                };
            }

            var cols = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    cols[y * w + x] = PaletteFor(mapData.GetTerrain(x, y));
            _tex.SetPixels32(cols);
            _tex.Apply(false);
        }

        private void BuildMesh()
        {
            int w = mapData.Width, h = mapData.Height;
            int s = Mathf.Max(1, meshDownsample);
            float cell = mapData.CellSize;
            Vector3 o = mapData.Origin;

            // 정점 격자 크기 (마지막 정점은 가장자리 셀에 맞춤)
            int mw = (w - 1) / s + 1;
            int mh = (h - 1) / s + 1;

            var verts = new Vector3[mw * mh];
            var uvs = new Vector2[mw * mh];
            for (int gy = 0; gy < mh; gy++)
            {
                int cy = (gy == mh - 1) ? h - 1 : gy * s;
                for (int gx = 0; gx < mw; gx++)
                {
                    int cx = (gx == mw - 1) ? w - 1 : gx * s;
                    int vi = gy * mw + gx;

                    float wy = Mathf.Max(0f, mapData.GetHeightSigned01(cx, cy)) * heightScale;
                    verts[vi] = o + new Vector3((cx + 0.5f) * cell, wy, (cy + 0.5f) * cell);
                    uvs[vi] = new Vector2((cx + 0.5f) / w, (cy + 0.5f) / h);
                }
            }

            // 위를 향하는(법선 +Y) 와인딩: (a,c,b),(b,c,d)
            var tris = new int[(mw - 1) * (mh - 1) * 6];
            int ti = 0;
            for (int gy = 0; gy < mh - 1; gy++)
            {
                for (int gx = 0; gx < mw - 1; gx++)
                {
                    int a = gy * mw + gx;
                    int b = a + 1;
                    int c = a + mw;
                    int d = c + 1;
                    tris[ti++] = a; tris[ti++] = c; tris[ti++] = b;
                    tris[ti++] = b; tris[ti++] = c; tris[ti++] = d;
                }
            }

            if (_mesh == null)
                _mesh = new Mesh { name = "WorldMapTerrain", hideFlags = HideFlags.DontSave };
            _mesh.Clear();
            _mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; // 정점 65535 초과 대비
            _mesh.vertices = verts;
            _mesh.uv = uvs;
            _mesh.triangles = tris;
            _mesh.RecalculateNormals();
            _mesh.RecalculateBounds();
        }

        private void BuildMaterial()
        {
            if (_mat == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                                ?? Shader.Find("Standard");
                _mat = new Material(shader) { name = "WorldMapMat", hideFlags = HideFlags.DontSave };
                if (_mat.HasProperty("_Smoothness")) _mat.SetFloat("_Smoothness", 0f);
                if (_mat.HasProperty("_Metallic")) _mat.SetFloat("_Metallic", 0f);
            }
            if (_mat.HasProperty("_BaseMap")) _mat.SetTexture("_BaseMap", _tex);
            _mat.mainTexture = _tex;
        }

        private static Color32 PaletteFor(TerrainType t)
        {
            switch (t)
            {
                case TerrainType.DeepSea: return new Color32(30, 70, 140, 255);
                case TerrainType.ShallowSea: return new Color32(70, 130, 200, 255);
                case TerrainType.Land: return new Color32(95, 150, 70, 255);
                case TerrainType.Mountain: return new Color32(120, 110, 95, 255);
                case TerrainType.Port: return new Color32(225, 200, 80, 255);
                default: return new Color32(255, 0, 255, 255);
            }
        }

        private void CleanupGenerated()
        {
            CleanupTexture();
            DestroySafe(_mesh); _mesh = null;
            DestroySafe(_mat); _mat = null;
        }

        private void CleanupTexture() { DestroySafe(_tex); _tex = null; }

        private static void DestroySafe(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Destroy(o);
            else DestroyImmediate(o);
        }
    }
}

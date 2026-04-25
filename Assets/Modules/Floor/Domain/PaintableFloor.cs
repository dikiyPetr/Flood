using UnityEngine;

namespace Floor
{
    /// <summary>
    /// Раскрашиваемая поверхность: владеет RenderTexture-маской кисти, привязанной к материалу
    /// указанного рендерера, и предоставляет метод <see cref="PaintAt"/> для нанесения мазка.
    /// Кисть рисуется на GPU через blit-стампер (см. <c>Flood/FloorBrush</c>).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PaintableFloor : MonoBehaviour
    {
        [SerializeField] private PaintableFloorConfig _config;
        [SerializeField] private Renderer _targetRenderer;

        [SerializeField] private Shader _brushShader;

        // Развороты UV под ориентацию меша. Default Plane: оба off. Quad повёрнутый -90° по X: _invertV = on.
        private RenderTexture _paintRT;
        private RenderTexture _tempRT;
        private Material _materialInstance;
        private Material _brushMaterial;

        private void Reset()
        {
            _targetRenderer = GetComponent<Renderer>();
        }

        private void Awake()
        {
            if (_targetRenderer == null)
            {
                _targetRenderer = GetComponent<Renderer>();
            }

            var w = _config.TextureResolution.x;
            var h = _config.TextureResolution.y;
            _paintRT = CreateMaskRT(w, h);
            _tempRT = CreateMaskRT(w, h);
            ClearRT(_paintRT, _config.ClearData);

            _brushMaterial = new Material(_brushShader);

            // .material — изолированный инстанс, чтобы текстура не утекала на другие объекты,
            // использующие тот же исходный материал в проекте.
            _materialInstance = _targetRenderer.material;
            _materialInstance.SetTexture("_Paint_Mask", _paintRT);
        }

        private void OnDestroy()
        {
            if (_paintRT != null) _paintRT.Release();
            if (_tempRT != null) _tempRT.Release();
            if (_brushMaterial != null) Destroy(_brushMaterial);
        }

        /// <summary>
        /// Наносит мазок кисти на маску пола в проекции заданной мировой XZ-точки.
        /// </summary>
        public void PaintAt(Vector2 worldXZ)
        {
            var floorCenter = new Vector2(transform.position.x, transform.position.z);
            var local = worldXZ - floorCenter;
            var u = local.x / _config.WorldSize.x + 0.5f;
            var v = local.y / _config.WorldSize.y + 0.5f;
            // фикс зеркальной рисовки
            u = 1f - u;
            v = 1f - v;

            // Радиус кисти переводится из текселей в UV: 2 текселя на 128 ширины = 0.0156 UV.
            var radiusUV = _config.BrushRadiusInTexels / (float)_config.TextureResolution.x;
            var ageNormalized = (Time.time % _config.AgeCycleSeconds) / _config.AgeCycleSeconds;

            _brushMaterial.SetVector("_BrushUV", new Vector4(u, v, 0f, 0f));
            _brushMaterial.SetFloat("_BrushRadius", radiusUV);
            // SetVector вместо SetColor: обходим возможную gamma-конверсию, R/G/B/A идут как есть.
            _brushMaterial.SetVector("_BrushColor", new Vector4(1f, ageNormalized, 0f, 1f));

            // Ping-pong: brush blits painted result в temp, затем temp копируется обратно в main.
            // Source-as-dest в Blit — UB, поэтому промежуточный буфер обязателен.
            Graphics.Blit(_paintRT, _tempRT, _brushMaterial);
            Graphics.Blit(_tempRT, _paintRT);
        }

        private static RenderTexture CreateMaskRT(int width, int height)
        {
            // Linear ReadWrite — критично: иначе sRGB-конверсия перекосит G-канал (age).
            var rt = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                useMipMap = false,
                anisoLevel = 0,
            };
            rt.Create();
            return rt;
        }

        private static void ClearRT(RenderTexture rt, Color color)
        {
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            GL.Clear(false, true, color);
            RenderTexture.active = prev;
        }
    }
}
using System;
using UnityEngine;

namespace Floor
{
    /// <summary>
    /// Раскрашиваемая поверхность: владеет двумя RenderTexture-масками — постоянной территории
    /// (<c>_Paint_Mask</c>) и активного следа игрока (<c>_Line_Mask</c>) — обе привязаны к
    /// материалу указанного рендерера. Кисть рисуется на GPU через blit-стампер
    /// (см. <c>Flood/FloorBrush</c>).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PaintableFloor : MonoBehaviour
    {
        [SerializeField] private PaintableFloorConfig _config;
        [SerializeField] private Renderer _targetRenderer;

        [SerializeField] private Shader _brushShader;

        // Развороты UV под ориентацию меша. Default Plane: оба off. Quad повёрнутый -90° по X: _invertV = on.
        private RenderTexture _paintRT;
        private RenderTexture _lineRT;
        private RenderTexture _tempRT;
        private Material _materialInstance;
        private Material _brushMaterial;

        /// <summary>
        /// Стреляет после успешной закраски территории через <see cref="PaintAt"/>.
        /// Параметры: мировая XZ-точка кисти и радиус кисти в мировых единицах.
        /// Не стреляет на <see cref="PaintLineAt"/> — линия не территория.
        /// </summary>
        public event Action<Vector2, float> Painted;

        public Vector2 FloorCenterXZ => new Vector2(transform.position.x, transform.position.z);
        public Vector2 WorldSize => _config.WorldSize;

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
            _lineRT = CreateMaskRT(w, h);
            _tempRT = CreateMaskRT(w, h);
            ClearRT(_paintRT, _config.ClearData);
            ClearRT(_lineRT, _config.ClearData);

            _brushMaterial = new Material(_brushShader);

            // .material — изолированный инстанс, чтобы текстура не утекала на другие объекты,
            // использующие тот же исходный материал в проекте.
            _materialInstance = _targetRenderer.material;
            _materialInstance.SetTexture("_Paint_Mask", _paintRT);
            _materialInstance.SetTexture("_Line_Mask", _lineRT);
        }

        private void OnDestroy()
        {
            if (_paintRT != null) _paintRT.Release();
            if (_lineRT != null) _lineRT.Release();
            if (_tempRT != null) _tempRT.Release();
            if (_brushMaterial != null) Destroy(_brushMaterial);
        }

        /// <summary>
        /// Наносит мазок кисти на маску территории (<c>_paintRT</c>) в проекции заданной мировой XZ-точки.
        /// После успешной закраски стреляет <see cref="Painted"/>.
        /// </summary>
        public void PaintAt(Vector2 worldXZ)
        {
            BlitBrush(_paintRT, worldXZ, PaintBrushColor(), _config.BrushRadiusInTexels);
            var worldRadius = WorldRadiusOfBrush(_config.BrushRadiusInTexels);
            Painted?.Invoke(worldXZ, worldRadius);
        }

        /// <summary>
        /// То же, что <see cref="PaintAt"/>, но без стрельбы события <see cref="Painted"/> и с
        /// расширенным радиусом кисти (<see cref="PaintableFloorConfig.BrushRadiusInTexels"/> +
        /// <see cref="PaintableFloorConfig.FillBrushExtraRadiusInTexels"/>). Расширение нужно,
        /// чтобы мазки заливки от внутренних клеток перекрывали соседние линейные и
        /// продолжались за их центр на ширину линии — без зазора после стирания оверлея.
        ///
        /// Используется для батч-операций (FloodFillAnimator), где вызывающий сам управляет
        /// состоянием грида и event-обратная связь привела бы к преждевременной маркировке
        /// соседей как Territory и поломке фронта BFS.
        /// </summary>
        public void PaintAtSilent(Vector2 worldXZ)
        {
            var radius = _config.BrushRadiusInTexels + _config.FillBrushExtraRadiusInTexels;
            BlitBrush(_paintRT, worldXZ, PaintBrushColor(), radius);
        }

        /// <summary>
        /// Наносит мазок кисти на маску активного следа (<c>_lineRT</c>) в проекции заданной мировой XZ-точки.
        /// Не стреляет <see cref="Painted"/>: линия — отдельный визуальный слой, не часть территории.
        /// </summary>
        public void PaintLineAt(Vector2 worldXZ)
        {
            BlitBrush(_lineRT, worldXZ, PaintBrushColor(), _config.BrushRadiusInTexels);
        }

        /// <summary>
        /// Стирает диск под брашем на маске активного следа в проекции заданной точки.
        /// Шейдер браша делает <c>lerp(source, brushColor, inBrush)</c>, поэтому нулевой цвет
        /// корректно зануляет область диска. Радиус — базовый (как у PaintLineAt), чтобы
        /// erase точно совпадал с тем, что было нарисовано.
        /// </summary>
        public void EraseLineAt(Vector2 worldXZ)
        {
            BlitBrush(_lineRT, worldXZ, Vector4.zero, _config.BrushRadiusInTexels);
        }

        /// <summary>
        /// Очищает маску активного следа. Вызывается после замыкания петли.
        /// </summary>
        public void ClearLine()
        {
            ClearRT(_lineRT, _config.ClearData);
        }

        private void BlitBrush(RenderTexture target, Vector2 worldXZ, Vector4 brushColor, int radiusInTexels)
        {
            var floorCenter = FloorCenterXZ;
            var local = worldXZ - floorCenter;
            var u = local.x / _config.WorldSize.x + 0.5f;
            var v = local.y / _config.WorldSize.y + 0.5f;
            // фикс зеркальной рисовки
            u = 1f - u;
            v = 1f - v;

            var radiusUV = radiusInTexels / (float)_config.TextureResolution.x;

            _brushMaterial.SetVector("_BrushUV", new Vector4(u, v, 0f, 0f));
            _brushMaterial.SetFloat("_BrushRadius", radiusUV);
            // SetVector вместо SetColor: обходим возможную gamma-конверсию, R/G/B/A идут как есть.
            _brushMaterial.SetVector("_BrushColor", brushColor);

            // Ping-pong: brush blits painted result в temp, затем temp копируется обратно в target.
            // Source-as-dest в Blit — UB, поэтому промежуточный буфер обязателен.
            Graphics.Blit(target, _tempRT, _brushMaterial);
            Graphics.Blit(_tempRT, target);
        }

        private Vector4 PaintBrushColor()
        {
            var ageNormalized = (Time.time % _config.AgeCycleSeconds) / _config.AgeCycleSeconds;
            return new Vector4(1f, ageNormalized, 0f, 1f);
        }

        private float WorldRadiusOfBrush(int radiusInTexels)
        {
            var radiusUV = radiusInTexels / (float)_config.TextureResolution.x;
            return radiusUV * _config.WorldSize.x;
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

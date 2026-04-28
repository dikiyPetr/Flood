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

        /// <summary>
        /// Стреляет после <see cref="PaintAtSilent"/> с world-радиусом расширенной кисти заливки
        /// (<see cref="PaintableFloorConfig.BrushRadiusInTexels"/> +
        /// <see cref="PaintableFloorConfig.FillBrushExtraRadiusInTexels"/>). Используется
        /// <see cref="ArenaState"/> для синхронизации CPU-грида с GPU-bleed: мазок заливки
        /// перекрывает texel'ы соседних клеток, и без синка эти клетки остаются Empty в
        /// гриде, хотя визуально закрашены.
        /// </summary>
        public event Action<Vector2, float> PaintedSilent;

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
        /// То же, что <see cref="PaintAt(Vector2)"/>, но с явным радиусом в мировых единицах вместо
        /// <see cref="PaintableFloorConfig.BrushRadiusInTexels"/>. Симметрично <see cref="EraseAt"/>:
        /// world-радиус конвертируется в texel'ы через текущее разрешение текстуры. Событие
        /// <see cref="Painted"/> стреляется с фактическим world-радиусом после округления до texel'ов,
        /// чтобы CPU-грид и GPU-маска не разъехались.
        /// </summary>
        public void PaintAt(Vector2 worldXZ, float worldRadius)
        {
            var radiusUV = worldRadius / _config.WorldSize.x;
            var radiusInTexels = Mathf.Max(1, Mathf.CeilToInt(radiusUV * _config.TextureResolution.x));
            BlitBrush(_paintRT, worldXZ, PaintBrushColor(), radiusInTexels);
            Painted?.Invoke(worldXZ, WorldRadiusOfBrush(radiusInTexels));
        }

        /// <summary>
        /// То же, что <see cref="PaintAt"/>, но с расширенным радиусом кисти
        /// (<see cref="PaintableFloorConfig.BrushRadiusInTexels"/> +
        /// <see cref="PaintableFloorConfig.FillBrushExtraRadiusInTexels"/>). Расширение нужно,
        /// чтобы мазки заливки от внутренних клеток перекрывали соседние линейные и
        /// продолжались за их центр на ширину линии — без зазора после стирания оверлея.
        ///
        /// Стреляет отдельным событием <see cref="PaintedSilent"/> вместо <see cref="Painted"/>:
        /// для батч-заливки <see cref="FloodFillAnimator"/> аниматор сам уже пометил pending-клетки
        /// как Territory, но GPU-мазок может «забрызгать» соседние Empty-клетки за пределами
        /// pending-сета — их грид нужно догнать, иначе игрок и враги получают визуально-закрашенные,
        /// но грид-Empty области (десинк CPU/GPU). Подписчик-синкер (ArenaState) обязан пропускать
        /// Line-клетки, чтобы не превратить активный трейл в Territory.
        /// </summary>
        public void PaintAtSilent(Vector2 worldXZ)
        {
            var radius = _config.BrushRadiusInTexels + _config.FillBrushExtraRadiusInTexels;
            BlitBrush(_paintRT, worldXZ, PaintBrushColor(), radius);
            var worldRadius = WorldRadiusOfBrush(radius);
            PaintedSilent?.Invoke(worldXZ, worldRadius);
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
        /// Стирает диск на постоянной маске территории (<c>_paintRT</c>) в радиусе мировых единиц.
        /// Зеркало <see cref="EraseLineAt"/>, но для территории. Не стреляет <see cref="Painted"/> —
        /// событие предназначено только для добавления территории. Используется эрозией от
        /// врагов (GDD §3.2).
        /// </summary>
        public void EraseAt(Vector2 worldXZ, float worldRadius)
        {
            var radiusUV = worldRadius / _config.WorldSize.x;
            var radiusInTexels = Mathf.Max(1, Mathf.CeilToInt(radiusUV * _config.TextureResolution.x));
            BlitBrush(_paintRT, worldXZ, Vector4.zero, radiusInTexels);
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

using UnityEngine;

namespace Floor
{
    /// <summary>
    /// Конфигурация раскрашиваемого пола: размер в мире, разрешение текстуры, параметры кисти.
    /// </summary>
    [CreateAssetMenu(menuName = "Flood/Floor/Paintable Floor Config", fileName = "PaintableFloorConfig")]
    public sealed class PaintableFloorConfig : ScriptableObject
    {
        /// <summary>
        /// Размер пола в мировых единицах по осям X и Z.
        /// </summary>
        [field: SerializeField]
        public Vector2 WorldSize { get; private set; } = new Vector2(20f, 20f);

        /// <summary>
        /// Разрешение текстуры пола в пикселях.
        /// </summary>
        [field: SerializeField]
        public Vector2Int TextureResolution { get; private set; } = new Vector2Int(128, 128);

        /// <summary>
        /// Радиус кисти в пикселях текстуры.
        /// </summary>
        [field: SerializeField, Min(0)]
        public int BrushRadiusInTexels { get; private set; } = 2;

        /// <summary>
        /// Период (сек), за который нормализованное время нанесения в G-канале укладывается в [0..1].
        /// 8-битный G даёт 256 градаций — на 60 сек это шаг ~0.23 сек, достаточно для fade-in.
        /// </summary>
        [field: SerializeField, Min(1f)]
        public float AgeCycleSeconds { get; private set; } = 60f;

        /// <summary>
        /// Начальные данные текселя (mask=0 — пустота).
        /// </summary>
        [field: SerializeField]
        public Color ClearData { get; private set; } = new Color(0f, 0f, 0f, 0f);
    }
}

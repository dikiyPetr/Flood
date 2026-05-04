using UnityEngine;

namespace Floor
{
    /// <summary>
    /// Параметры арены: дискретизация (CPU-сетка территории и плотность GPU-маски),
    /// параметры кисти и цикла возраста. Размер мира берётся из <see cref="PaintableFloor.WorldSize"/>
    /// (вычисляется из bounds сценного рендерера) — единый источник истины.
    /// Один конфиг на арену; ссылается и <see cref="PaintableFloor"/>, и <see cref="ArenaState"/>
    /// (через <see cref="PaintableFloor.Config"/>). Тайминг волны заливки — отдельно в
    /// <see cref="FloodFillAnimatorConfig"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Flood/Floor/Arena Config", fileName = "ArenaConfig")]
    public sealed class ArenaConfig : ScriptableObject
    {
        [Header("Дискретизация")]

        /// <summary>
        /// Размерность сетки территории по обеим осям. Сетка квадратная.
        /// </summary>
        [field: SerializeField, Min(4)]
        public int GridResolution { get; private set; } = 64;

        /// <summary>
        /// Плотность маски: текселов на мировую единицу. Фактическое разрешение текстуры пола
        /// вычисляется в <see cref="PaintableFloor"/> на Awake как ceil(worldSize * TexelsPerWorldUnit)
        /// по каждой оси, где worldSize берётся из bounds корневого рендерера.
        /// </summary>
        [field: SerializeField, Min(0.01f)]
        public float TexelsPerWorldUnit { get; private set; } = 2.56f;

        [Header("Кисть")]

        /// <summary>
        /// Радиус кисти в пикселях текстуры. Используется для активного следа
        /// (<see cref="PaintableFloor.PaintLineAt"/>), стирания следа
        /// (<see cref="PaintableFloor.EraseLineAt"/>) и init-кисти
        /// (<see cref="PaintableFloor.PaintAt(Vector2)"/>).
        /// </summary>
        [field: SerializeField, Min(0)]
        public int BrushRadiusInTexels { get; private set; } = 2;

        /// <summary>
        /// Доп. радиус кисти заливки (<see cref="PaintableFloor.PaintAtSilent"/>) сверх
        /// <see cref="BrushRadiusInTexels"/>. Полный радиус заливочного мазка =
        /// <c>BrushRadiusInTexels + FillBrushExtraRadiusInTexels</c>. По умолчанию = ширине
        /// линии (диаметр базовой кисти = <c>BrushRadiusInTexels * 2</c>): мазок заливки
        /// от внутренней клетки перекрывает соседнюю линейную клетку и продолжается ещё на
        /// ширину линии за её центр. Это убирает зазор фона между залитой территорией и
        /// тем местом, где визуально оканчивалась линия, после её стирания.
        /// </summary>
        [field: SerializeField, Min(0)]
        public int FillBrushExtraRadiusInTexels { get; private set; } = 4;

        [Header("Цикл и очистка")]

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

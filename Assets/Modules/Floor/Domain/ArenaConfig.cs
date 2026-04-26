using UnityEngine;

namespace Floor
{
    /// <summary>
    /// Параметры арены: грид территории (CPU-зеркало рендертекстуры пола) и постепенная заливка.
    /// Размер мира берётся из <see cref="PaintableFloorConfig.WorldSize"/> — единый источник истины.
    /// </summary>
    [CreateAssetMenu(menuName = "Flood/Floor/Arena Config", fileName = "ArenaConfig")]
    public sealed class ArenaConfig : ScriptableObject
    {
        /// <summary>
        /// Размерность сетки территории по обеим осям. Сетка квадратная.
        /// По GDD: 64 клетки на сторону при 128-пиксельной текстуре (2 пикселя/клетка).
        /// </summary>
        [field: SerializeField, Min(4)]
        public int GridResolution { get; private set; } = 64;

        /// <summary>
        /// Интервал между тиками постепенной заливки (сек). Один тик = один слой
        /// фронта BFS, поэтому это единственный регулятор скорости волны.
        /// </summary>
        [field: SerializeField, Min(0.001f)]
        public float FillTickInterval { get; private set; } = 0.05f;
    }
}

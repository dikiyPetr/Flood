using Floor;
using UnityEngine;

namespace Navigation
{
    /// <summary>
    /// Параметры flow-field навигации: стоимости проходимости по типам клеток, частота rebuild'а,
    /// вес-смещение для seed'ов.
    /// </summary>
    [CreateAssetMenu(menuName = "Flood/Navigation/Navigation Config", fileName = "NavigationConfig")]
    public sealed class NavigationConfig : ScriptableObject
    {
        [SerializeField, Min(1)] private int _emptyCost = 1;
        [SerializeField, Min(1)] private int _territoryCost = 8;
        [SerializeField, Min(1)] private int _lineCost = 1;

        [Tooltip("Преобразование Weight цели в seed-интеграцию: seed = (1 − weight) * MaxSeedOffset. " +
                 "Чем больше — тем сильнее цели с малым весом «отталкивают» фронт.")]
        [SerializeField, Min(0)] private int _maxSeedOffset = 64;

        [Tooltip("Минимальный интервал между rebuild'ами (сек) — throttle. 0 = пересобирать сразу при dirty. " +
                 "Rebuild идёт асинхронно в Task.Run, поэтому даже большой интервал не блокирует main thread; " +
                 "но при 0 фон занят почти всегда, что грузит CPU без необходимости. " +
                 "Разумный диапазон для крупного грида (≥256): 0.1–0.3.")]
        [SerializeField, Min(0f)] private float _rebuildIntervalSeconds = 0f;

        [SerializeField] private bool _allowDiagonal = true;

        public int EmptyCost => _emptyCost;
        public int TerritoryCost => _territoryCost;
        public int LineCost => _lineCost;
        public int MaxSeedOffset => _maxSeedOffset;
        public float RebuildIntervalSeconds => _rebuildIntervalSeconds;
        public bool AllowDiagonal => _allowDiagonal;

        /// <summary>
        /// Sentinel-стоимость для непроходимой клетки. <see cref="int.MaxValue"/>/32 даёт
        /// запас от overflow в Dijkstra-релаксации (orthogonal mul=10, diag mul=14, поэтому
        /// запас в 32× безопасен на любое число шагов в пределах грида). Любая клетка с
        /// cost ≥ <see cref="ObstacleCost"/> считается препятствием — Dijkstra её не релаксирует.
        /// </summary>
        public const int ObstacleCost = int.MaxValue / 32;

        public int CostFor(CellState state)
        {
            switch (state)
            {
                case CellState.Empty: return _emptyCost;
                case CellState.Territory: return _territoryCost;
                case CellState.Line: return _lineCost;
                case CellState.Obstacle: return ObstacleCost;
                default: return _emptyCost;
            }
        }
    }
}

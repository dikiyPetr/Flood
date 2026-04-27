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

        [Tooltip("Минимальный интервал между rebuild'ами (сек). 0 = пересобирать сразу при dirty.")]
        [SerializeField, Min(0f)] private float _rebuildIntervalSeconds = 0f;

        [SerializeField] private bool _allowDiagonal = true;

        public int EmptyCost => _emptyCost;
        public int TerritoryCost => _territoryCost;
        public int LineCost => _lineCost;
        public int MaxSeedOffset => _maxSeedOffset;
        public float RebuildIntervalSeconds => _rebuildIntervalSeconds;
        public bool AllowDiagonal => _allowDiagonal;

        public int CostFor(CellState state)
        {
            switch (state)
            {
                case CellState.Empty: return _emptyCost;
                case CellState.Territory: return _territoryCost;
                case CellState.Line: return _lineCost;
                default: return _emptyCost;
            }
        }
    }
}

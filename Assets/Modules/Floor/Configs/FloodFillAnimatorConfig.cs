using UnityEngine;

namespace Floor
{
    /// <summary>
    /// Тюнинг анимации волны заливки <see cref="FloodFillAnimator"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Flood/Floor/Flood Fill Animator Config", fileName = "FloodFillAnimatorConfig")]
    public sealed class FloodFillAnimatorConfig : ScriptableObject
    {
        /// <summary>
        /// Интервал между тиками постепенной заливки (сек). Один тик = один слой
        /// фронта BFS, поэтому это единственный регулятор скорости волны.
        /// </summary>
        [field: SerializeField, Min(0.001f)]
        public float FillTickInterval { get; private set; } = 0.05f;
    }
}

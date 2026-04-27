using UnityEngine;

namespace Navigation
{
    /// <summary>
    /// Цель flow-field навигации. Используется как seed Dijkstra'а в <see cref="FlowFieldBuilder"/>.
    /// </summary>
    /// <remarks>
    /// <see cref="Weight"/> — нормированная привлекательность (0..1, 1 = максимум).
    /// Преобразуется в начальную стоимость seed-клетки: <c>seed = (1 − Weight) * MaxSeedOffset</c>.
    /// При нескольких целях клетка тяготеет к той, у которой weighted-расстояние меньше.
    /// </remarks>
    public readonly struct NavigationGoal
    {
        public readonly Vector2 WorldXZ;
        public readonly float Weight;

        public NavigationGoal(Vector2 worldXZ, float weight)
        {
            WorldXZ = worldXZ;
            Weight = Mathf.Clamp01(weight);
        }
    }
}

using UnityEngine;

namespace Navigation
{
    /// <summary>
    /// Снимок flow-field'а: cost-карта, integration (cumulative cost до ближайшей цели),
    /// direction (нормированный вектор-указатель к соседу с минимальной integration).
    /// Pure C#, переиспользуется in-place внутри <see cref="FlowFieldBuilder"/> (без аллокаций
    /// после первого Build при стабильном Resolution).
    /// </summary>
    public sealed class FlowField
    {
        public const int Unreachable = int.MaxValue;

        public int Resolution { get; }

        // Flat row-major: idx = x * Resolution + y. Та же ось, что и ArenaGrid.
        public int[] Cost { get; }
        public int[] Integration { get; }
        public Vector2[] Direction { get; }
        public bool[] GoalMask { get; }

        public FlowField(int resolution)
        {
            Resolution = resolution;
            var n = resolution * resolution;
            Cost = new int[n];
            Integration = new int[n];
            Direction = new Vector2[n];
            GoalMask = new bool[n];
        }

        public int IndexOf(int x, int y) => x * Resolution + y;
        public int IndexOf(Vector2Int cell) => cell.x * Resolution + cell.y;

        public bool IsInside(Vector2Int cell)
        {
            return cell.x >= 0 && cell.x < Resolution
                && cell.y >= 0 && cell.y < Resolution;
        }

        /// <summary>Направление потока в указанной клетке. <see cref="Vector2.zero"/> если клетка цель/недостижима.</summary>
        public Vector2 SampleDirection(Vector2Int cell)
        {
            if (!IsInside(cell)) return Vector2.zero;
            return Direction[IndexOf(cell)];
        }

        public int GetIntegration(Vector2Int cell)
        {
            if (!IsInside(cell)) return Unreachable;
            return Integration[IndexOf(cell)];
        }
    }
}

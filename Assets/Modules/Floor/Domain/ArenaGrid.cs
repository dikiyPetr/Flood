using System;
using UnityEngine;

namespace Floor
{
    /// <summary>
    /// CPU-зеркало текстуры пола в дискретной сетке клеток. Хранит состояние каждой клетки
    /// и предоставляет проекцию мир ↔ клетка. Размер мира и центр пола передаются извне —
    /// грид остаётся pure C# без зависимости от MonoBehaviour.
    /// </summary>
    public sealed class ArenaGrid
    {
        private readonly CellState[,] _cells;

        public int Resolution { get; }

        public ArenaGrid(int resolution)
        {
            Resolution = resolution;
            _cells = new CellState[resolution, resolution];
        }

        public CellState Get(Vector2Int cell)
        {
            return _cells[cell.x, cell.y];
        }

        public void Set(Vector2Int cell, CellState state)
        {
            _cells[cell.x, cell.y] = state;
        }

        public bool IsInside(Vector2Int cell)
        {
            return cell.x >= 0 && cell.x < Resolution
                && cell.y >= 0 && cell.y < Resolution;
        }

        /// <summary>
        /// Копирует состояние всех клеток в внешний буфер того же размера. Используется
        /// для snapshot'а перед фоновым вычислением (например, <see cref="EnclosedRegionFinder"/>):
        /// фон читает только snapshot, main thread свободно правит live grid без race.
        /// </summary>
        public void CopyCellsTo(CellState[,] dst)
        {
            Array.Copy(_cells, dst, _cells.Length);
        }

        /// <summary>
        /// Проектирует мировую XZ-точку на клетку грида. Использует ту же ось что и
        /// <see cref="FloorProjection.ProjectWorldToTexel"/> (без инверсии), поэтому
        /// результат остаётся согласован с самим собой при обратной операции
        /// <see cref="CellCenterWorld"/>.
        /// </summary>
        public Vector2Int WorldToCell(Vector2 worldXZ, Vector2 floorCenterXZ, Vector2 worldSize)
        {
            return FloorProjection.ProjectWorldToTexel(
                worldXZ,
                floorCenterXZ,
                worldSize,
                new Vector2Int(Resolution, Resolution));
        }

        /// <summary>
        /// Возвращает мировую XZ-точку центра клетки. Обратное к <see cref="WorldToCell"/>.
        /// </summary>
        public Vector2 CellCenterWorld(Vector2Int cell, Vector2 floorCenterXZ, Vector2 worldSize)
        {
            var u = (cell.x + 0.5f) / Resolution;
            var v = (cell.y + 0.5f) / Resolution;
            var localX = (u - 0.5f) * worldSize.x;
            var localY = (v - 0.5f) * worldSize.y;
            return floorCenterXZ + new Vector2(localX, localY);
        }
    }
}

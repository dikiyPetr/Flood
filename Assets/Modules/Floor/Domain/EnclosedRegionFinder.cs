using System.Collections.Generic;
using UnityEngine;

namespace Floor
{
    /// <summary>
    /// Находит клетки <see cref="CellState.Empty"/>, недостижимые BFS-обходом 4-связности
    /// от Empty-клеток на краях сетки. Такие клетки замкнуты территорией и/или линией —
    /// они становятся целью flood fill.
    /// </summary>
    public static class EnclosedRegionFinder
    {
        private static readonly Vector2Int[] Dirs =
        {
            new Vector2Int(1, 0),
            new Vector2Int(-1, 0),
            new Vector2Int(0, 1),
            new Vector2Int(0, -1),
        };

        public static List<Vector2Int> FindEnclosed(ArenaGrid grid)
        {
            var resolution = grid.Resolution;
            var visited = new bool[resolution, resolution];
            var queue = new Queue<Vector2Int>();

            for (var i = 0; i < resolution; i++)
            {
                TrySeed(grid, visited, queue, new Vector2Int(i, 0));
                TrySeed(grid, visited, queue, new Vector2Int(i, resolution - 1));
                TrySeed(grid, visited, queue, new Vector2Int(0, i));
                TrySeed(grid, visited, queue, new Vector2Int(resolution - 1, i));
            }

            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                for (var d = 0; d < Dirs.Length; d++)
                {
                    var n = cell + Dirs[d];
                    if (!grid.IsInside(n)) continue;
                    if (visited[n.x, n.y]) continue;
                    if (grid.Get(n) != CellState.Empty) continue;
                    visited[n.x, n.y] = true;
                    queue.Enqueue(n);
                }
            }

            var enclosed = new List<Vector2Int>();
            for (var x = 0; x < resolution; x++)
            {
                for (var y = 0; y < resolution; y++)
                {
                    if (visited[x, y]) continue;
                    var cell = new Vector2Int(x, y);
                    if (grid.Get(cell) == CellState.Empty)
                    {
                        enclosed.Add(cell);
                    }
                }
            }

            return enclosed;
        }

        private static void TrySeed(ArenaGrid grid, bool[,] visited, Queue<Vector2Int> queue, Vector2Int cell)
        {
            if (visited[cell.x, cell.y]) return;
            if (grid.Get(cell) != CellState.Empty) return;
            visited[cell.x, cell.y] = true;
            queue.Enqueue(cell);
        }
    }
}

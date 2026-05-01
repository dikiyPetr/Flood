using System.Collections.Generic;
using UnityEngine;

namespace Floor
{
    /// <summary>
    /// Находит Empty-клетки, охваченные именно текущим замыканием — те, что
    /// (а) не достижимы BFS от краёв арены через Empty (т.е. отрезаны Territory/Line)
    /// и (б) 4-связно соединены через Empty-клетки с одной из <c>lineCells</c>
    /// текущего трейла. Старые «дыры» от прерванных заливок сюда не попадают —
    /// их новое замыкание не должно автоматически переоткрывать.
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

        public static List<Vector2Int> FindEnclosed(ArenaGrid grid, IReadOnlyList<Vector2Int> lineCells)
        {
            var resolution = grid.Resolution;
            var outside = new bool[resolution, resolution];
            var queue = new Queue<Vector2Int>();

            // Фаза 1: BFS от краёв арены через Empty. Всё достижимое — «снаружи».
            for (var i = 0; i < resolution; i++)
            {
                TrySeed(grid, outside, queue, new Vector2Int(i, 0));
                TrySeed(grid, outside, queue, new Vector2Int(i, resolution - 1));
                TrySeed(grid, outside, queue, new Vector2Int(0, i));
                TrySeed(grid, outside, queue, new Vector2Int(resolution - 1, i));
            }

            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                for (var d = 0; d < Dirs.Length; d++)
                {
                    var n = cell + Dirs[d];
                    if (!grid.IsInside(n)) continue;
                    if (outside[n.x, n.y]) continue;
                    if (grid.Get(n) != CellState.Empty) continue;
                    outside[n.x, n.y] = true;
                    queue.Enqueue(n);
                }
            }

            // Фаза 2: BFS от Empty-соседей line-клеток через Empty (не-outside).
            // Стенами являются Territory, Line и outside-клетки. Достигнутые Empty-клетки
            // — это и есть enclosed-регион ИМЕННО этого замыкания.
            var enclosedVisited = new bool[resolution, resolution];
            var enclosed = new List<Vector2Int>();

            if (lineCells != null)
            {
                for (var i = 0; i < lineCells.Count; i++)
                {
                    var lineCell = lineCells[i];
                    for (var d = 0; d < Dirs.Length; d++)
                    {
                        var n = lineCell + Dirs[d];
                        if (!grid.IsInside(n)) continue;
                        if (enclosedVisited[n.x, n.y]) continue;
                        if (outside[n.x, n.y]) continue;
                        if (grid.Get(n) != CellState.Empty) continue;
                        enclosedVisited[n.x, n.y] = true;
                        queue.Enqueue(n);
                        enclosed.Add(n);
                    }
                }
            }

            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                for (var d = 0; d < Dirs.Length; d++)
                {
                    var n = cell + Dirs[d];
                    if (!grid.IsInside(n)) continue;
                    if (enclosedVisited[n.x, n.y]) continue;
                    if (outside[n.x, n.y]) continue;
                    if (grid.Get(n) != CellState.Empty) continue;
                    enclosedVisited[n.x, n.y] = true;
                    queue.Enqueue(n);
                    enclosed.Add(n);
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

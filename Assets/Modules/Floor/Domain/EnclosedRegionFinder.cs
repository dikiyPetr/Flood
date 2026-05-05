using System;
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
    ///
    /// Instance-class: буферы <c>_outside</c>/<c>_enclosedVisited</c>/<c>_queue</c>/<c>_enclosed</c>
    /// выделяются один раз в конструкторе и переиспользуются между вызовами через
    /// <see cref="Array.Clear(Array,int,int)"/>/<see cref="List{T}.Clear"/>. На <c>GridResolution=500</c>
    /// это снимает ~500 КБ managed-аллокаций на каждое замыкание.
    ///
    /// Thread-safe для использования из <see cref="System.Threading.Tasks.Task"/>: <see cref="FindEnclosed"/>
    /// читает только <paramref name="gridSnapshot"/> (передаваемая копия), live <see cref="ArenaGrid"/>
    /// не трогает. Конкурентные вызовы запрещены — finder имеет mutable state. Если фон ещё работает,
    /// новое замыкание должно либо ждать, либо запускаться в другом инстансе finder'а.
    /// </summary>
    public sealed class EnclosedRegionFinder
    {
        private static readonly Vector2Int[] Dirs =
        {
            new Vector2Int(1, 0),
            new Vector2Int(-1, 0),
            new Vector2Int(0, 1),
            new Vector2Int(0, -1),
        };

        private readonly int _resolution;
        private readonly bool[,] _outside;
        private readonly bool[,] _enclosedVisited;
        private readonly Queue<Vector2Int> _queue = new Queue<Vector2Int>();
        private readonly List<Vector2Int> _enclosed = new List<Vector2Int>();

        public EnclosedRegionFinder(int resolution)
        {
            _resolution = resolution;
            _outside = new bool[resolution, resolution];
            _enclosedVisited = new bool[resolution, resolution];
        }

        /// <summary>
        /// Двухфазный BFS. Принимает snapshot (CPU-копия) грида — не live <see cref="ArenaGrid"/>,
        /// чтобы метод можно было вызывать из background thread без race. Возвращает поле-список
        /// <c>_enclosed</c>; следующий вызов <see cref="FindEnclosed"/> его очистит, поэтому
        /// потребитель должен скопировать результат до повторного вызова.
        /// </summary>
        public IReadOnlyList<Vector2Int> FindEnclosed(CellState[,] gridSnapshot, IReadOnlyList<Vector2Int> lineCells)
        {
            Array.Clear(_outside, 0, _outside.Length);
            Array.Clear(_enclosedVisited, 0, _enclosedVisited.Length);
            _queue.Clear();
            _enclosed.Clear();

            // Фаза 1: BFS от краёв арены через Empty. Всё достижимое — «снаружи».
            for (var i = 0; i < _resolution; i++)
            {
                TrySeed(gridSnapshot, new Vector2Int(i, 0));
                TrySeed(gridSnapshot, new Vector2Int(i, _resolution - 1));
                TrySeed(gridSnapshot, new Vector2Int(0, i));
                TrySeed(gridSnapshot, new Vector2Int(_resolution - 1, i));
            }

            while (_queue.Count > 0)
            {
                var cell = _queue.Dequeue();
                for (var d = 0; d < Dirs.Length; d++)
                {
                    var n = cell + Dirs[d];
                    if (!IsInside(n)) continue;
                    if (_outside[n.x, n.y]) continue;
                    if (gridSnapshot[n.x, n.y] != CellState.Empty) continue;
                    _outside[n.x, n.y] = true;
                    _queue.Enqueue(n);
                }
            }

            // Фаза 2: BFS от Empty-соседей line-клеток через Empty (не-outside).
            // Стенами являются Territory, Line и outside-клетки. Достигнутые Empty-клетки
            // — это и есть enclosed-регион ИМЕННО этого замыкания.
            if (lineCells != null)
            {
                for (var i = 0; i < lineCells.Count; i++)
                {
                    var lineCell = lineCells[i];
                    for (var d = 0; d < Dirs.Length; d++)
                    {
                        var n = lineCell + Dirs[d];
                        if (!IsInside(n)) continue;
                        if (_enclosedVisited[n.x, n.y]) continue;
                        if (_outside[n.x, n.y]) continue;
                        if (gridSnapshot[n.x, n.y] != CellState.Empty) continue;
                        _enclosedVisited[n.x, n.y] = true;
                        _queue.Enqueue(n);
                        _enclosed.Add(n);
                    }
                }
            }

            while (_queue.Count > 0)
            {
                var cell = _queue.Dequeue();
                for (var d = 0; d < Dirs.Length; d++)
                {
                    var n = cell + Dirs[d];
                    if (!IsInside(n)) continue;
                    if (_enclosedVisited[n.x, n.y]) continue;
                    if (_outside[n.x, n.y]) continue;
                    if (gridSnapshot[n.x, n.y] != CellState.Empty) continue;
                    _enclosedVisited[n.x, n.y] = true;
                    _queue.Enqueue(n);
                    _enclosed.Add(n);
                }
            }

            return _enclosed;
        }

        private bool IsInside(Vector2Int cell)
        {
            return cell.x >= 0 && cell.x < _resolution
                && cell.y >= 0 && cell.y < _resolution;
        }

        private void TrySeed(CellState[,] gridSnapshot, Vector2Int cell)
        {
            if (_outside[cell.x, cell.y]) return;
            if (gridSnapshot[cell.x, cell.y] != CellState.Empty) return;
            _outside[cell.x, cell.y] = true;
            _queue.Enqueue(cell);
        }
    }
}

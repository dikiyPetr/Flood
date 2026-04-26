using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Floor
{
    /// <summary>
    /// Анимирует постепенную заливку замкнутого региона BFS-волной от клеток-источников
    /// (соседи которых — <see cref="CellState.Territory"/>) внутрь региона. За тик
    /// заполняет до <see cref="ArenaConfig.FillCellsPerTick"/> клеток с интервалом
    /// <see cref="ArenaConfig.FillTickInterval"/>.
    ///
    /// Поддерживает мерж нескольких <see cref="Enqueue"/> в одну активную корутину.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FloodFillAnimator : MonoBehaviour
    {
        private static readonly Vector2Int[] Dirs =
        {
            new Vector2Int(1, 0),
            new Vector2Int(-1, 0),
            new Vector2Int(0, 1),
            new Vector2Int(0, -1),
        };

        [SerializeField] private ArenaState _state;

        private readonly Queue<Vector2Int> _front = new Queue<Vector2Int>();
        private readonly HashSet<Vector2Int> _regionSet = new HashSet<Vector2Int>();
        private bool[,] _inFront;
        private Coroutine _routine;

        public void Enqueue(IReadOnlyList<Vector2Int> region)
        {
            if (region == null || region.Count == 0) return;

            var grid = _state.Grid;
            if (_inFront == null)
            {
                _inFront = new bool[grid.Resolution, grid.Resolution];
            }

            for (var i = 0; i < region.Count; i++)
            {
                _regionSet.Add(region[i]);
            }

            for (var i = 0; i < region.Count; i++)
            {
                var cell = region[i];
                if (HasTerritoryNeighbor(grid, cell))
                {
                    PushToFront(cell);
                }
            }

            if (_routine == null && _front.Count > 0)
            {
                _routine = StartCoroutine(FillRoutine());
            }
        }

        private IEnumerator FillRoutine()
        {
            var config = _state.Config;
            var grid = _state.Grid;
            var floor = _state.Floor;
            var wait = new WaitForSeconds(config.FillTickInterval);

            while (_front.Count > 0)
            {
                for (var i = 0; i < config.FillCellsPerTick && _front.Count > 0; i++)
                {
                    var cell = _front.Dequeue();
                    _regionSet.Remove(cell);

                    // Клетка уже Territory (например, OnPainted от соседнего PaintAt-всплеска
                    // успел её пометить через event) — пропускаем заливку и расширение.
                    if (grid.Get(cell) == CellState.Territory) continue;

                    grid.Set(cell, CellState.Territory);
                    // Silent: Painted event от PaintAt привёл бы OnPainted к маркировке соседних
                    // клеток как Territory, и expand BFS пропустил бы их (они уже не Empty).
                    floor.PaintAtSilent(grid.CellCenterWorld(cell, floor.FloorCenterXZ, floor.WorldSize));

                    for (var d = 0; d < Dirs.Length; d++)
                    {
                        var n = cell + Dirs[d];
                        if (!grid.IsInside(n)) continue;
                        if (_inFront[n.x, n.y]) continue;
                        if (!_regionSet.Contains(n)) continue;
                        if (grid.Get(n) != CellState.Empty) continue;
                        PushToFront(n);
                    }
                }

                yield return wait;
            }

            ResetState();
        }

        private void PushToFront(Vector2Int cell)
        {
            if (_inFront[cell.x, cell.y]) return;
            _inFront[cell.x, cell.y] = true;
            _front.Enqueue(cell);
        }

        private bool HasTerritoryNeighbor(ArenaGrid grid, Vector2Int cell)
        {
            for (var d = 0; d < Dirs.Length; d++)
            {
                var n = cell + Dirs[d];
                if (grid.IsInside(n) && grid.Get(n) == CellState.Territory) return true;
            }
            return false;
        }

        private void ResetState()
        {
            _regionSet.Clear();
            if (_inFront != null)
            {
                Array.Clear(_inFront, 0, _inFront.Length);
            }
            _routine = null;
        }
    }
}

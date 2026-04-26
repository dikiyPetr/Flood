using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Floor
{
    /// <summary>
    /// Постоянно работающий painter: каждый тик пытается продвинуть заливку по
    /// "ожидающим закраски" клеткам, при пустоте — ждёт. Не запускается извне:
    /// замыкание петли только дописывает данные в <see cref="AddPending"/> /
    /// <see cref="AddLineCleanup"/>, painter подхватывает их сам на следующем тике.
    ///
    /// Контракт состояний грида: к моменту <see cref="AddPending"/> переданные
    /// клетки уже <see cref="CellState.Territory"/> (закрытая область). Painter
    /// не меняет состояние грида, только наносит мазок на <c>_paintRT</c>.
    ///
    /// Заливка — BFS по <see cref="_pendingPaint"/> от уже **закрашенных**
    /// Territory-соседей. "Закрашенная" = Territory в гриде, **не** в
    /// <see cref="_pendingPaint"/> и **не** в <see cref="_pendingLines"/>.
    /// Сидинг через линии (conduit): если у enclosed-клетки сосед — клетка из
    /// <see cref="_pendingLines"/>, у которой есть закрашенный сосед, она тоже
    /// становится сидом. Это спасает кейс петли в чистой пустоте от маленькой
    /// базы — линия касается базы только в концах, и без conduit-правила фронт
    /// не запустится.
    ///
    /// Линии (<see cref="_pendingLines"/>) painter не закрашивает — соседние
    /// enclosed-стампы с радиусом 1 клетки + билинейная фильтрация и
    /// <c>smoothstep</c> в шейдере (PaintableFloorFunctions.hlsl:59) дают
    /// видимую границу территории ровно по центру старой линейной клетки.
    /// Когда <see cref="_pendingPaint"/> опустошён, painter одним пакетом
    /// стирает <see cref="_pendingLines"/> на <c>_lineRT</c> (через
    /// <see cref="PaintableFloor.EraseLineAt"/>).
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

        private readonly HashSet<Vector2Int> _pendingPaint = new HashSet<Vector2Int>();
        private readonly HashSet<Vector2Int> _pendingLines = new HashSet<Vector2Int>();
        private readonly Queue<Vector2Int> _front = new Queue<Vector2Int>();
        private bool[,] _inFront;
        private Coroutine _routine;

        /// <summary>
        /// Регистрирует клетки закрытой области, которым нужен мазок на
        /// <c>_paintRT</c>. Состояние грида у этих клеток должно уже быть
        /// <see cref="CellState.Territory"/>. Идемпотентно: повторное добавление
        /// той же клетки — no-op.
        /// </summary>
        public void AddPending(IReadOnlyList<Vector2Int> cells)
        {
            if (cells == null || cells.Count == 0) return;
            for (var i = 0; i < cells.Count; i++)
            {
                _pendingPaint.Add(cells[i]);
            }
        }

        /// <summary>
        /// Регистрирует клетки уже-закрытой линии для отложенного стирания
        /// оверлея <c>_lineRT</c>. Стирание произойдёт, когда фронт фазы
        /// закраски опустеет.
        /// </summary>
        public void AddLineCleanup(IReadOnlyList<Vector2Int> cells)
        {
            if (cells == null || cells.Count == 0) return;
            for (var i = 0; i < cells.Count; i++)
            {
                _pendingLines.Add(cells[i]);
            }
        }

        private void OnEnable()
        {
            if (_routine == null)
            {
                _routine = StartCoroutine(Loop());
            }
        }

        private void OnDisable()
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }
        }

        private IEnumerator Loop()
        {
            // Инициализация _inFront отложена — _state.Grid создаётся в Awake,
            // OnEnable может прийти до этого в зависимости от порядка скриптов.
            while (_state == null || _state.Grid == null)
            {
                yield return null;
            }

            var resolution = _state.Grid.Resolution;
            _inFront = new bool[resolution, resolution];

            var wait = new WaitForSeconds(_state.Config.FillTickInterval);

            while (true)
            {
                ProcessTick();
                yield return wait;
            }
        }

        private void ProcessTick()
        {
            var grid = _state.Grid;
            var floor = _state.Floor;
            var floorCenter = floor.FloorCenterXZ;
            var worldSize = floor.WorldSize;

            // 1. Пополняем фронт сидами, если он пуст. Сид = inside-клетка
            // (НЕ из _pendingLines), у которой есть закрашенный сосед или conduit
            // через концевую line-клетку, касающуюся базы.
            if (_front.Count == 0 && _pendingPaint.Count > 0)
            {
                foreach (var cell in _pendingPaint)
                {
                    if (HasReachableNeighbor(grid, cell))
                    {
                        PushToFront(cell);
                    }
                }
            }

            // 2. Обрабатываем ВЕСЬ текущий слой BFS за тик — фронт продвигается
            // на одну клетку равномерно во все стороны, как волна. Снимок размера
            // фиксирует «текущий слой»: новые соседи, которые расширение пушит
            // в хвост очереди, остаются на следующий тик.
            //
            // Inside-клетка → мазок PaintAtSilent. Line-клетка (в _pendingLines)
            // → только EraseLineAt, без мазка: соседние inside-стампы с расширенным
            // радиусом перекрывают её _paintRT-текселы. Линия во фронте нужна
            // как мостик BFS к изолированным enclosed-регионам (самопересечение).
            var layerSize = _front.Count;
            for (var i = 0; i < layerSize; i++)
            {
                var cell = _front.Dequeue();
                _inFront[cell.x, cell.y] = false;

                if (!_pendingPaint.Remove(cell)) continue;
                // Защита от рассинхрона грида.
                if (grid.Get(cell) != CellState.Territory) continue;

                var world = grid.CellCenterWorld(cell, floorCenter, worldSize);
                if (_pendingLines.Remove(cell))
                {
                    floor.EraseLineAt(world);
                }
                else
                {
                    floor.PaintAtSilent(world);
                }

                // Расширяем фронт на pending-соседей (без различения inside/line —
                // BFS должен пройти насквозь, чтобы добраться до inner-регионов).
                for (var d = 0; d < Dirs.Length; d++)
                {
                    var n = cell + Dirs[d];
                    if (!grid.IsInside(n)) continue;
                    if (_inFront[n.x, n.y]) continue;
                    if (!_pendingPaint.Contains(n)) continue;
                    PushToFront(n);
                }
            }

            // 3. Подстраховка: если в _pendingLines остались клетки без
            // соответствия в _pendingPaint (теоретически не должно быть —
            // мы добавляем их парой), стираем пакетом.
            if (_pendingPaint.Count == 0 && _pendingLines.Count > 0)
            {
                foreach (var line in _pendingLines)
                {
                    floor.EraseLineAt(grid.CellCenterWorld(line, floorCenter, worldSize));
                }
                _pendingLines.Clear();
            }
        }

        private void PushToFront(Vector2Int cell)
        {
            if (_inFront[cell.x, cell.y]) return;
            _inFront[cell.x, cell.y] = true;
            _front.Enqueue(cell);
        }

        private bool HasReachableNeighbor(ArenaGrid grid, Vector2Int cell)
        {
            // Line-клетка не может быть начальным сидом — она достигается BFS-расширением
            // от inside-клеток. Это сохраняет "фронт от существующей территории" даже
            // когда концы линии касаются базы (иначе BFS пошёл бы и от линии).
            if (_pendingLines.Contains(cell)) return false;

            for (var d = 0; d < Dirs.Length; d++)
            {
                var n = cell + Dirs[d];
                if (!grid.IsInside(n)) continue;
                if (grid.Get(n) != CellState.Territory) continue;
                var isLine = _pendingLines.Contains(n);
                var isPending = _pendingPaint.Contains(n);
                if (isLine)
                {
                    // Conduit: pending line-клетка с закрашенным соседом (концом у базы).
                    // Запускает фронт от inside через линию.
                    if (HasPaintedTerritoryNeighbor(grid, n)) return true;
                }
                else if (!isPending)
                {
                    // Уже закрашенная Territory.
                    return true;
                }
            }
            return false;
        }

        private bool HasPaintedTerritoryNeighbor(ArenaGrid grid, Vector2Int cell)
        {
            for (var d = 0; d < Dirs.Length; d++)
            {
                var n = cell + Dirs[d];
                if (!grid.IsInside(n)) continue;
                if (grid.Get(n) != CellState.Territory) continue;
                if (_pendingPaint.Contains(n)) continue;
                if (_pendingLines.Contains(n)) continue;
                return true;
            }
            return false;
        }
    }
}

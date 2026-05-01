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
    /// Сид-кандидатами могут быть и inside-, и line-клетки: line-сиды нужны
    /// для тонких петель ширины 1 (вперёд-назад), где inside-клеток просто нет.
    /// Сидинг через линии (conduit): если у enclosed-клетки сосед — клетка из
    /// <see cref="_pendingLines"/>, у которой есть закрашенный сосед, она тоже
    /// становится сидом. Спасает кейс петли в чистой пустоте от маленькой базы —
    /// линия касается базы только в концах, изолированная inner-область пускает
    /// фронт сквозь концевую line-клетку.
    ///
    /// Линии (<see cref="_pendingLines"/>) painter закрашивает наравне с inside,
    /// но бесплатно (без <see cref="PaintBank.TryConsume"/>) и с одновременным
    /// стиранием оверлея <c>_lineRT</c>. Это даёт минимальную ширину Territory=1
    /// для тонких петель «вперёд-назад» (без inside-соседей), а для обычных петель
    /// — лёгкий double-paint на границе, безвреден.
    /// Когда <see cref="_pendingPaint"/> опустошён, painter одним пакетом
    /// стирает остаток <see cref="_pendingLines"/> на <c>_lineRT</c> (через
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
        [SerializeField] private PaintBank _paintBank;

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

            // 1. Пополняем фронт сидами каждый тик. Сид = inside-клетка (НЕ из
            // _pendingLines), у которой есть закрашенный сосед или conduit через
            // концевую line-клетку, касающуюся базы.
            //
            // Re-seed на каждом тике (а не «только если фронт пуст») — это позволяет
            // нескольким одновременно-живущим замыканиям заливаться параллельно: если
            // игрок замкнул вторую петлю пока первая ещё разливается, её граничные
            // клетки сидируются сразу, не ожидая завершения первой волны. Внутри одной
            // области BFS-волна сохраняется: cells, уже стоящие в _front, отсеиваются
            // через PushToFront/_inFront, а deeper-cells той же волны остаются вне
            // фронта пока их соседи pending — HasReachableNeighbor требует НЕ-pending
            // закрашенного соседа.
            if (_pendingPaint.Count > 0)
            {
                foreach (var cell in _pendingPaint)
                {
                    if (_inFront[cell.x, cell.y]) continue;
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

                // Contains+Remove на коммите вместо одношагового Remove: при отказе TryConsume
                // клетка остаётся в _pendingPaint и попадает под откат в AbortFill.
                if (!_pendingPaint.Contains(cell)) continue;
                // Защита от рассинхрона грида.
                if (grid.Get(cell) != CellState.Territory)
                {
                    _pendingPaint.Remove(cell);
                    continue;
                }

                var world = grid.CellCenterWorld(cell, floorCenter, worldSize);
                if (_pendingLines.Contains(cell))
                {
                    // Line-клетки бесплатны (GDD §3.1: расход 1 ед./клетка заливки —
                    // только inside). Мазок ставим — соседние inside-стампы расширенным
                    // радиусом и так перекроют, но для тонких петель ширины 1 (вперёд-назад
                    // по той же тропе) inside-соседей нет, и без мазка линия осталась бы
                    // невидимой Territory.
                    _pendingLines.Remove(cell);
                    _pendingPaint.Remove(cell);
                    floor.PaintAtSilent(world);
                    floor.EraseLineAt(world);
                }
                else
                {
                    if (_paintBank != null && !_paintBank.TryConsume(1))
                    {
                        AbortFill(grid, floor, floorCenter, worldSize);
                        return;
                    }
                    _pendingPaint.Remove(cell);
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

        /// <summary>
        /// Краска кончилась посреди заливки (GDD §3.1). Останавливает текущую заливку
        /// и приводит арену в консистентное состояние:
        /// 1. <c>_pendingLines</c> — стираем с <c>_lineRT</c> и возвращаем грид в
        ///    <see cref="CellState.Empty"/>. ResolveClosure пометил их Territory в
        ///    предположении, что заливка пройдёт; без неё грид-Territory без визуала
        ///    оставит «невидимую стену» для игрока и врагов.
        /// 2. <c>_pendingPaint</c> (без line-клеток, обработанных выше) — откатываем
        ///    в <see cref="CellState.Empty"/> (по GDD «оставшаяся часть области
        ///    остаётся пустотой»). Уже закрашенные клетки (вышедшие из <c>_pendingPaint</c>
        ///    через <see cref="PaintableFloor.PaintAtSilent"/>) остаются Territory.
        /// 3. <c>_front</c>/<c>_inFront</c> — обнуляем, чтобы следующее замыкание стартовало
        ///    с чистого фронта.
        ///
        /// НЕ трогает активный (ещё не замкнутый) трейл игрока: его клетки <see cref="CellState.Line"/>
        /// в гриде, но вне <c>_pendingPaint</c>/<c>_pendingLines</c>.
        /// </summary>
        private void AbortFill(ArenaGrid grid, PaintableFloor floor, Vector2 floorCenter, Vector2 worldSize)
        {
            foreach (var cell in _pendingLines)
            {
                var world = grid.CellCenterWorld(cell, floorCenter, worldSize);
                floor.EraseLineAt(world);
                grid.Set(cell, CellState.Empty);
            }
            foreach (var cell in _pendingPaint)
            {
                if (_pendingLines.Contains(cell)) continue;
                grid.Set(cell, CellState.Empty);
            }
            _front.Clear();
            if (_inFront != null) Array.Clear(_inFront, 0, _inFront.Length);
            _pendingPaint.Clear();
            _pendingLines.Clear();
            Debug.Log("[Paint] fill aborted, paint depleted");
        }

        private bool HasReachableNeighbor(ArenaGrid grid, Vector2Int cell)
        {
            // Line-клетки тоже могут быть сидами: для тонких петель ширины 1 (вперёд-назад
            // по той же тропе) inside-клеток вообще нет, и без права быть сидом ни одна
            // line-клетка не запустила бы фронт — заливка зависла бы вечно. Для обычных
            // петель это лишь означает, что концевые line-клетки у базы засеются вместе с
            // inside; фронт всё равно сойдётся в волну.

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

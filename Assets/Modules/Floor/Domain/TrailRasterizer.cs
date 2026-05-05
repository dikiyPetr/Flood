using System.Collections.Generic;
using UnityEngine;

namespace Floor
{
    /// <summary>
    /// Растеризует траекторию игрока в клетки <see cref="ArenaGrid"/> 4-связным Bresenham от
    /// последней зарегистрированной клетки до новой. Empty-клетки помечаются как
    /// <see cref="CellState.Line"/> с параллельным мазком кисти на маске линии.
    /// Трейл живёт в двух фазах: "спящей" (ни одной Line-клетки) и "активной". В спящей фазе
    /// Bresenham проходит сквозь <see cref="CellState.Territory"/>-клетки без эффекта (движение
    /// игрока внутри заливки), в активной — попадание в Territory детектится как замыкание.
    /// Попадание в существующую <see cref="CellState.Line"/> — no-op (self-hit без последствий
    /// на этапах 1–4; смерть от собственного следа добавляется в дальнейшем).
    ///
    /// <see cref="ActiveLineCells"/> ведётся инкрементально: при пометке клетки Line — добавляется,
    /// при <see cref="Reset"/>/<see cref="ResetAfterClosure"/> — очищается. Снимает full-grid scan
    /// в <see cref="ArenaState.ResolveClosure"/>/<see cref="ArenaState.ClearActiveTrail"/> на 500×500.
    /// </summary>
    public sealed class TrailRasterizer
    {
        private readonly ArenaGrid _grid;
        private readonly PaintableFloor _floor;
        private readonly List<Vector2Int> _activeLineCells = new List<Vector2Int>();
        private Vector2Int? _lastCell;
        private bool _trailHasLineCells;

        /// <summary>
        /// Снимок Line-клеток текущей активной фазы трейла. Список переиспользуется между
        /// замыканиями: после <see cref="ResetAfterClosure"/> он пуст. Потребитель должен либо
        /// скопировать данные перед сбросом, либо обработать список до сброса.
        /// </summary>
        public IReadOnlyList<Vector2Int> ActiveLineCells => _activeLineCells;

        public TrailRasterizer(ArenaGrid grid, PaintableFloor floor)
        {
            _grid = grid;
            _floor = floor;
        }

        public void Reset()
        {
            _lastCell = null;
            _trailHasLineCells = false;
            _activeLineCells.Clear();
        }

        // Сбрасывает фазу активного трейла после ResolveClosure, но сохраняет _lastCell:
        // он указывает на клетку, ставшую Territory в результате закрытия, и служит якорем
        // следующего трейла без зазора у старта.
        public void ResetAfterClosure()
        {
            _trailHasLineCells = false;
            _activeLineCells.Clear();
        }

        public RasterResult AppendPoint(Vector2 worldXZ)
        {
            var floorCenter = _floor.FloorCenterXZ;
            var worldSize = _floor.WorldSize;
            var newCell = _grid.WorldToCell(worldXZ, floorCenter, worldSize);

            if (_lastCell == null)
            {
                // Первая точка трейла: только инициализируем якорь, без растеризации.
                if (_grid.IsInside(newCell))
                {
                    _lastCell = newCell;
                }
                return RasterResult.NoClose;
            }

            var fromCell = _lastCell.Value;
            if (newCell == fromCell)
            {
                return RasterResult.NoClose;
            }

            // 4-связный Bresenham: за итерацию шагаем только по одной оси, без диагоналей —
            // это гарантирует, что линия не "просочится" между угловыми клетками при flood fill.
            var dx = Mathf.Abs(newCell.x - fromCell.x);
            var dy = Mathf.Abs(newCell.y - fromCell.y);
            var sx = fromCell.x < newCell.x ? 1 : -1;
            var sy = fromCell.y < newCell.y ? 1 : -1;
            var x = fromCell.x;
            var y = fromCell.y;
            var err = dx - dy;

            Vector2Int lastVisited = fromCell;
            while (true)
            {
                var cell = new Vector2Int(x, y);
                if (cell != fromCell)
                {
                    if (!_grid.IsInside(cell))
                    {
                        // Вышли за карту. Останавливаемся на последней валидной клетке.
                        break;
                    }

                    var state = _grid.Get(cell);
                    if (state == CellState.Territory)
                    {
                        if (_trailHasLineCells)
                        {
                            // Замыкание. _lastCell — клетка перед Territory, чтобы продолжить с неё после resolve.
                            _lastCell = lastVisited;
                            return RasterResult.Close(cell);
                        }
                        // Спящая фаза: ни одной Line-клетки ещё нет. Это движение игрока внутри Territory,
                        // не закрытие петли. Идём сквозь, чтобы первая будущая Empty-клетка стала Line
                        // вплотную к границе территории.
                    }
                    else if (state == CellState.Empty)
                    {
                        _grid.Set(cell, CellState.Line);
                        _floor.PaintLineAt(_grid.CellCenterWorld(cell, floorCenter, worldSize));
                        _trailHasLineCells = true;
                        _activeLineCells.Add(cell);
                    }
                    // Line → no-op (этапы 1–4: self-hit игнорируем).
                }

                lastVisited = cell;

                if (x == newCell.x && y == newCell.y)
                {
                    break;
                }

                var e2 = 2 * err;
                if (e2 > -dy)
                {
                    err -= dy;
                    x += sx;
                }
                else
                {
                    err += dx;
                    y += sy;
                }
            }

            _lastCell = lastVisited;
            return RasterResult.NoClose;
        }
    }

    public readonly struct RasterResult
    {
        public readonly bool Closed;
        public readonly Vector2Int ClosureCell;

        private RasterResult(bool closed, Vector2Int closureCell)
        {
            Closed = closed;
            ClosureCell = closureCell;
        }

        public static readonly RasterResult NoClose = new RasterResult(false, default);

        public static RasterResult Close(Vector2Int closureCell)
        {
            return new RasterResult(true, closureCell);
        }
    }
}

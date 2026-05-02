using System;
using System.Collections.Generic;
using UnityEngine;

namespace Floor
{
    /// <summary>
    /// Состояние арены: владеет CPU-сеткой <see cref="ArenaGrid"/>, синхронизирует её с
    /// рендертекстурой пола (через подписку на <see cref="PaintableFloor.Painted"/>) и резолвит
    /// замыкание петли.
    ///
    /// На этапе 3 — мгновенная заливка enclosed-региона. На этапе 4 — делегирует заливку в
    /// <see cref="FloodFillAnimator"/>, если он назначен в инспекторе.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ArenaState : MonoBehaviour
    {
        [SerializeField] private ArenaConfig _config;
        [SerializeField] private PaintableFloor _floor;
        [SerializeField] private FloodFillAnimator _animator;

        private ArenaGrid _grid;
        private TrailRasterizer _rasterizer;

        public ArenaGrid Grid => _grid;
        public PaintableFloor Floor => _floor;
        public ArenaConfig Config => _config;

        /// <summary>
        /// Стреляет после <see cref="EraseTerritoryAt"/> (даже если стирать было нечего —
        /// идемпотентность сохраняется, но событие шумит). Подписчики (например, навигатор) могут
        /// помечать свои производные структуры как dirty. Параметры: точка стирания и мировой радиус.
        /// </summary>
        public event Action<Vector2, float> TerritoryErased;

        /// <summary>
        /// Стреляет после <see cref="MarkObstacleCells"/>/<see cref="UnmarkObstacleCells"/> —
        /// сигнал, что состав <see cref="CellState.Obstacle"/>-клеток изменился. Подписчики
        /// (например, <c>FlowFieldNavigator</c>) помечают свои производные структуры как dirty.
        /// Параметров нет — потребитель пересоберёт всё поле, точечная инвалидация не нужна.
        /// </summary>
        public event Action ObstacleChanged;

        private void Awake()
        {
            _grid = new ArenaGrid(_config.GridResolution);
            _rasterizer = new TrailRasterizer(_grid, _floor);
        }

        private void OnEnable()
        {
            _floor.Painted += OnPainted;
            _floor.PaintedSilent += OnPainted;
        }

        private void OnDisable()
        {
            // Защита от порядка уничтожения: если _floor разрушен раньше при выгрузке сцены,
            // обращение к Painted упало бы на overrided Unity-операторе ==.
            if (_floor != null)
            {
                _floor.Painted -= OnPainted;
                _floor.PaintedSilent -= OnPainted;
            }
        }

        /// <summary>
        /// Регистрирует новую точку траектории игрока. При замыкании — резолвит петлю.
        /// </summary>
        public void AppendTrailPoint(Vector2 worldXZ)
        {
            var result = _rasterizer.AppendPoint(worldXZ);
            if (result.Closed)
            {
                ResolveClosure();
            }
        }

        private void OnPainted(Vector2 worldXZ, float worldRadius)
        {
            // Любая закраска _paintRT (дебажная кисть, init-зона, FloodFillAnimator) отражается
            // в гриде как Territory. Line- и Obstacle-клетки пропускаем: активный трейл — отдельный
            // слой (его поглощает только ResolveClosure / ClearActiveTrail), а Obstacle — статичная
            // непроходимая область, которая не должна перекрашиваться bleed'ом мазка PaintAtSilent
            // от соседней inside-клетки (иначе сломались бы инварианты «Line ≠ Territory» и
            // «Obstacle ≠ Territory»).
            var floorCenter = _floor.FloorCenterXZ;
            var worldSize = _floor.WorldSize;
            var center = _grid.WorldToCell(worldXZ, floorCenter, worldSize);

            // Центральная клетка маркируется всегда, даже если радиус меньше cellSize.
            if (_grid.IsInside(center))
            {
                var centerState = _grid.Get(center);
                if (centerState != CellState.Line && centerState != CellState.Obstacle)
                {
                    _grid.Set(center, CellState.Territory);
                }
            }

            var cellSize = worldSize.x / _grid.Resolution;
            var cellRadius = Mathf.CeilToInt(worldRadius / cellSize);
            if (cellRadius <= 0) return;

            for (var dx = -cellRadius; dx <= cellRadius; dx++)
            {
                for (var dy = -cellRadius; dy <= cellRadius; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    var cell = new Vector2Int(center.x + dx, center.y + dy);
                    if (!_grid.IsInside(cell)) continue;
                    var state = _grid.Get(cell);
                    if (state == CellState.Line || state == CellState.Obstacle) continue;

                    var cellWorld = _grid.CellCenterWorld(cell, floorCenter, worldSize);
                    if ((cellWorld - worldXZ).sqrMagnitude > worldRadius * worldRadius) continue;

                    _grid.Set(cell, CellState.Territory);
                }
            }
        }

        private void ResolveClosure()
        {
            // Замыкание = расширение закрытой области. Только обновляем грид и
            // отдаём данные painter'у — никакой логики запуска заливки здесь нет,
            // painter крутится постоянно и подхватит новые клетки сам.
            var resolution = _grid.Resolution;

            var lineCells = new List<Vector2Int>();
            for (var x = 0; x < resolution; x++)
            {
                for (var y = 0; y < resolution; y++)
                {
                    var cell = new Vector2Int(x, y);
                    if (_grid.Get(cell) == CellState.Line)
                    {
                        lineCells.Add(cell);
                    }
                }
            }

            // Enclosed считается до конвертации линии — линия на момент поиска
            // ещё стена для BFS из EnclosedRegionFinder, иначе пустота "вытечет".
            // Скоуп — только клетки, 4-связно достижимые от текущих lineCells через Empty:
            // старые «дыры» от прерванных заливок не подхватываются новым замыканием.
            var enclosed = EnclosedRegionFinder.FindEnclosed(_grid, lineCells);

            var floorCenter = _floor.FloorCenterXZ;
            var worldSize = _floor.WorldSize;

            // Линия + внутренняя область — теперь часть закрытой области (Territory).
            // enclosed.Count == 0 (тонкая петля «вперёд-назад» по той же тропе) — это
            // не повод дискардить: сама линия становится Territory ширины 1. Стейт
            // грида обновляем сразу; стампы на _paintRT и стирание оверлея _lineRT —
            // работа painter'а.
            for (var i = 0; i < lineCells.Count; i++)
            {
                _grid.Set(lineCells[i], CellState.Territory);
            }
            for (var i = 0; i < enclosed.Count; i++)
            {
                _grid.Set(enclosed[i], CellState.Territory);
            }

            _rasterizer.ResetAfterClosure();

            if (_animator != null)
            {
                // enclosed → нужны мазки на _paintRT.
                // line — тоже в _pendingPaint: даёт мазок (минимум ширины 1 для тонких
                // петель «вперёд-назад») + служит мостиком BFS к изолированным
                // enclosed-регионам при самопересечении трейла. Painter распознаёт
                // line-клетки по совпадению с _pendingLines: бесплатно по краске,
                // но мазок наносит наравне с inside-клетками.
                _animator.AddPending(enclosed);
                _animator.AddPending(lineCells);
                _animator.AddLineCleanup(lineCells);
                return;
            }

            // Fallback без painter'а: рисуем enclosed и линию мазками, стираем оверлей.
            for (var i = 0; i < enclosed.Count; i++)
            {
                _floor.PaintAtSilent(_grid.CellCenterWorld(enclosed[i], floorCenter, worldSize));
            }
            for (var i = 0; i < lineCells.Count; i++)
            {
                var cellWorld = _grid.CellCenterWorld(lineCells[i], floorCenter, worldSize);
                _floor.PaintAtSilent(cellWorld);
                _floor.EraseLineAt(cellWorld);
            }
        }

        /// <summary>
        /// Стирает территорию вокруг точки в радиусе клеток (CPU-грид + GPU-маска). Используется
        /// эрозией врагов (GDD §3.2). <see cref="CellState.Line"/>-клетки не трогает — активный
        /// трейл игрока врагами не разрывается (GDD §3.5). Идемпотентна.
        /// </summary>
        public void EraseTerritoryAt(Vector2 worldXZ, int radiusInCells)
        {
            if (radiusInCells < 0) return;
            var floorCenter = _floor.FloorCenterXZ;
            var worldSize = _floor.WorldSize;
            var center = _grid.WorldToCell(worldXZ, floorCenter, worldSize);

            if (_grid.IsInside(center) && _grid.Get(center) == CellState.Territory)
            {
                _grid.Set(center, CellState.Empty);
            }

            var cellSize = worldSize.x / _grid.Resolution;
            var worldRadius = (radiusInCells + 0.5f) * cellSize;

            for (var dx = -radiusInCells; dx <= radiusInCells; dx++)
            {
                for (var dy = -radiusInCells; dy <= radiusInCells; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    var cell = new Vector2Int(center.x + dx, center.y + dy);
                    if (!_grid.IsInside(cell)) continue;

                    var cellWorld = _grid.CellCenterWorld(cell, floorCenter, worldSize);
                    if ((cellWorld - worldXZ).sqrMagnitude > worldRadius * worldRadius) continue;

                    if (_grid.Get(cell) == CellState.Territory)
                    {
                        _grid.Set(cell, CellState.Empty);
                    }
                }
            }

            _floor.EraseAt(worldXZ, worldRadius);
            TerritoryErased?.Invoke(worldXZ, worldRadius);
        }

        /// <summary>
        /// Регистрирует клетки как непроходимое препятствие (<see cref="CellState.Obstacle"/>).
        /// Источник — компонент <see cref="Obstacle"/> на сцене (Collider + Unity-слой из
        /// <c>Core.GameLayersConfig.ObstacleLayers</c>). Клетки <see cref="CellState.Empty"/>
        /// помечаются Obstacle; <see cref="CellState.Territory"/>/<see cref="CellState.Line"/>
        /// пропускаются с <see cref="Debug.LogWarning"/> — препятствие нельзя ставить поверх
        /// уже занятой клетки (автор сцены должен это предотвращать). После применения —
        /// событие <see cref="ObstacleChanged"/>.
        /// </summary>
        public void MarkObstacleCells(IReadOnlyList<Vector2Int> cells)
        {
            if (cells == null || cells.Count == 0) return;
            for (var i = 0; i < cells.Count; i++)
            {
                var cell = cells[i];
                if (!_grid.IsInside(cell)) continue;
                var state = _grid.Get(cell);
                if (state == CellState.Empty)
                {
                    _grid.Set(cell, CellState.Obstacle);
                    continue;
                }
                if (state == CellState.Obstacle) continue;
                Debug.LogWarning($"[ArenaState] MarkObstacleCells skipped {cell} — state {state} (можно ставить только поверх Empty).");
            }
            ObstacleChanged?.Invoke();
        }

        /// <summary>
        /// Снимает пометку <see cref="CellState.Obstacle"/> с клеток, возвращая их в
        /// <see cref="CellState.Empty"/>. Прочие state не трогает (на случай рассинхрона
        /// между списком клеток и текущим гридом). После применения — событие
        /// <see cref="ObstacleChanged"/>.
        /// </summary>
        public void UnmarkObstacleCells(IReadOnlyList<Vector2Int> cells)
        {
            if (cells == null || cells.Count == 0) return;
            for (var i = 0; i < cells.Count; i++)
            {
                var cell = cells[i];
                if (!_grid.IsInside(cell)) continue;
                if (_grid.Get(cell) != CellState.Obstacle) continue;
                _grid.Set(cell, CellState.Empty);
            }
            ObstacleChanged?.Invoke();
        }

        /// <summary>
        /// Сбрасывает активный незакрытый трейл игрока: грид-клетки <see cref="CellState.Line"/>
        /// возвращаются в <see cref="CellState.Empty"/>, диск EraseLineAt стирает их с
        /// <c>_lineRT</c>, якорь <see cref="TrailRasterizer"/> сбрасывается. Не трогает
        /// закрытую территорию и накопленные в painter'е <c>_pendingLines</c> — это
        /// уже-закрытые линии, ими занимается painter.
        /// TODO: вызывать при смерти игрока (день 2 GDD §3.4 "Текущий след теряется").
        /// </summary>
        public void ClearActiveTrail()
        {
            var floorCenter = _floor.FloorCenterXZ;
            var worldSize = _floor.WorldSize;
            var resolution = _grid.Resolution;
            for (var x = 0; x < resolution; x++)
            {
                for (var y = 0; y < resolution; y++)
                {
                    var cell = new Vector2Int(x, y);
                    if (_grid.Get(cell) != CellState.Line) continue;
                    _grid.Set(cell, CellState.Empty);
                    _floor.EraseLineAt(_grid.CellCenterWorld(cell, floorCenter, worldSize));
                }
            }
            _rasterizer.Reset();
        }
    }
}

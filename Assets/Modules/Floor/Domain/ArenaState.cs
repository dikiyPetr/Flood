using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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
        [SerializeField] private PaintableFloor _floor;

        [Tooltip("Disabled-компонент → fallback в ResolveClosure: enclosed/line закрашиваются " +
                 "мазками без анимации.")]
        [SerializeField] private FloodFillAnimator _animator;

        private ArenaGrid _grid;
        private TrailRasterizer _rasterizer;
        private EnclosedRegionFinder _enclosedRegionFinder;

        // Snapshot для фонового поиска enclosed: snapshot grid'а и список line-клеток
        // фиксируются на main thread'е перед запуском Task.Run и читаются только фоном.
        // Live _grid main thread свободно правит без race.
        private CellState[,] _gridSnapshot;
        private readonly List<Vector2Int> _lineCellsSnapshot = new List<Vector2Int>();
        private Task<IReadOnlyList<Vector2Int>> _closureTask;
        private bool _closureInFlight;

        public ArenaGrid Grid => _grid;
        public PaintableFloor Floor => _floor;

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
            var resolution = _floor.Config.GridResolution;
            _grid = new ArenaGrid(resolution);
            _rasterizer = new TrailRasterizer(_grid, _floor);
            _enclosedRegionFinder = new EnclosedRegionFinder(resolution);
            _gridSnapshot = new CellState[resolution, resolution];
        }

        private void Update()
        {
            // Polling завершения фонового поиска enclosed. Дёшево (несколько ns/кадр) когда idle.
            if (_closureInFlight && _closureTask.IsCompleted)
            {
                _closureInFlight = false;
                if (_closureTask.IsFaulted)
                {
                    Debug.LogException(_closureTask.Exception);
                    return;
                }
                ApplyEnclosed(_closureTask.Result);
            }
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
            // Замыкание двухчастное: (1) синхронно превращаем линию в Territory + просим painter
            // её закрасить — мгновенный визуальный feedback на main thread; (2) асинхронно ищем
            // enclosed-клетки (BFS на фоне), результат применяется через Update polling. Между
            // (1) и (2) проходит несколько кадров — игрок видит, что петля «защёлкнулась», а
            // заливка внутренности появляется через ~100 мс без stutter'а.
            var lineCells = _rasterizer.ActiveLineCells;
            var floorCenter = _floor.FloorCenterXZ;
            var worldSize = _floor.WorldSize;

            // (1) синхронно: линия → Territory + painter/fallback стампит её сразу.
            for (var i = 0; i < lineCells.Count; i++)
            {
                _grid.Set(lineCells[i], CellState.Territory);
            }

            if (_animator.isActiveAndEnabled)
            {
                // AddPending копирует элементы в HashSet — после этого lineCells можно сбрасывать.
                _animator.AddPending(lineCells);
                _animator.AddLineCleanup(lineCells);
            }
            else
            {
                for (var i = 0; i < lineCells.Count; i++)
                {
                    var cellWorld = _grid.CellCenterWorld(lineCells[i], floorCenter, worldSize);
                    _floor.PaintAtSilent(cellWorld);
                    _floor.EraseLineAt(cellWorld);
                }
            }

            // (2) асинхронно: snapshot грида и линий, запуск Task.Run.
            // EnclosedRegionFinder имеет mutable state — конкурентный запуск запрещён.
            // MVP-поведение при втором замыкании во время фона: пропуск enclosed-фазы,
            // линия всё равно превратится в Territory (видно visually), но внутренность
            // не зальётся. Достаточно для редкого кейса; если станет проблемой — pool
            // finder'ов или очередь pending'ов.
            if (_closureInFlight)
            {
                Debug.LogWarning("[ArenaState] Closure dropped: previous still in flight. Line marked Territory, enclosed skipped.");
                _rasterizer.ResetAfterClosure();
                return;
            }

            // Snapshot линий: копируем в собственный буфер, чтобы ResetAfterClosure
            // мог сразу очистить _activeLineCells.
            _lineCellsSnapshot.Clear();
            for (var i = 0; i < lineCells.Count; i++) _lineCellsSnapshot.Add(lineCells[i]);

            // Snapshot грида: после этой строки фон работает на копии, main thread свободен.
            // Линия в snapshot уже Territory (мы только что её Set'нули) — для finder'а это
            // стена, как и должно быть.
            _grid.CopyCellsTo(_gridSnapshot);

            _rasterizer.ResetAfterClosure();

            _closureInFlight = true;
            // Локальные ссылки в замыкании — чтобы не поймать race на ре-инициализацию полей.
            var snapshot = _gridSnapshot;
            var snapshotLines = _lineCellsSnapshot;
            var finder = _enclosedRegionFinder;
            _closureTask = Task.Run(() => finder.FindEnclosed(snapshot, snapshotLines));
        }

        private void ApplyEnclosed(IReadOnlyList<Vector2Int> enclosed)
        {
            // Между snapshot'ом и моментом apply main thread мог изменить грид: линия уже стала
            // Territory (мы её сами поставили в ResolveClosure), эрозия врагов могла Empty'нуть
            // часть бывших Territory, painter мог замазать пересекающие enclosed-клетки.
            // Применяем enclosed → Territory только на клетках, всё ещё Empty в live-гриде:
            // не перетираем эрозию и не меняем самопересекающую новую линию.
            var floorCenter = _floor.FloorCenterXZ;
            var worldSize = _floor.WorldSize;
            for (var i = 0; i < enclosed.Count; i++)
            {
                var cell = enclosed[i];
                if (_grid.Get(cell) == CellState.Empty)
                {
                    _grid.Set(cell, CellState.Territory);
                }
            }

            if (_animator.isActiveAndEnabled)
            {
                _animator.AddPending(enclosed);
            }
            else
            {
                for (var i = 0; i < enclosed.Count; i++)
                {
                    _floor.PaintAtSilent(_grid.CellCenterWorld(enclosed[i], floorCenter, worldSize));
                }
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
            // Инкрементальный список линий — ровно те клетки, что TrailRasterizer пометил Line
            // в текущей активной фазе. Без full-grid scan'а O(res²).
            var lineCells = _rasterizer.ActiveLineCells;
            for (var i = 0; i < lineCells.Count; i++)
            {
                var cell = lineCells[i];
                _grid.Set(cell, CellState.Empty);
                _floor.EraseLineAt(_grid.CellCenterWorld(cell, floorCenter, worldSize));
            }
            _rasterizer.Reset();
        }
    }
}

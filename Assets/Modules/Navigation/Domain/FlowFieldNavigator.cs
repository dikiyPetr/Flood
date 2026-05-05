using System.Collections.Generic;
using System.Threading.Tasks;
using Floor;
using UnityEngine;

namespace Navigation
{
    /// <summary>
    /// Владелец flow-field'а сцены: подписан на события <see cref="ArenaState"/>, помечающие
    /// terrain-change (Painted, PaintedSilent, TerritoryErased, ObstacleChanged), и пересобирает
    /// поле on-demand. API для потребителей (Enemy и т.п.) — <see cref="SampleDirection"/>.
    ///
    /// Rebuild идёт асинхронно через <see cref="Task.Run"/> на background thread'е:
    /// 1. На main thread'е снимается snapshot грида (<see cref="ArenaGrid.CopyCellsTo"/>) и целей.
    /// 2. <see cref="FlowFieldBuilder.Build"/> работает на snapshot'е, мутирует back-буфер.
    /// 3. После завершения — atomic swap front↔back на main thread'е (через <see cref="Update"/>).
    ///
    /// Двойной буфер: <c>_frontField</c> — текущий read-only снимок для <see cref="SampleDirection"/>,
    /// <c>_backField</c> — write target для фонового билда. Враги читают front-буфер на любом
    /// кадре, фон мутирует back — без race.
    ///
    /// Throttle через <see cref="NavigationConfig.RebuildIntervalSeconds"/>: минимальный интервал
    /// между завершением одного build'а и стартом следующего. При 0 — rebuild сразу при dirty.
    /// Если карта не меняется — события не приходят, <c>_dirty</c> остаётся false, rebuild не
    /// запускается вообще.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FlowFieldNavigator : MonoBehaviour
    {
        [SerializeField] private ArenaState _arena;
        [SerializeField] private NavigationConfig _config;
        [SerializeField] private List<NavigationGoalBinding> _goals = new List<NavigationGoalBinding>();

        private FlowField _frontField;
        private FlowField _backField;
        private FlowFieldBuilder _builder;

        private CellState[,] _gridSnapshot;
        private readonly List<NavigationGoal> _resolvedGoalsSnapshot = new List<NavigationGoal>();
        private Vector2 _snapshotFloorCenter;
        private Vector2 _snapshotWorldSize;
        private int _snapshotResolution;

        private Task _rebuildTask;
        private bool _rebuildInFlight;
        private bool _dirty = true;
        private float _lastBuildTime = float.NegativeInfinity;

        public FlowField Field => _frontField;
        public ArenaState Arena => _arena;
        public NavigationConfig Config => _config;
        public bool IsDirty => _dirty;

        /// <summary>Принудительно отметить поле как требующее пересборки.</summary>
        public void MarkDirty() { _dirty = true; }

        private void Awake()
        {
            _builder = new FlowFieldBuilder();
        }

        private void OnEnable()
        {
            _arena.Floor.Painted += HandleTerrainChange;
            _arena.Floor.PaintedSilent += HandleTerrainChange;
            _arena.TerritoryErased += HandleTerrainChange;
            _arena.ObstacleChanged += HandleObstacleChange;
            _dirty = true;
        }

        private void OnDisable()
        {
            // Teardown-гард: Unity-overridden == возвращает true для Destroyed-объектов,
            // если _arena/_arena.Floor разрушены раньше навигатора при выгрузке сцены.
            if (_arena == null) return;
            if (_arena.Floor != null)
            {
                _arena.Floor.Painted -= HandleTerrainChange;
                _arena.Floor.PaintedSilent -= HandleTerrainChange;
            }
            _arena.TerritoryErased -= HandleTerrainChange;
            _arena.ObstacleChanged -= HandleObstacleChange;
        }

        private void HandleTerrainChange(Vector2 _, float __) { _dirty = true; }
        private void HandleObstacleChange() { _dirty = true; }

        private void Update()
        {
            // ArenaState.Awake создаёт грид; порядок Awake между MB не гарантирован.
            if (_arena.Grid == null) return;

            // Завершение фона — atomic swap front↔back, освобождение builder'а.
            if (_rebuildInFlight && _rebuildTask.IsCompleted)
            {
                _rebuildInFlight = false;
                if (_rebuildTask.IsFaulted)
                {
                    Debug.LogException(_rebuildTask.Exception);
                }
                else
                {
                    SwapFields();
                    _lastBuildTime = Time.time;
                }
            }

            // Старт нового билда: только если карта менялась с прошлого билда (event-driven _dirty),
            // фон не занят, и прошёл throttle-интервал.
            if (!_dirty) return;
            if (_rebuildInFlight) return;
            if (Time.time - _lastBuildTime < _config.RebuildIntervalSeconds) return;

            StartRebuild();
        }

        private void StartRebuild()
        {
            var grid = _arena.Grid;
            var floor = _arena.Floor;
            var resolution = grid.Resolution;

            // Lazy-create / re-create буферов при изменении Resolution.
            if (_frontField == null || _frontField.Resolution != resolution)
            {
                _frontField = new FlowField(resolution);
                _backField = new FlowField(resolution);
                _gridSnapshot = new CellState[resolution, resolution];
            }

            // Snapshot main thread'а: грид + цели + параметры пола. Фон не трогает Unity API.
            grid.CopyCellsTo(_gridSnapshot);
            _snapshotResolution = resolution;
            _snapshotFloorCenter = floor.FloorCenterXZ;
            _snapshotWorldSize = floor.WorldSize;

            _resolvedGoalsSnapshot.Clear();
            for (var i = 0; i < _goals.Count; i++)
            {
                var binding = _goals[i];
                if (binding == null) continue;
                _resolvedGoalsSnapshot.Add(new NavigationGoal(binding.ResolveWorldXZ(_snapshotFloorCenter), binding.Weight));
            }
            // Smoke-friendly fallback: если целей нет — центр пола.
            if (_resolvedGoalsSnapshot.Count == 0)
            {
                _resolvedGoalsSnapshot.Add(new NavigationGoal(_snapshotFloorCenter, 1f));
            }

            _dirty = false;
            _rebuildInFlight = true;

            // Замыкаем локальные ссылки — на случай переинициализации полей в Awake/OnEnable
            // во время фона (теоретическая возможность при reload сцены).
            var snapshot = _gridSnapshot;
            var res = _snapshotResolution;
            var config = _config;
            var goals = _resolvedGoalsSnapshot;
            var floorCenter = _snapshotFloorCenter;
            var worldSize = _snapshotWorldSize;
            var target = _backField;
            var builder = _builder;

            _rebuildTask = Task.Run(() =>
            {
                builder.Build(snapshot, res, config, goals, floorCenter, worldSize, target);
            });
        }

        private void SwapFields()
        {
            var tmp = _frontField;
            _frontField = _backField;
            _backField = tmp;
        }

        /// <summary>
        /// Возвращает направление потока в клетке, содержащей <paramref name="worldXZ"/>.
        /// <see cref="Vector2.zero"/> — если поле не построено, точка вне грида или клетка
        /// — цель/недостижима.
        /// </summary>
        public Vector2 SampleDirection(Vector2 worldXZ)
        {
            // _frontField — runtime-state, до первого Build он null; _grid тоже до Awake.
            if (_frontField == null || _arena.Grid == null) return Vector2.zero;
            var floor = _arena.Floor;
            var cell = _arena.Grid.WorldToCell(worldXZ, floor.FloorCenterXZ, floor.WorldSize);
            return _frontField.SampleDirection(cell);
        }
    }
}

using System.Collections.Generic;
using Floor;
using UnityEngine;

namespace Navigation
{
    /// <summary>
    /// Владелец flow-field'а сцены: подписан на события <see cref="ArenaState"/>, помечающие
    /// terrain-change (Painted, PaintedSilent, TerritoryErased), и пересобирает поле on-demand.
    /// API для потребителей (Enemy и т.п.) — <see cref="SampleDirection"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FlowFieldNavigator : MonoBehaviour
    {
        [SerializeField] private ArenaState _arena;
        [SerializeField] private NavigationConfig _config;
        [SerializeField] private List<NavigationGoalBinding> _goals = new List<NavigationGoalBinding>();

        private FlowField _field;
        private FlowFieldBuilder _builder;
        private readonly List<NavigationGoal> _resolvedGoals = new List<NavigationGoal>();
        private bool _dirty = true;
        private float _lastBuildTime = float.NegativeInfinity;

        public FlowField Field => _field;
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
            if (_arena == null) return;
            _arena.Floor.Painted += HandleTerrainChange;
            _arena.Floor.PaintedSilent += HandleTerrainChange;
            _arena.TerritoryErased += HandleTerrainChange;
            _dirty = true;
        }

        private void OnDisable()
        {
            if (_arena == null) return;
            if (_arena.Floor != null)
            {
                _arena.Floor.Painted -= HandleTerrainChange;
                _arena.Floor.PaintedSilent -= HandleTerrainChange;
            }
            _arena.TerritoryErased -= HandleTerrainChange;
        }

        private void HandleTerrainChange(Vector2 _, float __) { _dirty = true; }

        private void Update()
        {
            if (_arena == null || _config == null || _arena.Grid == null) return;
            if (!_dirty) return;
            if (Time.time - _lastBuildTime < _config.RebuildIntervalSeconds) return;
            Rebuild();
        }

        private void Rebuild()
        {
            var grid = _arena.Grid;
            if (_field == null || _field.Resolution != grid.Resolution)
            {
                _field = new FlowField(grid.Resolution);
            }

            var floor = _arena.Floor;
            var floorCenterXZ = floor.FloorCenterXZ;
            var worldSize = floor.WorldSize;

            _resolvedGoals.Clear();
            for (var i = 0; i < _goals.Count; i++)
            {
                var binding = _goals[i];
                if (binding == null) continue;
                _resolvedGoals.Add(new NavigationGoal(binding.ResolveWorldXZ(floorCenterXZ), binding.Weight));
            }

            // Если списка целей нет — fallback на центр пола, чтобы поле всё равно строилось
            // (полезно для smoke-тестов до настройки целей в инспекторе).
            if (_resolvedGoals.Count == 0)
            {
                _resolvedGoals.Add(new NavigationGoal(floorCenterXZ, 1f));
            }

            _builder.Build(grid, _config, _resolvedGoals, floorCenterXZ, worldSize, _field);
            _dirty = false;
            _lastBuildTime = Time.time;
        }

        /// <summary>
        /// Возвращает направление потока в клетке, содержащей <paramref name="worldXZ"/>.
        /// <see cref="Vector2.zero"/> — если поле не построено, точка вне грида или клетка
        /// — цель/недостижима.
        /// </summary>
        public Vector2 SampleDirection(Vector2 worldXZ)
        {
            if (_field == null || _arena == null || _arena.Grid == null) return Vector2.zero;
            var floor = _arena.Floor;
            var cell = _arena.Grid.WorldToCell(worldXZ, floor.FloorCenterXZ, floor.WorldSize);
            return _field.SampleDirection(cell);
        }
    }
}

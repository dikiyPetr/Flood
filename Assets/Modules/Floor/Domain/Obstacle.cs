using System.Collections.Generic;
using Core;
using UnityEngine;

namespace Floor
{
    /// <summary>
    /// Авторская пометка непроходимой области поверх <see cref="ArenaGrid"/>. На <c>OnEnable</c>
    /// агрегирует AABB всех <see cref="Collider"/>'ов в иерархии (сам объект + дети), проецирует
    /// в клетки грида и зовёт <see cref="ArenaState.MarkObstacleCells"/> — все попавшие клетки
    /// переходят в <see cref="CellState.Obstacle"/>. На <c>OnDisable</c> симметрично снимает
    /// пометку. Достаточно повесить компонент на корень префаба — детей с коллайдерами не
    /// нужно помечать индивидуально. Поддерживает и сценную статику, и runtime add/remove.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Obstacle : MonoBehaviour
    {
        [SerializeField] private ArenaState _arena;
        [SerializeField] private GameLayersConfig _gameLayers;

        private readonly List<Vector2Int> _cells = new List<Vector2Int>();
        private readonly List<Collider> _colliders = new List<Collider>();
        private bool _marked;

        private void Awake()
        {
            // includeInactive=true: на момент Awake часть детей может быть выключена, но
            // позже включится и должна попасть в обструкцию. Активность проверяется при
            // каждой пометке в TryMark, а не один раз в Awake.
            GetComponentsInChildren(true, _colliders);
        }

        private void OnEnable()
        {
            TryMark();
        }

        private void Start()
        {
            // Если OnEnable пришёл до ArenaState.Awake (порядок Awake/OnEnable
            // между MB не гарантирован) — пометка отложилась до Start.
            if (!_marked) TryMark();
            ValidateLayer();
        }

        private void OnDisable()
        {
            if (!_marked) return;
            // Teardown-гард: Unity-overridden == возвращает true для Destroyed _arena
            // при выгрузке сцены (порядок Destroy между MB не гарантирован).
            if (_arena != null) _arena.UnmarkObstacleCells(_cells);
            _cells.Clear();
            _marked = false;
        }

        private void TryMark()
        {
            if (_colliders.Count == 0) return;
            // Awake-гонка: TryMark может прийти из OnEnable до ArenaState.Awake
            // (ArenaGrid создаётся там). Дождёмся, пока Start попытается ещё раз.
            var grid = _arena.Grid;
            if (grid == null) return;
            var floor = _arena.Floor;

            var floorCenter = floor.FloorCenterXZ;
            var worldSize = floor.WorldSize;

            _cells.Clear();
            for (var ci = 0; ci < _colliders.Count; ci++)
            {
                var c = _colliders[ci];
                if (c == null) continue;
                if (!c.enabled || !c.gameObject.activeInHierarchy) continue;

                // Conservative AABB-растеризация по XZ. Поворот коллайдера учитывается тем,
                // что bounds — мировой AABB, оборачивающий повёрнутую форму.
                var bounds = c.bounds;
                var minXZ = new Vector2(bounds.min.x, bounds.min.z);
                var maxXZ = new Vector2(bounds.max.x, bounds.max.z);
                var k0 = grid.WorldToCell(minXZ, floorCenter, worldSize);
                var k1 = grid.WorldToCell(maxXZ, floorCenter, worldSize);

                // FloorProjection может инвертировать ось — нормализуем диапазон.
                var x0 = Mathf.Min(k0.x, k1.x);
                var x1 = Mathf.Max(k0.x, k1.x);
                var y0 = Mathf.Min(k0.y, k1.y);
                var y1 = Mathf.Max(k0.y, k1.y);

                for (var x = x0; x <= x1; x++)
                {
                    for (var y = y0; y <= y1; y++)
                    {
                        var cell = new Vector2Int(x, y);
                        if (!grid.IsInside(cell)) continue;
                        _cells.Add(cell);
                    }
                }
            }

            // Дубликаты между перекрывающимися коллайдерами безопасны: MarkObstacleCells
            // идемпотентен (повторная пометка Obstacle-клетки — no-op).
            if (_cells.Count == 0) return;
            _arena.MarkObstacleCells(_cells);
            _marked = true;
        }

        private void ValidateLayer()
        {
            var mask = _gameLayers.ObstacleLayers.value;
            if (mask == 0)
            {
                Debug.LogWarning($"[Obstacle] {name}: GameLayersConfig.ObstacleLayers пуст — не будет фильтрации препятствий по слою. Назначьте слой Obstacle в конфиге.", this);
                return;
            }
            if ((mask & (1 << gameObject.layer)) == 0)
            {
                Debug.LogWarning($"[Obstacle] {name}: GameObject на слое '{LayerMask.LayerToName(gameObject.layer)}', который не входит в GameLayersConfig.ObstacleLayers.", this);
            }
        }
    }
}

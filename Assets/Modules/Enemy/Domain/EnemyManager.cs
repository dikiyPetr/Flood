using System.Collections.Generic;
using Floor;
using Navigation;
using UnityEngine;

namespace Enemy
{
    /// <summary>
    /// Оркестратор врагов: владеет списком активных, пулом по префабам, гоняет общий
    /// движенческий Update и тик 1Hz урона + эрозии. См. CLAUDE.md модуля «Tier 0».
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyManager : MonoBehaviour
    {
        [SerializeField] private ArenaState _arena;
        [SerializeField] private FlowFieldNavigator _navigator;
        [SerializeField] private float _tickIntervalSeconds = 1f;

        [Header("Pressure-amplified erosion")]
        [Tooltip("Радиус (мировые единицы) для подсчёта соседей при скейле eat-радиуса.")]
        [SerializeField, Min(0f)] private float _pressureRadius = 1.5f;
        [Tooltip("Прибавка к eat-радиусу за каждого соседа в _pressureRadius. " +
                 "effectiveRadius = baseRadius + floor(neighbors * factor). 0 = выключено.")]
        [SerializeField, Min(0f)] private float _pressureBonusPerNeighbor = 0.4f;

        [Header("Crowd separation (Boids)")]
        [Tooltip("Радиус (мировые единицы) отталкивания соседей. 0 = выключено. " +
                 "Брать ~ диаметр визуальной модели врага.")]
        [SerializeField, Min(0f)] private float _separationRadius = 0.6f;
        [Tooltip("Вес отталкивания относительно направления к цели. 0 = выкл, ~1 = равноценно потоку.")]
        [SerializeField, Min(0f)] private float _separationWeight = 1f;

        [Header("Crowd-aware speed scaling")]
        [Tooltip("Радиус (мировые единицы) для подсчёта плотности при замедлении. " +
                 "Обычно ≤ _separationRadius — замедляет именно «застрявших в спине».")]
        [SerializeField, Min(0f)] private float _crowdSlowdownRadius = 0.6f;
        [Tooltip("Сила замедления: speed *= 1 / (1 + factor * neighbors). 0 = выкл, " +
                 "0.4 → 4 соседа дают ~38% скорости, 10 соседей → 20%.")]
        [SerializeField, Min(0f)] private float _crowdSlowdownFactor = 0.4f;

        private readonly List<Enemy> _active = new List<Enemy>();
        private readonly Dictionary<Enemy, Stack<Enemy>> _poolByPrefab = new Dictionary<Enemy, Stack<Enemy>>();
        private readonly EnemySpatialHash _spatialHash = new EnemySpatialHash();
        private readonly List<Enemy> _queryBuffer = new List<Enemy>(64);
        private float _accumulator;

        public ArenaState Arena => _arena;

        public Enemy Spawn(Enemy prefab, EnemyConfig config, Vector3 position)
        {
            Enemy enemy;
            if (_poolByPrefab.TryGetValue(prefab, out var pool) && pool.Count > 0)
            {
                enemy = pool.Pop();
                enemy.transform.position = position;
                enemy.gameObject.SetActive(true);
            }
            else
            {
                enemy = Instantiate(prefab, position, Quaternion.identity, transform);
            }

            enemy.Init(this, config, prefab);
            _active.Add(enemy);
            return enemy;
        }

        internal void Return(Enemy enemy)
        {
            _active.Remove(enemy);
            enemy.gameObject.SetActive(false);
            if (!_poolByPrefab.TryGetValue(enemy.Prefab, out var pool))
            {
                pool = new Stack<Enemy>();
                _poolByPrefab[enemy.Prefab] = pool;
            }

            pool.Push(enemy);
        }

        private void Update()
        {
            if (_arena == null || _arena.Grid == null) return;

            var dt = Time.deltaTime;

            // Spatial hash перестраивается раз в кадр перед всеми запросами (MoveAll + Tick).
            // CellSize = max радиуса любого Query, чтобы один запрос трогал ≤4 bin'а.
            // Стейл-позиции (на ~один dt) для Tick безвредны: max сдвиг = MoveSpeed * dt < 0.05u.
            var binSize = Mathf.Max(_separationRadius, _crowdSlowdownRadius);
            binSize = Mathf.Max(binSize, _pressureRadius);
            _spatialHash.Rebuild(_active, Mathf.Max(0.5f, binSize));

            MoveAll(dt);

            _accumulator += dt;
            if (_accumulator >= _tickIntervalSeconds)
            {
                _accumulator -= _tickIntervalSeconds;
                Tick();
            }
        }

        private void MoveAll(float dt)
        {
            var grid = _arena.Grid;
            var floor = _arena.Floor;
            var floorCenter = floor.FloorCenterXZ;
            var worldSize = floor.WorldSize;

            var sepActive = _separationRadius > 0f && _separationWeight > 0f;
            var slowActive = _crowdSlowdownRadius > 0f && _crowdSlowdownFactor > 0f;
            var sepRadiusSqr = _separationRadius * _separationRadius;
            var slowRadiusSqr = _crowdSlowdownRadius * _crowdSlowdownRadius;
            var queryRadius = Mathf.Max(_separationRadius, _crowdSlowdownRadius);

            for (var i = 0; i < _active.Count; i++)
            {
                var enemy = _active[i];
                var pos = enemy.WorldXZ;

                // Flow-field указывает направление с учётом cost-карты (Empty/Territory/Line);
                // Territory всё ещё блокирует шаг ниже — поле лишь выбирает оптимальный путь.
                var dir = _navigator.SampleDirection(pos);
                if (dir == Vector2.zero)
                {
                    // Клетка-цель или недостижимая клетка — fallback на прямой seek, чтобы
                    // враг не зависал, дойдя до seed'а или попав в изолированную зону.
                    dir = floorCenter - pos;
                    var sqrFallback = dir.sqrMagnitude;
                    if (sqrFallback < 0.0001f) continue;
                    dir /= Mathf.Sqrt(sqrFallback);
                }

                // Один Query на врага под максимальный радиус. Sep + slow считаются в одном
                // проходе по буферу, без двойного скана хеша.
                var sep = Vector2.zero;
                var slowdownNeighbors = 0;
                if ((sepActive || slowActive) && queryRadius > 0f)
                {
                    _spatialHash.Query(pos, queryRadius, _queryBuffer);
                    for (var j = 0; j < _queryBuffer.Count; j++)
                    {
                        var other = _queryBuffer[j];
                        if (other == null || other == enemy) continue;
                        var delta = pos - other.WorldXZ;
                        var sqr = delta.sqrMagnitude;
                        if (sqr < 1e-6f) continue;
                        if (sepActive && sqr < sepRadiusSqr)
                        {
                            var d = Mathf.Sqrt(sqr);
                            var intensity = _separationRadius / d - 1f;
                            sep += (delta / d) * intensity;
                        }
                        if (slowActive && sqr <= slowRadiusSqr) slowdownNeighbors++;
                    }
                }

                if (sep != Vector2.zero)
                {
                    var combined = dir + sep * _separationWeight;
                    var combinedSqr = combined.sqrMagnitude;
                    if (combinedSqr > 1e-6f) dir = combined / Mathf.Sqrt(combinedSqr);
                }

                // Crowd-aware speed scaling: speed = base / (1 + factor * neighbors). Solo враг
                // (neighbors=0) → factor=1, full speed. Stable equilibrium: чем плотнее толпа,
                // тем медленнее давит → soft separation удерживает упаковку без boiling'а.
                var speed = enemy.Config.MoveSpeed;
                if (slowActive && slowdownNeighbors > 0)
                {
                    speed /= 1f + _crowdSlowdownFactor * slowdownNeighbors;
                }

                var stepLen = speed * dt;
                var step = dir * stepLen;

                // 4-fallback sliding (порядок детерминированный, чтобы избежать джиттера):
                //   1) полный шаг
                //   2) только X — скользим вдоль Z-стены
                //   3) только Z — скользим вдоль X-стены
                //   4) перпендикуляр ccw — wall-follow
                //   5) перпендикуляр cw — wall-follow в другую сторону
                if (TryStep(enemy, pos, step, grid, floorCenter, worldSize)) continue;
                if (TryStep(enemy, pos, new Vector2(step.x, 0f), grid, floorCenter, worldSize)) continue;
                if (TryStep(enemy, pos, new Vector2(0f, step.y), grid, floorCenter, worldSize)) continue;

                var perpCcw = new Vector2(-dir.y, dir.x) * stepLen;
                if (TryStep(enemy, pos, perpCcw, grid, floorCenter, worldSize)) continue;
                var perpCw = new Vector2(dir.y, -dir.x) * stepLen;
                if (TryStep(enemy, pos, perpCw, grid, floorCenter, worldSize)) continue;
            }
        }

        /// <summary>
        /// Пробует применить шаг: проверяет, что новая клетка не Territory, и при успехе
        /// двигает врага. Возвращает true если шаг применён.
        /// </summary>
        private static bool TryStep(Enemy enemy, Vector2 pos, Vector2 step, ArenaGrid grid, Vector2 floorCenter, Vector2 worldSize)
        {
            if (step.sqrMagnitude < 1e-6f) return false;
            var newPos = pos + step;
            var newCell = grid.WorldToCell(newPos, floorCenter, worldSize);
            if (grid.IsInside(newCell) && grid.Get(newCell) == CellState.Territory) return false;
            enemy.MoveByXZ(step);
            return true;
        }

        private void Tick()
        {
            var grid = _arena.Grid;
            var floor = _arena.Floor;
            var floorCenter = floor.FloorCenterXZ;
            var worldSize = floor.WorldSize;

            // Reverse-iter — безопасный RemoveAt на гибели врага (Tier 0 mass-кулл).
            for (var i = _active.Count - 1; i >= 0; i--)
            {
                var enemy = _active[i];
                if (enemy == null)
                {
                    _active.RemoveAt(i);
                    continue;
                }

                var pos = enemy.WorldXZ;
                var cell = grid.WorldToCell(pos, floorCenter, worldSize);

                // Damage только когда стоит на Territory (закрыли вокруг). Обычный
                // путь — враг блокируется на границе и грызёт, не получая урона.
                if (grid.IsInside(cell) && grid.Get(cell) == CellState.Territory)
                {
                    enemy.ApplyTerritoryDamage(1);
                    if (enemy.Hp <= 0)
                    {
                        enemy.Despawn();
                        continue;
                    }
                }

                // Pressure-amplified erosion: чем плотнее толпа жмёт на одну точку,
                // тем шире каждый враг прогрызает. Solo (others=0) → bonus=0.
                var bonus = 0;
                if (_pressureBonusPerNeighbor > 0f && _pressureRadius > 0f)
                {
                    var others = CountOtherNeighborsXZ(enemy, pos, _pressureRadius);
                    bonus = Mathf.FloorToInt(others * _pressureBonusPerNeighbor);
                }
                var effectiveRadius = enemy.Config.EatRadiusCells + bonus;
                _arena.EraseTerritoryAt(pos, effectiveRadius);
            }
        }

        /// <summary>
        /// Подсчёт врагов в радиусе вокруг <paramref name="self"/>, исключая самого. Через
        /// spatial hash → амортизированно O(1) на запрос (≤4 bin'а × среднюю плотность).
        /// </summary>
        private int CountOtherNeighborsXZ(Enemy self, Vector2 pos, float radius)
        {
            _spatialHash.Query(pos, radius, _queryBuffer);
            var radiusSqr = radius * radius;
            var count = 0;
            for (var i = 0; i < _queryBuffer.Count; i++)
            {
                var other = _queryBuffer[i];
                if (other == null || other == self) continue;
                if ((other.WorldXZ - pos).sqrMagnitude <= radiusSqr) count++;
            }
            return count;
        }
    }
}

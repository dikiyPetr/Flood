using System.Collections.Generic;
using Floor;
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
        [SerializeField] private float _tickIntervalSeconds = 1f;

        private readonly List<Enemy> _active = new List<Enemy>();
        private readonly Dictionary<Enemy, Stack<Enemy>> _poolByPrefab = new Dictionary<Enemy, Stack<Enemy>>();
        private float _accumulator;

        public ArenaState Arena => _arena;
        public Vector2 ArenaCenterXZ => _arena.Floor.FloorCenterXZ;

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

            for (var i = 0; i < _active.Count; i++)
            {
                var enemy = _active[i];
                var pos = enemy.WorldXZ;
                var dir = floorCenter - pos;
                var sqr = dir.sqrMagnitude;
                if (sqr < 0.0001f) continue;
                dir /= Mathf.Sqrt(sqr);

                var step = dir * (enemy.Config.MoveSpeed * dt);
                var newPos = pos + step;

                // Строгий блок: вход на Territory запрещён всегда, без исключений.
                // Если врага накрыло заливкой (центральная клетка стала Territory) — он
                // заморожен до тех пор, пока 1Hz Tick не съест клетку под ним и/или соседей,
                // открыв Empty-проход. Раньше тут был выпускающий if (!currentIsTerritory),
                // он превращал попадание в краску в free pass — враг ходил по territory как угодно.
                var newCell = grid.WorldToCell(newPos, floorCenter, worldSize);
                if (grid.IsInside(newCell) && grid.Get(newCell) == CellState.Territory) continue;

                enemy.MoveByXZ(step);
            }
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

                // Эрозия идёт всегда: радиус включает соседние клетки спереди — это
                // механизм продвижения. На Empty-соседях EraseTerritoryAt — no-op.
                _arena.EraseTerritoryAt(pos, enemy.Config.EatRadiusCells);
            }
        }
    }
}

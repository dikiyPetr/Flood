using UnityEngine;

namespace Enemy
{
    /// <summary>
    /// Дебаг-спавнер: периодически кладёт префаб с конфигом в случайную точку диска радиуса
    /// <see cref="EnemyRadiusSpawnerConfig.Radius"/> вокруг собственной позиции (XZ-плоскость,
    /// Y фиксирован у позиции спавнера). В отличие от <see cref="EnemySpawner"/> (точки по
    /// периметру арены) — позволяет быстро локально набросать толпу для теста flow-field навигации.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyRadiusSpawner : MonoBehaviour
    {
        [SerializeField] private EnemyManager _manager;
        [SerializeField] private Enemy _prefab;
        [SerializeField] private EnemyConfig _config;
        [SerializeField] private EnemyRadiusSpawnerConfig _spawnerConfig;

        private float _accumulator;
        private int _aliveCount;

        private void Update()
        {
            _accumulator += Time.deltaTime;
            if (_accumulator < _spawnerConfig.IntervalSeconds) return;
            _accumulator -= _spawnerConfig.IntervalSeconds;

            for (var i = 0; i < _spawnerConfig.PerTick; i++)
            {
                if (_aliveCount >= _spawnerConfig.MaxAlive) return;
                _manager.Spawn(_prefab, _config, PickPoint());
                _aliveCount++;
            }
        }

        private Vector3 PickPoint()
        {
            // Равномерное сэмплирование в кольце [innerRadius, radius]: r = sqrt(lerp(inner², outer²)),
            // φ ∈ [0, 2π). Простой sqrt(rand) сместил бы плотность к центру при innerRadius=0,
            // а вариант через квадраты остаётся равномерным и для кольца.
            var innerRadius = _spawnerConfig.InnerRadius;
            var radius = _spawnerConfig.Radius;
            var inner2 = innerRadius * innerRadius;
            var outer2 = radius * radius;
            var r = Mathf.Sqrt(Mathf.Lerp(inner2, outer2, Random.value));
            var phi = Random.value * Mathf.PI * 2f;
            var p = transform.position;
            return new Vector3(p.x + Mathf.Cos(phi) * r, p.y, p.z + Mathf.Sin(phi) * r);
        }

        private void OnDrawGizmosSelected()
        {
            // Edit-time: Awake/Start не отрабатывали, _spawnerConfig может быть unassigned
            // в свежем префабе — без guard'а Unity заваливает gizmos-pass NRE-логами.
            if (_spawnerConfig == null) return;
            Gizmos.color = new Color(1f, 0.7f, 0.2f, 0.6f);
            DrawCircleXZ(transform.position, _spawnerConfig.Radius, 48);
            if (_spawnerConfig.InnerRadius > 0f)
            {
                Gizmos.color = new Color(1f, 0.4f, 0.4f, 0.6f);
                DrawCircleXZ(transform.position, _spawnerConfig.InnerRadius, 32);
            }
        }

        private static void DrawCircleXZ(Vector3 center, float radius, int segments)
        {
            var prev = center + new Vector3(radius, 0f, 0f);
            for (var i = 1; i <= segments; i++)
            {
                var a = i / (float)segments * Mathf.PI * 2f;
                var next = center + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }
    }
}

using Floor;
using UnityEngine;

namespace Enemy
{
    /// <summary>
    /// Дебаг-источник врагов: периодически спавнит указанный префаб с указанным конфигом
    /// в случайной точке края арены (по периметру `WorldSize`). Полноценные волны GDD §4.2
    /// (расписание, телеграф секторов) — отдельная история.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemySpawner : MonoBehaviour
    {
        [SerializeField] private EnemyManager _manager;
        [SerializeField] private Enemy _prefab;
        [SerializeField] private EnemyConfig _config;
        [SerializeField] private float _intervalSeconds = 3f;
        [SerializeField] private int _maxAlive = 20;

        private float _accumulator;
        private int _aliveCount;

        private void Update()
        {
            if (_manager == null || _prefab == null || _config == null) return;
            _accumulator += Time.deltaTime;
            if (_accumulator < _intervalSeconds) return;
            _accumulator -= _intervalSeconds;

            if (_aliveCount >= _maxAlive) return;

            var pos = PickEdgePoint(_manager.Arena.Floor);
            _manager.Spawn(_prefab, _config, pos);
            _aliveCount++;
        }

        private static Vector3 PickEdgePoint(PaintableFloor floor)
        {
            var center = floor.FloorCenterXZ;
            var size = floor.WorldSize;
            var halfX = size.x * 0.5f;
            var halfY = size.y * 0.5f;

            // Выбираем сторону: 0=top,1=bottom,2=left,3=right.
            var side = Random.Range(0, 4);
            float x, z;
            switch (side)
            {
                case 0: x = center.x + Random.Range(-halfX, halfX); z = center.y + halfY; break;
                case 1: x = center.x + Random.Range(-halfX, halfX); z = center.y - halfY; break;
                case 2: x = center.x - halfX; z = center.y + Random.Range(-halfY, halfY); break;
                default: x = center.x + halfX; z = center.y + Random.Range(-halfY, halfY); break;
            }
            return new Vector3(x, 0f, z);
        }
    }
}

using UnityEngine;

namespace Enemy
{
    /// <summary>
    /// Тюнинг локального дебаг-спавнера (<see cref="EnemyRadiusSpawner"/>): кольцо
    /// сэмплирования вокруг позиции спавнера, темп и кап.
    /// </summary>
    [CreateAssetMenu(menuName = "Flood/Enemy/Enemy Radius Spawner Config", fileName = "EnemyRadiusSpawnerConfig")]
    public sealed class EnemyRadiusSpawnerConfig : ScriptableObject
    {
        [SerializeField, Min(0f)] private float _radius = 3f;
        [SerializeField, Min(0f)] private float _innerRadius = 0f;
        [SerializeField, Min(0.01f)] private float _intervalSeconds = 0.5f;
        [SerializeField, Min(1)] private int _maxAlive = 30;
        [SerializeField, Min(1)] private int _perTick = 1;

        public float Radius => _radius;
        public float InnerRadius => _innerRadius;
        public float IntervalSeconds => _intervalSeconds;
        public int MaxAlive => _maxAlive;
        public int PerTick => _perTick;
    }
}

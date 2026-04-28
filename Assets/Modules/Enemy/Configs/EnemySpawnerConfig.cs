using UnityEngine;

namespace Enemy
{
    /// <summary>
    /// Тюнинг периметрального дебаг-спавнера (<see cref="EnemySpawner"/>): период между
    /// спавнами и кап одновременно живых.
    /// </summary>
    [CreateAssetMenu(menuName = "Flood/Enemy/Enemy Spawner Config", fileName = "EnemySpawnerConfig")]
    public sealed class EnemySpawnerConfig : ScriptableObject
    {
        [SerializeField, Min(0.01f)] private float _intervalSeconds = 3f;
        [SerializeField, Min(1)] private int _maxAlive = 20;

        public float IntervalSeconds => _intervalSeconds;
        public int MaxAlive => _maxAlive;
    }
}

using UnityEngine;

namespace Enemy
{
    /// <summary>
    /// Юнит-враг. Data-holder + transform: HP, конфиг, ссылка на менеджер и префаб для пула.
    /// Логика тиков (урон + эрозия) и движения живёт в <see cref="EnemyManager"/>, чтобы
    /// один Update гонял весь список (см. CLAUDE.md модуля, Tier 0).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Enemy : MonoBehaviour
    {
        private EnemyManager _manager;
        private EnemyConfig _config;
        private Enemy _prefab;
        private int _hp;

        public EnemyConfig Config => _config;
        public Enemy Prefab => _prefab;
        public int Hp => _hp;
        public Vector2 WorldXZ => new Vector2(transform.position.x, transform.position.z);

        public void Init(EnemyManager manager, EnemyConfig config, Enemy prefab)
        {
            _manager = manager;
            _config = config;
            _prefab = prefab;
            _hp = config.MaxHp;
        }

        public void ApplyTerritoryDamage(int amount)
        {
            _hp -= amount;
        }

        public void Despawn()
        {
            _manager.Return(this);
        }

        public void MoveByXZ(Vector2 stepXZ)
        {
            var p = transform.position;
            p.x += stepXZ.x;
            p.z += stepXZ.y;
            transform.position = p;
        }
    }
}

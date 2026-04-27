using UnityEngine;

namespace Enemy
{
    /// <summary>
    /// Параметры одного типа врага (GDD §4.1). Heavy/масло — вне MVP-объёма.
    /// </summary>
    [CreateAssetMenu(menuName = "Flood/Enemy/Enemy Config")]
    public sealed class EnemyConfig : ScriptableObject
    {
        [SerializeField] private EnemyType _type = EnemyType.Infantry;
        [SerializeField] private int _maxHp = 2;
        [SerializeField] private float _moveSpeed = 1.5f;
        [SerializeField] private int _eatRadiusCells = 1;

        public EnemyType Type => _type;
        public int MaxHp => _maxHp;
        public float MoveSpeed => _moveSpeed;
        public int EatRadiusCells => _eatRadiusCells;
    }
}

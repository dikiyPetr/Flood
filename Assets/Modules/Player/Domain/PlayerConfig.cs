using UnityEngine;

namespace Player
{
    /// <summary>
    /// Конфигурация передвижения игрока.
    /// </summary>
    [CreateAssetMenu(menuName = "Flood/Player/Player Config", fileName = "PlayerConfig")]
    public sealed class PlayerConfig : ScriptableObject
    {
        /// <summary>
        /// Базовая скорость движения, мировых единиц в секунду.
        /// </summary>
        [field: SerializeField, Min(0f)]
        public float MoveSpeed { get; private set; } = 5f;

        /// <summary>
        /// Множитель скорости при включённом спринте.
        /// </summary>
        [field: SerializeField, Min(1f)]
        public float SprintMultiplier { get; private set; } = 1.6f;

        /// <summary>
        /// Угловая скорость доворота к направлению ввода, градусов в секунду.
        /// </summary>
        [field: SerializeField, Min(0f)]
        public float RotationSpeed { get; private set; } = 720f;

        internal void SetForTests(float moveSpeed, float sprintMultiplier, float rotationSpeed)
        {
            MoveSpeed = moveSpeed;
            SprintMultiplier = sprintMultiplier;
            RotationSpeed = rotationSpeed;
        }
    }
}

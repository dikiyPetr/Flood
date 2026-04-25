using UnityEngine;

namespace Player
{
    /// <summary>
    /// Результат расчёта одного кадра передвижения: смещение и желаемое направление взгляда.
    /// </summary>
    public readonly struct MovementStep
    {
        /// <summary>
        /// Мировое смещение игрока за кадр, в юнитах.
        /// </summary>
        public readonly Vector3 Displacement;

        /// <summary>
        /// Нормализованное желаемое направление взгляда; <see cref="Vector3.zero"/>, если ввода нет.
        /// </summary>
        public readonly Vector3 FacingDirection;

        /// <summary>
        /// Создаёт шаг передвижения.
        /// </summary>
        /// <param name="displacement">Смещение за кадр.</param>
        /// <param name="facingDirection">Направление взгляда.</param>
        public MovementStep(Vector3 displacement, Vector3 facingDirection)
        {
            Displacement = displacement;
            FacingDirection = facingDirection;
        }
    }
}

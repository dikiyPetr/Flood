using System;
using UnityEngine;

namespace Player
{
    /// <summary>
    /// Чистый расчёт передвижения игрока: преобразует двумерный ввод в плоское смещение по XZ.
    /// </summary>
    public sealed class PlayerMovementCalculator
    {
        private readonly PlayerConfig _config;

        /// <summary>
        /// Создаёт калькулятор поверх заданной конфигурации.
        /// </summary>
        /// <param name="config">Конфигурация игрока.</param>
        /// <exception cref="ArgumentNullException">Если <paramref name="config"/> равен <c>null</c>.</exception>
        public PlayerMovementCalculator(PlayerConfig config)
        {
            _config = config != null ? config : throw new ArgumentNullException(nameof(config));
        }

        /// <summary>
        /// Считает шаг передвижения для текущего ввода.
        /// </summary>
        /// <param name="input">Ввод на плоскости (x → world X, y → world Z).</param>
        /// <param name="isSprinting">Признак активного спринта.</param>
        /// <param name="deltaTime">Длительность кадра в секундах.</param>
        /// <returns>Смещение и направление взгляда для кадра.</returns>
        public MovementStep Calculate(Vector2 input, bool isSprinting, float deltaTime)
        {
            var planar = new Vector3(input.x, 0f, input.y);
            if (planar.sqrMagnitude <= Mathf.Epsilon)
            {
                return default;
            }

            var magnitude = planar.magnitude;
            var direction = planar / magnitude;
            // Причина зажима вместо безусловной нормализации:
            // сохраняем частичное отклонение аналогового стика (значения <1 двигают медленнее),
            // но при этом гасим возможные >1 значения от композитных вводов клавиатуры.
            var clampedMagnitude = Mathf.Min(magnitude, 1f);
            var speed = _config.MoveSpeed * (isSprinting ? _config.SprintMultiplier : 1f);
            var displacement = deltaTime > 0f
                ? direction * (clampedMagnitude * speed * deltaTime)
                : Vector3.zero;
            return new MovementStep(displacement, direction);
        }
    }
}

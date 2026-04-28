using UnityEngine;

namespace Enemy
{
    /// <summary>
    /// Тюнинг оркестратора врагов: тик урона/эрозии, pressure-amplified erosion,
    /// crowd separation (Boids) и crowd-aware speed scaling. Формулы и калибровка —
    /// в CLAUDE.md модуля Enemy, разделы «Контракты» и «Производительность».
    /// </summary>
    [CreateAssetMenu(menuName = "Flood/Enemy/Enemy Manager Config", fileName = "EnemyManagerConfig")]
    public sealed class EnemyManagerConfig : ScriptableObject
    {
        [SerializeField, Min(0.01f)] private float _tickIntervalSeconds = 1f;

        [Header("Pressure-amplified erosion")]
        [Tooltip("Радиус (мировые единицы) для подсчёта соседей при скейле eat-радиуса.")]
        [SerializeField, Min(0f)] private float _pressureRadius = 1.5f;
        [Tooltip("Прибавка к eat-радиусу за каждого соседа в _pressureRadius. " +
                 "effectiveRadius = baseRadius + floor(neighbors * factor). 0 = выключено.")]
        [SerializeField, Min(0f)] private float _pressureBonusPerNeighbor = 0.4f;

        [Header("Crowd separation (Boids)")]
        [Tooltip("Радиус (мировые единицы) отталкивания соседей. 0 = выключено. " +
                 "Брать ~ диаметр визуальной модели врага.")]
        [SerializeField, Min(0f)] private float _separationRadius = 0.6f;
        [Tooltip("Вес отталкивания относительно направления к цели. 0 = выкл, ~1 = равноценно потоку.")]
        [SerializeField, Min(0f)] private float _separationWeight = 1f;

        [Header("Crowd-aware speed scaling")]
        [Tooltip("Радиус (мировые единицы) для подсчёта плотности при замедлении. " +
                 "Обычно ≤ _separationRadius — замедляет именно «застрявших в спине».")]
        [SerializeField, Min(0f)] private float _crowdSlowdownRadius = 0.6f;
        [Tooltip("Сила замедления: speed *= 1 / (1 + factor * neighbors). 0 = выкл, " +
                 "0.4 → 4 соседа дают ~38% скорости, 10 соседей → 20%.")]
        [SerializeField, Min(0f)] private float _crowdSlowdownFactor = 0.4f;

        public float TickIntervalSeconds => _tickIntervalSeconds;
        public float PressureRadius => _pressureRadius;
        public float PressureBonusPerNeighbor => _pressureBonusPerNeighbor;
        public float SeparationRadius => _separationRadius;
        public float SeparationWeight => _separationWeight;
        public float CrowdSlowdownRadius => _crowdSlowdownRadius;
        public float CrowdSlowdownFactor => _crowdSlowdownFactor;
    }
}

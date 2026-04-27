using System;
using UnityEngine;

namespace Navigation
{
    /// <summary>
    /// Инспектор-биндинг цели: либо привязка к <see cref="Anchor"/> (положение читается каждый
    /// rebuild — для движущихся целей), либо фиксированная мировая точка <see cref="WorldPoint"/>.
    /// При обоих null/zero-anchor — будет использован центр пола (см. <see cref="FlowFieldNavigator"/>).
    /// </summary>
    [Serializable]
    public sealed class NavigationGoalBinding
    {
        [SerializeField] private Transform _anchor;
        [SerializeField] private Vector3 _worldPoint;
        [SerializeField, Range(0f, 1f)] private float _weight = 1f;

        public Transform Anchor => _anchor;
        public Vector3 WorldPoint => _worldPoint;
        public float Weight => _weight;

        /// <summary>Текущая мировая XZ-точка цели — Anchor.position если задан, иначе WorldPoint.</summary>
        public Vector2 ResolveWorldXZ(Vector2 fallbackXZ)
        {
            if (_anchor != null)
            {
                var p = _anchor.position;
                return new Vector2(p.x, p.z);
            }
            // Если WorldPoint не задан явно (всё нули) — используем fallback (обычно центр пола).
            if (_worldPoint == Vector3.zero) return fallbackXZ;
            return new Vector2(_worldPoint.x, _worldPoint.z);
        }
    }
}

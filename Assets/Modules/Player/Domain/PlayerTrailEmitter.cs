using Floor;
using UnityEngine;

namespace Player
{
    /// <summary>
    /// Эмиттер активного следа игрока: каждый кадр после движения передаёт мировую XZ-точку
    /// в <see cref="ArenaState.AppendTrailPoint"/>. Растеризация в клетки, рисование на маске
    /// линии и детекция замыкания живут в Floor-модуле.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerTrailEmitter : MonoBehaviour
    {
        [SerializeField] private ArenaState _arena;

        private void LateUpdate()
        {
            // LateUpdate, чтобы PlayerController уже передвинул transform в Update.
            var position = transform.position;
            _arena.AppendTrailPoint(new Vector2(position.x, position.z));
        }
    }
}

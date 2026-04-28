using UnityEngine;

namespace Floor
{
    /// <summary>
    /// Простейший компонент-кисть: каждый кадр проектирует собственную мировую XZ-позицию
    /// на материал указанного <see cref="PaintableFloor"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FloorPainter : MonoBehaviour
    {
        [SerializeField] private PaintableFloor _floor;
        [SerializeField, Min(0f)] private float _radius = 0.5f;

        private void Update()
        {
            var position = transform.position;
            _floor.PaintAt(new Vector2(position.x, position.z), _radius);
        }
    }
}

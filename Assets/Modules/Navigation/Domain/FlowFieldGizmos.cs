using Floor;
using UnityEngine;

namespace Navigation
{
    /// <summary>
    /// Визуализация flow-field'а в Scene view. Два режима, тогглятся независимо: стрелки потока
    /// (цвет по <see cref="CellState"/>) и препятствия. Не выполняет работы в рантайме —
    /// только <c>OnDrawGizmos</c>.
    ///
    /// Стрелки sub-sample'ятся по гриду через <see cref="_arrowStride"/>: рисуется каждая N-я
    /// клетка по X и Z. На <c>res=500, stride=4</c> → ~125×125 = ~15 000 стрелок вместо 250 000;
    /// число <see cref="Gizmos.DrawLine"/> вызовов падает на два порядка, Scene view не лагает.
    /// При stride=1 поведение совпадает с полным рендером.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FlowFieldGizmos : MonoBehaviour
    {
        [SerializeField] private FlowFieldNavigator _navigator;

        [Header("Modes")]
        [SerializeField] private bool _drawArrows = true;
        [SerializeField] private bool _drawObstacles = true;

        [Header("Arrows")]
        [Tooltip("Шаг sub-sampling'а: рисовать каждую N-ю клетку по X и Z. Главный регулятор " +
                 "стоимости рендера на крупном гриде. Для res=500 рекомендуемое значение 4-8.")]
        [SerializeField, Range(1, 32)] private int _arrowStride = 4;
        [SerializeField, Range(0.1f, 0.9f)] private float _arrowLengthFactor = 0.4f;
        [SerializeField, Range(0f, 0.05f)] private float _yOffset = 0.02f;

        private void OnDrawGizmos()
        {
            // Edit-time: на свежем компоненте без assignment Unity вызывает gizmos-pass
            // до Awake — без guard'а посыпались бы NRE-логи в Scene view.
            if (_navigator == null) return;
            var arena = _navigator.Arena;
            var field = _navigator.Field;
            // _field/_grid — runtime-state, до первого Build/Awake они null.
            if (field == null || arena.Grid == null) return;

            var floor = arena.Floor;
            var center = floor.FloorCenterXZ;
            var worldSize = floor.WorldSize;
            var grid = arena.Grid;
            var res = grid.Resolution;
            var cellSize = worldSize.x / res;
            var y = transform.position.y + _yOffset;

            if (_drawObstacles) DrawObstacles(grid, center, worldSize, cellSize, y);
            if (_drawArrows) DrawArrows(grid, field, center, worldSize, cellSize, y);
        }

        private void DrawObstacles(ArenaGrid grid, Vector2 center, Vector2 worldSize, float cellSize, float y)
        {
            var size = new Vector3(cellSize * 0.95f, 0.05f, cellSize * 0.95f);
            Gizmos.color = new Color(0.6f, 0f, 0.8f, 0.7f);
            for (var x = 0; x < grid.Resolution; x++)
            {
                for (var z = 0; z < grid.Resolution; z++)
                {
                    if (grid.Get(new Vector2Int(x, z)) != CellState.Obstacle) continue;
                    var cellWorld = grid.CellCenterWorld(new Vector2Int(x, z), center, worldSize);
                    Gizmos.DrawCube(new Vector3(cellWorld.x, y, cellWorld.y), size);
                }
            }
        }

        private void DrawArrows(ArenaGrid grid, FlowField field, Vector2 center, Vector2 worldSize, float cellSize, float y)
        {
            var len = cellSize * _arrowLengthFactor;
            var stride = Mathf.Max(1, _arrowStride);
            for (var x = 0; x < field.Resolution; x += stride)
            {
                for (var z = 0; z < field.Resolution; z += stride)
                {
                    var idx = field.IndexOf(x, z);
                    var dir = field.Direction[idx];
                    if (dir == Vector2.zero) continue;

                    var cellWorld = grid.CellCenterWorld(new Vector2Int(x, z), center, worldSize);
                    var from = new Vector3(cellWorld.x, y, cellWorld.y);
                    var to = new Vector3(cellWorld.x + dir.x * len, y, cellWorld.y + dir.y * len);

                    Gizmos.color = ColorForCell(grid.Get(new Vector2Int(x, z)));
                    Gizmos.DrawLine(from, to);

                    // Наконечник: две короткие линии под ±30° от dir, длиной len * 0.35.
                    var headLen = len * 0.35f;
                    var ang = Mathf.Atan2(dir.y, dir.x);
                    var a1 = ang + Mathf.PI * (1f - 30f / 180f);
                    var a2 = ang - Mathf.PI * (1f - 30f / 180f);
                    var h1 = new Vector3(to.x + Mathf.Cos(a1) * headLen, y, to.z + Mathf.Sin(a1) * headLen);
                    var h2 = new Vector3(to.x + Mathf.Cos(a2) * headLen, y, to.z + Mathf.Sin(a2) * headLen);
                    Gizmos.DrawLine(to, h1);
                    Gizmos.DrawLine(to, h2);
                }
            }
        }

        private static Color ColorForCell(CellState state)
        {
            switch (state)
            {
                case CellState.Territory: return new Color(1f, 0.4f, 0.4f);
                case CellState.Line: return Color.cyan;
                case CellState.Obstacle: return new Color(0.6f, 0f, 0.8f);
                default: return Color.white;
            }
        }
    }
}

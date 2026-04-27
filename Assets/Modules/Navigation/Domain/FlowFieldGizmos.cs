using Floor;
using UnityEngine;

namespace Navigation
{
    /// <summary>
    /// Визуализация flow-field'а в Scene view. Три режима, тогглятся независимо:
    /// стрелки потока, heat-карта по integration, трассировка пути от пробы.
    /// Не выполняет работы в рантайме — только OnDrawGizmos.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FlowFieldGizmos : MonoBehaviour
    {
        [SerializeField] private FlowFieldNavigator _navigator;

        [Header("Modes")]
        [SerializeField] private bool _drawArrows = true;
        [SerializeField] private bool _drawHeat;
        [SerializeField] private bool _drawProbePath;

        [Header("Arrows")]
        [SerializeField, Range(0.1f, 0.9f)] private float _arrowLengthFactor = 0.4f;
        [SerializeField, Range(0f, 0.05f)] private float _yOffset = 0.02f;

        [Header("Heat")]
        [SerializeField, Min(1)] private int _heatMaxIntegration = 4096;
        [SerializeField, Range(0f, 1f)] private float _heatAlpha = 0.35f;

        [Header("Probe")]
        [SerializeField] private Transform _probe;
        [SerializeField, Min(1)] private int _probeSteps = 96;
        [SerializeField, Range(0.1f, 1f)] private float _probeStepFactor = 0.5f;

        private void OnDrawGizmos()
        {
            if (_navigator == null) return;
            var field = _navigator.Field;
            var arena = _navigator.Arena;
            if (field == null || arena == null || arena.Grid == null) return;

            var floor = arena.Floor;
            if (floor == null) return;
            var center = floor.FloorCenterXZ;
            var worldSize = floor.WorldSize;
            var grid = arena.Grid;
            var res = grid.Resolution;
            var cellSize = worldSize.x / res;
            var y = transform.position.y + _yOffset;

            if (_drawHeat) DrawHeat(grid, field, center, worldSize, cellSize, y);
            if (_drawArrows) DrawArrows(grid, field, center, worldSize, cellSize, y);
            if (_drawProbePath) DrawProbePath(grid, field, center, worldSize, cellSize, y);
        }

        private void DrawArrows(ArenaGrid grid, FlowField field, Vector2 center, Vector2 worldSize, float cellSize, float y)
        {
            var len = cellSize * _arrowLengthFactor;
            for (var x = 0; x < field.Resolution; x++)
            {
                for (var z = 0; z < field.Resolution; z++)
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

        private void DrawHeat(ArenaGrid grid, FlowField field, Vector2 center, Vector2 worldSize, float cellSize, float y)
        {
            var size = new Vector3(cellSize * 0.95f, 0.001f, cellSize * 0.95f);
            for (var x = 0; x < field.Resolution; x++)
            {
                for (var z = 0; z < field.Resolution; z++)
                {
                    var idx = field.IndexOf(x, z);
                    var integ = field.Integration[idx];
                    if (integ == FlowField.Unreachable) continue;

                    var t = Mathf.Clamp01(integ / (float)_heatMaxIntegration);
                    var col = Color.Lerp(Color.green, Color.red, t);
                    col.a = _heatAlpha;
                    Gizmos.color = col;

                    var cellWorld = grid.CellCenterWorld(new Vector2Int(x, z), center, worldSize);
                    Gizmos.DrawCube(new Vector3(cellWorld.x, y, cellWorld.y), size);
                }
            }
        }

        private void DrawProbePath(ArenaGrid grid, FlowField field, Vector2 center, Vector2 worldSize, float cellSize, float y)
        {
            if (_probe == null) return;

            var step = cellSize * _probeStepFactor;
            var p = new Vector2(_probe.position.x, _probe.position.z);
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(new Vector3(p.x, y, p.y), cellSize * 0.15f);

            for (var s = 0; s < _probeSteps; s++)
            {
                var cell = grid.WorldToCell(p, center, worldSize);
                if (!field.IsInside(cell)) break;
                var dir = field.SampleDirection(cell);
                if (dir == Vector2.zero) break;

                var next = p + dir * step;
                Gizmos.DrawLine(new Vector3(p.x, y, p.y), new Vector3(next.x, y, next.y));
                p = next;
            }

            Gizmos.color = Color.green;
            Gizmos.DrawSphere(new Vector3(p.x, y, p.y), cellSize * 0.2f);
        }

        private static Color ColorForCell(CellState state)
        {
            switch (state)
            {
                case CellState.Territory: return new Color(1f, 0.4f, 0.4f);
                case CellState.Line: return Color.cyan;
                default: return Color.white;
            }
        }
    }
}

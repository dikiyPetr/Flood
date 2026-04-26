using System.Collections.Generic;
using UnityEngine;

namespace Floor
{
    /// <summary>
    /// Состояние арены: владеет CPU-сеткой <see cref="ArenaGrid"/>, синхронизирует её с
    /// рендертекстурой пола (через подписку на <see cref="PaintableFloor.Painted"/>) и резолвит
    /// замыкание петли.
    ///
    /// На этапе 3 — мгновенная заливка enclosed-региона. На этапе 4 — делегирует заливку в
    /// <see cref="FloodFillAnimator"/>, если он назначен в инспекторе.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ArenaState : MonoBehaviour
    {
        [SerializeField] private ArenaConfig _config;
        [SerializeField] private PaintableFloor _floor;
        [SerializeField] private FloodFillAnimator _animator;

        private ArenaGrid _grid;
        private TrailRasterizer _rasterizer;

        public ArenaGrid Grid => _grid;
        public PaintableFloor Floor => _floor;
        public ArenaConfig Config => _config;

        private void Awake()
        {
            _grid = new ArenaGrid(_config.GridResolution);
            _rasterizer = new TrailRasterizer(_grid, _floor);
        }

        private void OnEnable()
        {
            _floor.Painted += OnPainted;
        }

        private void OnDisable()
        {
            // Защита от порядка уничтожения: если _floor разрушен раньше при выгрузке сцены,
            // обращение к Painted упало бы на overrided Unity-операторе ==.
            if (_floor != null)
            {
                _floor.Painted -= OnPainted;
            }
        }

        /// <summary>
        /// Регистрирует новую точку траектории игрока. При замыкании — резолвит петлю.
        /// </summary>
        public void AppendTrailPoint(Vector2 worldXZ)
        {
            var result = _rasterizer.AppendPoint(worldXZ);
            if (result.Closed)
            {
                ResolveClosure();
            }
        }

        private void OnPainted(Vector2 worldXZ, float worldRadius)
        {
            // Любая закраска _paintRT (дебажная кисть, init-зона, FloodFillAnimator) отражается
            // в гриде как Territory. Set идемпотентен — повторный Territory от Animator OK.
            var floorCenter = _floor.FloorCenterXZ;
            var worldSize = _floor.WorldSize;
            var center = _grid.WorldToCell(worldXZ, floorCenter, worldSize);

            // Центральная клетка маркируется всегда, даже если радиус меньше cellSize.
            if (_grid.IsInside(center))
            {
                _grid.Set(center, CellState.Territory);
            }

            var cellSize = worldSize.x / _grid.Resolution;
            var cellRadius = Mathf.CeilToInt(worldRadius / cellSize);
            if (cellRadius <= 0) return;

            for (var dx = -cellRadius; dx <= cellRadius; dx++)
            {
                for (var dy = -cellRadius; dy <= cellRadius; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    var cell = new Vector2Int(center.x + dx, center.y + dy);
                    if (!_grid.IsInside(cell)) continue;

                    var cellWorld = _grid.CellCenterWorld(cell, floorCenter, worldSize);
                    if ((cellWorld - worldXZ).sqrMagnitude > worldRadius * worldRadius) continue;

                    _grid.Set(cell, CellState.Territory);
                }
            }
        }

        private void ResolveClosure()
        {
            // Шаг 1: все Line-клетки превращаются в Territory с мазком на _paintRT.
            // PaintAtSilent — Painted event тут не нужен, состоянием грида управляем напрямую.
            var resolution = _grid.Resolution;
            var floorCenter = _floor.FloorCenterXZ;
            var worldSize = _floor.WorldSize;

            for (var x = 0; x < resolution; x++)
            {
                for (var y = 0; y < resolution; y++)
                {
                    var cell = new Vector2Int(x, y);
                    if (_grid.Get(cell) != CellState.Line) continue;
                    _grid.Set(cell, CellState.Territory);
                    _floor.PaintAtSilent(_grid.CellCenterWorld(cell, floorCenter, worldSize));
                }
            }

            // Шаг 2: поиск enclosed-региона до очистки маски линии.
            var enclosed = EnclosedRegionFinder.FindEnclosed(_grid);

            // Шаг 3: очистка активного следа.
            _floor.ClearLine();
            _rasterizer.Reset();

            if (enclosed.Count == 0) return;

            // Шаг 4: заливка региона. Animator — постепенно; иначе мгновенно (этап 3 fallback).
            if (_animator != null)
            {
                _animator.Enqueue(enclosed);
                return;
            }

            for (var i = 0; i < enclosed.Count; i++)
            {
                var cell = enclosed[i];
                _grid.Set(cell, CellState.Territory);
                _floor.PaintAtSilent(_grid.CellCenterWorld(cell, floorCenter, worldSize));
            }
        }
    }
}

using System.Collections.Generic;
using Floor;
using UnityEngine;

namespace Navigation
{
    /// <summary>
    /// In-place билдер flow-field'а: cost ← gridSnapshot+config, integration ← Dijkstra от seed-клеток,
    /// direction ← min-neighbour. 8-связный граф, целочисленный cost (×10 ортогональ, ×14 диагональ —
    /// аппроксимация √2), переиспользуемый бинарный min-heap.
    ///
    /// Принимает <see cref="CellState"/>-snapshot (CPU-копия), не live <see cref="ArenaGrid"/>:
    /// <see cref="Build"/> можно вызывать из <see cref="System.Threading.Tasks.Task"/> на background
    /// thread'е без race с main thread'ом, который продолжает писать в live grid. Builder имеет
    /// mutable state (heap) — конкурентные вызовы запрещены.
    /// </summary>
    public sealed class FlowFieldBuilder
    {
        private const int OrthoMul = 10;
        private const int DiagMul = 14;

        // 8 направлений: 0..3 — ортогональные, 4..7 — диагональные.
        private static readonly int[] Dx = { 1, -1, 0, 0, 1, 1, -1, -1 };
        private static readonly int[] Dy = { 0, 0, 1, -1, 1, -1, 1, -1 };
        private static readonly int[] Mul = { OrthoMul, OrthoMul, OrthoMul, OrthoMul, DiagMul, DiagMul, DiagMul, DiagMul };

        private MinHeap _heap = new MinHeap(256);

        /// <summary>
        /// Пересобирает flow-field. <paramref name="target"/> должен иметь
        /// <c>Resolution == resolution</c>. Аллокаций нет, кроме автоувеличения внутреннего heap
        /// при первом большом seed-сете.
        /// </summary>
        public void Build(
            CellState[,] gridSnapshot,
            int resolution,
            NavigationConfig config,
            IReadOnlyList<NavigationGoal> goals,
            Vector2 floorCenterXZ,
            Vector2 worldSize,
            FlowField target)
        {
            var res = resolution;
            var n = res * res;
            var resolutionVec = new Vector2Int(res, res);

            // 1. Cost field из CellState.
            for (var x = 0; x < res; x++)
            {
                for (var y = 0; y < res; y++)
                {
                    target.Cost[target.IndexOf(x, y)] = config.CostFor(gridSnapshot[x, y]);
                }
            }

            // 2. Integration field. Reset + seed.
            for (var i = 0; i < n; i++)
            {
                target.Integration[i] = FlowField.Unreachable;
                target.GoalMask[i] = false;
            }

            _heap.Clear();
            var maxSeedOffset = config.MaxSeedOffset;
            var allowDiag = config.AllowDiagonal;
            var dirCount = allowDiag ? 8 : 4;

            for (var gi = 0; gi < goals.Count; gi++)
            {
                var goal = goals[gi];
                var cell = FloorProjection.ProjectWorldToTexel(goal.WorldXZ, floorCenterXZ, worldSize, resolutionVec);
                if (cell.x < 0 || cell.x >= res || cell.y < 0 || cell.y >= res) continue;

                var idx = target.IndexOf(cell);
                // Цель внутри препятствия не сидируем — иначе Dijkstra пытается «вылезти»
                // из недостижимой клетки наружу. Достаточно положиться на оставшиеся цели
                // (или fallback на FloorCenterXZ из FlowFieldNavigator.Rebuild).
                if (target.Cost[idx] >= NavigationConfig.ObstacleCost) continue;

                var seed = Mathf.RoundToInt((1f - goal.Weight) * maxSeedOffset);
                if (seed < target.Integration[idx])
                {
                    target.Integration[idx] = seed;
                    target.GoalMask[idx] = true;
                    _heap.Push(idx, seed);
                }
            }

            // 3. Dijkstra.
            while (_heap.Count > 0)
            {
                _heap.Pop(out var cIdx, out var dist);
                if (dist > target.Integration[cIdx]) continue; // stale

                var cx = cIdx / res;
                var cy = cIdx % res;

                for (var d = 0; d < dirCount; d++)
                {
                    var nx = cx + Dx[d];
                    var ny = cy + Dy[d];
                    if (nx < 0 || nx >= res || ny < 0 || ny >= res) continue;

                    var nIdx = nx * res + ny;
                    // Препятствие — не релаксируем. Без этого short-circuit'а sentinel-cost
                    // всё равно держит обструкцию, но overflow при `dist + step` теоретически
                    // возможен на больших грид-резолюциях; явный guard дешевле, чем рассуждать
                    // о границах.
                    if (target.Cost[nIdx] >= NavigationConfig.ObstacleCost) continue;
                    var step = target.Cost[nIdx] * Mul[d];
                    var nd = dist + step;
                    if (nd < target.Integration[nIdx])
                    {
                        target.Integration[nIdx] = nd;
                        _heap.Push(nIdx, nd);
                    }
                }
            }

            // 4. Direction field: для каждой клетки берём соседа с минимальной integration.
            for (var x = 0; x < res; x++)
            {
                for (var y = 0; y < res; y++)
                {
                    var idx = x * res + y;
                    if (target.Integration[idx] == FlowField.Unreachable
                        || target.GoalMask[idx]
                        || target.Cost[idx] >= NavigationConfig.ObstacleCost)
                    {
                        target.Direction[idx] = Vector2.zero;
                        continue;
                    }

                    var bestDx = 0;
                    var bestDy = 0;
                    var bestI = target.Integration[idx];
                    for (var d = 0; d < dirCount; d++)
                    {
                        var nx = x + Dx[d];
                        var ny = y + Dy[d];
                        if (nx < 0 || nx >= res || ny < 0 || ny >= res) continue;
                        var ni = target.Integration[nx * res + ny];
                        if (ni < bestI)
                        {
                            bestI = ni;
                            bestDx = Dx[d];
                            bestDy = Dy[d];
                        }
                    }

                    if (bestDx == 0 && bestDy == 0)
                    {
                        target.Direction[idx] = Vector2.zero;
                    }
                    else
                    {
                        // Ось грида (x, y) соответствует мировой (X, Z) — см. ArenaGrid.WorldToCell.
                        var v = new Vector2(bestDx, bestDy);
                        target.Direction[idx] = v.normalized;
                    }
                }
            }
        }

        /// <summary>
        /// Минимальная бинарная куча (idx, priority). Не общего назначения — поднимает только
        /// пары int/int, чтобы не аллоцировать на push (PriorityQueue&lt;,&gt; в .NET Standard 2.1
        /// недоступна).
        /// </summary>
        private sealed class MinHeap
        {
            private int[] _idx;
            private int[] _pri;
            public int Count;

            public MinHeap(int capacity)
            {
                _idx = new int[capacity];
                _pri = new int[capacity];
            }

            public void Clear() { Count = 0; }

            public void Push(int idx, int pri)
            {
                if (Count == _idx.Length) Grow();
                var i = Count++;
                _idx[i] = idx;
                _pri[i] = pri;
                while (i > 0)
                {
                    var p = (i - 1) >> 1;
                    if (_pri[p] <= _pri[i]) break;
                    Swap(p, i);
                    i = p;
                }
            }

            public void Pop(out int idx, out int pri)
            {
                idx = _idx[0];
                pri = _pri[0];
                Count--;
                if (Count == 0) return;
                _idx[0] = _idx[Count];
                _pri[0] = _pri[Count];
                var i = 0;
                while (true)
                {
                    var l = 2 * i + 1;
                    var r = l + 1;
                    if (l >= Count) break;
                    var m = (r < Count && _pri[r] < _pri[l]) ? r : l;
                    if (_pri[i] <= _pri[m]) break;
                    Swap(i, m);
                    i = m;
                }
            }

            private void Swap(int a, int b)
            {
                var ti = _idx[a]; _idx[a] = _idx[b]; _idx[b] = ti;
                var tp = _pri[a]; _pri[a] = _pri[b]; _pri[b] = tp;
            }

            private void Grow()
            {
                var cap = _idx.Length * 2;
                var ni = new int[cap];
                var np = new int[cap];
                System.Array.Copy(_idx, ni, _idx.Length);
                System.Array.Copy(_pri, np, _pri.Length);
                _idx = ni;
                _pri = np;
            }
        }
    }
}

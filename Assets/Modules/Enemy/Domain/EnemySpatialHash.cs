using System.Collections.Generic;
using UnityEngine;

namespace Enemy
{
    /// <summary>
    /// Uniform-grid bin'инг врагов по XZ для амортизированного O(1) запроса соседей.
    /// Rebuild раз в кадр, Query(pos, radius, output) копирует кандидатов в переданный
    /// буфер (caller сам фильтрует точное расстояние). При <c>cellSize ≥ maxQueryRadius</c>
    /// один запрос трогает ≤ 4 bin'а, средняя плотность bin'а ≈ <c>density × cellSize²</c>.
    /// Без аллокаций после прогрева: словарь bin'ов и пул List'ов переиспользуются.
    /// </summary>
    public sealed class EnemySpatialHash
    {
        private readonly Dictionary<long, List<Enemy>> _bins = new Dictionary<long, List<Enemy>>(64);
        private readonly Stack<List<Enemy>> _listPool = new Stack<List<Enemy>>(32);
        private readonly List<long> _keyScratch = new List<long>(64);
        private float _cellSize = 1f;

        public float CellSize => _cellSize;

        /// <summary>
        /// Перестраивает индекс. Вызывать раз в кадр перед запросами. <paramref name="cellSize"/>
        /// должен быть ≥ максимального радиуса будущих запросов — иначе один Query трогает >4 bin'а.
        /// </summary>
        public void Rebuild(IReadOnlyList<Enemy> enemies, float cellSize)
        {
            _cellSize = Mathf.Max(0.0001f, cellSize);

            // Возвращаем все списки в пул и чистим словарь. Снимаем ключи в scratch, чтобы
            // не модифицировать словарь во время foreach.
            _keyScratch.Clear();
            foreach (var kvp in _bins) _keyScratch.Add(kvp.Key);
            for (var i = 0; i < _keyScratch.Count; i++)
            {
                var list = _bins[_keyScratch[i]];
                list.Clear();
                _listPool.Push(list);
            }
            _bins.Clear();

            for (var i = 0; i < enemies.Count; i++)
            {
                var e = enemies[i];
                if (e == null) continue;
                var pos = e.WorldXZ;
                var key = MakeKey(FloorDiv(pos.x, _cellSize), FloorDiv(pos.y, _cellSize));
                if (!_bins.TryGetValue(key, out var list))
                {
                    list = _listPool.Count > 0 ? _listPool.Pop() : new List<Enemy>(8);
                    _bins[key] = list;
                }
                list.Add(e);
            }
        }

        /// <summary>
        /// Заполняет <paramref name="output"/> кандидатами из bin'ов, перекрывающих круг
        /// (<paramref name="pos"/>, <paramref name="radius"/>). Caller сам делает финальную
        /// проверку sqr-distance и пропускает себя/null. Output чистится в начале запроса.
        /// </summary>
        public void Query(Vector2 pos, float radius, List<Enemy> output)
        {
            output.Clear();
            if (radius <= 0f) return;

            var minX = FloorDiv(pos.x - radius, _cellSize);
            var maxX = FloorDiv(pos.x + radius, _cellSize);
            var minY = FloorDiv(pos.y - radius, _cellSize);
            var maxY = FloorDiv(pos.y + radius, _cellSize);

            for (var x = minX; x <= maxX; x++)
            {
                for (var y = minY; y <= maxY; y++)
                {
                    if (!_bins.TryGetValue(MakeKey(x, y), out var list)) continue;
                    for (var i = 0; i < list.Count; i++) output.Add(list[i]);
                }
            }
        }

        // Mathf.FloorToInt + division: корректно округляет вниз для отрицательных координат
        // (стандартный (int)(neg/cell) ведёт к ошибкам бинирования возле начала координат).
        private static int FloorDiv(float v, float cellSize) => Mathf.FloorToInt(v / cellSize);

        // Long-key: 32-bit X в верхних, 32-bit Y в нижних. uint-каст обеспечивает корректное
        // склеивание отрицательных y без потери бит.
        private static long MakeKey(int x, int y) => ((long)x << 32) | (uint)y;
    }
}

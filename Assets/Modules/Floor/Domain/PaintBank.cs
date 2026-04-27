using UnityEngine;

namespace Floor
{
    /// <summary>
    /// Глобальный счётчик краски (GDD §3.6). Заливка списывает 1 ед./клетку через
    /// <see cref="TryConsume"/>; источники (база, шахты, бутыльки) докидывают через
    /// <see cref="Add"/>. Дисплей в MVP — Debug.Log на каждом изменении.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PaintBank : MonoBehaviour
    {
        [SerializeField] private int _initialPaint = 20;
        [SerializeField] private int _maxPaint = 100;

        private int _current;

        public int Current => _current;
        public int Max => _maxPaint;

        private void Awake()
        {
            _current = Mathf.Clamp(_initialPaint, 0, _maxPaint);
            Debug.Log($"[Paint] init {_current}/{_maxPaint}");
        }

        /// <summary>
        /// Атомарно списывает <paramref name="amount"/> ед. Возвращает false, если краски не хватает —
        /// баланс при этом не меняется. Вызывающий обязан корректно обработать отказ
        /// (например, <see cref="FloodFillAnimator"/> прерывает заливку).
        /// </summary>
        public bool TryConsume(int amount)
        {
            if (amount <= 0) return true;
            if (_current < amount) return false;
            var prev = _current;
            _current -= amount;
            Debug.Log($"[Paint] {prev} → {_current} (-{amount})");
            return true;
        }

        public void Add(int amount)
        {
            if (amount <= 0) return;
            var prev = _current;
            _current = Mathf.Min(_current + amount, _maxPaint);
            if (_current == prev) return;
            Debug.Log($"[Paint] {prev} → {_current} (+{_current - prev})");
        }
    }
}

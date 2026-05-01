using UnityEngine;

namespace Floor
{
    /// <summary>
    /// Глобальный счётчик краски (GDD §3.6). Заливка списывает 1 ед./клетку через
    /// <see cref="TryConsume"/>; источники (база, шахты, бутыльки) докидывают через
    /// <see cref="Add"/>. Дисплей в MVP — Debug.Log на каждом изменении.
    /// Конкретный банк под <see cref="ResourceId.Paint"/>; абстракция и работа шахт через
    /// <see cref="ResourceBankBase"/>.
    /// </summary>
    public sealed class PaintBank : ResourceBankBase
    {
        [SerializeField] private int _initialPaint = 20;
        [SerializeField] private int _maxPaint = 100;

        private int _current;

        public override ResourceId Resource => ResourceId.Paint;
        public override int Current => _current;
        public override int Max => _maxPaint;

        private void Awake()
        {
            _current = Mathf.Clamp(_initialPaint, 0, _maxPaint);
        }

        /// <summary>
        /// Атомарно списывает <paramref name="amount"/> ед. Возвращает false, если краски не хватает —
        /// баланс при этом не меняется. Вызывающий обязан корректно обработать отказ
        /// (например, <see cref="FloodFillAnimator"/> прерывает заливку).
        /// </summary>
        public override bool TryConsume(int amount)
        {
            if (amount <= 0) return true;
            if (_current < amount) return false;
            _current -= amount;
            return true;
        }

        public override void Add(int amount)
        {
            if (amount <= 0) return;
            var prev = _current;
            _current = Mathf.Min(_current + amount, _maxPaint);
            if (_current == prev) return;
        }
    }
}

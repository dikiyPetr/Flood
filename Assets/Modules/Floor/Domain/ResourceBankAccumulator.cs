using System.Collections.Generic;
using UnityEngine;

namespace Floor
{
    /// <summary>
    /// Аккумулятор поступлений в банк ресурса. Источники с непрерывной выдачей (шахты в зоне,
    /// база) регистрируют свой rate (ед/сек) через <see cref="SetRate"/>. Аккумулятор интегрирует
    /// `rate × deltaTime` во внутренний float-сток и раз в
    /// <see cref="ResourceBankAccumulatorConfig.TickInterval"/> сек докидывает в банк целую часть.
    /// Дробная часть остаётся внутри и переносится в следующий тик — за период длиной T выдача
    /// в банк суммарно совпадает с `rate × T` с точностью до 1 единицы.
    /// Геттер <see cref="CurrentRatePerSecond"/> отдаёт сумму всех зарегистрированных rate'ов
    /// для UI. Для one-shot вкладов (флаш void-стока шахты, future-фичи) — <see cref="AddBurst"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ResourceBankAccumulator : MonoBehaviour
    {
        [SerializeField] private ResourceBankAccumulatorConfig _config;
        [SerializeField] private ResourceBankBase _bank;

        private readonly Dictionary<MonoBehaviour, float> _sources = new Dictionary<MonoBehaviour, float>();
        private readonly List<MonoBehaviour> _pruneBuffer = new List<MonoBehaviour>();
        private float _stock;
        private float _flushTimer;

        public ResourceBankBase Bank => _bank;
        public float CurrentRatePerSecond { get; private set; }

        /// <summary>
        /// Установить rate (ед/сек) от источника. <paramref name="rate"/> ≤ 0 — снять регистрацию.
        /// Идемпотентно: повторный вызов перезаписывает прежнее значение.
        /// </summary>
        public void SetRate(MonoBehaviour source, float rate)
        {
            if (source == null) return;
            if (rate <= 0f)
            {
                _sources.Remove(source);
                return;
            }
            _sources[source] = rate;
        }

        /// <summary>
        /// Мгновенный one-shot вклад в банк. Минует rate-учёт — не отражается в
        /// <see cref="CurrentRatePerSecond"/>. Для флаша void-стока шахты и подобных скачкообразных
        /// поступлений.
        /// </summary>
        public void AddBurst(int amount)
        {
            if (amount <= 0) return;
            _bank.Add(amount);
        }

        private void Update()
        {

            // Прочистка ссылок на разрушенные MB на случай, если кто-то не сделал
            // SetRate(this, 0) в OnDisable. Унаследованный Unity == даёт fake-null проверку.
            _pruneBuffer.Clear();
            foreach (var kv in _sources)
            {
                if (kv.Key == null) _pruneBuffer.Add(kv.Key);
            }
            for (var i = 0; i < _pruneBuffer.Count; i++) _sources.Remove(_pruneBuffer[i]);

            var total = 0f;
            foreach (var kv in _sources) total += kv.Value;
            CurrentRatePerSecond = total;

            _stock += total * Time.deltaTime;

            _flushTimer += Time.deltaTime;
            if (_flushTimer < _config.TickInterval) return;
            _flushTimer -= _config.TickInterval;

            if (_stock < 1f) return;
            var whole = Mathf.FloorToInt(_stock);
            _stock -= whole;
            _bank.Add(whole);
        }
    }
}

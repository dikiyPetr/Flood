using Floor;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// View-компонент для <see cref="ResourceBankBase"/>: раз в кадр пишет current/max в
    /// <see cref="TMP_Text"/> и/или заполняет <see cref="Image.fillAmount"/>. Скорость берётся
    /// напрямую из <see cref="ResourceBankAccumulator.CurrentRatePerSecond"/> — это автoritetный
    /// rate, не оценка по дельте банка. Если аккумулятор не задан — скорость 0.
    /// Тип ресурса не зашит: работает поверх любого потомка <see cref="ResourceBankBase"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ResourceBankView : MonoBehaviour
    {
        [SerializeField] private ResourceBankBase _bank;
        [SerializeField] private ResourceBankAccumulator _accumulator;
        [SerializeField] private ResourceBankViewConfig _config;
        [SerializeField] private TMP_Text _label;
        [SerializeField] private Image _fillBar;

        private bool _ready;

        private void Start()
        {
            if (_config == null) { Debug.LogError($"[ResourceBankView] {name}: config not set"); return; }
            if (_bank == null) { Debug.LogError($"[ResourceBankView] {name}: bank not set"); return; }
            if (_label == null && _fillBar == null)
            {
                Debug.LogError($"[ResourceBankView] {name}: no label and no fill bar — view has nothing to render");
                return;
            }
            _ready = true;
        }

        private void Update()
        {
            if (!_ready) return;

            var current = _bank.Current;
            var max = _bank.Max;
            var rate = _accumulator != null ? _accumulator.CurrentRatePerSecond : 0f;

            if (_label != null)
            {
                _label.text = string.Format(_config.Format, _bank.Resource, current, max, rate);
            }
            if (_fillBar != null && max > 0)
            {
                _fillBar.fillAmount = (float)current / max;
            }
        }
    }
}

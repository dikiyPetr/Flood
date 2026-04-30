using UnityEngine;

namespace Floor
{
    /// <summary>
    /// Источник пассивной регенерации краски: регистрирует постоянный rate в
    /// <see cref="ResourceBankAccumulator"/>. Соответствует роли «база» из GDD §3.6 (1 ед./2 сек).
    /// На полный геймплейный лоп будет заменён комбинацией базы + шахт.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PaintRegenerator : MonoBehaviour
    {
        [SerializeField] private ResourceBankAccumulator _accumulator;
        [SerializeField, Min(0)] private int _amountPerTick = 1;
        [SerializeField, Min(0.01f)] private float _intervalSeconds = 2f;

        private void OnEnable()
        {
            if (_accumulator == null) return;
            _accumulator.SetRate(this, _amountPerTick / _intervalSeconds);
        }

        private void OnDisable()
        {
            if (_accumulator != null) _accumulator.SetRate(this, 0f);
        }
    }
}

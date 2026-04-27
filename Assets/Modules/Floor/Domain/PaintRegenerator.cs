using UnityEngine;

namespace Floor
{
    /// <summary>
    /// Дебаг-источник пассивной регенерации краски: тикает в <see cref="PaintBank.Add"/> с
    /// фиксированным интервалом. Соответствует роли «база» из GDD §3.6 (1 ед./2 сек).
    /// На полный геймплейный лоп будет заменён комбинацией базы + шахт.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PaintRegenerator : MonoBehaviour
    {
        [SerializeField] private PaintBank _bank;
        [SerializeField] private int _amountPerTick = 1;
        [SerializeField] private float _intervalSeconds = 2f;

        private float _accumulator;

        private void Update()
        {
            if (_bank == null || _intervalSeconds <= 0f) return;
            _accumulator += Time.deltaTime;
            if (_accumulator < _intervalSeconds) return;
            _accumulator -= _intervalSeconds;
            _bank.Add(_amountPerTick);
        }
    }
}

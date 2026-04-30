using UnityEngine;

namespace Floor
{
    /// <summary>
    /// Параметры <see cref="ResourceBankAccumulator"/>: интервал между выдачами целой части
    /// накопленного во внутреннем float-стоке. Меньше — плавнее притекает в банк (и UI), но
    /// чаще дёргается <c>Bank.Add</c>. Дефолт 0.1 сек = 10 раз в секунду.
    /// </summary>
    [CreateAssetMenu(menuName = "Flood/Floor/Resource Bank Accumulator Config")]
    public sealed class ResourceBankAccumulatorConfig : ScriptableObject
    {
        [SerializeField, Min(0.01f)]
        private float _tickInterval = 0.1f;

        public float TickInterval => _tickInterval;
    }
}

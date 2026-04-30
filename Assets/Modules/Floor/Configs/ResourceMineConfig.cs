using UnityEngine;

namespace Floor
{
    /// <summary>
    /// Параметры шахты ресурса (GDD §3.6). Для краски: 3 ед. / 10 сек.
    /// Для масла: 1 ед. / 30 сек. <see cref="VoidStockCap"/> — лимит накопления у
    /// шахты-в-пустоте, чтобы заброшенные шахты не превращались в бесконечный буфер.
    /// </summary>
    [CreateAssetMenu(menuName = "Flood/Floor/Resource Mine Config")]
    public sealed class ResourceMineConfig : ScriptableObject
    {
        [SerializeField] private ResourceId _resource = ResourceId.Paint;
        [SerializeField, Min(1)] private int _amountPerTick = 3;
        [SerializeField, Min(0.01f)] private float _intervalSeconds = 10f;
        [SerializeField, Min(0)] private int _voidStockCap = 15;

        public ResourceId Resource => _resource;
        public int AmountPerTick => _amountPerTick;
        public float IntervalSeconds => _intervalSeconds;
        public int VoidStockCap => _voidStockCap;
    }
}

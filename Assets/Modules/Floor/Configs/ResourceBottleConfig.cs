using UnityEngine;

namespace Floor
{
    /// <summary>
    /// Параметры одноразового бутылька ресурса (GDD §3.6). Краска: 10 ед. за бутылёк.
    /// Масло: 2 ед. за бутылёк. Бутылёк выдаёт <see cref="Amount"/> единиц при включении в петлю
    /// (клетка бутылька переходит в <see cref="CellState.Territory"/>) и саморазрушается.
    /// </summary>
    [CreateAssetMenu(menuName = "Flood/Floor/Resource Bottle Config")]
    public sealed class ResourceBottleConfig : ScriptableObject
    {
        [SerializeField] private ResourceId _resource = ResourceId.Paint;
        [SerializeField, Min(1)] private int _amount = 10;

        public ResourceId Resource => _resource;
        public int Amount => _amount;
    }
}

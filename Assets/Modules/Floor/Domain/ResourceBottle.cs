using UnityEngine;

namespace Floor
{
    /// <summary>
    /// Одноразовый бутылёк ресурса (GDD §3.6). Сам не детектит коллизии — поручает это
    /// <see cref="Pickable"/> на том же GameObject (через <see cref="RequireComponentAttribute"/>).
    /// На событие <see cref="Pickable.Picked"/> выдаёт <see cref="ResourceBottleConfig.Amount"/>
    /// в банк и вызывает <see cref="Object.Destroy(Object)"/> на gameObject.
    /// Pickable сам гарантирует одноразовость, поэтому собственного `_claimed` тут не нужно.
    /// </summary>
    [RequireComponent(typeof(Pickable))]
    [DisallowMultipleComponent]
    public sealed class ResourceBottle : MonoBehaviour
    {
        [SerializeField] private ResourceBottleConfig _config;
        [SerializeField] private ResourceBankBase _bank;

        private Pickable _pickable;

        private void Awake()
        {
            _pickable = GetComponent<Pickable>();
        }

        private void OnEnable()
        {
            _pickable.Picked += OnPicked;
        }

        private void OnDisable()
        {
            _pickable.Picked -= OnPicked;
        }

        private void Start()
        {
            if (_config.Resource != _bank.Resource)
            {
                Debug.LogError($"[Bottle] {name}: resource mismatch — config={_config.Resource}, bank={_bank.Resource}");
            }
        }

        private void OnPicked(GameObject picker)
        {
            if (_config.Resource != _bank.Resource) return;
            _bank.Add(_config.Amount);
            Destroy(gameObject);
        }
    }
}

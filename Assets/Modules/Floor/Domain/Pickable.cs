using System;
using Core;
using UnityEngine;

namespace Floor
{
    /// <summary>
    /// Универсальный триггер подбора. Стреляет <see cref="Picked"/> один раз при первом
    /// касании коллайдером с подходящего слоя (берётся из <see cref="GameLayersConfig.PickerLayers"/>).
    /// Поведение после подбора (выдача ресурса, спавн эффекта, Destroy и т.д.) — на стороне
    /// подписчика; сам Pickable знает только «коснулись и фильтр сошёлся».
    /// Поддерживает оба варианта коллайдеров: trigger (через <c>OnTriggerEnter</c>) и solid
    /// (через <c>OnCollisionEnter</c>). _consumed страхует от повторного вызова в одном кадре.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    [DisallowMultipleComponent]
    public sealed class Pickable : MonoBehaviour
    {
        [SerializeField] private GameLayersConfig _layers;

        private bool _consumed;

        /// <summary>
        /// Событие подбора. Аргумент — gameObject, который коснулся (может пригодиться подписчику
        /// для эффектов, привязанных к подобравшему). Срабатывает максимум один раз на жизнь
        /// компонента, до повторного <c>OnEnable</c>.
        /// </summary>
        public event Action<GameObject> Picked;

        private void OnEnable()
        {
            _consumed = false;
        }

        private void OnTriggerEnter(Collider other)
        {
            TryPick(other.gameObject);
        }

        private void OnCollisionEnter(Collision collision)
        {
            TryPick(collision.gameObject);
        }

        private void TryPick(GameObject picker)
        {
            if (_consumed) return;
            if (_layers == null)
            {
                Debug.LogError($"[Pickable] {name}: GameLayersConfig not set");
                _consumed = true;
                return;
            }
            if ((_layers.PickerLayers.value & (1 << picker.layer)) == 0) return;
            _consumed = true;
            Picked?.Invoke(picker);
        }
    }
}

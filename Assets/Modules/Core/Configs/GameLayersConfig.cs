using UnityEngine;

namespace Core
{
    /// <summary>
    /// Централизованный реестр Unity-слоёв проекта. Один SO-ассет на игру; на него ссылаются
    /// все потребители, которым нужно «фильтровать по типу сущности» (триггеры подбора, зоны
    /// урона, AI-сенсоры и т.п.). Логика фильтра остаётся у потребителя — конфиг лишь декларирует
    /// «вот эти слои = подбиратели», «вот эти слои = враги».
    /// Когда добавляется новая роль (бутылёк, ловушка, снаряд) — новое поле в этом конфиге,
    /// без правок Tag/Layer-констант по проекту.
    /// </summary>
    [CreateAssetMenu(menuName = "Flood/Core/Game Layers Config")]
    public sealed class GameLayersConfig : ScriptableObject
    {
        [SerializeField, Tooltip("Слои объектов, которые могут подобрать пикапы (игрок, в будущем — союзники).")]
        private LayerMask _pickerLayers;

        [SerializeField, Tooltip("Слои непроходимых препятствий — Floor.Obstacle регистрирует клетки только если объект на одном из этих слоёв.")]
        private LayerMask _obstacleLayers;

        public LayerMask PickerLayers => _pickerLayers;
        public LayerMask ObstacleLayers => _obstacleLayers;
    }
}

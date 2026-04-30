using UnityEngine;

namespace Floor
{
    /// <summary>
    /// Абстрактный родитель банков ресурсов. Конкретный потомок (<see cref="PaintBank"/>,
    /// будущий OilBank) фиксирует <see cref="Resource"/> и хранит счётчик. Шахты/бутыльки
    /// (<see cref="ResourceMine"/>, <see cref="ResourceBottle"/>) ссылаются на банк через
    /// этот тип, чтобы один класс источника работал на любой ресурс.
    /// </summary>
    [DisallowMultipleComponent]
    public abstract class ResourceBankBase : MonoBehaviour
    {
        public abstract ResourceId Resource { get; }
        public abstract int Current { get; }
        public abstract int Max { get; }

        /// <summary>
        /// Атомарно списывает <paramref name="amount"/>. Возвращает false если ресурса не хватает —
        /// баланс при этом не меняется. Вызывающий обязан корректно обработать отказ.
        /// </summary>
        public abstract bool TryConsume(int amount);

        /// <summary>
        /// Докидывает <paramref name="amount"/>, клампится по <see cref="Max"/>. Источники (база,
        /// шахты, бутыльки) дёргают этот метод.
        /// </summary>
        public abstract void Add(int amount);
    }
}

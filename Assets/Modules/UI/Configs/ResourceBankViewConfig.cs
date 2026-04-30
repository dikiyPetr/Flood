using UnityEngine;

namespace UI
{
    /// <summary>
    /// Параметры одного <c>ResourceBankView</c>: формат строки.
    /// Шаблон принимает четыре аргумента: {0}=Resource (enum), {1}=current, {2}=max,
    /// {3}=rate/sec (float). Дефолт даёт строку вида "Paint 47/100 (+3.0/s)".
    /// </summary>
    [CreateAssetMenu(menuName = "Flood/UI/Resource Bank View Config")]
    public sealed class ResourceBankViewConfig : ScriptableObject
    {
        [SerializeField, Tooltip("string.Format с аргументами: {0}=Resource, {1}=current, {2}=max, {3}=rate/sec.")]
        private string _format = "{0} {1}/{2} ({3:+0.0;-0.0;0.0}/s)";

        public string Format => _format;
    }
}

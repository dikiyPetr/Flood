namespace Floor
{
    /// <summary>
    /// Тип игрового ресурса (GDD §3.6). Шахты/бутыльки не знают про конкретный банк —
    /// сравнивают <see cref="ResourceId"/> своего конфига с <see cref="ResourceBankBase.Resource"/>
    /// подключённого банка. <see cref="Oil"/> зарезервирован под Day 2 GDD §3.3, банк/потребление
    /// будут добавлены позже.
    /// </summary>
    public enum ResourceId
    {
        Paint = 0,
        Oil = 1,
    }
}

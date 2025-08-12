namespace DaJet.Metadata.Model
{
    public enum PropertyPurpose
    {
        /// <summary>
        /// <b>Стандартный реквизит</b>
        /// <br>Состав стандартных реквизитов зависит от вида объекта метаданных.</br>
        /// </summary>
        System,
        /// <summary>
        /// <b>Реквизит</b>
        /// <br>Определяемый пользователем реквизит объекта метаданных.</br>
        /// </summary>
        Property,
        /// <summary>
        /// <b>Измерение</b>
        /// <br>Определяемое пользователем для регистра измерение.</br>
        /// </summary>
        Dimension,
        /// <summary>
        /// <b>Ресурс</b>
        /// <br>Определяемый пользователем для регистра ресурс.</br>
        /// </summary>
        Measure,
        /// <summary>
        /// <b>Признак учёта плана счетов</b>
        /// </summary>
        AccountingFlag,
        /// <summary>
        /// <b>Признак учёта субконто плана счетов</b>
        /// </summary>
        AccountingDimensionFlag,
        /// <summary>
        /// <b>Реквизит адресации задачи</b>
        /// </summary>
        RoutingProperty
    }
    public static class PropertyPurposeExtensions
    {
        public static string GetNameRu(this PropertyPurpose purpose)
        {
            if (purpose == PropertyPurpose.System) { return "СтандартныйРеквизит"; }
            else if (purpose == PropertyPurpose.Measure) { return "Ресурс"; }
            else if (purpose == PropertyPurpose.Property) { return "Реквизит"; }
            else if (purpose == PropertyPurpose.Dimension) { return "Измерение"; }
            else if (purpose == PropertyPurpose.RoutingProperty) { return "РеквизитАдресации"; }
            else if (purpose == PropertyPurpose.AccountingFlag) { return "ПризнакУчёта"; }
            else if (purpose == PropertyPurpose.AccountingDimensionFlag) { return "ПризнакУчётаСубконто"; }
            else
            {
                return "Свойство";
            }
        }
    }
}
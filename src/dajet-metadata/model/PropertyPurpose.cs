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
        Measure
    }
}
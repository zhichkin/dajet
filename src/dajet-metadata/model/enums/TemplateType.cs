namespace DaJet.Metadata.Model
{
    ///<summary>Типы макетов прикладных объектов метаданных</summary>
    public enum TemplateType
    {
        ///<summary>Табличный документ</summary>
        SpreadsheetDocument = 0,
        ///<summary>Двоичные данные</summary>
        BinaryData = 1,
        ///<summary>Active document</summary>
        ActiveDocument = 2,
        ///<summary>Документ HTML</summary>
        HtmlDocument = 3,
        ///<summary>Текстовый документ</summary>
        TextDocument = 4,
        ///<summary>Географическая схема</summary>
        GeographicalSchema = 5,
        ///<summary>Схема компоновки данных</summary>
        DataCompositionSchema = 6,
        ///<summary>Макет оформления компоновки данных</summary>
        DataCompositionAppearanceTemplate = 7,
        ///<summary>Графическая схема</summary>
        GraphicalSchema = 8,
        ///<summary>Внешняя компонента</summary>
        AddIn = 9
    }
}
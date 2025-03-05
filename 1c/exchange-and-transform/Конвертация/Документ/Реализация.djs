DECLARE @Метаданные object = SELECT Тип = '', Имя = '', ПолноеИмя = ''
DECLARE @КлючОбъекта entity
DECLARE @ОбъектДанных object

SELECT Ссылка, Дата, Номер, Покупатель
  INTO @ОбъектДанных
  FROM Документ.Реализация AS Шапка

APPEND (SELECT НомерСтроки, Номенклатура, Количество
          FROM Документ.Реализация.Товары
         WHERE Ссылка = @КлючОбъекта
         ORDER BY Ссылка ASC, KeyField ASC) AS Товары

WHERE Шапка.Ссылка = @КлючОбъекта

RETURN @ОбъектДанных
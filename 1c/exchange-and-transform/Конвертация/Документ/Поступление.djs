DECLARE @Метаданные object = SELECT Тип = '', Имя = '', ПолноеИмя = ''
DECLARE @КлючОбъекта entity
DECLARE @ОбъектДанных object

SELECT Ссылка, Дата, Номер, Поставщик
  INTO @ОбъектДанных
  FROM Документ.Поступление AS Шапка

APPEND (SELECT НомерСтроки, Номенклатура, Количество
          FROM Документ.Поступление.Товары
         WHERE Ссылка = @КлючОбъекта
         ORDER BY Ссылка ASC, KeyField ASC) AS Товары

WHERE Шапка.Ссылка = @КлючОбъекта

RETURN @ОбъектДанных
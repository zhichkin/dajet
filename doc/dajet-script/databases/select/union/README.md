## DaJet Script

[SELECT](https://github.com/zhichkin/dajet/tree/main/doc/dajet-script/databases/select/README.md)

### Оператор UNION

Табличный оператор **UNION** реализован DaJet Script аналогично стандарту ```SQL```. В ниже следующем примере использованы все доступные опции оператора. Опция **ALL** - необязательна, её назначение и функциональность ровно такие же, как в стандарте ```SQL```. Тоже самое можно сказать про использование в запросе опционального предложения **ORDER BY**.

```SQL
DECLARE @table array

USE 'mssql://zhichkin/ut-demo'

   SELECT 'НДС 10%' AS Name, Перечисление.СтавкиНДС.НДС10 AS Value
     INTO @table
   UNION ALL SELECT 'НДС 18%', Перечисление.СтавкиНДС.НДС18
   UNION ALL SELECT 'Без НДС', Перечисление.СтавкиНДС.БезНДС
   ORDER BY Name DESC

END

IF @table = NULL
THEN RETURN 'Нет данных'
ELSE RETURN @table
END
```

**Результат выполнения скрипта в DaJet Studio**
|**Name**|**Value**|
|--------|---------|
|НДС 18%|93e8f7aa-e0ce-1fcd-48a8-76b826b5ef6b|
|НДС 10%|d0d192a2-e8ae-72f0-45d0-62c1b99522a7|
|Без НДС|5dc678af-171c-41ad-4e88-46212489abf1|

[Наверх](#оператор-union)

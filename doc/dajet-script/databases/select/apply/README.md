## DaJet Script

[SELECT](https://github.com/zhichkin/dajet/tree/main/doc/dajet-script/databases/select/README.md)

### Оператор APPLY

Оператор **APPLY** реализован DaJet Script согласно стандартному ```SQL```. Этот оператор выполняет соединение уже полученного табличного результата родительского запроса (левый операнд) и коррелированного подзапроса (правый операнд). По сути своей это соединение каждой строки родительского запроса со встроенной табличной функцией (inline table function). При этом такой подзапрос использует замыкание (closure) на родительскую область видимости данных.

Оператор **APPLY** реализует два вида соединений: **CROSS** и **OUTER**. По функциональности они аналогичны соединениям вида **INNER** и **LEFT** табличного оператора **JOIN**. Соответственно: **CROSS** аналогичен **INNER**, а **OUTER** - **LEFT**.

> **На заметку:** в PostgreSQL оператор **APPLY** называется **JOIN LATERAL**. DaJet Script заимствовал название и синтаксис оператора **APPLY** от Microsoft SQL Server.

В ниже следующих примерах подзапрос оператора **APPLY** ссылается на родительские данные в своём предложении **WHERE**:
```SQL
WHERE Номенклатура = Товары.Ссылка -- Ссылка на результат родительского запроса
```

Кроме этого важно отметить, что в свою очередь родительский запрос обращается к двум полям ```Период``` и ```Цена``` подзапроса так, как если бы это были записи выборки данных. Это принципиальное отличие боковых соединений от обычных коррелированных подзапросов. Последние могут возвращать только одно значение или массив одиночных значений. Боковое соединение оперирует всей таблицей, получаемой в результате выполнения подзапроса.

```SQL
SELECT Наименование = Товары.Наименование
     , Период       = ПрайсЛист.Период -- Обращение к результату
     , Цена         = ПрайсЛист.Цена   -- выполнения подзапроса
```

Во втором примере использования **OUTER APPLY** обратите внимание на то, что используется функция **ISNULL**, так как в результате выполнения соединения возможно получение значений ```NULL``` аналогично семантике соединений **LEFT JOIN**.

**Пример CROSS APPLY**
```SQL
DECLARE @table array

USE 'mssql://server/database'

   SELECT Наименование = Товары.Наименование
        , Период       = ПрайсЛист.Период
        , Цена         = ПрайсЛист.Цена
     INTO @table
     FROM (SELECT TOP 5 Ссылка, Наименование FROM Справочник.Номенклатура) AS Товары
    CROSS APPLY (SELECT Период, Цена
                   FROM РегистрСведений.ЦеныНоменклатуры
                  WHERE Номенклатура = Товары.Ссылка) AS ПрайсЛист
    ORDER BY
          Товары.Наименование DESC, ПрайсЛист.Период ASC

END

IF @table = NULL
THEN RETURN 'Нет данных'
ELSE RETURN @table
END
```

**Результат выполнение запроса**
|**Наименование**|**Период**|**Цена**|
|----------------|----------|--------|
|Товар 10|01/03/2024 00:00:00|123.00|
|Товар 1|01/01/2024 00:00:00|1.00|
|Товар 1|01/02/2024 00:00:00|1.23|
|Товар 1|01/03/2024 00:00:00|3.21|

[Наверх](#оператор-apply)

**Пример OUTER APPLY**
```SQL
DECLARE @table array

USE 'mssql://server/database'

   SELECT Наименование = Товары.Наименование
        , Период       = ISNULL(ПрайсЛист.Период, '2024-01-01T00:00:00')
        , Цена         = CASE WHEN NOT ПрайсЛист.Цена IS NULL
                              THEN ПрайсЛист.Цена ELSE 0.00 END
     INTO @table
     FROM (SELECT TOP 5 Ссылка, Наименование FROM Справочник.Номенклатура) AS Товары
    OUTER APPLY (SELECT Период, Цена
                   FROM РегистрСведений.ЦеныНоменклатуры
                  WHERE Номенклатура = Товары.Ссылка) AS ПрайсЛист
    ORDER BY
          Товары.Наименование DESC, ПрайсЛист.Период ASC

END

IF @table = NULL
THEN RETURN 'Нет данных'
ELSE RETURN @table
END
```

**Результат выполнение запроса**
|**Наименование**|**Период**|**Цена**|
|----------------|----------|--------|
|Товар 10000|01/01/2024 00:00:00|0.00|
|Товар 1000|01/01/2024 00:00:00|0.00|
|Товар 100|01/01/2024 00:00:00|0.00|
|Товар 10|01/03/2024 00:00:00|123.00|
|Товар 1|01/01/2024 00:00:00|1.00|
|Товар 1|01/02/2024 00:00:00|1.23|
|Товар 1|01/03/2024 00:00:00|3.21|

[Наверх](#оператор-apply)

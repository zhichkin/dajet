## Описание типов реквизита объекта метаданных

Объект описания типов может выглядеть следующим образом:
```txt
{"Pattern",
{"S",10,1}
}
```
Для составного типа данных, например, таким образом:
```txt
{"Pattern",
{"S",10,1},
{"N",10,2,0}
}
```
При преобразовании формата 1С в формат парсера DaJet это будет выглядеть, например, так:
```txt
---[1] (1.4) DaJet.Metadata.Model.ConfigObject
-------[2] (1.4.0) "Pattern"
-------[2] (1.4.1) DaJet.Metadata.Model.ConfigObject
-----------[3] (1.4.1.0) "S"
-----------[3] (1.4.1.1) "10"
-----------[3] (1.4.1.2) "1"
-------[2] (1.4.2) DaJet.Metadata.Model.ConfigObject
-----------[3] (1.4.2.0) "N"
-----------[3] (1.4.2.1) "10"
-----------[3] (1.4.2.2) "2"
-----------[3] (1.4.2.3) "0"
```
Таким образом схема описания типов выглядит следующим образом:
- **(ROOT)** - начало объекта описания типов данных, например, реквизита.
- **(ROOT.0)** - значение "Pattern" обозначет тип данных 1С **ОписаниеТипов**.
- **(ROOT.N)** - начало объекта описания одного типа данных (может повторяться для составных типов данных).
- **(ROOT.N.0)** - тип значения.
- **(ROOT.N.1)** - квалификатор 1 типа значения.
- **(ROOT.N.2)** - квалификатор 2 типа значения.
- **(ROOT.N.3)** - квалификатор 3 типа значения.

Для составных типов данных узлы или объекты **(ROOT.N)** могут повторяться.

Возможные значения описания одного типа данных представлены в следующей таблице:

| **Тип данных**                            | **Пример формата**                         |
|-------------------------------------------|--------------------------------------------|
| Булево                                    | {"B"}                                      |
| Дата (дата и время)                       | {"D"}                                      |
| Дата (дата)                               | {"D","D"}                                  |
| Дата (время)                              | {"D","T"}                                  |
| Строка (неограниченная) Переменная длина  | {"S"}                                      |
| Строка(10) Фиксированная длина            | {"S",10,0}                                 |
| Строка(10) Переменная длина               | {"S",10,1}                                 |
| Число(10,2) Может быть отрицательным      | {"N",10,2,0}                               |
| Число(10,2) Неотрицательное число         | {"N",10,2,1}                               |
| ХранилищеЗначения                         | {"#",e199ca70-93cf-46ce-a54b-6edc88c3a296} |
| УникальныйИдентификатор                   | {"#",fc01b5df-97fe-449b-83d4-218a090e681e} |
| Ссылка (идентификатор объекта метаданных) | {"#",70497451-981e-43b8-af46-fae8d65d16f2} |

Возможные предопределённые значения для ссылок определены в классе [MetadataRegistry](https://github.com/zhichkin/dajet-metadata-core/blob/main/src/dajet-metadata-core/MetadataRegistry.cs).

Чтение объекта **ОписаниеТипов** реализуется классом [DataTypeSetParser](https://github.com/zhichkin/dajet-metadata-core/blob/main/src/dajet-metadata-core/parsers/DataTypeSetParser.cs).

Основной метод чтения этого объекта на C# выглядит так:

```C#
public void Parse(in ConfigFileReader source, out DataTypeSet target)
{
    // Параметр source должен быть позиционирован в данный момент на корневом узле
    // объекта описания типов данных реквизита объекта метаданных:
    // source.Char == '{' && source.Token == TokenType.StartObject

    target = new DataTypeSet(); // Объект "ОписаниеТипов"
    List<Guid> references = new List<Guid>(); // Список ссылок на объекты метаданных

    _ = source.Read(); // Читаем первое значение объекта "ОписаниеТипов"

    if (source.Value != "Pattern")
    {
        return; // это не объект описания типов
    }
            
    while (source.Read())
    {
        if (source.Token == TokenType.EndObject)
        {
            break; // Завершаем чтение объекта "ОписаниеТипов"
        }
        else if (source.Token == TokenType.StartObject)
        {
            // Начинаем чтение нового описания одного типа данных:
            // обнуляем текущие значения квалификаторов
            _pointer = -1;
            _qualifiers[0] = null;
            _qualifiers[1] = null;
            _qualifiers[2] = null;
        }
        else if (source.Token == TokenType.Value || source.Token == TokenType.String)
        {
            if (source.Path[source.Level] == 0) // значение дискриминатора типа данных
            {
                if (source.Value == MetadataTokens.B) // {"B"}
                {
                    ReadBoolean(in target); // Читаем описание типа "Булево"
                }
                else if (source.Value == MetadataTokens.D) // {"D"} | {"D","D"} | {"D","T"}
                {
                    ReadDateTime(in source, in target); // Читаем описание типа "Дата"
                }
                else if (source.Value == MetadataTokens.S) // {"S"} | {"S",10,0} | {"S",10,1}
                {
                    ReadString(in source, in target); // Читаем описание типа "Строка"
                }
                else if (source.Value == MetadataTokens.N) // {"N",10,2,0} | {"N",10,2,1}
                {
                    ReadNumeric(in source, in target); // Читаем описание типа "Число"
                }
                else if (source.Value == MetadataTokens.R) // {"#",70497451-981e-43b8-af46-fae8d65d16f2}
                {
                    ReadReference(in source, in target, in references); // Читаем описание типа "Ссылка"
                }
            }
        }
    }

    target.References = references; // Сохраняем прочитанные ссылки в объекте "ОписаниеТипов"
}
```

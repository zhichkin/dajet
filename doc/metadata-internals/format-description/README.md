## Формат файлов описания метаданных 1С:Предприятие 8

Если прочитать любой файл описания метаданных, например, из таблицы **Config**,
предварительно распаковав его при необходимости, то можно увидеть нечто подобное:

```txt
{1,
{56,4738c44e-f1e2-48ee-9f18-ca100a49d1fa,2110b310-b714-4729-a843-11e0587971de,
{0,
{3,
{1,0,d09bb39d-27be-4aba-a843-5fe1dc57bb4f},"СправочникИерархическийГруппы",
{1,"ru","Справочник (иерархический, группы)"},"",0,0,00000000-0000-0000-0000-000000000000,0}
},3,1,
{0,0},1,1,1,0,8,1,32,1,00000000-0000-0000-0000-000000000000,
{0,0},0,79ffbbb3-c835-4690-b74b-335aefd395d9,1a9b8906-d9f0-4c99-95be-2a9948d895c1,0,1,0,0,2,0,
```

Внимательное изучение этого формата представления данных может привести к следующим выводам:
- данные содержат объекты или структуры выделенные при помощи фигурных скобок **{** *объект* **}**;
- объекты могут быть вложены друг в друга;
- объекты содержат свойства или поля, отделённые друг от друга при помощи запятых;
- значениями свойств объектов могут быть числа, уникальные идентификаторы и строки;
- строки выделяются при помощи кавычек **"** *строка* **"**;
- кавычки внутри строк дублируются, например, вот так: **"** Космодром ""Восточный"", Амурская область **"**;
- значение может быть NULL, например, это может выглядеть так:
  - **{}** - объект с единственным значением равным NULL;
  - **{,1}** - объект, первое значение которого равно NULL;
  - **{2,}** - объект, второе значение которого равно NULL;
  - **{1,,2}** - объект, второе значение которого (между двумя запятыми) равно NULL.

Например, корневой файл конфигурации 1С, который содержит идентификатор конфигурации,
используемый для получения описания всех объектов метаданных, входящих в её состав,
может выглядеть следующим образом:

```txt
{2,684c8f2b-d93f-49cc-b766-b3cc3896b047,}
```

Этот файл можно прочитать из таблицы **Config** следующим SQL скриптом:

```SQL
SELECT BinaryData FROM Config WHERE FileName = 'root';
```

Файл **root** состоит всего лишь из одного объекта, имеющего 3 значения.
Второе значение - это идентификатор конфигурации 1С и указатель на главный файл.

Далее можно прочитать главный файл описания объектов метаданных:

```SQL
SELECT BinaryData FROM Config WHERE FileName = '684c8f2b-d93f-49cc-b766-b3cc3896b047';
```

**Абстрагируясь от того, что всё это значит, становится понятным,
что формат файла описания метаданных 1С представляет собой иерархическую
структуру вложенных друг в друга на произвольную глубину объектов,
имеющих такое же произвольное количество свойств.**

**Предположение:**
> Учитывая тот факт, что 1С написано на языке C++, структура этих данных определяется классами
> или структурами, определёнными средствами этого языка программирования. При этом используется
> метод сериализации/десериализации этих структур данных аналогичный Protocol Buffers (protobuf)
> или любому другому методу бинарной сериализации/десериализации, а именно строгое соблюдение
> последовательности следования свойств или полей объектов друг за другом.

Кроме этого, можно утверждать, что GUID'ы - это идентификаторы типов объектов.
Некоторые из таких GUID'ов можно найти в каталоге [**/dajet-metadata/core/constants**](https://github.com/zhichkin/dajet/blob/main/src/dajet-metadata/core/constants).

Таким образом, в целях создания парсера для данного формата данных, можно сформулировать
для него следующую грамматику, выраженную при помощи следующей псевдо BNF нотации:

```txt
<value>  = <integer> | <string> | <uuid> | <object> | <NULL>
<object> = "{" [<value> | "," <value>]? "}"
```

Библиотека **DaJet.Metadata** реализует такой парсер при помощи класса [**ConfigFileReader**](https://github.com/zhichkin/dajet/blob/main/src/dajet-metadata/core/ConfigFileReader.cs).
Данные файла, обработанные этим парсером, могут быть представлены в следующем более удобном виде:

```txt
[0] (0) "2"
[0] (1) DaJet.Metadata.Model.ConfigObject
----[1] (1.0) "684c8f2b-d93f-49cc-b766-b3cc3896b047"
[0] (2) "7"
[0] (3) DaJet.Metadata.Model.ConfigObject
----[1] (3.0) "9cd510cd-abfc-11d4-9434-004095e12fc7"
----[1] (3.1) DaJet.Metadata.Model.ConfigObject
--------[2] (3.1.0) "1"
--------[2] (3.1.1) DaJet.Metadata.Model.ConfigObject
------------[3] (3.1.1.0) "62"
------------[3] (3.1.1.1) DaJet.Metadata.Model.ConfigObject
----------------[4] (3.1.1.1.0) "0"
----------------[4] (3.1.1.1.1) DaJet.Metadata.Model.ConfigObject
--------------------[5] (3.1.1.1.1.0) "3"
--------------------[5] (3.1.1.1.1.1) DaJet.Metadata.Model.ConfigObject
------------------------[6] (3.1.1.1.1.1.0) "1"
------------------------[6] (3.1.1.1.1.1.1) "0"
------------------------[6] (3.1.1.1.1.1.2) "db425e45-1ec5-45ba-b5e9-e0a059301888"
--------------------[5] (3.1.1.1.1.2) "dajet_metadata"
--------------------[5] (3.1.1.1.1.3) DaJet.Metadata.Model.ConfigObject
------------------------[6] (3.1.1.1.1.3.0) "1"
------------------------[6] (3.1.1.1.1.3.1) "ru"
------------------------[6] (3.1.1.1.1.3.2) "DaJet Metadata"
--------------------[5] (3.1.1.1.1.4) "Тестовая ИБ для чтения метаданных 1С"
--------------------[5] (3.1.1.1.1.5) "0"
--------------------[5] (3.1.1.1.1.6) "0"
--------------------[5] (3.1.1.1.1.7) "00000000-0000-0000-0000-000000000000"
--------------------[5] (3.1.1.1.1.8) "0"
```

В данном случае в квадратных скобках слева указывается уровень вложенности объектов,
а в круглых скобках - путь к значению объекта от корневого объекта файла.

Например, путь **[5] (3.1.1.1.1.4)** указывает на комментарий к конфигурации 1С,
который находится на 5-ом уровне вложенности.
Данное кодирование путей к значениям файла описания метаданных 1С аналогично кодированию
путей к каталогам и файлам в файловой системе.

Такой вариант визуализации позволяет обнаруживать некоторые хорошо понятные объекты. Например,
можно обнаружить типизированные коллекции данных, которые на языке C# могли бы быть выражены как **List\<T\>**.

Следующий пример взят из корневого файла конфигурации, который описывает состав объектов метаданных.
В данном случае это список констант, определённых в конфигурации:

```txt
---[3] (4.1.1.3) DaJet.Metadata.Model.ConfigObject
-------[4] (4.1.1.3.0) "0195e80c-b157-11d4-9435-004095e12fc7"
-------[4] (4.1.1.3.1) "64"
-------[4] (4.1.1.3.2) "e4243239-7b78-4ae2-8b29-4dc9d57c9bf9"
-------[4] (4.1.1.3.3) "d90ce0a7-528b-4361-92c9-d517df40b1b9"
-------[4] (4.1.1.3.4) "9893e2d6-f3f8-4d73-bb06-19bf26d216ab"
-------[4] (4.1.1.3.5) "5e24b87e-d9d7-428c-aabc-a937ff0781f7"
-------[4] (4.1.1.3.6) "c9410635-c424-43aa-9178-62a212bd8348"
-------[4] (4.1.1.3.7) "16392cf6-dd83-4df0-a54b-6efc29778a89"
-------[4] (4.1.1.3.8) "e693cdd0-8ead-4ba5-adec-86bab74ee7cd"
-------[4] (4.1.1.3.9) "21ef1d5b-ef87-41f6-8e10-75c03464a135"
```

Немного расшифруем, что мы тут видим:
- **(4.1.1.3)** - начало строго типизированной коллекции идентификаторов.
- **(4.1.1.3.0)** - идентификатор типа данных в коллекции - константы.
- **(4.1.1.3.1)** - количество элементов коллекции.
- **(4.1.1.3.2)** - первый идентификатор объекта метаданных "Константа".
- **(4.1.1.3.N)** - все последующие элементы коллекции, где N - количество элементов.

Или, например, описание типа данных реквизита объекта может выглядеть так:

```txt
---[3] (1.1.1.2) DaJet.Metadata.Model.ConfigObject
-------[4] (1.1.1.2.0) "Pattern"
-------[4] (1.1.1.2.1) DaJet.Metadata.Model.ConfigObject
-----------[5] (1.1.1.2.1.0) "S"
-----------[5] (1.1.1.2.1.1) "9"
-----------[5] (1.1.1.2.1.2) "0"
```

- **(1.1.1.2)** - начало объекта описания типов данных, например, реквизита.
- **(1.1.1.2.0)** - значение "Pattern" обозначет тип данных 1С **ОписаниеТипов**.
- **(1.1.1.2.1)** - начало объекта описания одного типа данных (может повторяться для составных типов данных).
- **(1.1.1.2.1.0)** - тип значения, в данном случае S - это строка.
- **(1.1.1.2.1.1)** - квалификатор длины строки.
- **(1.1.1.2.1.2)** - квалификатор допустимой длины строки.

Таким образом, исследуя файлы описания объектов метаданных 1С, можно изучить
структуру хранения необходимых значений и использовать их для написания парсера.

В частности, библиотека **DaJet.Metadata** позволяет получить все необходимые значения основных объектов
конфигурации 1С достаточные для точной работы с таблицами СУБД этих прикладных объектов.

Получить вышеуказанное удобное представление файлов описания конфигурации 1С средствами библиотеки
**DaJet.Metadata** можно при помощи следующего кода на C#:

```C#
public void WriteRootConfigToFile()
{
    Guid root;
            
    using (ConfigFileReader reader = new ConfigFileReader(
        DatabaseProvider.SQLServer, MS_CONNECTION_STRING, ConfigTableNames.Config, "root"))
    {
        root = new RootFileParser().Parse(in reader);
    }

    ConfigObject config;

    using (ConfigFileReader reader = new ConfigFileReader(
        DatabaseProvider.SQLServer, MS_CONNECTION_STRING, ConfigTableNames.Config, root))
    {
        config = new ConfigFileParser().Parse(in reader);
    }

    new ConfigFileWriter().Write(config, "C:\\dump-1c\\config.txt");
}
```
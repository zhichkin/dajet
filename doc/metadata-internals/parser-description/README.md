## Принцип работы парсера файлов метаданных

Как уже было сказано [ранее](https://github.com/zhichkin/dajet/blob/main/doc/metadata-internals/format-description/README.md)
структура файла описания метаданных 1С:Предприятие 8 представляет из себя иерархическую структуру в виде дерева.

```txt
---[3] (1.1.1.2) DaJet.Metadata.Model.ConfigObject
-------[4] (1.1.1.2.0) "Pattern"
-------[4] (1.1.1.2.1) DaJet.Metadata.Model.ConfigObject
-----------[5] (1.1.1.2.1.0) "S"
-----------[5] (1.1.1.2.1.1) "9"
-----------[5] (1.1.1.2.1.2) "0"
```

Класс [**ConfigFileReader**](https://github.com/zhichkin/dajet/blob/main/src/dajet-metadata/core/ConfigFileReader.cs)
реализует последовательное чтение такого файла в виде отдельных токенов. По сути своей этот класс является
токенайзером или лексером, если угодно. Чтение файла выполняется однонаправленно (forward only).

Таблица реализованных [токенов](https://github.com/zhichkin/dajet/blob/main/src/dajet-metadata/core/TokenType.cs):

| **Токен**   | **Текущий символ** | **Значение**                       |
|-------------|--------------------|------------------------------------|
| StartFile   | '{'                | Самая первая фигурная скобка файла |
| StartObject | '{'                | Начало любого объекта метаданных   |
| Value       | неважно            | Текущее прочитанное значение объекта метаданных, в том числе NULL |
| String      | неважно            | Текущее строковое значение объекта метаданных |
| EndObject   | '}'                | Конец любого объекта метаданных    |
| EndFile     | '}'                | Последняя фигурная скобка файла    |

Код чтения любого файла метаданных может выглядеть следующим образом:

```C#
// Идентификатор объекта метаданных - имя его файла в таблице Config
Guid uuid = new Guid("f6d7a041-3a57-457c-b303-ff888c9e98b7");

using (ConfigFileReader reader = new ConfigFileReader(
    DatabaseProvider.SQLServer, MS_CONNECTION_STRING, ConfigTableNames.Config, uuid))
{
    while (reader.Read()) // Читаем файл до конца
    {
        if (reader.Token == TokenType.StartFile)
        {
            // Обрабатываем начало файла
        }
        else if (reader.Token == TokenType.StartObject)
        {
            // Обрабатываем начало объекта метаданных
        }
        else if (reader.Token == TokenType.Value)
        {
            // Обрабатываем не строковое значение (число, uuid и т.д.)
        }
        else if (reader.Token == TokenType.String)
        {
            // Обрабатываем строковое значение
        }
        else if (reader.Token == TokenType.EndObject)
        {
            // Обрабатываем конец объекта метаданных
        }
        else if (reader.Token == TokenType.EndFile)
        {
            // Обрабатываем конец файла
        }
    }
}
```

Данный подход имеет право на существование, но есть одна проблема: структура файлов метаданных
не документирована фирмой 1С, а сам формат не содержит имён свойств объектов, как, например, JSON.

Для того, чтобы ориентироваться в файле в процессе его чтения класс **ConfigFileReader** реализует
отслеживание текущего уровня вложенности и пути к текущему значению объекта метаданных. Для этих
целей класс имеет свойства **Level** (int) и **Path** (массив).

Рассмотрим как это работает на примере, а затем объясним практический смысл этих действий.

Предположим, что мы читаем следующую структуру:

```txt
{1,{2,"Привет, 1С!",3}}
```

Каждая строка таблицы ниже соответствует одному вызову метода **Read** класса **ConfigFileReader**:

| **Токен**   | **Level** | **Path**     | **Value**        | **Смысловое объяснение** |
|-------------|-----------|--------------|------------------|--------------------------|
| StartFile   | 0         | [-1]         | нет              | Путь из одного элемента, который не указывает ни на какое значение объекта (-1) уровня 0 |
| Value       | 0         | [0]          | 1                | Путь из одного элемента, который указывает на первое значение объекта уровня 0 |
| StartObject | 1         | [1][-1]      | нет              | Путь из двух элементов, который указывает ни на какое значение объекта (-1) уровня 1 |
| Value       | 1         | [1][0]       | 2                | Путь из двух элементов, который указывает на первое значение объекта уровня 1 |
| String      | 1         | [1][1]       | "Привет, 1С!"    | Путь из двух элементов, который указывает на второе значение объекта уровня 1 |
| Value       | 1         | [1][2]       | 3                | Путь из двух элементов, который указывает на третье значение объекта уровня 1 |
| EndObject   | 0         | [1]          | нет              | Путь из одного элемента, который не указывает ни на какое значение объекта уровня 0 |
| EndFile     | -1        | нет          | нет              | Путь и уровень отсутствуют |

Следует отметить, что нумерация уровней и значений объекта начинается с нуля.
Длина пути всегда равна значению уровня + 1, то есть Path.Length = Level + 1.
Другими словами Level всегда указывает на последний элемент пути: Path[Level] = текущий индекс значения текущего объекта метаданных.

> Исключительной ситуацией является положение в позиции **StartObject**, так как начало объекта само является
> значением для другого, родительского по отношению к нему самому, объекта. То есть текущее значение в файле
> объекта метаданных для позиции **StartObject** является Path[Level - 1] - начало объекта указывает на самого себя.

Когда класс читает токен **EndObject** его путь и уровень уменьшаются на 1.
Таким образом он указывает на только что завершившийся объект: Path[Level] = 1 (второе значение объекта уровня 0).

Зачем вся эта математика нужна ? Представим наш пример в формате DaJet:

```txt
[0] (0) "1"
[0] (1) DaJet.Metadata.Model.ConfigObject
----[1] (1.0) "2"
----[1] (1.1) "Привет, 1С!"
----[1] (1.2) "3"
```

Как видите пути (0), (1), (1.0), (1.1) и (1.2) точно соответствуют значениям в выше приведённой таблице.
Да, немного путает путь [1][-1] для StartObject (DaJet.Metadata.Core.ConfigObject), но это, как уже говорилось,
небольшое исключение, которое можно понять.

Всё это помогает решить выше обозначенную проблему отсутствия документации и имён свойств следующим образом.

Используя [методику исследования файлов метаданных](https://github.com/zhichkin/dajet/blob/main/doc/metadata-internals/reverse-engineering/README.md),
а также выгрузку файлов метаданных в формате DaJet на диск, мы можем точно определить нужные нам значения и их пути.
Остаётся открытым только один вопрос: как удобно обрабатывать эти значения программно ?

Для удобства программирования по путям в иерархической структуре разработано ещё два вспомогательных класса:
[**ConfigFileParser**](https://github.com/zhichkin/dajet/blob/main/src/dajet-metadata/core/ConfigFileParser.cs)
и [**ConfigFileConverter**](https://github.com/zhichkin/dajet/blob/main/src/dajet-metadata/core/ConfigFileConverter.cs).
Оба этих класса фактически реализуют паттерн программирования [Visitor (Посетитель)](https://ru.wikipedia.org/wiki/%D0%9F%D0%BE%D1%81%D0%B5%D1%82%D0%B8%D1%82%D0%B5%D0%BB%D1%8C_(%D1%88%D0%B0%D0%B1%D0%BB%D0%BE%D0%BD_%D0%BF%D1%80%D0%BE%D0%B5%D0%BA%D1%82%D0%B8%D1%80%D0%BE%D0%B2%D0%B0%D0%BD%D0%B8%D1%8F)).

Класс **ConfigFileParser** обходит дерево объектов и для каждого пути выполняет заданный для него обработчик.
Обработчики для путей задаёт класс **ConfigFileConverter**, являясь таким образом настроечным для класса **ConfigFileParser**.

Для нашего примера выше разработаем специализированный класс-парсер:

```C#
public sealed class HelloParser
{
    private ConfigFileParser _parser = new ConfigFileParser();
    private ConfigFileConverter _converter = new ConfigFileConverter();

    public int Значение1  { get; set; }
    public int Значение2  { get; set; }
    public int Значение3  { get; set; }
    public int Hello { get; set; }

    public void Parse(in ConfigFileReader reader)
    {
        // Настраиваем конвертер значений файла по известным нам путям
        _converter[0] += ОбработатьЗначение1;
        _converter[1][0] += ОбработатьЗначение2;
        _converter[1][1] += ОбработатьHello;
        _converter[1][2] += ОбработатьЗначение3;

        // Выполняем последовательное чтение файла с вызовом указанных обработчиков
        _parser.Parse(in reader, in _converter);
    }
    private void ОбработатьЗначение1(in ConfigFileReader source, in CancelEventArgs args)
    {
        Значение1 = source.GetInt32();
    }
    private void ОбработатьЗначение2(in ConfigFileReader source, in CancelEventArgs args)
    {
        Значение2 = source.GetInt32();
    }
    private void ОбработатьЗначение3(in ConfigFileReader source, in CancelEventArgs args)
    {
        Значение3 = source.GetInt32();
    }
    private void ОбработатьHello(in ConfigFileReader source, in CancelEventArgs args)
    {
        Hello = source.Value;
    }
}
```

Использование **HelloParser**:

```C#

HelloParser parser = new HelloParser();

using (ConfigFileReader reader = new ConfigFileReader(
    DatabaseProvider.SQLServer, MS_CONNECTION_STRING, ConfigTableNames.Config, "hello"))
{
    parser.Parse(in reader);
}

Console.WriteLine($"Hello = {parser.Hello}");
Console.WriteLine($"Значение 1 = {parser.Значение1}");
Console.WriteLine($"Значение 2 = {parser.Значение2}");
Console.WriteLine($"Значение 3 = {parser.Значение3}");

// Ожидаемый вывод в консоль:
// 
// Hello = Привет, 1С!
// Значение 1 = 1
// Значение 2 = 2
// Значение 3 = 3

```

Самый простой пример парсера (читает GUID конфигурации из файла 'root'):
[**RootFileParser**](https://github.com/zhichkin/dajet/blob/main/src/dajet-metadata/parsers/RootFileParser.cs).

Более сложный пример - класс для чтения коллекций реквизитов объектов метаданных:
[**MetadataPropertyCollectionParser**](https://github.com/zhichkin/dajet/blob/main/src/dajet-metadata/parsers/MetadataPropertyCollectionParser.cs).

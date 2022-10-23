## Анатомия метаданных 1С:Предприятие 8

0. [Введение](#введение)
1. [Описание формата файлов метаданных](https://github.com/zhichkin/dajet/blob/main/doc/metadata-internals/format-description/README.md)
2. [Схема хранения метаданных и связи между ними](https://github.com/zhichkin/dajet/blob/main/doc/metadata-internals/metadata-storage/README.md)
3. [Описание типов реквизита объекта метаданных](https://github.com/zhichkin/dajet/blob/main/doc/metadata-internals/datatypes-description/README.md)
4. [Методика исследования файлов метаданных](https://github.com/zhichkin/dajet/blob/main/doc/metadata-internals/reverse-engineering/README.md)
5. [Принцип работы парсера файлов метаданных](https://github.com/zhichkin/dajet/blob/main/doc/metadata-internals/parser-description/README.md)
6. [Методика тестирования библиотеки DaJet.Metadata]((https://github.com/zhichkin/dajet/blob/main/doc/metadata-internals/checklist/README.md))

### Введение

Клиент-серверная версия 1С:Предприятие 8 (далее просто 1С) хранит описание своих объетов (метаданные)
в таблицах СУБД.

В частности, библиотека **DaJet.Metadata** использует следующие таблицы:
- **_YearOffset** - смещение дат (добавляется ко всем значениям дат в СУБД)
- **IBVersion** - версия среды выполнения конфигурации 1С (влияет на структуру хранения данных)
- **Params** - файл DBNames хранит сопоставление объектов метаданных соответствующим объектам СУБД
- **Config** - хранит описание объектов метаданных основной конфигурации (root - корневой файл)

![Файл DBNames таблицы Params](https://github.com/zhichkin/dajet/blob/main/doc/metadata-internals/images/params_dbnames.png)

Таблица **Config** является главной таблицей описания метаданных. Она имеет следующие поля:

| **Имя поля**   | **Тип данных** | **Назначение**                                 |
|----------------|----------------|------------------------------------------------|
| **FileName**   | nvarchar(128)  | Идентификатор (GUID) файла и часто объекта метаданных |
| **Creation**   | datetime2      | Дата создания файла данных |
| **Modified**   | datetime2      | Дата последнего изменения файла данных |
| **Attributes** | smallint       | Атрибуты файла данных |
| **DataSize**   | bigint         | Размер файла данных в байтах |
| **BinaryData** | varbinary(max) | Данные файла описания метаданных, как правило 1 файл - 1 объект |
| **PartNo**     | int            | Добавлено в версии 1С 8.3 для нумерации частей большого файла метаданных |

Поле **BinaryData** содержит описание объекта метаданных в формате похожем на что-то между Protocol Buffers
(protobuf) и JSON. При этом сами данные чаще всего сжаты по алгоритму deflate (зависит от размера файла).
Кодировка данных файла - UTF-8. Чтобы понять сжаты данные или нет достаточно проверить первые 3 байта
бинарных данных поля **BinaryData**. Если это несжатые данные в формате UTF-8, то эти 3 байта будут равны
значениям 0xEF, 0xBB и 0xBF, что соответствует bite order mark (BOM) формата UTF-8.

Прочитать данные файла можно при помощи следующего кода SQL:

```SQL
-- Microsoft SQL Server

SELECT
    (CASE WHEN SUBSTRING(BinaryData, 1, 3) = 0xEFBBBF THEN 1 ELSE 0 END) AS UTF8,
    CAST(DataSize AS int) AS DataSize,
    BinaryData
FROM
    Config
WHERE
    FileName = @FileName;

-- PostgreSQL

SELECT
    (CASE WHEN SUBSTRING(binarydata, 1, 3) = E'\\\\xEFBBBF' THEN 1 ELSE 0 END) AS UTF8,
    CAST(datasize AS int) AS datasize,
    binarydata
FROM
    config
WHERE
    CAST(filename AS varchar) = @filename;
```

Получить данные в формате UTF-8 при помощи C# можно при помощи следующего кода:

```C#

byte[] fileData = GetFileDataFromDatabase();

bool utf8 = false; // может быть прочитано напрямую из СУБД

if (fileData.Length > 2)
{
    utf8 = fileData[0] == 0xEF  // (b)yte
        && fileData[1] == 0xBB  // (o)rder
        && fileData[2] == 0xBF; // (m)ark
}

MemoryStream memory = new MemoryStream(fileData);

StreamReader stream;

if (utf8)
{
    stream = new StreamReader(memory, Encoding.UTF8);
}
else
{
    DeflateStream deflate = new DeflateStream(memory, CompressionMode.Decompress);
    stream = new StreamReader(deflate, Encoding.UTF8);
}

string fileText = stream.ReadToEnd();

```

Для чтения файлов конфигурации 1С библиотека **DaJet.Metadata** использует класс
[ConfigFileReader](https://github.com/zhichkin/dajet/blob/main/src/dajet-metadata/core/ConfigFileReader.cs).

```C#
Guid root = Guid.Empty;
using (ConfigFileReader reader = new ConfigFileReader(
           DatabaseProvider.SQLServer, MS_CONNECTION_STRING,
           ConfigTableNames.Config, "root"))
{
    root = new RootFileParser().Parse(in reader);
}
```


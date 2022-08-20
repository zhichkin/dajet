# DaJet
Набор инструментов на C# для интеграции и расширения возможностей платформы 1С:Предприятие 8.

Поддерживается работа с базами данных 1С под управлением СУБД Microsoft SQL Server и PostgreSQL.

Работа DaJet основана на чтении метаданных 1С:Предприятие 8 напрямую из баз данных 1С.

Базовыми библиотеками являются DaJet.Metadata и DaJet.Scripting.

[Обсуждение и поддержка пользователей DaJet](https://t.me/dajet_studio_group)

Пример чтения метаданных 1С при помощи DaJet.Metadata:
```C#
using DaJet.Metadata;
using DaJet.Metadata.Model;

static void Main(string[] args)
{
   // Строка подключения к базе данных 1С
   string MS_CONNECTION_STRING = "Data Source=MY_DATABASE_SERVER;Initial Catalog=MY_1C_DATABASE;Integrated Security=True;Encrypt=False;";

   // Регистрируем настройки подключения к базе данных 1С по строковому ключу
   InfoBaseOptions options = new()
   {
      Key = "my_1c_infobase",
      ConnectionString = MS_CONNECTION_STRING,
      DatabaseProvider = DatabaseProvider.SqlServer
   };

   MetadataService metadata = new();
   metadata.Add(options);

   // Подключаемся к информационной базе 1С
   if (!metadata.TryGetMetadataCache(options.Key, out MetadataCache cache, out string error))
   {
      Console.WriteLine($"Ошибка открытия информационной базы: {error}");
      return;
   }

   // Читаем метаданные справочников
   foreach (MetadataItem item in cache.GetMetadataItems(MetadataTypes.Catalog))
   {
      Catalog catalog = cache.GetMetadataObject<Catalog>(item.Uuid);

      // Имя справочника и его таблицы СУБД
      Console.WriteLine($"{catalog.Name} [{catalog.TableName}]");

      // Читаем метаданные свойств справочников
      foreach (MetadataProperty property in catalog.Properties)
      {
         // Имя свойства справочника
         Console.WriteLine(property.Name);

         // Читаем метаданные колонок таблицы СУБД
         foreach (MetadataColumn column in property.Columns)
         {
            // Имя колонки таблицы СУБД
            Console.WriteLine(column.Name);
         }
      }
   }
}
```

Пример выполнения запроса 1С при помощи DaJet.Scripting:
```C#
// Класс для вывода данных
public class ProductInfo
{
   public string Code { get; set; }
   public string Name { get; set; }
   public EntityRef Reference { get; set; }
   public bool IsMarkedForDeletion { get; set; }
}

// Метод для получения данных
public void GetProductInfo()
{
   // Строка подключения к базе данных 1С
   string MS_CONNECTION_STRING = "Data Source=MY_DATABASE_SERVER;Initial Catalog=MY_1C_DATABASE;Integrated Security=True;Encrypt=False;";

   // Регистрируем настройки подключения к базе данных 1С по строковому ключу
   InfoBaseOptions options = new()
   {
      Key = "my_1c_infobase",
      ConnectionString = MS_CONNECTION_STRING,
      DatabaseProvider = DatabaseProvider.SqlServer
   };

   MetadataService metadata = new();
   metadata.Add(options);

   // Подключаемся к информационной базе 1С
   if (!metadata.TryGetMetadataCache(options.Key, out MetadataCache cache, out string error))
   {
      Console.WriteLine($"Ошибка открытия информационной базы: {error}");
      return;
   }

   // Настраиваем выполнение запроса к базе данных 1С
   ScriptExecutor executor = new(_cache);
   executor.Parameters.Add("КодТовара", "А-1234");

   string script = "ВЫБРАТЬ "
      + "Код             КАК Code, "
      + "Наименование    КАК Name, "
      + "Ссылка          КАК Reference, "
      + "ПометкаУдаления КАК IsMarkedForDeletion "
      + "ИЗ Справочник.Номенклатура "
      + "WHERE Код = @КодТовара;";

   // Выполняем запрос и выводим данные на консоль
   try
   {
      foreach (ProductInfo entity in executor.ExecuteReader<ProductInfo>(script))
      {
         Console.WriteLine($"[{entity.Code}] {entity.Name} : {entity.Reference} {entity.IsMarkedForDeletion}");
      }
   }
   catch (Exception exception)
   {
      Console.WriteLine(ExceptionHelper.GetErrorMessage(exception));
   }
}
```
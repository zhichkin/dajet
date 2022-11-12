using DaJet.Data;
using DaJet.Data.Provider;

namespace DaJet_Console
{
    internal static class Program
    {
        private const string PG_CONNECTION_STRING = "Host=127.0.0.1;Port=5432;Database=dajet-metadata-pg;Username=postgres;Password=postgres;";
        private const string MS_CONNECTION_STRING = "Data Source=ZHICHKIN;Initial Catalog=dajet-metadata-ms;Integrated Security=True;Encrypt=False;";
        static void Main()
        {
            UsageExample1();
        }
        private static void UsageExample1()
        {
            string commandText =
                "ВЫБРАТЬ ПЕРВЫЕ 1 " +
                "Ссылка, Код, Наименование, ПометкаУдаления, " +
                "РеквизитДата, РеквизитЧисло, СоставнойТип, Валюта " +
                "ИЗ Справочник.Номенклатура;";

            using (OneDbConnection connection = new(PG_CONNECTION_STRING))
            {
                connection.Open();

                using (OneDbCommand command = connection.CreateCommand())
                {
                    command.CommandText = commandText;

                    using (OneDbDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            for (int ordinal = 0; ordinal < reader.FieldCount; ordinal++)
                            {
                                Console.WriteLine(string.Format(" {0,-16} {1,-9} {2,-42} {3}",
                                    reader.GetName(ordinal),         // Имя поля выборки
                                    reader.GetDataTypeName(ordinal), // Тип данных значения
                                    reader.GetValue(ordinal),        // Значение поля выборки
                                    reader.GetFieldType(ordinal)));  // Полное имя типа данных
                            }
                        }
                    }
                }
            }
        }
    }
}
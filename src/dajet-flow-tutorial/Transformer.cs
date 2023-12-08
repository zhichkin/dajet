using DaJet.Data;
using DaJet.Json;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace DaJet.Flow.Tutorial
{
    /// <summary>
    /// Блок <b>Transformer</b> выполняет функцию перобразования любых объектов данных в формат JSON.
    /// <br/>После сериализации <b>Transformer</b> передаёт результат далее по конвейеру в следующий блок.
    /// </summary>
    public sealed class Transformer : TransformerBlock<DataObject, DataObject>
    {
        // Настройки сериализатора DataObject
        private static readonly JsonWriterOptions JsonOptions = new()
        {
            Indented = true,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        };
        // Создаём сериализатор DataObject в формат JSON
        private static readonly DataObjectJsonConverter _converter = new();

        private readonly TransformerOptions _options;
        public Transformer(TransformerOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }
        protected override void _Transform(in DataObject input, out DataObject output)
        {
            // Формируем исходящие для данного блока данные
            DataObject message = new();
            message.SetValue("ТипСообщения", input.GetName());
            message.SetValue("ФорматСообщения", _options.ContentType);

            // Выполняем сериализацию входящего в блок объекта данных
            // в формат JSON, используя буфер оперативной памяти
            using (MemoryStream memory = new())
            {
                // Создаём объект записи в формате JSON с нашими настройками
                using (Utf8JsonWriter writer = new(memory, JsonOptions))
                {
                    // Используем свой сериализатор DataObject
                    _converter.Write(writer, input, null);

                    // Завершаем запись JSON
                    writer.Flush();

                    // Забираем JSON из буфера памяти и кодируем его в UTF-8
                    string json = Encoding.UTF8.GetString(memory.ToArray());

                    // Помещаем полученный JSON в исходящие данные блока
                    message.SetValue("ТелоСообщения", json);
                }
            }
            
            output = message; // Передаём данные этого блока дальше по стеку вызовов
        }
    }
}
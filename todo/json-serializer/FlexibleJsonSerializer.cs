using System.Text.Json;

namespace MDLP
{
    public interface IFlexibleJsonSerializer
    {
        T FromJson<T>(string json);
        string ToJson<T>(T entity);
        ISerializationBinder Binder { get; }
    }
    public sealed class FlexibleJsonSerializer : IFlexibleJsonSerializer
    {
        private readonly IReferenceResolver _resolver = new JsonReferenceResolver();
        private readonly ISerializationBinder _binder = new JsonSerializationBinder();
        private readonly JsonSerializerOptions _options = new JsonSerializerOptions();
        public FlexibleJsonSerializer()
        {
            _options.WriteIndented = true;
            _options.Converters.Add(new FlexibleJsonConverter(_binder, _resolver));
        }
        public ISerializationBinder Binder { get { return _binder; } }
        public string ToJson<T>(T entity)
        {
            _resolver.Clear();
            return JsonSerializer.Serialize(entity, _options);
        }
        public T FromJson<T>(string json)
        {
            return JsonSerializer.Deserialize<T>(json, _options);
        }
    }
}

//public Module()
//{
//    _serializer = new FlexibleJsonSerializer();
//    var knownTypes = _serializer.Binder.KnownTypes;
//    knownTypes.Add(1, typeof(Language));
//    knownTypes.Add(2, typeof(Namespace));
//}
//public void Persist(Entity model)
//{
//    string json = _serializer.ToJson(model);

//    string filePath = GetModuleFilePath();
//    using (StreamWriter writer = File.CreateText(filePath))
//    {
//        writer.Write(json);
//    }
//}
//private void ReadModuleFromFile()
//{
//    string filePath = GetModuleFilePath();
//    string json = File.ReadAllText(filePath);
//    if (string.IsNullOrWhiteSpace(json)) return;

//    Entity entity = _serializer.FromJson(json);
//    BuildTreeView(entity);
//}
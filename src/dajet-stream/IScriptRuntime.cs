namespace DaJet
{
    public interface IScriptRuntime
    {
        bool TrySetValue(in string name, in object value);
        bool TryGetValue(in string name, out object value);
    }
}
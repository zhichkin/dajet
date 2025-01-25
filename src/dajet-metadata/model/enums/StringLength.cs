namespace DaJet.Metadata.Model
{
    public enum StringKind
    {
        Fixed = 0,
        Variable = 1
    }
    public static class StringKindExtensions
    {
        public static string GetNameRu(this StringKind kind)
        {
            if (kind == StringKind.Fixed) { return "Фиксированная"; }
            else if (kind == StringKind.Variable) { return "Переменная"; }
            else
            {
                return "Переменная";
            }
        }
    }
}
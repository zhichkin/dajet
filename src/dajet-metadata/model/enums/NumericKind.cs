namespace DaJet.Metadata.Model
{
    public enum NumericKind
    {
        CanBeNegative = 0,
        AlwaysPositive = 1
    }
    public static class NumericKindExtensions
    {
        public static string GetNameRu(this NumericKind kind)
        {
            if (kind == NumericKind.CanBeNegative) { return "Знаковое"; }
            else if (kind == NumericKind.AlwaysPositive) { return "Беззнаковое"; }
            else
            {
                return "Знаковое";
            }
        }
        public static string GetNameEn(this NumericKind kind)
        {
            if (kind == NumericKind.CanBeNegative) { return "Signed"; }
            else if (kind == NumericKind.AlwaysPositive) { return "Unsigned"; }
            else
            {
                return "Signed";
            }
        }
    }
}
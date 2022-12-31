namespace DaJet.Studio.Model
{
    public class DataTypeModel
    {
        public bool IsMultipleType { get; set; }
        public bool IsUuid { get; set; }
        public bool IsBinary { get; set; }
        public bool IsValueStorage { get; set; }
        public bool CanBeBoolean { get; set; }
        public bool CanBeString { get; set; }
        public int StringLength { get; set; } = 10;
        public StringKind StringKind { get; set; } = StringKind.Variable;
        public bool CanBeNumeric { get; set; }
        public int NumericScale { get; set; } = 0;
        public int NumericPrecision { get; set; } = 10;
        public NumericKind NumericKind { get; set; } = NumericKind.CanBeNegative;
        public bool CanBeDateTime { get; set; }
        public DateTimePart DateTimePart { get; set; } = DateTimePart.Date;
        public bool CanBeReference { get; set; }
        public int TypeCode { get; set; } = 0;
        public Guid Reference { get; set; } = Guid.Empty;
        public List<Guid> Identifiers { get; set; } = new();
        public List<MetadataItemModel> References { get; } = new();
        public override string ToString()
        {
            if (IsMultipleType) return "Multiple";
            else if (IsUuid) return "Uuid";
            else if (IsBinary) return "Binary";
            else if (IsValueStorage) return "ValueStorage";
            else if (CanBeString) return "String";
            else if (CanBeBoolean) return "Boolean";
            else if (CanBeNumeric) return "Numeric";
            else if (CanBeDateTime) return "DateTime";
            else if (CanBeReference) return "Reference";
            else return "Undefined";
        }
    }
}
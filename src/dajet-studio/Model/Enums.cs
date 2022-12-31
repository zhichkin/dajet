namespace DaJet.Studio.Model
{
    public enum PropertyPurpose
    {
        System = 0,
        Property = 1,
        Dimension = 2,
        Measure = 3
    }
    public enum StringKind
    {
        Fixed = 0,
        Variable = 1
    }
    public enum NumericKind
    {
        CanBeNegative = 0,
        AlwaysPositive = 1
    }
    public enum DateTimePart
    {
        Date = 0,
        Time = 1,
        DateTime = 2
    }
}
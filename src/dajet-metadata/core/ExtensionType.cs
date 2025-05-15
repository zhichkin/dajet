using System;

namespace DaJet.Metadata.Core
{
    [Flags] public enum ExtensionType
    {
        None = 0,
        DataSchema = 1,
        ChangeTracking = 2
    }
    public static class ExtensionTypeHelper
    {
        public static bool IsDataTracking(this ExtensionType type)
        {
            return (type & ExtensionType.DataSchema) == ExtensionType.DataSchema;
        }
        public static bool IsChangeTracking(this ExtensionType type)
        {
            return (type & ExtensionType.ChangeTracking) == ExtensionType.ChangeTracking;
        }
    }
}
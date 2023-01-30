using System;

namespace DaJet.Metadata.Core
{
    public static class SingleTypes
    {
        public static readonly Guid ValueStorage = new Guid("e199ca70-93cf-46ce-a54b-6edc88c3a296"); // ХранилищеЗначения - varbinary(max)
        public static readonly Guid UniqueIdentifier = new Guid("fc01b5df-97fe-449b-83d4-218a090e681e"); // УникальныйИдентификатор - binary(16)
    }
}
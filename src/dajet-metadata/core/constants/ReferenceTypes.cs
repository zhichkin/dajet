using System;

namespace DaJet.Metadata.Core
{
    public static class ReferenceTypes
    {
        ///<summary>ЛюбаяСсылка</summary>
        public static readonly Guid AnyReference = new Guid("280f5f0e-9c8a-49cc-bf6d-4d296cc17a63");
        ///<summary>ПланСчетовСсылка</summary>
        public static readonly Guid Account = new("c5f4f1d2-da30-4348-a76e-e4a2ab5ecfcc");
        ///<summary>СправочникСсылка</summary>
        public static readonly Guid Catalog = new Guid("e61ef7b8-f3e1-4f4b-8ac7-676e90524997");
        ///<summary>ДокументСсылка</summary>
        public static readonly Guid Document = new Guid("38bfd075-3e63-4aaa-a93e-94521380d579");
        ///<summary>ПеречислениеСсылка</summary>
        public static readonly Guid Enumeration = new Guid("474c3bf6-08b5-4ddc-a2ad-989cedf11583");
        ///<summary>ПланОбменаСсылка</summary>
        public static readonly Guid Publication = new Guid("0a52f9de-73ea-4507-81e8-66217bead73a");
        ///<summary>ПланВидовХарактеристикСсылка</summary>
        public static readonly Guid Characteristic = new Guid("99892482-ed55-4fb5-a7f7-20888820a758");
    }
}
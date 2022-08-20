using System;

namespace DaJet.Metadata.Core
{
    internal static class Components
    {
        public static Guid General = new Guid("9cd510cd-abfc-11d4-9434-004095e12fc7"); // 3.0 - Общие объекты
        public static Guid Operations = new Guid("9fcd25a0-4822-11d4-9414-008048da11f9"); // 4.0 - Оперативный учёт
        public static Guid Accounting = new Guid("e3687481-0a87-462c-a166-9f34594f9bba"); // 5.0 - Бухгалтерский учёт
        public static Guid Calculation = new Guid("9de14907-ec23-4a07-96f0-85521cb6b53b"); // 6.0 - Расчёт
        public static Guid BusinessProcess = new Guid("51f2d5d8-ea4d-4064-8892-82951750031e"); // 7.0 - Бизнес-процессы
    }
}
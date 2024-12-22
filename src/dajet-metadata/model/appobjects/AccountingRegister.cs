using System;

namespace DaJet.Metadata.Model
{
    public sealed class AccountingRegister : ApplicationObject
    {
        ///<summary>План счетов</summary>
        public Guid ChartOfAccounts { get; set; } = Guid.Empty;
        ///<summary>Корреспонденция</summary>
        public bool UseCorrespondence { get; set; } = false;
        ///<summary>Разрешить разделение итогов</summary>
        public bool UseSplitter { get; set; } = true;
    }
}
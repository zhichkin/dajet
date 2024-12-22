namespace DaJet.Metadata.Model
{
    public sealed class AccountingRegister : ApplicationObject
    {
        /// <summary>
        /// Разрешить разделение итогов
        /// </summary>
        public bool UseSplitter { get; set; } = true;
    }
}
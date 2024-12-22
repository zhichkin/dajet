namespace DaJet.Metadata.Model
{
    ///<summary>
    ///Вид счёта плана счетов:
    ///<br>Active (Активный) = 0</br>
    ///<br>Passive (Пассивный) = 1</br>
    ///<br>ActivePassive (Активно-пассивный) = 2</br>
    ///</summary>
    public enum AccountType
    {
        ///<summary>Активный</summary>
        Active = 0,
        ///<summary>Пассивный</summary>
        Passive = 1,
        ///<summary>Активно-пассивный</summary>
        ActivePassive = 2
    }
}
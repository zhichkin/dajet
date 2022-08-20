namespace DaJet.Metadata.Model
{
    ///<summary>
    ///Вид движения регистра накопления остатков:
    ///<br>Receipt (Приход) = 0</br>
    ///<br>Expense (Расход) = 1</br>
    ///</summary>
    public enum RecordType
    {
        ///<summary>Приход</summary>
        Receipt = 0,
        ///<summary>Расход</summary>
        Expense = 1
    }
}
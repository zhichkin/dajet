
DECLARE @Отправитель string = 'PG_TEST'

DECLARE @Объект  object
DECLARE @Счётчик integer

USE 'pgsql://postgres:postgres@localhost:5432/test'

   STREAM Ссылка, Код, Наименование, ПометкаУдаления, Вид
     INTO @Объект
     FROM Справочник.Номенклатура
    ORDER BY Код ASC

   USE 'mssql://Z-NOTEBOOK/test'
      INSERT РегистрСведений.ВходящаяОчередь
      SELECT НомерСообщения = VECTOR('so_import')
           , ДатаВремя      = NOW()
           , Отправитель    = @Отправитель
           , ТипСообщения   = 'Справочник.Номенклатура'
           , ТелоСообщения  = JSON(@Объект)
   END

   SET @Счётчик = @Счётчик + 1
END

RETURN '[STREAM] PG_TEST > MS_TEST = ' + @Счётчик
DECLARE @Владелец uuid
DECLARE @МассивЗначений array
DECLARE @Значение object = SELECT Ссылка = NEWUUID()
                                , Имя = ''
                                , Синоним = ''

FOR @Значение IN @МассивЗначений

   INSERT Справочник.ЗначенияПеречислений
   SELECT Ссылка       = NEWUUID()
        , Владелец     = @Владелец
        , Наименование = @Значение.Имя
        , Синоним      = @Значение.Синоним
        , Значение     = @Значение.Ссылка
END
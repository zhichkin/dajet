DECLARE @Ссылка uuid
DECLARE @ИнфоБаза uuid
DECLARE @Объект object = SELECT Ссылка = NEWUUID()
                              , Тип = ''
                              , Имя = ''
                              , Код = 0.0
                              , Синоним = ''
                              , Таблица = ''
                              , ПолноеИмя = ''
                              , Свойства = NULL
SET @Ссылка = NEWUUID()

INSERT Справочник.ОбъектыМетаданных
SELECT Ссылка        = @Ссылка
     , Владелец      = @ИнфоБаза
     , Код           = @Объект.Код
     , Тип           = @Объект.Тип
     , Наименование  = @Объект.Имя
     , Синоним       = @Объект.Синоним
     , ПолноеИмя     = @Объект.ПолноеИмя
     , Идентификатор = @Объект.Ссылка
     , Таблица       = @Объект.Таблица

EXECUTE 'file://code/md-streamer/properties.djs'
   WITH Владелец      = @Ссылка
      , МассивСвойств = @Объект.Свойства
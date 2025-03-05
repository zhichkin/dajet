DECLARE @Объект object
DECLARE @СоставПланаОбмена array
DECLARE @КодПланаОбмена number
DECLARE @ИмяПланаОбмена string = 'ПланОбмена.ОбменДанными.Состав'
DECLARE @task object
DECLARE @tasks array
DECLARE @Выполнено boolean = FALSE

USE 'mssql://server/database'

   SET @КодПланаОбмена = TYPEOF('ПланОбмена.ОбменДанными')
   PRINT '[ПЛАН ОБМЕНА] ПланОбмена.ОбменДанными {' + @КодПланаОбмена + '}'

   PROCESS @ИмяПланаОбмена WITH DaJet.Runtime.Metadata INTO @СоставПланаОбмена

   FOR @Объект IN @СоставПланаОбмена
      EXECUTE WORK 'file://code/ОбменДанными/Экспорт/{@Объект.Тип}/{@Объект.Имя}.djs'
      DEFAULT 'file://code/ОбменДанными/ПравилаОбменаНеОпределены.djs'
           AS @Объект.ПолноеИмя -- Наименование задания (потока выполнения)
         WITH ПланОбмена = @КодПланаОбмена
            , Метаданные = @Объект
         INTO @tasks
   END
END

-- Ждём пока не завершатся все задания
WHILE @Выполнено = FALSE

   -- Ждём завершения всех заданий 30 секунд
   WAIT ALL @tasks INTO @Выполнено TIMEOUT 30

   -- Выводим состояние всех заданий
   FOR @task IN @tasks
      PRINT '[' + @task.Name + '] ' + @task.Status
   END

END
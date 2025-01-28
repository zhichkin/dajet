DECLARE @Объект object -- Объект метаданных конфигурации 1С:Предприятие 8
DECLARE @Команда string = 'stream-metadata' --'check-database-schema'
DECLARE @ИнфоБаза uuid
DECLARE @Счётчик number = 0

PRINT '[' + @Команда + '] НАЧАЛО'

USE 'mssql://zhichkin/unf'

   PROCESS @Команда WITH DaJet.Runtime.MetadataStreamer INTO @Объект

   USE 'mssql://zhichkin/metadata-registry'

      CASE WHEN @Объект.Тип = 'ИнформационнаяБаза' THEN
                EXECUTE 'file://code/md-streamer/infobase.djs' WITH ИнфоБаза = @Объект INTO @ИнфоБаза

           WHEN @Объект.Тип = 'ОбщийРеквизит' OR @Объект.Тип = 'ОпределяемыйТип' THEN
                PRINT @Объект.ПолноеИмя -- Игнорируем (не влияет на работу с базой данных)

           WHEN @Объект.Тип = 'Перечисление' THEN
                EXECUTE 'file://code/md-streamer/enum.djs'
                   WITH ИнфоБаза = @ИнфоБаза, Объект = @Объект

           WHEN @Объект.Тип = 'ПланОбмена' OR @Объект.Тип = 'ПланСчетов'
             OR @Объект.Тип = 'Справочник' OR @Объект.Тип = 'Документ'
             OR @Объект.Тип = 'ПланВидовХарактеристик' THEN
                EXECUTE 'file://code/md-streamer/object.djs'
                   WITH ИнфоБаза = @ИнфоБаза, Объект = @Объект

           WHEN @Объект.Тип = 'РегистрСведений'
             OR @Объект.Тип = 'РегистрНакопления'
             OR @Объект.Тип = 'РегистрБухгалтерии' THEN
                EXECUTE 'file://code/md-streamer/register.djs'
                   WITH ИнфоБаза = @ИнфоБаза, Объект = @Объект
      END
   END

   SET @Счётчик = @Счётчик + 1

END -- Конец выполнения потокового процессора MetadataStreamer

PRINT '[' + @Команда + '] Обработано ' + @Счётчик + ' объектов'

RETURN '[' + @Команда + '] КОНЕЦ'
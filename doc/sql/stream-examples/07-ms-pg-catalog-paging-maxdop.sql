-- *************************************************************
-- * Источник сообщений SQL Server - справочник "Номенклатура" *
-- *************************************************************
USE "mssql://ZHICHKIN/dajet-metadata-ms"

DECLARE @message object
DECLARE @page    object
DECLARE @iterator array

          SELECT RowOffset = 0, PageSize = 2 INTO @iterator
UNION ALL SELECT RowOffset = 2, PageSize = 2
UNION ALL SELECT RowOffset = 4, PageSize = 2

FOR EACH @page IN @iterator --MAXDOP 4

SELECT Ссылка       = UUIDOF(Ссылка)
     , Код          = RTRIM(LTRIM(Код))
     , Наименование = SUBSTRING(Наименование, 1, 10)
  INTO @message
  FROM Справочник.Номенклатура
 ORDER BY Код DESC
OFFSET      @page.RowOffset ROWS
 FETCH NEXT @page.PageSize  ROWS ONLY

-- ************************************************************************
-- * Приёмник сообщений PostgreSQL - регистр сведений "ВходящиеСообщения" *
-- ************************************************************************
USE "pgsql://postgres:postgres@127.0.0.1:5432/dajet-metadata-pg"

INSERT РегистрСведений.ВходящиеСообщения
SELECT НомерСообщения = VECTOR('so_incoming_queue')
     , Отправитель    = @message.Код
     , ТипСообщения   = "Справочник.Номенклатура"
     , ТелоСообщения  = DaJet.Json(@message)
     , ОтметкаВремени = NOW()
USE [my_exchange];
GO

IF NOT EXISTS(SELECT 1 FROM sys.databases WHERE database_id = DB_ID('my_exchange') AND is_broker_enabled = 0x01)
BEGIN
	ALTER DATABASE [my_exchange] SET ENABLE_BROKER; -- Требуется монопольный доступ к базе данных!
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.service_queues WHERE [name] = 'dajet-agent-export-queue')
BEGIN
	CREATE QUEUE [dajet-agent-export-queue] WITH POISON_MESSAGE_HANDLING (STATUS = OFF);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.services WHERE [name] = 'dajet-agent-export-service')
BEGIN
	CREATE SERVICE [dajet-agent-export-service] ON QUEUE [dajet-agent-export-queue] ([DEFAULT]);
	--GRANT CONTROL ON SERVICE::[dajet-agent-export-service] TO [dajet-agent-user];
	--GRANT SEND ON SERVICE::[dajet-agent-export-service] TO [PUBLIC];
END;
GO

DECLARE @handle AS UNIQUEIDENTIFIER = CAST('00000000-0000-0000-0000-000000000000' AS UNIQUEIDENTIFIER);

BEGIN DIALOG @handle
FROM SERVICE [dajet-agent-export-service]
TO SERVICE 'dajet-agent-export-service', 'CURRENT DATABASE'
ON CONTRACT [DEFAULT] WITH ENCRYPTION = OFF;

SELECT @handle;
GO

--END CONVERSATION 'F41429B9-0559-EB11-9C8F-408D5C93CC8E' WITH CLEANUP;
--DROP SERVICE [dajet-agent-export-service];
--DROP QUEUE [dajet-agent-export-queue];

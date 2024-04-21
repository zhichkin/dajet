USE [my_exchange];
GO

BEGIN TRANSACTION;

WAITFOR (RECEIVE TOP (1)
		conversation_handle AS [dialog_handle],
		message_type_name   AS [message_type],
		message_body        AS [message_body]
		FROM [dajet-agent-export-queue]
	), TIMEOUT 1000;

COMMIT TRANSACTION;

--SELECT * FROM sys.dm_os_waiting_tasks where wait_type = 'BROKER_RECEIVE_WAITFOR';

--SELECT TOP(10) * FROM [dajet-agent-export-queue] WITH(NOLOCK); --WITH(READCOMMITTEDLOCK, READPAST);
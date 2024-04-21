USE [my_exchange];
GO

IF OBJECT_ID('tr_after_insert_Reference157', 'TR') IS NOT NULL DROP TRIGGER [dbo].[tr_after_insert_Reference157];
GO

CREATE TRIGGER [dbo].[tr_after_insert_Reference157] ON [dbo].[_Reference157]
AFTER INSERT
AS
	DECLARE @dialog_handle AS UNIQUEIDENTIFIER;

	SELECT @dialog_handle = [conversation_handle]
	FROM sys.conversation_endpoints AS e
	INNER JOIN sys.services AS s ON e.service_id = s.service_id
	AND e.is_initiator = 1
	AND e.state IN ('SO', 'CO')
	AND s.name = 'dajet-agent-export-service'
	AND e.far_service = 'dajet-agent-export-service';

	-- SO Started outbound. SQL Server processed a BEGIN CONVERSATION
	--    for this conversation, but no messages have yet been sent.

	-- CO Conversing. The conversation is established, and both sides of the conversation
	--    may send messages. Most of the communication for a typical service takes place
	--    when the conversation is in this state.

	IF (NOT @dialog_handle IS NULL)
	BEGIN
		DECLARE @notifications_count int;
		SELECT @notifications_count = COUNT(*) FROM [dajet-agent-export-queue] WITH(READCOMMITTEDLOCK, READPAST);
		IF (@notifications_count = 0)
		BEGIN
			DECLARE @message_body VARBINARY(MAX);
			SELECT @message_body = [_IDRRef] FROM inserted;
			SEND ON CONVERSATION (@dialog_handle) MESSAGE TYPE [DEFAULT] (@message_body);
		END;
	END;
GO

ALTER TABLE [dbo].[_Reference157] ENABLE TRIGGER [tr_after_insert_Reference157];
GO
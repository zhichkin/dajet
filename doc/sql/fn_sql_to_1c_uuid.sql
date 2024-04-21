
CREATE OR ALTER FUNCTION [dbo].[fn_sql_to_1c_uuid] (@uuid_sql binary(16))
RETURNS nvarchar(36)
AS
BEGIN
	DECLARE @uuid_1c binary(16) = CAST(REVERSE(SUBSTRING(@uuid_sql, 9, 8)) AS binary(8)) + SUBSTRING(@uuid_sql, 1, 8);
	
	RETURN CAST(CAST(@uuid_1c AS uniqueidentifier) AS nvarchar(36));
END;

--DROP FUNCTION iF EXISTS [dbo].[fn_sql_to_1c_uuid];



--DECLARE @result binary(16) =
--SUBSTRING(@uuid_sql, 16, 1)
--+ SUBSTRING(@uuid_sql, 15, 1)
--+ SUBSTRING(@uuid_sql, 14, 1)
--+ SUBSTRING(@uuid_sql, 13, 1)
--+ SUBSTRING(@uuid_sql, 12, 1)
--+ SUBSTRING(@uuid_sql, 11, 1)
--+ SUBSTRING(@uuid_sql, 10, 1)
--+ SUBSTRING(@uuid_sql,  9, 1)
--+ SUBSTRING(@uuid_sql,  1, 8);
DECLARE @counter number = 5

WHILE @counter > 0

  PRINT 'WHILE : ' + @counter

  SET @counter = @counter - 1

END -- WHILE

IF @counter = 0

  THEN PRINT 'END OF SCRIPT'

  ELSE PRINT '??? ERROR ???'

END -- IF
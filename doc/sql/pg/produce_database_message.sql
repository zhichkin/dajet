INSERT INTO _reference38
(_code, _idrref, _marked, _predefinedid,
_fld130, _fld131, _fld132, _fld133, _fld134)
VALUES (@p1, @p2, @p3, @p4,
@p5, CAST(@p6 AS mvarchar), CAST(@p7 AS mvarchar), CAST(@p8 AS mvarchar), CAST(@p9 AS mvarchar));

command.Parameters.AddWithValue("p1", message.Code);
command.Parameters.AddWithValue("p2", message.Uuid.ToByteArray());
command.Parameters.AddWithValue("p3", message.DeletionMark);
command.Parameters.AddWithValue("p4", message.PredefinedID.ToByteArray());
command.Parameters.AddWithValue("p5", message.DateTimeStamp);
command.Parameters.AddWithValue("p6", message.Sender);
command.Parameters.AddWithValue("p7", message.OperationType);
command.Parameters.AddWithValue("p8", message.MessageType);
command.Parameters.AddWithValue("p9", message.MessageBody);
WITH cte AS
(SELECT _code, _idrref FROM _reference39 ORDER BY _code ASC, _idrref ASC LIMIT 10)
DELETE FROM _reference39 t USING cte
WHERE t._code = cte._code AND t._idrref = cte._idrref
RETURNING
t._code AS "Код", t._idrref AS "Ссылка", t._version AS "ВерсияДанных",
t._fld135 AS "ДатаВремя", CAST(t._fld136 AS varchar) AS "Отправитель",
CAST(t._fld137 AS varchar) AS "Получатели", CAST(t._fld138 AS varchar) AS "ТипОперации",
CAST(t._fld139 AS varchar) AS "ТипСообщения", CAST(t._fld140 AS text) AS "ТелоСообщения";

message.Code = reader.IsDBNull(0) ? 0 : (long)reader.GetDecimal(0);
message.Uuid = reader.IsDBNull(1) ? Guid.Empty : new Guid((byte[])reader[1]);
message.Version = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);
message.DateTimeStamp = reader.IsDBNull(3) ? DateTime.MinValue : reader.GetDateTime(3);
message.Sender = reader.IsDBNull(4) ? string.Empty : reader.GetString(4);
message.Recipients = reader.IsDBNull(5) ? string.Empty : reader.GetString(5);
message.OperationType = reader.IsDBNull(6) ? string.Empty : reader.GetString(6);
message.MessageType = reader.IsDBNull(7) ? string.Empty : reader.GetString(7);
message.MessageBody = reader.IsDBNull(8) ? string.Empty : reader.GetString(8);
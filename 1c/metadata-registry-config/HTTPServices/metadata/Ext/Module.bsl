﻿
Функция Registry_POST(Запрос)
	
	Ответ = Новый HTTPСервисОтвет(200);
	
	ТелоЗапроса = Запрос.ПолучитьТелоКакСтроку();
	
	UTF8  = КодировкаТекста.UTF8;
	NoBOM = ИспользованиеByteOrderMark.НеИспользовать;
	
	Ответ.УстановитьТелоИзСтроки("OK", UTF8, NoBOM);
	
	Возврат Ответ;
	
КонецФункции

## Нагрузочное тестирование DaJet web api

Тестирование проводилось на сервере максимально близком по своим характеристикам к серверу, подходящим для промышленной эксплуатации. Для того, чтобы понимать насколько технологии DaJet эффективны с практической точки зрения, был выполнен аналогичной тест для http-сервиса 1C:Предприятия 8. Тест проводился на следующей конфигурации оборудования:
- Сервер приложений (BIG BOSS)
  - физический сервер: Ryzen 9 9950x, RAM 192 Gb DDR-5 3600 MHz, 1xNVME 4Tb Gen 4 + 4xNVME 4Tb Gen 4
  - виртуальная машина: KVM + QEMU, CPU 16 ядер (32 потока), RAM 64 Gb
  - 1C:Предприятие 8.3.27.1606 (клиент-сервер)
  - Apache HTTP Server
  - DaJet 3.11.5 (.NET 8)
  - Kestrel Web Server 1.0.2
  - NBomber 6.0.2 (ПО для нагрузочного тестирования)
- Сервер СУБД (ESB)
  - физический сервер: 2 CPU Intel Xeon Gold 5218R 2.10 GHz, RAM 768 Gb
  - Microsoft SQL Server 2022

<img width="724" height="345" alt="big-boss-esb" src="https://github.com/user-attachments/assets/ec7a9b95-602e-48c3-876a-ff0773ee538c" />

Тестирование проводилось по следующему сценарию: 100 одновременно работающих пользователей пытаются за 10 секунд выполнить как можно большее количество запросов к web api сервису. Всего выполнено 5 тестов:
1. 1С http-сервис возвращает строковое значение "ОК" и не запрашивает данные в СУБД.
2. Аналогичный 1С код на DaJet Script возвращает значение "ОК" и не запрашивает данные в СУБД.
3. DaJet database web api запрашивает один документ по его номеру.
4. 

**Настройки http-сервиса 1С из файла ```vrd```**
```
<service name="test_POST" rootUrl="test_POST" enable="true"
reuseSessions="autouse"
sessionMaxAge="10"
poolSize="300"
poolTimeout="5">
```

**Код HTTP-сервиса 1С:Предприятие**
```
Функция test_POST(Запрос)
   Ответ = Новый HTTPСервисОтвет(200);
   UTF8  = КодировкаТекста.UTF8;
   NoBOM = ИспользованиеByteOrderMark.НеИспользовать;
   Ответ.УстановитьТелоИзСтроки("OK", UTF8, NoBOM);
   Возврат Ответ;
КонецФункции
```



**Скрипт DaJet Script web api, аналогичный коду 1С**
```
RETURN 'OK'
```

**Скрипт DaJet database web api**
```
SELECT Номер
  FROM Документ.Расш1_Документ1
 WHERE Номер = '000000002'
```

**Скрипт DaJet Script web api, используя команду ```REQUEST```**
```
DEFINE record (_Code string)
DECLARE @result object OF record

REQUEST 'mssql://localhost/test?sql'
   WITH Script = 'file://code/mssql.sql' 
   INTO @result

RETURN @result
```

**Код T-SQL для команды ```REQUEST``` из предыдущего скрипта**
```
SELECT _Number FROM test.dbo._Document53X1 WHERE _Number = '000000002'
```

**Скрипт DaJet Script web api, используя команду ```USE```**
```
DECLARE @object object
USE 'mssql://server/database'
   SELECT Ссылка, Код, Наименование
     INTO @object
     FROM Справочник.Номенклатура
    WHERE Код = 'ФР-00000285'
END
RETURN @object
```



## Нагрузочное тестирование DaJet web api

Тестирование проводилось на сервере максимально близком по своим характеристикам к серверу, подходящему для промышленной эксплуатации. Для того, чтобы понимать насколько технологии DaJet эффективны с практической точки зрения, был выполнен аналогичной тест для http-сервиса 1C:Предприятия 8. Тест проводился на следующей конфигурации оборудования:
- Сервер приложений (BIG BOSS)
  - физический сервер: Ryzen 9 9950x, RAM 192 Gb DDR-5 3600 MHz, 1xNVME 4Tb Gen 4 + 4xNVME 4Tb Gen 4
  - виртуальная машина: KVM + QEMU, CPU 16 ядер (32 потока), RAM 64 Gb
  - 1C:Предприятие 8.3.27.1606 (клиент-сервер)
  - Apache HTTP Server (используется 1С)
  - DaJet 3.11.5 (.NET 8)
  - Kestrel Web Server 1.0.2 (используется DaJet)
  - NBomber 6.0.2 (ПО для нагрузочного тестирования)
- Сервер СУБД (ESB)
  - физический сервер: 2 CPU Intel Xeon Gold 5218R 2.10 GHz, RAM 768 Gb
  - Microsoft SQL Server 2022

<img width="724" height="345" alt="big-boss-esb" src="https://github.com/user-attachments/assets/ec7a9b95-602e-48c3-876a-ff0773ee538c" />


**Настройки http-сервиса 1С из файла ```vrd```**
```
<service name="test_POST" rootUrl="test_POST" enable="true"
reuseSessions="autouse"
sessionMaxAge="10"
poolSize="300"
poolTimeout="5">
```

Тестирование проводилось по следующему сценарию: 100 одновременно работающих пользователей пытаются за 10 секунд выполнить как можно большее количество запросов к web api сервису. Все тесты для DaJet выполняют запрос к базе данных для получения значения одного реквизита конкретного документа по его номеру. Тест для http-сервиса 1С:Предприятие 8 очень простой - возвращается строковое значение "ОК" без выполнения запроса к базе данных. Ниже по каждому тесту приводится код скрипта и результат его выполнения.

> Изначально **DaJet Script** не проектировался для использования в подобных сценариях. Тем не менее возможность вызова скриптов по HTTP была заложена и для него тоже с целью развивать эту функциональность в будущем. Таким образом всё-таки было решено включить **DaJet Script web api** в тестирование. В конечном итоге, в сочетании с командой ```REQUEST```, этот тип DaJet web api показал себя вполне достойно.

[Расшифровка отчётов NBomber](https://nbomber.com/docs/reporting/reports/)

### Код HTTP-сервиса 1С:Предприятие
```
Функция test_POST(Запрос)
   Ответ = Новый HTTPСервисОтвет(200);
   UTF8  = КодировкаТекста.UTF8;
   NoBOM = ИспользованиеByteOrderMark.НеИспользовать;
   Ответ.УстановитьТелоИзСтроки("OK", UTF8, NoBOM);
   Возврат Ответ;
КонецФункции
```

<img width="1160" height="502" alt="1-http-load-test-1c-simple-return" src="https://github.com/user-attachments/assets/2b29bce6-ea2e-4fc1-8c7d-7b2e34098d82" />

### Скрипт DaJet Script web api (полный аналог кода 1С)
```
RETURN 'OK'
```

<img width="1160" height="502" alt="1-http-load-test-dajet-script-simple-return" src="https://github.com/user-attachments/assets/89044f94-7a76-42b0-b18d-f7f6845f792e" />

### Скрипт DaJet database web api
```
SELECT Номер
  FROM Документ.Расш1_Документ1
 WHERE Номер = '000000002'
```

<img width="1160" height="502" alt="1-http-load-test-dajet-database-web-api" src="https://github.com/user-attachments/assets/43cfd372-d6c3-4a59-b857-73d625c06cbe" />

### Скрипт DaJet Script web api, используя команду ```REQUEST```
```
DEFINE record (_Code string)
DECLARE @result object OF record

REQUEST 'mssql://localhost/test?sql'
   WITH Script = 'file://code/mssql.sql' 
   INTO @result

RETURN @result
```

### Код T-SQL для команды ```REQUEST``` из предыдущего скрипта
```
SELECT _Number FROM test.dbo._Document53X1 WHERE _Number = '000000002'
```

<img width="1160" height="502" alt="1-http-load-test-dajet-script-request" src="https://github.com/user-attachments/assets/af2f60b8-ef15-46b6-818d-d677fe4a5214" />

### Скрипт DaJet Script web api, используя команду ```USE```
```
DECLARE @result object

USE 'mssql://localhost/test?mdex'
   SELECT Номер
     INTO @result
     FROM Документ.Расш1_Документ1
    WHERE Номер = '000000002'
END

RETURN @result
```

<img width="1160" height="502" alt="1-http-load-test-dajet-script-use" src="https://github.com/user-attachments/assets/a6625758-5bf3-47eb-9925-18d3adfd2c7a" />

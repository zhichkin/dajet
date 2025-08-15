## DaJet Script

[Начало](https://github.com/zhichkin/dajet/tree/main/doc/dajet-script/README.md)

### Реляционные базы данных
- [Общее описание](#общее-описание)
- [SELECT](https://github.com/zhichkin/dajet/tree/main/doc/dajet-script/databases/select/README.md)
- [STREAM](https://github.com/zhichkin/dajet/tree/main/doc/dajet-script/databases/stream/README.md) (потоковый SELECT)
- [CONSUME](https://github.com/zhichkin/dajet/tree/main/doc/dajet-script/databases/consume/README.md) (деструктивный STREAM)
- [Мониторинг таблиц-очередей](https://github.com/zhichkin/dajet/tree/main/doc/dajet-script/databases/queue-monitor/README.md)
- [INSERT](https://github.com/zhichkin/dajet/tree/main/doc/dajet-script/databases/insert/README.md)
- [UPDATE](https://github.com/zhichkin/dajet/tree/main/doc/dajet-script/databases/update/README.md) (обычный и потоковый)
- [DELETE](https://github.com/zhichkin/dajet/tree/main/doc/dajet-script/databases/delete/README.md)
- [REQUEST](https://github.com/zhichkin/dajet/tree/main/doc/dajet-script/databases/request/README.md) (хранимые процедуры)

#### Общее описание

DaJet Script поддерживает работу с базами данных 1С:Предприятие 8 под управлением СУБД Microsoft SQL Server или PostgreSQL. При этом нужно отметить, что имеется ввиду специальная [сборка PostgreSQL для 1С](https://v8.1c.ru/tekhnologii/systemnye-trebovaniya-1s-predpriyatiya-8/subd-postgresql/). Существенным отличием этой сборки PostgreSQL от стандартной является использование 1С строкового пользовательского типа данных ```mchar``` и ```mvarchar```. Возможно имеются и другие отличия, которые для работы DaJet Script не имеют значения.

DaJet Script поддерживает работу со следующими объектами 1С:Предприятие 8:
- Свойства конфигурации
- Общие реквизиты
- Определяемые типы
- [Константы](https://github.com/zhichkin/dajet/tree/main/doc/dajet-script/databases/constants/README.md)
- Планы видов характеристик
- Перечисления
- Справочники
- Документы
- Планы счетов
- Планы обмена (плюс таблицы регистрации изменений)
- Регистры сведений (плюс таблицы настроек и итогов)
- Регистры накопления (плюс таблицы настроек и итогов)
- [Регистры бухгалтерии](https://github.com/zhichkin/dajet/tree/main/doc/dajet-script/databases/accounting-register/README.md) (плюс таблица значений субконто)
- Задачи
- Бизнес-процессы

Язык запросов DaJet Script позволяет обращаться к таблицам 1С:Предприятие 8 в терминах этой платформы. Синтаксис универсализирован, аналогично языку запросов 1С, то есть он не зависит от типа СУБД. Тем не менее, в отличие от языка запросов 1С, DaJet Script дополнительно реализует следующие стандартные возможности SQL:
- коррелированные подзапросы
- общие табличные выражения
- рекурсивные запросы
- оконные (аналитические) функции
- латеральные (боковые) соединения
- DML команды (INSERT, UPDATE, DELETE)
- вызов хранимых процедур базы данных
- дополнительные полезные функции SQL

Кроме этого, DaJet Script дополняет язык запросов своими специфическими командами, реализуя логику потоковой обработки и обмена данными, например, STREAM и CONSUME. Более того, в одном скрипте DaJet Script возможно обращение к нескольким базам данных, в том числе расположенных на разных серверах и находящихся под управлением различных СУБД.

[Наверх](#реляционные-базы-данных)

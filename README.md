## DaJet - платформа для разработки бэкенда и интеграции

### [Документация DaJet](https://zhichkin.github.io/)
### [Школа DaJet на YouTube](https://www.youtube.com/playlist?list=PLyBbhdsc7InutmVxyUszw-ZNJ5zKVBot2)

![dajet-studio](https://github.com/zhichkin/dajet/blob/main/doc/dajet-studio/dajet-architecture.png)

>Название **DaJet** (даджет) образовано от слов **data** (данные) и **jet** (реактивный).
>Кроме этого является модификацией слова gadget (гаджет) и может быть интерпретировано как "инструмент для работы с данными".

Платформа реализована на C# (.NET 7.0) и состоит из **сервера DaJet**, который работает под управлением
[web сервера Kestrel](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/kestrel?view=aspnetcore-7.0)
и встроенного графического интерфейса пользователя - **DaJet Studio** (Blazor Web Assembly).

**Сервер DaJet** реализует web api для доступа к основным библиотекам платформы, а именно:
- **DaJet.Metadata** - описание прикладной модели и её схемы данных
- **DaJet.Scripting** - SQL-подобный язык запросов DaJet (DDL + DML)

**DaJet Studio** реализовано как приложение
[Microsoft Blazor](https://learn.microsoft.com/en-us/ASPNET/core/blazor/?view=aspnetcore-7.0)
Web Assembly (wasm) и интегрированно непосредственно в сервер DaJet.
DaJet Studio является средой разработки хранимых скриптов на SQL-подобном языке **DaJet QL**.

>Вызов скриптов DaJet доступен для внешних систем как прикладной **web api сервера DaJet**.

На текущий момент времени поддерживается работа с СУБД Microsoft SQL Server и PostgreSQL,
а также брокерами сообщений RabbitMQ и Apache Kafka.

Исторически так сложилось, что платформа DaJet частично заимствовала и развивает структуру хранения
пользовательских данных платформы 1С:Предприятие 8, а также её язык запросов. Таким образом,
это обеспечивает некоторую совместимость DaJet и 1С.

[Канал DaJet в Телеграм](https://t.me/dajet_studio)

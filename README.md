# DaJet - платформа для разработки бэкенда и интеграции

[Документация DaJet](https://zhichkin.github.io/)

![dajet-studio](https://github.com/zhichkin/dajet/blob/main/doc/dajet-studio/dajet-studio.png)

Платформа состоит из сервера DaJet, который работает под управлением
[web сервера Kestrel](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/kestrel?view=aspnetcore-7.0)
и графического интерфейса пользователя DaJet Studio.

**Сервер DaJet** реализует Web Api для доступа к основным библиотекам платформы, а именно:
- DaJet.Metadata
- DaJet.Scripting

**DaJet Studio** реализовано как приложение
[Microsoft Blazor](https://learn.microsoft.com/en-us/ASPNET/core/blazor/?view=aspnetcore-7.0)
Web Assembly (wasm) и интегрированно непосредственно в сервер DaJet.
DaJet Studio является средой разработки хранимых скриптов на SQL-подобном языке **DaJet QL** (DJQL).
Вызов этих скриптов доступен для внешних систем как web api сервера DaJet.

На текущий момент времени поддерживается работа с СУБД Microsoft SQL Server и PostgreSQL,
а также брокерами сообщений RabbitMQ и Apache Kafka.

Исторически так сложилось, что платформа DaJet частично заимствовала и развивает структуру хранения
пользовательских данных платформы 1С:Предприятие 8, а также её язык запросов. Таким образом,
это обеспечивает некоторую совместимость DaJet и 1С.

[Канал DaJet в Телеграм](https://t.me/dajet_studio_group)
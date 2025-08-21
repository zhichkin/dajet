# DaJet - язык интеграции

[Документация](https://zhichkin.github.io/)

DaJet Script – это расширяемый SQL-подобный язык программирования для организации обмена данными между узлами интеграции. Узлами интеграции в контексте платформы DaJet называются базы данных Sqlite, PostgreSQL или Microsoft SQL Server, в том числе 1С:Предприятие 8, брокеры сообщений RabbitMQ или Apache Kafka, а также Web API сервисы (в том числе сервера DaJet).

Скрипт DaJet формируется из произвольного количества команд, которые используются средой выполнения как блоки для построения конвейера обработки и обмена данными. Имеются команды условного и параллельного выполнения кода, а также вызова других скриптов, что делает DaJet Script полноценным процедурным языком программирования для решения широкого спектра задач.

Функционал DaJet Script расширяется при помощи команд-процессоров, которые разрабатываются на языке C#. Пользовательские команды включаются в скрипты наравне с родными командами DaJet Script.

Хостинг (регулярное) или одноразовое выполнение скриптов может осуществляться при помощи консольной программы [DaJet Script Host](https://zhichkin.github.io/dajet-host), сервера DaJet aka [DaJet Studio](https://zhichkin.github.io/dajet-studio) или программно на языке C#. Любой скрипт может быть вызван как метод [Web API сервера DaJet](https://zhichkin.github.io/dajet-web-api).

Хост DaJet может быть установлен в качестве сервиса Windows или демона Linux (поддерживается systemd).

### Описание проектов репозитория

|Название|Тип|Описание|
|--------|---|--------|
|dajet-file-logger|dll|Файловый лог DaJet|
|dajet-model|dll|Базовые классы и типы DaJet|
|dajet-data|dll|Базовые классы, зависимости и ORM DaJet для работы с разными СУБД|
|dajet-metadata|dll|Провайдер метаданных СУБД, в том числе 1С:Предприятие 8|
|dajet-dbview-generator|dll|Генератор представлений СУБД для 1С:Предприятие 8|
|dajet-scripting|dll|Парсер DaJet Script и транспайлер SQL для Microsoft SQL Server и PostgreSQL|
|dajet|exe|Утилита/служба для выполнения/хостинга скриптов DaJet Script|
|dajet-data-client|dll|ADO .NET провайдер данных для 1С:Предприятие 8|
|dajet-flow|dll|Подсистема конвейерной обработки данных, аналог Kafka Streams|
|dajet-flow-*|dll|Разные плагины для DaJet Flow|
|dajet-http-server|exe|Многофункциональный сервер DaJet|
|dajet-http-client|dll|Обёртка для Web API сервера DaJet|
|dajet-studio|WASM|Web-интерфейс сервера DaJet на Blazor|

### Сборка DaJet из исходников

- [build-and-release-dajet-linux.yml](https://github.com/zhichkin/dajet/blob/main/.github/workflows/build-and-release-dajet-linux.yml)
- [build-and-release-dajet-windows.yml](https://github.com/zhichkin/dajet/blob/main/.github/workflows/build-and-release-dajet-windows.yml)
- [deploy-to-docker-linux.yml](https://github.com/zhichkin/dajet/blob/main/.github/workflows/deploy-to-docker-linux.yml)
- [deploy-to-docker-windows.yml](https://github.com/zhichkin/dajet/blob/main/.github/workflows/deploy-to-docker-windows.yml)

Актуальные образы DaJet Studio для Linux и Windows публикуются на [DockerHub](https://hub.docker.com/r/zhichkin/dajet-studio).

[DaJet в Telegram](https://t.me/dajet_studio)

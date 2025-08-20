# DaJet - язык интеграции

[Документация](https://zhichkin.github.io/)

DaJet Script – это расширяемый SQL-подобный язык программирования для организации обмена данными между узлами интеграции. Узлами интеграции в контексте платформы DaJet называются базы данных Sqlite, PostgreSQL или Microsoft SQL Server, в том числе 1С:Предприятие 8, брокеры сообщений RabbitMQ или Apache Kafka, а также Web API сервисы (в том числе сервера DaJet).

Скрипт DaJet формируется из произвольного количества команд, которые используются средой выполнения как блоки для построения конвейера обработки и обмена данными. Имеются команды условного и параллельного выполнения кода, а также вызова других скриптов, что делает DaJet Script полноценным процедурным языком программирования для решения широкого спектра задач.

Точками расширения DaJet Script являются пользовательские функции и команды-процессоры, которые создаются на языке C# путём реализации соответствующих интерфейсов. Далее они могут быть подключены в среду выполнения и использованы в скриптах наравне с родными командами DaJet Script.

Хостинг (регулярное) или одноразовое выполнение скриптов может осуществляться при помощи консольной программы [DaJet Script Host](https://zhichkin.github.io/dajet-host), сервера DaJet aka [DaJet Studio](https://zhichkin.github.io/dajet-studio) или программно на языке C#. Любой скрипт может быть вызван как метод [Web API сервера DaJet](https://zhichkin.github.io/dajet-web-api).

Хост DaJet может быть установлен в качестве сервиса Windows или демона Linux (поддерживается systemd).

### Сборка DaJet из исходников

- [build-and-release-dajet-linux.yml](https://github.com/zhichkin/dajet/blob/main/.github/workflows/build-and-release-dajet-linux.yml)
- [build-and-release-dajet-windows.yml](https://github.com/zhichkin/dajet/blob/main/.github/workflows/build-and-release-dajet-windows.yml)
- [deploy-to-docker-linux.yml](https://github.com/zhichkin/dajet/blob/main/.github/workflows/deploy-to-docker-linux.yml)
- [deploy-to-docker-windows.yml](https://github.com/zhichkin/dajet/blob/main/.github/workflows/deploy-to-docker-windows.yml)

Актуальные образы DaJet Studio для Linux и Windows публикуются на [DockerHub](https://hub.docker.com/r/zhichkin/dajet-studio).

[DaJet в Telegram](https://t.me/dajet_studio)

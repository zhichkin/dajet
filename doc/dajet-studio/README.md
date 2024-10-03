## DaJet Studio - web сервер и редактор кода

[Репозиторий web сервера](https://github.com/zhichkin/dajet/tree/main/src/dajet-http-server)

[Репозиторий DaJet Studio](https://github.com/zhichkin/dajet/tree/main/src/dajet-studio)

### Краткое описание

Веб-сервер DaJet используется для выполнения скриптов, имеет встроенный web интерфейс для управления сервером и редактирования кода - **DaJet Studio**. Интерфейс разработан по технологии [ASP.NET Core Blazor](https://dotnet.microsoft.com/en-us/apps/aspnet/web-apps/blazor) и расположен в папке ```ui``` корневого каталога установки сервера DaJet.

DaJet Studio позволяет подключать информационные базы 1С:Предприятие 8, просматривать их метаданные и выполнять запросы к прикладным данным на SQL-подобном языке **DaJet Script** ```djs``` в терминах 1С. DaJet Studio является средой разработки на этом языке программирования с интегрированным в неё редактором кода [Monaco Editor](https://github.com/microsoft/monaco-editor).

> Любой скрипт доступен по URL, то есть формирует web api сервера DaJet.

**Схематически архитектура решения выглядит следующим образом:**

![architecture](https://github.com/zhichkin/dajet/blob/main/doc/img/dajet-studio-architecture.png)

### Установка и запуск

1. Установить [Microsoft .NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
2. Скачать [дистрибутив DaJet Studio](https://github.com/zhichkin/dajet/releases/latest)
3. Создать рабочий каталог и распаковать в него дистрибутив, например: ```C:\dajet```
4. Перейти в каталог установки и запустить исполняемый файл ```DaJet.Http.Server.exe```
5. Открыть web интерфейс DaJet Studio в браузере по адресу ```http://localhost:5000```

|**Windows**|**Linux**|
|-----------|---------|
|![run-windows](https://github.com/zhichkin/dajet/blob/main/doc/img/dajet-studio-run-windows.png)|![run-linux](https://github.com/zhichkin/dajet/blob/main/doc/img/dajet-studio-run-linux.png)|

|**Внешний вид web интерфейса DaJet Studio в браузере**|
|------------------------------------------------------|
|![run-windows](https://github.com/zhichkin/dajet/blob/main/doc/img/dajet-studio-web-ui.png)|

### Настройка сервера DaJet

Сервер DaJet реализован на основании web сервера Kestrel. Соответственно для настройки именно web сервера следует использовать [официальную документацию Kestrel](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/servers/kestrel).

Файл настроек сервера **appsettings.json** расположен в корневом каталоге установки DaJet. В свою очередь, специфические настройки DaJet хранятся в базе данных **dajet.db** (формат Sqlite3), которая расположена там же и создаётся автоматически при первом запуске.

**Файл appsettings.json по умолчанию**
```JSON
{
  "HostOptions": {
    "ShutdownTimeout": "00:00:20"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "urls": "http://localhost:5000"
}
```

Сервер DaJet может быть установлен как служба Windows или демон Linux (поддерживается **systemd**).

Пример команды Windows для создания службы (запускается от имени Администратора):
> **sc create** "DaJet Server" binPath="C:\DaJet\DaJet.Http.Server.exe"

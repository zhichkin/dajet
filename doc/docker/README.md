[Начало](https://github.com/zhichkin/dajet/blob/main/README.md)

## DaJet Studio и Docker

Стабильные релизы DaJet Studio размещаются на [Docker Hub](https://hub.docker.com/r/zhichkin/dajet-studio) в виде Docker образов. Эти образы обновляются по мере выхода новых версий DaJet Studio. Существует два образа: для Linux и Windows. В качестве базового образа для Linux используется ```mcr.microsoft.com/dotnet/aspnet:8.0```, а для Windows - ```mcr.microsoft.com/dotnet/aspnet:8.0-nanoserver-1809```.

**Сценарии GitHub Actions для сборки и публикации DaJet Studio на Docker Hub:**
- [deploy-to-docker-linux.yml](https://github.com/zhichkin/dajet/blob/main/.github/workflows/deploy-to-docker-linux.yml)
- [deploy-to-docker-windows.yml](https://github.com/zhichkin/dajet/blob/main/.github/workflows/deploy-to-docker-windows.yml)

Для запуска DaJet Studio в Docker контейнере необходимо установить [Docker](https://docs.docker.com/engine/install/) или [Docker Desktop](https://docs.docker.com/desktop/). Второй вариант предпочтительнее для обычных пользователей, так как предоставляет удобный и интуитивно понятный интерфейс для управления образами и контейнерами. Получение образов DaJet Studio при помощи командной строки:

**Linuх-образ DaJet Studio**
```
docker pull zhichkin/dajet-studio:linux
```

**Windows-образ DaJet Studio**
```
docker pull zhichkin/dajet-studio:windows
```

Выполнение DaJet Studio в контейнере Docker по умолчанию осуществляется от имени пользователя с ограниченными правами. Это означает, что вся файловая система приложения доступна только для чтения и выполнения. Таким образом становится невозможным создание новых скриптов, их редактирование, а также настройка подключений к базам данных. Для того, чтобы решить эту проблему, необходимо запустить контейнер от пользователя, обладающего соответствующими правами.

Рассмотрим пример запуска DaJet Studio в контейнере для Linux. Настроим подключение DaJet Studio к базе данных под управлением MS SQL Server 2022, который работает на локальной машине, но не в контейнере.

Следующая команда запускает контейнер от пользователя ```root```, обладающего правами администратора системы в Linux.
```
docker run --name dajet-studio --user=root -it -p 5000:5000 zhichkin/dajet-studio:linux
```

После выполнения этой команды мы должны увидеть следующий консольный вывод:

<img width="890" height="380" alt="image" src="https://github.com/user-attachments/assets/edfced04-84a8-40e0-b1f7-3d17b38cd0c6" />

При использовании Docker Desktop в списке контейнеров мы можем увидеть, например, следующее: появился новый контейнер с именем ```dajet-studio``` и он работает.

<img width="997" height="354" alt="image" src="https://github.com/user-attachments/assets/30d7e6bf-2711-4d64-9fa2-ec31565c15ac" />

Далее нужно запустить браузер на локальной машине и подключиться к DaJet Studio по адресу ```http://localhost:5000```.

Однако подключения к внешним по отношению к контейнеру базам данных будут недоступны. В данном примере используется локальная машина под управлением Windows 10 Pro. Для открытия выхода из контейнера "наружу" необходимо сделать, во-первых, доступным IP-адрес локальний машины, а также, во-вторых, открыть порт MS SQL Server 1433.

При установке Docker Desktop создаёт сопоставление локального IP-адреса имени ```host.docker.internal```. Это можно увидеть в системном файле Windows ```hosts```.

<img width="762" height="322" alt="image" src="https://github.com/user-attachments/assets/5fdd214d-9fe0-45ee-9779-a2bcd18b64a4" />

Таким образом мы можем отредактировать строку подключения к базе данных следующим образом:
```
Data Source=host.docker.internal;Initial Catalog=my-database;Integrated Security=True;Encrypt=False;
```

Далее нужно настроить правило для входящих соединений на локальной машине, другими словами открыть порт 1433. Это можно легко сделать в панели управления Windows Defender Firewall.

<img width="781" height="472" alt="image" src="https://github.com/user-attachments/assets/39408eeb-d3c2-4deb-a034-fce766588595" />

Можно указать порт SQL Server в строке подключения:
```
Data Source=host.docker.internal, 1433;Initial Catalog=my-database;Integrated Security=True;Encrypt=False;
```

Однако, если после этого попытаться подключиться к базе данных, то у нас ничего не получится. Дело в том, что базовый Linux-образ от Microsoft, который мы используем, не имеет установленного по умолчанию Kerberos, а, следовательно, инструкция ```Integrated Security=True``` в строке подключения выше не будет работать. В частности, мы можем увидеть следующий текст ошибки в консольном выводе.
```
Cannot load library libgssapi_krb5.so.2 
Error: libgssapi_krb5.so.2: cannot open shared object file: No such file or directory
```

В таком случае используем аутентификацию SQL Server, например, таким образом:
```
Data Source=host.docker.internal;Initial Catalog=my-database;User ID=sa;Password=sa;Encrypt=False;
```

Для скриптов DaJet Script аналогичная настройка подключения может выглядеть следующим образом:
```
DECLARE @table array

USE 'mssql://sa:sa@host.docker.internal/my-database'

   SELECT TOP 3 Наименование
     INTO @table
     FROM Справочник.Номенклатура

END

RETURN @table
```

Таким образом мы рассмотрели один из простейших случаев настройки и запуска DaJet Studio в контейнере Docker.

[Наверх](#dajet-studio-и-docker)

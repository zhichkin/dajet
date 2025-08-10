[Начало](https://github.com/zhichkin/dajet/blob/main/README.md)

## DaJet Studio и Docker

Самые свежие и стабильные релизы DaJet Studio размещаются на [Docker Hub](https://hub.docker.com/r/zhichkin/dajet-studio). Существует два образа: для Linux и Windows. В качестве базового образа для Linux используется ```mcr.microsoft.com/dotnet/aspnet:8.0```, а для Windows - ```mcr.microsoft.com/dotnet/aspnet:8.0-nanoserver-1809```.

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

Выполнение DaJet Studio в контейнере Docker по умолчанию осуществляется от имени пользователя с ограниченными правами. Это означает, что вся файловая система приложения доступна только для чтения и выполнения. Таким образом становится невозможным создание новых скриптов, их редактирование, а также настройка подключений к базам данных. Для того, чтобы решить эту проблему, необходимо запустить контейнер от пользователя, обладающего соответствующими правами. Рассмотрим пример запуска DaJet Studio в контейнере для Linux.

Следующая команда запускает контейнер от пользователя ```root```, обладающего правами администратора системы в Linux.
```
docker run --name dajet-studio --user=root -it -p 5000:5000 zhichkin/dajet-studio:linux
```

После выполнения этой команды мы должны увидеть следующий вывод в консоли:

<img width="890" height="380" alt="image" src="https://github.com/user-attachments/assets/edfced04-84a8-40e0-b1f7-3d17b38cd0c6" />


При использовании Docker Desktop в списке контейнеров мы должны увидеть следующее:

<img width="997" height="354" alt="image" src="https://github.com/user-attachments/assets/30d7e6bf-2711-4d64-9fa2-ec31565c15ac" />


[Наверх](#dajet-studio-и-docker)

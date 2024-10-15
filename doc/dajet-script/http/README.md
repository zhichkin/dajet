## DaJet Script

[Начало](https://github.com/zhichkin/dajet/tree/main/doc/dajet-script/README.md)

### Вызов скриптов по http

Скрипты DaJet Script можно вызвать удалённо по протоколу ```http```. Для этого необходимо разместить файлы скриптов в каталоге ```code``` корневого каталога [http-сервера DaJet](https://github.com/zhichkin/dajet/blob/main/doc/dajet-studio/README.md). Например, создадим в каталоге ```code``` вложенный каталог ```api``` и поместим в него файл скрипта ```test.djs```.

> Если в корневом каталоге http-сервера DaJet каталог ```code``` отсутствует, то создайте его вручную.

**Скрипт test.djs**
```SQL
DECLARE @input  string -- Входящий параметр
DECLARE @output object -- Тело http-ответа

SET @output = SELECT say = 'Hello, ' + @input + '!'

RETURN @output
```

Вызвать этот скрипт можно, обратившись к сервису ```dajet/exe``` http-сервера DaJet. Путь к скрипту для этого сервиса будет выглядеть следующим образом: ```/api/test.djs```. Сервис обслуживает каталог ```code``` - указывать его в URL не нужно.

**Пример вызова скрипта по http при помощи программы Postman**
![dajet-script-http-postman](https://github.com/zhichkin/dajet/blob/main/doc/img/dajet-script-http-postman.png)

[Наверх](#вызов-скриптов-по-http)

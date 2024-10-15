## DaJet Script

[Начало](https://github.com/zhichkin/dajet/tree/main/doc/dajet-script/README.md)

### Вызов скриптов по http

Скрипты DaJet Script можно вызвать удалённо по протоколу ```http```. Для этого необходимо разместить файлы скриптов в любой папке корневого каталога http-сервера DaJet. Например, пусть у нас будет такой каталог ```code```, а в нём скрипт ```test.djs```.

**Скрипт test.djs**
```SQL
DECLARE @input  string -- Входящий параметр
DECLARE @output object -- Тело http-ответа

SET @output = SELECT say = 'Hello, ' + @input + '!'

RETURN @output
```

[Наверх](#вызов-скриптов-по-http)

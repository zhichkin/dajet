## DaJet Script

[Начало](https://github.com/zhichkin/dajet/tree/main/doc/dajet-script/README.md)

### Генерация представлений СУБД для 1С:Предприятие 8

- Генерация скрипта ```sql```
- Создание представлений
- Удаление представлений
- Выполнение скрипта при помощи утилиты **dajet**

DaJet Script имеет встроенный процессор [DbViewProcessor](https://github.com/zhichkin/dajet/blob/main/src/dajet-runtime/extensions/DbViewProcessor.cs) для генерации представлений СУБД для 1С:Предприятие 8.

Данный процессор позволяет генерировать файл скрипта ```sql```, а также создавать и удалять представления СУБД: выполнять команды CREATE VIEW или DROP VIEW непосредственно в целевой СУБД.

> Поддерживаются MS SQL Server и PostgreSQL (специальная сборка для 1С).



[Наверх](#генерация-представлений-субд-для-1спредприятие-8)

## DaJet Script

[Начало](https://github.com/zhichkin/dajet/tree/main/doc/dajet-script/README.md)

### Команда PROCESS

DaJet Script реализует механизм расширения своих программных возможностей при помощи команды **PROCESS**. Эта команда позволяет подключать в скрипты плагины, написанные на языке программирования ```C#```.

Синтаксис команды выглядит следующим образом:
```SQL
PROCESS <variables>
   WITH <c-sharp-interface>
   INTO <variable>
[SELECT <options>]
```



[Наверх](#команда-process)

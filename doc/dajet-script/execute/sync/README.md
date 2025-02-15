## DaJet Script

[Команда EXECUTE](https://github.com/zhichkin/dajet/tree/main/doc/dajet-script/execute/README.md)

### Синхронизация потока сообщений

В некоторых случаях может потребоваться обрабатывать сигнал синхронизации, который генерирует [команда **CONSUME**](https://github.com/zhichkin/dajet/blob/main/doc/dajet-script/databases/consume/README.md) перед фиксацией транзакции обработки пакета своих сообщений (записей СУБД). Количество сообщений в этом пакете указывается в предложении **TOP** команды **CONSUME**. Такой сигнал генерируют также команды [**STREAM**](https://github.com/zhichkin/dajet/blob/main/doc/dajet-script/databases/stream/README.md) и [потоковый **UPDATE**](https://github.com/zhichkin/dajet/blob/main/doc/dajet-script/databases/update/README.md#%D0%BF%D0%BE%D1%82%D0%BE%D0%BA%D0%BE%D0%B2%D1%8B%D0%B9-update) после полного своего выполнения, то есть выдачи последнего сообщения.

> Данная версия **SYNC** команды **EXECUTE** может быть полезна, например, в тех случаях, когда нужно выполнить тот или иной алгоритм не для каждого сообщения в потоке, а только один раз для всего пакета.

Для обработки сигнала синхронизации можно использовать опцию **SYNC** команды **EXECUTE**. Таким образом выполнение команды происходит только в случае получения сигнала. Функциональность полностью аналогична поведению синхронной версии команды **EXECUTE**.

```
EXECUTE SYNC 'file://<script>'
[DEFAULT 'file://<default-script>']
[WITH <parameters>]
[INTO <variable>]
```



[Наверх](#синхронизация-потока-сообщений)

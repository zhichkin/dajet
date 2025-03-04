## DaJet Script

[Команда EXECUTE](https://github.com/zhichkin/dajet/tree/main/doc/dajet-script/execute/README.md)

### Асинхронный вызов скриптов
- ["Выстрелил и забыл" (fire and forget)](#выстрелил-и-забыл-fire-and-forget)
- [Ожидание завершения всех потоков](#ожидание-завершения-всех-потоков)
- [Ожидание завершения любого потока](#ожидание-завершения-любого-потока)
- [Возврат значения из фонового задания](#возврат-значения-из-фонового-задания)

Внешний или подключаемый скрипт можно вызвать асинхронно при помощи опций **TASK** или **WORK** команды **EXECUTE**. В том и другом случае скрипт будет выполнен асинхронно в другом потоке операционной системы, а не в текущем потоке вызова. Разница между двумя этими опциями заключается в том, что в первом случае поток выполнения "арендуется" из пула потоков .NET, а во втором - создаётся отдельный поток операционной системы. Опцию **TASK** рекомендуется использовать для коротких небольших заданий, а **WORK** наоборот для долгих, возможно "вечных", процессов.

Полный синтаксис асинхронной команды **EXECUTE** выглядит следующим образом:
```
EXECUTE [{ TASK | WORK }] 'file://<async-script>.djs'
[DEFAULT 'file://<default-script>.djs']
[AS <name>]
[WITH <parameters>]
[INTO <array-of-tasks>]
```

Команда **EXECUTE** создаёт и добавляет в переменную ```array``` объекты ```task```. Такой массив необходимо заранее объявить именно для этих целей в шапке вызывающего родительского скрипта. Ссылка на этот массив указывется в необязательном предложении **INTO** команды **EXECUTE**. Добавление ```task``` в ```array``` выполняется после запуска дочернего потока или помещения задания в очередь на выполнение пулом потоков .NET. Доступ к массиву заданий осуществляется обычным для [типа ```array```](https://github.com/zhichkin/dajet/blob/main/doc/dajet-script/array/README.md) способом. Элементами массива являются объекты, которые имеют следующую структуру:

|Свойство|Тип<br>данных|Описание|
|--------|-------------|--------|
|Id|number|Системный идентификатор потока|
|Name|string|Пользовательское имя задания, которое может быть выражением.<br>Определяется при помощи опции **AS** команды **EXECUTE**.|
|Status|string|Текущее состояние потока|
|Result|любое<br>значение|Результат выполнения задания|
|IsFaulted|boolean|Признак завершения потока из-за ошибки|
|IsCanceled|boolean|Признак завершения потока системой|
|IsCompleted|boolean|Признак завершения выполнения потока|
|IsSucceeded|boolean|Признак завершения потока без ошибок|

**Таблица состояний потока (свойство "Status")**
|Значение|Описание|
|--------|--------|
|Created|Задание создано, но ещё не запланировано к выполнению системой|
|WaitingToRun|Задание запланировано и ожидает начала выполнения|
|Running|Задание выполняется|
|WaitingForChildrenToComplete|Задание выполнено и неявно ожидает завершения, подключенных дочерних задач|
|RanToCompletion|Задание выполнено без ошибок|
|Canceled|Выполнение задания прервано системой по сигналу немедленного завершения|
|Faulted|Задание завершилось из-за необработанной ошибки|

Для синхронизации выполнения, созданных таким образом дочерних потоков, используется команда **WAIT**. Эта команда синхронно ожидает завершения всех или одного любого потока из указанного массива заданий (опции **ALL** и **ANY** соответственно). На время своего ожидания команда **WAIT** блокирует выполнение текущего родительского потока. Опция **ANY** требует обязательного использования передложения **INTO**, где указывается ссылка на переменную, в которую будет возвращено значение завершившегося задания. Такую переменную необходимо объявить заранее именно для этих целей в шапке вызывающего родительского скрипта.

Полный синтаксис команды **WAIT** выглядит следующим образом:
```
WAIT [{ ALL | ANY }] <array-of-tasks> [INTO <result>] [TIMEOUT <seconds>]
```

- **\<array-of-tasks\>** - массив заданий, завершение которых ожидает команда.
- **\<seconds\>** - период ожидания завершения заданий в секундах. По истечению этого периода времени возникает тайм-аут и выполнение команды прекращается. Данная опция необязательна, в таком случае команда может ожидать завершения заданий или задания бесконечно.
- **\<result\>** - результат ожидания выполнения заданий:
  - **ALL** - значение типа ```boolean```:
    - ```TRUE``` - все задания выполнились до завершения тайм-аута.
    - ```FALSE``` - до завершения всех заданий возник тайм-аут.
  - **ANY** - значение типа ```task``` или ```NULL```:
    - ```NULL``` - ни одно задание не завершилось до наступления тайм-аута.
    - ```task``` - задание, которое завершилось до наступления тайм-аута.

Команда **WAIT** получает и помещает результат выполнения каждого дочернего потока в свойство "Result" соответствующего объекта ```task```. Внешний скрипт может возвращать результат своей работы вызывающему родительскому скрипту при помощи команды **RETURN**. Если эта команда не используется, то в свойство "Result" возвращается значение ```NULL```. Кроме этого команда **WAIT** выполняет перехват всех, необработанных дочерними скриптами, ошибок. Описание ошибки сохраняется в том же самом свойстве "Result" как текст (тип данных ```string```). Результат выполнения доступен только после полного завершения работы потока через соответствующий ему объект ```task```.

> Если команда **WAIT** не используется, то выполнение дочерних потоков продолжается даже тогда, когда родительский скрипт уже завершился и его поток уничтожен операционной системой. Таким образом время жизни дочерних потоков совпадает со временем жизни хоста DaJet Script, то есть процессом операционной системы, в котором работает такой хост.

[Наверх](#асинхронный-вызов-скриптов)

#### "Выстрелил и забыл" (fire and forget)

Самый простой способ асинхронного вызова "без обязательств". Основным недостатком такого подхода является невозможность родительскому скрипту узнать об успешности или неуспешности выполнения дочернего скрипта.

```SQL
*******************************
* Дочерние скрипты task-n.djs *
*******************************
PRINT '[TASK N] НАЧАЛО'
SLEEP 3 -- Имитируем работу
PRINT '[TASK N] КОНЕЦ'
```

```SQL
*******************************************
* Родительский скрипт fire-and-forget.djs *
*******************************************
PRINT '[MAIN] НАЧАЛО'
EXECUTE TASK 'file://task-1.djs'
EXECUTE TASK 'file://task-2.djs'
EXECUTE TASK 'file://task-3.djs'
PRINT '[MAIN] КОНЕЦ'
```

**Результат выполнения скрипта fire-and-forget.djs**
```
[2025-02-15 19:37:29] [MAIN] НАЧАЛО
[2025-02-15 19:37:29] [TASK 2] НАЧАЛО
[2025-02-15 19:37:29] [MAIN] КОНЕЦ
[2025-02-15 19:37:29] [TASK 1] НАЧАЛО
[2025-02-15 19:37:29] [TASK 3] НАЧАЛО
[2025-02-15 19:37:32] [TASK 3] КОНЕЦ
[2025-02-15 19:37:32] [TASK 1] КОНЕЦ
[2025-02-15 19:37:32] [TASK 2] КОНЕЦ
```

[Наверх](#асинхронный-вызов-скриптов)

#### Ожидание завершения всех потоков

Дочерние скрипты имеют такой же код как в предыдущем примере ["Выстрелил и забыл"](#выстрелил-и-забыл-fire-and-forget).

```SQL
****************************************
* Родительский скрипт wait-for-all.djs *
****************************************
DECLARE @task object
DECLARE @tasks array

PRINT '[MAIN] НАЧАЛО'
EXECUTE TASK 'file://task-1.djs' INTO @tasks
EXECUTE TASK 'file://task-2.djs' INTO @tasks
EXECUTE TASK 'file://task-3.djs' INTO @tasks

PRINT '[MAIN] WAIT ALL'
WAIT ALL @tasks

FOR @task IN @tasks
   PRINT '***'
   PRINT 'Id          = ' + @task.Id
   PRINT 'Status      = ' + @task.Status
   PRINT 'IsFaulted   = ' + @task.IsFaulted
   PRINT 'IsCanceled  = ' + @task.IsCanceled
   PRINT 'IsCompleted = ' + @task.IsCompleted
   PRINT 'IsSucceeded = ' + @task.IsSucceeded
   PRINT 'Result      = ' + @task.Result
END

PRINT '[MAIN] КОНЕЦ'
```

**Результат выполнения скрипта wait-for-all.djs**
```
[2025-02-15 20:23:07] [MAIN] НАЧАЛО
[2025-02-15 20:23:07] [TASK 1] НАЧАЛО
[2025-02-15 20:23:07] [MAIN] WAIT ALL
[2025-02-15 20:23:07] [TASK 2] НАЧАЛО
[2025-02-15 20:23:07] [TASK 3] НАЧАЛО
[2025-02-15 20:23:10] [TASK 3] КОНЕЦ
[2025-02-15 20:23:10] [TASK 1] КОНЕЦ
[2025-02-15 20:23:10] [TASK 2] КОНЕЦ
[2025-02-15 20:23:10] ***
[2025-02-15 20:23:10] Id          = 1
[2025-02-15 20:23:10] Status      = RanToCompletion
[2025-02-15 20:23:10] IsFaulted   = false
[2025-02-15 20:23:10] IsCanceled  = false
[2025-02-15 20:23:10] IsCompleted = true
[2025-02-15 20:23:10] IsSucceeded = true
[2025-02-15 20:23:10] Result      = null
[2025-02-15 20:23:10] ***
[2025-02-15 20:23:10] Id          = 2
[2025-02-15 20:23:10] Status      = RanToCompletion
[2025-02-15 20:23:10] IsFaulted   = false
[2025-02-15 20:23:10] IsCanceled  = false
[2025-02-15 20:23:10] IsCompleted = true
[2025-02-15 20:23:10] IsSucceeded = true
[2025-02-15 20:23:10] Result      = null
[2025-02-15 20:23:10] ***
[2025-02-15 20:23:10] Id          = 3
[2025-02-15 20:23:10] Status      = RanToCompletion
[2025-02-15 20:23:10] IsFaulted   = false
[2025-02-15 20:23:10] IsCanceled  = false
[2025-02-15 20:23:10] IsCompleted = true
[2025-02-15 20:23:10] IsSucceeded = true
[2025-02-15 20:23:10] Result      = null
[2025-02-15 20:23:10] [MAIN] КОНЕЦ
```

[Наверх](#асинхронный-вызов-скриптов)

#### Ожидание завершения любого потока

Дочерние скрипты имеют такой же код как в предыдущем примере ["Выстрелил и забыл"](#выстрелил-и-забыл-fire-and-forget). Результат выполнения скрипта ```wait-for-any.djs``` будет практически идентичен результату выполнения скрипта из предыдущего примера ["Ожидание завершения всех потоков"](#ожидание-завершения-всех-потоков).

```SQL
****************************************
* Родительский скрипт wait-for-any.djs *
****************************************
DECLARE @task object
DECLARE @tasks array

PRINT '[MAIN] НАЧАЛО'
EXECUTE TASK 'file://task-1.djs' INTO @tasks
EXECUTE TASK 'file://task-2.djs' INTO @tasks
EXECUTE TASK 'file://task-3.djs' INTO @tasks

PRINT '[MAIN] WAIT ANY'

WHILE ARRAY_COUNT(@tasks) > 0

   WAIT ANY @tasks INTO @task

   PRINT '***'
   PRINT 'Id          = ' + @task.Id
   PRINT 'Status      = ' + @task.Status
   PRINT 'IsFaulted   = ' + @task.IsFaulted
   PRINT 'IsCanceled  = ' + @task.IsCanceled
   PRINT 'IsCompleted = ' + @task.IsCompleted
   PRINT 'IsSucceeded = ' + @task.IsSucceeded
   PRINT 'Result      = ' + @task.Result
END

PRINT '[MAIN] КОНЕЦ'
```

[Наверх](#асинхронный-вызов-скриптов)

#### Возврат значения из фонового задания

Обращаем ваше внимание на то, что дочерний скрипт ```task-2.djs``` завершается с ошибкой и возвращает вызывающему потоку исключение.

```SQL
******************************
* Дочерний скрипт task-1.djs *
******************************
PRINT '[TASK 1] НАЧАЛО'
SLEEP 3 -- Имитируем работу
PRINT '[TASK 1] КОНЕЦ'
-- Возвращаем результат
-- вызывающему потоку
RETURN '[TASK 1] RETURN'
```

```SQL
******************************
* Дочерний скрипт task-2.djs *
******************************
PRINT '[TASK 2] НАЧАЛО'
SLEEP 3 -- Имитируем работу
PRINT '[TASK 2] КОНЕЦ'
-- Возвращаем ошибку
-- вызывающему потоку
THROW '[TASK 2] ERROR'
```

```SQL
******************************
* Дочерний скрипт task-3.djs *
******************************
PRINT '[TASK 3] НАЧАЛО'
SLEEP 3 -- Имитируем работу
PRINT '[TASK 3] КОНЕЦ'
-- Возвращаем результат
-- вызывающему потоку
RETURN '[TASK 3] RETURN'
```

```SQL
*************************************************
* Родительский скрипт return-value-or-error.djs *
*************************************************
DECLARE @task object
DECLARE @tasks array

PRINT '[MAIN] НАЧАЛО'
EXECUTE TASK 'file://code/task/task-1.djs' INTO @tasks
EXECUTE TASK 'file://code/task/task-2.djs' INTO @tasks
EXECUTE TASK 'file://code/task/task-3.djs' INTO @tasks

PRINT '[MAIN] WAIT ALL'
WAIT ALL @tasks

FOR @task IN @tasks
   PRINT '***'
   PRINT 'Id          = ' + @task.Id
   PRINT 'Status      = ' + @task.Status
   PRINT 'IsFaulted   = ' + @task.IsFaulted
   PRINT 'IsCanceled  = ' + @task.IsCanceled
   PRINT 'IsCompleted = ' + @task.IsCompleted
   PRINT 'IsSucceeded = ' + @task.IsSucceeded
   PRINT 'Result      = ' + @task.Result
END
PRINT '[MAIN] КОНЕЦ'
```

**Результат выполнения скрипта return-value-or-error.djs**
```
[2025-02-15 20:29:05] [MAIN] НАЧАЛО
[2025-02-15 20:29:05] [TASK 1] НАЧАЛО
[2025-02-15 20:29:05] [TASK 2] НАЧАЛО
[2025-02-15 20:29:05] [MAIN] WAIT ALL
[2025-02-15 20:29:05] [TASK 3] НАЧАЛО
[2025-02-15 20:29:08] [TASK 2] КОНЕЦ
[2025-02-15 20:29:08] [TASK 1] КОНЕЦ
[2025-02-15 20:29:08] [TASK 3] КОНЕЦ
[2025-02-15 20:29:08] ***
[2025-02-15 20:29:08] Id          = 10
[2025-02-15 20:29:08] Status      = RanToCompletion
[2025-02-15 20:29:08] IsFaulted   = false
[2025-02-15 20:29:08] IsCanceled  = false
[2025-02-15 20:29:08] IsCompleted = true
[2025-02-15 20:29:08] IsSucceeded = true
[2025-02-15 20:29:08] Result      = [TASK 1] RETURN
[2025-02-15 20:29:08] ***
[2025-02-15 20:29:08] Id          = 11
[2025-02-15 20:29:08] Status      = Faulted
[2025-02-15 20:29:08] IsFaulted   = true
[2025-02-15 20:29:08] IsCanceled  = false
[2025-02-15 20:29:08] IsCompleted = true
[2025-02-15 20:29:08] IsSucceeded = false
[2025-02-15 20:29:08] Result      = One or more errors occurred. ([TASK 2] ERROR)
[2025-02-15 20:29:08] ***
[2025-02-15 20:29:08] Id          = 12
[2025-02-15 20:29:08] Status      = RanToCompletion
[2025-02-15 20:29:08] IsFaulted   = false
[2025-02-15 20:29:08] IsCanceled  = false
[2025-02-15 20:29:08] IsCompleted = true
[2025-02-15 20:29:08] IsSucceeded = true
[2025-02-15 20:29:08] Result      = [TASK 3] RETURN
[2025-02-15 20:29:08] [MAIN] КОНЕЦ
```

[Наверх](#асинхронный-вызов-скриптов)

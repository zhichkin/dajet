## DaJet Script

[Начало](https://github.com/zhichkin/dajet/tree/main/doc/dajet-script/README.md)

### Параллельное выполнение кода

Параллельное выполнение кода реализуется DaJet Script при помощи команды **FOR** со специальным ключевым словом **MAXDOP**, после которого указывается значение параллелизма: число или ключевое слово **UNBOUNDED** (без ограничений). Таким образом, весь блок команд команды **FOR** рассматривается как обособленный поток выполнения операционной системы.

```SQL
FOR <object> IN <array> MAXDOP { <degree-of-parallelism> | UNBOUNDED }
   <statements>
END
```
**\<array\>** - переменная типа ```array``` - массив элементов типа ```object```.<br>
**\<object\>** - переменная типа ```object``` - элемент массива \<array\>.<br>
**\<statements\>** - блок команд DaJet Script, который выполняется для каждого значения \<object\> в отдельном потоке операционной системы.

**\<degree-of-parallelism\>** - (целое число) максимально допустимое количество потоков операционной системы, одновременно используемых для обработки заданий \<object\>. Фактическое количество потоков может быть меньше, но не должно превышать значение равное \<degree-of-parallelism\>. Количество потоков и своевременная загрузка освободившихся потоков новыми заданиями контроллируется средой выполнения DaJet Script. В случае, если, указанное в опции **MAXDOP** значение \<degree-of-parallelism\> превышает фактическое количество ядер ЦПУ, установленных в системе, среда выполнения DaJet Script автоматически установит его равным фактическому количеству ядер.

**UNBOUNDED** - опция, которая позволяет игнорировать количество ядер ЦПУ, установленных в системе. Будет создано такое количество потоков операционной системы, которое равно количеству \<object\> указанного \<array\>.

Таким образом, прежде, чем начать параллельное выполнение кода, необходимо сформировать нужное количество заданий \<object\>, поместив их в массив \<array\>. Нужно отметить, что изначально параллельное выполнение кода DaJet Script проектировалось для обработки наборов заданий (записей таблиц или сообщений обмена данными), сформированных запросом к базе данных.

Важно также отметить, что значения всех переменных, на которые ссылаются команды \<statements\> будут переданы в поток выполнения по значению, то есть их текущие значения будут скопированы, а все обращения к ним изолированы в области видимости блока команды **FOR** соответствующего потока.

> Важно! Один поток - одна обособленная область видимости переменных.

> Таким образом изменение соответствующих переменных из родительской области видимости невозможно!

**Параллельное выполнение с ограничением количества потоков**

В данном примере обратите внимание, что задания 1 и 2 выполнились одновременно. При этом задание 2 выполнилось раньше, чем задание 1, что говорит о том, что параллельное выполнение кода не даёт каких-либо гарантий относительно последовательности выполнения заданий.

```SQL
DECLARE @task object -- Объект одного задания
DECLARE @tasks array -- Массив всех заданий

PRINT 'START'

-- Формируем массив заданий
SET @tasks = JSON('[
   { "ИмяЗадания": "Задание 1" },
   { "ИмяЗадания": "Задание 2" },
   { "ИмяЗадания": "Задание 3" }
]')

-- Выполняем параллельную обработку заданий
-- Максимально допустимое количество
-- одновременно работающих потоков = 2

FOR @task IN @tasks MAXDOP 2
   
   SLEEP 1 -- Имитируем работу: "спим" 1 секунду
   
   PRINT @task.ИмяЗадания -- Логируем выполнение

END

PRINT 'FINISH'

-- Результат выполнения скрипта
[2024-10-20 20:55:08] START
[2024-10-20 20:55:09] Задание 2
[2024-10-20 20:55:09] Задание 1
[2024-10-20 20:55:10] Задание 3
[2024-10-20 20:55:10] FINISH
```

**Параллельное выполнение без ограничения количества потоков**

В данном примере обратите внимание, что все задания 1, 2 и 3 выполнились одновременно, в отличие от примера с ограничением максимально допустимого количества потоков. При этом порядок выполнения заданий произвольный, что опять же говорит о том, что параллельное выполнение кода не даёт каких-либо гарантий относительно последовательности выполнения заданий.

Важно так же отметить, в данном примере это видно очень наглядно, что блок параллельного выполнения **FOR** не ожидает завершения работы своих потоков - сообщение ```FINISH``` появилось раньше, чем закончилось выполнение заданий 1, 2 и 3.

> **На заметку:** в будущем планируется добавить команде **FOR** возможность ожидать завершения одного (любого) или всех потоков выполнения при помощи специальных опций. Например, ```[WAIT { ANY | ALL }]```.

```SQL
DECLARE @task object
DECLARE @tasks array

PRINT 'START'

-- Формируем массив заданий
SET @tasks = JSON('[
   { "ИмяЗадания": "Задание 1" },
   { "ИмяЗадания": "Задание 2" },
   { "ИмяЗадания": "Задание 3" }
]')

-- Выполняем параллельную обработку заданий
-- Максимально допустимое количество
-- одновременно работающих потоков неограничено!

FOR @task IN @tasks MAXDOP UNBOUNDED
   
   SLEEP 1 -- Имитируем работу: "спим" 1 секунду
   
   PRINT @task.ИмяЗадания -- Логируем выполнение

END

PRINT 'FINISH'

-- Результат выполнения скрипта
[2024-10-20 21:05:48] START
[2024-10-20 21:05:48] FINISH
[2024-10-20 21:05:49] Задание 2
[2024-10-20 21:05:49] Задание 3
[2024-10-20 21:05:49] Задание 1
```

[Наверх](#параллельное-выполнение-кода)

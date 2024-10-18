## DaJet Script

[Начало](https://github.com/zhichkin/dajet/tree/main/doc/dajet-script/README.md)

### Алгоритмические возможности

- Условное выполнение кода
  - [IF](#команда-if)
  - [CASE](#команда-case)
- Циклическое выполнение кода
  - [WHILE](#команда-while)
  - [FOR](#команда-for)
- Структурированная обработка ошибок
  - [TRY](#команда-try)
  - [THROW](#команда-throw)
  - [Функция ERROR_MESSAGE](#функция-error-message)
- [Приостановка выполнения кода (пауза)](#команда-sleep)
- Параллельное выполнение кода
  - FOR \<object\> IN \<array\> ... MAXDOP \<degree-of-parallelism\> END

Все команды, представленные в этом разделе, являются блочными. Это означает, что все они имеют обособленные блоки (области видимости объявления переменных), состоящие из одной или более команд DaJet Script.

#### Команда IF

```SQL
IF <condition> THEN <then_statements> [ELSE <else_statements> END
```

**\<condition\>** - выражение, которое возвращает значение типа ```boolean```.<br>
**\<then_statements\>** - блок команд DaJet Script, который выполняется если выражение **\<condition\>** вернуло значение ```true```.<br>
**\<else_statements\>** - (опционально) блок команд DaJet Script, который выполняется если выражение **\<condition\>** вернуло значение ```false```.

```SQL
DECLARE @value number = 1

IF @value = 1
   THEN PRINT '(@value = 1) = true'
   ELSE PRINT '(@value = 1) = false'
END

--Результат выполнения скрипта
[2024-10-12 21:24:21] (@value = 1) = true
```

[Наверх](#алгоритмические-возможности)

#### Команда CASE

```SQL
CASE
  WHEN <condition> THEN <then_statements> [...n]
 [ELSE <else_statements>]
END
```

**\<condition\>** - выражение, которое возвращает значение типа ```boolean```.<br>
**\<then_statements\>** - блок команд DaJet Script, который выполняется если выражение **\<condition\>** вернуло значение ```true```.<br>
**\<else_statements\>** - (опционально) блок команд DaJet Script, который выполняется если ни одно выражение **\<condition\>** не вернуло значение ```true```.

> Предложение **WHEN** может повторяться неограниченное количество раз.

```SQL
DECLARE @value number = 0

CASE WHEN @value = 1 THEN PRINT '@value = 1'
     WHEN @value = 2 THEN PRINT '@value = 2'
     WHEN @value = 3 THEN PRINT '@value = 3'
     ELSE PRINT 'Нет совпадений'
END

--Результат выполнения скрипта
[2024-10-12 21:24:21] Нет совпадений
```

[Наверх](#алгоритмические-возможности)

#### Команда WHILE

```SQL
WHILE <condition>
   <statements>
END
```

**\<condition\>** - выражение, которое возвращает значение типа ```boolean```.<br>
**\<statements\>** - блок команд DaJet Script, который выполняется пока выражение **\<condition\>** возвращает значение ```true```.

```SQL
DECLARE @counter number = 0

WHILE @counter < 3
   PRINT '@counter = ' + @counter
   SET @counter = @counter + 1
END

PRINT '@counter = ' + @counter

--Результат выполнения скрипта
[2024-10-12 21:31:55] @counter = 0
[2024-10-12 21:31:55] @counter = 1
[2024-10-12 21:31:55] @counter = 2
[2024-10-12 21:31:55] @counter = 3
```

[Наверх](#алгоритмические-возможности)

#### Команда FOR

Команда **FOR** выполняет блок команд DaJet Script для каждого элемента ```object``` из массива ```array```.

```SQL
FOR <object> IN <array>
   <statements>
END
```
**\<array\>** - переменная типа ```array``` - массив элементов типа ```object```.<br>
**\<object\>** - переменная типа ```object``` - элемент массива \<array\>.<br>
**\<statements\>** - блок команд DaJet Script, который выполняется для каждого значения \<object\>.

```SQL
DECLARE @array  array
DECLARE @object object

USE 'mssql://server/database'
   SELECT TOP 3
          Наименование
     INTO @array
     FROM Справочник.Номенклатура
    ORDER BY Наименование ASC
END

FOR @object IN @array
   PRINT @object.Наименование
END

-- Результат выполнения скрипта
[2024-10-12 21:46:31] Товар MS-001
[2024-10-12 21:46:31] Товар MS-002
[2024-10-12 21:46:31] Товар MS-003
```

[Наверх](#алгоритмические-возможности)

#### Команда TRY

```SQL
TRY
   <try_statements>
CATCH
   <catch_statements>
FINALLY
   <finally_statements>
END
```
**\<try_statements\>** - блок команд, который может вызвать возникновение исключительной ситуации (ошибки).<br>
**\<catch_statements\>** - блок команд, который выполняется только в случае возникновения ошибки в блоке **TRY**.<br>
**\<finally_statements\>** - блок команд, который выполняется всегда и только один раз сразу же после выполнения блока **TRY** или **CATCH**.

> Для получения описания ошибки следует использовать функцию ```ERROR_MESSAGE```, которая возвращает строковое значение. Использование этой функции имеет смысл только в блоке **CATCH**. Использование этой функции в любом другом месте скрипта возвращает пустую строку.

Обязательно должен использоваться, либо блок **CATCH**, либо блок **FINALLY**, либо оба вместе. Это значит, что допустимы следующие виды использования команды структурированной обработки ошибок:

1. TRY \<statements\> CATCH \<statements\> END
2. TRY \<statements\> FINALLY \<statements\> END
3. TRY \<statements\> CATCH \<statements\> FINALLY \<statements\> END

```SQL
DECLARE @value number

TRY
   PRINT 'try block'
   SET @value = 10 / 0
CATCH
   PRINT 'catch block'
   PRINT 'error: ' + ERROR_MESSAGE()
FINALLY
   PRINT 'finally block'
END

-- Результат выполнения скрипта
[2024-10-12 22:08:04] try block
[2024-10-12 22:08:04] catch block
[2024-10-12 22:08:04] error: [SET] failed to evaluate initializer [Variable: @value]
[2024-10-12 22:08:04] finally block
```

#### Команда THROW

Команда **THROW** используется для генерации исключительной ситуации (ошибки) в любом месте скрипта DaJet Script. В качестве обязательного параметра этой команды используется строковое значение или выражение, которое является описанием ошибки.

```SQL
THROW <error_message>
```
**\<error_message\>** - строковое значение или выражение, описание ошибки.

```SQL
TRY
   PRINT 'try block'
   THROW 'Всё пошло не так!'
CATCH
   PRINT 'catch block'
   PRINT 'ERROR: ' + ERROR_MESSAGE()
FINALLY
   PRINT 'finally block'
END

-- Результат выполнения скрипта
[2024-10-12 22:13:59] try block
[2024-10-12 22:13:59] catch block
[2024-10-12 22:13:59] ERROR: Всё пошло не так!
[2024-10-12 22:13:59] finally block
```

[Наверх](#алгоритмические-возможности)

#### Функция ERROR_MESSAGE

Использование функции ```ERROR_MESSAGE()``` описано выше.

[Наверх](#алгоритмические-возможности)

#### Команда SLEEP

```SQL
SLEEP <seconds>
```
**\<seconds\>** - период "сна" текущего потока выполнения в секундах.

```SQL
PRINT 'Начало "сна"'
SLEEP 5
PRINT 'Конец  "сна"'

-- Результат выполнения скрипта
[2024-10-12 22:22:12] Начало "сна"
[2024-10-12 22:22:17] Конец  "сна"
```

[Наверх](#алгоритмические-возможности)
## DaJet Script

[Начало](https://github.com/zhichkin/dajet/tree/main/doc/dajet-script/databases/README.md)

### Команда SELECT

```
SELECT [DISTINCT] [TOP (<top_expression>)]
       { <column_alias> = <column_expression>
       | <column_expression> AS <column_alias>
       } [,...n]
  INTO {<object>|<array>}
  FROM {<table_identifier>|<table_expression>}

[[{LEFT|RIGHT|INNER|FULL}] JOIN {<table_identifier>|<table_expression>}]

[[{CROSS|OUTER}]APPLY <subquery>]

[WHERE]
[GROUP BY]
[HAVING]
[UNION [ALL]]
[ORDER BY <order_by_expression> [ASC|DESC] [,...n][<offset_fetch>]]

<offset_fetch> = OFFSET { integer_constant | offset_row_count_expression } { ROW | ROWS }
[FETCH { FIRST | NEXT } { integer_constant | fetch_row_count_expression } { ROW | ROWS } ONLY]
```

[Наверх](#команда-select)

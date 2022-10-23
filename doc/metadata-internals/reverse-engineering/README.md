## Методика исследования файлов метаданных 1С:Предприятие 8

[Подробнее про файлы описания метаданных 1С](https://github.com/zhichkin/dajet/blob/main/doc/metadata-internals/format-description/README.md)

### 1. Устанавливаем [**GitHub Desktop**](https://desktop.github.com)

### 2. Создаём репозиторий git для выгрузки файлов описания метаданных 1С.

![github_desktop_1](https://github.com/zhichkin/dajet/blob/main/doc/metadata-internals/images/github_desktop_1.png)

![github_desktop_2](https://github.com/zhichkin/dajet/blob/main/doc/metadata-internals/images/github_desktop_2.png)

### 3. Выполняем выгрузку файла объекта метаданных 1С в каталог репозитория, в примере выше 1c-dumps.

Выгрузку можно выполнить при помощи аналогичного кода на C#:

```C#
public void WriteRootConfigToFile()
{
    Guid root;
            
    using (ConfigFileReader reader = new ConfigFileReader(
        DatabaseProvider.SQLServer, MS_CONNECTION_STRING, ConfigTableNames.Config, "root"))
    {
        root = new RootFileParser().Parse(in reader);
    }

    ConfigObject config;

    using (ConfigFileReader reader = new ConfigFileReader(
        DatabaseProvider.SQLServer, MS_CONNECTION_STRING, ConfigTableNames.Config, root))
    {
        config = new ConfigFileParser().Parse(in reader);
    }

    new ConfigFileWriter().Write(config, "C:\\temp\\1c-dumps\\root-config.txt");
}
```

### 4. Делаем коммит в основную ветку репозитория.

![github_desktop_3](https://github.com/zhichkin/dajet/blob/main/doc/metadata-internals/images/github_desktop_3.png)

### 5. Теперь у нас есть эталонный файл, с которым будем сравнивать все последующие изменения конфигурации 1С.

![github_desktop_4](https://github.com/zhichkin/dajet/blob/main/doc/metadata-internals/images/github_desktop_4.png)

![github_desktop_5](https://github.com/zhichkin/dajet/blob/main/doc/metadata-internals/images/github_desktop_5.png)

### 6. Меняем, например, свойство "Иерархический справочник" объекта метаданных, в данном случае справочника  "ПростойСправочник".

![github_desktop_6](https://github.com/zhichkin/dajet/blob/main/doc/metadata-internals/images/github_desktop_6.png)

### 7. Сохраняем конфигурацию 1С.

![github_desktop_7](https://github.com/zhichkin/dajet/blob/main/doc/metadata-internals/images/github_desktop_7.png)

### 8. Повторяем пункт 3 ещё раз, чтобы выгрузить изменения конфигурации 1С (перезаписываем файл).

### 9. Смотрим что изменилось в GitHub Desktop.

![github_desktop_8](https://github.com/zhichkin/dajet/blob/main/doc/metadata-internals/images/github_desktop_8.png)
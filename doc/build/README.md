## Сборка DaJet Studio из исходников

Данная статья описывает процесс поэтапной сборки **http-сервера DaJet** из исходников. Второе, упрощённое, название - **DaJet Studio**. Сборка продукта выполняется вручную из Microsoft Visual Studio 2022 Community Edition. Правильнее наверное в данном случае говорить не "сборка", а "публикация".

#### 1. Сборка проекта ```dajet-http-server```

Собираем проект ```dajet-http-server``` - это будет целевой каталог сборки **DaJet Studio**. В случае успешного выполнения этого шага в целевом каталоге мы должны увидеть следующее:

<img width="477" height="376" alt="00-dajet-build-from-source-http-server" src="https://github.com/user-attachments/assets/fe08f3b9-4194-4dfb-9245-6a59b848f3d2" />

#### 2. Сборка проекта ```dajet-studio```

Собираем проект ```dajet-studio``` - это пользовательский web-интерфейс, который встраивается в **http-сервер DaJet**. По итогу должны получить следующие артефакты.

<img width="602" height="151" alt="01-dajet-build-from-source-dajet-studio" src="https://github.com/user-attachments/assets/c8520324-aa43-4e2c-a97d-404d23e7534f" />

На данном шаге нас интересуют только файлы, которые расположены в каталоге ```wwwroot```.

<img width="525" height="253" alt="03-dajet-build-from-source-dajet-studio-ui" src="https://github.com/user-attachments/assets/51c89eca-29e2-4c1b-a9f8-2fdbf2527b34" />

#### 3. Копирование файлов ```dajet-studio``` в целевой каталог сборки

Копируем файлы web-интерфейса в целевой каталог сборки, подкаталог ```ui```. Конечный результат должен выглядеть следующим образом:

<img width="521" height="274" alt="04-dajet-build-from-source-http-server-copy-web-ui" src="https://github.com/user-attachments/assets/36da5e76-7538-49ff-9521-38c7d8c0e598" />

#### 4. Сборка проекта ```dajet-flow-script``` (необязательно)

Следующий шаг - сборка проекта ```dajet-flow-script```. Этот шаг необязательный, но очень желательный. **DaJet.Flow.Script** это плагин для подсистемы **DaJet Flow**, который позволяет удобно запускать скрипты **DaJet Script** на сервере DaJet в качестве своеобразных служб. Подробнее про подсистему DaJet Flow можно узнать из соответствующих видео.

<img width="546" height="339" alt="05-dajet-build-from-source-dajet-flow-script-runner" src="https://github.com/user-attachments/assets/7667bb15-f4ae-449e-94df-735abbbb08d0" />

В данном случае нас интересуют два файла: ```DaJet.Flow.Script.dll``` и ```DaJet.Flow.Script.pdb```. Можно обойтись и одним файлом ```dll```.

#### 5. Копирование файлов ```dajet-flow-script``` в целевой каталог сборки (необязательно)

Копируем файлы плагина в целевой каталог сборки, подкаталог ```flow```. Этот каталог используется сервером DaJet для загрузки плагинов для подсистемы **DaJet Flow**.

<img width="540" height="129" alt="06-dajet-build-from-source-http-server-copy-dajet-flow-script-runner" src="https://github.com/user-attachments/assets/e12b6680-c9cf-4c24-a8d9-b32c00179095" />

#### 6. Проверяем успешность сборки http-сервера DaJet

Из целевого каталога сборки запускаем исполняемый файл ```DaJet.Http.Server``` сервера DaJet. Если делать это из консольного приложения, то мы должны увидеть следующее:

<img width="560" height="276" alt="07-dajet-build-from-source-http-server-successful-run" src="https://github.com/user-attachments/assets/64cee241-f666-4dc4-9870-6b1ccaab5d19" />

#### 7. Проверяем успешность сборки DaJet Studio

Открываем браузер и переходим по адресу ```http://localhost:5000```. Это URL сервера DaJet, используемый по умолчанию. Он настраивается в файле ```appsettings.json```, расположенном в корне целевого каталога сборки.

<img width="623" height="352" alt="08-dajet-build-from-source-dajet-studio-successful-run" src="https://github.com/user-attachments/assets/984a313a-5e3e-4e59-921c-45d04977cf55" />

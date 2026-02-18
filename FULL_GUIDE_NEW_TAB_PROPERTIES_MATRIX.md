# Полный пошаговый гайд: как вручную создать вкладку «Матрица свойств» в `frmSettings`

Этот документ описывает **все действия по шагам**, без сокращённых списков и без пропусков.

---

## Шаг 1. Открыть редактор VBA и форму

1. Откройте SolidWorks.
2. Откройте ваш макрос VBA.
3. Нажмите `Alt + F11`, чтобы открыть редактор Visual Basic for Applications.
4. В дереве проекта найдите форму `frmSettings`.
5. Откройте форму двойным щелчком.
6. Если окно свойств не видно, нажмите `F4`.
7. Если панель элементов Toolbox не видна, в меню выберите `View` → `Toolbox`.

---

## Шаг 2. Проверить, что на форме есть `MultiPage` с именем `mpMain`

1. На самой форме выделите элемент `MultiPage`.
2. В окне свойств убедитесь, что свойство `Name` равно `mpMain`.
3. Если `Name` отличается (например, `MultiPage1`), переименуйте его строго в `mpMain`.

---

## Шаг 3. Добавить новую страницу в `MultiPage`

1. Выделите `mpMain`.
2. Нажмите правой кнопкой по `mpMain` и выберите `New Page` (или через контекстное меню `Insert Page`, в зависимости от версии).
3. Выделите добавленную страницу.
4. В окне свойств задайте:
   - `Name = pgPropsMatrix`
   - `Caption = Матрица свойств`

---

## Шаг 4. Добавить заголовки блоков на странице `pgPropsMatrix`

Перейдите на страницу `pgPropsMatrix` и добавьте два Label.

1. Первый Label:
   - `Name = lblDelTitle`
   - `Caption = Удалять`
2. Второй Label:
   - `Name = lblCreTitle`
   - `Caption = Создавать`

---

## Шаг 5. Добавить подписи строк (классы документов)

На странице `pgPropsMatrix` добавьте шесть Label.

### Блок «Удалять»
1. `Name = lblDelRow_1`
2. `Name = lblDelRow_2`
3. `Name = lblDelRow_3`

### Блок «Создавать»
4. `Name = lblCreRow_1`
5. `Name = lblCreRow_2`
6. `Name = lblCreRow_3`

Текст (`Caption`) можете оставить пустым, он заполняется кодом автоматически.

---

## Шаг 6. Добавить подписи колонок для блока «Удалять»

На странице `pgPropsMatrix` добавьте одиннадцать Label.

1. `Name = lblDelCol_1`
2. `Name = lblDelCol_2`
3. `Name = lblDelCol_3`
4. `Name = lblDelCol_4`
5. `Name = lblDelCol_5`
6. `Name = lblDelCol_6`
7. `Name = lblDelCol_7`
8. `Name = lblDelCol_8`
9. `Name = lblDelCol_9`
10. `Name = lblDelCol_10`
11. `Name = lblDelCol_11`

Текст (`Caption`) можно оставить пустым.

---

## Шаг 7. Добавить подписи колонок для блока «Создавать»

На странице `pgPropsMatrix` добавьте десять Label.

1. `Name = lblCreCol_1`
2. `Name = lblCreCol_2`
3. `Name = lblCreCol_3`
4. `Name = lblCreCol_4`
5. `Name = lblCreCol_5`
6. `Name = lblCreCol_6`
7. `Name = lblCreCol_7`
8. `Name = lblCreCol_8`
9. `Name = lblCreCol_9`
10. `Name = lblCreCol_10`

Текст (`Caption`) можно оставить пустым.

---

## Шаг 8. Добавить все чекбоксы блока «Удалять»

На странице `pgPropsMatrix` добавьте ровно тридцать три CheckBox с точными именами.

### Строка класса 1
1. `chkDel_1_1`
2. `chkDel_1_2`
3. `chkDel_1_3`
4. `chkDel_1_4`
5. `chkDel_1_5`
6. `chkDel_1_6`
7. `chkDel_1_7`
8. `chkDel_1_8`
9. `chkDel_1_9`
10. `chkDel_1_10`
11. `chkDel_1_11`

### Строка класса 2
12. `chkDel_2_1`
13. `chkDel_2_2`
14. `chkDel_2_3`
15. `chkDel_2_4`
16. `chkDel_2_5`
17. `chkDel_2_6`
18. `chkDel_2_7`
19. `chkDel_2_8`
20. `chkDel_2_9`
21. `chkDel_2_10`
22. `chkDel_2_11`

### Строка класса 3
23. `chkDel_3_1`
24. `chkDel_3_2`
25. `chkDel_3_3`
26. `chkDel_3_4`
27. `chkDel_3_5`
28. `chkDel_3_6`
29. `chkDel_3_7`
30. `chkDel_3_8`
31. `chkDel_3_9`
32. `chkDel_3_10`
33. `chkDel_3_11`

Для всех этих CheckBox можно оставить пустой `Caption`, так как это таблица-флажки.

---

## Шаг 9. Добавить все чекбоксы блока «Создавать»

На странице `pgPropsMatrix` добавьте ровно тридцать CheckBox с точными именами.

### Строка класса 1
1. `chkCre_1_1`
2. `chkCre_1_2`
3. `chkCre_1_3`
4. `chkCre_1_4`
5. `chkCre_1_5`
6. `chkCre_1_6`
7. `chkCre_1_7`
8. `chkCre_1_8`
9. `chkCre_1_9`
10. `chkCre_1_10`

### Строка класса 2
11. `chkCre_2_1`
12. `chkCre_2_2`
13. `chkCre_2_3`
14. `chkCre_2_4`
15. `chkCre_2_5`
16. `chkCre_2_6`
17. `chkCre_2_7`
18. `chkCre_2_8`
19. `chkCre_2_9`
20. `chkCre_2_10`

### Строка класса 3
21. `chkCre_3_1`
22. `chkCre_3_2`
23. `chkCre_3_3`
24. `chkCre_3_4`
25. `chkCre_3_5`
26. `chkCre_3_6`
27. `chkCre_3_7`
28. `chkCre_3_8`
29. `chkCre_3_9`
30. `chkCre_3_10`

Для всех этих CheckBox можно оставить пустой `Caption`.

---

## Шаг 10. Проверить базовые контролы на других вкладках

Чтобы новая вкладка работала в общем потоке настроек, должны существовать и базовые контролы:

1. На `pgGeneral`:
   - `txtConstructorName`
   - `chkShowReport`
   - `chkCloseDocsAfterRun`
   - `chkShowSettingsOnStart`
2. На `pgRules`:
   - `txtFastenerPrefix`
   - `chkSkipReadOnly`
   - `txtExceptionInput`
3. На `pgProcessing`:
   - `chkClearProperties`
   - `chkEnableBBox`
4. Кнопки формы:
   - `btnOK`
   - `btnCancel`
   - `btnApply`

---

## Шаг 11. Вставить актуальный код

1. Откройте code-behind формы `frmSettings`.
2. Полностью удалите старый код формы.
3. Полностью вставьте содержимое файла `frmSettings_CODE.txt`.
4. Вставьте в стандартные модули содержимое:
   - `modSettings.bas`
   - `modMain.bas`

Важно: частичное копирование может оставить старые обработчики и вызвать конфликт.

---

## Шаг 12. Проверить компиляцию

1. В редакторе VBA выберите меню `Debug`.
2. Нажмите `Compile VBAProject`.
3. Если компиляция ругается на неизвестный контроль, значит имя в форме не совпадает с именем из этого гайда.
4. Исправьте имя и повторите компиляцию.

---

## Шаг 13. Проверить сохранение настроек матрицы

1. Запустите `modMain.main`.
2. Откройте вкладку `Матрица свойств`.
3. Измените несколько чекбоксов.
4. Нажмите `Применить`, затем `ОК`.
5. Откройте файл `PropertyN.ini` рядом с макросом.
6. Проверьте, что изменились значения в секциях:
   - `[DeleteMatrix]`
   - `[CreateMatrix]`

---

## Шаг 14. Что делать, если вкладка не работает

1. Проверьте, что у страницы имя `pgPropsMatrix`.
2. Проверьте, что `MultiPage` называется `mpMain`.
3. Проверьте каждое имя `chkDel_*_*` и `chkCre_*_*` без опечаток.
4. Проверьте, что в проекте вставлены свежие версии `modSettings.bas`, `modMain.bas`, `frmSettings_CODE.txt`.
5. Повторно выполните `Debug` → `Compile VBAProject`.

Если все имена точные, вкладка будет работать и сохраняться в `PropertyN.ini`.

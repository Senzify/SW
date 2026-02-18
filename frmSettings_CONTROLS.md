# Что создать в `frmSettings` (чётко по контролам)

## 1) Корневые элементы формы

- `UserForm` Name: `frmSettings`
- Caption: `Настройки макроса`
- Width/Height: по удобству (рекомендуемо ~980x620 для матрицы)

### Контрол на форме
- `MultiPage` Name: `mpMain`
  - Page(0) Name: `pgGeneral`, Caption: `Основные`
  - Page(1) Name: `pgRules`, Caption: `Исключения и правила`
  - Page(2) Name: `pgProcessing`, Caption: `Свойства и BBox`
  - Page(3) Name: `pgPropsMatrix`, Caption: `Матрица свойств`

---

## 2) Страница `pgGeneral`

- `Label` Name: `lblConstructorName`, Caption: `Конструктор:`
- `TextBox` Name: `txtConstructorName`
- `CheckBox` Name: `chkShowReport`, Caption: `Показывать отчёт после выполнения`
- `CheckBox` Name: `chkCloseDocsAfterRun`, Caption: `Закрывать документы, кроме стартового`
- `CheckBox` Name: `chkShowSettingsOnStart`, Caption: `Показывать форму настроек при запуске`

---

## 3) Страница `pgRules`

- `Label` Name: `lblFastenerPrefix`, Caption: `Префикс покупных:`
- `TextBox` Name: `txtFastenerPrefix`
- `CheckBox` Name: `chkSkipReadOnly`, Caption: `Пропускать ReadOnly файлы`
- `Label` Name: `lblExceptions`, Caption: `Исключения (по одному значению в строке):`
- `TextBox` Name: `txtExceptionInput`
  - `MultiLine = True`
  - `EnterKeyBehavior = True`
  - `ScrollBars = fmScrollBarsVertical`
  - (опционально) `WordWrap = False`

---

## 4) Страница `pgProcessing`

- `CheckBox` Name: `chkClearProperties`, Caption: `Очищать свойства перед записью`
- `CheckBox` Name: `chkEnableBBox`, Caption: `Строить Bounding Box для обычных деталей`

---

## 5) Страница `pgPropsMatrix` (новая)

### Заголовки блоков
- `Label` Name: `lblDelTitle` ("Удалять")
- `Label` Name: `lblCreTitle` ("Создавать")

### Подписи строк (классы)
- `lblDelRow_1`, `lblDelRow_2`, `lblDelRow_3`
- `lblCreRow_1`, `lblCreRow_2`, `lblCreRow_3`

### Подписи колонок
- `lblDelCol_1`..`lblDelCol_11`
- `lblCreCol_1`..`lblCreCol_10`

### Чекбоксы матрицы УДАЛЯТЬ
Имена по шаблону:
- `chkDel_<class>_<prop>`
- `class`: 1=Деталь, 2=Покупная/исключение, 3=Сборка
- `prop`:
  1=Обозначение
  2=Наименование
  3=Полное наименование
  4=Конструктор
  5=IsFastener
  6=Длина
  7=Ширина
  8=Толщина
  9=Масса
  10=Материал
  11=Другие

Пример:
- `chkDel_1_1`, `chkDel_1_2` ... `chkDel_3_11`

### Чекбоксы матрицы СОЗДАВАТЬ
Имена по шаблону:
- `chkCre_<class>_<prop>`
- `class`: 1..3
- `prop`: 1..10 (без "Другие")

Пример:
- `chkCre_1_1`, `chkCre_1_2` ... `chkCre_3_10`

---

## 6) Кнопки внизу формы (вне MultiPage)

- `CommandButton` Name: `btnOK`, Caption: `ОК`, Default = `True`
- `CommandButton` Name: `btnCancel`, Caption: `Отмена`, Cancel = `True`
- `CommandButton` Name: `btnApply`, Caption: `Применить`

Рекомендуемый порядок слева направо:
`btnApply` | `btnCancel` | `btnOK`

---

## 7) Куда вставлять код

- В стандартный модуль VBA вставить файл: `modSettings.bas`
- В стандартный модуль VBA вставить файл: `modMain.bas`
- В code-behind формы `frmSettings` вставить содержимое: `frmSettings_CODE.txt`

> Важно: имена контролов должны совпадать **символ в символ** с указанными выше, особенно для матрицы `chkDel_*_*` / `chkCre_*_*`.

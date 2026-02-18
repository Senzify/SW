# Исправление ошибок со скрина (Visual Studio на русском)

Этот файл — точная инструкция, что нажать и что заменить, чтобы убрать ошибки:

- `Недопустимый символ "\\"` в `ISwCommand.cs`
- `Повторяющийся атрибут ComVisible/Guid`
- `Пространство имен ... уже содержит определение для SwAddin/TaskPaneHost`
- `Не удалось найти пространство имен VasilevTools`
- `Не удалось найти тип SwPropertyNCommand`
- `Тип Size/Color определен в сборке, на которую нет ссылки (System.Drawing)`

---

## Шаг 1. Удалить дублирующий шаблон add-in

1. Открой **Обозреватель решений** (справа).
2. Найди файл `VasilevTaskPane_AddinTemplate.cs`.
3. Нажми по нему **правой кнопкой** -> **Исключить из проекта** (или **Удалить**).

> Этот файл примерный. Если его оставить вместе с вашим `SwAddin.cs`, получаются дубли классов и атрибутов.

---

## Шаг 2. Исправить `ISwCommand.cs`

1. Открой файл `ISwCommand.cs`.
2. Выдели **весь текст** (`Ctrl+A`) и удали.
3. Вставь **ровно этот код**:

```csharp
using SolidWorks.Interop.sldworks;

namespace VasilevTools.Commands
{
    public interface ISwCommand
    {
        string Name { get; }
        string Description { get; }
        void Execute(ISldWorks swApp);
    }
}
```

4. Сохрани (`Ctrl+S`).

> Ошибка `Недопустимый символ "\\"` появляется, когда в файл попал текст с экранированными `\n`/`\` из чата.

---

## Шаг 3. Проверить `SwPropertyNCommand.cs`

1. Открой `SwPropertyNCommand.cs`.
2. Убедись, что вверху есть:

```csharp
namespace VasilevTools.Commands
```

3. Сохрани (`Ctrl+S`).

---

## Шаг 4. Подключить команду в вашем `SwAddin.cs`

Открой ваш реальный файл `SwAddin.cs` (не шаблон), добавь:

1. Вверху файла:

```csharp
using VasilevTools.Commands;
```

2. Внутрь класса `SwAddin` добавь метод:

```csharp
private System.Collections.Generic.List<ISwCommand> BuildCommands()
{
    return new System.Collections.Generic.List<ISwCommand>
    {
        new SwPropertyNCommand()
    };
}
```

3. В `ConnectToSW(...)` после создания `TaskPaneHost` вызови:

```csharp
control.Initialize(swApp, BuildCommands());
```

---

## Шаг 5. Добавить метод `Initialize` в `TaskPaneHost`

Если в вашем `TaskPaneHost` его нет, вставь в класс:

```csharp
private ISldWorks _swApp;
private System.Collections.Generic.List<ISwCommand> _commands;

public void Initialize(ISldWorks swApp, System.Collections.Generic.List<ISwCommand> commands)
{
    _swApp = swApp;
    _commands = commands ?? new System.Collections.Generic.List<ISwCommand>();

    Controls.Clear();

    var panel = new FlowLayoutPanel
    {
        Dock = DockStyle.Fill,
        FlowDirection = FlowDirection.TopDown,
        WrapContents = false,
        AutoScroll = true
    };

    foreach (var cmd in _commands)
    {
        var btn = new Button
        {
            Width = 220,
            Height = 32,
            Text = cmd.Name
        };

        btn.Click += (s, e) => cmd.Execute(_swApp);
        panel.Controls.Add(btn);
    }

    Controls.Add(panel);
}
```

---

## Шаг 6. Пересобрать

1. Меню **Сборка** -> **Очистить решение**.
2. Меню **Сборка** -> **Собрать решение**.

Если снова есть ошибки, пришлите **скрин списка ошибок после этого шага**.

---

## Что должно остаться в проекте минимум

- `SwAddin.cs` (ваш основной, один штука)
- `ISwCommand.cs`
- `SwPropertyNCommand.cs`

И НЕ должно быть второго файла с дубликатом `SwAddin`/`TaskPaneHost`.

---

## Дополнительно: ошибки, которые вы прислали

1. **`Тип "Size"/"Color" определен в сборке, на которую нет ссылки`**
   - Открой `Обозреватель решений` -> `Ссылки` -> ПКМ -> `Добавить ссылку...`.
   - Вкладка `Сборки` -> поставь галочку `System.Drawing` -> `ОК`.

2. **`Имя "swCommands_e" не существует в текущем контексте`**
   - В коде используй числовую команду:
   - `swApp.RunCommand(954, string.Empty);`
   - Это уже исправлено в `SwPropertyNCommand.cs`.

3. **`Environment является неоднозначной ссылкой`**
   - Пиши полное имя:
   - `System.Environment.CurrentDirectory`
   - Это уже исправлено в `SwPropertyNCommand.cs`.


## Ошибка: «Отказано в доступе ...\SOLIDWORKS\PropertyN.ini»

Причина: надстройка пыталась писать настройки в папку `Program Files`, где обычно нет прав записи.

Что сделано в коде:
- теперь путь к настройкам автоматически переключается на пользовательскую папку:
  `%LOCALAPPDATA%\VasilevTools\PropertyN.ini`;
- если папка SolidWorks недоступна для записи, сохранение идёт в эту пользовательскую папку с уведомлением.

Что сделать у себя:
1. Обновить `SwPropertyNCommand.cs` из текущей версии.
2. Пересобрать проект (`Сборка -> Очистить решение`, затем `Сборка -> Собрать решение`).
3. Снова нажать кнопку `PropertyN` в TaskPane.


Дополнительно по вашей задаче синхронизации между ПК:
- приоритет пути теперь такой: `папка DLL/кода` -> `папка макроса` -> `текущая папка`;
- то есть если `PropertyN.ini` лежит рядом с вашим кодом в синхронизируемой папке, команда будет читать/писать именно туда.


## Где теперь искать `PropertyN.ini`

После запуска команды показывается окно с точным путём к файлу настроек (`Файл настроек PropertyN: ...`).

Логика выбора пути:
1. рядом с DLL/кодом add-in;
2. рядом с макросом (если есть);
3. текущая рабочая папка процесса;
4. fallback: `%LOCALAPPDATA%\VasilevTools\PropertyN.ini`.

Прогресс-форма теперь держится на экране весь цикл выполнения: тихий/основной проход, закрытие документов и формирование отчёта. В конце добавлена пауза ~0.7 сек на статусе `Завершено`.


## Если исключение не срабатывает

Проверьте 3 вещи:
1. В исключении должно быть имя файла **без** `.sldprt/.sldasm`.
2. Лучше копировать имя прямо из файла (чтобы не промахнуться в цифре, например `886` vs `86`).
3. Теперь в коде включена нормализация: регистр игнорируется, `ё` приводится к `е`, лишние двойные пробелы схлопываются.

Также добавлено мягкое сравнение: если имя файла начинается с исключения + `_` или пробел, это тоже считается совпадением.


Важно: теперь C# версия читает и пишет исключения также в legacy-формате:

[Exceptions]
item1=...
item2=...

То есть ваш старый формат VBA поддерживается напрямую.


## Почему исключения в окне сливаются в одну строку

Причина была в переводах строк: в INI использовался `\n`, а WinForms TextBox ожидает `CRLF` (`\r\n`) для корректного переноса.

Исправлено:
- при загрузке `ExceptionsText` теперь `\n` конвертируется в `Environment.NewLine`;
- при чтении секции `[Exceptions]` элементы склеиваются через `Environment.NewLine`;
- перед показом в textbox строка нормализуется к системным переносам Windows.


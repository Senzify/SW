# Полный гайд: как из вашего кода `SwAddin` подключить новый макрос PropertyN (шаг за шагом, для новичка)

Ниже инструкция прямо под ваш шаблон `SwAddin/TaskPaneHost`, который вы прислали.

---

## 0) Что у вас уже есть

У вас уже есть:
- `SwAddin : ISwAddin`;
- `TaskPaneHost : UserControl`;
- `ConnectToSW(...)`, `DisconnectFromSW()`;
- регистрация через `[ComRegisterFunction]`.

Это хорошая база.

---

## 1) Создать проект правильно

1. В Visual Studio нажмите `Создание проекта` (или `Create a new project`, если часть интерфейса на английском).
2. Выберите **Class Library (.NET Framework)**.
3. Укажите **.NET Framework 4.7.2**.
4. Имя проекта, например: `VasilevTaskPane`.

Почему так: SW COM add-in обычно проще и стабильнее на .NET Framework.

---

## 2) Добавить ссылки (References)

В проекте добавьте references:

1. `SolidWorks.Interop.sldworks`
2. `SolidWorks.Interop.swconst`
3. `SolidWorks.Interop.swpublished`
4. `System.Windows.Forms`

Проверка: в коде должны компилироваться `ISldWorks`, `ISwAddin`, `ITaskpaneView`, `UserControl`.

---

## 3) Добавить интерфейс команд

Создайте файл `ISwCommand.cs` и вставьте:

```csharp
using SolidWorks.Interop.sldworks;

public interface ISwCommand
{
    string Name { get; }
    string Description { get; }
    void Execute(ISldWorks swApp);
}
```

---


## 3.1) Какие файлы добавить в проект (самое важное)

Ниже минимальный набор файлов, который нужно добавить в ваш C# add-in:

1. `ISwCommand.cs`
   - Если интерфейс уже есть в проекте — **не добавляйте повторно**.
2. `SwPropertyNCommand.cs`
   - Основная команда PropertyN (настройки, прогресс, обработка).
3. `VasilevTaskPane_AddinTemplate.cs`
   - Шаблон `SwAddin + TaskPaneHost` с динамическими кнопками.

Если у вас уже есть свой `SwAddin.cs`, делайте так:
- **не удаляйте проект целиком**;
- откройте `VasilevTaskPane_AddinTemplate.cs` как образец;
- перенесите в свой файл только нужные части:
  1) `BuildCommands()`;
  2) `TaskPaneHost.Initialize(_swApp, BuildCommands());` в `ConnectToSW(...)`;
  3) динамический `TaskPaneHost` (генерация кнопок из `List<ISwCommand>`).

---

## 3.2) Как добавить файл в русской Visual Studio

Для каждого нового файла:

1. В `Обозревателе решений` нажмите правой кнопкой на проект.
2. Выберите `Добавить -> Класс...`.
3. Введите имя файла (например, `SwPropertyNCommand.cs`).
4. Нажмите `Добавить`.
5. Откройте созданный файл, удалите автосгенерированный код.
6. Вставьте нужный код.
7. Нажмите `Ctrl+S`.

Повторите это для всех файлов из списка выше.

---

## 4) Добавить команду PropertyN

1. Добавьте файл `SwPropertyNCommand.cs`.
2. Вставьте код команды из репозитория (`SwPropertyNCommand.cs`).

Внутри уже есть:
- настройки (программная WinForms-форма);
- прогресс-форма (аналог `frmProgress`);
- обработка модели (2 прохода);
- отчёт через `swApp.SendMsgToUser2(...)`.

---

## 5) Обновить ваш `SwAddin` под список команд

В вашем `ConnectToSW(...)` после `AddControl(...)` нужно не просто создать контрол, а передать в него:
- текущий `ISldWorks swApp`;
- список команд `List<ISwCommand>`.

Используйте готовый шаблон из файла:
- `VasilevTaskPane_AddinTemplate.cs`

Ключевая строка:

```csharp
TaskPaneHost.Initialize(_swApp, BuildCommands());
```

Где `BuildCommands()` возвращает:

```csharp
new List<ISwCommand>
{
    new SwPropertyNCommand()
};
```

---

## 6) Почему старый `MessageBox.Show("Работает!")` больше не нужен

Вместо тестовой кнопки теперь TaskPane строится динамически:
- одна кнопка = одна команда из списка;
- `btn.Text = command.Name`;
- `btn.Click => command.Execute(swApp)`.

Ошибки команды показываются через:

```csharp
swApp.SendMsgToUser2(...)
```

---

## 7) Важно про COM-атрибуты

Для класса TaskPane-контрола добавьте:
- `[ComVisible(true)]`
- `[Guid("...")]` (уникальный)
- `[ProgId("VasilevTaskPane.TaskPaneHost")]`

Это уже есть в `VasilevTaskPane_AddinTemplate.cs`.

---

## 8) Важно про регистрацию (админ-права)

Ваш код пишет в `HKLM`:
- `SOFTWARE\SolidWorks\Addins\{GUID}`
- `Software\Classes\CLSID\{GUID}\Control`

Для этого обычно нужны права администратора.

Практика:
1. Закройте Visual Studio.
2. Запустите Visual Studio **от имени администратора** (Run as Administrator).
3. Соберите проект.
4. Зарегистрируйте add-in (вашим текущим способом).

---

## 9) Иконка TaskPane

В `ConnectToSW(...)` используется `icon.bmp` рядом с DLL:

```csharp
string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon.bmp");
```

Сделайте так:
1. Добавьте `icon.bmp` в проект.
2. Свойства файла:
   - `Build Action = Content`
   - `Copy to Output Directory = Copy if newer`

---


## 9.1) Соответствие названий меню для русской Visual Studio

Чтобы не путаться, вот частые команды в русском интерфейсе:

- `Create a new project` → `Создание проекта`
- `Add -> Class...` → `Добавить -> Класс...`
- `Build` → `Сборка`
- `Build Solution` → `Собрать решение`
- `References` (в .NET Framework проекте) → `Ссылки`
- `Properties` (файла/проекта) → `Свойства`
- `Copy to Output Directory` → `Копировать в выходной каталог`

---

## 10) Сборка проекта

1. `Сборка -> Собрать решение` (или `Build -> Build Solution`).
2. Исправьте ошибки, если есть.
3. Должно быть `Build succeeded`.

---

## 11) Подключение в SolidWorks

1. Откройте SolidWorks.
2. Перейдите в `Сервис -> Надстройки` (или `Tools -> Add-Ins`).
3. Найдите ваш add-in `Vasilev TaskPane`.
4. Включите галочку `Active Add-ins`.
5. При необходимости поставьте `Start Up`.

---

## 12) Проверка работы команды

1. Откройте TaskPane `Vasilev Tools`.
2. Нажмите кнопку `PropertyN`.
3. Откроется окно настроек.
4. После подтверждения начнётся обработка.
5. Появится прогресс-окно.
6. В конце отчёт через `SendMsgToUser2`.

---

## 13) Где хранится конфиг

Файл настроек:
- `PropertyN.ini`

Создаётся рядом с macro path (если доступен), иначе в `CurrentDirectory`.

---

## 14) Если не работает — быстрый чеклист

1. Проверить .NET Framework = 4.7.2.
2. Проверить все SW interop references.
3. Проверить, что TaskPaneHost помечен `ComVisible + Guid + ProgId`.
4. Проверить, что `BuildCommands()` содержит `new SwPropertyNCommand()`.
5. Проверить права администратора для HKLM-регистрации.
6. Проверить, что `icon.bmp` копируется рядом с DLL.

---

## 15) Что именно заменить в вашем коде

1. Заменить текущий `TaskPaneHost` с одной кнопкой `Работает!` на динамическую генерацию кнопок по `List<ISwCommand>`.
2. В `SwAddin.ConnectToSW(...)` после `AddControl(...)` добавить `TaskPaneHost.Initialize(_swApp, BuildCommands());`.
3. Добавить `BuildCommands()` и вернуть `new SwPropertyNCommand()`.
4. Добавить файл команды `SwPropertyNCommand.cs`.

Чтобы не ошибиться, используйте готовый файл:
- `VasilevTaskPane_AddinTemplate.cs`

---

Если хотите, следующим шагом могу дать **минимальный .csproj-шаблон** под этот add-in (Framework 4.7.2 + нужные references), чтобы вы просто вставили и собрали.


## Быстрый пошаговый режим (только клики)

Если нужен формат "куда тыкнуть" без лишней теории, откройте:
- `RUS_VISUAL_STUDIO_CLICK_BY_CLICK.md`


## Если вы уже добавили шаблон и получили дубли классов

Если после добавления шаблона появились ошибки про повтор `SwAddin`, `TaskPaneHost`, `ComVisible`, `Guid` — откройте файл:
- `RUS_VISUAL_STUDIO_CLICK_BY_CLICK.md` (шаг 14)

Там есть точный разбор каждой ошибки и быстрый план исправления.


## Если вы совсем новичок

Откройте короткий файл без сложных терминов:
- `START_HERE_IF_YOU_ARE_NEW.md`

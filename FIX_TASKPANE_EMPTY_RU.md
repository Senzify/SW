# TASKPANE ПУСТО (СПРАВА БЕЛОЕ ПОЛЕ) — БЫСТРЫЙ ФИКС

Если вкладка `Vasilev Tools` открывается, но справа пусто — почти всегда причина в том, что вы создаёте **не тот экземпляр** `TaskPaneHost`.

Неправильно (частая ошибка):

```csharp
control = new TaskPaneHost();
taskPane.AddControl(control.GetType().FullName, "");
control.Initialize(swApp, BuildCommands());
```

`AddControl(...)` создаёт **свой внутренний экземпляр** контрола, а `Initialize(...)` вызывается у другого.

---

## СДЕЛАЙТЕ РОВНО ТАК

## 1) В `TaskPaneHost` добавьте bootstrap

```csharp
[ComVisible(true)]
public class TaskPaneHost : UserControl
{
    private static ISldWorks _bootstrapSwApp;
    private static List<ISwCommand> _bootstrapCommands;

    private ISldWorks _swApp;
    private List<ISwCommand> _commands;

    public static void SetBootstrap(ISldWorks swApp, List<ISwCommand> commands)
    {
        _bootstrapSwApp = swApp;
        _bootstrapCommands = commands;
    }

    public TaskPaneHost()
    {
        // ЭТОТ КОНСТРУКТОР БУДЕТ ВЫЗВАН ИМЕННО ДЛЯ ЭКЗЕМПЛЯРА,
        // КОТОРЫЙ СОЗДАЕТ SOLIDWORKS ЧЕРЕЗ AddControl
        if (_bootstrapSwApp != null)
        {
            Initialize(_bootstrapSwApp, _bootstrapCommands ?? new List<ISwCommand>());
        }
    }

    public void Initialize(ISldWorks swApp, List<ISwCommand> commands)
    {
        _swApp = swApp;
        _commands = commands ?? new List<ISwCommand>();

        Controls.Clear();

        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true
        };

        if (_commands.Count == 0)
        {
            panel.Controls.Add(new Label
            {
                Text = "Команды не найдены",
                AutoSize = true
            });
            Controls.Add(panel);
            return;
        }

        foreach (var cmd in _commands)
        {
            var btn = new Button
            {
                Width = 230,
                Height = 34,
                Text = cmd.Name
            };

            btn.Click += (s, e) =>
            {
                try { cmd.Execute(_swApp); }
                catch (Exception ex)
                {
                    _swApp?.SendMsgToUser2(
                        $"Ошибка команды '{cmd.Name}': {ex.Message}",
                        (int)swMessageBoxIcon_e.swMbStop,
                        (int)swMessageBoxBtn_e.swMbOk);
                }
            };

            panel.Controls.Add(btn);
        }

        Controls.Add(panel);
    }
}
```

## 2) В `ConnectToSW(...)` УДАЛИТЕ ручное `new TaskPaneHost()`

Было:

```csharp
control = new TaskPaneHost();
taskPane.AddControl(control.GetType().FullName, "");
control.Initialize(swApp, BuildCommands());
```

Должно стать:

```csharp
TaskPaneHost.SetBootstrap(swApp, BuildCommands());
taskPane.AddControl(typeof(TaskPaneHost).FullName, "");
```

## 3) Соберите заново

1. `Сборка -> Очистить решение`
2. `Сборка -> Собрать решение`
3. Перезапустите SolidWorks (или отключить/включить надстройку)

---

## Если всё ещё пусто

Проверьте 3 пункта:

1. У `TaskPaneHost` есть атрибут `[ComVisible(true)]`.
2. В проекте только **один** класс `TaskPaneHost`.
3. `BuildCommands()` реально возвращает хотя бы одну команду, например `new SwPropertyNCommand()`.

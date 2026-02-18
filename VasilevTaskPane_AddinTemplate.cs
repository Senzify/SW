using System;
using System.Collections.Generic;
using System.Windows.Forms;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

// ВАЖНО:
// Это БЕЗОПАСНЫЙ ШАБЛОН-ПРИМЕР, который НЕ должен конфликтовать
// с уже существующими классами SwAddin/TaskPaneHost в вашем проекте.
//
// Если у вас уже есть рабочие классы:
// - SwAddin
// - TaskPaneHost
// то НЕ копируйте эти классы повторно.
// Просто перенесите нужные куски логики из этого файла в существующие классы.

namespace VasilevTaskPane.TemplateExamples
{
    /// <summary>
    /// Минимальный интерфейс команды для примера динамического TaskPane.
    /// Если в проекте уже есть ISwCommand — используйте ваш существующий интерфейс.
    /// </summary>
    public interface ITemplateSwCommand
    {
        string Name { get; }
        string Description { get; }
        void Execute(ISldWorks swApp);
    }

    /// <summary>
    /// Пример логики построения списка команд (без привязки к конкретной реализации add-in).
    /// </summary>
    public static class CommandWiringExample
    {
        public static List<ITemplateSwCommand> BuildCommands()
        {
            return new List<ITemplateSwCommand>
            {
                // ДОБАВЬТЕ СЮДА ВАШУ РЕАЛЬНУЮ КОМАНДУ, например:
                // new SwPropertyNCommand()
                // (убедитесь, что namespace команды подключен и класс существует)
            };
        }

        public static void RunCommandSafely(ISldWorks swApp, ITemplateSwCommand command)
        {
            if (command == null) return;

            try
            {
                command.Execute(swApp);
            }
            catch (Exception ex)
            {
                swApp?.SendMsgToUser2(
                    $"Ошибка команды '{command.Name}': {ex.Message}",
                    (int)swMessageBoxIcon_e.swMbStop,
                    (int)swMessageBoxBtn_e.swMbOk);
            }
        }
    }

    /// <summary>
    /// Пример построения панели кнопок по динамическому списку команд.
    /// Можно перенести этот код в ваш существующий TaskPaneHost.
    /// </summary>
    public static class TaskPaneUiExample
    {
        public static void BuildButtons(
            Control container,
            ISldWorks swApp,
            IReadOnlyList<ITemplateSwCommand> commands)
        {
            if (container == null) return;

            container.Controls.Clear();

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                AutoScroll = true
            };
            container.Controls.Add(root);

            if (commands == null || commands.Count == 0)
            {
                root.Controls.Add(new Label
                {
                    Dock = DockStyle.Top,
                    AutoSize = true,
                    Text = "Нет зарегистрированных команд"
                });
                return;
            }

            foreach (var command in commands)
            {
                var btn = new Button
                {
                    Dock = DockStyle.Top,
                    Height = 34,
                    Text = command.Name
                };

                var tip = new ToolTip();
                tip.SetToolTip(btn, command.Description);

                btn.Click += (_, __) => CommandWiringExample.RunCommandSafely(swApp, command);
                root.Controls.Add(btn);
            }
        }
    }
}

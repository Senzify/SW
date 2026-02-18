# СТАРТ ЗДЕСЬ (если вы новичок в C# и Visual Studio)
> Если у вас ошибки как на скрине (дубли `SwAddin`, `ComVisible`, символ `\` в `ISwCommand.cs`) — сразу откройте `FIX_SCREEN_ERRORS_RU.md` и выполните шаги 1–6.
> Если справа TaskPane пустой (белое поле) — откройте `FIX_TASKPANE_EMPTY_RU.md` и выполните шаги 1–3.

Если всё сложно — делайте только эти 7 шагов.

## 1) Создайте проект
- Visual Studio → **Создание проекта**
- Шаблон: **Библиотека классов (.NET Framework)**
- Версия: **.NET Framework 4.7.2**

## 2) Добавьте ссылки
- `SolidWorks.Interop.sldworks`
- `SolidWorks.Interop.swconst`
- `SolidWorks.Interop.swpublished`
- `System.Windows.Forms`
- `System.Drawing`

## 3) Добавьте файл `ISwCommand.cs`
Если уже есть — пропустите.

## 4) Добавьте файл `SwPropertyNCommand.cs`
Вставьте код целиком.

## 5) НЕ трогайте старый `SwAddin.cs`
- Не переносите вручную куски кода.
- Не добавляйте второй `SwAddin`/`TaskPaneHost`.

## 6) Соберите проект
- **Сборка → Собрать решение**

## 7) Если ошибка про дубли (`SwAddin`, `TaskPaneHost`, `ComVisible`, `Guid`)
- Значит в проекте два одинаковых класса.
- Оставьте только один `SwAddin` и один `TaskPaneHost`.

---

Если хотите подробную версию «куда нажимать», откройте:
- `RUS_VISUAL_STUDIO_CLICK_BY_CLICK.md`

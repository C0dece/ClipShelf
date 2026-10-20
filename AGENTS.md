# AGENTS.md - Справка для инженеров и агентов проекта ClipShelf

## 1. Назначение и стек
- **Назначение**: Утилита Windows для продвинутого буфера обмена в стиле Windows 11 Fluent, плавающей полки файлов SuperHub и моментальной вставки изображений и текста как файлов через Ctrl+V.
- **Стек**: C# (.NET Framework 4.0 / 4.5, C# 5 синтаксис), WinForms, Win32 API / PInvoke (DWM, Shell, User32).
- **Сборка**: Без внешних nuget-пакетов, встроенным компилятором `csc.exe` через `build.bat`.

## 2. Ключевые команды
- **Сборка**: `cmd.exe /c build.bat` (компилирует `src\*.cs` в `dist\ClipShelf.exe` и `dist\PasteImageAsFile.exe`).
- **Запуск демона**: `dist\ClipShelf.exe --daemon`
- **Открыть настройки**: `dist\ClipShelf.exe`
- **Вызов буфера обмена**: `dist\ClipShelf.exe --history`
- **Генерация скриншотов UI**: `dist\ClipShelf.exe --screenshot [папка]`
- **Установка в систему**: `dist\ClipShelf.exe --install`

## 3. Архитектура модулей
- `src/Program.cs`: Точка входа, IPC, трей-меню, поиск активных пользовательских окон и маршрутизация команд.
- `src/ClipboardWatcher.cs`: Отслеживание изменений буфера обмена (`AddClipboardFormatListener`), дополнение формата `FileDropList`, FileSystemWatcher для рабочего стола, глобальные горячие клавиши (`RegisterHotKey` и `WH_KEYBOARD_LL` для перехвата `Win+V`, `Ctrl+Alt+C` и `Esc` для закрытия буфера).
- `src/ClipboardHistoryManager.cs`: Автономное хранилище истории буфера обмена и полки SuperHub (сохранение в `history.json`), метод быстрого продвижения текущего буфера в SuperHub (`PromoteCurrentOrLatestToSuperHub`).
- `src/ClipboardFlyoutForm.cs`: Пользовательский интерфейс буфера обмена и полки SuperHub в стиле нативного системного буфера обмена Windows 11 (`Win+V`) с полупрозрачно-матовым эффектом размытия фона (`AcrylicPanel` / `AcrylicCardPanel`), чистой иконкой шестеренки настроек без обводки, открытием рядом с курсором или в углах/краях экрана без перехвата фокуса (`ShowWithoutActivation`, `MA_NOACTIVATE`) и мгновенной вставкой при клике. Поддерживает динамический ресайз с дебаунсом перезахвата акрила, обработкой `WM_EXITSIZEMOVE`, надежное автозакрытие при уводе курсора с меню и маркера с настраиваемой задержкой (`SuperHubAutoHideDelay`) и мгновенное закрытие при клике мимо меню.
- `src/FolderPromptForm.cs`: Диалоговое окно для создания и переименования папок/вкладок SuperHub в стиле Windows 11 Fluent.
- `src/ImageDropHelper.cs`: Обработка перетаскивания изображений из браузеров (Chromium / Firefox / Edge) с извлечением реальных битмапов и бинарных данных картинок вместо текстовых ссылок.
- `src/ItemViewerForm.cs`: Окно детального просмотра (зум колесиком мыши 10%-500%, панорамирование картинок, кнопка булавки [📌] поверх всех окон, извлечение чистого текста из .docx/.doc через DeflateStream, выделение и частичное копирование текста).
- `src/SuperHubDockForm.cs`: Плавающая полка SuperHub у края экрана: разделенный двухцветный маркер увеличенной высоты (`138x5px`, первая половина открывает Буфер обмена, вторая половина — SuperHub; отключаемо в настройках), авто-выдвижение при Drag-and-Drop или наведении мыши. Защищено от циклического перезапуска при нахождении курсора на маркере (`IsFlyoutOpen` guard).
- `src/ThemeHelper.cs`: Определение системной темы Windows (`AppsUseLightTheme`), графитовая палитра Windows 11 (`#1F1F1F`, `#2D2D2D`, `#363636`), захват и многопроходное размытие фона экрана (`CaptureAcrylicBackdrop`, `DrawAcrylicBackground`), аппаратное исключение формы из захвата экрана DWM через `SetWindowDisplayAffinity(hwnd, WDA_EXCLUDEFROMCAPTURE = 0x11)`, безопасная отрисовка акрила без артефактов при выходе за границы (`Rectangle.Intersect`).
- `src/Config.cs`: Управление конфигурацией в реестре (`HKCU\Software\PasteImageAsFile`), включая `SuperHubSplitMarker`, `SuperHubAccentColor`, `SuperHubAutoHideDelay`, `ClipboardHotkey`, `DisableWinClipboardHistory` (`EnableClipboardHistory=0` в `HKCU\Software\Microsoft\Clipboard`), `SuperHubAddHotkey`.
- `src/DesktopHelper.cs`: Позиционирование файлов на рабочем столе под курсором через IFolderView / IShellFolder.
- `src/FileIconHelper.cs`: Извлечение системных иконок для файлов через `SHGetFileInfo`.
- `src/MainForm.cs`: Окно управления и настроек (вкладки Основные, Буфер обмена с выбором хоткея `Win+V` и отключением системного буфера Windows, SuperHub с настройкой акцентного цвета SuperHub, двухцветного маркера, хоткея `Ctrl+Alt+C` и задержки автозакрытия).
- `src/ShellIntegration.cs`: Интеграция с Проводником и автозапуском реестра.
- `src/UpdateHelper.cs`: Проверка релизов GitHub через Releases API и fallback на редиректы, сравнение версий, атомарное скачивание и самообновление через bat-скрипт.
- `src/UpdateDialogForm.cs`: Диалоговое окно проверки и скачивания обновлений в стиле Windows 11 Fluent с индикатором прогресса загрузки.

## 4. Критические ограничения, соглашения и грабли (Caveats)
- **Синтаксис C# 5**: Не использовать expression-bodied properties (`=>`), строковую интерполяцию (`$""`), null-conditional операторы `?.` - встроенный компилятор `csc.exe` (.NET 4.0) их не поддерживает.
- **Безопасные пути**: Для любых путей файлов обязательно использовать `SafeGetFileName`, `SafeFileExists`, `SafeDirectoryExists` во избежание `ArgumentException` на непечатных символах заголовков окон.
- **Конфликт WS_EX_TRANSPARENT и WS_EX_COMPOSITED**: В формах с `WS_EX_COMPOSITED` категорически запрещено применять `WS_EX_TRANSPARENT` к кнопкам и панелям. Это приводит к несмываемым артефактам и дублированию иконок при скролле или ресайзе. Вместо этого — явный вызов `Graphics.Clear(bg)` в `OnPaintBackground`/`OnPaint`.
- **Захват фона под акрил**: `CopyFromScreen` (GDI `BitBlt`) игнорирует `WDA_EXCLUDEFROMCAPTURE = 0x11`. Захват акрила рабочего стола производится строго ДО показа окна (`Visible = false` в `AnimateIn`) сразу на весь размер монитора `Screen.FromPoint(loc).Bounds` с сохранением `BackdropOrigin`. Это гарантирует, что при интерактивном ресайзе формы пользователем по ширине и высоте под всей поверхностью окна уже есть готовый размытый акрил экрана без разрывов, черных полос и необходимости перезахвата в реальном времени. В `WM_EXITSIZEMOVE` перезахват выполняется только при перемещении на другой монитор.
- **Защита ресайза в DrawAcrylicBackground**: Перед рисованием акрила холст контрола обязан гарантированно очищаться `g.Clear(Background)`. Экранные координаты контрола высчитываются как `(rootForm.Left + pt.X - backdropOrigin.X, rootForm.Top + pt.Y - backdropOrigin.Y)`, а прямоугольник чтения безопасно ограничивается через `Rectangle.Intersect(srcRect, bmpRect)`.
- **Фокус ввода и вставка**: Окно буфера обмена не перехватывает фокус (`ShowWithoutActivation => true`, `MA_NOACTIVATE`), позволяя продолжать печатать при открытом окне `Win+V`. При клике по карточке элемент мгновенно вставляется в активное окно через `Program.ForceForegroundWindow(targetWnd)` + `Ctrl+V`.
- **Защита маркера дока от циклического дребезга**: В `SuperHubDockForm.cs` перед открытием флайаута проверять `ClipboardFlyoutForm.IsFlyoutOpen`. Если флайаут уже открыт, повторные вызовы `ExpandShelf` блокируются.
- **Запрет base.InvokePaint(this, e) в OnPaint**: В WinForms `InvokePaint(c, e)` вызывает `c.OnPaint(e)`. Вызов `base.InvokePaint(this, e)` внутри `OnPaint` вызывает бесконечную рекурсию и переполнение стека (`STATUS_STACK_BUFFER_OVERRUN`). Всегда вызывать `base.OnPaint(e)`.
- **Автозакрытие флайаута при уводе мыши**: Зона удержания мыши включает `this.Bounds` (+8px) и `dockMarkerBounds` (+6px). Запрещено использовать жесткие флаги вроде `hasMouseEnteredDock` для блокировки закрытия — если пользователь случайно задел маркер и отвел мышь без входа в меню, окно обязано надежно скрыться через `SuperHubAutoHideDelay`. Клик ЛКМ вне зоны меню (`IsLeftMouseButtonDown`) закрывает окно немедленно. Открытые контекстные меню (`#32768`) защищают окно от случайного закрытия.

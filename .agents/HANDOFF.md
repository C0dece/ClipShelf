# HANDOFF: ClipShelf

## Текущее состояние
Репозиторий полностью переименован и обновлен до **ClipShelf (v1.8.0)**:
- **Новое название**: ClipShelf — акцент на буфере обмена (Clipboard) и плавающей полке для файлов (Shelf / SuperHub).
- **Новая иконка**: Сгенерирована и интегрирована полноформатная иконка в стиле **Windows 11 Fluent 3D / Acrylic** (`assets/app_icon.png` 512x512 с прозрачным фоном и `assets/app.ico` со всеми системными размерами от 16x16 до 256x256).
- **Кодовая база**: 
  - Названия окон, диалогов, трея, IPC-сообщений и мьютексов обновлены на `ClipShelf`.
  - Реализована плавная обратная совместимость для реестра (`Software\ClipShelf` с fallback на `Software\PasteImageAsFile`).
  - Сохранена генерация `dist\PasteImageAsFile.exe` наряду с `dist\ClipShelf.exe`.
  - Обновлен чекер релизов GitHub в `UpdateHelper.cs`.
- **Документация и GitHub**:
  - `README.md` полностью переработан с презентацией трех столпов (SuperHub, Win11 Fluent Clipboard Manager, Ctrl+V Smart Pasting).
  - `AGENTS.md` актуализирован.
  - Коммиты отправлены в ветку `main` на GitHub.

## Ключевые файлы
- `assets/app_icon.png` — 512x512 3D Fluent иконка приложения.
- `assets/app.ico` — системный мульти-резолюшн значок (.ico).
- `dist/ClipShelf.exe` — скомпилированный бинарник приложения.
- `src/ShellIntegration.cs` — интеграция в Windows, контекстные меню, автозапуск.
- `src/Config.cs` — реестр настроек ClipShelf + fallback.
- `src/Program.cs` — точка входа и жизненный цикл.
- `src/ClipboardWatcher.cs` — глобальные хоткеи и перехват буфера.
- `src/ClipboardFlyoutForm.cs` — UI буфера и шторки.
- `build.bat` — скрипт сборки без внешних зависимостей.

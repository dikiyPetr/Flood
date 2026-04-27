# Core

## Назначение

Тонкая обёртка над Unity Input System. Содержит только сгенерированный из `.inputactions` код — не ручной.

## Зависимости (asmdef)

- `Unity.InputSystem` — источник `InputAction`/`InputActionAsset`.

## Ключевые типы

| Тип | Файл | Роль |
|---|---|---|
| `InputSystem_Actions` | `InputSystem_Actions.cs` | Сгенерированный класс действий (Move, Sprint и т.п.). Используется через `new InputSystem_Actions()`. |

## Контракты

- `InputSystem_Actions.cs` **генерируется автоматически** из `InputSystem_Actions.inputactions`. Не редактировать руками — следующая регенерация затрёт изменения. Менять схему действий через Unity Editor (Input Actions inspector).
- `InputSystem_Actions` реализует `IDisposable` — потребитель обязан вызвать `Dispose()` в `OnDestroy`.

## Точки расширения

- Новые карты действий (например, `Combat` для масла из GDD §3.3) добавляются в `.inputactions`-ассет, не в код.

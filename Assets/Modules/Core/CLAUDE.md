# Core

## Назначение

Общая инфраструктура проекта, общая для нескольких модулей: ввод (Input System) и кросс-модульные конфиги (реестр Unity-слоёв). Не содержит геймплейной логики — только базовые ссылки и описания, на которые опираются другие модули.

## Зависимости (asmdef)

- `Unity.InputSystem` — источник `InputAction`/`InputActionAsset`.

## Ключевые типы

| Тип | Файл | Роль |
|---|---|---|
| `InputSystem_Actions` | `InputSystem_Actions.cs` | Сгенерированный класс действий (Move, Sprint и т.п.). Используется через `new InputSystem_Actions()`. |
| `GameLayersConfig` | `Configs/GameLayersConfig.cs` | SO. Централизованный реестр Unity-слоёв проекта. `PickerLayers` (кто подбирает пикапы). По мере роста — добавляются новые маски (враги, ловушки, снаряды). Меню: `Flood/Core/Game Layers Config`. Один ассет на игру, шарится между модулями. |

## Контракты

- `InputSystem_Actions.cs` **генерируется автоматически** из `InputSystem_Actions.inputactions`. Не редактировать руками — следующая регенерация затрёт изменения. Менять схему действий через Unity Editor (Input Actions inspector).
- `InputSystem_Actions` реализует `IDisposable` — потребитель обязан вызвать `Dispose()` в `OnDestroy`.
- `GameLayersConfig` — **только декларация**. Маски лежат тут, логика фильтрации (`(mask.value & (1 << layer)) != 0`) — на стороне потребителя. Если маска не задана (default = 0) — потребитель никого не пропустит, что обычно проявляется логом «pickup не сработал».

## Точки расширения

- Новые карты действий (например, `Combat` для масла из GDD §3.3) добавляются в `.inputactions`-ассет, не в код.
- Новые роли слоёв (`EnemyLayers` для AI-сенсоров, `DamageableLayers` для зон масла) — поле в `GameLayersConfig` + read-only property. Не плодить отдельные SO-конфиги на каждую роль.

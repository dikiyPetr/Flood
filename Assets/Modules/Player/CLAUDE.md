# Player

## Назначение

Управление игроком: чтение ввода, расчёт перемещения, эмиссия точек активного следа в `Floor.ArenaState`. Краску игрок здесь не тратит — рисование следа бесплатно (GDD §3.1).

## Зависимости (asmdef)

- `Core` — `InputSystem_Actions`.
- `Floor` — `ArenaState.AppendTrailPoint(Vector2)` для эмиссии трейла.
- `Unity.InputSystem` — типы `InputAction`.

## Ключевые типы

| Тип | Файл | Роль |
|---|---|---|
| `PlayerController` | `Domain/PlayerController.cs` | MonoBehaviour. `Update`: ввод → `PlayerMovementCalculator` → `CharacterController.Move` + поворот. |
| `PlayerMovementCalculator` | `Domain/PlayerMovementCalculator.cs` | Pure C#. `Calculate(Vector2 input, bool sprint, float dt) → MovementStep`. Покрыт юнит-тестами. |
| `MovementStep` | `Domain/MovementStep.cs` | readonly struct: `Displacement`, `FacingDirection`. |
| `PlayerConfig` | `Domain/PlayerConfig.cs` | SO: `MoveSpeed`, `SprintMultiplier`, `RotationSpeed`. Меню: `Flood/Player/Player Config`. |
| `PlayerTrailEmitter` | `Domain/PlayerTrailEmitter.cs` | MonoBehaviour. `LateUpdate` → `_arena.AppendTrailPoint(transform.position.xz)`. |

## Контракты

- `PlayerTrailEmitter.LateUpdate` обязан срабатывать **после** `PlayerController.Update` — порядок гарантируется самим жизненным циклом Unity (Late > Update). Не переносить эмиссию в Update.
- `PlayerMovementCalculator` — без зависимости от `MonoBehaviour`/`Time`/`Transform`. Любая чистая логика движения должна оставаться там, не утекать в `PlayerController`.
- `PlayerMovementCalculator.Calculate` клампит magnitude (не нормализует) — сохраняет аналоговый стик, гасит keyboard-overshoot. Не менять без тестов.
- `InputSystem_Actions` создаётся в `Awake`, `Dispose` в `OnDestroy` (см. `PlayerController`).

## Точки расширения

- HP/смерть/респаун (GDD §3.4) — отдельный компонент в `Domain/`, на смерть вызывает `Floor.ArenaState.ClearActiveTrail()`.
- Кнопка разлива масла (GDD §3.3) — новая карта в `Core/InputSystem_Actions.inputactions`, обработчик в `PlayerController` или отдельный `PlayerOilCaster`.

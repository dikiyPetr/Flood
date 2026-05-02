# Navigation

## Назначение

Crowd-навигация для толп врагов через flow-field: одно поле направлений на всю арену с конфигурируемой стоимостью прохождения по типам клеток (`Empty`/`Territory`/`Line`) и несколькими целями с весами. Позиционируется между RTS и tower-defense — толпа выбирает оптимальный путь к ближайшей (по weighted-distance) цели, обходит дорогие участки, упирается и грызёт там, где обход дороже прямого пути. Терраформируемое: подписка на `Floor.ArenaState` события, dirty-rebuild через Dijkstra.

## Зависимости (asmdef)

- `Floor` — `ArenaState` (события `Painted`/`PaintedSilent`/`TerritoryErased`, `Grid`, `Floor`), `ArenaGrid` (`WorldToCell`/`CellCenterWorld`/`Resolution`/`Get`), `CellState`, `PaintableFloor` (`FloorCenterXZ`/`WorldSize`).

## Ключевые типы

| Тип | Файл | Роль |
|---|---|---|
| `NavigationGoal` | `Domain/NavigationGoal.cs` | readonly struct: `Vector2 WorldXZ`, `float Weight ∈ [0,1]`. Seed для Dijkstra. |
| `NavigationGoalBinding` | `Domain/NavigationGoalBinding.cs` | Serializable. Инспектор-биндинг: `Transform Anchor` (приоритет, читается каждый rebuild) или `Vector3 WorldPoint`. `ResolveWorldXZ(fallback)` → текущая мировая XZ. |
| `NavigationConfig` | `Domain/NavigationConfig.cs` | SO: `EmptyCost=1`, `TerritoryCost=8`, `LineCost=1`, `MaxSeedOffset=64`, `RebuildIntervalSeconds=0`, `AllowDiagonal=true`. `CostFor(CellState.Obstacle) = ObstacleCost = int.MaxValue/32` (sentinel; не настраивается). Меню: `Flood/Navigation/Navigation Config`. |
| `FlowField` | `Domain/FlowField.cs` | Pure C#. Flat-массивы `Cost`/`Integration`/`Direction`/`GoalMask` длиной `Resolution²`. `SampleDirection(cell)`/`GetIntegration(cell)`. `Unreachable = int.MaxValue`. |
| `FlowFieldBuilder` | `Domain/FlowFieldBuilder.cs` | Пере-исп. билдер: cost ← config, integration ← 8-связный Dijkstra (×10/×14 целочисленный), direction ← min-сосед. Внутренний `MinHeap` без аллокаций после прогрева. |
| `FlowFieldNavigator` | `Domain/FlowFieldNavigator.cs` | MonoBehaviour. Подписан на `ArenaState.Floor.Painted/PaintedSilent`, `ArenaState.TerritoryErased`, `ArenaState.ObstacleChanged` → dirty. `Update`: rebuild при dirty с дросселем `RebuildIntervalSeconds`. API: `SampleDirection(worldXZ)`, `Field`, `IsDirty`, `MarkDirty()`. |
| `FlowFieldGizmos` | `Domain/FlowFieldGizmos.cs` | MonoBehaviour, только `OnDrawGizmos`. Тогглы: стрелки потока (цвет по `CellState`), heat по integration, probe path (трассировка от `_probe.position`). |

## Контракты

- **Resolution-инвариант.** `FlowField.Resolution == ArenaGrid.Resolution`. При несовпадении (смена `ArenaConfig.GridResolution` в рантайме) `FlowFieldNavigator` пересоздаёт `FlowField` в `Rebuild`. Пользователю не нужно дёргать руками.
- **Ось грида ↔ ось мира.** `Direction[idx]` — это `(dx, dy)` в координатах грида, где `(x, y)` совпадает с `(worldX, worldZ)` через `ArenaGrid.WorldToCell`. Потребитель сэмпла трактует `dir.x` как X-компоненту и `dir.y` как Z-компоненту в world-space.
- **Goal-клетка → `Direction = Vector2.zero`.** Достигнув seed-клетки, потребитель должен иметь fallback (например, `EnemyManager` падает на naive seek, чтобы враг не «прилипал» к центру). `Vector2.zero` также возвращается для недостижимых клеток (изолированный регион).
- **Dirty-цикл.** `MarkDirty()` → ближайший `Update()` пересобирает (если прошёл `RebuildIntervalSeconds`). События `Painted/PaintedSilent/TerritoryErased/ObstacleChanged` ставят dirty автоматически. Один rebuild за кадр максимум.

- **Препятствия как state клетки, не отдельный слой.** Источник правды — `ArenaGrid.Get(cell) == CellState.Obstacle`. `FlowFieldBuilder.Build` читает stateм через `NavigationConfig.CostFor`, который для `Obstacle` возвращает `NavigationConfig.ObstacleCost` (`int.MaxValue/32`). Билдер дополнительно ставит явные guard'ы: (1) seed-клетка с cost ≥ `ObstacleCost` пропускается (цель внутри препятствия не сидируется), (2) Dijkstra не релаксирует соседа с cost ≥ `ObstacleCost` (защита от теоретического overflow в `dist + step`), (3) direction-фаза для obstacle-клетки даёт `Vector2.zero` (враги не «прилипают» к препятствию, а откатываются на naive seek в `EnemyManager`). Один источник правды — добавление/снятие препятствий зовёт `ArenaState.MarkObstacleCells`/`UnmarkObstacleCells`, навигатор пересобирает поле через `ObstacleChanged` → dirty.
- **Goals.** Если `_goals` пуст или все binding'и дают неинициализированный fallback — навигатор подставит одну цель = `FloorCenterXZ`, weight=1, чтобы поле всё равно строилось (smoke-friendly).
- **Чтение направления — без аллокаций.** `SampleDirection(worldXZ)` — один проектор + один index lookup. Безопасно вызывать каждый кадр для всех врагов.

## Точки расширения

- **Билинейный сэмпл.** `FlowFieldNavigator.SampleDirection` сейчас читает направление из клетки, в которой стоит точка. Плавнее — взвешенный сэмпл по 4 ближайшим клеткам по UV внутри клетки. Локальная замена в одном методе.
- **Несколько навигаторов под разные классы врагов.** Создать отдельный `FlowFieldNavigator` с другим `NavigationConfig` (например, у бегунов `TerritoryCost=1` — ломятся напрямую) и привязать через `EnemyManager._navigator` (или через `EnemyConfig` per-type, если делать раздельные навигаторы — тогда `EnemyManager` хранит словарь `EnemyType → FlowFieldNavigator`).
- **GPU jump-flooding (Tier 1).** Триггер — профайлер: rebuild >2 мс или `GridResolution≥256`. Заменить `FlowFieldBuilder.Build` на compute-shader: integration в float-RT через jump-flood, direction в RGBA-RT, `SampleDirection` через `RenderTexture.GetData` или sample в GPU-потребителе. API навигатора не меняется.
- **Пер-цельные веса в инспекторе.** Сейчас `NavigationGoalBinding.Weight` — статичный slider. Расширение: `Func<float>`-провайдер веса (например, у игрока вес растёт при низком HP — толпа агрессивнее идёт за ним).
- **Динамические препятствия от MB вне `Floor.Obstacle`** (трупы громил, турели, временные стены). API готов: компонент в `Awake/OnEnable` собирает свои клетки и зовёт `ArenaState.MarkObstacleCells(cells)`, в `OnDestroy/OnDisable` — `UnmarkObstacleCells(cells)`. `FlowFieldNavigator` уже подписан на `ObstacleChanged`, поле пересобирается автоматически. Дополнительные слои в `FlowFieldBuilder` не нужны — препятствия уже унифицированы через `CellState.Obstacle`.

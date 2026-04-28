# Floor

## Назначение

Арена: CPU-сетка территории (`ArenaGrid`), GPU-маски (`PaintableFloor`), детекция замыкания петли, прогрессивная заливка enclosed-региона, расход краски, эрозия территории (внешняя). Реализует GDD §3.1, §3.2 (включая выедание API), §3.6 (источник «база»).

## Зависимости (asmdef)

Пусто — Floor самодостаточен, не ссылается на другие модули. Player ссылается **на** Floor, не наоборот; не вводить обратную зависимость.

## Ключевые типы

| Тип | Файл | Роль |
|---|---|---|
| `ArenaState` | `Domain/ArenaState.cs` | Оркестратор. `AppendTrailPoint`, `ResolveClosure`, `ClearActiveTrail`, `EraseTerritoryAt(worldXZ, radiusInCells)`. События: `TerritoryErased(worldXZ, worldRadius)` — стреляет в конце `EraseTerritoryAt`, сигнал dirty для производных структур (навигатора). Подписан на `PaintableFloor.Painted`/`PaintedSilent` для синка грида. |
| `ArenaConfig` | `Domain/ArenaConfig.cs` | SO. `GridResolution`, `FillTickInterval`. Меню: `Flood/Floor/Arena Config`. |
| `PaintableFloor` | `Domain/PaintableFloor.cs` | MonoBehaviour. Владеет `_paintRT`/`_lineRT`. `PaintAt(worldXZ)`/`PaintAt(worldXZ, worldRadius)`/`PaintAtSilent`/`PaintLineAt`/`EraseLineAt`/`EraseAt(worldXZ, worldRadius)`/`ClearLine`. Стреляет `event Painted(worldXZ, worldRadius)` (от обоих overload'ов `PaintAt`) и `event PaintedSilent(worldXZ, worldRadius)` (от `PaintAtSilent`, с расширенным радиусом). |
| `PaintableFloorConfig` | `Domain/PaintableFloorConfig.cs` | SO. `WorldSize`, `TextureResolution`, `BrushRadiusInTexels`, `FillBrushExtraRadiusInTexels`, `AgeCycleSeconds`. |
| `ArenaGrid` | `Domain/ArenaGrid.cs` | CPU-зеркало. `Get/Set/IsInside/WorldToCell/CellCenterWorld`. Состояния: `Empty/Territory/Line`. |
| `CellState` | `Domain/CellState.cs` | enum: `Empty`, `Territory`, `Line`. |
| `TrailRasterizer` | `Domain/TrailRasterizer.cs` | 4-связный Bresenham, помечает `Empty → Line`, детектирует касание `Territory` → `RasterResult.Close`. |
| `EnclosedRegionFinder` | `Domain/EnclosedRegionFinder.cs` | Static. BFS от краёв арены: непосещённые `Empty` = enclosed. |
| `FloodFillAnimator` | `Domain/FloodFillAnimator.cs` | Постоянно работающий painter. `AddPending`/`AddLineCleanup`. Фронт BFS на тик. Списывает краску из `PaintBank`, abort'ит заливку при обнулении. |
| `PaintBank` | `Domain/PaintBank.cs` | MonoBehaviour-счётчик краски. `TryConsume(int)`, `Add(int)`, `Current`, `Max`. Лог через `Debug.Log`. |
| `PaintRegenerator` | `Domain/PaintRegenerator.cs` | Debug-компонент пассивной регенерации (GDD §3.6 «база»). Тикает `PaintBank.Add`. |
| `FloorProjection` | `Domain/FloorProjection.cs` | Pure C#. World↔cell↔texel-проекция. Покрыт тестами. |
| `FloorPainter` | `Domain/FloorPainter.cs` | Дебаг-кисть для теста рендера, не геймплейный. |

## Контракты

- **Пайплайн закрытия:** `PlayerTrailEmitter → ArenaState.AppendTrailPoint → TrailRasterizer.AppendPoint → (на касании Territory) ArenaState.ResolveClosure → EnclosedRegionFinder.FindEnclosed → грид: Line/Enclosed → Territory → FloodFillAnimator.AddPending`. Painter крутится постоянно, подхватит pending сам. **Вырожденный кейс** (`enclosed.Count == 0`): claim не происходит — line-клетки возвращаются в `Empty` и стираются с `_lineRT`, аниматор не вовлекается. По GDD «нет области → нет расширения».
- **CPU↔GPU синхронизация.** Каждый стамп `PaintAt` стреляет `Painted`, каждый стамп `PaintAtSilent` — `PaintedSilent` (с world-радиусом расширенной кисти заливки). `ArenaState.OnPainted` подписан на оба события и маркирует Territory в гриде по диску радиуса. **Line-клетки фильтруются** — иначе bleed мазка PaintAtSilent у соседней inside-клетки превратил бы активный трейл в Territory. Это не ломает BFS-инвариант: бывшие-Empty-теперь-Territory клетки **внешние** к замыканию (никогда не были в `_pendingPaint`), а сидирование `FloodFillAnimator` идёт только из `_pendingPaint`.
- **`FloodFillAnimator` BFS-инвариант.** Сидируется только inside-клетка с уже-закрашенным Territory-соседом. Line-клетка сидом быть не может — фронт идёт от существующей территории. Conduit-исключение: line-клетка с закрашенным соседом (концом у базы) пускает фронт через себя на изолированный inner-регион при самопересечении. Seed-фаза прогоняется **каждый тик**, не только при пустом фронте — это даёт параллельную заливку нескольким одновременно-живущим замыканиям (вторая петля сидируется сразу, не ожидая первой). `_inFront` дедупит cells уже стоящие в очереди, а `HasReachableNeighbor` требует не-pending закрашенного соседа — поэтому deeper-cells текущей волны не реседируются и анимация остаётся волной в каждой отдельной области.
- **Line-клетки не получают `PaintAtSilent`** — только `EraseLineAt`. Их визуально покрывает соседний inside-стамп с расширенным радиусом (`BrushRadiusInTexels + FillBrushExtraRadiusInTexels`).
- **Расход краски** (GDD §3.1) списывается ровно перед `floor.PaintAtSilent(world)` в `FloodFillAnimator.ProcessTick`. Line-клетки бесплатны. На отказе `PaintBank.TryConsume` — `AbortFill`: `_pendingLines` стираются с `_lineRT` **и** возвращаются в `Empty` в гриде (без этого Territory без визуала была бы невидимой стеной для игрока/врагов), `_pendingPaint` откатываются в `Empty`, `_front`/`_inFront` обнуляются. Уже закрашенные клетки (вышедшие из `_pendingPaint` через успешный `PaintAtSilent`) остаются Territory. Активный (ещё не замкнутый) трейл (`Line` вне pending-сетов) не трогается.
- **`ArenaState.ClearActiveTrail`** обнуляет **все** `Line`-клетки грида, не только pending. Использовать на смерти игрока (GDD §3.4 «текущий след теряется»), не на abort заливки.
- **`ArenaState.EraseTerritoryAt(worldXZ, radiusInCells)`** обновляет грид (`Territory → Empty` в радиусе клеток) и вызывает `PaintableFloor.EraseAt` с эквивалентным мировым радиусом. `Line`-клетки **не** трогает (трейл игрока не разрывается врагами, GDD §3.5). Идемпотентна.

## Точки расширения

- Шахты/бутыльки (GDD §3.6) — отдельные MonoBehaviour-источники, тикают в `PaintBank.Add`. Шахта-в-зоне vs шахта-в-пустоте — проверка через `ArenaGrid.Get(WorldToCell(...))`.
- Дыры в заливке вокруг врагов (GDD §3.5) — субтракт радиуса вокруг каждой клетки врага из `enclosed` в `ResolveClosure` перед `_animator.AddPending`. Список координат — снимок из `Enemy.EnemyManager` (см. модуль Enemy «Точки расширения»).
- Batched-эрозия (Tier 1, см. Enemy/CLAUDE.md «Производительность») — при N≥500 врагов перевести `EraseAt` на массив центров: shader-side uniform-array + новый `PaintableFloor.EraseBatch(buffer)`. Локальная замена, не ломает API.

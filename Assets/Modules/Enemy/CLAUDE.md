# Enemy

## Назначение

Враги MVP-объёма (GDD §4.1, без Heavy и масла): спавн на периметре, наивное движение к центру арены. Territory — **барьер**, не штрафной пол: враги не входят на неё из пустоты, упираются в границу и продвигаются за счёт эрозии (грызут спереди). Урон от территории применяется только если враг оказался стоящим на ней (кейс замыкания вокруг).

## Зависимости (asmdef)

- `Floor` — `ArenaState.EraseTerritoryAt`, `ArenaGrid.Get/IsInside/WorldToCell`, `CellState.Territory`, `PaintableFloor.FloorCenterXZ/WorldSize`.
- `Navigation` — `FlowFieldNavigator.SampleDirection` для выбора направления движения толпы.

## Ключевые типы

| Тип | Файл | Роль |
|---|---|---|
| `EnemyType` | `Domain/EnemyType.cs` | enum: `Infantry`, `Runner`. |
| `EnemyConfig` | `Domain/EnemyConfig.cs` | SO per type: `MaxHp`, `MoveSpeed`, `EatRadiusCells`. Меню: `Flood/Enemy/Enemy Config`. |
| `Enemy` | `Domain/Enemy.cs` | MB-data-holder: HP, конфиг, ссылка на префаб (для пула). Логика тиков и движения — в Manager. |
| `EnemyManager` | `Domain/EnemyManager.cs` | Список + пул по префабам. Один `Update`: движение всех + 1Hz тик (damage→erosion). `Spawn(prefab, config, pos)` / `Return(enemy)`. Опциональная ссылка `_navigator` (Navigation/FlowFieldNavigator) — если задана, направление берётся из flow-field; иначе naive seek к центру арены. |
| `EnemySpawner` | `Domain/EnemySpawner.cs` | Дебаг: периодический спавн префаба с конфигом по периметру арены, кап `_maxAlive`. |
| `EnemyRadiusSpawner` | `Domain/EnemyRadiusSpawner.cs` | Дебаг: спавн в случайной точке кольца `[innerRadius, radius]` вокруг собственной позиции. `_perTick` штук за интервал. Альтернатива `EnemySpawner` для локального теста толпы. |
| `EnemySpatialHash` | `Domain/EnemySpatialHash.cs` | Pure C# uniform-grid bin'инг по XZ. `Rebuild(active, cellSize)` + `Query(pos, radius, output)` копирует кандидатов в переданный буфер. Используется `EnemyManager` под separation/slowdown/pressure-erosion. Без аллокаций после прогрева (словарь bin'ов и pool списков переиспользуются). |

## Контракты

- **Tier 0 архитектура** (≤200 врагов): manager-driven loop, не per-MB Update; пул GameObject'ов по префабу; reverse-iter тик с `RemoveAt(i)`. См. ниже секцию «Производительность».
- **Выбор направления — flow-field, блок Territory сохраняется.** Если `_navigator` задан и поле построено, `MoveAll` сэмплит `Vector2 SampleDirection(pos)` из `FlowFieldNavigator` (cost-aware маршрут с учётом весов целей и стоимостей `CellState`). На zero-vector (цель/недостижимо) — fallback на прямой seek, чтобы враг не зависал. Контракт «Territory блокирует шаг» **не меняется**: поле лишь предлагает направление, шаг всё равно отменяется при `newCell == Territory`, эрозия вытачивает проход. Без `_navigator` — старая naive-seek логика к `floorCenter`.
- **Territory — барьер с 4-fallback sliding.** Шаг применяется по детерминированному порядку: 1) полный, 2) только X, 3) только Z, 4) перпендикуляр ccw, 5) перпендикуляр cw. Первый успех — применяется и движение завершается. Если все варианты упираются в Territory — враг стоит, ждёт эрозии в `Tick` (и отдельный кейс заморозки при попадании заливкой — центральная клетка стала Territory). Детерминированный порядок важен: без фиксации толпа на углу джиттерит между ccw и cw. Per-enemy «предпочитаемая сторона» не сохраняется — лишний state, не нужен на джем-объёме.
- **Crowd separation (Boids).** В `MoveAll` к выбранному `dir` (flow-field или fallback-seek) прибавляется сумма отталкивающих векторов от соседей в `_separationRadius` с весом `_separationWeight`: `dir = normalize(dir + sep * weight)`. Falloff `radius/d − 1` — резко растёт при малых `d`, ноль на границе. Sliding применяется уже к смешанному направлению, поэтому Territory всё равно блокирует — separation не толкает в стену, просто меняет угол подхода. `_separationWeight = 0` или `_separationRadius = 0` — feature off. Большой `_separationWeight` (>≈1.5) без crowd-slowdown'а вызывает «бурление» — толпа осциллирует на стыке flow и sep сил.
- **Crowd-aware speed scaling.** Эффективная скорость врага = `MoveSpeed / (1 + _crowdSlowdownFactor * neighbors)`, где `neighbors` — число других врагов в `_crowdSlowdownRadius`. Solo (`neighbors=0`) → full speed. Главный гаситель boiling'а: в плотной толпе скорость падает → импульс маленький → soft-separation легко удерживает стабильную упаковку без осцилляций. Передние ряды толпы (мало соседей) идут полным ходом, задние тормозят и не пихают вперёд. `_crowdSlowdownFactor = 0` — feature off. Радиус обычно ≤ `_separationRadius` — замедляет именно «застрявших в спине».
- **Spatial hash.** `_spatialHash` (uniform-grid bin'ы по XZ) перестраивается раз в кадр в начале `Update`, до `MoveAll`/`Tick`. CellSize = `max(_separationRadius, _crowdSlowdownRadius, _pressureRadius)` (минимум 0.5) — гарантирует что один Query трогает ≤4 bin'а. `MoveAll` делает один Query на врага под `max(sep, slow)` радиус и считает обе фичи в одном проходе по `_queryBuffer`. `Tick.CountOtherNeighborsXZ` — отдельный Query под `_pressureRadius`. Стейл-позиции в Tick (на ~1 dt) безвредны: max сдвиг между rebuild и tick = `MoveSpeed * dt` < 0.05u.
- **Эрозия идёт каждый Tick безусловно с pressure-bonus'ом.** На Empty/Line-соседях `EraseTerritoryAt` — no-op (фильтр `if (Get == Territory)` внутри). Базовый радиус — `EatRadiusCells`, скейлится по плотности толпы: `effectiveRadius = base + floor(others * _pressureBonusPerNeighbor)`, где `others` — число **других** врагов в `_pressureRadius` (исключая себя). Solo (`others=0`) → bonus=0, поведение совпадает с константным радиусом. `_pressureBonusPerNeighbor=0` или `_pressureRadius=0` — feature off.
- **Damage применяется только когда враг стоит на `Territory`** — обычный поток (упёрся в границу, грызёт) урона не даёт. Кейс замыкания вокруг → 1HP/тик пока не выкарабкается.
- **Порядок тика:** сначала damage, потом erosion (логика деспавна должна сработать до того, как враг съест клетку под собой).
- **Враг на `Line`-клетке** не получает урона и не эродирует Line — `Line ≠ Territory`. Активный трейл игрока не разрывается (GDD §3.5).
- **`EnemyManager.Return`** — единственный путь деспавна. `Enemy.Despawn()` — обёртка. После `Return` объект в пуле, `gameObject.SetActive(false)`. Нельзя `Destroy(enemy.gameObject)` руками — пул оставит висячую ссылку.
- **`Enemy.Init`** обязателен сразу после `Spawn` — менеджер вызывает сам. Голый `Instantiate` без `Init` — UB.

## Производительность (Tier 0 / Tier 1)

**Сейчас (Tier 0):** разово каждый враг при тике делает один `PaintableFloor.EraseAt` → два `Graphics.Blit`. На N=40 это ~80 drawcall/с — без профайлера. На N=200 — ~400/с, всё ещё ОК. Все neighbor-запросы (separation, slowdown в `MoveAll`; pressure-erosion в `Tick`) идут через `EnemySpatialHash` — амортизированно O(N) на кадр и O(N) на тик независимо от плотности толпы (cell sized под max query radius, ≤4 bin'а на запрос). Пул убирает GC от спавна/деспавна; reverse-iter держит mass-кулл волны O(k).

**Когда переходить на Tier 1:** если профайлер показывает `Graphics.Blit` >5% кадра, или эмпирически N≥1000 и FPS просел. Симптом — рендертред в потолке при тике врагов, не в основной логике. Spatial hash снимает квадратичную нагрузку, потолок теперь у GPU-блитов, не у neighbor-count.

**Что менять в Tier 1:** один batched-блит вместо N. Технически:
- Расширить `FloorBrush.shader` под массив центров (`StructuredBuffer<float4>` или uniform-array на 64 точки).
- В `EnemyManager.Tick` собрать все `(worldXZ, radius)` в буфер, дернуть `PaintableFloor.EraseBatch(buffer)` один раз.
- `ArenaState.EraseTerritoryAt` остаётся на CPU (грид) и вызывает уже batched-API на GPU-стороне.

Замена локальная — не трогает `Enemy`/`EnemyManager.Tick` снаружи, только тело Tick.

**Tier 2 (DOTS/Burst, ECS-инстансинг):** только при N≥5000. Для жанра нереалистично, не закладываем как roadmap.

## Точки расширения

- Heavy + масло (GDD §3.3, §4.1) — добавить `IgnoresPaintDamage` в `EnemyConfig`, развилку в `EnemyManager.Tick`. Инструмент масла — отдельный модуль/компонент игрока.
- Дыры в петле вокруг врагов (GDD §3.5) — `EnemyManager.GetEnemyCells()` снимок + субтракт в `ArenaState.ResolveClosure` перед `_animator.AddPending`.
- Волны (GDD §4.2) — отдельный `WaveDirector`, гасит `EnemySpawner` и сам управляет очередями + телеграфом. Сектор атаки — параметр спавна (диапазон углов вместо случайной стороны).
- Контактный урон игрока (GDD §3.4) — Trigger-collider на враге, событие в `Player`.
- AI лучше naive seek — flow-field уже подключается через модуль `Navigation`. Дальнейшие шаги: бесшовный сэмпл с билинейной интерполяцией 4 соседних клеток для плавных траекторий у границ; раздельные навигаторы под разные классы врагов (например, у бегунов выше `TerritoryCost` — обходят, у громил низкий — ломятся напрямую).
- **Hard push relaxation поверх separation.** Если `_separationWeight` максимальный, а толпа всё равно перекрывается в очень плотных кейсах — добавить второй проход после `MoveAll`: для каждой пары в радиусе раздвинуть на половину overlap'а (с проверкой `Territory` для конечной точки). Жёстче, чем градиентное отталкивание, но дороже на 0.5×O(N²) за кадр. Включается опционально; сейчас не нужно.
- **B — Density-modulated flow-field** (если A+C недостаточно). В `Navigation.FlowFieldBuilder` принимать density-карту по позициям врагов: `cost = base + k * density²`. Толпа сама расползается по альтернативным маршрутам; полезно когда есть несколько проходов одинаковой длины и хочется избежать «слипания в один». Локализуется в Navigation, не трогает Enemy.
- **D — Hydraulic wear.** Per-cell wear-счётчик в `Floor.ArenaGrid` (или параллельной структуре), инкрементируется на проход врага. Клетки с `wear > threshold` эродируются ускоренно — пробитые тропы становятся постоянными «руслами». Нужно для долгоиграющих визуальных следов и геймплейного «помни, куда хлынули». Расширяет Floor, не Enemy.

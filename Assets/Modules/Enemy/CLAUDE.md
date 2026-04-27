# Enemy

## Назначение

Враги MVP-объёма (GDD §4.1, без Heavy и масла): спавн на периметре, наивное движение к центру арены. Territory — **барьер**, не штрафной пол: враги не входят на неё из пустоты, упираются в границу и продвигаются за счёт эрозии (грызут спереди). Урон от территории применяется только если враг оказался стоящим на ней (кейс замыкания вокруг).

## Зависимости (asmdef)

- `Floor` — `ArenaState.EraseTerritoryAt`, `ArenaGrid.Get/IsInside/WorldToCell`, `CellState.Territory`, `PaintableFloor.FloorCenterXZ/WorldSize`.

## Ключевые типы

| Тип | Файл | Роль |
|---|---|---|
| `EnemyType` | `Domain/EnemyType.cs` | enum: `Infantry`, `Runner`. |
| `EnemyConfig` | `Domain/EnemyConfig.cs` | SO per type: `MaxHp`, `MoveSpeed`, `EatRadiusCells`. Меню: `Flood/Enemy/Enemy Config`. |
| `Enemy` | `Domain/Enemy.cs` | MB-data-holder: HP, конфиг, ссылка на префаб (для пула). Логика тиков и движения — в Manager. |
| `EnemyManager` | `Domain/EnemyManager.cs` | Список + пул по префабам. Один `Update`: движение всех + 1Hz тик (damage→erosion). `Spawn(prefab, config, pos)` / `Return(enemy)`. |
| `EnemySpawner` | `Domain/EnemySpawner.cs` | Дебаг: периодический спавн префаба с конфигом по периметру арены, кап `_maxAlive`. |

## Контракты

- **Tier 0 архитектура** (≤200 врагов): manager-driven loop, не per-MB Update; пул GameObject'ов по префабу; reverse-iter тик с `RemoveAt(i)`. См. ниже секцию «Производительность».
- **Territory — барьер.** В `MoveAll`: если **новая клетка** — `Territory`, шаг отменяется **всегда**, без исключений по текущей клетке. Если врага накрыло заливкой (его центральная клетка стала `Territory`) — он заморожен до 1Hz `Tick`, который съест клетку под ним (eat-радиус включает центр) и тем самым откроет Empty. Без этого строгого правила «уже на Territory → ходи свободно» делает закрашенную петлю прозрачной для попавших внутрь врагов.
- **Эрозия идёт каждый Tick безусловно** — это и есть механизм продвижения. На Empty/Line-соседях `EraseTerritoryAt` — no-op (фильтр `if (Get == Territory)` внутри). Радиус — `EatRadiusCells` вокруг позиции врага.
- **Damage применяется только когда враг стоит на `Territory`** — обычный поток (упёрся в границу, грызёт) урона не даёт. Кейс замыкания вокруг → 1HP/тик пока не выкарабкается.
- **Порядок тика:** сначала damage, потом erosion (логика деспавна должна сработать до того, как враг съест клетку под собой).
- **Враг на `Line`-клетке** не получает урона и не эродирует Line — `Line ≠ Territory`. Активный трейл игрока не разрывается (GDD §3.5).
- **`EnemyManager.Return`** — единственный путь деспавна. `Enemy.Despawn()` — обёртка. После `Return` объект в пуле, `gameObject.SetActive(false)`. Нельзя `Destroy(enemy.gameObject)` руками — пул оставит висячую ссылку.
- **`Enemy.Init`** обязателен сразу после `Spawn` — менеджер вызывает сам. Голый `Instantiate` без `Init` — UB.

## Производительность (Tier 0 / Tier 1)

**Сейчас (Tier 0):** разово каждый враг при тике делает один `PaintableFloor.EraseAt` → два `Graphics.Blit`. На N=40 это ~80 drawcall/с — без профайлера. На N=200 — ~400/с, всё ещё ОК. Пул убирает GC от спавна/деспавна; reverse-iter держит mass-кулл волны O(k).

**Когда переходить на Tier 1:** если профайлер показывает `Graphics.Blit` >5% кадра, или эмпирически N≥500 и FPS просел. Симптом — рендертред в потолке при тике врагов, не в основной логике.

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
- AI лучше naive seek — A* по гриду (используя `ArenaGrid` как карту проходимости, Territory = непроходимо до съедания).
- Per-axis sliding на границе Territory — сейчас блок полный (шаг отменяется). Косметика: проверять X- и Z-компоненты по отдельности, чтобы враг скользил вдоль кромки в проход. Не нужно для функциональности, только сглаживает движение в углах.

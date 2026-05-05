---
id: arena
title: Арена
status: live
gdd_section: "4.3"
code_refs:
  - Assets/Modules/Floor/Domain/ArenaConfig.cs
  - Assets/Modules/Floor/Domain/PaintableFloor.cs
  - Assets/Modules/Floor/Domain/ArenaGrid.cs
  - Assets/Modules/Floor/Domain/Obstacle.cs
last_synced: 2026-05-05
owner: shared
---

# Арена

Игровое поле фиксированной раскладки. На MVP — одна арена. Камера — adaptive zoom-out (см. [pillars](../pillars.md)): статична на маленькой петле, отъезжает при крупной, удерживая «обзор как на одном экране».

## Размеры

> SoT: code — фактический размер мира берётся из `bounds` сценного рендерера (`PaintableFloor.WorldSize`), не из конфига.

> SoT: design — целевые размеры зафиксированы в [meta/perf-budget](../meta/perf-budget.md): мир **100×100 world units**, grid **500×500** (целевой) / **256×256** (минимум приёмки). Сцена должна совпадать.

- **Размер мира** — авторитет: `_targetRenderer.bounds.size` по XZ. Не дублируется в `ArenaConfig`. Целевой размер на MVP — 100×100 (Plane scale = {10, 1, 10}).
- **Сетка территории** — квадратная, `ArenaConfig.GridResolution × GridResolution`. Целевой дефолт после scale-up — **500 × 500** (клетка ≈ 0.2 ед.). Текущее значение в проекте — 64 × 64 (наследие до scale-up).
- **Маска пола (RT)** — плотность `ArenaConfig.TexelsPerWorldUnit` текселов на мировую единицу. Текущий дефолт **2.56**: на 100×100 → RT 256×256. Возможно повышение до 5.12 (RT 512×512) — открытый вопрос, см. [perf-budget](../meta/perf-budget.md).
- **Соотношение сетки к RT** — производное; при grid=500 и density=2.56 RT в 2× меньше grid, что может потребовать тюнинга density.

| Параметр | Текущий | Целевой (после scale-up) | Где |
|---|---|---|---|
| `GridResolution` | 64 | **500** (минимум 256) | `ArenaConfig` |
| `TexelsPerWorldUnit` | 2.56 | TBD (2.56 или 5.12) | `ArenaConfig` |
| `BrushRadiusInTexels` | 2 | 2 (без изменений) | `ArenaConfig` |
| `FillBrushExtraRadiusInTexels` | 4 | 4 (без изменений) | `ArenaConfig` |
| `AgeCycleSeconds` | 60 | 60 (без изменений) | `ArenaConfig` |
| World mesh scale | {2,1,2} (≈20×20) | **{10,1,10} (100×100)** | `Assets/Scenes/New Scene.unity` Plane |

## Границы

Edge-коллайдеры по периметру `WorldSize`. Физика не выпускает игрока наружу. Бесшовно с механикой замыкания: касание следом края арены = валидное замыкание (см. [painting](painting.md)).

## База

Стартовая закрашенная зона в центре арены. Одна центральная база (не «филиалы»), радиус **≈4–6 ед.** на 100×100 карте — иначе путь от центра к краю слишком длинный для микроцикла.

- Источник пассивной регенерации краски (см. [resources](resources.md), `PaintRegenerator`).
- Точка респауна игрока после смерти (см. [player-hp](player-hp.md), TODO).

> SoT: design — радиус 4–6 ед. зафиксирован интервью 2026-05-05 как функция от размера карты. При изменении размера мира — пересмотреть.

> TODO: точная форма (круг / квадрат / комбинация) и положение в сцене — задаётся вручную в сцене на этапе scale-up.

## Ресурсы на карте

> SoT: design — на MVP сохраняем 12 предопределённых точек / 2 шахты краски + 1 шахта масла. Расстановка ручная в сцене, перераспределённая по 100×100 площади.

- 2 шахты краски + 1 шахта масла в 3 из 12 предопределённых точек.
- Бутыльки: появляются раз в 20–30 сек в случайной точке на расстоянии от базы.
- Точки разнесены по большей площади после scale-up — дальние шахты = больше риска = больше дизайн-награды.

> SoT: design — спавнер шахт по 12 точкам в коде не делаем в этой задаче. На MVP-объёме (≤4 источника) ручная расстановка префабов в сцене — рабочая опция. Прежний DISPUTED закрыт ответом интервью 2026-05-05 (Q4.2). Если плейтест покажет, что 12 точек на 100×100 ощущаются нечестно — отдельный тикет на `MineSpawner` / `BottleSpawner` в `Assets/Modules/Floor`.

## Препятствия

Клетки `CellState.Obstacle` — стены для BFS, флоу-филда и трейла. Источник правды — `Obstacle` MonoBehaviour на сцене (агрегирует AABB своих коллайдеров и помечает соответствующие клетки).

> SoT: code — `Assets/Modules/Floor/CLAUDE.md`, контракт «Препятствия как стены BFS бесплатно».

Препятствия не входили в исходный §4.3 монолита — добавлены в коде.

## Что НЕ в скоупе

- Несколько арен / биомов (см. [meta/out-of-scope](../meta/out-of-scope.md)). GPU-compute pipeline scale-up закладывает техоснову, но скоуп MVP — одна арена.
- Процедурная генерация раскладки.
- Динамические границы (изменение размера арены за забег).
- Tier 1 batched-блит для эрозии (≥500 врагов) — отдельная задача после scale-up, см. [perf-budget](../meta/perf-budget.md).

## Связи

- [meta/perf-budget](../meta/perf-budget.md) — числовые потолки и стратегия оптимизации (ResolveClosure на GPU, jump-flood для flow-field).
- [pillars](../pillars.md) — adaptive zoom-out как pillar обзора.
- [Todo/scale-and-optimize-map](../../Todo/scale-and-optimize-map.md) — задача реализации scale-up в worktree `unity`.

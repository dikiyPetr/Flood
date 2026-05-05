---
id: resources
title: Ресурсы
status: live
gdd_section: "3.6"
code_refs:
  - Assets/Modules/Floor/Domain/ResourceId.cs
  - Assets/Modules/Floor/Domain/ResourceBankBase.cs
  - Assets/Modules/Floor/Domain/PaintBank.cs
  - Assets/Modules/Floor/Domain/ResourceBankAccumulator.cs
  - Assets/Modules/Floor/Configs/ResourceBankAccumulatorConfig.cs
  - Assets/Modules/Floor/Domain/ResourceMine.cs
  - Assets/Modules/Floor/Configs/ResourceMineConfig.cs
  - Assets/Modules/Floor/Domain/ResourceBottle.cs
  - Assets/Modules/Floor/Configs/ResourceBottleConfig.cs
  - Assets/Modules/Floor/Domain/Pickable.cs
  - Assets/Modules/Floor/Domain/PaintRegenerator.cs
last_synced: 2026-05-05
owner: shared
---

# Ресурсы

Два ресурса: **краска** и **масло**. Краска — основная механика (см. [painting](painting.md)). Масло — антитолпа/антибосс (см. [oil](oil.md)).

## Типы

| Ресурс | Назначение | Дефицит |
|---|---|---|
| Краска | Рисование (бесплатно) + заливка (1/клетка) | Тратится постоянно, регенерация через базу и шахты |
| Масло | Антитолпа и антибосс | Критически редкий, ~1–2 за забег |

Перечисление: `ResourceId = { Paint, Oil }`.

> SoT: code — `Assets/Modules/Floor/Domain/ResourceId.cs`.

## Источники

> SoT: design — таблица соответствует `ResourceMineConfig`/`ResourceBottleConfig` дефолтам, но фактические `.asset`-значения могут отличаться.

| Источник | Выдача | Интервал |
|---|---|---|
| База (центр) | 1 ед. краски | каждые 2 сек |
| Шахта краски | 3 ед. краски | каждые 10 сек |
| Шахта масла | 1 ед. масла | каждые 30 сек |
| Бутылёк краски | 10 ед. краски | разово |
| Бутылёк масла | 2 ед. масла | разово |

> DISPUTED: интервал базовой регенерации (1 ед. / 2 сек) задаётся параметрами `PaintRegenerator` MB на сцене и `ResourceBankAccumulatorConfig.TickInterval` (дефолт 0.1 сек). Конкретный rate в коде не пинится — нужно сверить значения `.asset`-ассета базы. Owner: code. Предложение: завести `BaseRegenConfig` SO с явным `AmountPerSecond`, чтобы дизайнер видел число в одном месте.

## Архитектура

### Банк (`ResourceBankBase`)

Счётчик ресурса с лимитом. `Current`, `Max`, `TryConsume(int)`, `Add(int)`. Реализации: `PaintBank` (для краски). Будущая `OilBank` — точка расширения.

### Аккумулятор (`ResourceBankAccumulator`)

Один на банк. Принимает rate'ы (ед./сек) от источников через `SetRate(MonoBehaviour, float)`, интегрирует во внутренний float-сток, раз в `TickInterval` сек докидывает целую часть в банк.

> SoT: code — `Assets/Modules/Floor/CLAUDE.md`, контракт «Источники с непрерывной выдачей не пишут в банк напрямую».

Это даёт:

1. Единый авторитетный rate для UI (`CurrentRatePerSecond`).
2. Плавное «капание» в банк вместо скачков.
3. Конфигурируемую частоту обновления отдельно от частоты тика самой шахты.

`AddBurst(int)` — one-shot вклад в обход rate-учёта (например, флаш void-стока шахты при включении в петлю).

### Источники

| Источник | MB | Поведение |
|---|---|---|
| База | `PaintRegenerator` | Регистрирует постоянный rate `_amountPerTick / _intervalSeconds` в аккумулятор |
| Шахта в `Territory` | `ResourceMine` | `SetRate(this, AmountPerTick / IntervalSeconds)` |
| Шахта в `Empty`/`Line` | `ResourceMine` | `SetRate(this, 0)` + копит float-сток с капом `VoidStockCap` (дефолт 15 ед.). На переходе в `Territory` — `AddBurst` на целую часть стока |
| Бутылёк | `ResourceBottle` + `Pickable` | На событие `Picked` — `Add(Amount)` + `Destroy(gameObject)` |

`ResourceMine` опрашивает `ArenaState.Grid.Get(_cell)` каждый кадр (не подписан на события — грид единый источник правды).

## Расхождения с GDD

> DISPUTED #2: бутылёк подбирается через коллизию игрока (`Pickable`-компонент), а не при включении в петлю. GDD §3.6 говорит: «бутылёк собирается только при включении в петлю». Owner: design. Предложение: либо добавить `EnclosureGate` поверх `Pickable.enabled` (флаг «активен после первого попадания клетки в `Territory`»), либо зафиксировать новую семантику в GDD как принятое изменение. Вторая опция проще, GDD-семантика затрудняет выдачу масла на удалённых бутыльках.

> SoT: code — реализация в `Assets/Modules/Floor/Domain/ResourceBottle.cs` явно подписана на `Pickable.Picked`, не на события `ArenaState`. Комментарий в `ResourceBottleConfig.cs` описывает GDD-семантику и сейчас вводит в заблуждение — кандидат на правку при разрешении DISPUTED.

## Правила сбора (дизайнерская модель)

- Шахта в `Territory` → ресурс идёт автоматически в банк.
- Шахта в `Empty`/`Line` → копит сток у себя; забирается замыканием петли вокруг.
- Бутылёк (фактически) → собирается при касании игроком; (дизайнерски) — при включении в петлю.

## Открытые вопросы

> TODO: визуальный фидбэк «шахта копит» vs «шахта выдаёт» — пока неочевиден игроку.

> TODO: появление бутыльков на карте (раз в 20–30 сек) пока не автоматизировано — спавнер планировался, не реализован. См. [arena](arena.md), DISPUTED.

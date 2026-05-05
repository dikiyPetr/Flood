---
id: perf-budget
title: Перформанс-бюджет
status: live
last_synced: 2026-05-05
owner: design
---

# Перформанс-бюджет

Источник правды для всех решений «можно ли так быстро/много/тяжело». Любое числовое perf-обещание в `systems/*.md` обязано ссылаться сюда. Если возникает конфликт — авторитет здесь.

## Таргет

> SoT: design — все цифры зафиксированы интервью 2026-05-05.

| Параметр | Значение |
|---|---|
| Платформа | **Standalone PC** (Windows). WebGL не цель MVP. |
| Целевой FPS | **60 FPS** стабильно |
| Минимально допустимый FPS | 45 FPS на пиковых событиях (волна + замыкание + пиковая толпа) |
| Допустимый stutter | 0 фреймов на любое игровое событие, включая замыкание петли |

## Потолки контента

| Сущность | Потолок | Комментарий |
|---|---|---|
| Враги одновременно | **≤200** (Tier 0) | Подтверждено в [enemies](../systems/enemies.md). Tier 1 (≥1000) — отдельная задача после scale-up. |
| Размер мира | **100×100 world units** | Plane scale = {10, 1, 10}. Pillar — adaptive zoom-out (см. [pillars](../pillars.md)). |
| Grid resolution | **500×500 — целевое; 256×256 — минимум приёмки** | См. [arena](../systems/arena.md). При невозможности 500 — задокументировать узкое место и фактический потолок. |
| RT (маска пола) | производное от `TexelsPerWorldUnit` | TBD по факту реализации. При density=2.56 на 100×100 → 256×256 (1 МБ ARGB32). При повышении density до 5.12 → 512×512 (4 МБ). |
| Шахты | 12 предопределённых точек | Разнесены по большей площади после scale-up; ручная расстановка префабов. |

## Стратегия оптимизации

> SoT: design — следствия Q1.4 («поднять разрешение grid до 256+») и Q3.2 («GPU всю цепь замыкания»).

| Подсистема | Решение | Триггер |
|---|---|---|
| `ResolveClosure` (поиск охваченной области) | **GPU compute** — два прохода BFS по grid-маске на compute-shader | Обязательно при `GridResolution≥256`. |
| `FloodFillAnimator` (заливка) | **GPU compute** — параллельная волна по фронту, ping-pong RT | Обязательно при `GridResolution≥256`. Сохранить `FillTickInterval` как gating для расхода краски. |
| `FlowFieldNavigator` (Dijkstra) | **GPU jump-flooding** — compute-shader, integration в float-RT | Обязательно при `GridResolution≥256`, см. `Assets/Modules/Navigation/CLAUDE.md`. |
| Эрозия `EraseAt` (per-enemy blit) | оставить как есть (Tier 0) | Tier 1 batched-blit — пост-scale-up при N≥500 врагов. |

## Допустимо / недопустимо

**Допустимо:**
- Rebuild flow-field every-frame на dirty — при условии GPU jump-flood.
- RT-память до **8 МБ** на маски пола суммарно (paint + line + любые служебные).
- 1 compute-dispatch на замыкание (ResolveClosure) и непрерывные dispatch'и на флуд-филле.

**Недопустимо:**
- Stop-the-frame stutter на замыкании. Замыкание петли — частое событие; любой stutter ломает игровой ритм.
- CPU full-grid scan каждый кадр на `GridResolution≥256` (текущий `ResolveClosure` придётся снять с CPU).
- Аллокации в Update'ах горячих путей (стандарт для всего кодбаза, см. модульные `Assets/Modules/*/CLAUDE.md`).

## Открытые вопросы

> TODO: точный `TexelsPerWorldUnit` для 100×100 + grid 500. Дефолт 2.56 даёт RT ~ 256×256 — это в 2× меньше grid; возможно, надо повысить до 5.12 (RT 512×512). Решение фиксируется по факту реализации.

> TODO: функция adaptive zoom камеры (привязка к радиусу активной петли / к скорости / к проценту арены) — открытый вопрос для плейтеста.

> TODO: «фактический потолок» grid — если 500×500 не дотянет до 60 FPS, зафиксировать в этом файле найденное безопасное разрешение и узкое место.

## Связи

- [arena](../systems/arena.md) — целевые размеры мира и сетки.
- [enemies](../systems/enemies.md) — потолок врагов и Tier-архитектура.
- [pillars](../pillars.md) — pillar adaptive zoom-out.
- [Todo/scale-and-optimize-map](../../Todo/scale-and-optimize-map.md) — чеклист реализации.

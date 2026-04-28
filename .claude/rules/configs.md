---
paths:
  - "Assets/Modules/**/Domain/*.cs"
  - "Assets/Modules/**/Configs/*.cs"
---

# Конфиги (ScriptableObject)

Тюнинг MonoBehaviour'ов — в SO-конфигах. На MB — сценные ссылки и runtime-state.

## Что выносить

**В конфиг:** всё, что не привязано к конкретному экземпляру в сцене — тюнинг, ссылки на ассеты и префабы, ссылки на другие конфиги. Дефолт — inline в поле конфига.

**У MonoBehaviour:**
- сценные ссылки (`MonoBehaviour`/`Transform`/`Renderer`);
- runtime-state — счётчики, аккумуляторы, кэши;
- сама ссылка на конфиг.

Малое число полей не повод оставлять тюнинг на MB. Конфиг группируется по доменному назначению, не по имени MB; может быть общим для нескольких MB. На сценные объекты конфиг не ссылается.

## Расположение и именование

- Класс: `Assets/Modules/<Module>/Configs/<Name>Config.cs`.
- `.asset`: `Assets/Modules/<Module>/Configs/Assets/<Variant>.asset`.
- Имя по теме (`CrowdConfig`) или по MB 1:1 (`EnemyManagerConfig`).
- Namespace модульный, как у Domain.
- Обязателен `[CreateAssetMenu]` с путём `Flood/<Module>/...`.

## Стиль и подключение

- `public sealed class <Name>Config : ScriptableObject`.
- Приватное `_field` + public read-only property, дефолт inline.
- Валидаторы (`Min`, `Range`) — в одном атрибуте через запятую.
- `[Tooltip]` — если значение неочевидно.
- Setter'ы — только `internal void SetForTests(...)`.
- На MB подключается через `[SerializeField]`; ранний выход на `null`.
- В горячем цикле — копия в локальную переменную перед `for`.

## Анти-паттерны

- Загружать через `Resources.Load`/`Addressables`.
- `static`-конфиги, singleton'ы.

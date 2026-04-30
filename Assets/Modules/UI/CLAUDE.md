# UI

## Назначение

Презентационный слой над gameplay-данными. На MVP — view для банков ресурсов (`ResourceBankView`). Не владеет состоянием игры; только отображает.

## Зависимости (asmdef)

- `Floor` — `ResourceBankBase`, `ResourceId` для типизации ссылок и форматирования.
- `Unity.TextMeshPro` — `TMP_Text` для текстовых лейблов.

## Ключевые типы

| Тип | Файл | Роль |
|---|---|---|
| `ResourceBankView` | `Domain/ResourceBankView.cs` | MonoBehaviour. Раз в кадр опрашивает `ResourceBankBase` (current/max) и `ResourceBankAccumulator.CurrentRatePerSecond` (rate). Пишет результат в `TMP_Text` и/или заполняет `Image.fillAmount`. Аккумулятор опционален — без него rate=0. |
| `ResourceBankViewConfig` | `Configs/ResourceBankViewConfig.cs` | SO. `Format` (string.Format аргументы: {0}=Resource, {1}=current, {2}=max, {3}=rate/sec). Меню: `Flood/UI/Resource Bank View Config`. |

## Контракты

- **View — read-only.** `ResourceBankView` только опрашивает банк и аккумулятор, никогда не вызывает `Add`/`TryConsume`/`SetRate`. Нет обратной связи UI → game state.
- **Скорость — авторитетная, не оценочная.** Берётся напрямую из `ResourceBankAccumulator.CurrentRatePerSecond` — это сумма зарегистрированных в аккумуляторе rate'ов от источников (шахты в зоне, база). View не считает дельту банка, не сглаживает. Если аккумулятор не подключён (`_accumulator == null`) — rate=0, лейбл всё равно работает.
- **Опциональные лейбл и бар.** `_label` и `_fillBar` независимы; любой из них может быть null, но не оба одновременно (иначе `Start` логирует ошибку и `_ready=false`). При `_fillBar != null` и `Max > 0` заполнение = `current / max`.
- **Resource — енам, не локализованная строка.** В формате выводится `Resource.ToString()` (Paint/Oil). Локализация — вне MVP-объёма.

## Точки расширения

- Локализованное имя ресурса — словарь `ResourceId → string` в SO + перегруженный формат.
- Цветовая индикация низкого ресурса — порог в конфиге, переключение цвета `_label.color`/`_fillBar.color` на пересечении.
- Анимация изменения (tween бара, мигание лейбла на расходе) — отдельный `ResourceBankViewAnimator`, подписывается на изменения в `Update`.
- HP, таймер до волны (Day 3 GDD §5) — отдельные view-MB того же стиля, опрашивают свои источники (Player HP, WaveDirector). Общий шаблон: read-only `Update`-poller с конфигом форматирования.

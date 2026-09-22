# CaptureFlagClien — описание изменений

Ветка: `fix/CaptureFlagClien`
Дата: 19.09.2026

## Кратко

Исправлен клиентский рендер флага цели «Захват флага» (CaptureObjective) и добавлен
осмотр (examine), показывающий контролирующую фракцию. Раньше клиент ничего не рисовал:
в `ClientObjectiveCaptureSystem` после проверки `TryComp<AppearanceComponent>` был ранний
выход, а в самом конце применялось только `_sawmill.Debug`. Никакого реального изменения
спрайта не происходило. Дополнительно исправлена опечатка в дефолтном спрайт-стейте
opfor: `"uaflagworn"` (не существует в RSI) → `"uaflag_worn"`.

Дизайн — вариант A: сервер является единственным владельцем игрового состояния и
вычисляет спрайт-стейт, клиент лишь применяет полученное по сети значение через
`SpriteSystem.LayerSetRsiState`. Это соответствует правилу CONVENTIONS «у любого
состояния один владелец».

## Изменённые файлы и суть правок

### 1. `Content.CMU/Shared/Round/Objectives/Type/CaptureObjectiveComponent.cs`
Why/что:
- Добавлен `[AutoGenerateComponentState(true)]`. Без него генератор автосостояния не
  создавал бы сетевые поля, а клиентский хук `AfterAutoHandleStateEvent` не поднимался бы.
- Новый константный стейт нейтрала: `NeutralFlagState = "uaflag"` (общий нейтральный
  флаг, когда фракция не контролирует цель или её стейт не задан).
- `CurrentController` помечен `[AutoNetworkedField]` — поле теперь уходит на клиент и
  доступно в общем (shared) examine-коде.
- Два новых сетевых поля, заполняются на сервере:
  - `CurrentSpriteState` — стейт спрайта, который клиент применяет к слою флага.
    Пустое значение = фракция-контролёр без спрайта (например `weyu`), клиент не
    трогает спрайт.
  - `ControllerDisplayName` — готовое отображаемое имя контролёра для осмотра.

### 2. `Content.CMU/Server/Round/Objectives/Type/ObjCaptureSystem.cs`
- **Исправлена опечатка**: `opforFlag = ... ?? "uaflagworn"` → `?? "uaflag_worn"`
  (в двух местах: в дефолте и в сравнении govfor/opfor). Стейта `uaflagworn` нет в
  `wallflags.rsi/meta.json`, поэтому opfor-флаг рендерился бы в несуществующий стейт.
- Новый приватный метод `ResolveFlagSpriteState(...)`: маппинг
  `"" → uaflag`, `govfor → govforFlag`, `opfor → opforFlag`, `clf → clfflag`,
  неизвестная фракция → `""` (не менять). Пустой `govforFlag`/`opforFlag`
  (когда у взвода нет `platoonFlag`) тоже фолбэчится на нейтральный `uaflag`.
- Новый метод `GetFactionDisplayName(...)`: для govfor/opfor возвращает имя выбранного
  взвода (fallback — локализованное имя фракции), для clf/weyu — локализованное имя.
  Используется и в попапах, и для `ControllerDisplayName`.
- `Update(...)`: спрайт-стейт вычисляется ДО проверки `objComp.Active`, чтобы спрайт
  работал независимо от активации цели. Сеть меняется только при изменении значения
  (guard + `Dirty`). `GovforFlagState`/`OpforFlagState` по-прежнему переписываются
  каждый тик для совместимости.
- Подъём/опускание флага и сброс (`OnReset`) теперь ведут `ControllerDisplayName` и
  вызывают `Dirty` для репликации.

### 3. `Content.CMU/Client/Round/Objectives/ClientObjectiveCaptureSystem.cs`
Полностью переписан:
- Убраны `ComponentStartup`/`ComponentHandleState` и `AppearanceComponent` (у флагов
  его нет — из-за этого клиент всегда выходил раньше времени).
- Убран `_sawmill.Debug` (лог ни на что не влиял).
- Убрана клиентская логика выбора стейта по фракции — она дублировала серверную.
- Единственная подписка: `AfterAutoHandleStateEvent` — обновляет спрайт когда сервер
  присылает новое состояние (это единственный владелец изменения спрайта на клиенте).
- Применение: `_sprite.LayerSetRsiState((ent, sprite), 0, ent.Comp.CurrentSpriteState)`.
  Слой 0 — единственный слой спрайта флага (`state:` из прототипов флагов стен).
  Пустой `CurrentSpriteState` игнорируется (спрайт не меняем).

### 4. `Content.CMU/Shared/Round/Objectives/SharedObjectiveCaptureSystem.cs`
Добавлен осмотр цели:
- Подписка `ExaminedEvent` для `CaptureObjectiveComponent`.
- Если контролёра нет (`ControllerDisplayName` пуст) — строка
  `cmu-capture-objective-examine-uncontrolled`.
- Иначе — `cmu-capture-objective-examine-controlled` с подстановкой
  `ControllerDisplayName` (имя взвода или локализованное имя фракции, уже готовое к
  показу). Examine работает и на клиенте, и на сервере.

[+] Дополнение: время начисления очков в описании флага
- Если у флага есть `CaptureObjectiveComponent`, связанная активная
  `CMUObjectiveComponent` (`Active == true`) и `PointIncrementTime > 0`, то в описание
  добавляется строка `cmu-capture-objective-examine-points-time` с переменной
  `$time` — отформатированная длительность одного инкремента очков
  (`cmu-capture-objective-duration-seconds` = «{ $seconds } с» для значений короче
  60 с, иначе `cmu-capture-objective-duration-minutes-seconds` = «{ $minutes } мин
  { $seconds } с»).
- Форматирование вынесено в хелпер `FormatPointsTime(float seconds)` — округление
  вверх, минимум 0; значения меньше минуты показываются в секундах.
- Строка показывается только для АКТИВНОЙ цели (флаг, который фактически начисляет
  очки); у неактивного/декоративного флага время не выводится.

### 5. `Content.CMU/Resources/Locale/en-US/CMU14/objective-interactions.ftl`
Добавлены ключи:
- `cmu-capture-objective-faction-govfor`, `-opfor`, `-clf`, `-weyu` — имена фракций
  как fallback, когда взвод не выбран.
- `cmu-capture-objective-examine-uncontrolled`, `cmu-capture-objective-examine-controlled`.
- Из строки осмотра `cmu-capture-objective-examine-controlled` убран красный цвет
  (`[color=red]...[/color]`) по просьбе пользователя.

### 5a. `Content.CMU/Resources/Locale/ru-RU/CMU14/objective-interactions.ftl` (новый)
Русская локализация целиком файла `objective-interactions.ftl` (и capture-, и
interact-ключи). Файл был создан с нуля — до этого ru-RU объективной локализации не было.

### 6. `Content.IntegrationTests/_CMU14/Round/CaptureObjectiveSpriteStateTest.cs` (новый)
Интеграционный тест на сервере: спавнится тестовый флаг
(`CaptureObjectiveComponent` + `CMUObjectiveComponent`), выставляется `CurrentController`,
прогоняются 2 тика (срабатывает `ObjCaptureSystem.Update`) и проверяется вычисленное
`CurrentSpriteState`. Кейсы (используют дефолтные флаги взводов, т.к. в тесте взвод не выбран):
- `"" → uaflag` (нейтрал)
- `govfor → uaflag`
- `opfor → uaflag_worn` (проверяет исправленную опечатку)
- `clf → clfflag`
- `weyu → ""` (незнакомая фракция — спрайт не меняем)

## Почему именно такой дизайн
- Сетевой геймплей (подъём/опускание) уже был серверным — сервер является источником
  истины. Рендер спрайта оставлен на клиенте, но без собственных решений: клиент только
  применяет переданное состояние, что исключает расхождения между клиентами.
- `AutoGenerateComponentState` — современный механизм генерации состояния в проекте
  (см. `HotspotObjectiveComponent`, `SimpleSpriteOverlayComponent` и т.п.);
  `ComponentHandleState`/`ComponentStartup` для такого компонента больше не поднимаются,
  поэтому клиент переведён на `AfterAutoHandleStateEvent`.
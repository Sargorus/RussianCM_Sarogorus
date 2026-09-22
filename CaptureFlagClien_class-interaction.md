# Взаимодействие изменённых классов

Схема потока данных для спрайта флага и осмотра (после изменений в `fix/CaptureFlagClien`).

```
                  Общий (shared) слой
   ┌──────────────────────────────────────────────────────────────┐
   │ CaptureObjectiveComponent                                     │
   │  - CurrentController      (автор: сервер) [AutoNetworkedField] │
   │  - CurrentSpriteState     (автор: сервер) [AutoNetworkedField] │
   │  - ControllerDisplayName  (автор: сервер) [AutoNetworkedField] │
   │  - GovforFlagState / OpforFlagState (не сетевые, служебные)    │
   │  - NeutralFlagState = "uaflag" (const)                         │
   └──────────────────────────────────────────────────────────────┘
              ▲                                │
              │ сеть (компонент-стейт)         │ локальное чтение
              │ репликация                     ▼
   Сервер                                    Клиент
```

## 1. Сервер: `ObjCaptureSystem.Update()`

Запускается каждый тик. Для каждого флага `CaptureObjectiveComponent` + `CMUObjectiveComponent`:

1. `_platoonSpawnRuleSystem` даёт выбранные взводы; берутся `PlatoonFlag` (сейчас в
   прототипах закомментированы, на практике дефолты `"uaflag"` / `"uaflag_worn"`).
2. Метод `ResolveFlagSpriteState(CurrentController, govforFlag, opforFlag)` выбирает
   спрайт-стейт: `"" → uaflag`, `govfor`, `opfor`, `clf`, `_ → ""`.
3. Если стейт изменился — значение пишется в компонент и помечается `Dirty(uid, comp)`
   (условие «не диртить впустую» из CONVENTIONS).
4. `GetFactionDisplayName(faction)` формирует имя контролёра (взвод или локализованное
   имя фракции); используется для попапов подъёма/опускания и для `ControllerDisplayName`.

Ветка записи `ControllerDisplayName` (обновление через события):
- `CaptureHoistFlagDoAfterEvent` → `OnHoistFlagDoAfter`: поднятие -> контролёр +
  имя, спуск -> очистка; обе ветки дают `Dirty`.
- `ObjectiveResetEvent` → `OnReset`: очистка `CurrentController` + имя + `Dirty`.
- урон (в `Update`) → сбрасывает флаг (контроллер/имя очищены, попап).

Итог: сервер — единственный писатель всех трёх `[AutoNetworkedField]`.

## 2. Клиент: `ClientObjectiveCaptureSystem`

Единственный потребитель `CurrentSpriteState` на клиенте.

- Регистрируется обработчик `AfterAutoHandleStateEvent` (событие, которое генерирует
  инфраструктура автосостояния после применения нового сетевого стейта — работает,
  потому что у компонента `[AutoGenerateComponentState(true)]`).
- При каждом обновлении состояния вызывается
  `_sprite.LayerSetRsiState((ent, sprite), 0, CurrentSpriteState)`:
  `SpriteSystem` меняет стейт слоя 0 (`SpriteComponent`) флага.
- Если `CurrentSpriteState == ""` (незнакомая фракция) — метод ничего не делает:
  на слое остаётся дефолтный `state:` из прототипа флага.
- У системы нет собственного мнения про «какой стейт у какой фракции» — исключен
  риск рассинхрона; отображение всегда = тому, что насчитал сервер.

## 3. Shared: `SharedObjectiveCaptureSystem.OnExamined`

Общий код осмотра, работает и на сервере, и на клиенте.

1. Разбирается `ExaminedEvent` для `CaptureObjectiveComponent`.
2. Если `ControllerDisplayName` пуст — push
   `cmu-capture-objective-examine-uncontrolled` (никто не контролирует).
3. Иначе push `cmu-capture-objective-examine-controlled` c
   `("faction", ControllerDisplayName)` — имя уже готово, дополнительных решений
   клиент/сервер не требуют.
4. `ControllerDisplayName` заполняется сервером и доезжает в examine благодаря
   `[AutoNetworkedField]`; на сервере значение актуально всегда.

[+] Дополнение: время начисления очков
5. Дополнительно, если у флага есть активная `CMUObjectiveComponent`
   (`Active == true`) и `PointIncrementTime > 0`, сервернится строка
   `cmu-capture-objective-examine-points-time` с переменной `$time` —
   отформатированная через хелпер `FormatPointsTime(float seconds)` длительность
   одного инкремента очков.
6. `FormatPointsTime`: округление секунд вверх (минимум 0); значения короче минуты
   → `cmu-capture-objective-duration-seconds` («{ $seconds } с»), иначе
   `cmu-capture-objective-duration-minutes-seconds` («{ $minutes } мин { $seconds } с»).
7. Строка выводится только для АКТИВНОЙ цели: у неактивных / декоративных флагов
   (`CMUObjectiveComponent` отсутствует или `Active == false`) время не показывается.

## 4. Интеграционный тест: `CaptureObjectiveSpriteStateTest`

Проверяет связку «сервер → компонент» без клиента:
спавнить тестовый флаг → выставить `CurrentController` напрямую → 2 тика (прогнать
`ObjCaptureSystem.Update`) → проверить `CurrentSpriteState`. Флаги взводов в тесте
не выбраны, поэтому используются дефолтные `"uaflag"` / `"uaflag_worn"`.

## 5. FTL

`objective-interactions.ftl` (en-US) снабжает ключами все `Loc.GetString(...)`,
используемые в попапах сервера и в examine (shared). Новые ключи живут рядом со
старыми `cmu-capture-objective-*` в `Content.CMU/Resources/Locale/en-US/CMU14/`.

Локализация:
- en-US: `Content.CMU/Resources/Locale/en-US/CMU14/objective-interactions.ftl`.
- ru-RU: `Content.CMU/Resources/Locale/ru-RU/CMU14/objective-interactions.ftl` (новый
  файл, полный перевод обоих разделов файла).

В строке «флаг контролируется фракцией { $faction }» маркер цвета убран (оба языка),
в строке «флаг не контролирует ни одна фракция» красный цвет сохранён как
предупреждение.

## Итог по ответственности

- `ObjCaptureSystem` (Server) — владелец game-состояния и его сетизации (автор).
- `SharedObjectiveCaptureSystem` (Shared) — владелец события осмотра (читатель).
- `ClientObjectiveCaptureSystem` (Client) — единственный владелец изменения спрайта (читатель).
- `CaptureObjectiveComponent` (Shared) — контейнер данных, заявленных на сеть.
- Никакие два класса не пишут одно и то же состояние; чтение/запись не конфликтуют.
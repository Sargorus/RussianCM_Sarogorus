# Capture Objective — описание изменений по багу (полный набор)

Ветка: `fix/CaptureFlagClien`

Этот файл описывает **весь** набор изменений, внесённых для решения бага с флагом
«Захват флага» (CaptureObjective): рендер спрайта, осмотр (examine) с владельцем,
подписью «цель/не цель», прогнозом времени начисления очков и серверный гейт,
гарантирующий, что очки начисляются только фракции-держателю, у которой флаг
действительно является целью.

Связанные заметки: `CaptureFlagClien_changes.md` (ранняя часть: клиентский рендер +
первичный examine), `CaptureFlagClien_class-interaction.md` (диаграммы классов).

## Кратко: итоговое поведение

При осмотре флага игроком:

1. **Всегда** показывается владелец: «флаг контролируется фракцией X» (с именем
   взвода, если он выбран) или «флаг никто не контролирует».
2. **Всегда** показывается подпись «этот флаг является целью вашей фракции» /
   «не является целью вашей фракции» — осматривающему понятно, цель это или нет,
   даже когда у него нет цели на этот флаг.
3. Строка «приносит очки контролирующей фракции каждые X» показывается **только**
   фракциям, у которых этот флаг является целью.
4. Строка «до начисления очков осталось X» показывается **только** если фракция
   осматривающего имеет цель **и** текущий контролёр входит в список фракций цели
   (т.е. действительно может получить очки).

Это устраняет два бага: (а) раньше осмотревший вообще не видел ничего полезного,
когда у цели на флаге не было связанной активной `CMUObjectiveComponent` (из-за
раннего `if (!objComp.Active) return;` владелец и подпись пропадали); (б) отсчёт
начисления показывал время даже тогда, когда держатель (например CLF) не имеет
цели на этот флаг и очков получить не может.

## Изменённые файлы и суть правок

### 1. `Content.CMU/Client/Round/Objectives/ClientObjectiveCaptureSystem.cs`
Полностью переписан былой «чем дальше, тем ничего»:
- Убраны `ComponentStartup`/`ComponentHandleState` и `AppearanceComponent` (его у
  флагов нет — из-за этого клиент всегда выходил раньше времени).
- Единственная подписка: `AfterAutoHandleStateEvent` — когда сервер присылает новое
  сетевое состояние, клиент применяет `CurrentSpriteState` через
  `SpriteSystem.LayerSetRsiState` (слой 0 — единственный слой спрайта флага).
- Пустой `CurrentSpriteState` не трогает спрайт (фракции без спрайта, например
  `weyu`).

### 2. `Content.CMU/Shared/Round/Objectives/Type/CaptureObjectiveComponent.cs`
- `[AutoGenerateComponentState(true)]` + `[NetworkedComponent]` — генератор создаёт
  сетевые поля и поднимает `AfterAutoHandleStateEvent` на клиенте.
- `[AutoNetworkedField]` на: `CurrentController`, `CurrentSpriteState`,
  `ControllerDisplayName`, `ControllerPlatoonName`, `TimeUntilNextIncrement`.
- Константа `NeutralFlagState = "uaflag"`.

### 3. `Content.CMU/Server/Round/Objectives/Type/ObjCaptureSystem.cs`
- Исправлена опечатка `"uaflagworn"` → `"uaflag_worn"` (в дефолте opfor и в
  сравнении govfor/opfor).
- `ResolveFlagSpriteState(...)`: `"" → uaflag`, `govfor → …`, `opfor → …`,
  `clf → clfflag`, неизвестная → `""` (не менять).
- `GetFactionDisplayName(...)`: локализованные имена фракций (`cmu-capture-objective-faction-*`)
  через `Loc.GetString`.
- `GetFactionSubunitName(...)`: имя выбранного взвода `govfor`/`opfor`.
- `SetRemainingTime(...)`: обновление `TimeUntilNextIncrement` без пустых Dirty
  (guard по `Math.Abs`).
- Подъём/опускание/сброс ведут `ControllerDisplayName`/`ControllerPlatoonName`
  и вызывают `Dirty`.
- **`Update(...)`**: спрайт-стейт считается до проверки `objComp.Active`; сетевые
  поля дёргаются только при изменении значения.
- **Гейт начисления очков**: перед аккумуляцией `_timeSinceLastIncrement`
  добавлено `if (!IsObjectiveFaction(comp.CurrentController, objComp)) continue;` —
  фракция-держатель, не входящая в `objComp.Factions`, очки не получает и отсчёт
  не идёт. Хелпер `IsObjectiveFaction` (статический, см. п. 4).

### 4. `Content.CMU/Shared/Round/Objectives/SharedObjectiveCaptureSystem.cs`
Логика осмотра из «Кратко»:
- `OnExamined` (подписка `ExaminedEvent` для `CaptureObjectiveComponent`):
  1. Владелец — всегда: `cmu-capture-objective-examine-uncontrolled` /
     `cmu-capture-objective-examine-controlled` (+ `…-raised-by` с взводом).
  2. Если цель отсутствует/неактивна/`PointIncrementTime <= 0` — подпись
     `…-not-objective` и выход (владелец уже показан). Раннего выхода до владельца
     больше нет.
  3. Иначе — подпись `…-is-objective` / `…-not-objective` по фракции
     осматривающего.
  4. При наличие цели — строка `…-points-time`.
  5. Отсчёт `…-until-increment` — только если контролёр непустой и
     `IsObjectiveFaction(CurrentController, objComp)`.
- Хелперы: `ExaminerFactionHasObjective` (инстансный, `NpcFactionMemberComponent`
  осматривающего, нормализация `auweyu → weyu`), `IsObjectiveFaction` (статический,
  пустой `Factions` → цель общая), `FormatPointsTime` (`<60 c` в секундах, иначе
  «мин с», округление вверх).
- Вся локализация — `Loc.GetString` с ключами `cmu-capture-objective-*`; хардкод
  строк для игрока отсутствует.

### 5. Локализация
- `Content.CMU/Resources/Locale/en-US/CMU14/objective-interactions.ftl`:
  добавлены ключи для examine, «цель/не цель», точек/отсчёта, длительностей, имён
  фракций; убран `[color=red]` у `…-controlled`.
- `Content.CMU/Resources/Locale/ru-RU/CMU14/objective-interactions.ftl` (новый):
  полностью русифицирован файл. Паритет ключей с en-US подтверждён
  (`Compare-Object` — расхождений нет, по 32 строки одинаковой структуры).

### 6. Тесты (`Content.IntegrationTests/_CMU14/Round/`)
- `CaptureObjectiveSpriteStateTest.cs` (новый): `""→uaflag`, `govfor→uaflag`,
  `opfor→uaflag_worn` (проверяет исправленную опечатку), `clf→clfflag`,
  `weyu→""` — 5 кейсов через `[TestCase]`.
- `CaptureObjectiveExamineTest.cs` (новый): тестовая цель
  `CMUTestCaptureFlagExamine` (`factions: [govfor, opfor]`) и экзаменаторы с
  `NpcFactionMember`. Ожидаемые строки собираются через `Loc.GetString` с теми же
  ключами. Кейсы: CLF на govfor-флаге, GovFor на govfor-флаге, GovFor на
  clf-флаге (держатель без цели → точка видна, отсчёт скрыт), неактивная цель
  (владелец виден, подпись «не цель», очков/отсчёта нет).

## Соответствие правилам корня проекта

- **Локализация через ключи.** Все читаемые игроком строки — через
  `Loc.GetString("cmu-…")`; ни одного хардкод-текста в коде. Ключи kebab-case с
  префиксом `cmu-`; файлы лежат в `Content.CMU/Resources/Locale/{en-US,ru-RU}/CMU14/`
  (см. `CONVENTIONS.md — Localization`, `CONTRIBUTING.md — Naming`).
- **Границы CMU.** Все файлы — в `Content.CMU/` (CMU-зона ветки) и
  `Content.IntegrationTests/_CMU14/`; новые сущности тестов — ID `CMU*`;
  меток `CMU14` не требуется (изменений вне CMU-зон нет).
- **ECS.** Компонент хранит данные (`[AutoNetworkedField]`), системы — логику.
  Классы `sealed`, file-scoped namespaces, `Entity<T>`-подписки, guard-`Dirty` без
  повторных dirt-ов неизменных значений.
- **Тесты.** Проверяют регрессии и контракты поведения, не балансовые значения;
  ожидаемые строки генерируются из тех же локалей, что и код (`CONVENTIONS.md —
  Verification`).
- **Форматирование.** Принят стиль `.editorconfig`/ближайших примеров CMU.

## Верификация

- Сборка затронутых проектов: `Content.Shared`, `Content.Server`, `Content.Client`
  (Release).
- `dotnet test Content.IntegrationTests/Content.IntegrationTests.csproj -c Release
  --no-restore --filter 'FullyQualifiedName~CaptureObjective'` —
  **6/6 тестов пройдено** (5 спрайт + 1 examine).
- Паритет локалей: сверен автоматически (en-US vs ru-RU), расхождений нет.

### Замечания окружения
- Debug/`DebugOpt`-сборки движка на этом чек-ауте нестабильны (TOOLS-зависимые
  сборки), поэтому сборка/тесты гоняются в `Release`.
- Перед сборкой/тестами убиваются зависшие процессы `Content.Server`/`Content.Client`
  (они блокируют общие `bin\`).
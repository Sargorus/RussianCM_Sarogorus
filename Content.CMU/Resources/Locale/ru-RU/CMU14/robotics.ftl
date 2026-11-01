ent-CMURoboticsFabricator = робототехнический фабрикатор
    .desc = Фабрикатор «Вейланд-Ютани» для рам, конечностей, планшетов управления, модулей и гусеничных шасси дронов. Требует универсальную топливную ячейку WL-6 и обученного робототехника.

ent-CMUCrateRoboticsFabricator = ящик робототехнического фабрикатора
    .desc = Ящик с одним робототехническим фабрикатором «Вейланд-Ютани» и универсальной топливной ячейкой WL-6 для питания.

ent-CMURoboticsOrderConsole = консоль робототехнических заказов
    .desc = Контрактный терминал «Вейланд-Ютани» для робототехнических работ. Возьмите заказ на создание или починку, затем поставьте готового дрона рядом с консолью и сдайте его.

ent-CMUCrateRoboticsOrderConsole = ящик консоли робототехнических заказов
    .desc = Ящик с одной консолью робототехнических заказов «Вейланд-Ютани».

ent-CMUCombatDrone = гусеничный боевой дрон
    .desc = Дистанционно управляемый гусеничный дрон с импульсной пушкой и сектором обстрела 180 градусов.

ent-CMUFlamerDrone = гусеничный огнемётный дрон
    .desc = Дистанционно управляемый гусеничный дрон с двумя огнемётными соплами на клешнях.

ent-CMUDroneAndroidUnarmed = безоружный дроид-андройд
    .desc = Урезанная синтетическая рама дрона с открытыми сервоприводами. Её шина управления намеренно заблокирована от использования огнестрельного оружия.

ent-CMUDroneAndroidCombat = боевой дроид-андройд
    .desc = Усиленная синтетическая рама дрона с боевой шиной управления, оружейными процедурами и слоем корпоративной брони.

ent-CMUDroneBodyFrameUnarmed = рама безоружного дроида-андройда
    .desc = Голая синтетическая рама торса, шина управления которой заблокирована от огнестрельного оружия. Порты конечностей закрыты, слот активации пуст.

ent-CMUDroneBodyFrameCombat = рама боевого дроида-андройда
    .desc = Усиленная синтетическая рама торса с боевой шиной управления. Порты конечностей закрыты, слот активации пуст.

cmu-robotics-fabricator-cell-already = В фабрикаторе уже есть топливная ячейка.
cmu-robotics-fabricator-cell-insert-start = Вы начинаете вставлять топливную ячейку в фабрикатор.
cmu-robotics-fabricator-cell-insert-fail = Топливная ячейка не подходит.
cmu-robotics-fabricator-cell-insert-finish = Вы вставляете топливную ячейку в фабрикатор.
cmu-robotics-fabricator-cell-none = В фабрикаторе нет топливной ячейки.
cmu-robotics-fabricator-cell-remove-start = Вы начинаете извлекать топливную ячейку из фабрикатора.
cmu-robotics-fabricator-cell-remove-finish = Вы извлекаете топливную ячейку из фабрикатора.
cmu-robotics-fabricator-examine-cell-inserted = Топливная ячейка [color=cyan]вставлена[/color].
cmu-robotics-fabricator-examine-cell-empty = Топливная ячейка [color=red]не вставлена[/color].
cmu-robotics-fabricator-examine-research = Требуется [color=cyan]исследование {$level}[/color].
cmu-robotics-fabricator-skill-required = Для работы с фабрикатором нужен навык исследования {$level}.

cmu-robotics-console-title = Консоль робототехнических заказов
cmu-robotics-console-heading = РОБОТОТЕХНИЧЕСКИЕ ЗАКАЗЫ
cmu-robotics-console-subheading = Контрактный терминал «Вейланд-Ютани»
cmu-robotics-console-active = Активный заказ
cmu-robotics-console-available = Доступные заказы
cmu-robotics-console-no-active = Заказ не взят.
cmu-robotics-console-no-orders = Нет доступных заказов.
cmu-robotics-console-active-exists = Сначала сдайте или отмените текущий заказ.
cmu-robotics-console-accepted = Заказ принят: {$target}. Оплата: {$reward}.
cmu-robotics-console-repair-spawn-failed = Не удалось выдать повреждённый дрон.
cmu-robotics-console-no-repair-target = Рядом с консолью нет повреждённого дрона по этому заказу.
cmu-robotics-console-no-build-target = Рядом с консолью нет дрона нужной модели.
cmu-robotics-console-rejected = Заказчик не доволен результатом: {$reason}. Деньги не выплачены.
cmu-robotics-console-no-id = Не найдена ID-карта для получения оплаты.
cmu-robotics-console-delivered = Заказ успешно сдан: {$target}. Заказчик оплатил {$reward}.
cmu-robotics-console-cancelled = Заказ отменён.
cmu-robotics-console-refresh-active = Нельзя обновить список, пока активен заказ.
cmu-robotics-console-refreshed = Список заказов обновлён.
cmu-robotics-console-type = Тип
cmu-robotics-console-target = Цель
cmu-robotics-console-module = Требуемый модуль
cmu-robotics-console-reward = Оплата
cmu-robotics-console-type-build = Создание
cmu-robotics-console-type-repair = Починка
cmu-robotics-console-order-heading = {$type}: {$target}
cmu-robotics-console-accept = Взять заказ
cmu-robotics-console-submit = Сдать заказ
cmu-robotics-console-cancel = Отменить
cmu-robotics-console-refresh = Обновить список
cmu-robotics-console-delivery-hint = Поставьте готового дрона рядом с консолью (в радиусе 2 метров) и нажмите «Сдать заказ».
cmu-robotics-console-reason-wrong = это не тот дрон, который заказывали
cmu-robotics-console-reason-broken = дрон сломан
cmu-robotics-console-reason-not-repaired = дрон не отремонтирован
cmu-robotics-console-reason-module = не установлен модуль {$module}

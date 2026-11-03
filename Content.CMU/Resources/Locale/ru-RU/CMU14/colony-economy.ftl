cmu-atm-card-slot-occupied = В банкомате уже есть карта.
cmu-atm-no-card = У вас нет ID-карты, которую можно вставить.
cmu-atm-take-card-verb = Забрать карту
cmu-atm-take-cash-verb = Забрать наличные
cmu-atm-take-card-start = Вы начинаете вытаскивать карту из банкомата...
cmu-atm-take-card-start-others = {CAPITALIZE(THE($user))} начинает вытаскивать карту из банкомата!
cmu-atm-boot-title = КОЛОНИАЛЬНЫЕ ФИНАНСОВЫЕ СИСТЕМЫ W-Y
cmu-atm-boot-bios = BIOS 2.7 (C) 2179 W-Y CORP.
cmu-atm-boot-memory = ПАМЯТЬ 640K
cmu-atm-boot-keypad = КЛАВИАТУРА
cmu-atm-boot-reader = СЧИТЫВАТЕЛЬ КАРТ
cmu-atm-boot-dispenser = ВЫДАЧА НАЛИЧНЫХ
cmu-atm-boot-uplink = СВЯЗЬ С КАЗНАЧЕЙСТВОМ ООН
cmu-atm-boot-ok = ОК
cmu-atm-boot-loading = ЗАГРУЗКА ТЕРМИНАЛА...
cmu-atm-out-of-order = НЕ РАБОТАЕТ
cmu-atm-out-of-order-sorry = ВОСПОЛЬЗУЙТЕСЬ ДРУГИМ БАНКОМАТОМ
cmu-atm-hint-confirm = OK = подтвердить   X = назад
cmu-atm-hint-continue = Нажмите OK для продолжения.
cmu-atm-nav-title = Банкомат колонии
cmu-atm-nav-pin = Ваша карта №{ $account } — PIN { $pin }
cmu-atm-nav-no-card = У вас нет собственной карты
cmu-atm-nav-pin-unknown = Чтение вашей карты...
cmu-atm-nav-pop-out = Отдельное окно

# ATM screens and transaction messages
cmu-atm-busy = Кто-то уже использует этот банкомат.
cmu-atm-tampered = Внимание: обнаружены следы вмешательства в электронику.
cmu-atm-normal = Банкомат работает исправно.
cmu-atm-unknown = Неизвестно
cmu-atm-invalid-amount = Введите корректную сумму.
cmu-atm-insufficient-funds = Недостаточно средств.
cmu-atm-insufficient-hand = Недостаточно наличных в руках.
cmu-atm-invalid-account = Введите корректный пятизначный номер счёта.
cmu-atm-account-missing = Счёт не найден.
cmu-atm-insufficient-cash = Недостаточно наличных.
cmu-atm-own-account = Нельзя перевести деньги на собственный счёт.
cmu-atm-timeout = Время сеанса истекло.
cmu-atm-take-cash-first = Сначала заберите наличные.
cmu-atm-invalid-pin = Неверный формат PIN. Введите все { $digits } цифры.
cmu-atm-incorrect-pin = Неверный PIN. Попытка { $attempt }/{ $maximum }.
cmu-atm-withdraw-confirm = Снять ${ $amount }? После налога вы получите ${ $net }.
cmu-atm-dispensed = Выдано ${ $amount }. Баланс: ${ $balance }.
cmu-atm-deposited = Внесено ${ $amount }. Баланс: ${ $balance }.
cmu-atm-recipient = Получатель: { $name }. Введите сумму:
cmu-atm-remote-confirm = Внести ${ $amount } на счёт №{ $account }?
cmu-atm-remote-done = Внесено ${ $amount } на счёт №{ $account }.
cmu-atm-transfer-confirm = Перевести ${ $amount } на счёт №{ $account }?
cmu-atm-transfer-done = Переведено ${ $amount }. Баланс: ${ $balance }.
cmu-atm-welcome =
    ФИНАНСОВЫЙ ТЕРМИНАЛ КОЛОНИИ
    v2.7  КАЗНА ООН
    { "" }
    1) ВНЕСЕНИЕ НА ЧУЖОЙ СЧЁТ
    { "" }
    Вставьте ID-карту для доступа к счёту.
cmu-atm-pin-prompt = ВВЕДИТЕ PIN:
cmu-atm-locked =
    { "** КАРТА ЗАБЛОКИРОВАНА **" }
    Слишком много неверных попыток. Попробуйте позже.
cmu-atm-main-menu =
    Здравствуйте, { $name }.
    { "" }
    1) СНЯТИЕ
    2) ВНЕСЕНИЕ
    3) ПЕРЕВОД
    4) ВНЕСЕНИЕ НА ЧУЖОЙ СЧЁТ
    5) ИСТОРИЯ
    6) ВЫХОД
cmu-atm-withdraw-prompt =
    СНЯТИЕ
    Введите сумму:
cmu-atm-deposit-prompt =
    ВНЕСЕНИЕ
    Введите сумму:
cmu-atm-remote-prompt =
    ВНЕСЕНИЕ НА ЧУЖОЙ СЧЁТ
    Номер счёта получателя:
cmu-atm-transfer-prompt =
    ПЕРЕВОД
    Номер счёта получателя:
cmu-atm-history-title = ИСТОРИЯ СЧЁТА
cmu-atm-history-empty = Операций пока нет.
cmu-atm-history-back = OK = назад
cmu-atm-history-withdrawal = -${ $amount } СНЯТИЕ
cmu-atm-history-deposit = +${ $amount } ВНЕСЕНИЕ
cmu-atm-history-cash = +${ $amount } НАЛИЧНЫЕ
cmu-atm-history-out = -${ $amount } НА №{ $account }
cmu-atm-history-in = +${ $amount } ОТ №{ $account }
cmu-atm-history-returned = +${ $amount } ВОЗВРАТ
cmu-atm-history-purchase = -${ $amount } ПОКУПКА
cmu-atm-fault-fatal = СБОЙ { $byte }: ОШИБКА CRC РЕЕСТРА
cmu-atm-fault-segv = SEGV ПО АДРЕСУ { $address } В TXN_CORE
cmu-atm-fault-halted = TXN_CORE ОСТАНОВЛЕНО
cmu-atm-fault-watchdog = СБРОС WATCHDOG ... НЕ УДАЛСЯ
cmu-atm-fault-uplink = СВЯЗЬ ПОТЕРЯНА, ПОВТОР { $attempt }/3
cmu-atm-fault-dispenser = ОШИБКА ШИНЫ ВЫДАЧИ { $byte }
cmu-atm-fault-auth = ТАБЛИЦА ДОСТУПА ПОВРЕЖДЕНА: { $address }
cmu-atm-fault-kernel = ПАНИКА ЯДРА: НЕТ СИНХРОНИЗАЦИИ
cmu-atm-fault-stack = СТЕК ПОВРЕЖДЁН, ПРЕРЫВАНИЕ
cmu-atm-fault-trap = НЕОБРАБОТАННОЕ ИСКЛЮЧЕНИЕ { $byte }: { $address }
cmu-atm-fault-garbled = ?? ???? ?????? ??? ??

# RUMC: налог на посылки, проданные через торговые платформы
admin-console-trade-tax-section = -- Налог на продажу посылок --
admin-console-current-trade-tax = Текущий налог на продажу посылок: { $percent }%
admin-console-trade-tax-description = Берётся из суммы, которую дрон платит за посылку, проданную на торговой платформе. Эта доля уходит в бюджет колонии, остальное получает продавец.
admin-console-trade-tax-announcement = Налог колонии на продажу посылок установлен в { $percent }%. Эта доля от каждой посылки, проданной на торговой платформе, уходит колонии.

# Консоль корпоративных дел
ent-AU14CorporateConsole = консоль корпоративных дел
    .desc = Терминал корпоративных дел «Вейланд-Ютани». Позволяет управлять транзитными тарифами, корпоративными средствами и поддержкой третьих сторон. Все изменения тарифа объявляются по всей колонии.

corporate-console-title = Консоль корпоративных дел
corporate-console-budget = Корпоративный бюджет: ${ $amount }
corporate-console-withdraw-section = -- Снять наличные --
corporate-console-transit-tariff-section = -- Транзитный тариф --
corporate-console-current-transit-tariff = Текущий транзитный тариф: { $percent }%
corporate-console-transit-tariff-description = Процент от всех выплат за сдачу ресурсов, который уходит в корпоративный бюджет вместо бюджета колонии.
corporate-console-new-tariff = Новый тариф (0–50%):
corporate-console-tariff-placeholder = напр. 15
corporate-console-withdraw-tax-note = Примечание: с снятия удерживается подоходный налог { $percent }%.
corporate-console-withdraw-no-tax = Снятие не облагается подоходным налогом.
corporate-console-announcement-sender = Корпоративные дела
corporate-console-tariff-announcement = Корпоративный транзитный тариф установлен на { $percent }%. Выплаты колонии за сдачу скорректированы.

cmu-corporate-heading = КОРПОРАТИВНЫЕ ДЕЛА
cmu-corporate-subheading = ОФИС КОЛОНИАЛЬНОГО СВЯЗНОГО // ТРАНЗИТ И ПОДДЕРЖКА
cmu-corporate-treasury = Корпоративная казна
cmu-corporate-withdraw-tax = При снятии удерживается подоходный налог колонии { $percent }%.
cmu-corporate-withdraw-no-tax = Снятие сейчас не облагается налогом.
cmu-corporate-tariff = Транзитный тариф
cmu-corporate-current-tariff = Текущая ставка
cmu-corporate-exact = ТОЧНАЯ СТАВКА (0–{ $max }%)
cmu-corporate-apply = ПРИМЕНИТЬ
cmu-corporate-tariff-announced = Каждое изменение тарифа объявляется по всей колонии.
cmu-corporate-support = Корпоративная поддержка
cmu-corporate-support-hint = Вызовите корпоративную охрану, синтетиков или подрядчиков за счёт корпоративного бюджета.
cmu-corporate-footer = Тарифные поступления идут в корпоративный бюджет с каждой сдачей.
cmu-ambassador-withdraw = СНЯТЬ ${ $amount }
cmu-ambassador-economy = Экономика колонии
cmu-ambassador-open-third-party = ОТКРЫТЬ МЕНЮ ПОДДЕРЖКИ

# Corporate liaison safe
cmu-liaison-safe-lock-verb = Закрыть сейф
cmu-liaison-safe-unlock-verb = Открыть сейф
cmu-liaison-safe-locked = Сейф закрыт.
cmu-liaison-safe-pin-title = PIN сейфа
cmu-liaison-safe-pin-prompt = Введите PIN-код сейфа.
cmu-liaison-safe-current-pin-title = Смена PIN-кода сейфа
cmu-liaison-safe-current-pin-prompt = Введите текущий PIN-код сейфа.
cmu-liaison-safe-pin-current-required = Перед сменой PIN-кода введите текущий PIN-код.
cmu-liaison-safe-pin-wrong = Неверный PIN-код.
cmu-liaison-safe-pin-accepted = PIN-код принят.
cmu-liaison-safe-set-pin-verb = Задать PIN-код
cmu-liaison-safe-change-pin-verb = Изменить PIN-код
cmu-liaison-safe-set-pin-title = Установка PIN-кода
cmu-liaison-safe-set-pin-prompt = Введите новый четырёхзначный PIN-код для этого сейфа.
cmu-liaison-safe-pin-set = PIN-код установлен.
cmu-liaison-safe-pin-invalid = PIN-код должен состоять ровно из четырёх цифр.
cmu-liaison-safe-pin-no-card = Для установки PIN-кода нужна ID-карта корпоративного связного.

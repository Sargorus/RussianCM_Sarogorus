ent-CMURoboticsFabricator = robotics fabricator
    .desc = A Weyland-Yutani fabricator for android frames, limbs, control tablets, modules and activation keys. It needs a WL-6 universal fuel cell and a trained roboticist.

ent-CMUCrateRoboticsFabricator = robotics fabricator crate
    .desc = A sealed crate containing one Weyland-Yutani robotics fabricator and a WL-6 universal fuel cell to power it. Pry it open with a crowbar.

ent-CMURoboticsOrderConsole = robotics order console
    .desc = A Weyland-Yutani contract terminal for robotics work. Accept a build or repair order, receive its synthetic transport crates in cargo, then attach the order slip to a loaded crate and lower the platform. It can be anchored, moved and rotated.

ent-CMUCrateRoboticsOrderConsole = robotics order console crate
    .desc = A sealed crate containing one Weyland-Yutani robotics order console. Pry it open with a crowbar.

ent-CMUCrateSyntheticTransport = synthetic transport crate
    .desc = A black lockable transport crate sized for one synthetic. Attach a robotics order slip to its label slot before lowering it with the cargo platform.

ent-CMUWorkingJoeRepair = damaged Working Joe
    .desc = A soulless Working Joe sent for repair. It is missing limbs and has burn and blunt damage.

ent-CMUCombatDrone = gun UGV
    .desc = A remotely operated tracked UGV with an IFF-equipped pulse gun and a 180-degree forward firing arc.

ent-CMUFlamerDrone = flamer UGV
    .desc = A remotely operated tracked UGV with twin claw-mounted flame nozzles.

ent-CMUDroneAndroidUnarmed = unarmed drone android
    .desc = A stripped-down synthetic drone frame with exposed servos. Its control bus is deliberately locked out of firearm use.

ent-CMUDroneAndroidUnarmedRepair = damaged unarmed drone android
    .desc = A repair-order drone android. It is missing limbs and has burn and blunt damage.

ent-CMUDroneAndroidCombatRepair = damaged combat drone android
    .desc = A repair-order combat drone android. It is missing limbs and has burn and blunt damage.

ent-CMUDroneAndroidCombat = combat drone android
    .desc = A reinforced synthetic drone frame with a combat-rated control bus, weapon routines, and a layer of corporate armour.

ent-CMUDroneBodyFrameUnarmed = unarmed drone android body frame
    .desc = A bare synthetic torso frame whose control bus is locked out of firearm use. Its limb ports are sealed and its activation socket is empty.

ent-CMUDroneBodyFrameCombat = combat drone android body frame
    .desc = A reinforced synthetic torso frame with a combat-rated control bus. Its limb ports are sealed and its activation socket is empty.

cmu-lathe-category-robotics-drone-limbs = Drone limbs
cmu-lathe-category-robotics-synth-limbs = Synthetic limbs
cmu-lathe-category-robotics-working-joe-limbs = Working Joe limbs
cmu-lathe-category-robotics-prosthetics = Human prosthetics
cmu-lathe-category-robotics-components = Components

cmu-robotics-fabricator-cell-already = The fabricator already has a fuel cell.
cmu-robotics-fabricator-cell-insert-start = You start inserting the fuel cell into the fabricator.
cmu-robotics-fabricator-cell-insert-fail = The fuel cell does not fit.
cmu-robotics-fabricator-cell-insert-finish = You insert the fuel cell into the fabricator.
cmu-robotics-fabricator-cell-none = The fabricator has no fuel cell to remove.
cmu-robotics-fabricator-cell-remove-start = You start removing the fuel cell from the fabricator.
cmu-robotics-fabricator-cell-remove-finish = You remove the fuel cell from the fabricator.
cmu-robotics-fabricator-examine-cell-inserted = The fuel cell is [color=cyan]inserted[/color].
cmu-robotics-fabricator-examine-cell-empty = The fuel cell is [color=red]not inserted[/color].
cmu-robotics-fabricator-examine-research = Requires [color=cyan]Research {$level}[/color].
cmu-robotics-fabricator-skill-required = You need Research {$level} to operate this fabricator.

cmu-robotics-console-title = Robotics Order Console
cmu-robotics-console-heading = ROBOTICS ORDERS
cmu-robotics-console-subheading = Weyland-Yutani contract terminal
cmu-robotics-console-active = Active order
cmu-robotics-console-available = Available orders
cmu-robotics-console-no-active = No order accepted.
cmu-robotics-console-no-orders = No orders available.
cmu-robotics-console-active-exists = Finish or cancel the current order first.
cmu-robotics-console-accepted-repair = Order accepted: {$target}. {$crates} synthetic transport crates with {$amount} damaged units were added to the cargo queue. An order slip was printed; raise the platform (lower and raise it if already raised) to receive the crates, then put each repaired unit into a crate, attach the slip to a loaded crate, and lower the platform. Reward: {$reward}.
cmu-robotics-console-accepted-build = Order accepted: {$target}. {$crates} empty synthetic transport crate was added to the cargo queue. An order slip was printed; raise the platform (lower and raise it if already raised) to receive the crate, put the finished android inside, attach the slip to the crate, and lower the platform. Reward: {$reward}.
cmu-robotics-console-no-queue-space = The order cannot be accepted: the cargo queue does not have enough space (needs {$needed}, {$free} free).
cmu-robotics-console-no-platform = No corporate cargo platform was found.
cmu-robotics-console-rejected = The customer is not satisfied: {$reason}. No payment was made.
cmu-robotics-console-delivered = Order successfully delivered: {$target}. {$reward} was transferred to the Weyland-Yutani account.
cmu-robotics-console-cancelled = Order cancelled.
cmu-robotics-console-print-slip = Print order slip
cmu-robotics-console-slip-printed = Order slip printed.
cmu-robotics-slip-attach = Attach order slip
cmu-robotics-slip-remove = Remove order slip
cmu-robotics-console-time-left = Time left
cmu-robotics-console-failed-timeout = The order deadline expired. A fine of {$reward} was charged to the Weyland-Yutani account.
cmu-robotics-console-refresh-active = You cannot refresh the list while an order is active.
cmu-robotics-console-refreshed = Order list refreshed.
cmu-robotics-console-type = Type
cmu-robotics-console-target = Target
cmu-robotics-console-module = Required module
cmu-robotics-console-amount = Units
cmu-robotics-console-reward = Reward
cmu-robotics-console-type-build = Build
cmu-robotics-console-type-repair = Repair
cmu-robotics-console-order-heading = {$type}: {$target}
cmu-robotics-console-accept = Accept order
cmu-robotics-console-cancel = Cancel
cmu-robotics-console-refresh = Refresh list
cmu-robotics-console-delivery-hint-repair = Put each repaired unit into a synthetic transport crate, attach the order slip to a loaded crate, and lower the cargo platform.
cmu-robotics-console-delivery-hint-build = Put the finished android into a synthetic transport crate, attach the order slip to it, and lower the cargo platform.
cmu-robotics-console-reason-wrong = this is not the ordered unit
cmu-robotics-console-reason-broken = the unit is broken or damaged
cmu-robotics-console-reason-not-repaired = the unit is not repaired
cmu-robotics-console-reason-missing-units = not all {$amount} order units are inside transport crates on the platform
cmu-robotics-console-reason-limbs = the unit is still missing limbs or extremities
cmu-robotics-console-reason-wrong-limb = the unit has a limb from the wrong robotics limb set
cmu-robotics-console-reason-module = the {$module} is not installed
cmu-robotics-console-reason-no-slip = the order slip is not attached to a transport crate
cmu-robotics-console-reason-slip-not-on-crate = the order slip must be attached to a transport crate that contains an order unit
cmu-robotics-slip-name = robotics work order #{$id}
cmu-robotics-slip-content = [head=2]WEYLAND-YUTANI ROBOTICS WORK ORDER #{$id}[/head]\nOrder: {$target}. Units: {$amount}. Reward: {$reward}. Deadline: {$minutes} minutes. Put the finished units into synthetic transport crates, attach this slip to a loaded crate, and lower the corporate cargo platform.

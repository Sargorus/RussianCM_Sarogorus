ent-CMURoboticsFabricator = robotics fabricator
    .desc = A Weyland-Yutani fabricator for drone frames, limbs, control tablets, modules and tracked chassis. It needs a WL-6 universal fuel cell and a trained roboticist.

ent-CMUCrateRoboticsFabricator = robotics fabricator crate
    .desc = A crate containing one Weyland-Yutani robotics fabricator and a WL-6 universal fuel cell to power it.

ent-CMURoboticsOrderConsole = robotics order console
    .desc = A Weyland-Yutani contract terminal for robotics work. Accept a build or repair order, then place the finished drone next to the console and submit it.

ent-CMUCrateRoboticsOrderConsole = robotics order console crate
    .desc = A crate containing one Weyland-Yutani robotics order console.

ent-CMUCombatDrone = gun UGV
    .desc = A remotely operated tracked UGV with an IFF-equipped pulse gun and a 180-degree forward firing arc.

ent-CMUFlamerDrone = flamer UGV
    .desc = A remotely operated tracked UGV with twin claw-mounted flame nozzles.

ent-CMUDroneAndroidUnarmed = unarmed drone android
    .desc = A stripped-down synthetic drone frame with exposed servos. Its control bus is deliberately locked out of firearm use.

ent-CMUDroneAndroidCombat = combat drone android
    .desc = A reinforced synthetic drone frame with a combat-rated control bus, weapon routines, and a layer of corporate armour.

ent-CMUDroneBodyFrameUnarmed = unarmed drone android body frame
    .desc = A bare synthetic torso frame whose control bus is locked out of firearm use. Its limb ports are sealed and its activation socket is empty.

ent-CMUDroneBodyFrameCombat = combat drone android body frame
    .desc = A reinforced synthetic torso frame with a combat-rated control bus. Its limb ports are sealed and its activation socket is empty.

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
cmu-robotics-console-accepted = Order accepted: {$target}. Reward: {$reward}.
cmu-robotics-console-repair-spawn-failed = Failed to issue the damaged unit.
cmu-robotics-console-no-repair-target = No damaged unit for this order is next to the console.
cmu-robotics-console-no-build-target = No drone of the required model is next to the console.
cmu-robotics-console-rejected = The customer is not satisfied: {$reason}. No payment was made.
cmu-robotics-console-no-id = No ID card found to receive the payment.
cmu-robotics-console-delivered = Order successfully delivered: {$target}. The customer paid {$reward}.
cmu-robotics-console-cancelled = Order cancelled.
cmu-robotics-console-refresh-active = You cannot refresh the list while an order is active.
cmu-robotics-console-refreshed = Order list refreshed.
cmu-robotics-console-type = Type
cmu-robotics-console-target = Target
cmu-robotics-console-module = Required module
cmu-robotics-console-reward = Reward
cmu-robotics-console-type-build = Build
cmu-robotics-console-type-repair = Repair
cmu-robotics-console-order-heading = {$type}: {$target}
cmu-robotics-console-accept = Accept order
cmu-robotics-console-submit = Submit order
cmu-robotics-console-cancel = Cancel
cmu-robotics-console-refresh = Refresh list
cmu-robotics-console-delivery-hint = Place the finished drone next to the console (within 2 metres) and press Submit order.
cmu-robotics-console-reason-wrong = this is not the ordered unit
cmu-robotics-console-reason-broken = the unit is broken
cmu-robotics-console-reason-not-repaired = the unit is not repaired
cmu-robotics-console-reason-module = the {$module} is not installed

﻿generator-clogged = {$generator} раптово зупиняється!

portable-generator-verb-start = Запустити генератор
portable-generator-verb-start-msg-unreliable = Запускає генератор. Можливо треба буде декілька спроб.
portable-generator-verb-start-msg-reliable = Запускає генератор.
portable-generator-verb-start-msg-unanchored = Генератор спочатку треба пригвинтити!
portable-generator-verb-stop = Зупинити генератор
portable-generator-start-fail = Ти дьорнув за дріт, але нажаль генератор не запустився.
portable-generator-start-success = Ти дьоргаєш за дріт та генератор запускається.

portable-generator-ui-title = Ручний Генератор
portable-generator-ui-status-stopped = Зупинен:
portable-generator-ui-status-starting = Стартує:
portable-generator-ui-status-running = Працює:
portable-generator-ui-start = Запустити
portable-generator-ui-stop = Зупинити
portable-generator-ui-target-power-label = Цільова видача (кВт):
portable-generator-ui-efficiency-label = Ефективність:
portable-generator-ui-fuel-use-label = Використання палива:
portable-generator-ui-fuel-left-label = Залишилося палива:
portable-generator-ui-clogged = У паливі інорідні хімікати!
portable-generator-ui-eject = Вийняти
portable-generator-ui-eta = (~{ $minutes } хв.)
portable-generator-ui-unanchored = Відгвинчено
portable-generator-ui-current-output = Поточна видача: {$voltage}
portable-generator-ui-network-stats = Зв'язок:
portable-generator-ui-network-stats-value = { POWERWATTS($supply) } / { POWERWATTS($load) }
portable-generator-ui-network-stats-not-connected = Не під'єднано

power-switchable-generator-examine = Видача живлення {$voltage}.
power-switchable-generator-switched = Перемкнуто живлення на {$voltage}!

power-switchable-voltage = { $voltage ->
    [HV] [color=orange]ВВ[/color]
    [MV] [color=yellow]СВ[/color]
    *[LV] [color=green]НВ[/color]
}
power-switchable-switch-voltage = Перемкнути на {$voltage}

fuel-generator-verb-disable-on = Спочатку вимкнить генератор!

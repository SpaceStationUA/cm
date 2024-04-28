reagent-effect-condition-guidebook-total-damage =
    { $max ->
        [2147483648] має щонайменше {NATURALFIXED($min, 2)} загальних ушкоджень
        *[other] { $min ->
                    [0] має щонайбільш {NATURALFIXED($max, 2)} загальних ушкоджень
                    *[other] має між {NATURALFIXED($min, 2)} та {NATURALFIXED($max, 2)} загальних ушкоджень
                 }
    }

reagent-effect-condition-guidebook-total-hunger =
    { $max ->
        [2147483648] має щонайменш {NATURALFIXED($min, 2)} загального голоду
        *[other] { $min ->
                    [0] має щонайбільше {NATURALFIXED($max, 2)} загального голоду
                    *[other] має між {NATURALFIXED($min, 2)} та {NATURALFIXED($max, 2)} загального голоду
                 }
    }

reagent-effect-condition-guidebook-reagent-threshold =
    { $max ->
        [2147483648] щонайменше {NATURALFIXED($min, 2)}ю {$reagent}
        *[other] { $min ->
                    [0] щонайбільше {NATURALFIXED($max, 2)}ю {$reagent}
                    *[other] між {NATURALFIXED($min, 2)}ю та {NATURALFIXED($max, 2)}ю {$reagent}
                 }
    }

reagent-effect-condition-guidebook-mob-state-condition =
    сутність { $state }

reagent-effect-condition-guidebook-solution-temperature =
    температура рідини { $max ->
            [2147483648] щонайменше {NATURALFIXED($min, 2)}k
            *[other] { $min ->
                        [0] щонайбільше {NATURALFIXED($max, 2)}k
                        *[other] між {NATURALFIXED($min, 2)}к та {NATURALFIXED($max, 2)}к
                     }
    }

reagent-effect-condition-guidebook-body-temperature =
    температура тіла { $max ->
            [2147483648] щонайменше {NATURALFIXED($min, 2)}к
            *[other] { $min ->
                        [0] щонайбільше {NATURALFIXED($max, 2)}к
                        *[other] між {NATURALFIXED($min, 2)}к та {NATURALFIXED($max, 2)}к
                     }
    }

reagent-effect-condition-guidebook-organ-type =
    метаболічний орган { $shouldhave ->
                                [true]
                                *[false] не
                           } {INDEFINITE($name)} {$name} орган

reagent-effect-condition-guidebook-has-tag =
    ціль { $invert ->
                 [true] не має
                 *[false] має
                } мітки {$tag}
reagent-effect-condition-guidebook-this-reagent = цей реагент

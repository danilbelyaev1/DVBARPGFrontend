# Настройка Animator и привязка анимаций к скиллам (quick_shot, unarmed_strike и др.)

**Анимации кастуются только для атакующего скилла и мувмент-скилла.** Support A и Support B анимации не запускаются.

---

## Как сейчас работают анимации

1. **Сервер** в каждом снапшоте может выставить `Player.AttackAnimTriggered = true` (атака) и присылает `MovementActive` + `MovementSkillId` (мувмент). В том же снапшоте приходит словарь **Cooldowns** (SkillId → оставшееся время кулдауна в секундах).

2. **SkillPresentationDriver** подписан на снапшоты. При `AttackAnimTriggered` вызывает `PlaySkillEvent(attackSkillId, CastStart, cooldown)`. При старте/остановке мувмента — `PlaySkillEvent(movementSkillId, MovementStart/Stop, cooldown)`. Кулдаун берётся из `snap.Cooldowns[skillId]`.

3. **PlayerAbilityAnimationDriver.PlaySkill(skillId, cooldownSec)**:
   - выставляет **CastMode** (0 = stand_only, 1 = move_only, 2 = any) из каталога скиллов;
   - дергает триггер **UseSkill**;
   - по SkillId находит триггер в списке Abilities (или во встроенных маппингах) и дергает его (например **QuickShot**);
   - поднимает вес слоёв атак до 1;
   - если задан параметр **AttackSpeed** в Animator и передан `cooldownSec > 0`, в том же кадре/Update подставляет **скорость анимации** так, чтобы длина клипа / скорость = кулдаун (анимация успевает проиграться за время кулдауна).

4. В **Animator** срабатывает переход по триггеру (например Any State → QuickShot), проигрывается клип. Вес слоя атак плавно сбрасывается, когда стейт заканчивается.

5. **Скорость анимации:** в слое скилла в стейте должен быть параметр **Speed** (Multiply by Speed) или аналог. В драйвере задаётся имя float-параметра (по умолчанию `AttackSpeed`). Драйвер выставляет `speed = clipLength / cooldownSec` (с ограничением min/max), чтобы визуал совпадал с кулдауном.

---

## Встроенные маппинги (работают без заполнения Inspector)

В `PlayerAbilityAnimationDriver` уже заданы маппинги только для **атаки** и **мувмента**:

| SkillId          | Триггер в Animator |
|------------------|---------------------|
| `slash`          | Slash               |
| `quick_shot`     | QuickShot           |
| `arc_bolt`       | ArcBolt             |
| `unarmed_strike` | UnarmedStrike       |
| `dash`           | Dash                |
| `combat_roll`    | CombatRoll          |
| `rift_step`      | RiftStep            |

В Animator достаточно добавить параметры и стейты с этими именами — список Abilities в Inspector можно не заполнять. Заполнение в Inspector переопределяет встроенные значения для указанного SkillId.

### Дефолт по типу оружия

Если в **текущем слое** для скилла нет своей анимации, драйвер подставляет **дефолтную анимацию по типу оружия**. Оружие определяется так:

1. **По лоадуту:** из `ServerLoadout.AttackSkillId` (скилл атаки) через список **Skill To Weapon Type** → тип оружия (sword, bow, staff, unarmed).
2. **Если лоадут ещё не пришёл:** из `SelectedClassId` через список **Class To Weapon Type** → тип оружия.
3. По типу оружия в **Weapon Default Triggers** находится триггер (Slash, QuickShot, ArcBolt, UnarmedStrike и т.д.).

В Inspector задаются три списка:

| Список | Назначение |
|--------|------------|
| **Skill To Weapon Type** | Скилл атаки → тип оружия (slash→sword, quick_shot→bow, arc_bolt→staff, unarmed_strike→unarmed). |
| **Class To Weapon Type** | Класс → тип оружия (vanguard→sword, hunter→bow, mystic→staff). Используется, пока нет лоадута с сервера. |
| **Weapon Default Triggers** | Тип оружия → триггер анимации (sword→Slash, bow→QuickShot, staff→ArcBolt, unarmed→UnarmedStrike). |

- **Когда используется дефолт:**  
  - для данного SkillId нет записи в маппинге (Abilities), **или**  
  - для текущего слоя задан список «разрешённых» SkillId, и этот скилл в него не входит — тогда в этом слое играет дефолт по оружию.
- Если тип оружия не определён или для него нет триггера — используется общий **Fallback Trigger** (по умолчанию `Attack`).

**Списки по слоям (опционально):** **Attack Standing Skill Ids**, **Attack Moving Skill Ids**, **Attack Any Skill Ids**. Если список для слоя **не пустой**, то в этом слое «своя» анимация есть только для перечисленных SkillId; для остальных — дефолт по оружию. Если список пустой — все скиллы из маппинга считаются имеющими анимацию в этом слое.

Пример: в слое **AttackStanding** есть только стейты **Slash** и **UnarmedStrike**. В **Attack Standing Skill Ids** укажите `slash`, `unarmed_strike`. При атаке стоя скиллом `quick_shot` (лук в лоадуте) в этом слое будет играться дефолт по оружию **QuickShot** (bow→QuickShot).

---

## 1. Параметры в Animator

В окне **Animator** (на контроллере персонажа) во вкладке **Parameters** добавьте:

| Тип     | Имя           | Назначение |
|---------|----------------|------------|
| **Int** | `CastMode`     | Режим каста (0/1/2). Используется для выбора стейта (stand/move). |
| **Float** | `AttackSpeed` | Скорость воспроизведения анимации скилла. Драйвер подставляет значение по кулдауну (длина_клипа / cooldown). Опционально; если параметра нет — скорость не меняется. |
| **Trigger** | `UseSkill` | Общий триггер «играть скилл»; драйвер дергает его перед конкретным триггером. |
| **Trigger** | `QuickShot` | Для SkillId `quick_shot`. |
| **Trigger** | `UnarmedStrike` | Для SkillId `unarmed_strike`. |
| **Trigger** | `Slash` | Для `slash`. |
| **Trigger** | `ArcBolt` | Для `arc_bolt`. |
| **Trigger** | `Attack` | Fallback (имя задаётся в драйвере как `fallbackTrigger`). |

Имена триггеров должны **точно совпадать** с именами стейтов и с теми, что в списке Triggers в `PlayerAbilityAnimationDriver`.

---

## 2. Слои (Layers) в Animator и нужная структура

В драйвере по умолчанию ожидаются слои **в таком порядке** (порядок в списке **Attack Layer Names** задаёт соответствие CastMode):

| Индекс в списке | Имя слоя       | Когда вес = 1          |
|-----------------|----------------|------------------------|
| 0               | `AttackStanding` | CastMode 0 (stand_only) |
| 1               | `AttackMoving`   | CastMode 1 (move_only) |
| 2               | `AttackAny`      | CastMode 2 (any)       |

Драйвер поднимает вес **только у одного** слоя — того, чей индекс совпадает с CastMode. Остальные остаются с весом 0.

### Структура каждого слоя (AttackStanding, AttackMoving, AttackAny)

У всех трёх слоёв структура **одинаковая**; различаться могут только клипы (например, атака стоя и атака в движении — разные анимации).

1. **State Machine слоя**
   - **Entry** → по умолчанию ведёт в стейт «покой» (например **Idle** или **Empty**).
   - **Any State** → от него переходы в каждый стейт скилла по триггеру.
   - Стейты скиллов: **QuickShot**, **Slash**, **UnarmedStrike**, **ArcBolt**, **Dash**, **CombatRoll**, **RiftStep**, при необходимости **Attack** (fallback). Имя стейта = имя триггера.
   - У каждого стейта скилла в **Motion** — нужный Animation Clip (в AttackStanding — вариант «стоя», в AttackMoving — «в движении», в AttackAny — универсальный или любой).

2. **Переходы**
   - **Any State → QuickShot**: Conditions — триггер **QuickShot**, Has Exit Time выключен.
   - **Any State → Slash**: Conditions — триггер **Slash**, Has Exit Time выключен.
   - Аналогично для остальных триггеров (UnarmedStrike, ArcBolt, Dash, CombatRoll, RiftStep, Attack).
   - **QuickShot → Exit** (или в Idle/Empty): один переход с **Has Exit Time** (Normalized Time ≈ 0.9–1), без условий. То же для Slash, UnarmedStrike и т.д.

3. **Параметр скорости (опционально)**  
   В каждом стейте скилла: включить **Speed** (Multiply by Speed), параметр — **AttackSpeed**. Тогда драйвер подставит скорость по кулдауну.

4. **Weight** слоя вручную не трогать — драйвер выставляет 1 только у слоя по CastMode и сбрасывает к 0 после окончания анимации. **Blending** — обычно Override; Mask — по желанию (например, только верх тела).

### Сводка по слоям

```
AttackStanding (индекс 0, вес при CastMode 0)
├── Entry → Idle/Empty
├── Any State → [QuickShot, Slash, UnarmedStrike, ArcBolt, ...] по триггеру
├── Стейты: QuickShot, Slash, UnarmedStrike, ArcBolt, Dash, CombatRoll, RiftStep, Attack
└── Каждый стейт → Exit (Has Exit Time)

AttackMoving (индекс 1, вес при CastMode 1)
├── та же структура, можно другие клипы для «атака в движении»
└── ...

AttackAny (индекс 2, вес при CastMode 2)
├── та же структура
└── ...
```

Можно использовать **один** слой (например, только **AttackAny**) — тогда в **Attack Layer Names** оставьте один элемент; CastMode 0 и 1 тоже будут вести на него (индекс 0).

---

## 3. Стейты атаки и имена

В слое атаки (например, **AttackAny**) создайте по одному стейту на каждую анимацию:

- Имя стейта **должно совпадать с именем Trigger-параметра**:
  - стейт `QuickShot` → клип анимации выстрела;
  - стейт `UnarmedStrike` → клип удара без оружия;
  - стейт `Slash` → клип удара мечом;
  - стейт `ArcBolt` → клип заклинания и т.д.

Причина: драйвер по `state.shortNameHash` определяет, что мы в «атакующем» стейте, и держит вес слоя, пока анимация не закончится. Имя стейта = имя триггера.

**Скорость анимации:** в каждом стейте скилла (QuickShot, Slash и т.д.) в Inspector стейта включите **Speed** (Multiply by Speed) и привяжите к параметру **AttackSpeed** (или тому имени, что указано в `PlayerAbilityAnimationDriver` → Attack Speed Param). Тогда драйвер будет подставлять скорость так, чтобы анимация укладывалась в кулдаун с сервера.

**Как создать стейт:**

1. ПКМ по пустому месту в слое → **Create State** → **Empty** (или сразу **From New Clip**).
2. Переименуйте стейт в точности как триггер (например `QuickShot`).
3. В **Motion** перетащите нужный Animation Clip.
4. Повторите для UnarmedStrike, Slash, ArcBolt и т.д.

---

## 4. Переходы (Transitions)

Чтобы по триггеру запускалась нужная анимация:

1. **Откуда:** в слое атаки сделайте стейт **Any State** (если его нет — он обычно есть по умолчанию) или отдельный стейт **Entry**.
2. **Куда:** стейт с анимацией (например, `QuickShot`).
3. Создайте **Transition**: Any State → QuickShot.
4. В переходе:
   - **Conditions**: нажмите **+** и выберите триггер **QuickShot**.
   - **Has Exit Time** — выключено (чтобы срабатывало по триггеру, а не по времени).
   - **Transition Duration** — по вкусу (0 или короткое, чтобы не было задержки).
5. Обратный переход: из **QuickShot** обратно в стейт «покоя» или **Exit**:
   - либо переход с **Has Exit Time** (Normalized Time ≈ 0.9–1), без условия;
   - либо отдельный стейт Idle в этом слое и переход QuickShot → Idle по Has Exit Time.

Аналогично для **UnarmedStrike**, **Slash**, **ArcBolt**: переход из Any State в одноимённый стейт по одноимённому триггеру.

---

## 5. Привязка SkillId к триггеру в Unity (Inspector)

На объекте с `PlayerAbilityAnimationDriver` в списке **Abilities** задаётся соответствие **SkillId (с сервера) → триггеры в Animator**:

| SkillId (строка)   | Triggers (список)   |
|--------------------|---------------------|
| `quick_shot`       | `QuickShot`         |
| `unarmed_strike`   | `UnarmedStrike`     |
| `slash`            | `Slash`             |
| `arc_bolt`         | `ArcBolt`           |

**Как заполнить:**

1. Выберите объект игрока (где висит `PlayerAbilityAnimationDriver`).
2. В компоненте найдите **Способности игрока** → **Abilities**.
3. Нажмите **+** для новой строки.
4. В **Skill Id** введите ровно то, что приходит с сервера/лоадута: `quick_shot`, `unarmed_strike`, `slash`, `arc_bolt` и т.д. (без пробелов, как в API).
5. В **Triggers** нажмите **+** и введите имя триггера так же, как в Animator: `QuickShot`, `UnarmedStrike`, `Slash`, `ArcBolt`.

Если для одного SkillId нужно несколько вариантов анимации, добавьте в Triggers несколько имён (например `QuickShot`, `QuickShot2`). Драйвер выберет один из них по режиму **Variant Mode** (Random или Round Robin).

**Fallback:** если SkillId не найден в списке, сработает **Fallback Trigger** (по умолчанию `Attack`). Для этого в Animator нужны параметр и стейт с именем `Attack`.

---

## 6. Краткая схема потока

1. Сервер в снапшоте выставляет `Player.AttackAnimTriggered = true`.
2. `SkillPresentationDriver` получает снапшот и вызывает `PlaySkillEvent(attackSkillId, CastStart)`, где `attackSkillId` — из лоадута (например `quick_shot`).
3. `PlayerAbilityAnimationDriver.PlaySkill("quick_shot")`:
   - выставляет **CastMode** (int);
   - дергает триггер **UseSkill**;
   - ищет в списке Abilities запись с SkillId `quick_shot` и дергает триггер из Triggers (например **QuickShot**);
   - поднимает вес слоёв атак до 1.
4. В Animator срабатывает переход Any State → **QuickShot** по триггеру **QuickShot**, играет клип.
5. Когда стейт заканчивается, драйвер в Update видит по `shortNameHash`, что мы вышли из атакующего стейта, и плавно сбрасывает вес слоя атак к 0.

---

## 7. Чек-лист

- [ ] В Animator есть параметры: **CastMode** (Int), **UseSkill** (Trigger), **QuickShot**, **UnarmedStrike**, **Slash**, **ArcBolt**, **Attack** (и другие по необходимости).
- [ ] Есть слой атаки с именем из списка в драйвере (например **AttackAny**).
- [ ] В этом слое есть стейты с именами **точно как триггеры**: QuickShot, UnarmedStrike, Slash, ArcBolt; каждому назначен свой клип.
- [ ] Переходы: Any State → каждый стейт по одноимённому триггеру, Has Exit Time выключен.
- [ ] Обратный переход со стейта атаки (по Has Exit Time или в Idle/Exit).
- [ ] В Inspector у `PlayerAbilityAnimationDriver` в **Abilities** заполнены пары: SkillId (`quick_shot`, `unarmed_strike`, …) → Triggers (`QuickShot`, `UnarmedStrike`, …).
- [ ] Fallback Trigger (например `Attack`) совпадает с именем параметра и стейта в Animator, если используете fallback.
- [ ] Для учёта скорости: в Animator есть Float **AttackSpeed**, в стейтах скилла включён **Speed** и привязан к этому параметру; в драйвере задано имя параметра (по умолчанию `AttackSpeed`). Кулдауны приходят в снапшоте и передаются в `PlaySkill(skillId, cooldownSec)`.

После этого при приходе с сервера атаки с `attackSkillId = "quick_shot"` или `"unarmed_strike"` будет проигрываться соответствующая анимация, при необходимости с подстроенной скоростью под кулдаун.

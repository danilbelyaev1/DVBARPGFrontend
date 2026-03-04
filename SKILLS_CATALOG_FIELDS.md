# Поля скиллов из `/skills/catalog`

Ниже приведены поля, которые приходят в каталоге скиллов, и их назначение.

## Основные поля
1. `Id` — уникальный идентификатор скилла (SkillId).
2. `Name` — отображаемое имя.
3. `Category` — категория (`attack`, `support`, `movement`).
4. `SkillFamily` — семейство (`melee`, `ranged`, `magic`, `utility`).
5. `RequiredLevel` — минимальный уровень.
6. `CastMode` — режим каста (`stand_only`, `move_only`, `stand_or_move`).
7. `CooldownSec` — базовый кулдаун.
8. `Range` — дальность.
9. `BaseDamage` — базовый урон.
10. `ProjectileSpeed` — скорость снаряда (0 = без снаряда).
11. `ProjectileRadius` — радиус снаряда/AoE.
12. `Tags` — теги скилла (правила/баланс/фильтрация).
13. `DamageType` — тип урона (например `phys`, `fire`, `none`).
14. `StatusId` — идентификатор статуса.
15. `StatusChance` — шанс наложения статуса.
16. `StatusDurationSec` — длительность статуса.
17. `StatusDps` — урон статуса в секунду.

## Movement-поля (только когда `Category == movement`)
1. `MovementDurationSec` — длительность движения.
2. `MovementSpeedMultiplier` — множитель скорости.
3. `MovementSpeedRamp` — плавный разгон.
4. `MovementDamageTakenMultiplier` — множитель входящего урона.
5. `MovementEvasionChance` — шанс уклонения.
6. `MovementLockAttacks` — блок атак во время движения.
7. `MovementFrontBlock` — фронтальный блок.
8. `MovementNoCooldown` — отсутствие кулдауна.
9. `MovementRequiresShield` — требует щит.
10. `MovementPullRadius` — радиус притяжения.
11. `MovementPullStrength` — сила притяжения.
12. `MovementTickIntervalSec` — интервал тика эффекта.
13. `MovementTickDamageScale` — множитель урона тиком.

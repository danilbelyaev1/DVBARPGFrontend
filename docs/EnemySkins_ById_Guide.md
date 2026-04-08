# Система скинов врагов по ID: инструкция

Документ описывает:

- что сделать **сейчас**, чтобы система заработала в проекте;
- как потом добавлять новых врагов/скины/анимации без правок кода.

## 1) Что уже реализовано в коде

Добавлены компоненты и каталоги:

- `Assets/Game/Network/EnemySkinCatalog.cs`
- `Assets/Game/Network/EnemyAnimationSetCatalog.cs`
- `Assets/Game/Network/EnemyVisualHost.cs`
- `Assets/Game/Network/EnemySkinResolver.cs`
- `Assets/Game/Network/EnemyAnimationBinder.cs`
- интеграция в `Assets/Game/Scripts/Network/NetworkMonstersReplicator.cs`

Логика:

- монстр получает `skinId` (override по `monsterId` или default по `monsterType`);
- резолвер выбирает визуал из каталога;
- биндер применяет анимации через `animationSetId` (`baseController` / `overrideController` / `avatar`).

## 2) Что нужно сделать сейчас (в Unity)

### Шаг 1. Создать каталог наборов анимаций

1. В Project: `Create -> DVBARPG -> Enemies -> Animation Set Catalog`.
2. Назовите, например: `EnemyAnimationSetCatalog.asset`.
3. Для каждого набора заполните:
  - `animationSetId` (пример: `humanoid_melee_v1`);
  - `baseController` (или `overrideController`);
  - `avatar` (опционально, если риг отличается).

### Шаг 2. Создать каталог скинов

1. В Project: `Create -> DVBARPG -> Enemies -> Skin Catalog`.
2. Назовите, например: `EnemySkinCatalog.asset`.
3. Для каждого скина заполните:
  - `skinId` (уникальный, пример: `wolf_black_01`);
  - `monsterType` (тип из снапшота, например `melee`/`ranged`);
  - `fallbackPrefab` (визуал врага);
  - `animationSetId` (должен совпадать с ID из animation catalog);
  - `isDefaultForType` (включить для дефолтного скина типа).

Примечание:

- поле `addressableKey` уже есть на будущее, но в текущей сборке используется `fallbackPrefab`.

### Шаг 3. Подключить каталоги в сцене Run

1. Откройте объект с `NetworkMonstersReplicator`.
2. Заполните поля:
  - `Enemy Skin Catalog` -> `EnemySkinCatalog.asset`;
  - `Enemy Animation Set Catalog` -> `EnemyAnimationSetCatalog.asset`;
  - `Fallback Skin Id` (опционально, если нужен общий fallback).

### Шаг 4. Проверить

1. Запустите Run.
2. Убедитесь, что монстры появляются с корректным визуалом и анимациями.
3. Если у типа нет скина, должен примениться fallback.

## 3) Как добавлять новых врагов потом

## Вариант A: новый визуал для существующего типа

1. Подготовьте новый префаб врага (с `Animator` и нужным ригом).
2. Добавьте новый `Entry` в `EnemySkinCatalog`:
  - новый `skinId`;
  - существующий `monsterType`;
  - `fallbackPrefab` = новый префаб;
  - `animationSetId` = нужный набор.
3. Если нужен новый набор анимаций, добавьте `Entry` в `EnemyAnimationSetCatalog`.

Готово: код менять не нужно.

## Вариант B: новый тип врага из сервера

1. Убедитесь, что сервер в `MonsterSnapshot.Type` присылает новый `monsterType`.
2. В `EnemySkinCatalog` добавьте хотя бы один `Entry` с:
  - `monsterType` = новый тип;
  - `isDefaultForType` = true.
3. Привяжите префаб и `animationSetId`.

Готово: новый тип подхватится по default для типа.

## 4) Runtime override (для ивентов/скриптов)

Можно принудительно назначать скин конкретному монстру по его `monsterId`:

- `NetworkMonstersReplicator.SetSkinOverride(monsterId, skinId);`
- `NetworkMonstersReplicator.ClearSkinOverride(monsterId);`

Это удобно для временных эффектов, элитных мобов, сезонных ивентов.

## 5) Частые проблемы

- **Нет анимации**: проверьте, что в visual prefab есть `Animator`.
- **Не те клипы**: проверьте соответствие `animationSetId` между двумя каталогами.
- **Неверный скин**: проверьте `monsterType` и флаг `isDefaultForType`.
- **Пустой результат**: проверьте, что в `NetworkMonstersReplicator` назначены оба каталога.
- **Type mismatch в `fallbackPrefab`**:
  - поле `fallbackPrefab` в коде имеет тип `GameObject` (`EnemySkinCatalog.Entry.fallbackPrefab`);
  - перетаскивайте **только prefab-asset из окна Project** (синий куб), а не объект из `Hierarchy`;
  - нельзя назначать `AnimatorController`, `Avatar`, `AnimationClip` или `FBX`-подассет вместо prefab;
  - если у вас только модель (`.fbx`), сначала создайте из неё prefab (`Create Prefab Variant` или перетащите в сцену и сохраните как prefab), затем назначьте этот prefab;
  - у назначаемого prefab желательно должен быть `Animator` на корне или дочернем объекте.

## 6) Рекомендация по именованию ID

- `skinId`: `race_or_theme_variant_index`  
Пример: `orc_warrior_01`, `undead_elite_02`
- `animationSetId`: `rig_role_version`  
Пример: `humanoid_melee_v1`, `beast_ranged_v1`


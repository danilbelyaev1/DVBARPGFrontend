# CONTENT_GUIDE.md — контент для скиллов/анимаций/карт

Документ для работы с презентацией скиллов, анимациями атак и будущими картами.

## 1) Архитектура презентации скиллов (клиент)

Сервер рассчитывает бой. Клиент только показывает визуал по `SkillId` и событиям.

Основные элементы:
- `SkillPresentation` — ScriptableObject с `SkillId`, триггером анимации и списком VFX по событиям.
- `SkillPresentationCatalog` — каталог всех презентаций по `SkillId`.
- `SkillSocketLocator` — поиск сокетов для привязки VFX (руки, muzzle, ground и т.д.).
- `SkillPresentationDriver` — слушает снапшоты и запускает анимацию/VFX.
- `PlayerAbilityAnimationDriver` — дергает Animator-триггеры по `SkillId`.
- `SkillPresentationBootstrap` — загрузка `/skills/catalog` и `/characters/{characterId}` для подстановки скиллов.

Интеграция с сервером:
- Получаем `/skills/catalog` из runtime.
- Получаем `/characters/{characterId}` для экипированных скиллов.
- По `SkillId` подключаем нужные презентации (анимации/VFX/меши) без игровой логики.

## 2) Как добавить новый скилл (анимации/VFX/меши)

1. Убедиться, что `SkillId` существует на сервере и приходит в `/skills/catalog`.
2. (Опционально) создать папку: `Assets/Game/Skills/Presentation/<SkillId>/`.
3. Создать ScriptableObject:
   - `Assets/Create/DVBARPG/Skills/Skill Presentation`.
   - Заполнить:
     - `SkillId` — идентификатор из сервера.
     - `AnimationTrigger` — триггер в Animator (если пусто, используется маппинг в `PlayerAbilityAnimationDriver`).
     - `VFX` — список эффектов по событиям.
4. Добавить созданный `SkillPresentation` в `SkillPresentationCatalog` (если используете базовый каталог).
5. В персонаже:
   - На объекте игрока должен быть `PlayerAbilityAnimationDriver`.
   - На объекте игрока должен быть `SkillPresentationDriver`.
   - На объекте игрока должен быть `SkillPresentationBootstrap`.
   - В `SkillPresentationDriver` указать `SkillPresentationCatalog`, `SkillSocketLocator`, `VfxRoot`.
6. В `SkillSocketLocator` зарегистрировать сокеты, например `Hand_R`, `Muzzle`, `Ground`.
7. В Animator убедиться, что триггеры совпадают с названиями стейтов (shortNameHash).
8. Проверка:
   - `SkillPresentationBootstrap` получает `/skills/catalog` и `/characters/{characterId}`.
   - При `AttackAnimTriggered` из снапшота должна играть анимация и VFX.

Примечания:
- Клиент не считает урон и логику. Только визуал.
- Если анимация не проигрывается, сначала проверить: `SkillId`, `AnimationTrigger`, и маппинг `PlayerAbilityAnimationDriver`.

## 3) Как добавить анимации атак для монстров

Анимации монстров подключаются через `MonsterAnimationDriver`.

Шаги:
1. В Animator монстра создать стейты атак с названиями, совпадающими с триггерами.
2. В `MonsterAnimationDriver` заполнить:
   - `MeleeTriggers` — триггеры ближней атаки.
   - `RangedTriggers` — триггеры дальней атаки.
3. Убедиться, что в сетевых снапшотах для монстра `state` содержит значение атаки (по умолчанию `attack`).
4. Если атака слишком быстрая/медленная:
   - Настроить `defaultMeleeCooldown` / `defaultRangedCooldown`.
   - Включить `autoDetectAnimLength`, чтобы скорость подбиралась от длины клипа.

## 4) Добавление новых карт (будущая инструкция)

Текущее направление (когда будем делать карты):
1. Подготовить сцену/меши карты в Unity.
2. Использовать `Assets/Tools/Editor/RuntimeMapExporter.cs` для экспорта карты в JSON.
3. Использовать `Assets/Tools/Editor/IslandObstacleGenerator.cs` для генерации ObstacleMesh.
4. Готовый JSON размещается на сервере runtime/laravel (не трогаем без отдельного запроса).

Детализация будет добавлена, когда начнем полноценный пайплайн карт.

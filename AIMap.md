# AIMap (Frontend Scripts Only)

Карта **только скриптов** в клиентской части.  
Без ассетов, анимаций, сцен и серверного кода.

## Документация
- `AGENTS.md` — правила работы и ограничения по изменениям.
- `PROJECT_OVERVIEW.md` — архитектура, кто что вызывает, где искать по задаче.
- `AIConcept.md` — концепция игры, боёвка и управление.
- `README.md` — краткое описание проекта.

## Assets/Core
- `Assets/Core/GameRoot.cs` — корневой синглтон, регистрирует сервисы и запускает систему.
- `Assets/Core/Services/AuthSession.cs` — модель авторизационной сессии.
- `Assets/Core/Services/ClassLoadoutPresets.cs` — пресеты лоадута по классу (vanguard/hunter/mystic).
- `Assets/Core/Services/RuntimeMetaModels.cs` — модели /runtime/seasons/current и /runtime/characters.
- `Assets/Core/Services/InventoryModels.cs` — DTO инвентаря (InventoryItemDto, InventoryResult и т.д.).
- `Assets/Core/Services/MarketModels.cs` — DTO маркета и валюты (MarketListingDto, CurrencyBalanceResult и т.д.).
- `Assets/Core/Services/ServiceInterfaces.cs` — интерфейсы сервисов (Auth/Profile/Session и т.д.).
- `Assets/Core/Services/ServiceRegistry.cs` — простой DI-контейнер.
- `Assets/Core/Services/CharacterAppearanceData.cs` — DTO внешности персонажа (SpeciesId, Parts, BlendShapes, FaceBlendShapes, ColorPresetId) для API и Sidekick.
- `Assets/Core/Services/ClassSidekickSpeciesMap.cs` — маппинг classId → имя вида Sidekick (hunter→Elf и т.д.).

## Assets/Game/Animation
- `Assets/Game/Animation/PlayerAbilityAnimationDriver.cs` — анимации способностей игрока (SkillId -> Trigger).
- `Assets/Game/Animation/MonsterAnimationDriver.cs` — анимации атак монстров (melee/ranged).
- `Assets/Game/Animation/MovementAnimator.cs` — переключает анимации движения на объекте.

## Assets/Game/Camera
- `Assets/Game/Camera/TopDownFollowCamera.cs` — верхняя камера с зумом и следованием.

## Assets/Game/CharacterCreation
- `Assets/Game/CharacterCreation/SidekickAppearanceBuilder.cs` — сборка GameObject персонажа из CharacterAppearanceData (части, блендшейпы, морфы лица, цветовой пресет) для превью и Run.
- `Assets/Game/CharacterCreation/PreviewRotateOnDrag.cs` — вращение объекта по зажатой ПКМ (для превью в CharacterCreate).

## Assets/Game/Combat
- `Assets/Game/Combat/CombatSlots.cs` — константы слотов боя (attack/supportA/supportB).

## Assets/Game/Skills
- `Assets/Game/Skills/SkillPresentationDriver.cs` — драйвер презентации скиллов игрока (анимации/VFX по SkillId).
- `Assets/Game/Skills/SkillPresentationBootstrap.cs` — загрузка `/skills/catalog` и `/characters/{characterId}` для автоподстановки скиллов.
- `Assets/Game/Skills/SkillRangeCatalog.cs` — кэш Range по SkillId из `/skills/catalog`.
- `Assets/Game/Skills/Presentation/SkillPresentation.cs` — ScriptableObject презентации скилла.
- `Assets/Game/Skills/Presentation/SkillPresentationCatalog.cs` — каталог презентаций по SkillId.
- `Assets/Game/Skills/Presentation/SkillSocketLocator.cs` — поиск сокетов для VFX.
- `Assets/Game/Skills/Presentation/SkillVfxBinding.cs` — описание VFX по событию.
- `Assets/Game/Skills/Presentation/SkillVfxEventType.cs` — типы событий VFX.

## Assets/Game/Dev
- `Assets/Game/Dev/DevRunBootstrap.cs` — dev-старт забега из Run.

## Assets/Game/Network
- `Assets/Game/Network/MonsterCatalogClient.cs` — загрузка базовых статов монстров по HTTP (runtime/laravel).
- `Assets/Game/Network/MonsterCatalogBootstrap.cs` — автозапуск загрузки статов монстров в сцене Run.
- `Assets/Game/Network/FloatingDamageText.cs` — визуализация всплывающего урона.
- `Assets/Game/Network/NetworkDamageFromSnapshots.cs` — вывод урона по снапшотам.
- `Assets/Game/Network/NetworkMonstersReplicator.cs` — репликация монстров из снапшотов.
- `Assets/Game/Network/NetworkLootDropsReplicator.cs` — репликация дропов лута (золото/предметы), подбор по клику, команда pickup.
- `Assets/Game/Network/LootDropMarker.cs` — данные дропа на инстансе (Index, Type, Rarity, DisplayText); цвет рарности для тултипа.
- `Assets/Game/Network/LootDropTooltipUI.cs` — рамка с названием предмета при наведении (стиль PoE), цвет по рарности.
- `Assets/Game/Network/NetworkPlayerHpLabel.cs` — UI HP игрока.
- `Assets/Game/Network/NetworkProjectilesReplicator.cs` — репликация снарядов.
- `Assets/Game/Network/NetworkRunConnector.cs` — соединение с сервером из Run.
- `Assets/Game/Network/RunResultState.cs` — состояние завершения забега (смерть/досрочный выход) для экрана результатов.
- `Assets/Game/Network/RunEndController.cs` — подписка на RunEnded, скрытие игрока, метод досрочного выхода (finish).
- `Assets/Game/Network/RunSceneCleanup.cs` — при выгрузке Run отключает UDP-сессию (Disconnect).

## Assets/Game/Player
- `Assets/Game/Player/AutoSkillToggleController.cs` — переключение авто-использования слотов и отправка на сервер.
- `Assets/Game/Player/JoystickInput.cs` — виртуальный джойстик.
- `Assets/Game/Player/NetworkPlayerReplicator.cs` — репликация/предсказание игрока.
- `Assets/Game/Player/PlayerInputController.cs` — ввод (WASD/мышь/джойстик) и отправка в сеть.
- `Assets/Game/Player/PlayerTargetFacing.cs` — поворот игрока к ближайшей цели в радиусе максимального Range.

## Assets/Game/World
- `Assets/Game/World/UnifiedHeightSampler.cs` — единый сэмплер высоты (terrain + меши).

## Assets/Net/Commands
- `Assets/Net/Commands/IClientCommand.cs` — базовый интерфейс команды клиента.
- `Assets/Net/Commands/CmdMove.cs` — команда движения.
- `Assets/Net/Commands/CmdStop.cs` — команда остановки.
- `Assets/Net/Commands/CmdSlotToggle.cs` — команда включения/выключения слота.
- `Assets/Net/Commands/CmdFinish.cs` — команда досрочного завершения забега (finish).
- `Assets/Net/Commands/CmdPickup.cs` — команда подбора дропа по индексу (pickup).
- `Assets/Net/Commands/CmdDebug.cs` — debug-команды для dev режима.

## Assets/Net/Local
- `Assets/Net/Local/LocalInventoryService.cs` — локальная логика инвентаря.
- `Assets/Net/Local/LocalItemRollService.cs` — локальный ролл предметов.
- `Assets/Net/Local/LocalMarketService.cs` — локальный маркет.
- `Assets/Net/Local/LocalStatService.cs` — локальные статы.

## Assets/Net/Mock
- `Assets/Net/Mock/MockAuthService.cs` — мок авторизации.
- `Assets/Net/Mock/MockProfileService.cs` — мок профиля.

## Assets/Net/Network
- `Assets/Net/Network/NetworkProtocol.cs` — модели сетевого протокола.
- `Assets/Net/Network/NetworkSessionRunner.cs` — UDP транспорт + буфер снапшотов.
- `Assets/Net/Network/RuntimeMetaService.cs` — запрос /runtime/seasons/current, /runtime/characters, auth/validate и PUT loadout.
- `Assets/Net/Network/BackendInventoryService.cs` — HTTP-клиент инвентаря Laravel (GET inventory, equip, unequip, move, split, merge).
- `Assets/Net/Network/BackendMarketService.cs` — HTTP-клиент маркета Laravel (listings, list, cancel, buy).
- `Assets/Net/Network/BackendCurrencyService.cs` — HTTP-клиент валюты Laravel (balance, ledger).

## Assets/Tools/Editor
- `Assets/Tools/Editor/IslandObstacleGenerator.cs` — генерация ObstacleMesh и экспортных боксов.
- `Assets/Tools/Editor/RuntimeMapExporter.cs` — экспорт карты в JSON для сервера.

## Assets/UI
- `Assets/UI/CharacterSelect/CharacterSelectScreen.cs` — список персонажей (префаб строки), кнопка «Играть», кнопка «Создать персонажа» → CharacterCreate.
- `Assets/UI/CharacterSelect/SelectedClassDebugLabel.cs` — debug-лейбл выбранного класса (опционально).
- `Assets/UI/CharacterCreate/CharacterCreateScreen.cs` — создание персонажа: класс (vanguard/hunter/mystic), пол, имя, панель внешности (заглушка); CreateCharacter + SetLoadout → CharacterSelect.
- `Assets/UI/Dev/DevCommandsPanel.cs` — dev панель команд.
- `Assets/UI/Login/LoginScreen.cs` — логика кнопки Login.
- `Assets/UI/Run/AutoSkillTogglePanel.cs` — UI панель авто-скиллов + отображение ServerLoadout.
- `Assets/UI/Run/RunResultsPanel.cs` — панель результатов забега (поражение/завершён, убийства, кнопка «В меню»).
- `Assets/UI/Run/RunExitButton.cs` — кнопка досрочного выхода из забега (команда finish).
- `Assets/UI/Run/InventoryOpener.cs` — кнопка и клавиша I для открытия/закрытия сцены инвентаря в Run (additive).
- `Assets/UI/Inventory/InventorySceneHelper.cs` — статический хелпер: открыть/закрыть сцену Inventory (additive), Toggle, IsLoaded.
- `Assets/UI/Inventory/InventoryScreen.cs` — экран инвентаря в сцене Inventory (ячейки сумки по bagCapacity, привязка слотов экипировки, панель описания по клику); кнопка «Закрыть» выгружает сцену.
- `Assets/UI/Inventory/ItemDetailPanel.cs` — панель описания предмета (название, описание, Экипировать/Снять, позиция: центр на мобилке, над предметом на ПК).
- `Assets/UI/Inventory/OpenInventorySceneButton.cs` — кнопка открытия сцены инвентаря (для CharacterSelect и др.).
- `Assets/UI/Market/MarketScreen.cs` — экран маркета (список лотов, кнопка купить).
- `Assets/UI/Currency/CurrencyLabel.cs` — отображение баланса валюты.
- `Assets/UI/Talents/TalentsScreen.cs` — экран талантов (вызов POST talents/allocate).

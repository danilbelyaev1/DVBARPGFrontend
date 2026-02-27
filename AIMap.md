# AIMap (Frontend Scripts Only)

Карта **только скриптов** в клиентской части.  
Без ассетов, анимаций, сцен и серверного кода.

## Assets/Core
- `Assets/Core/GameRoot.cs` — корневой синглтон, регистрирует сервисы и запускает систему.
- `Assets/Core/Services/AuthSession.cs` — модель авторизационной сессии.
- `Assets/Core/Services/ServiceInterfaces.cs` — интерфейсы сервисов (Auth/Profile/Session и т.д.).
- `Assets/Core/Services/ServiceRegistry.cs` — простой DI-контейнер.

## Assets/Data
- `Assets/Data/ClassData.cs` — ScriptableObject с данными класса (используется в выборе класса).

## Assets/Game/Animation
- `Assets/Game/Animation/MovementAnimator.cs` — переключает анимации движения на объекте.

## Assets/Game/Camera
- `Assets/Game/Camera/TopDownFollowCamera.cs` — верхняя камера с зумом и следованием.

## Assets/Game/Dev
- `Assets/Game/Dev/DevRunBootstrap.cs` — dev-старт забега из Run.

## Assets/Game/Network
- `Assets/Game/Network/FloatingDamageText.cs` — визуализация всплывающего урона.
- `Assets/Game/Network/NetworkDamageFromSnapshots.cs` — вывод урона по снапшотам.
- `Assets/Game/Network/NetworkMonstersReplicator.cs` — репликация монстров из снапшотов.
- `Assets/Game/Network/NetworkPlayerHpLabel.cs` — UI HP игрока.
- `Assets/Game/Network/NetworkProjectilesReplicator.cs` — репликация снарядов.
- `Assets/Game/Network/NetworkRunConnector.cs` — соединение с сервером из Run.

## Assets/Game/Player
- `Assets/Game/Player/JoystickInput.cs` — виртуальный джойстик.
- `Assets/Game/Player/NetworkPlayerReplicator.cs` — репликация/предсказание игрока.
- `Assets/Game/Player/PlayerInputController.cs` — ввод (WASD/мышь/джойстик) и отправка в сеть.

## Assets/Game/World
- `Assets/Game/World/UnifiedHeightSampler.cs` — единый сэмплер высоты (terrain + меши).

## Assets/Net/Commands
- `Assets/Net/Commands/IClientCommand.cs` — базовый интерфейс команды клиента.
- `Assets/Net/Commands/CmdMove.cs` — команда движения.
- `Assets/Net/Commands/CmdStop.cs` — команда остановки.
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

## Assets/Tools (Editor)
- `Assets/Tools/IslandObstacleGenerator.cs` — генерация ObstacleMesh и экспортных боксов.
- `Assets/Tools/RuntimeMapExporter.cs` — экспорт карты в JSON для сервера.

## Assets/UI
- `Assets/UI/CharacterSelect/CharacterSelectScreen.cs` — логика UI выбора класса.
- `Assets/UI/CharacterSelect/SelectedClassDebugLabel.cs` — debug-лейбл выбранного класса.
- `Assets/UI/Dev/DevCommandsPanel.cs` — dev панель команд.
- `Assets/UI/Login/LoginScreen.cs` — логика кнопки Login.

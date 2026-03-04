# Обзор проекта DVBARPG

Документ для быстрой ориентации в проекте: архитектура, потоки данных, кто что вызывает. Поддерживать в актуальном состоянии при значимых изменениях.

---

## 1. Структура репозитория

| Часть | Путь | Назначение |
|-------|------|------------|
| **Клиент** | Корень репозитория, `Assets/` | Unity-клиент (сцены Login → CharacterSelect → Run, UI, репликация). |
| **Data-бэк** | `RuntimeServerARPG/apps/laravel-server/` | Laravel: авторизация, персонажи, сезоны, инвентарь, маркет, валюта, таланты, run/finish. HTTP API. |
| **Runtime-бэк** | `RuntimeServerARPG/apps/runtime-server/` | ASP.NET Core: игровой цикл (тики), UDP-сессии, инстансы, бой, снапшоты. Проксирует часть запросов к Laravel. |

Системные папки (vendor, node_modules, bin/obj и т.п.) не трогать.

---

## 2. Кто к кому ходит

```
Клиент (Unity)
    │
    ├── HTTP ──► Laravel (порт 8000): seasons/current, characters, auth/validate, loadout, inventory, market, currency, talents/allocate
    │
    ├── HTTP ──► Runtime (порт 8080): /skills/catalog, /characters/{id}, /maps/{mapId}/monsters  [прокси к Laravel или кэш]
    │
    └── UDP ───► Runtime (порт 8081): connect, start, move, stop, slot_toggle, finish → снапшоты (player, monsters, projectiles, cooldowns)

Runtime (порт 8081)
    └── HTTP ──► Laravel: validate при connect, скиллы, пул монстров, граф персонажа; POST run/finish при окончании инстанса
```

- **Laravel** — источник правды по профилям, инвентарю, маркету, валюте, результатам забегов.
- **Runtime** — источник правды по позициям, HP, мобам, снарядам; по завершении забега сам шлёт run/finish в Laravel.

---

## 3. Клиент: точки входа и сцены

- **Bootstrap** (опционально) → **Login** → **CharacterSelect** ⇄ **CharacterCreate** → **Run**.
- Регистрация сервисов: `Assets/Core/GameRoot.cs` (RegisterCoreServices). Там же создаются GameObject для NetworkSessionRunner, RuntimeMetaService, BackendInventoryService, BackendMarketService, BackendCurrencyService.
- Список скриптов по папкам: `AIMap.md`.

Ключевые компоненты по сценам:

| Сцена | Ключевые скрипты |
|-------|------------------|
| Login | `LoginScreen.cs` — кнопка Play → CharacterSelect |
| CharacterSelect | `CharacterSelectScreen.cs` — список персонажей, «Играть» → Run, «Создать персонажа» → CharacterCreate |
| CharacterCreate | `CharacterCreateScreen.cs` — класс, пол, имя, внешность (заглушка); CreateCharacter + SetLoadout → CharacterSelect |
| Run | `NetworkRunConnector.cs` — ожидание персонажа/сезона, ValidateAuth (Laravel), UDP Connect. `NetworkSessionRunner` — отправка команд, приём снапшотов. `RunEndController`, `RunResultsPanel`, `RunExitButton` — завершение забега и экран результатов. `SkillPresentationBootstrap` — запросы к runtime /skills/catalog и /characters/{id}. `MonsterCatalogClient` — /maps/{mapId}/monsters. |

---

## 4. Клиент: сервисы и реализация

| Интерфейс | Реализация | Где используется |
|-----------|------------|-------------------|
| IAuthService | MockAuthService | Login/CharacterSelect |
| IProfileService | MockProfileService | Везде (текущий персонаж, сезон, лоадут) |
| IRuntimeMetaService | RuntimeMetaService | CharacterSelect, NetworkRunConnector (seasons, characters, auth/validate, SetLoadout, AllocateTalent) |
| ISessionService | NetworkSessionRunner | Run (UDP connect, Send команд) |
| IInventoryService | BackendInventoryService | InventoryScreen |
| IMarketService | BackendMarketService | MarketScreen |
| ICurrencyService | BackendCurrencyService | CurrencyLabel |
| IStatService, IItemRollService | Local* (заглушки) | — |

---

## 5. Laravel API (клиент вызывает напрямую)

Базовый URL задаётся в сервисах (например `backendBaseUrl` в RuntimeMetaService). Заголовки: `Authorization: Bearer {token}`, `X-Api-Key`, `X-Contract-Version`. Префикс: `/api/runtime/`.

| Метод | Путь | Кто вызывает | Назначение |
|-------|------|--------------|------------|
| GET | seasons/current | RuntimeMetaService | Текущий сезон |
| GET | characters | RuntimeMetaService | Список персонажей |
| POST | characters | RuntimeMetaService | Создание персонажа (name, classId, gender) |
| POST | auth/validate | RuntimeMetaService | Токен + characterId + seasonId → лоадут, статы |
| PUT | characters/{id}/loadout | RuntimeMetaService | Установка скиллов (attack, supportA, supportB, movementSlot) |
| POST | characters/{id}/talents/allocate | RuntimeMetaService | Выделение таланта (talentCode, requestId) |
| GET | characters/{id}/inventory | BackendInventoryService | Инвентарь (query: seasonId) |
| POST | characters/{id}/inventory/equip | BackendInventoryService | Экипировать (instanceId, slot, requestId) |
| POST | characters/{id}/inventory/unequip | BackendInventoryService | Снять (slot, requestId) |
| POST | characters/{id}/inventory/move, split, merge | BackendInventoryService | Перемещение/деление/объединение |
| GET | market/listings | BackendMarketService | Список лотов (seasonId, limit, offset) |
| POST | characters/{id}/market/list, cancel, buy | BackendMarketService | Выставить/снять/купить |
| GET | characters/{id}/currency/balance | BackendCurrencyService | Баланс (seasonId, currencyCode) |
| GET | characters/{id}/currency/ledger | BackendCurrencyService | История (seasonId, limit, offset) |

Run/finish клиент не вызывает — это делает runtime при окончании инстанса.

---

## 6. Runtime HTTP (клиент вызывает :8080)

Базовый URL обычно собирается из UDP URL (например udp://host:8081 → http://host:8080). Заголовок: `Authorization: Bearer {token}`.

| Метод | Путь | Назначение |
|-------|------|------------|
| GET | /skills/catalog | Каталог скиллов (SkillPresentationBootstrap) |
| GET | /characters/{id} | Граф персонажа, runtimeProfiles, combatLoadout (SkillPresentationBootstrap) |
| GET | /maps/{mapId}/monsters | Пул монстров для карты (MonsterCatalogClient) |

---

## 7. UDP-протокол (клиент ↔ runtime :8081)

- **Клиент → сервер:** команды в виде JSON: `connect`, `start`, `move`, `stop`, `slot_toggle`, `finish` (+ debug по необходимости). Модели: `NetworkProtocol.cs` (CommandEnvelope и др.).
- **Сервер → клиент:** `hello`, `connect_ok`, `instance_start`, `snapshot`, `error`, `ack`, `net_stats`. Снапшот содержит Player (HP, позиция, флаги слотов), Monsters[], Projectiles[], Cooldowns.
- Отправка команд: `ISessionService.Send(IClientCommand)`. Реализация: `NetworkSessionRunner.Send()` (CmdMove, CmdStop, CmdSlotToggle, CmdFinish, CmdDebug).
- Завершение забега: по снапшоту с Player.Hp <= 0 вызывается RunEnded(true); при отправке finish — RunEnded(false). RunEndController обновляет RunResultState; RunResultsPanel показывает экран и кнопку «В меню».

---

## 8. Классы и пресеты на бэке (для согласования с клиентом)

- Классы: vanguard (melee), hunter (ranged), mystic (mage). Оружие: sword, bow, staff.
- Пресеты лоадута на клиенте: `ClassLoadoutPresets.cs` (slash/stone_skin_aura/dash для melee; quick_shot/battle_hymn/combat_roll для ranged; arc_bolt/ghost_shroud_aura/rift_step для mage). movementSlot = supportB.
- Laravel: `RuntimeControllerHelpers` — CLASS_WEAPON_MAP, starterSkillsForWeapon, normalizeCombatLoadout; слоты экипировки в EQUIPMENT_SLOTS.

---

## 9. Где искать по задаче

| Задача | Где смотреть |
|--------|----------------|
| Смена сцены, кнопки меню | `Assets/UI/` (Login, CharacterSelect, Run, Inventory, Market, Talents) |
| Запросы к Laravel | `RuntimeMetaService.cs`, `BackendInventoryService.cs`, `BackendMarketService.cs`, `BackendCurrencyService.cs` |
| Запросы к runtime HTTP | `SkillPresentationBootstrap.cs`, `MonsterCatalogClient.cs` |
| UDP и снапшоты | `NetworkSessionRunner.cs`, `NetworkProtocol.cs`; репликация — `*Replicator.cs` в Game/Network и Game/Player |
| Модели API (DTO) | `Assets/Core/Services/*.cs` (RuntimeMetaModels, InventoryModels, MarketModels), `NetworkProtocol.cs` |
| Регистрация сервисов | `GameRoot.cs` → RegisterCoreServices |
| Правила и ограничения | `AGENTS.md` |
| Карта скриптов клиента | `AIMap.md` |
| Концепция игры и боёвка | `AIConcept.md` |

---

## 10. Важные замечания

- Не менять бэкенд без явного запроса пользователя.
- Префабы и сцены (.unity) в редакторе не менять автоматически; давать инструкции, что привязать в инспекторе.
- Сериализуемые поля — с `Header` и `Tooltip` на русском.
- Поддерживать `AIMap.md` при добавлении/переименовании скриптов.

# MVP Plan (Client + Backend Verification)

Этот план нужен для передачи задач агенту по этапам.  
Формат каждого пункта:
- Что нужно сделать агенту
- Что нужно сделать мне

## Важное правило для агента (обязательно)

Если в API есть сомнения по полям, статусам, ошибкам или форматам ответов, агент **обязан проверить бэкенд** в:

- `C:\UnityProjects\RuntimeServerARPG`

Что именно проверять:
- Laravel routes (какой endpoint реально существует)
- контроллер/handler (какие поля отдаются)
- коды ошибок (`error`, `code`, `message`)
- обязательные query/body параметры

Минимум для сверки:
- маршрут
- пример успешного ответа
- пример ошибки

---

## 1) Каркас архитектуры (state + flow)

### Что нужно сделать агенту:
- Добавить состояния:
  - `SessionState` (`token`, `seasonId`, `characterId`, `mapId`)
  - `CampaignState` (`unlockedMapCodes`, `visitedMapCodes`, `travelOptionsByMap`, `quests`)
  - `ShopState` (`npcs`, `offers`, `pendingBuy`)
- Добавить `FlowRouter/StateMachine`:
  - `Login -> CharacterSelect -> Hub -> RunLoading -> Run -> Hub`
- Зарегистрировать новые сервисы/состояния в `GameRoot`.

### Что нужно сделать мне:
- Подтвердить названия сцен: `Hub`, `RunLoading`, `QuestJournal`, `NpcShop`.
- Добавить эти сцены в Build Settings.

---

## 2) Runtime API-клиент под новый MVP

### Что нужно сделать агенту:
- Сделать/расширить единый HTTP слой и подключить:
  - `GET /api/runtime/seasons/current`
  - `GET /api/runtime/characters`
  - `GET /api/runtime/characters/{characterId}/campaign?seasonId=...`
  - `POST /api/runtime/characters/{characterId}/travel/validate`
  - `GET /api/runtime/content/npcs?mapId=act1_hub`
  - `GET /api/runtime/characters/{characterId}/shops/{npcCode}?seasonId=...`
  - `POST /api/runtime/characters/{characterId}/shops/{npcCode}/buy`
- Заголовки в каждом запросе:
  - `Authorization: Bearer ...`
  - `X-Contract-Version: 1.1`
  - `X-Api-Key` (если требуется окружением)
- Добавить DTO для campaign/travel/shop/npc.
- Если контракт неочевиден: проверить `C:\UnityProjects\RuntimeServerARPG`.

### Что нужно сделать мне:
- Подтвердить, обязателен ли `X-Api-Key` в текущем окружении.
- По возможности дать агенту эталонные JSON-примеры.

---

## 3) Единая обработка ошибок

### Что нужно сделать агенту:
- Сделать `ErrorMapper` (`error code -> локализованный текст`).
- Сделать `ErrorToast` и подключить в новые pipeline.
- Поддержать коды:
  - `map_locked`
  - `travel_requirement_not_met`
  - `shop_offer_unavailable`
  - `shop_insufficient_funds`
  - `inventory_full`
- Если названия кодов отличаются на сервере: проверить в `C:\UnityProjects\RuntimeServerARPG` и синхронизировать.

### Что нужно сделать мне:
- Утвердить финальные тексты ошибок для игрока.
- Проверить визуал toast (время, позиция, читаемость).

---

## 4) Экран HubWorldMap

### Что нужно сделать агенту:
- Создать экран `HubWorldMap`.
- Отрисовывать узлы/связи на основе `travelOptionsByMap`.
- Показать статусы: `locked`, `unlocked`, `visited`.
- Кнопки:
  - `Portal` только для first visit
  - `Teleport` только для visited
- При входе в Hub грузить campaign и обновлять `CampaignState`.

### Что нужно сделать мне:
- Подготовить/утвердить минимальный UI (префабы нод, иконки, подписи).
- Проверить читаемость графа и поведение кнопок.

---

## 5) Travel pipeline (критично)

### Что нужно сделать агенту:
- Реализовать:
  - click location -> `travel/validate`
  - если `ok` -> переход в `RunLoading` и запись `mapId`
  - если ошибка -> `ErrorToast` через `ErrorMapper`
- Гарантировать: UDP `start` только после успешного `travel/validate`.
- Если backend возвращает дополнительные условия: проверить их в `C:\UnityProjects\RuntimeServerARPG`.

### Что нужно сделать мне:
- Подтвердить UX-поведение first visit / teleport.
- Прогнать руками сценарии: success, `map_locked`, `travel_requirement_not_met`.

---

## 6) Экран RunLoading

### Что нужно сделать агенту:
- Создать экран перед UDP стартом.
- Показать `seasonId`, `characterId`, `mapId`, последний статус/ошибку.
- Добавить таймаут и кнопку "Вернуться в Hub".

### Что нужно сделать мне:
- Подтвердить таймаут (например 10-15 сек).
- Проверить, что нет зависания при неуспешном подключении.

---

## 7) NPC Shop pipeline

### Что нужно сделать агенту:
- В `act1_hub` грузить NPC:
  - `GET /api/runtime/content/npcs?mapId=act1_hub`
- При открытии магазина NPC:
  - `GET /api/runtime/characters/{characterId}/shops/{npcCode}?seasonId=...`
- Покупка:
  - `POST /api/runtime/characters/{characterId}/shops/{npcCode}/buy`
- После покупки обновлять связанные state (валюта/инвентарь/campaign по контракту backend).
- Ошибки покупки показывать через `ErrorToast`.
- При расхождении полей оффера или buy-ответа: проверить backend в `C:\UnityProjects\RuntimeServerARPG`.

### Что нужно сделать мне:
- Утвердить минимальный UI магазина (карточка оффера, цена, disabled-состояния).
- Проверить кейсы: успех, нехватка валюты, оффер недоступен, inventory full.

---

## 8) QuestJournal MVP

### Что нужно сделать агенту:
- Экран `QuestJournal` с вкладками:
  - `main`
  - `side`
- Биндинг из `CampaignState.quests`.
- Минимальный рендер: название, статус, краткая цель.

### Что нужно сделать мне:
- Утвердить макет карточки квеста.
- Проверить длину текстов и локализацию.

---

## 9) Возврат Run -> Hub

### Что нужно сделать агенту:
- После `finish`/`return_to_map` делать гарантированный переход в Hub.
- После возврата обновлять campaign/shop при необходимости.
- Проверить совместимость с текущими `NetworkSessionRunner` и Run-end логикой.

### Что нужно сделать мне:
- Протестировать ручной выход и авто-завершение.
- Проверить, что Hub после возврата показывает актуальные статусы.

---

## 10) Debug overlay + smoke-check

### Что нужно сделать агенту:
- Добавить `DebugOverlay`:
  - `seasonId`
  - `characterId`
  - `mapId`
  - `last API error`
- Добавить компактные debug-логи для цепочки:
  - `Hub -> travel/validate -> RunLoading -> Run -> Hub`

### Что нужно сделать мне:
- Проверить overlay в Editor и dev build.
- Держать короткий smoke-чеклист перед каждым merge.

---

## Порядок постановки задач агенту (выполнять строго по порядку)

1. Каркас state + flow/router  
2. Runtime API + DTO  
3. ErrorMapper + ErrorToast  
4. HubWorldMap  
5. Travel pipeline  
6. RunLoading  
7. NpcShop pipeline  
8. QuestJournal  
9. Возврат Run -> Hub  
10. Debug overlay + полировка


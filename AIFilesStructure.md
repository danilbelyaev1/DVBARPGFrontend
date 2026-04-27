# AI Files Structure

Maintained map of user-owned files in this repository.
Update this file in the same change when user files are created/removed/renamed or their role changes.
Last updated: 2026-04-13 (UI Toolkit unified HUD screens for shop/teleport/inventory/NPC).

## Scope policy
- Scan only user-owned project code/config/docs.
- Exclude from routine scanning:
  - `Library/**`, `Logs/**`, `Temp/**`, `obj/**`
  - `.git/**`, `.idea/**`
  - `**/node_modules/**`, `**/.cache/**`
  - generated/build artifacts

## Repository root (user-owned)
- `AGENTS.md` - operational rules for AI agents.
- `AIFilesStructure.md` - this user-file map.
- `README.md` - repository overview.
- `AIConcept.md` - concept notes.
- `AIMap.md` - internal project map.
- `CONTENT_GUIDE.md` - content conventions.
- `MVP_CLIENT_PLAN.md` - MVP plan.
- `PROJECT_OVERVIEW.md` - architecture and context.
- `SKILLS_CATALOG_FIELDS.md` - skill catalog schema reference.
- `GameClient.sln` - solution file.
- `Assembly-CSharp.csproj`, `Assembly-CSharp-Editor.csproj` - project build files.
- `docs/**` - client docs.
- `Assets/**` - Unity game content and scripts.
  - `Assets/UI/Common/HudWindowCoordinator.cs` - shared HUD window docking and left-window exclusivity coordinator.
  - `Assets/UI/Toolkit/Styles/HudPanels.uss` - shared runtime UI Toolkit style sheet for HUD-like modal panels.
  - `Assets/UI/Toolkit/UXML/ShopScreen.uxml` - UI Toolkit layout for NPC shop window.
  - `Assets/UI/Toolkit/UXML/TeleportScreen.uxml` - UI Toolkit layout for hub teleport window with detail section.
  - `Assets/UI/Toolkit/UXML/InventoryScreen.uxml` - UI Toolkit layout for inventory, equipment and item detail action.
  - `Assets/UI/Toolkit/UXML/NpcInteractionScreen.uxml` - UI Toolkit layout for NPC interaction action menu.
  - `Assets/Shaders/Outline/NpcHoverOutline.shader` - runtime white silhouette outline shader used for NPC hover highlight.
- `Packages/**` - Unity package manifests.
- `ProjectSettings/**` - Unity project settings.
- `UserSettings/**` - local/editor settings tracked by project policy.

## External dependency reference
- Backend client contract source: `C:\UnityProjects\RuntimeServerARPG\docs\client-contract.md`.

## Explicitly non-user in this map
- `Library/**`, `Logs/**`, `Temp/**`, `obj/**`.

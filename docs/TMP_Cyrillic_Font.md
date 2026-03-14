# Кириллица в TextMesh Pro (LiberationSans SDF)

Если русский текст отображается квадратиками (□), в Font Asset нет глифов для кириллицы. Нужно пересобрать атлас с диапазоном символов, включающим кириллицу.

## Быстрый способ в Unity

1. **Открой Font Asset Creator**  
   Меню: **Window → TextMeshPro → Font Asset Creator**.

2. **Source Font File**  
   Укажи шрифт с поддержкой кириллицы, например:
   - `Assets/TextMesh Pro/Fonts/LiberationSans.ttf` (Liberation Sans поддерживает кириллицу в TTF).

3. **Character Set**  
   - Выбери **Unicode Range (Hex)**.
   - Добавь диапазоны:
     - `0020-007F` — базовая латиница (пробел и ASCII).
     - `0400-04FF` — кириллица (все буквы русского/украинского и т.д.).
   - Либо выбери **Custom Characters** и вставь строку со всеми нужными символами (например, алфавит и цифры).

4. **Atlas Resolution**  
   Оставь, например, 1024×1024 или 2048×2048 (при большом наборе символов может понадобиться больше).

5. **Generate Font Atlas**  
   Нажми кнопку — будет создан атлас.

6. **Сохрани как новый Font Asset**  
   - **Save** или **Save as** — сохрани в  
     `Assets/TextMesh Pro/Resources/Fonts & Materials/`  
     с именем, например, **LiberationSans SDF** (перезапись существующего) или **LiberationSans SDF Cyrillic** (новый файл).
   - Если создаёшь новый ассет: в сценах и префабах в компонентах **Text (TMP)** в поле **Font Asset** выбери новый шрифт.

7. **Fallback**  
   Если используешь **LiberationSans SDF - Fallback**: его тоже нужно пересобрать с тем же Unicode Range (включая 0400–04FF), иначе недостающие символы по-прежнему будут заменяться на □.

## Причина ошибки

Сообщение вида «The character with Unicode value \u0430 was not found» означает: символ \u0430 (буква «а») отсутствует в выбранном Font Asset и в его fallback-списке. После добавления диапазона **0400–04FF** (кириллица) в Font Asset Creator и пересборки атласа русский текст начнёт отображаться.

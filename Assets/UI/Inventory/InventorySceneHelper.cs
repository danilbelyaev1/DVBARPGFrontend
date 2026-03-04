using UnityEngine.SceneManagement;

namespace DVBARPG.UI.Inventory
{
    /// <summary>
    /// Открытие/закрытие сцены инвентаря. Инвентарь — отдельная сцена, которую можно открыть из Run (кнопка/I) и из CharacterSelect (кнопка).
    /// </summary>
    public static class InventorySceneHelper
    {
        public const string SceneName = "Inventory";

        public static bool IsLoaded
        {
            get
            {
                var s = SceneManager.GetSceneByName(SceneName);
                return s.IsValid() && s.isLoaded;
            }
        }

        /// <summary>Загрузить сцену инвентаря поверх текущей (additive).</summary>
        public static void Open()
        {
            if (IsLoaded) return;
            SceneManager.LoadScene(SceneName, LoadSceneMode.Additive);
        }

        /// <summary>Выгрузить сцену инвентаря. Вызывать из самой сцены (кнопка «Закрыть») или из Run по клавише I.</summary>
        public static void Close()
        {
            var s = SceneManager.GetSceneByName(SceneName);
            if (s.IsValid() && s.isLoaded)
                SceneManager.UnloadSceneAsync(s);
        }

        /// <summary>Переключить: если сцена загружена — выгрузить, иначе загрузить.</summary>
        public static void Toggle()
        {
            if (IsLoaded) Close();
            else Open();
        }
    }
}

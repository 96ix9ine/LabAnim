using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public class MainMenuController : MonoBehaviour
{
    [Header("UI Panels")]
    public GameObject mainMenuPanel;
    public GameObject playerPanel;
    
    [Header("Animation Data")]
    public AnimationData[] animations = new AnimationData[9];
    
    [Header("Animation Controller")]
    public AnimationController animationController;
    
    [Header("Buttons")]
    public Button[] menuButtons = new Button[9];
    
    void Start()
    {
        SetupButtons();
        SetupAnimationController();
        HideInitialPrefabsInHierarchy();
    }
    
    void SetupButtons()
    {
        for (int i = 0; i < menuButtons.Length && i < animations.Length; i++)
        {
            int buttonIndex = i; // Замыкание для корректной работы
            menuButtons[i].onClick.AddListener(() => OnAnimationButtonClicked(buttonIndex));
            
            // Устанавливаем текст кнопки
            Text buttonText = menuButtons[i].GetComponentInChildren<Text>();
            if (buttonText != null && animations[i] != null)
            {
                buttonText.text = animations[i].animationName;
            }
        }
    }

    // Авто-скрытие сценовых экземпляров префабов из AnimationData, чтобы не мешали плееру
    // Исключаем клоны, созданные в MainAnimPanel (Animation Container в AnimationController)
    void HideInitialPrefabsInHierarchy()
    {
        if (animations == null || animations.Length == 0) return;

        // Ищем контейнер для анимаций, чтобы не трогать его содержимое
        Transform animationContainerTransform = null;
        if (animationController != null && animationController.animationContainer != null)
        {
            animationContainerTransform = animationController.animationContainer.transform;
        }
        else
        {
            var containerGo = GameObject.Find("MainAnimPanel");
            if (containerGo != null) animationContainerTransform = containerGo.transform;
        }

        var allSceneObjects = FindObjectsByType<GameObject>(FindObjectsSortMode.None);

        foreach (var animData in animations)
        {
            if (animData == null || animData.prefab == null) continue;

            string prefabName = animData.prefab.name;

            foreach (var go in allSceneObjects)
            {
                if (go == null) continue;

                // Пропускаем сам контейнер и всё, что под ним
                if (animationContainerTransform != null)
                {
                    if (go == animationContainerTransform.gameObject) continue;
                    if (go.transform.IsChildOf(animationContainerTransform)) continue;
                }

                // Скрываем только корневые сценовые объекты, чтобы не трогать вложенные элементы UI и др.
                if (go.transform.parent != null) continue;

                // Считаем «исходным сценовым экземпляром» объект с тем же именем, без суффикса (Clone)
                // или полностью совпадающим именем префаба
                string cleanName = go.name.Replace("(Clone)", string.Empty).Trim();
                bool nameMatches = cleanName == prefabName;

                if (!nameMatches) continue;

                // Скрываем только те объекты, которые не были созданы динамически в нашем контейнере
                // и находятся в активной сцене
                if (go.scene.IsValid() && go.scene.isLoaded)
                {
                    // Не трогаем уже неактивные
                    if (go.activeSelf)
                    {
                        go.SetActive(false);
                        Debug.Log($"Скрыт сценовый экземпляр префаба '{prefabName}': {go.name}");
                    }
                }
            }
        }
    }
    
    void SetupAnimationController()
    {
        if (animationController == null)
            animationController = FindFirstObjectByType<AnimationController>();
            
        if (animationController != null)
        {
            animationController.mainMenuPanel = mainMenuPanel;
            animationController.playerPanel = playerPanel;
        }
    }
    
    void OnAnimationButtonClicked(int animationIndex)
    {
        if (animationIndex >= 0 && animationIndex < animations.Length)
        {
            AnimationData selectedAnimation = animations[animationIndex];
            if (animationController != null)
            {
                animationController.LoadAnimation(selectedAnimation);
            }
            else
            {
                Debug.LogError("AnimationController не найден!");
            }
        }
    }
    
    // Метод для программной загрузки анимации
    public void LoadAnimation(int animationIndex)
    {
        OnAnimationButtonClicked(animationIndex);
    }
    
    // Метод для добавления новой анимации
    public void AddAnimation(int index, AnimationData animationData)
    {
        if (index >= 0 && index < animations.Length)
        {
            animations[index] = animationData;
            
            // Обновляем текст кнопки
            if (menuButtons[index] != null)
            {
                Text buttonText = menuButtons[index].GetComponentInChildren<Text>();
                if (buttonText != null)
                {
                    buttonText.text = animationData.animationName;
                }
            }
        }
    }
    
    // Метод для возврата в главное меню
    public void ReturnToMainMenu()
    {
        if (playerPanel != null)
            playerPanel.SetActive(false);
            
        if (mainMenuPanel != null)
            mainMenuPanel.SetActive(true);
    }
} 
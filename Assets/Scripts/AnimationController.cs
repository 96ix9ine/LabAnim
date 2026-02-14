using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Playables;
using UnityEngine.Animations;
using System.Linq;
using UnityEngine.Video;

public class AnimationController : MonoBehaviour
{
    [Header("UI Panels")]
    public GameObject mainMenuPanel;
    public GameObject playerPanel;
    
    [Header("Animation Control UI")]
    public Slider timelineSlider;
    public Button playButton;
    public Button pauseButton;
    public Button restartButton;
    public Button closeButton;
    
    [Header("3D Animation Display")]
    public GameObject animationContainer; // Контейнер для 3D объектов в сцене
    
    [Header("Tooltip System")]
    public GameObject tooltipPanel; // Панель для подсказок
    public GameObject tooltipContainerPrefab; // Prefab TooltipContainer
    
    private PlayableDirector playableDirector;
    private PlayableDirector tooltipPlayableDirector; // PlayableDirector для подсказок на TooltipPanel
    private bool isDragging = false;
    private bool isPlaying = false; // Флаг воспроизведения анимации
    private float savedAnimationTime = 0f; // Сохраненная позиция анимации при паузе
    private float savedTooltipTime = 0f; // Сохраненная позиция Timeline подсказок при паузе
    private double savedVideoTime = 0d; // Сохраненная позиция видео при паузе
    
    // 3D Animation objects
    private GameObject currentAnimationObject;
    private Animator currentAnimator;
    private Animation currentAnimation;
    private AnimationClip currentAnimationClip;
    private AnimationState currentAnimationState;
    private Camera prefabCamera; // Камера из префаба
    
    // Tooltip objects
    private GameObject tooltipContainerInstance;
    private TMPro.TextMeshProUGUI tooltipTextComponent; // Компонент текста подсказки
    private TooltipData[] currentTooltips; // Текущие подсказки для анимации

    // Video objects
    private GameObject currentVideoObject;
    private VideoPlayer currentVideoPlayer;
    private AudioSource currentVideoAudioSource;
    private RenderTexture currentVideoRenderTexture;
    private RawImage videoRawImage;
    private bool isVideoMode = false;
    private bool isVideoScrubbing = false;
    private float storedVideoVolume = 1f;
    
    void Start()
    {
        SetupUI();
        Setup3DAnimationDisplay();
        SetupTooltipSystem();
    }
    
    void SetupUI()
    {
        // Ищем UI элементы в PlayerPanel и ActionsPanel
        var playerPanel = GameObject.Find("PlayerPanel");
        if (playerPanel != null)
        {
            timelineSlider = playerPanel.GetComponentInChildren<Slider>();
            
            var buttonsContainer = playerPanel.transform.Find("ButtonsContainer");
            if (buttonsContainer != null)
            {
                playButton = buttonsContainer.Find("PlayButton")?.GetComponent<Button>();
                pauseButton = buttonsContainer.Find("PauseButton")?.GetComponent<Button>();
                restartButton = buttonsContainer.Find("RestartButton")?.GetComponent<Button>();
            }
        }
        
        var actionsPanel = GameObject.Find("ActionsPanel");
        if (actionsPanel != null)
        {
            closeButton = actionsPanel.transform.Find("CloseButton")?.GetComponent<Button>();
        }
        
        if (timelineSlider != null)
        {
            timelineSlider.onValueChanged.AddListener(OnSliderValueChanged);
            
            var eventTrigger = timelineSlider.gameObject.GetComponent<EventTrigger>();
            if (eventTrigger == null)
                eventTrigger = timelineSlider.gameObject.AddComponent<EventTrigger>();
            
            var pointerDownEntry = new EventTrigger.Entry();
            pointerDownEntry.eventID = EventTriggerType.PointerDown;
            pointerDownEntry.callback.AddListener((data) => OnSliderDragStart());
            eventTrigger.triggers.Add(pointerDownEntry);
            
            var pointerUpEntry = new EventTrigger.Entry();
            pointerUpEntry.eventID = EventTriggerType.PointerUp;
            pointerUpEntry.callback.AddListener((data) => OnSliderDragEnd());
            eventTrigger.triggers.Add(pointerUpEntry);
        }
        
        if (playButton != null)
            playButton.onClick.AddListener(PlayAnimation);
        
        if (pauseButton != null)
            pauseButton.onClick.AddListener(PauseAnimation);
        
        if (restartButton != null)
            restartButton.onClick.AddListener(RestartAnimation);
        
        if (closeButton != null)
            closeButton.onClick.AddListener(CloseAnimation);
    }
    
    void Setup3DAnimationDisplay()
    {
        // Ищем MainAnimPanel как контейнер для 3D объектов
        if (animationContainer == null)
        {
            animationContainer = GameObject.Find("MainAnimPanel");
            if (animationContainer == null)
            {
                Debug.LogError("MainAnimPanel не найден! Создайте панель с именем MainAnimPanel");
            }
        }
        
        // Отключаем Gizmos глобально
        DisableGizmos();
    }
    
    void SetupTooltipSystem()
    {
        // Ищем TooltipPanel
        if (tooltipPanel == null)
        {
            tooltipPanel = GameObject.Find("TooltipPanel");
            if (tooltipPanel == null)
            {
                Debug.LogWarning("TooltipPanel не найден!");
                return;
            }
        }
        
        // Создаем PlayableDirector на TooltipPanel для управления подсказками
        tooltipPlayableDirector = tooltipPanel.GetComponent<PlayableDirector>();
        if (tooltipPlayableDirector == null)
        {
            tooltipPlayableDirector = tooltipPanel.AddComponent<PlayableDirector>();
            Debug.Log("Создан PlayableDirector на TooltipPanel");
        }
        else
        {
            Debug.Log("PlayableDirector уже существует на TooltipPanel");
        }
        
        // Создаем инстанс TooltipContainer в TooltipPanel
        if (tooltipContainerPrefab != null && tooltipPanel != null)
        {
            tooltipContainerInstance = Instantiate(tooltipContainerPrefab, tooltipPanel.transform);
            tooltipContainerInstance.transform.localPosition = Vector3.zero;
            tooltipContainerInstance.transform.localRotation = Quaternion.identity;
            tooltipContainerInstance.transform.localScale = Vector3.one;
            
            // Отключаем PlayableDirector в TooltipContainer, чтобы избежать конфликтов
            var existingPlayableDirector = tooltipContainerInstance.GetComponent<PlayableDirector>();
            if (existingPlayableDirector != null)
            {
                existingPlayableDirector.enabled = false;
                Debug.Log("PlayableDirector в TooltipContainer отключен");
            }
            
            // Настраиваем дочерние объекты
            Debug.Log($"TooltipContainer содержит {tooltipContainerInstance.transform.childCount} дочерних объектов:");
            for (int i = 0; i < tooltipContainerInstance.transform.childCount; i++)
            {
                var child = tooltipContainerInstance.transform.GetChild(i);
                if (child.name == "TooltipText")
                {
                    // TooltipText всегда активен
                    child.gameObject.SetActive(true);
                }
                else
                {
                    // Остальные объекты отключаем
                    child.gameObject.SetActive(false);
                }
            }
            
            // TooltipContainer оставляем активным, чтобы Timeline мог им управлять
            tooltipContainerInstance.SetActive(true);
            
            // Находим компонент текста
            tooltipTextComponent = tooltipContainerInstance.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (tooltipTextComponent != null)
            {               
                // Устанавливаем правильную прозрачность по умолчанию
                Color defaultColor = tooltipTextComponent.color;
                defaultColor.a = 1f; // Полная непрозрачность
                tooltipTextComponent.color = defaultColor;
            }
            else
            {
                Debug.LogWarning("Компонент текста подсказки не найден!");
            }
        }
    }
    
    void DisableGizmos()
    {
        // Отключаем Gizmos для всех объектов в сцене
        var allObjects = FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        foreach (var obj in allObjects)
        {
            // Отключаем Gizmos на объектах
            var gizmoComponents = obj.GetComponents<MonoBehaviour>();
            foreach (var gizmo in gizmoComponents)
            {
                if (gizmo.GetType().Name.Contains("Gizmo") || gizmo.GetType().Name.Contains("Handle"))
                {
                    gizmo.enabled = false;
                }
            }
        }
        
        // Отключаем Gizmos в PlayableDirector
        if (playableDirector != null)
        {
            // Попытка отключить Gizmos через reflection
            var gizmoField = playableDirector.GetType().GetField("m_ShowGizmos", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (gizmoField != null)
            {
                gizmoField.SetValue(playableDirector, false);
            }
        }
    }
    
    public void LoadAnimation(AnimationData animationData)
    {
        if (animationData == null) return;
        
        // Debug.Log($"Загружается анимация: {animationData.animationName}");
        // Debug.Log($"  - animationClip: {animationData.animationClip != null}");
        // Debug.Log($"  - timelineAsset: {animationData.timelineAsset != null}");
        // Debug.Log($"  - prefab: {animationData.prefab != null}");
        // Debug.Log($"  - tooltips: {animationData.tooltips?.Length ?? 0}");
        
        // Загружаем подсказки для этой анимации
        currentTooltips = animationData.tooltips;
        if (currentTooltips != null && currentTooltips.Length > 0)
        {
            // Debug.Log($"Загружено {currentTooltips.Length} подсказок для анимации");
        }
        
        // Определяем режим: видео или 3D анимация
        isVideoMode = animationData.useVideo && animationData.videoClip != null;
        
        // Если timelineAsset не назначен, попробуем найти его автоматически
        if (animationData.timelineAsset == null)
        {
            // Ищем Timeline Asset в папке Assets
            var timelineAssets = Resources.FindObjectsOfTypeAll<UnityEngine.Timeline.TimelineAsset>();
            foreach (var asset in timelineAssets)
            {
                if (asset.name.Contains(animationData.animationName) || 
                    asset.name.Contains("TooltipContainerTimeline"))
                {
                    animationData.timelineAsset = asset;
                    Debug.Log($"Автоматически найден Timeline Asset: {asset.name}");
                    break;
                }
            }
        }

        // Удаляем старые объекты/ресурсы
        if (currentAnimationObject != null)
        {
            Destroy(currentAnimationObject);
            currentAnimationObject = null;
            currentAnimator = null;
            currentAnimation = null;
            currentAnimationState = null;
            prefabCamera = null;
        }
        if (currentVideoObject != null)
        {
            if (currentVideoPlayer != null)
            {
                try { currentVideoPlayer.Stop(); } catch {}
            }
            Destroy(currentVideoObject);
            currentVideoObject = null;
        }
        currentVideoPlayer = null;
        currentVideoAudioSource = null;
        currentVideoRenderTexture = null;
        
        // Сбрасываем состояние подсказок
        if (tooltipContainerInstance != null)
        {
            // Настраиваем дочерние объекты
            for (int i = 0; i < tooltipContainerInstance.transform.childCount; i++)
            {
                var child = tooltipContainerInstance.transform.GetChild(i);
                if (child.name == "TooltipText")
                {
                    // TooltipText всегда активен
                    child.gameObject.SetActive(true);
                }
                else
                {
                    // Остальные объекты отключаем
                    child.gameObject.SetActive(false);
                }
            }
            
            // TooltipContainer оставляем активным
            tooltipContainerInstance.SetActive(true);
        }
        
        // Сбрасываем текст подсказки
        if (tooltipTextComponent != null)
        {
            tooltipTextComponent.text = "";
        }
        if (isVideoMode)
        {
            // ВИДЕО РЕЖИМ
            if (animationContainer == null)
            {
                Debug.LogError("MainAnimPanel не найден для размещения видеоплеера");
            }
            else
            {
                currentVideoObject = new GameObject("VideoPlayer");
                currentVideoObject.transform.SetParent(animationContainer.transform, false);

                currentVideoPlayer = currentVideoObject.AddComponent<VideoPlayer>();
                currentVideoPlayer.playOnAwake = false;
                currentVideoPlayer.isLooping = true; // зацикливаем видео
                currentVideoPlayer.source = VideoSource.VideoClip;
                currentVideoPlayer.clip = animationData.videoClip;
                currentVideoPlayer.renderMode = VideoRenderMode.RenderTexture;
                currentVideoPlayer.skipOnDrop = true;
                currentVideoPlayer.playOnAwake = true;
                currentVideoPlayer.waitForFirstFrame = true;
                currentVideoPlayer.playbackSpeed = 1f;

                // UI для видео + RenderTexture
                EnsureVideoUI(animationData);
                if (currentVideoRenderTexture != null)
                {
                    currentVideoPlayer.targetTexture = currentVideoRenderTexture;
                }

                // Аудио
                currentVideoAudioSource = currentVideoObject.AddComponent<AudioSource>();
                currentVideoAudioSource.playOnAwake = false;
                currentVideoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
                currentVideoPlayer.EnableAudioTrack(0, true);
                currentVideoPlayer.SetTargetAudioSource(0, currentVideoAudioSource);

                // Подготавливаем, чтобы знать длительность и нормализовано искать
                currentVideoPlayer.prepareCompleted += vp =>
                {
                    if (isPlaying)
                    {
                        vp.time = savedVideoTime;
                        vp.Play();
                        currentVideoAudioSource.Play();
                    }
                    // Если RT не задан — создадим по размеру клипа
                    if (currentVideoRenderTexture == null && vp.clip != null)
                    {
                        int w = (int)vp.clip.width;
                        int h = (int)vp.clip.height;
                        if (w <= 0) w = 1920;
                        if (h <= 0) h = 1080;
                        currentVideoRenderTexture = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32);
                        currentVideoRenderTexture.name = $"VideoRT_{w}x{h}";
                        vp.targetTexture = currentVideoRenderTexture;
                        if (videoRawImage != null)
                        {
                            videoRawImage.texture = currentVideoRenderTexture;
                            videoRawImage.gameObject.SetActive(true);
                        }
                    }
                    LogAnimationDuration();
                };
                currentVideoPlayer.Prepare();

                // Сбрасываем clip, чтобы ветки, завязанные на анимацию, не мешали
                currentAnimationClip = null;
            }
        }
        else
        {
            // 3D АНИМАЦИЯ
            // Скрываем видео-UI если был создан
            if (videoRawImage != null)
            {
                videoRawImage.gameObject.SetActive(false);
            }
            // Инстанцируем prefab внутри MainAnimPanel
            if (animationData.prefab != null && animationContainer != null)
            {
                currentAnimationObject = Instantiate(animationData.prefab, animationContainer.transform);
                // Явно активируем клон, чтобы не наследовал неактивность исходного сценового объекта
                if (!currentAnimationObject.activeSelf)
                {
                    currentAnimationObject.SetActive(true);
                }
                currentAnimationObject.transform.localPosition = Vector3.zero;
                currentAnimationObject.transform.localRotation = Quaternion.identity;
                currentAnimationObject.transform.localScale = Vector3.one;

                // Ищем камеру в префабе
                prefabCamera = currentAnimationObject.GetComponentInChildren<Camera>();
                
                // Если камера найдена, настраиваем её для рендера
                if (prefabCamera != null)
                {
                    // Делаем камеру из префаба основной
                    prefabCamera.gameObject.SetActive(true);
                    prefabCamera.tag = "MainCamera";
                    
                    Debug.Log("Камера из префаба установлена как основная");
                }
                else
                {
                    Debug.LogWarning("Камера не найдена в префабе!");
                }

                // Ищем Animator или Animation
                currentAnimator = currentAnimationObject.GetComponent<Animator>();
                currentAnimation = currentAnimationObject.GetComponent<Animation>();

                // Применяем AnimationClip
                if (animationData.animationClip != null)
                {
                    currentAnimationClip = animationData.animationClip;
                    
                    if (currentAnimator != null)
                    {
                        // Используем существующий контроллер или создаем простой
                        if (currentAnimator.runtimeAnimatorController == null)
                        {
                            // Создаем простой контроллер через Resources
                            var controller = Resources.Load<RuntimeAnimatorController>("SimpleController");
                            if (controller == null)
                            {
                                // Если контроллер не найден, используем Animation компонент
                                currentAnimator.enabled = false;
                                currentAnimation = currentAnimationObject.AddComponent<Animation>();
                                currentAnimation.clip = animationData.animationClip;
                                currentAnimationState = currentAnimation[animationData.animationClip.name];
                                currentAnimation.Play();
                            }
                            else
                            {
                                currentAnimator.runtimeAnimatorController = controller;
                                currentAnimator.Play("Animation", 0, 0f);
                            }
                        }
                        else
                        {
                            currentAnimator.Play("Animation", 0, 0f);
                        }
                    }
                    else if (currentAnimation != null)
                    {
                        currentAnimation.clip = animationData.animationClip;
                        currentAnimationState = currentAnimation[animationData.animationClip.name];
                        currentAnimation.Play();
                    }
                }
            }
        }

        // Timeline для подсказок
        // Debug.Log($"Проверка Timeline: timelineAsset={animationData.timelineAsset != null}, tooltipPlayableDirector={tooltipPlayableDirector != null}");
        if (animationData.timelineAsset != null && tooltipPlayableDirector != null)
        {
            tooltipPlayableDirector.playableAsset = animationData.timelineAsset;
            
            // Привязываем дочерние объекты TooltipContainer к Timeline
            if (tooltipContainerInstance != null)
            {
                // Получаем все треки из Timeline
                var timelineAsset = tooltipPlayableDirector.playableAsset as UnityEngine.Timeline.TimelineAsset;
                if (timelineAsset != null)
                {
                // Debug.Log($"Timeline содержит {timelineAsset.GetOutputTracks().Count()} треков:");
                        
                    foreach (var track in timelineAsset.GetOutputTracks())
                    {
                        // Debug.Log($"  - Трек: {track.name} (тип: {track.GetType().Name})");
                            
                        // Показываем информацию о клипах в треке
                        if (track is UnityEngine.Timeline.ActivationTrack activationTrack)
                        {
                            var clips = activationTrack.GetClips();
                            // Debug.Log($"    Клипы в треке {track.name}: {clips.Count()}");
                            foreach (var clip in clips)
                            {
                                // Debug.Log($"      - {clip.displayName}: {clip.start}s - {clip.end}s");
                            }
                        }
                        
                        if (track is UnityEngine.Timeline.ActivationTrack)
                        {
                            // Привязываем Activation Track к TooltipContainer
                            if (tooltipContainerInstance != null)
                            {
                                tooltipPlayableDirector.SetGenericBinding(track, tooltipContainerInstance);
                                Debug.Log($"TooltipContainer привязан к треку: {track.name}");
                            }
                            else
                            {
                                Debug.LogWarning("TooltipContainer не найден для привязки");
                            }
                        }
                    }
                }
            }
                
            if (tooltipPlayableDirector.duration > 0)
            {
                tooltipPlayableDirector.Play();
                Debug.Log("Timeline подсказок запущен");
                
                // Логируем длительность анимации после инициализации Timeline
                LogAnimationDuration();
            }
            }
            else
            {
                Debug.LogWarning("Timeline не найден или PlayableDirector не инициализирован");
            }

        SwitchToPlayerPanel();
        PlayAnimation();
    }

    private void EnsureVideoUI(AnimationData animationData)
    {
        if (animationContainer == null) return;

        // Найдём или создадим RawImage в MainAnimPanel
        if (videoRawImage == null)
        {
            videoRawImage = animationContainer.GetComponentInChildren<RawImage>(true);
        }

        if (videoRawImage == null)
        {
            var go = new GameObject("VideoDisplay", typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(animationContainer.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            videoRawImage = go.GetComponent<RawImage>();
            videoRawImage.raycastTarget = false;
        }

        // Обеспечим RenderTexture
        currentVideoRenderTexture = animationData.videoRenderTexture;
        if (currentVideoRenderTexture == null)
        {
            // Попробуем размеры клипа, иначе дефолт
            int w = 1920;
            int h = 1080;
            if (animationData.videoClip != null)
            {
                w = (int)animationData.videoClip.width;
                h = (int)animationData.videoClip.height;
                if (w <= 0) w = 1920;
                if (h <= 0) h = 1080;
            }
            currentVideoRenderTexture = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32);
            currentVideoRenderTexture.name = $"VideoRT_{w}x{h}";
        }

        // Подвяжем к RawImage
        if (videoRawImage != null)
        {
            videoRawImage.texture = currentVideoRenderTexture;
            videoRawImage.gameObject.SetActive(true);
        }
    }
    
    void OnSliderValueChanged(float value)
    {
        value = Mathf.Clamp01(value);
        
        // Если пользователь перетаскивает слайдер, обновляем время анимации
        if (isDragging)
        {
            UpdateAnimationTime(value);
        }
    }
    
    void OnSliderDragStart()
    {
        isDragging = true;
        // При начале перетаскивания обновляем время анимации
        if (timelineSlider != null)
        {
            UpdateAnimationTime(timelineSlider.value);
        }
        if (isVideoMode && currentVideoPlayer != null)
        {
            isVideoScrubbing = true;
            if (currentVideoPlayer.isPlaying)
            {
                currentVideoPlayer.Pause();
            }
            currentVideoPlayer.playbackSpeed = 0f; // точный скраббинг покадрово
            if (currentVideoAudioSource != null)
            {
                storedVideoVolume = currentVideoAudioSource.volume;
                currentVideoAudioSource.volume = 0f;
            }
        }
    }
    
    void OnSliderDragEnd()
    {
        isDragging = false;
        // При окончании перетаскивания обновляем время анимации
        if (timelineSlider != null)
        {
            UpdateAnimationTime(timelineSlider.value);
        }
        if (isVideoMode && currentVideoPlayer != null)
        {
            isVideoScrubbing = false;
            if (isPlaying)
            {
                currentVideoPlayer.playbackSpeed = 1f;
                currentVideoPlayer.Play();
                if (currentVideoAudioSource != null) currentVideoAudioSource.volume = storedVideoVolume;
            }
            else
            {
                // Обновим кадр
                currentVideoPlayer.playbackSpeed = 0f;
                if (currentVideoAudioSource != null) currentVideoAudioSource.volume = storedVideoVolume;
            }
        }
    }
    
    void LogAnimationDuration()
    {
        float animationDuration = 0f;
        float timelineDuration = 0f;
        float tooltipDuration = 0f;
        float videoDuration = 0f;
        
        // Получаем длительность анимации
        if (currentAnimationClip != null)
        {
            animationDuration = currentAnimationClip.length;
        }
        else if (isVideoMode && currentVideoPlayer != null && currentVideoPlayer.clip != null)
        {
            videoDuration = (float)currentVideoPlayer.clip.length;
        }
        
        // Получаем длительность основного Timeline
        if (playableDirector != null && playableDirector.playableAsset != null)
        {
            timelineDuration = (float)playableDirector.duration;
        }
        
        // Получаем длительность Timeline подсказок
        if (tooltipPlayableDirector != null && tooltipPlayableDirector.playableAsset != null)
        {
            tooltipDuration = (float)tooltipPlayableDirector.duration;
        }
        
        Debug.Log($"=== Длительность воспроизведения ===");
        if (!isVideoMode)
            Debug.Log($"Анимация: {animationDuration:F2} сек ({animationDuration/60:F1} мин)");
        else
            Debug.Log($"Видео: {videoDuration:F2} сек ({videoDuration/60:F1} мин)");
        Debug.Log($"Основной Timeline: {timelineDuration:F2} сек ({timelineDuration/60:F1} мин)");
        Debug.Log($"Timeline подсказок: {tooltipDuration:F2} сек ({tooltipDuration/60:F1} мин)");
        Debug.Log($"===============================");
    }
    
    void UpdateAnimationTime(float sliderValue)
    {
        if (!isVideoMode && currentAnimationClip != null)
        {
            float targetTime = sliderValue * currentAnimationClip.length;
            
            if (currentAnimator != null)
            {
                currentAnimator.Play("Animation", 0, sliderValue);
                if (!isPlaying)
                {
                    currentAnimator.speed = 0; // Останавливаем проигрывание если не воспроизводится
                }
            }
            else if (currentAnimation != null && currentAnimationState != null)
            {
                currentAnimationState.time = targetTime;
                currentAnimation.Sample();
            }
        }
        else if (isVideoMode && currentVideoPlayer != null && currentVideoPlayer.clip != null)
        {
            double duration = currentVideoPlayer.clip.length;
            double targetTime = Mathf.Clamp01(sliderValue) * duration;
            savedVideoTime = targetTime;
            if (currentVideoPlayer.isPrepared)
            {
                // Предпочитаем покадровый seek для мгновенного обновления изображения
                long frameCount = (long)currentVideoPlayer.clip.frameCount;
                if (frameCount > 0)
                {
                    long targetFrame = (long)Mathf.Clamp(Mathf.Round((float)(Mathf.Clamp01(sliderValue) * frameCount)), 0, Mathf.Max(0, frameCount - 1));
                    if (targetFrame <= 0)
                    {
                        currentVideoPlayer.frame = 0;
                        currentVideoPlayer.StepForward();
                    }
                    else
                    {
                        currentVideoPlayer.frame = targetFrame - 1; // StepForward отрисует нужный кадр
                        currentVideoPlayer.StepForward();
                    }
                }
                else
                {
                    currentVideoPlayer.time = targetTime;
                    // В некадровом режиме кратко Play+Pause обновляет RT
                    if (isVideoScrubbing || !isPlaying)
                    {
                        currentVideoPlayer.Play();
                        currentVideoPlayer.Pause();
                    }
                }
            }
        }
        
        if (playableDirector != null && playableDirector.playableAsset != null && 
            playableDirector.duration > 0)
        {
            playableDirector.time = sliderValue * playableDirector.duration;
        }
        
        // Синхронизируем Timeline подсказок со слайдером
        if (tooltipPlayableDirector != null && tooltipPlayableDirector.playableAsset != null && 
            tooltipPlayableDirector.duration > 0)
        {
            tooltipPlayableDirector.time = sliderValue * tooltipPlayableDirector.duration;
            // Обновляем сохраненную позицию подсказок при перетаскивании
            savedTooltipTime = (float)tooltipPlayableDirector.time;
        }
    }
    
    void Update()
    {
        UpdateSlider();
        UpdateTooltipText();
    }
    
    void UpdateTooltipText()
    {
        if (tooltipTextComponent == null || currentTooltips == null || currentTooltips.Length == 0)
            return;
            
        if (tooltipPlayableDirector == null || tooltipPlayableDirector.playableAsset == null)
            return;
            
        // Получаем текущее время Timeline
        double currentTime = tooltipPlayableDirector.time;
        
        // Проверяем, активен ли TooltipContainer
        bool isContainerActive = tooltipContainerInstance != null && tooltipContainerInstance.activeInHierarchy;
        
        if (!isContainerActive)
        {
            // Если контейнер неактивен, очищаем текст
            tooltipTextComponent.text = "";
            return;
        }
        
        // Ищем активную подсказку по времени
        TooltipData activeTooltip = null;
        foreach (var tooltip in currentTooltips)
        {
            if (currentTime >= tooltip.startTime && currentTime <= tooltip.endTime)
            {
                activeTooltip = tooltip;
                break;
            }
        }
        
        // Обновляем текст
        if (activeTooltip != null)
        {
            tooltipTextComponent.text = activeTooltip.tooltipText;
            
            // Обновляем цвет с учетом настроек прозрачности
            Color newColor = activeTooltip.textColor;
            newColor.a = activeTooltip.alpha; // Используем настройку прозрачности из TooltipData
            tooltipTextComponent.color = newColor;
            
            // Если подсказка важная, можно добавить визуальное выделение
            if (activeTooltip.isImportant)
            {
                // Например, добавить анимацию или изменить стиль
                tooltipTextComponent.fontStyle = TMPro.FontStyles.Bold;
            }
            else
            {
                tooltipTextComponent.fontStyle = TMPro.FontStyles.Normal;
            }
        }
        else
        {
            // Если нет активной подсказки, но контейнер активен, показываем пустой текст
            tooltipTextComponent.text = "";
        }
    }
    
    void UpdateSlider()
    {
        if (timelineSlider == null || isDragging) return;
        
        float progress = 0f;
        
        if (!isVideoMode && currentAnimationClip != null)
        {
            if (currentAnimator != null)
            {
                progress = currentAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime;
            }
            else if (currentAnimation != null && currentAnimationState != null)
            {
                progress = (float)(currentAnimationState.time / currentAnimationClip.length);
            }
        }
        else if (isVideoMode && currentVideoPlayer != null && currentVideoPlayer.clip != null)
        {
            double duration = currentVideoPlayer.clip.length;
            double time = currentVideoPlayer.time;
            if (duration > 0 && !double.IsNaN(time) && !double.IsInfinity(time))
            {
                progress = (float)(time / duration);
            }
        }
        else if (playableDirector != null && playableDirector.playableAsset != null && 
                 playableDirector.duration > 0 && !double.IsNaN(playableDirector.time))
        {
            progress = (float)(playableDirector.time / playableDirector.duration);
        }
        
        progress = Mathf.Clamp01(progress);
        timelineSlider.value = progress;
    }
    
    public void PlayAnimation()
    {
        isPlaying = true;
        
        if (!isVideoMode && currentAnimator != null)
        {
            // Восстанавливаем позицию анимации
            currentAnimator.Play("Animation", 0, savedAnimationTime / currentAnimationClip.length);
            currentAnimator.speed = 1;
        }
        else if (!isVideoMode && currentAnimation != null)
        {
            currentAnimationState.time = savedAnimationTime;
            currentAnimation.Play();
        }
        else if (isVideoMode && currentVideoPlayer != null)
        {
            if (currentVideoPlayer.clip != null)
            {
                if (currentVideoPlayer.isPrepared)
                {
                    currentVideoPlayer.time = savedVideoTime;
                    currentVideoPlayer.Play();
                    if (currentVideoAudioSource != null)
                        currentVideoAudioSource.Play();
                }
                else
                {
                    // Продолжим при завершении Prepare
                }
            }
        }
        
        if (playableDirector != null && playableDirector.playableAsset != null && 
            playableDirector.duration > 0)
        {
            playableDirector.time = savedAnimationTime;
            playableDirector.Play();
        }
            
        if (tooltipPlayableDirector != null && tooltipPlayableDirector.playableAsset != null && 
            tooltipPlayableDirector.duration > 0)
        {
            tooltipPlayableDirector.time = savedTooltipTime;
            tooltipPlayableDirector.Play();
        }
    }
    
    public void PauseAnimation()
    {
        isPlaying = false;
        
        // Сохраняем текущую позицию анимации
        if (!isVideoMode && currentAnimationClip != null)
        {
            if (currentAnimator != null)
            {
                var stateInfo = currentAnimator.GetCurrentAnimatorStateInfo(0);
                savedAnimationTime = stateInfo.normalizedTime * currentAnimationClip.length;
            }
            else if (currentAnimation != null && currentAnimationState != null)
            {
                savedAnimationTime = currentAnimationState.time;
            }
        }
        else if (isVideoMode && currentVideoPlayer != null)
        {
            savedVideoTime = currentVideoPlayer.time;
        }
        
        // Сохраняем позицию Timeline
        if (playableDirector != null && playableDirector.playableAsset != null)
        {
            savedAnimationTime = (float)playableDirector.time;
        }
        
        // Сохраняем позицию Timeline подсказок
        if (tooltipPlayableDirector != null && tooltipPlayableDirector.playableAsset != null)
        {
            savedTooltipTime = (float)tooltipPlayableDirector.time;
        }
        
        if (!isVideoMode && currentAnimator != null)
        {
            currentAnimator.speed = 0;
        }
        else if (!isVideoMode && currentAnimation != null)
        {
            currentAnimation.Stop();
        }
        else if (isVideoMode && currentVideoPlayer != null)
        {
            currentVideoPlayer.Pause();
            if (currentVideoAudioSource != null)
                currentVideoAudioSource.Pause();
        }
        
        if (playableDirector != null)
            playableDirector.Pause();
            
        if (tooltipPlayableDirector != null)
            tooltipPlayableDirector.Pause();
            
        if (!isVideoMode)
            Debug.Log($"Анимация приостановлена на позиции: {savedAnimationTime:F2} сек, Timeline подсказок: {savedTooltipTime:F2} сек");
        else
            Debug.Log($"Видео приостановлено на позиции: {savedVideoTime:F2} сек, Timeline подсказок: {savedTooltipTime:F2} сек");
    }
    
    public void RestartAnimation()
    {
        // Сбрасываем сохраненные позиции
        savedAnimationTime = 0f;
        savedTooltipTime = 0f;
        savedVideoTime = 0f;
        
        if (!isVideoMode && currentAnimator != null)
        {
            currentAnimator.Play("Animation", 0, 0f);
            currentAnimator.speed = 1;
        }
        else if (!isVideoMode && currentAnimation != null && currentAnimationState != null)
        {
            currentAnimationState.time = 0;
            currentAnimation.Play();
        }
        else if (isVideoMode && currentVideoPlayer != null)
        {
            if (currentVideoPlayer.isPrepared)
            {
                currentVideoPlayer.time = 0;
                if (isPlaying)
                {
                    currentVideoPlayer.Play();
                    if (currentVideoAudioSource != null)
                        currentVideoAudioSource.Play();
                }
                else
                {
                    currentVideoPlayer.Play();
                    currentVideoPlayer.Pause();
                }
            }
        }
        
        if (playableDirector != null)
            playableDirector.time = 0;
            
        if (tooltipPlayableDirector != null)
            tooltipPlayableDirector.time = 0;
        
        // Обновляем слайдер
        if (timelineSlider != null)
        {
            timelineSlider.value = 0f;
        }
        
        PlayAnimation();
    }
    
    public void CloseAnimation()
    {
        // Сбросить состояние воспроизведения к исходному
        if (isVideoMode && currentVideoPlayer != null)
        {
            try
            {
                currentVideoPlayer.time = 0;
                currentVideoPlayer.Pause();
                savedVideoTime = 0;
                if (currentVideoAudioSource != null)
                {
                    currentVideoAudioSource.Stop();
                    currentVideoAudioSource.time = 0f;
                }
                // Обновим кадр в нулевое положение
                if (currentVideoPlayer.isPrepared)
                {
                    if (currentVideoPlayer.clip != null && currentVideoPlayer.clip.frameCount > 0)
                    {
                        currentVideoPlayer.frame = 0;
                    }
                    else
                    {
                        currentVideoPlayer.time = 0;
                        currentVideoPlayer.Play();
                        currentVideoPlayer.Pause();
                    }
                }
            }
            catch {}
        }
        else
        {
            // Сброс анимации
            savedAnimationTime = 0f;
            if (currentAnimator != null)
            {
                currentAnimator.Play("Animation", 0, 0f);
                currentAnimator.speed = 0f;
            }
            if (currentAnimation != null && currentAnimationState != null)
            {
                currentAnimationState.time = 0f;
                currentAnimation.Sample();
                currentAnimation.Stop();
            }
        }

        // Сброс таймлайнов и UI
        if (playableDirector != null)
        {
            playableDirector.time = 0;
            playableDirector.Pause();
        }
        if (tooltipPlayableDirector != null)
        {
            savedTooltipTime = 0f;
            tooltipPlayableDirector.time = 0;
            tooltipPlayableDirector.Pause();
        }
        if (timelineSlider != null)
        {
            timelineSlider.value = 0f;
        }

        isPlaying = false;
        SwitchToMainMenu();
    }
    
    void SwitchToPlayerPanel()
    {
        if (mainMenuPanel != null)
            mainMenuPanel.SetActive(false);
        
        if (playerPanel != null)
            playerPanel.SetActive(true);
    }
    
    void SwitchToMainMenu()
    {
        if (playerPanel != null)
            playerPanel.SetActive(false);
        
        if (mainMenuPanel != null)
            mainMenuPanel.SetActive(true);
    }
}
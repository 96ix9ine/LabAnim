using UnityEngine;

[System.Serializable]
public class TooltipData
{
    [Header("Timeline Info")]
    public string trackName; // Имя трека в Timeline
    public float startTime; // Время начала показа подсказки (в секундах)
    public float endTime; // Время окончания показа подсказки (в секундах)
    
    [Header("Tooltip Content")]
    [TextArea(3, 10)] // Многострочное текстовое поле
    public string tooltipText; // Текст подсказки
    
    [Header("Optional Settings")]
    public Color textColor = Color.white; // Цвет текста
    [Range(0f, 1f)]
    public float alpha = 1f; // Прозрачность текста (0 = прозрачный, 1 = непрозрачный)
    public bool isImportant = false; // Важная подсказка (можно выделить визуально)
}

[System.Serializable]
public class AnimationTooltips
{
    public string animationName; // Имя анимации
    public TooltipData[] tooltips; // Массив подсказок для этой анимации
} 
using UnityEngine;
using UnityEngine.Timeline;
using UnityEngine.Video;

[System.Serializable]
public class AnimationData
{
    [Header("Basic Info")]
    public string animationName;
    public AnimationClip animationClip;
    public TimelineAsset timelineAsset;
    public Sprite previewImage;
    public GameObject prefab;
    
    [Header("Tooltips")]
    public TooltipData[] tooltips; // Подсказки для этой анимации

    [Header("Video (optional)")]
    public bool useVideo = false; // Если true – используем видео вместо 3D анимации
    public VideoClip videoClip; // Видео-клип из папки Videos
    public RenderTexture videoRenderTexture; // RenderTexture, на который выводится видео
}
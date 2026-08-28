using UnityEngine;

[CreateAssetMenu(
    fileName = "TextAnimationProfile",
    menuName = "MyNDS/Dialogue Visual Editor/Text Animation Profile")]
public sealed class TextAnimationProfile : ScriptableObject
{
    public DialogueTextEffectType EffectType = DialogueTextEffectType.None;
    public float Amplitude = 4f;
    public float Frequency = 4f;
    public float PhaseOffset = 0.05f;
    public bool Loop = true;
}

public enum DialogueTextEffectType
{
    None,
    Wave,
    Zigzag,
    Staircase,
    Shake,
    FadeIn,
    Bounce
}

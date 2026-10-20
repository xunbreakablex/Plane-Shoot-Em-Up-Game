using System.Collections;
using UnityEngine;

// Tam ekran karartma (boss fazindan donus gibi gecisler). Tum OnGUI cizimlerinin ustunde kalir.
public class ScreenFader : MonoBehaviour
{
    public float Alpha;

    /// <summary>Karartmayi verilen sureye yayarak hedefe goturur (zaman olcegi yavaslamasindan etkilenmez).</summary>
    public IEnumerator Fade(float target, float duration)
    {
        float from = Alpha;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            Alpha = Mathf.Lerp(from, target, Mathf.Clamp01(t / Mathf.Max(duration, 0.01f)));
            yield return null;
        }
        Alpha = target;
    }

    private void OnGUI()
    {
        if (Alpha <= 0.001f || Event.current.type != EventType.Repaint) return;
        GUI.depth = -1000;
        GUI.color = new Color(0f, 0f, 0f, Alpha);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }
}

// Assets/Scripts/FX/HitStopManager.cs
using System.Collections;
using UnityEngine;

public class HitStopManager : MonoBehaviour
{
    private static HitStopManager _i;
    private float _endAt = -1f;
    private bool _active;
    private float _prevTS = 1f, _prevFDT = 0.02f;
    private Coroutine _co;

    public static void Request(float seconds)
    {
        if (seconds <= 0f) return;
        Ensure().RequestInternal(seconds);
    }

    // --- internals ---
    private static HitStopManager Ensure()
    {
        if (_i) return _i;
        var go = new GameObject("HitStopManager");
        DontDestroyOnLoad(go);
        _i = go.AddComponent<HitStopManager>();
        return _i;
    }

    private void RequestInternal(float seconds)
    {
        float until = Time.unscaledTime + seconds;
        if (until > _endAt) _endAt = until;

        if (!_active)
        {
            _prevTS = Time.timeScale;
            _prevFDT = Time.fixedDeltaTime;
            Time.timeScale = 0f;
            Time.fixedDeltaTime = 0f;
            _active = true;

            if (_co != null) StopCoroutine(_co);
            _co = StartCoroutine(Loop());
        }
    }

    private IEnumerator Loop()
    {
        while (Time.unscaledTime < _endAt) yield return null;

        Time.timeScale = _prevTS;
        Time.fixedDeltaTime = _prevFDT;
        _active = false;
        _co = null;
    }
}

// AssistantController.cs
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class AssistantController : MonoBehaviour
{
    [SerializeField] private LineLibrary _library;
    [SerializeField] private TMP_Text _subtitleText;
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private float _charsPerSecond = 16f;   // длительность без озвучки
    [SerializeField] private float _minLineDuration = 1.5f;

    private readonly Queue<string> _queue = new Queue<string>();
    private Coroutine _worker;

    public bool IsSpeaking { get; private set; }
    public event Action<string> LineStarted;
    public event Action<string> LineFinished;
    public event Action QueueEmpty;

    public bool HasLine(string id) => _library != null && _library.Contains(id);

    public void Say(string lineId)            // добавить в конец очереди
    {
        _queue.Enqueue(lineId);
        _worker ??= StartCoroutine(Run());
    }

    public void SayNow(string lineId)         // прервать всё и сказать сразу
    {
        ClearQueue();
        Say(lineId);
    }

    public void ClearQueue()
    {
        _queue.Clear();
        if (_worker != null) StopCoroutine(_worker);
        _worker = null;
        if (_audioSource != null) _audioSource.Stop();
        SetSubtitle(string.Empty);
        IsSpeaking = false;
    }

    private IEnumerator Run()
    {
        while (_queue.Count > 0)
        {
            string id = _queue.Dequeue();
            if (_library == null || !_library.TryGet(id, out LineEntry line))
            {
                Debug.LogError($"[Assistant] Реплика '{id}' не найдена", this);
                continue;                      // пропускаем, игра не падает
            }

            IsSpeaking = true;
            SetSubtitle(line.text);
            LineStarted?.Invoke(id);

            float duration;
            if (line.audio != null && _audioSource != null)
            {
                _audioSource.clip = line.audio;
                _audioSource.Play();
                duration = line.audio.length;
            }
            else
            {
                duration = Mathf.Max(_minLineDuration, line.text.Length / _charsPerSecond);
            }

            yield return new WaitForSeconds(duration);

            IsSpeaking = false;
            LineFinished?.Invoke(id);
            if (line.pauseAfter > 0f) yield return new WaitForSeconds(line.pauseAfter);
        }

        SetSubtitle(string.Empty);
        _worker = null;
        QueueEmpty?.Invoke();
    }

    private void SetSubtitle(string text)
    {
        if (_subtitleText != null) _subtitleText.text = text;
    }
}

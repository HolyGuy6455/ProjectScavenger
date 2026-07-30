using System;
using System.Collections.Concurrent;
using UnityEngine;

namespace Scavenger.Network
{
    public class UnityMainThreadDispatcher : MonoBehaviour
    {
        private static readonly ConcurrentQueue<Action> _executionQueue = new ConcurrentQueue<Action>();

        private static UnityMainThreadDispatcher _instance;

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Update()
        {
            while (_executionQueue.TryDequeue(out var action))
            {
                action?.Invoke();
            }
        }

        public static void Enqueue(Action action)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }
            
            if (_instance == null)
            {
                Debug.LogWarning("[UnityMainThreadDispatcher] 씬에 Dispatcher가 없어서 큐가 실행되지 않을 수 있습니다! 씬에 스크립트를 추가해주세요.");
            }
            
            _executionQueue.Enqueue(action);
        }
    }
}

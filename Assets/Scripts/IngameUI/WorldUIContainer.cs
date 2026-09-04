using UnityEngine;

public class WorldUIContainer : MonoBehaviour
{
    private static Transform _instance;
    
    public static Transform Instance 
    { 
        get 
        {
            if (_instance == null)
            {
                WorldUIContainer container = FindAnyObjectByType<WorldUIContainer>();
                if (container != null)
                {
                    _instance = container.transform;
                }
            }
            return _instance;
        }
    }

    private void Awake()
    {
        _instance = transform;
    }
}

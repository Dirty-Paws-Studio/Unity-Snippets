using System;
using UnityEngine;

/**
 * Usage: public class YourSingletonController : Singleton<YourSingletonController>
 */
public abstract class PersistentSingleton<T> : Singleton<T> where T : MonoBehaviour
{
    private void Awake()
    {
        if (!ThisIsTheSingletonInstance(true))
        {
            return;
        }

        // ** Your child class's Awake code would be placed below in your class**
    }

    /**
     * @return true if the current instance is the singleton instance. false if there is already another singleton and
     * the current instance will be destroyed. 
     */
    protected bool InitSingletonInstance()
    {
        return base.ThisIsTheSingletonInstance(true);
    }
}
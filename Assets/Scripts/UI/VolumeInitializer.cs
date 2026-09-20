using UnityEngine;

namespace NumberBrawl
{
    public class VolumeInitializer : MonoBehaviour
    {
        private const string MasterVolumeKey = "MasterVolume";

        private void Awake()
        {
            AudioListener.volume = PlayerPrefs.GetFloat(MasterVolumeKey, 1f);
        }
    }
}
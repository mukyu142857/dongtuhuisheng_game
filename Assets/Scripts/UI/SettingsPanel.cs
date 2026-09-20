using UnityEngine;
using UnityEngine.UI;

namespace NumberBrawl
{
    public class SettingsPanel : MonoBehaviour
    {
        private const string MasterVolumeKey = "MasterVolume";

        [SerializeField] private Slider volumeSlider;
        [SerializeField] private Text volumeValueText;
        [SerializeField] private Button closeButton;

        private void Awake()
        {
            volumeSlider.minValue = 0f;
            volumeSlider.maxValue = 1f;
            volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
            closeButton.onClick.AddListener(OnCloseClicked);
        }

        private void OnEnable()
        {
            float volume = PlayerPrefs.GetFloat(MasterVolumeKey, 1f);
            volumeSlider.SetValueWithoutNotify(volume);
            ApplyVolume(volume);
        }

        private void OnDestroy()
        {
            volumeSlider.onValueChanged.RemoveListener(OnVolumeChanged);
            closeButton.onClick.RemoveListener(OnCloseClicked);
        }

        private void OnVolumeChanged(float value)
        {
            ApplyVolume(value);
            PlayerPrefs.SetFloat(MasterVolumeKey, value);
            PlayerPrefs.Save();
        }

        private void ApplyVolume(float value)
        {
            // 优先调用 AudioManager 统一管理音量
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.SetMasterVolume(value);
            }
            else
            {
                // 如果场景里没有 AudioManager，就退回直接设置 AudioListener
                AudioListener.volume = value;
            }

            if (volumeValueText != null)
            {
                volumeValueText.text = Mathf.RoundToInt(value * 100f) + "%";
            }
        }

        private void OnCloseClicked()
        {
            gameObject.SetActive(false);
        }
    }
}
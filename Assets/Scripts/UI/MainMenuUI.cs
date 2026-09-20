using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NumberBrawl
{
    public class MainMenuUI : MonoBehaviour
    {
        [Header("Main Buttons")]
        [SerializeField] private Button startButton;       // 开始乱斗
        [SerializeField] private Button settingsButton;    // 设置
        [SerializeField] private Button quitButton;        // 退出

        [Header("Sub Buttons")]
        [SerializeField] private Button instructionsButton; // 使用说明
        [SerializeField] private Button teamButton;         // 关于团队
        [SerializeField] private Button leaderboardButton;  // 排行榜

        [Header("Panels")]
        [SerializeField] private GameObject settingsPanel;

        private void Awake()
        {
            // 绑定主按钮
            startButton.onClick.AddListener(OnStartClicked);
            settingsButton.onClick.AddListener(OnSettingsClicked);
            quitButton.onClick.AddListener(OnQuitClicked);

            // 绑定子按钮
            instructionsButton.onClick.AddListener(OnInstructionsClicked);
            teamButton.onClick.AddListener(OnTeamClicked);
            leaderboardButton.onClick.AddListener(OnLeaderboardClicked);
        }

        private void OnStartClicked()
        {
            SceneManager.LoadScene(GameScenes.OperatorSelect);
        }

        private void OnSettingsClicked()
        {
            settingsPanel.SetActive(true);
        }

        private void OnQuitClicked()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // 下面是新增的按钮逻辑（暂时留空或用 Debug 打印）
        private void OnInstructionsClicked()
        {
            Debug.Log("打开使用说明面板（待实现）");
            // TODO: 这里可以打开一个放使用说明的 Panel
        }

        private void OnTeamClicked()
        {
            Debug.Log("打开关于团队面板（待实现）");
        }

        private void OnLeaderboardClicked()
        {
            Debug.Log("打开排行榜面板（待实现）");
        }
    }
}
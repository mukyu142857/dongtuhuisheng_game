using System.Collections.Generic;
using ShapeCastle.AI;
using ShapeCastle.Characters;
using ShapeCastle.Weapons;
using UnityEngine;

namespace ShapeCastle.Gameplay
{
    /// <summary>
    /// Owns the fixed match clock, freezes gameplay when time expires and
    /// decides the winner from the highest current character number.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    [DisallowMultipleComponent]
    public sealed class MatchController : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float matchDuration = 120f;
        [SerializeField, Min(0.5f)] private float timerTopMargin = 1.2f;

        private static MatchController instance;

        private float remainingTime;
        private bool isMatchOver;
        private int winningValue;
        private string resultTitle = string.Empty;
        private string resultDetail = string.Empty;
        private GameObject generatedTimerView;
        private TextMesh timerText;
        private TextMesh timerShadowText;
        private GUIStyle timerStyle;
        private GUIStyle resultTitleStyle;
        private GUIStyle resultDetailStyle;

        public float RemainingTime => Mathf.Max(0f, remainingTime);
        public bool IsMatchOver => isMatchOver;
        public int WinningValue => winningValue;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureMatchControllerExists()
        {
            EnsureExists();
        }

        public static MatchController EnsureExists()
        {
            MatchController existingController = FindObjectOfType<MatchController>();
            if (existingController != null)
            {
                return existingController;
            }

            GameObject controllerObject = new GameObject("Match Controller");
            return controllerObject.AddComponent<MatchController>();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            Time.timeScale = 1f;
            remainingTime = matchDuration;
            CreateOrResolveTimerView();
        }

        private void OnValidate()
        {
            matchDuration = Mathf.Max(1f, matchDuration);
            timerTopMargin = Mathf.Max(0.5f, timerTopMargin);
        }

        private void Update()
        {
            if (isMatchOver)
            {
                UpdateTimerView();
                return;
            }

            remainingTime = Mathf.Max(0f, remainingTime - Time.deltaTime);
            UpdateTimerView();
            if (remainingTime <= 0f)
            {
                EndMatch();
            }
        }

        private void OnDestroy()
        {
            if (instance != this)
            {
                return;
            }

            Time.timeScale = 1f;
            instance = null;

            if (generatedTimerView != null)
            {
                Destroy(generatedTimerView);
            }
        }

        private void OnGUI()
        {
            EnsureGuiStyles();
            GUI.depth = -100;

            if (timerText == null)
            {
                DrawFallbackTimer();
            }

            if (!isMatchOver)
            {
                return;
            }

            Rect panelRect = new Rect(
                Screen.width * 0.5f - 230f,
                Screen.height * 0.5f - 105f,
                460f,
                210f);
            GUI.Box(panelRect, GUIContent.none);
            GUI.Label(new Rect(panelRect.x, panelRect.y + 30f, panelRect.width, 70f),
                resultTitle, resultTitleStyle);
            GUI.Label(new Rect(panelRect.x + 20f, panelRect.y + 108f, panelRect.width - 40f, 60f),
                resultDetail, resultDetailStyle);
        }

        private void EndMatch()
        {
            if (isMatchOver)
            {
                return;
            }

            isMatchOver = true;
            remainingTime = 0f;
            DetermineResult();
            FreezeGameplay();
            Time.timeScale = 0f;
        }

        private void DetermineResult()
        {
            CharacterNumber[] characters = FindObjectsOfType<CharacterNumber>();
            if (characters.Length == 0)
            {
                winningValue = 0;
                resultTitle = "无法判定";
                resultDetail = "场景中没有可参与结算的角色";
                return;
            }

            winningValue = 0;
            foreach (CharacterNumber character in characters)
            {
                winningValue = Mathf.Max(winningValue, character.CurrentValue);
            }

            List<CharacterNumber> winners = new List<CharacterNumber>();
            foreach (CharacterNumber character in characters)
            {
                if (character.CurrentValue == winningValue)
                {
                    winners.Add(character);
                }
            }

            if (winners.Count == 1)
            {
                resultTitle = $"{winners[0].gameObject.name} 胜利！";
                resultDetail = $"最终数字：{winningValue}";
            }
            else
            {
                resultTitle = "平局！";
                resultDetail = $"{winners.Count} 名角色并列最高数字：{winningValue}";
            }
        }

        private static void FreezeGameplay()
        {
            MonoBehaviour[] behaviours = FindObjectsOfType<MonoBehaviour>();
            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (behaviour is MeleeWeaponController weapon)
                {
                    weapon.CancelCurrentAction();
                    weapon.enabled = false;
                }
                else if (behaviour is PlayerMovementController
                    || behaviour is PlayerSkillStateMachine
                    || behaviour is PlayerExecutionSkill
                    || behaviour is PlayerProtectionSkill
                    || behaviour is EnemyController)
                {
                    behaviour.enabled = false;
                }
            }

            Rigidbody2D[] bodies = FindObjectsOfType<Rigidbody2D>();
            foreach (Rigidbody2D body in bodies)
            {
                body.velocity = Vector2.zero;
                body.angularVelocity = 0f;
            }
        }

        private void EnsureGuiStyles()
        {
            if (timerStyle != null)
            {
                return;
            }

            timerStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 26,
                fontStyle = FontStyle.Bold
            };
            timerStyle.normal.textColor = Color.white;

            resultTitleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 36,
                fontStyle = FontStyle.Bold
            };
            resultTitleStyle.normal.textColor = new Color(1f, 0.88f, 0.25f, 1f);

            resultDetailStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                wordWrap = true
            };
            resultDetailStyle.normal.textColor = Color.white;
        }

        private void DrawFallbackTimer()
        {
            int totalSeconds = Mathf.CeilToInt(RemainingTime);
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;
            Rect safeArea = Screen.safeArea;
            float safeAreaTop = Screen.height - safeArea.yMax;
            Rect timerRect = new Rect(safeArea.center.x - 140f, safeAreaTop + 14f, 280f, 54f);
            GUI.Box(timerRect, GUIContent.none);
            GUI.Label(timerRect, $"TIME  {minutes:00}:{seconds:00}", timerStyle);
        }

        private void CreateOrResolveTimerView()
        {
            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                return;
            }

            Transform existingView = mainCamera.transform.Find("Match Timer (Generated)");
            if (existingView != null)
            {
                generatedTimerView = existingView.gameObject;
                timerText = generatedTimerView.GetComponent<TextMesh>();
                Transform shadowTransform = generatedTimerView.transform.Find("Shadow");
                timerShadowText = shadowTransform != null
                    ? shadowTransform.GetComponent<TextMesh>()
                    : null;
            }

            if (generatedTimerView == null)
            {
                generatedTimerView = new GameObject("Match Timer (Generated)")
                {
                    hideFlags = HideFlags.DontSave
                };
                generatedTimerView.transform.SetParent(mainCamera.transform, false);
            }

            if (timerText == null)
            {
                timerText = generatedTimerView.AddComponent<TextMesh>();
                ConfigureTimerText(timerText, Color.white, 1001);
            }

            if (timerShadowText == null)
            {
                GameObject shadowObject = new GameObject("Shadow")
                {
                    hideFlags = HideFlags.DontSave
                };
                shadowObject.transform.SetParent(generatedTimerView.transform, false);
                shadowObject.transform.localPosition = new Vector3(0.035f, -0.035f, 0.02f);
                timerShadowText = shadowObject.AddComponent<TextMesh>();
                ConfigureTimerText(timerShadowText, new Color(0f, 0f, 0f, 0.9f), 1000);
            }

            UpdateTimerView();
        }

        private void UpdateTimerView()
        {
            if (timerText == null)
            {
                CreateOrResolveTimerView();
            }

            if (timerText == null || generatedTimerView == null)
            {
                return;
            }

            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                return;
            }

            int totalSeconds = Mathf.CeilToInt(RemainingTime);
            string displayText = $"TIME  {totalSeconds / 60:00}:{totalSeconds % 60:00}";
            timerText.text = displayText;
            if (timerShadowText != null)
            {
                timerShadowText.text = displayText;
            }

            float verticalOffset = mainCamera.orthographic
                ? Mathf.Max(0.5f, mainCamera.orthographicSize - timerTopMargin)
                : 5f;
            Vector3 cameraPosition = mainCamera.transform.position;
            Vector3 worldPosition = new Vector3(cameraPosition.x, cameraPosition.y + verticalOffset, 0f);
            generatedTimerView.transform.position = worldPosition;
            generatedTimerView.transform.rotation = Quaternion.identity;
            generatedTimerView.transform.localScale = Vector3.one;
        }

        private static void ConfigureTimerText(TextMesh textMesh, Color color, int sortingOrder)
        {
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;
            textMesh.fontSize = 64;
            textMesh.characterSize = 0.075f;
            textMesh.fontStyle = FontStyle.Bold;
            textMesh.color = color;

            MeshRenderer textRenderer = textMesh.GetComponent<MeshRenderer>();
            if (textRenderer != null)
            {
                textRenderer.sortingOrder = sortingOrder;
            }
        }
    }
}

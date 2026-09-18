using ShapeCastle.AI;
using ShapeCastle.Characters;
using ShapeCastle.Gameplay;
using ShapeCastle.Weapons;
using ShapeCastle.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShapeCastle.Editor
{
    public static class BasicArenaSceneSetup
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";

        [MenuItem("Shape Castle/Setup Basic Arena")]
        public static void CreateArenaScene()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject arenaObject = FindOrCreateRoot(scene, "Arena");
            arenaObject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            arenaObject.transform.localScale = Vector3.one;

            ArenaGrid arena = GetOrAddComponent<ArenaGrid>(arenaObject);
            while (arenaObject.GetComponents<BoxCollider2D>().Length < 4)
            {
                arenaObject.AddComponent<BoxCollider2D>();
            }
            arena.RebuildArena();

            GameObject matchControllerObject = FindOrCreateRoot(scene, "Match Controller");
            GetOrAddComponent<MatchController>(matchControllerObject);

            GameObject playerObject = FindOrCreateRoot(scene, "Player");
            playerObject.transform.SetPositionAndRotation(new Vector3(-0.5f, 0.5f, -0.2f), Quaternion.identity);
            playerObject.transform.localScale = Vector3.one;

            Rigidbody2D body = GetOrAddComponent<Rigidbody2D>(playerObject);
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.sleepMode = RigidbodySleepMode2D.NeverSleep;

            BoxCollider2D hitbox = GetOrAddComponent<BoxCollider2D>(playerObject);
            hitbox.isTrigger = false;
            hitbox.offset = Vector2.zero;
            hitbox.size = Vector2.one;

            GetOrAddComponent<PlayerMovementController>(playerObject);
            PlayerAvatarView playerView = GetOrAddComponent<PlayerAvatarView>(playerObject);
            playerView.Configure("15", new Color(0.12f, 0.68f, 0.95f, 1f));
            CharacterNumber playerNumber = GetOrAddComponent<CharacterNumber>(playerObject);
            playerNumber.ConfigureInitialValue(15);
            GetOrAddComponent<CharacterRespawnController>(playerObject);
            GetOrAddComponent<PlayerSkillStateMachine>(playerObject);
            WeaponHolder weaponHolder = GetOrAddComponent<WeaponHolder>(playerObject);

            Transform weaponTransform = playerObject.transform.Find("Minus Weapon");
            if (weaponTransform == null)
            {
                GameObject weaponObject = new GameObject("Minus Weapon");
                weaponTransform = weaponObject.transform;
                weaponTransform.SetParent(playerObject.transform, false);
            }

            weaponTransform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            weaponTransform.localScale = Vector3.one;

            BoxCollider2D weaponSensor = GetOrAddComponent<BoxCollider2D>(weaponTransform.gameObject);
            weaponSensor.isTrigger = true;
            weaponSensor.offset = new Vector2(0.9f, 0f);
            weaponSensor.size = new Vector2(2f, 0.5f);
            MeleeWeaponController weaponController = GetOrAddComponent<MeleeWeaponController>(weaponTransform.gameObject);
            weaponController.SetRandomizeOperatorOnStart(true);
            GetOrAddComponent<OperatorWeaponBodyView>(weaponTransform.gameObject);
            GetOrAddComponent<OperatorWeaponSymbolView>(weaponTransform.gameObject);
            weaponHolder.TryClaim(weaponController);
            GetOrAddComponent<PlayerExecutionSkill>(playerObject);
            GetOrAddComponent<PlayerProtectionSkill>(playerObject);

            GameObject[] enemyObjects =
            {
                CreateOrConfigureEnemy(scene, "Enemy 1", new Vector3(4f, 4f, -0.2f), playerObject.transform),
                CreateOrConfigureEnemy(scene, "Enemy 2", new Vector3(-4f, 4f, -0.2f), playerObject.transform),
                CreateOrConfigureEnemy(scene, "Enemy 3", new Vector3(0f, -4f, -0.2f), playerObject.transform)
            };

            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                GameObject cameraObject = FindOrCreateRoot(scene, "Main Camera");
                cameraObject.tag = "MainCamera";
                mainCamera = GetOrAddComponent<Camera>(cameraObject);
                GetOrAddComponent<AudioListener>(cameraObject);
            }

            ArenaCameraFitter cameraFitter = GetOrAddComponent<ArenaCameraFitter>(mainCamera.gameObject);
            cameraFitter.SetTarget(playerObject.transform);
            cameraFitter.ApplyNow();

            EditorUtility.SetDirty(arenaObject);
            EditorUtility.SetDirty(matchControllerObject);
            EditorUtility.SetDirty(playerObject);
            EditorUtility.SetDirty(weaponTransform.gameObject);
            foreach (GameObject enemyObject in enemyObjects)
            {
                EditorUtility.SetDirty(enemyObject);
            }
            EditorUtility.SetDirty(mainCamera.gameObject);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();

            Debug.Log("Basic arena created: 20x20 grid, 1x1 player hitbox, WASD world movement.");
        }

        private static GameObject CreateOrConfigureEnemy(
            Scene scene,
            string enemyName,
            Vector3 spawnPosition,
            Transform target)
        {
            GameObject enemyObject = FindOrCreateRoot(scene, enemyName);
            enemyObject.transform.SetPositionAndRotation(spawnPosition, Quaternion.identity);
            enemyObject.transform.localScale = Vector3.one;

            Rigidbody2D enemyBody = GetOrAddComponent<Rigidbody2D>(enemyObject);
            enemyBody.bodyType = RigidbodyType2D.Dynamic;
            enemyBody.gravityScale = 0f;
            enemyBody.freezeRotation = true;
            enemyBody.interpolation = RigidbodyInterpolation2D.Interpolate;
            enemyBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            enemyBody.sleepMode = RigidbodySleepMode2D.NeverSleep;

            BoxCollider2D enemyHitbox = GetOrAddComponent<BoxCollider2D>(enemyObject);
            enemyHitbox.isTrigger = false;
            enemyHitbox.offset = Vector2.zero;
            enemyHitbox.size = Vector2.one;

            PlayerAvatarView enemyView = GetOrAddComponent<PlayerAvatarView>(enemyObject);
            enemyView.Configure("15", new Color(0.95f, 0.22f, 0.18f, 1f));
            CharacterNumber enemyNumber = GetOrAddComponent<CharacterNumber>(enemyObject);
            enemyNumber.ConfigureInitialValue(15);
            GetOrAddComponent<CharacterRespawnController>(enemyObject);
            WeaponHolder enemyWeaponHolder = GetOrAddComponent<WeaponHolder>(enemyObject);

            Transform enemyWeaponTransform = enemyObject.transform.Find("Minus Weapon");
            if (enemyWeaponTransform == null)
            {
                GameObject enemyWeaponObject = new GameObject("Minus Weapon");
                enemyWeaponTransform = enemyWeaponObject.transform;
                enemyWeaponTransform.SetParent(enemyObject.transform, false);
            }

            enemyWeaponTransform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            enemyWeaponTransform.localScale = Vector3.one;

            BoxCollider2D enemyWeaponSensor = GetOrAddComponent<BoxCollider2D>(enemyWeaponTransform.gameObject);
            enemyWeaponSensor.isTrigger = true;
            enemyWeaponSensor.offset = new Vector2(0.9f, 0f);
            enemyWeaponSensor.size = new Vector2(2f, 0.5f);

            MeleeWeaponController enemyWeapon =
                GetOrAddComponent<MeleeWeaponController>(enemyWeaponTransform.gameObject);
            enemyWeapon.SetAcceptPlayerInput(false);
            enemyWeapon.SetRandomizeOperatorOnStart(true);
            GetOrAddComponent<OperatorWeaponBodyView>(enemyWeaponTransform.gameObject);
            GetOrAddComponent<OperatorWeaponSymbolView>(enemyWeaponTransform.gameObject);
            enemyWeaponHolder.TryClaim(enemyWeapon);

            EnemyController enemyController = GetOrAddComponent<EnemyController>(enemyObject);
            enemyController.SetTarget(target);
            enemyController.SetWeapon(enemyWeapon);

            EditorUtility.SetDirty(enemyWeaponTransform.gameObject);
            return enemyObject;
        }

        private static GameObject FindOrCreateRoot(Scene scene, string objectName)
        {
            foreach (GameObject rootObject in scene.GetRootGameObjects())
            {
                if (rootObject.name == objectName)
                {
                    return rootObject;
                }
            }

            GameObject newObject = new GameObject(objectName);
            SceneManager.MoveGameObjectToScene(newObject, scene);
            return newObject;
        }

        private static T GetOrAddComponent<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            return component != null ? component : target.AddComponent<T>();
        }
    }
}

using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// プレイヤーのアニメーションを、メニュー1回でプロジェクトに組み込むエディタ拡張。
///
///   1. Models/Player の fbx を Generic で取り込み、モデル (SK_Player_Idle) から Avatar を作って他の fbx に使い回す
///   2. 移動・構えのクリップをループにし、投げは1回だけ流す
///   3. AC_Player を作り直す
///      ・Locomotion : MoveX / MoveZ の 2D ブレンド (待機 / 前 / 後ろ / 左 / 右)
///      ・Aim        : 構えたままの 2D ブレンド (構え / 構え前 / 構え後ろ / 構え左 / 構え右)
///      ・Throw      : 投げを1回流して Locomotion へ戻る
///   4. P_Player の仮モデル (testchara) を止め、新しいモデルを置いて AC_Player と PlayerAnimation をつなぐ
///
/// 無いクリップは飛ばして結果に書くので、fbx が揃う前でも実行できる。後から fbx を足したらもう一度実行すればよい。
/// AC_Player は毎回中身を作り直す (アセットは残すので、P_Player などからの参照は切れない)。
/// </summary>
public static class PlayerAnimationSetupMenu
{
    // ==========================================
    // Private Fields
    // ==========================================

    private const string MENU_PATH = "Bomb/Setup/Player Animation";
    private const string DIALOG_TITLE = "Player Animation Setup";

    private const string MODEL_FOLDER = "Assets/_Project/Models/Player";

    /// <summary>見た目のモデル。Avatar もここから作る。待機のクリップが別に無ければ、これのクリップを待機に使う。</summary>
    private const string IDLE_PATH = MODEL_FOLDER + "/SK_Player_Idle.fbx";

    /// <summary>待機だけの fbx (taiki)。あればモデルのクリップの代わりに待機に使う。</summary>
    private const string IDLE_CLIP_PATH = MODEL_FOLDER + "/A_Player_Idle.fbx";

    private const string RUN_PATH = MODEL_FOLDER + "/A_Player_Run.fbx";
    private const string BACK_PATH = MODEL_FOLDER + "/A_Player_Back.fbx";
    private const string LEFT_PATH = MODEL_FOLDER + "/A_Player_Left.fbx";
    private const string RIGHT_PATH = MODEL_FOLDER + "/A_Player_Right.fbx";

    private const string KAMAE_PATH = MODEL_FOLDER + "/A_Player_Kamae.fbx";

    /// <summary>構えたまま前に進む fbx。まだ無いので、無ければ前も構え (Kamae) で埋める。</summary>
    private const string KAMAE_FRONT_PATH = MODEL_FOLDER + "/A_Player_KamaeFront.fbx";

    private const string KAMAE_BACK_PATH = MODEL_FOLDER + "/A_Player_KamaeBack.fbx";
    private const string KAMAE_LEFT_PATH = MODEL_FOLDER + "/A_Player_KamaeLeft.fbx";
    private const string KAMAE_RIGHT_PATH = MODEL_FOLDER + "/A_Player_KamaeRight.fbx";

    private const string THROW_PATH = MODEL_FOLDER + "/A_Player_Throw.fbx";

    private const string CONTROLLER_FOLDER = "Assets/_Project/Animations/Player";
    private const string CONTROLLER_PATH = CONTROLLER_FOLDER + "/AC_Player.controller";
    private const string PLAYER_PREFAB_PATH = "Assets/_Project/Prefabs/P_Player.prefab";

    private const string RENDERER_NAME = "Renderer";
    private const string OLD_MODEL_NAME = "testchara";
    private const string NEW_MODEL_NAME = "SK_Player";

    private const string SPEED_PARAMETER = "Speed";
    private const string MOVE_X_PARAMETER = "MoveX";
    private const string MOVE_Z_PARAMETER = "MoveZ";
    private const string IS_AIMING_PARAMETER = "IsAiming";
    private const string THROW_PARAMETER = "Throw";

    private const string LOCOMOTION_STATE_NAME = "Locomotion";
    private const string AIM_STATE_NAME = "Aim";
    private const string THROW_STATE_NAME = "Throw";

    /// <summary>投げのクリップをどこまで流したら移動へ戻り始めるか (0〜1)。</summary>
    private const float THROW_EXIT_TIME = 0.85f;

    /// <summary>アニメーションを切り替えるときに重ねる時間 (秒)。</summary>
    private const float TRANSITION_SECONDS = 0.05f;

    /// <summary>構えに入る・抜けるときに重ねる時間 (秒)。</summary>
    private const float AIM_TRANSITION_SECONDS = 0.1f;

    /// <summary>足元を決める部品の名前に含まれる言葉。</summary>
    private const string FOOT_PART_KEYWORD = "boot";

    /// <summary>足元よりこれだけ下までは、足元より下の部品として捨てずに数える (モデルの座標)。</summary>
    private const float FLOOR_TOLERANCE = 0.001f;

    // ==========================================
    // Private Methods
    // ==========================================

    [MenuItem(MENU_PATH)]
    private static void Setup()
    {
        StringBuilder report = new StringBuilder();

        if (AssetDatabase.LoadAssetAtPath<GameObject>(IDLE_PATH) == null)
        {
            EditorUtility.DisplayDialog(DIALOG_TITLE, $"{IDLE_PATH} が無い。", "OK");
            return;
        }

        // モデルの fbx を先に取り込み、その Avatar を他の fbx に渡す。
        Avatar avatar = ImportModel(IDLE_PATH, null, true, report);

        string[] loopPaths =
        {
            IDLE_CLIP_PATH, RUN_PATH, BACK_PATH, LEFT_PATH, RIGHT_PATH,
            KAMAE_PATH, KAMAE_FRONT_PATH, KAMAE_BACK_PATH, KAMAE_LEFT_PATH, KAMAE_RIGHT_PATH,
        };

        foreach (string path in loopPaths)
        {
            ImportModel(path, avatar, true, report);
        }

        // 投げは1回だけ流すのでループしない。
        ImportModel(THROW_PATH, avatar, false, report);

        AnimatorController controller = RebuildController(report);
        SetupPrefab(controller, report);

        AssetDatabase.SaveAssets();

        string message = report.ToString();
        Debug.Log($"[{nameof(PlayerAnimationSetupMenu)}]\n{message}");
        EditorUtility.DisplayDialog(DIALOG_TITLE, message, "OK");
    }

    /// <summary>
    /// fbx を Generic で取り込み、この fbx の Avatar を返す。
    /// sourceAvatar が null ならこの fbx から Avatar を作る。fbx が無ければ何もせず null を返す。
    /// </summary>
    private static Avatar ImportModel(string path, Avatar sourceAvatar, bool isLoop, StringBuilder report)
    {
        if (AssetImporter.GetAtPath(path) is not ModelImporter importer)
        {
            report.AppendLine($"・{System.IO.Path.GetFileName(path)} が無いので飛ばした。");
            return null;
        }

        importer.animationType = ModelImporterAnimationType.Generic;

        if (sourceAvatar == null)
        {
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        }
        else
        {
            importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
            importer.sourceAvatar = sourceAvatar;
        }

        importer.importAnimation = true;

        // ここで1回取り込み直さないと、defaultClipAnimations が古い設定のまま返ってくる。
        importer.SaveAndReimport();

        ModelImporterClipAnimation[] clips = importer.clipAnimations.Length > 0
            ? importer.clipAnimations
            : importer.defaultClipAnimations;

        string clipName = System.IO.Path.GetFileNameWithoutExtension(path);

        for (int i = 0; i < clips.Length; i++)
        {
            clips[i].loopTime = isLoop;

            // fbx の中のテイク名はばらばらなので、ファイル名にそろえて探しやすくする。
            if (clips.Length == 1)
            {
                clips[i].name = clipName;
            }
        }

        importer.clipAnimations = clips;
        importer.SaveAndReimport();

        string loopText = isLoop ? "ループ" : "1回だけ";
        report.AppendLine($"・{System.IO.Path.GetFileName(path)} を Generic で取り込んだ (クリップ {clips.Length} 個、{loopText})。");

        return AssetDatabase.LoadAssetAtPath<Avatar>(path);
    }

    /// <summary>fbx の中のクリップを返す。fbx が無ければ null。</summary>
    private static AnimationClip LoadClip(string path)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);

        for (int i = 0; i < assets.Length; i++)
        {
            // fbx には Unity が足すプレビュー用のクリップも入っているので除く。
            if (assets[i] is AnimationClip clip && !clip.name.StartsWith("__preview__"))
            {
                return clip;
            }
        }

        return null;
    }

    /// <summary>先に見つかったほうのクリップを返す。待機は taiki が無ければモデルのクリップを使う、のように使う。</summary>
    private static AnimationClip LoadClip(string path, string fallbackPath)
    {
        AnimationClip clip = LoadClip(path);
        return clip != null ? clip : LoadClip(fallbackPath);
    }

    /// <summary>
    /// AC_Player の中身を作り直す。アセット自体は消さないので、P_Player などからの参照はそのまま。
    /// </summary>
    private static AnimatorController RebuildController(StringBuilder report)
    {
        if (!AssetDatabase.IsValidFolder(CONTROLLER_FOLDER))
        {
            AssetDatabase.CreateFolder("Assets/_Project/Animations", "Player");
        }

        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(CONTROLLER_PATH);

        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(CONTROLLER_PATH);
        }

        ClearController(controller);

        controller.AddParameter(SPEED_PARAMETER, AnimatorControllerParameterType.Float);
        controller.AddParameter(MOVE_X_PARAMETER, AnimatorControllerParameterType.Float);
        controller.AddParameter(MOVE_Z_PARAMETER, AnimatorControllerParameterType.Float);
        controller.AddParameter(IS_AIMING_PARAMETER, AnimatorControllerParameterType.Bool);
        controller.AddParameter(THROW_PARAMETER, AnimatorControllerParameterType.Trigger);

        AnimatorStateMachine machine = controller.layers[0].stateMachine;

        AnimationClip idleClip = LoadClip(IDLE_CLIP_PATH, IDLE_PATH);
        AnimationClip kamaeClip = LoadClip(KAMAE_PATH);

        // 体は常にカメラを向いているので、入力の向き = 体から見た向き。前後左右のクリップを 2D で混ぜる。
        AnimatorState locomotion = controller.CreateBlendTreeInController(LOCOMOTION_STATE_NAME, out BlendTree locomotionTree, 0);
        SetupBlendTree(locomotionTree, report, LOCOMOTION_STATE_NAME,
            (idleClip, Vector2.zero, "待機"),
            (LoadClip(RUN_PATH), Vector2.up, "前"),
            (LoadClip(BACK_PATH), Vector2.down, "後ろ"),
            (LoadClip(LEFT_PATH), Vector2.left, "左"),
            (LoadClip(RIGHT_PATH), Vector2.right, "右"));

        // 構えたまま前に進むクリップが無いうちは、前も構えの姿勢で埋める (止まったまま滑るように見える)。
        AnimationClip kamaeFrontClip = LoadClip(KAMAE_FRONT_PATH, KAMAE_PATH);

        AnimatorState aim = controller.CreateBlendTreeInController(AIM_STATE_NAME, out BlendTree aimTree, 0);
        SetupBlendTree(aimTree, report, AIM_STATE_NAME,
            (kamaeClip, Vector2.zero, "構え"),
            (kamaeFrontClip, Vector2.up, "構え前"),
            (LoadClip(KAMAE_BACK_PATH), Vector2.down, "構え後ろ"),
            (LoadClip(KAMAE_LEFT_PATH), Vector2.left, "構え左"),
            (LoadClip(KAMAE_RIGHT_PATH), Vector2.right, "構え右"));

        AnimatorState throwState = machine.AddState(THROW_STATE_NAME);
        throwState.motion = LoadClip(THROW_PATH);

        PlaceState(machine, locomotion, new Vector3(300f, 0f, 0f));
        PlaceState(machine, aim, new Vector3(600f, 0f, 0f));
        PlaceState(machine, throwState, new Vector3(450f, 150f, 0f));
        machine.defaultState = locomotion;

        // 歩いていても止まっていても、チャージを始めたら構える。
        AnimatorStateTransition toAim = AddTransition(locomotion, aim);
        toAim.duration = AIM_TRANSITION_SECONDS;
        toAim.AddCondition(AnimatorConditionMode.If, 0f, IS_AIMING_PARAMETER);

        AnimatorStateTransition toLocomotion = AddTransition(aim, locomotion);
        toLocomotion.duration = AIM_TRANSITION_SECONDS;
        toLocomotion.AddCondition(AnimatorConditionMode.IfNot, 0f, IS_AIMING_PARAMETER);

        // どこからでも投げた瞬間に投げを流す。投げの途中で次を投げたら頭から流し直す。
        AnimatorStateTransition toThrow = machine.AddAnyStateTransition(throwState);
        SetupTransition(toThrow);
        toThrow.canTransitionToSelf = true;
        toThrow.AddCondition(AnimatorConditionMode.If, 0f, THROW_PARAMETER);

        // 投げ終わったら移動へ。まだチャージしていれば、移動からすぐ構えに移る。
        AnimatorStateTransition throwToLocomotion = AddTransition(throwState, locomotion);
        throwToLocomotion.hasExitTime = true;
        throwToLocomotion.exitTime = THROW_EXIT_TIME;

        EditorUtility.SetDirty(controller);

        string throwText = throwState.motion != null ? "投げ" : "投げ (クリップ無し)";
        report.AppendLine($"・AC_Player を作り直した ({LOCOMOTION_STATE_NAME} / {AIM_STATE_NAME} / {throwText})。");
        return controller;
    }

    /// <summary>パラメータ・ステート・遷移と、前に作ったブレンドツリーを全部消す。</summary>
    private static void ClearController(AnimatorController controller)
    {
        while (controller.parameters.Length > 0)
        {
            controller.RemoveParameter(0);
        }

        AnimatorStateMachine machine = controller.layers[0].stateMachine;

        foreach (AnimatorStateTransition transition in machine.anyStateTransitions)
        {
            machine.RemoveAnyStateTransition(transition);
        }

        foreach (ChildAnimatorState child in machine.states)
        {
            machine.RemoveState(child.state);
        }

        // ブレンドツリーはステートを消しても AC_Player の中に残るので、別に消す。
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(CONTROLLER_PATH))
        {
            if (asset is BlendTree)
            {
                Object.DestroyImmediate(asset, true);
            }
        }
    }

    /// <summary>MoveX / MoveZ で混ぜる 2D ブレンドツリーにクリップを並べる。無いクリップは飛ばして結果に書く。</summary>
    private static void SetupBlendTree(
        BlendTree tree,
        StringBuilder report,
        string stateName,
        params (AnimationClip Clip, Vector2 Position, string Label)[] motions)
    {
        tree.blendType = BlendTreeType.FreeformDirectional2D;
        tree.blendParameter = MOVE_X_PARAMETER;
        tree.blendParameterY = MOVE_Z_PARAMETER;

        List<string> missing = new List<string>();

        foreach ((AnimationClip clip, Vector2 position, string label) in motions)
        {
            if (clip == null)
            {
                missing.Add(label);
                continue;
            }

            tree.AddChild(clip, position);
        }

        if (missing.Count > 0)
        {
            report.AppendLine($"・{stateName} のクリップが足りない: {string.Join("、", missing)}");
        }
    }

    /// <summary>ステートの置き場所を、Animator ウィンドウで見やすい位置にそろえる。</summary>
    private static void PlaceState(AnimatorStateMachine machine, AnimatorState state, Vector3 position)
    {
        ChildAnimatorState[] states = machine.states;

        for (int i = 0; i < states.Length; i++)
        {
            if (states[i].state == state)
            {
                states[i].position = position;
            }
        }

        machine.states = states;
    }

    private static AnimatorStateTransition AddTransition(AnimatorState from, AnimatorState to)
    {
        AnimatorStateTransition transition = from.AddTransition(to);
        SetupTransition(transition);
        return transition;
    }

    /// <summary>入力にすぐ反応させるため、最後まで待たずに短く重ねて切り替える。</summary>
    private static void SetupTransition(AnimatorStateTransition transition)
    {
        transition.hasExitTime = false;
        transition.duration = TRANSITION_SECONDS;
        transition.hasFixedDuration = true;
    }


    private static void SetupPrefab(AnimatorController controller, StringBuilder report)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PLAYER_PREFAB_PATH);

        if (root == null)
        {
            report.AppendLine($"・プレハブが見つからない: {PLAYER_PREFAB_PATH}");
            return;
        }

        try
        {
            Transform rendererRoot = root.transform.Find(RENDERER_NAME);

            if (rendererRoot == null)
            {
                report.AppendLine($"・P_Player に {RENDERER_NAME} が無い。");
                return;
            }

            Transform model = rendererRoot.Find(NEW_MODEL_NAME);

            if (model == null)
            {
                model = PlaceModel(rendererRoot, report);
            }
            else
            {
                report.AppendLine("・P_Player にはすでに新しいモデルがある。");
            }

            FitModel(root, model, report);
            HideOldModel(rendererRoot, report);

            Animator animator = model.GetComponent<Animator>();

            if (animator == null)
            {
                animator = model.gameObject.AddComponent<Animator>();
            }

            animator.runtimeAnimatorController = controller;
            animator.avatar = AssetDatabase.LoadAssetAtPath<Avatar>(IDLE_PATH);

            // 移動は CharacterController で行うので、アニメーションでは動かさない。
            animator.applyRootMotion = false;

            PlayerAnimation playerAnimation = root.GetComponent<PlayerAnimation>();

            if (playerAnimation == null)
            {
                playerAnimation = root.AddComponent<PlayerAnimation>();
                report.AppendLine("・P_Player に PlayerAnimation を付けた。");
            }

            SerializedObject serialized = new SerializedObject(playerAnimation);
            serialized.FindProperty("_animator").objectReferenceValue = animator;
            serialized.FindProperty("_controller").objectReferenceValue = root.GetComponent<PlayerController>();
            serialized.FindProperty("_thrower").objectReferenceValue = root.GetComponent<PlayerThrower>();
            serialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PLAYER_PREFAB_PATH);
            report.AppendLine("・P_Player を保存した。");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>新しいモデルを置く。大きさと高さは <see cref="FitModel"/> で合わせる。</summary>
    private static Transform PlaceModel(Transform rendererRoot, StringBuilder report)
    {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(IDLE_PATH);
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(source, rendererRoot);
        instance.name = NEW_MODEL_NAME;
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;

        report.AppendLine("・新しいモデルを置いた。");
        return instance.transform;
    }

    /// <summary>
    /// 待機アニメーションの姿勢で測ったモデルの高さと足元を、CharacterController に合わせる。
    /// モデルを置いたときの姿勢 (T ポーズなど) で測ると、再生してアニメーションが動いた瞬間に足が浮くため。
    /// </summary>
    private static void FitModel(GameObject root, Transform model, StringBuilder report)
    {
        if (!root.TryGetComponent(out CharacterController characterController))
        {
            report.AppendLine("・CharacterController が無いので、モデルの大きさは合わせなかった。");
            return;
        }

        if (!TryMeasureIdlePose(out Bounds bounds) || bounds.size.y <= 0.0001f)
        {
            report.AppendLine("・モデルの大きさを測れなかった。SK_Player の Scale と Position は手で合わせる。");
            return;
        }

        float scale = characterController.height / bounds.size.y;
        model.localScale = Vector3.one * scale;

        // CharacterController は Skin Width の分だけ地面から浮くので、その分も下げて足を地面に付ける。
        float bottom = characterController.center.y
            - characterController.height * 0.5f
            - characterController.skinWidth;

        Vector3 position = model.localPosition;
        position.y = bottom - bounds.min.y * scale;
        model.localPosition = position;

        report.AppendLine($"・待機の姿勢で測って、大きさ {scale:0.###} 倍、高さ {position.y:0.###} に合わせた。");
    }

    /// <summary>
    /// 待機アニメーションの最初の姿勢にした仮のモデルを作り、見た目の範囲をモデル自身の座標で返す。
    /// 骨で曲げた後の頂点で測るので、Renderer.bounds より正確になる。
    /// </summary>
    private static bool TryMeasureIdlePose(out Bounds bounds)
    {
        bounds = default;

        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(IDLE_PATH);

        if (source == null)
        {
            return false;
        }

        GameObject temp = Object.Instantiate(source);
        temp.hideFlags = HideFlags.HideAndDontSave;
        Mesh baked = new Mesh();
        bool hasBounds = false;
        StringBuilder measureLog = new StringBuilder();
        List<(string Name, float MinY, float MaxY)> parts = new List<(string Name, float MinY, float MaxY)>();

        try
        {
            temp.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            temp.transform.localScale = Vector3.one;

            AnimationClip idleClip = LoadClip(IDLE_PATH);

            if (idleClip != null)
            {
                idleClip.SampleAnimation(temp, 0f);
            }

            // fbx には作業用の板 (pPlane など) が非表示で入っているので、見えている物だけで測る。
            foreach (SkinnedMeshRenderer skinned in temp.GetComponentsInChildren<SkinnedMeshRenderer>(false))
            {
                if (!skinned.enabled || skinned.sharedMesh == null)
                {
                    continue;
                }

                skinned.BakeMesh(baked, false);
                Matrix4x4 toWorld = skinned.transform.localToWorldMatrix;
                float minY = float.MaxValue;
                float maxY = float.MinValue;

                // 行列でまとめて変換する。左右反転 (Scale がマイナス) の部品は、
                // 位置と回転だけで計算すると上下が逆になり、足元より下にあると誤って測ってしまう。
                foreach (Vector3 vertex in baked.vertices)
                {
                    Vector3 point = toWorld.MultiplyPoint3x4(vertex);
                    minY = Mathf.Min(minY, point.y);
                    maxY = Mathf.Max(maxY, point.y);
                }

                measureLog.AppendLine($"{skinned.name}: y {minY:0.###} 〜 {maxY:0.###}");
                parts.Add((skinned.name, minY, maxY));
            }

            foreach (MeshFilter filter in temp.GetComponentsInChildren<MeshFilter>(false))
            {
                if (filter.sharedMesh == null
                    || !filter.TryGetComponent(out MeshRenderer meshRenderer)
                    || !meshRenderer.enabled)
                {
                    continue;
                }

                float minY = float.MaxValue;
                float maxY = float.MinValue;

                foreach (Vector3 vertex in filter.sharedMesh.vertices)
                {
                    Vector3 point = filter.transform.TransformPoint(vertex);
                    minY = Mathf.Min(minY, point.y);
                    maxY = Mathf.Max(maxY, point.y);
                }

                measureLog.AppendLine($"{filter.name}: y {minY:0.###} 〜 {maxY:0.###}");
                parts.Add((filter.name, minY, maxY));
            }

            hasBounds = TryGetBodyRange(parts, out float floorY, out float topY);

            if (hasBounds)
            {
                bounds = new Bounds(new Vector3(0f, (floorY + topY) * 0.5f, 0f), new Vector3(0f, topY - floorY, 0f));
                measureLog.AppendLine($"→ 足元 {floorY:0.###}、頭の上 {topY:0.###} として合わせる。");
            }

            // 足が浮く・沈むときに、どの部品が範囲を広げているか Console で確かめられるようにする。
            Debug.Log($"[{nameof(PlayerAnimationSetupMenu)}] 測った部品 (待機の姿勢、モデルの座標)\n{measureLog}");
        }
        finally
        {
            Object.DestroyImmediate(baked);
            Object.DestroyImmediate(temp);
        }

        return hasBounds;
    }

    /// <summary>
    /// 部品ごとの高さから、足元と頭の上を決める。
    /// 足元は靴 (名前に boot を含む部品) の一番下。靴が無ければ全部品の一番下。
    /// 足元より下にある部品は数えない。左右反転の部品が上下逆の位置で取り込まれていて、
    /// それを数えると背が2倍あると判断してしまうため。
    /// </summary>
    private static bool TryGetBodyRange(
        List<(string Name, float MinY, float MaxY)> parts, out float floorY, out float topY)
    {
        floorY = float.MaxValue;
        topY = float.MinValue;

        if (parts.Count == 0)
        {
            return false;
        }

        foreach ((string name, float minY, float _) in parts)
        {
            if (name.IndexOf(FOOT_PART_KEYWORD, System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                floorY = Mathf.Min(floorY, minY);
            }
        }

        if (floorY == float.MaxValue)
        {
            foreach ((string _, float minY, float _) in parts)
            {
                floorY = Mathf.Min(floorY, minY);
            }
        }

        foreach ((string _, float minY, float maxY) in parts)
        {
            if (minY >= floorY - FLOOR_TOLERANCE)
            {
                topY = Mathf.Max(topY, maxY);
            }
        }

        return topY > floorY;
    }

    /// <summary>仮モデルは消さずに非表示にする。戻したいときに Inspector で有効にするだけで済むように。</summary>
    private static void HideOldModel(Transform rendererRoot, StringBuilder report)
    {
        Transform oldModel = rendererRoot.Find(OLD_MODEL_NAME);

        if (oldModel == null || !oldModel.gameObject.activeSelf)
        {
            return;
        }

        oldModel.gameObject.SetActive(false);
        report.AppendLine("・仮モデル testchara を非表示にした。");
    }
}

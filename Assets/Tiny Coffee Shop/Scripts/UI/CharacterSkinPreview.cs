using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

// A real 3D wardrobe, not a screenshot. The models live far outside the
// kitchen, are rendered by their own camera into the menu RawImage, and are
// animated with unscaled time because the front menu pauses gameplay.
public class CharacterSkinPreview : MonoBehaviour,
    IPointerDownHandler, IPointerUpHandler
{
    // Public because co-op reads it too. The other player's menu needs to
    // know which animal we picked, and asking this component would mean the
    // wardrobe has to exist in every scene the answer is needed in
    public const string skinPref = "CookedFast.Character.Selected";
    private const string hatPref = "CookedFast.Accessory.Hat.Selected";
    private const float previewLean = 16f;
    private static readonly int greetingState =
        Animator.StringToHash("Base Layer.Greet_Start");
    private static readonly int idleState =
        Animator.StringToHash("Base Layer.Idle");
    // The project's URP renderer draws opaque objects only from its selected
    // layer mask (2551); layer 30 is deliberately absent from that mask. The
    // preview stage is already 200 units outside the kitchen with a 20-unit
    // far plane, so Default is both isolated and renderable.
    private const int previewLayer = 0;

    [SerializeField] private RawImage display;
    [SerializeField] private Text skinName;
    [SerializeField] private Button previousButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private GameObject[] skinModels;
    [SerializeField] private string[] skinNames;
    [SerializeField] private Avatar sharedAvatar;
    [SerializeField] private RuntimeAnimatorController previewController;

    [Header("Magaza vitrini")]
    [SerializeField] private RawImage wardrobeDisplay;
    [SerializeField] private Text wardrobeSkinName;
    [SerializeField] private Button wardrobePreviousSkin;
    [SerializeField] private Button wardrobeNextSkin;
    [SerializeField] private Text hatName;
    [SerializeField] private Button previousHatButton;
    [SerializeField] private Button nextHatButton;
    [SerializeField] private GameObject[] hatModels;
    [SerializeField] private string[] hatNames;

    private RenderTexture texture;
    private GameObject stage;
    private GameObject shown;
    private Animator shownAnimator;
    private Camera previewCamera;
    private Coroutine preparing;
    private int selected;
    private int selectedHat = -1;
    private float pressedAt;
    private bool bound;
    // The authored kitchen player starts as Squirrel (index zero). Unlike the
    // selected preference, this tracks what is actually installed on Player,
    // so returning to the menu can replace Bear -> Cat or Bear -> Squirrel
    // without rebuilding the scene.
    private int appliedSelection;

    public int Selected => selected;
    public int SelectedHat => selectedHat;

    // Lent to the co-op mate slot, which shows the OTHER player's animal beside
    // ours. Borrowed rather than duplicated: a second serialized list of the
    // same models is a list that gets a new animal added to it half the time
    public int SkinCount => Count;
    public Avatar SharedAvatar => sharedAvatar;
    public RuntimeAnimatorController PreviewController => previewController;

    public GameObject SkinModel(int index)
    {
        return index >= 0 && index < Count ? skinModels[index] : null;
    }

    public string SkinLabel(int index)
    {
        // Bounds checked here rather than inside NameAt, which is called from
        // places that already know the index is good. This one is handed a
        // number that arrived over the network from another build
        return index >= 0 && index < Count ? NameAt(index) : "";
    }

    private int Count => skinModels == null ? 0 : skinModels.Length;
    private int HatCount => hatModels == null ? 0 : hatModels.Length;

    private void Awake()
    {
        if (!bound)
        {
            if (previousButton != null)
                previousButton.onClick.AddListener(Previous);
            if (nextButton != null)
                nextButton.onClick.AddListener(Next);
            if (wardrobePreviousSkin != null)
                wardrobePreviousSkin.onClick.AddListener(Previous);
            if (wardrobeNextSkin != null)
                wardrobeNextSkin.onClick.AddListener(Next);
            if (previousHatButton != null)
                previousHatButton.onClick.AddListener(PreviousHat);
            if (nextHatButton != null)
                nextHatButton.onClick.AddListener(NextHat);
            bound = true;
        }

        selected = Mathf.Clamp(PlayerPrefs.GetInt(skinPref, 0),
            0, Mathf.Max(0, Count - 1));
        selectedHat = Mathf.Clamp(PlayerPrefs.GetInt(hatPref, -1),
            -1, Mathf.Max(-1, HatCount - 1));
    }

    private void OnEnable()
    {
        GameLocalization.LanguageChanged += RefreshLanguage;
        BuildStage();
        ShowSelected();
    }

    private void OnDisable()
    {
        GameLocalization.LanguageChanged -= RefreshLanguage;
        ClearStage();
    }

    private void Update()
    {
        if (shownAnimator == null || !shownAnimator.isActiveAndEnabled ||
            !shownAnimator.GetCurrentAnimatorStateInfo(0).IsName("Greet_Start"))
            return;

        AnimatorStateInfo state = shownAnimator.GetCurrentAnimatorStateInfo(0);
        if (state.normalizedTime >= .98f && shownAnimator.HasState(0, idleState))
            shownAnimator.Play(idleState, 0, 0f);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        pressedAt = eventData.position.x;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        float movement = eventData.position.x - pressedAt;

        if (Mathf.Abs(movement) < 55f)
            return;

        if (movement < 0f)
            Next();
        else
            Previous();
    }

    public void Previous()
    {
        Step(-1);
    }

    public void Next()
    {
        Step(1);
    }

    public void PreviousHat()
    {
        StepHat(-1);
    }

    public void NextHat()
    {
        StepHat(1);
    }

    private void StepHat(int direction)
    {
        int slots = HatCount + 1; // one extra slot means "no hat"
        if (slots <= 1)
            return;

        int slot = selectedHat + 1;
        slot = (slot + direction + slots) % slots;
        selectedHat = slot - 1;
        SaveSelection();
        ShowHatOnPreview();
        RefreshLanguage();
        SoundManager.Play(SoundManager.Sound.CharacterChanged);
    }

    private void Step(int direction)
    {
        if (Count <= 0)
            return;

        selected = (selected + direction + Count) % Count;
        SaveSelection();
        ShowSelected();
        SoundManager.Play(SoundManager.Sound.CharacterChanged);
    }

    private void BuildStage()
    {
        if (stage != null)
            return;

        texture = new RenderTexture(512, 640, 24, RenderTextureFormat.ARGB32)
        {
            name = "Character Skin Preview",
            antiAliasing = 2,
            useMipMap = false,
            autoGenerateMips = false,
        };
        texture.Create();

        if (display != null)
            display.texture = texture;
        if (wardrobeDisplay != null)
            wardrobeDisplay.texture = texture;

        // Skinned meshes can be incorrectly culled when their animated bounds
        // are evaluated many thousands of units from the origin. Two hundred
        // units below the kitchen is already outside its camera and keeps the
        // skinning matrices numerically healthy.
        Vector3 origin = new Vector3(0f, -200f, 0f);

        stage = new GameObject("MENU 3D CHARACTER STAGE");
        stage.transform.position = origin;

        GameObject cameraObject = new GameObject("Preview Camera");
        cameraObject.transform.SetParent(stage.transform, false);
        cameraObject.transform.localPosition = new Vector3(0f, 1.65f, 6f);
        cameraObject.transform.LookAt(origin + Vector3.up * 1.65f);

        previewCamera = cameraObject.AddComponent<Camera>();
        previewCamera.clearFlags = CameraClearFlags.SolidColor;
        previewCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        previewCamera.orthographic = true;
        previewCamera.orthographicSize = 2.25f;
        previewCamera.nearClipPlane = .1f;
        previewCamera.farClipPlane = 20f;
        previewCamera.cullingMask = 1 << previewLayer;
        previewCamera.targetTexture = texture;
        previewCamera.allowHDR = true;
        previewCamera.allowMSAA = true;
        previewCamera.enabled = true;

        if (cameraObject.GetComponent<UniversalAdditionalCameraData>() == null)
            cameraObject.AddComponent<UniversalAdditionalCameraData>();

        MakeLight("Key Light", new Vector3(-2.5f, 4f, 3f),
            new Color(1f, .72f, .45f), 5f, 10f);
        MakeLight("Fill Light", new Vector3(2.5f, 2.5f, 2f),
            new Color(.35f, .65f, 1f), 2.5f, 8f);
    }

    private void MakeLight(string name, Vector3 place, Color color,
        float intensity, float range)
    {
        GameObject host = new GameObject(name);
        host.transform.SetParent(stage.transform, false);
        host.transform.localPosition = place;

        Light light = host.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
        light.cullingMask = 1 << previewLayer;
        light.shadows = LightShadows.Soft;
    }

    private void ShowSelected()
    {
        if (Count <= 0 || stage == null)
            return;

        if (shown != null)
        {
            shown.SetActive(false);
            Destroy(shown);
        }

        GameObject model = skinModels[selected];

        if (model == null)
            return;

        shown = Instantiate(model, stage.transform);
        shown.name = "Preview " + NameAt(selected);
        shown.transform.localPosition = Vector3.zero;
        // The menu camera is straight-on. A slight lean towards it reveals
        // the top plane of the capsule feet instead of flattening them into
        // two vertical blocks. This affects only the off-screen preview copy.
        shown.transform.localRotation = Quaternion.Euler(previewLean, 0f, 0f);
        shown.transform.localScale = Vector3.one;
        SetLayer(shown.transform, previewLayer);

        Animator animator = shown.GetComponent<Animator>();
        if (animator == null)
            animator = shown.AddComponent<Animator>();

        if (sharedAvatar != null)
            animator.avatar = sharedAvatar;
        if (previewController != null)
            animator.runtimeAnimatorController = previewController;

        animator.applyRootMotion = false;
        animator.speed = 1f;
        animator.updateMode = AnimatorUpdateMode.UnscaledTime;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.Rebind();
        animator.Update(0f);

        if (animator.runtimeAnimatorController != null)
        {
            if (animator.HasState(0, greetingState))
                animator.Play(greetingState, 0, 0f);
            else if (animator.HasState(0, idleState))
                animator.Play(idleState, 0, 0f);
        }

        shownAnimator = animator;

        if (preparing != null)
            StopCoroutine(preparing);
        preparing = StartCoroutine(PrepareModel(shown, animator));

        RefreshLanguage();
    }

    public void RefreshLanguage()
    {
        if (Count <= 0)
            return;

        string animal = NameAt(selected);
        string translated = GameLocalization.Get(
            "animal." + animal.ToLowerInvariant(), animal.ToUpperInvariant());

        if (skinName != null)
            skinName.text = translated;
        if (wardrobeSkinName != null)
            wardrobeSkinName.text = translated;

        if (hatName != null)
        {
            hatName.text = selectedHat < 0
                ? GameLocalization.Get("hat.none", "NO HAT")
                : HatNameAt(selectedHat);
        }
    }

    private IEnumerator PrepareModel(GameObject model, Animator animator)
    {
        // SkinnedMeshRenderer.bounds is not trustworthy on the frame a model
        // is instantiated. Waiting one rendered frame prevents a zero/old
        // bound from throwing the character outside the preview camera.
        yield return null;

        if (model == null || model != shown)
            yield break;

        animator.Update(0f);
        Fit(model, animator);
        ShowHatOnPreview();
        preparing = null;
    }

    private string NameAt(int index)
    {
        if (skinNames != null && index >= 0 && index < skinNames.Length &&
            !string.IsNullOrEmpty(skinNames[index]))
            return skinNames[index];

        return skinModels[index] == null ? "Skin" : skinModels[index].name;
    }

    private static void SetLayer(Transform root, int layer)
    {
        root.gameObject.layer = layer;

        foreach (Transform child in root)
            SetLayer(child, layer);
    }

    // How far the head BONE stands above the feet in the wardrobe, and where
    // those feet are. Chosen to reproduce the framing the old whole-silhouette
    // numbers (3.45 tall, centred at 1.65) gave a flat-headed animal.
    private const float bodyHeight = 1.96f;
    private const float floorLift = -.075f;

    // Fallback share of the silhouette the head bone sits at, for a model with
    // no Humanoid head to measure against.
    private const float bodyShare = .57f;

    // Every animal used to be scaled so its whole SILHOUETTE was 3.45 tall --
    // ears, antlers and tails counted. A rabbit spends a third of that budget
    // on ears and gets a small body; a pug has almost nothing above the skull
    // and fills the frame. That is the whole reason the wardrobe showed fifteen
    // different sizes.
    //
    // All fifteen share one skeleton, so the head bone is the one landmark that
    // does not care what is stuck above it. Normalising feet-to-head-bone makes
    // every BODY the same size and lets ears and tails rise past the frame,
    // which is what they are supposed to do.
    // Public because the co-op mate slot stands the other player's animal on
    // the same line as ours. Two lineups measured by two different sums is one
    // rabbit a head taller than the other for no reason anybody could see
    public static void Fit(GameObject model, Animator animator)
    {
        if (!TryVisualBounds(model.transform, out Bounds bounds))
            return;

        Transform head = animator != null && animator.isHuman
            ? animator.GetBoneTransform(HumanBodyBones.Head)
            : null;

        float measure = head != null
            ? head.position.y - bounds.min.y
            : bounds.size.y * bodyShare;

        if (measure > .0001f)
            model.transform.localScale *= bodyHeight / measure;

        if (!TryVisualBounds(model.transform, out bounds))
            return;

        // Feet on a fixed line rather than the silhouette centred. A lineup
        // reads as one lineup when everybody stands on the same floor -- with
        // the old centring, an animal with antlers was also pushed downwards.
        Vector3 stand = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        model.transform.position += model.transform.parent.position +
                                    Vector3.up * floorLift - stand;
    }

    private void ClearStage()
    {
        shown = null;
        shownAnimator = null;
        previewCamera = null;

        if (preparing != null)
            StopCoroutine(preparing);
        preparing = null;

        if (stage != null)
            Destroy(stage);
        stage = null;

        if (display != null)
            display.texture = null;
        if (wardrobeDisplay != null)
            wardrobeDisplay.texture = null;

        if (texture != null)
        {
            texture.Release();
            Destroy(texture);
        }
        texture = null;

    }

    // Called immediately before the kitchen starts or resumes. The first
    // squirrel selection leaves the authored, hand-tuned body untouched. After
    // another skin has actually been installed, choosing squirrel again is a
    // real replacement just like every other selection.
    public void ApplyToPlayer()
    {
        if (selected < 0 || selected >= Count || skinModels[selected] == null)
            return;

        PlayerAnimator player = Object.FindFirstObjectByType<PlayerAnimator>(
            FindObjectsInactive.Include);

        if (player == null || player.CurrentAnimator == null)
            return;

        if (selected == appliedSelection)
        {
            EquipHat(player.CurrentAnimator, player.CurrentAnimator.gameObject);
            return;
        }

        Animator oldAnimator = player.CurrentAnimator;
        Transform oldBody = oldAnimator.transform;
        Transform playerRoot = player.transform;
        Transform host = oldBody.parent == null ? playerRoot : oldBody.parent;

        Vector3 place = oldBody.localPosition;
        Quaternion turn = oldBody.localRotation;
        Vector3 size = oldBody.localScale;
        bool hadOldBounds = TryVisualBounds(oldBody, out Bounds oldBounds);
        RuntimeAnimatorController controller = oldAnimator.runtimeAnimatorController;

        Plateau plateau = player.GetComponentInChildren<Plateau>(true);
        Transform carried = null;
        Transform oldHand = null;
        Vector3 carryPlace = Vector3.zero;
        Quaternion carryTurn = Quaternion.identity;
        Vector3 carrySize = Vector3.one;

        if (plateau != null)
        {
            PlateauLevel.StabiliseFingerMount(plateau.transform);
            oldHand = CarryingHand(oldAnimator, plateau.transform);
            carried = AttachmentRoot(oldHand, plateau.transform);

            if (carried != null)
            {
                carryPlace = carried.localPosition;
                carryTurn = carried.localRotation;
                carrySize = carried.localScale;
                carried.SetParent(playerRoot, true);
            }
        }

        GameObject freshBody = Instantiate(skinModels[selected], host);
        freshBody.name = "Body";
        freshBody.transform.localPosition = place;
        freshBody.transform.localRotation = turn;
        freshBody.transform.localScale = size;

        Animator fresh = freshBody.GetComponent<Animator>();
        if (fresh == null)
            fresh = freshBody.AddComponent<Animator>();

        if (sharedAvatar != null)
            fresh.avatar = sharedAvatar;
        fresh.runtimeAnimatorController = controller;
        fresh.applyRootMotion = false;
        fresh.Rebind();
        fresh.Update(0f);

        // Prefab scale 1 does not mean equal drawn size: the capsule animals'
        // meshes have different authored dimensions. Match what the previous
        // Player actually occupied in the world so changing skin never makes
        // the chef suddenly tiny (or huge).
        if (hadOldBounds && TryVisualBounds(freshBody.transform,
                out Bounds freshBounds) && freshBounds.size.y > .0001f)
        {
            float factor = Mathf.Clamp(oldBounds.size.y / freshBounds.size.y,
                .65f, 1.65f);
            freshBody.transform.localScale *= factor;
        }

        if (carried != null)
        {
            bool right = oldHand == oldAnimator.GetBoneTransform(HumanBodyBones.RightHand);
            Transform newHand = fresh.GetBoneTransform(right
                ? HumanBodyBones.RightHand
                : HumanBodyBones.LeftHand);

            if (newHand != null)
            {
                carried.SetParent(newHand, false);
                carried.localPosition = carryPlace;
                carried.localRotation = carryTurn;
                carried.localScale = carrySize;

                if (plateau != null && plateau.TryGetComponent(out PlateauLevel level))
                    level.RebindCharacter(fresh.transform);
            }
        }

        player.ReplaceAnimator(fresh);
        EquipHat(fresh, freshBody);
        oldBody.gameObject.SetActive(false);
        Destroy(oldBody.gameObject);
        appliedSelection = selected;
    }

    public void ConfirmSelection()
    {
        SaveSelection();
        ApplyToPlayer();
    }

    public void ApplySelectedHat(Animator target)
    {
        if (target != null)
            EquipHat(target, target.gameObject);
    }

    private void SaveSelection()
    {
        PlayerPrefs.SetInt(skinPref, selected);
        PlayerPrefs.SetInt(hatPref, selectedHat);
        PlayerPrefs.Save();
    }

    private void ShowHatOnPreview()
    {
        if (shown == null || shownAnimator == null)
            return;

        EquipHat(shownAnimator, shown);
    }

    // The wardrobe is the only thing that knows which animal a body actually
    // is: in game the object is called "Body" and in the menu "Preview Cat".
    // So it is the only thing that can key the per-animal fit table.
    public string SelectedAnimal => Count <= 0 ? null : NameAt(selected);

    // The PREFAB's name, not the label the player reads.
    //
    // HatPowerBook is keyed on it: the index moves the moment a hat is added
    // in the middle of the list, and the label is translated -- neither
    // survives being the thing a power is looked up by.
    public string SelectedHatKey =>
        selectedHat >= 0 && selectedHat < HatCount && hatModels[selectedHat] != null
            ? hatModels[selectedHat].name
            : null;

    // For the tuner. Puts the current numbers back on screen without waiting
    // for the player to change their selection.
    public void RefreshHats()
    {
        ShowHatOnPreview();

        PlayerAnimator player = FindFirstObjectByType<PlayerAnimator>(
            FindObjectsInactive.Include);

        if (player != null && player.CurrentAnimator != null)
            ApplySelectedHat(player.CurrentAnimator);
    }

    private void EquipHat(Animator animator, GameObject character)
    {
        GameObject prefab = selectedHat >= 0 && selectedHat < HatCount
            ? hatModels[selectedHat]
            : null;
        PlayerHatFitter.Equip(animator, character, prefab, SelectedAnimal);
    }

    private string HatNameAt(int index)
    {
        if (hatNames != null && index >= 0 && index < hatNames.Length &&
            !string.IsNullOrWhiteSpace(hatNames[index]))
            return hatNames[index].ToUpperInvariant();

        return hatModels[index] == null ? "HAT" : hatModels[index].name.ToUpperInvariant();
    }

    // Inactive renderers included. The preview model is measured while the
    // panel that shows it may still be switched off, and an active-only sweep
    // comes back empty there.
    private static bool TryVisualBounds(Transform root, out Bounds bounds)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bounds = default;
        bool found = false;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || BelongsToManagedHat(renderer.transform) ||
                renderer.GetComponentInParent<Plateau>(true) != null)
                continue;

            if (!found)
            {
                bounds = renderer.bounds;
                found = true;
            }
            else
                bounds.Encapsulate(renderer.bounds);
        }

        return found;
    }

    private static bool BelongsToManagedHat(Transform item)
    {
        while (item != null)
        {
            if (item.name == PlayerHatFitter.MountedName)
                return true;
            item = item.parent;
        }
        return false;
    }

    private static Transform CarryingHand(Animator animator, Transform item)
    {
        Transform right = animator.GetBoneTransform(HumanBodyBones.RightHand);
        Transform left = animator.GetBoneTransform(HumanBodyBones.LeftHand);

        if (right != null && item.IsChildOf(right))
            return right;
        if (left != null && item.IsChildOf(left))
            return left;

        if (right == null)
            return left;
        if (left == null)
            return right;

        return (item.position - right.position).sqrMagnitude <=
               (item.position - left.position).sqrMagnitude ? right : left;
    }

    private static Transform AttachmentRoot(Transform hand, Transform item)
    {
        if (hand == null || item == null)
            return null;

        Transform walk = item;

        while (walk.parent != null && walk.parent != hand)
            walk = walk.parent;

        return walk.parent == hand ? walk : item;
    }
}

#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Every clip in the project that could drive a capsule animal, in one list, one
// click each.
//
// The two menu commands next door answer "is this one any good". This answers
// the question that comes straight after it -- what else is there -- and that
// one cannot be answered by a menu item per clip, because there are a hundred
// and three of them in the waiter pack alone.
//
// It writes nothing and decides nothing. Picking a clip here puts a model in
// the scene playing it, exactly as command 1 does; the controllers are still
// built by command 2 from the choices written in code. Which is the right split:
// this is for looking, and looking should not be able to change anything
public class CapsuleClipBrowser : EditorWindow
{
    [MenuItem("Cooked Fast/Karakter/0 - Klip Tarayici", priority = 699)]
    public static void Open()
    {
        CapsuleClipBrowser window = GetWindow<CapsuleClipBrowser>("Klip Tarayici");

        window.minSize = new Vector2(340f, 400f);
        window.Reload();
    }

    private class Shelf
    {
        public string label;
        public List<AnimationClip> clips = new List<AnimationClip>();
        public bool open = true;
    }

    private readonly List<Shelf> shelves = new List<Shelf>();

    private int animal = 12;
    private string filter = "";

    // Kept in EditorPrefs rather than in the window, because the window is
    // rebuilt every time the editor recompiles and losing a placement you spent
    // ten minutes dialling in to a stray script save is its own small tragedy
    private CapsuleCharacterSetup.TraySetup tray;
    private bool traySettings;

    private const string prefix = "CookedFast.CapsuleTray.";

    private void Load()
    {
        tray.on = EditorPrefs.GetBool(prefix + "on", true);
        tray.right = EditorPrefs.GetBool(prefix + "right", true);
        tray.size = EditorPrefs.GetFloat(prefix + "size", 0f);

        tray.place = new Vector3(
            EditorPrefs.GetFloat(prefix + "px", 0f),
            EditorPrefs.GetFloat(prefix + "py", 0f),
            EditorPrefs.GetFloat(prefix + "pz", 0f));

        tray.turn = new Vector3(
            EditorPrefs.GetFloat(prefix + "rx", 0f),
            EditorPrefs.GetFloat(prefix + "ry", 0f),
            EditorPrefs.GetFloat(prefix + "rz", 0f));
    }

    private void Save()
    {
        EditorPrefs.SetBool(prefix + "on", tray.on);
        EditorPrefs.SetBool(prefix + "right", tray.right);
        EditorPrefs.SetFloat(prefix + "size", tray.size);

        EditorPrefs.SetFloat(prefix + "px", tray.place.x);
        EditorPrefs.SetFloat(prefix + "py", tray.place.y);
        EditorPrefs.SetFloat(prefix + "pz", tray.place.z);

        EditorPrefs.SetFloat(prefix + "rx", tray.turn.x);
        EditorPrefs.SetFloat(prefix + "ry", tray.turn.y);
        EditorPrefs.SetFloat(prefix + "rz", tray.turn.z);
    }
    private Vector2 scroll;
    private AnimationClip playing;
    private int total;

    // What the last placement actually did -- which bone, what width, what the
    // bone's own scale was. Shown rather than logged, because the question it
    // answers ("where did the tray go") is asked while looking at this window
    private string trayStatus = "";

    // One call, from everywhere that can change where the tray sits.
    //
    // Everything here is a transform write on an object that already exists, so
    // it is cheap enough to run on every nudge of a Vector3 field. It also
    // rebuilds the tray if it is not there any more, which is why the toolbar's
    // Yenile calls it: whatever removes it, the next thing you touch puts it back
    private void Apply()
    {
        trayStatus = CapsuleCharacterSetup.Retray(tray);
    }

    // The socket wins, always.
    //
    // Two things can move the tray: these fields, and dragging the socket in
    // the Scene view. Only one of them can be the truth, and it has to be the
    // scene -- the fields are a view of an object that exists, not the other
    // way round. So before anything here is drawn or written through, whatever
    // the socket says gets pulled back into the numbers.
    //
    // This is what the earlier version got wrong. It let the two drift and then
    // tried to paper over the drift with a warning box, which put the job of
    // remembering which one was newer on the person using the window. Nobody
    // should have to hold that. Kept in step, they cannot disagree
    private void Sync()
    {
        if (!CapsuleCharacterSetup.Moved(tray))
            return;

        if (CapsuleCharacterSetup.ReadBack(ref tray, out _))
            Save();
    }

    private void OnEnable()
    {
        Load();

        if (shelves.Count <= 0)
            Reload();
    }

    // Read off disk rather than kept in a list somewhere.
    //
    // The waiter pack is a folder of FBXs and each FBX carries its clip as a
    // sub asset under a name that is not the file's name, so the only honest
    // way to know what is in there is to open all of them and look. Done once,
    // on demand, because a hundred and three FBXs is not a per frame job
    private void Reload()
    {
        shelves.Clear();
        total = 0;

        foreach (string[] library in CapsuleCharacterSetup.Libraries)
        {
            Shelf shelf = new Shelf { label = library[0] };

            foreach (string file in Files(library[1]))
            {
                foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(file))
                {
                    if (asset is not AnimationClip clip)
                        continue;

                    // Unity's own scrubbing clip, one per imported model. It is
                    // not an animation anybody made and it plays as a T-pose
                    if (clip.name.StartsWith("__preview__"))
                        continue;

                    shelf.clips.Add(clip);
                }
            }

            shelf.clips.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

            total += shelf.clips.Count;

            if (shelf.clips.Count > 0)
                shelves.Add(shelf);
        }
    }

    // FBXs and loose .anim files alike.
    //
    // It was FBX only, which was true of every library here until a weapon pack
    // turned up whose clips are authored as .anim assets sitting next to a
    // controller. A shelf that silently comes back empty because of the
    // extension it was written for is worse than no shelf.
    private static IEnumerable<string> Files(string path)
    {
        if (File.Exists(path))
            return new[] { path };

        if (!Directory.Exists(path))
            return new string[0];

        List<string> found = new List<string>(
            Directory.GetFiles(path, "*.fbx", SearchOption.AllDirectories));

        found.AddRange(
            Directory.GetFiles(path, "*.anim", SearchOption.AllDirectories));

        return found;
    }

    private void OnGUI()
    {
        Sync();

        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            animal = EditorGUILayout.Popup(animal, CapsuleCharacterSetup.Animals,
                EditorStyles.toolbarPopup, GUILayout.Width(90f));

            EditorGUI.BeginChangeCheck();

            // On by default, because the carry clips are the ones in question
            // and an empty hand does not let you judge one. It costs nothing on
            // the clips that are not about carrying -- the tray just rides along
            tray.on = GUILayout.Toggle(tray.on, "Plateau", EditorStyles.toolbarButton,
                GUILayout.Width(58f));

            // Which hand belongs to the ANIMATION, not to the character.
            // Waiter_Tray_* was authored holding the tray in one particular
            // hand and nothing in a clip file says which, so it is a button
            tray.right = GUILayout.Toggle(tray.right, tray.right ? "Sag el" : "Sol el",
                EditorStyles.toolbarButton, GUILayout.Width(50f));

            // Both of these move the tray on the model standing in the scene
            // right now. Swapping hands used to mean re-clicking a clip, which
            // restarted the animation -- so you never saw the same pose in the
            // other hand, which is the entire comparison being made
            if (EditorGUI.EndChangeCheck())
            {
                Save();
                Apply();
            }

            traySettings = GUILayout.Toggle(traySettings, "Ayar",
                EditorStyles.toolbarButton, GUILayout.Width(40f));

            filter = GUILayout.TextField(filter, EditorStyles.toolbarSearchField);

            // Rescans the clip list, and puts the tray back only if it is
            // missing. It used to re-place the tray unconditionally, which
            // meant a button whose job is "reread the folders" could undo an
            // afternoon of placing -- the widest possible gap between what a
            // button says and what it does
            if (GUILayout.Button("Yenile", EditorStyles.toolbarButton, GUILayout.Width(52f)))
            {
                Reload();

                trayStatus = CapsuleCharacterSetup.Restore(tray);
            }

            if (GUILayout.Button("Sil", EditorStyles.toolbarButton, GUILayout.Width(36f)))
            {
                CapsuleCharacterSetup.Clear();
                playing = null;
                trayStatus = "";
            }
        }

        EditorGUILayout.HelpBox(
            total + " klip. Bir tanesine tikla, secili hayvan sahnede onu oynasin.\n" +
            "SCENE penceresinden bak -- model mutfagin yanina konuluyor.",
            MessageType.None);

        if (traySettings)
            TrayPanel();

        if (EditorApplication.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "Play modunda calismaz -- olusan obje Play bitince kaybolur.",
                MessageType.Warning);
        }

        scroll = EditorGUILayout.BeginScrollView(scroll);

        foreach (Shelf shelf in shelves)
        {
            List<AnimationClip> shown = Matching(shelf);

            if (shown.Count <= 0)
                continue;

            shelf.open = EditorGUILayout.Foldout(
                shelf.open, shelf.label + "  (" + shown.Count + ")", true);

            if (!shelf.open)
                continue;

            foreach (AnimationClip clip in shown)
                Row(clip);
        }

        EditorGUILayout.EndScrollView();

        if (playing != null)
        {
            EditorGUILayout.LabelField("Oynayan: " + playing.name, EditorStyles.miniBoldLabel);

            // Said here rather than in the list, because it is a property of
            // the clip that only matters once you are looking at one: a clip
            // that does not loop plays through and then stands still, which
            // reads as the preview having broken
            EditorGUILayout.LabelField(
                "  " + playing.length.ToString("0.00") + " sn" +
                (playing.isLooping ? ", donguluyor" : ", DONGUSUZ -- bir kere oynar"),
                EditorStyles.miniLabel);
        }
    }

    // Where the tray sits, dialled by the person who can see it.
    //
    // Every value here is relative to the HAND BONE, which is why the numbers
    // are small and why they survive a change of animal -- all fifteen share
    // one skeleton, so a placement that works on the rabbit works on the bear.
    //
    // Applied live. Drag a value and the tray moves.
    //
    // The first version rebuilt the whole preview instead, which meant a number
    // did nothing until you clicked a clip again -- and clicking a clip throws
    // away the model, writes a controller to disk and restarts the animation,
    // so you were re-aiming at a pose that had already moved on. The thing being
    // asked for was never a rebuild. It was a transform write, and those are free
    private void TrayPanel()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUI.BeginChangeCheck();

            tray.place = EditorGUILayout.Vector3Field("Konum (kemige gore)", tray.place);
            tray.turn = EditorGUILayout.Vector3Field("Aci", tray.turn);

            tray.size = EditorGUILayout.FloatField(
                new GUIContent("Genislik", "Dunya birimi. 0 = karakterin boyuna gore hesapla"),
                tray.size);

            if (EditorGUI.EndChangeCheck())
            {
                Save();
                Apply();
            }

            // Typing at it is the fallback, not the method.
            //
            // The socket is a normal GameObject sitting in the hand, so the
            // right way to place it is to select it and use W and E like any
            // other object in the scene -- with the animation running, from
            // whatever angle answers the question. The fields above follow it
            // on their own, so there is nothing to press afterwards
            if (GUILayout.Button("Soketi sec (sahnede surukle)"))
            {
                if (!CapsuleCharacterSetup.SelectSocket(out string trouble))
                    trayStatus = trouble;
                else if (SceneView.lastActiveSceneView != null)
                    SceneView.lastActiveSceneView.Focus();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Sifirla"))
                {
                    tray.place = Vector3.zero;
                    tray.turn = Vector3.zero;
                    tray.size = 0f;
                    Save();
                    Apply();
                }

                // The point of the whole panel. Once it looks right, the
                // numbers have to get out of EditorPrefs and into the code that
                // builds the real prefabs -- and the way that happens in this
                // project is somebody pastes them to me
                if (GUILayout.Button("Degerleri kopyala"))
                {
                    EditorGUIUtility.systemCopyBuffer =
                        "el   : " + (tray.right ? "Sag" : "Sol") + "\n" +
                        "konum: " + tray.place.ToString("0.0000") + "\n" +
                        "aci  : " + tray.turn.ToString("0.0") + "\n" +
                        "genis: " + tray.size.ToString("0.0000");

                    Debug.Log("[Plateau ayari]\n" + EditorGUIUtility.systemCopyBuffer);
                }
            }

            // The tray's own account of itself. If it is not on screen this is
            // the line that says whether it was never made, made on the wrong
            // bone, or made a thousandth of the size it should be
            EditorGUILayout.LabelField(
                string.IsNullOrEmpty(trayStatus) ? "Tepsi: -" : "Tepsi: " + trayStatus,
                EditorStyles.miniLabel);
        }
    }

    private List<AnimationClip> Matching(Shelf shelf)
    {
        if (string.IsNullOrEmpty(filter))
            return shelf.clips;

        List<AnimationClip> shown = new List<AnimationClip>();

        foreach (AnimationClip clip in shelf.clips)
        {
            if (clip.name.ToLowerInvariant().Contains(filter.ToLowerInvariant()))
                shown.Add(clip);
        }

        return shown;
    }

    private void Row(AnimationClip clip)
    {
        bool current = clip == playing;

        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Space(12f);

            // The clip's own name, not the file's. They differ everywhere here
            // -- the Hypercasual ones still carry their whole Mixamo path -- and
            // the name in the controller will be this one
            if (GUILayout.Button(Short(clip.name),
                    current ? EditorStyles.miniButtonMid : EditorStyles.miniButton))
            {
                if (CapsuleCharacterSetup.Preview(
                        CapsuleCharacterSetup.Animals[animal], clip, tray, out string trouble))
                {
                    playing = clip;

                    // Re-placed rather than trusted. Preview already built the
                    // tray; this asks it where it ended up, so the readout in
                    // the panel is about the model actually standing there
                    Apply();
                }
                else
                {
                    EditorUtility.DisplayDialog("Klip Tarayici", trouble, "Tamam");
                }
            }

            // Straight to the FBX, for when the answer is "this one, but the
            // import settings are wrong"
            if (GUILayout.Button("...", EditorStyles.miniButton, GUILayout.Width(24f)))
                EditorGUIUtility.PingObject(clip);
        }
    }

    // Mixamo names are the whole armature path and they do not fit in a panel
    private static string Short(string name)
    {
        string[] parts = name.Split('|');

        return parts[parts.Length - 1];
    }
}
#endif

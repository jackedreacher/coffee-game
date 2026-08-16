using System.Collections.Generic;
using UnityEngine;

// A wheel of cheese rather than a pile of it.
//
// The ordinary station keeps a plateau topped up: it drops one item on every
// tick until the tray is full, so what you see is a stack that grows back a
// slice at a time. Cheese is meant to read the other way round -- a whole
// cheese sitting there, losing a wedge each time someone takes one, and only
// once the last wedge is gone does a fresh one appear.
//
// So the plateau is not used at all here. The pieces are laid out in a ring
// under a centre transform, and the inherited spawn timer is reused to mean
// "check whether the wheel needs rebuilding" instead of "drop another item"
public class CheeseWheelStation : FoodSpawnerStation
{
    [Header(" Wheel ")]
    [SerializeField] private Transform wheelCentre;

    [Tooltip("Bir cemberde kac parca olacak")]
    [SerializeField][Range(1, 16)] private int pieceCount = 6;

    // Zero is the right starting point for a wedge, not a mistake. A wedge is
    // already a slice of the wheel: leave every one of them on the centre and
    // just turn each a step further, and the slices close up into a round
    // cheese. Pushing them out along their own facing only opens the gaps
    [Tooltip("Parcalarin merkezden disari kaymasi. Kama seklinde mesh icin 0 dogru deger")]
    [SerializeField] private float radius;

    [Tooltip("Parcalar disari mi baksin, yoksa hepsi ayni yone mi")]
    [SerializeField] private bool faceOutwards = true;

    [Tooltip("Her turda parcalari biraz yukari basamakla")]
    [SerializeField] private float risePerPiece;

    // What is still on the wheel. Popping takes from the end so the ring empties
    // in one direction and the gap reads as a slice taken out, not as holes
    private readonly List<SpawnableFood> pieces = new List<SpawnableFood>();

    public int PiecesLeft => pieces.Count;

    // Read by Interactable when it works out what to bounce on a tap. It has to
    // ask rather than search, because at the moment it asks this transform is
    // empty -- the ring is not built until Start, one frame later -- and
    // anything looking for a renderer here finds nothing and walks away
    public Transform WheelCentre => wheelCentre;

    private void Start()
    {
        DropLeftoverPreviews();

        // Full from the first frame. Waiting a whole spawnDelay before the first
        // cheese appears makes the station look broken on entering play mode
        Refill();
    }

    // An editor preview piece saved into the scene would sit on the wheel for the
    // whole game: never taken, never counted, and keeping the wheel from ever
    // looking empty. Forgetting to remove one should cost nothing
    private void DropLeftoverPreviews()
    {
        if (wheelCentre == null)
            return;

        for (int i = wheelCentre.childCount - 1; i >= 0; i--)
        {
            Transform child = wheelCentre.GetChild(i);

            if (child.name.StartsWith("PREVIEW"))
                Destroy(child.gameObject);
        }
    }

    protected override void TrySpawnFood()
    {
        Forget();

        if (pieces.Count > 0)
            return;

        Refill();
    }

    // A piece that was taken has been reparented onto the carrier's tray, and one
    // that was served is destroyed somewhere down the line. Either way it is no
    // longer on the wheel, and a stale entry would stop the wheel ever counting
    // as empty -- which is the one condition that brings the next cheese
    private void Forget()
    {
        for (int i = pieces.Count - 1; i >= 0; i--)
        {
            if (pieces[i] == null || pieces[i].transform.parent != wheelCentre)
                pieces.RemoveAt(i);
        }
    }

    private void Refill()
    {
        if (SpawnableFoodPrefab == null || wheelCentre == null)
            return;

        Forget();

        for (int i = pieces.Count; i < pieceCount; i++)
        {
            // The false matters: the Transform overload preserves world scale by
            // default, which would divide the prefab's scale by the station's and
            // hand out cheese at the wrong size
            SpawnableFood piece = Instantiate(SpawnableFoodPrefab, wheelCentre, false);

            pieces.Add(piece);
        }

        Arrange();
    }

    private void Arrange()
    {
        for (int i = 0; i < pieces.Count; i++)
        {
            if (pieces[i] != null)
                Place(pieces[i].transform, i, pieceCount, radius, faceOutwards, risePerPiece);
        }
    }

    // Static, and taking every number as an argument, so the editor preview can
    // lay out a ring in edit mode with exactly this arithmetic. Two copies of it
    // would drift apart and the preview would stop being worth looking at
    public static void Place(Transform piece, int index, int count,
        float radius, bool faceOutwards, float risePerPiece)
    {
        Quaternion turn = Quaternion.Euler(0f, index * 360f / Mathf.Max(1, count), 0f);

        piece.localPosition = turn * (Vector3.forward * radius) + Vector3.up * (index * risePerPiece);
        piece.localRotation = faceOutwards ? turn : Quaternion.identity;
    }

    // Dragging the radius slider while the game runs rearranges what is already
    // on the wheel, instead of only showing up on the next refill
    private void OnValidate()
    {
        if (Application.isPlaying)
            Arrange();
    }

    public override SpawnableFood Pop()
    {
        Forget();

        if (pieces.Count <= 0)
            return null;

        int last = pieces.Count - 1;
        SpawnableFood piece = pieces[last];

        pieces.RemoveAt(last);

        return piece;
    }
}

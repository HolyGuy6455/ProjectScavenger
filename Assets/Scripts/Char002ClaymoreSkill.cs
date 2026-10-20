using Unity.Netcode;
using UnityEngine;

/// <summary>Plays the authored placement motion and requests one mine at its animation event.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterAnimationPresenter))]
public sealed class Char002ClaymoreSkill : MonoBehaviour
{
    [SerializeField] private NetworkObject minePrefab;
    [SerializeField] private Transform placementPoint;
    [SerializeField] private Renderer carriedMine;

    private int animationToken;
    private bool placementRequested;
    private PlayerController Player => GetComponentInParent<PlayerController>();
    private CharacterAnimationPresenter Presenter => GetComponent<CharacterAnimationPresenter>();
    internal int AnimationToken => animationToken;
    public bool IsPlaying => animationToken != 0 && Presenter.IsCurrentPlayback(animationToken);

    private void OnEnable()
    {
        if (Player != null) Player.PresentationInterrupted += Cancel;
    }

    private void OnDisable()
    {
        if (Player != null) Player.PresentationInterrupted -= Cancel;
        Cancel();
    }

    public bool TryPlay(float duration)
    {
        var player = Player;
        if (!isActiveAndEnabled || player == null || player.IsInputLocked || player.IsRolling
            || Presenter.IsFullBodyPlaying || Presenter.IsDeathPresented) return false;
        if (minePrefab == null || placementPoint == null || carriedMine == null)
        {
            Debug.LogError("[Char002ClaymoreSkill] Mine prefab, placement point and carried mesh must be assigned.", this);
            return false;
        }

        animationToken = Presenter.PlaySkill(duration);
        if (animationToken == 0)
        {
            Debug.LogError("[Char002ClaymoreSkill] Cannot play the Base Layer Skill state.", this);
            return false;
        }
        placementRequested = false;
        carriedMine.enabled = true;
        player.SetCinematicInputLocked(this, true);
        return true;
    }

    // AnimationEvent on Char002_skill at the authored release frame (84).
    public void PlaceClaymore()
    {
        if (!IsPlaying || placementRequested) return;
        placementRequested = true;
        carriedMine.enabled = false;
        GetComponentInParent<Char002ClaymoreSkillNetwork>()?.OnPlacementAnimationEvent();
    }

    public NetworkObject SpawnMineOnServer(int ammoId)
    {
        var network = GetComponentInParent<Char002ClaymoreSkillNetwork>();
        if (network == null || !network.IsServer || !network.IsSpawned) return null;
        if (minePrefab == null || placementPoint == null)
        {
            Debug.LogError("[Char002ClaymoreSkill] Cannot place a mine without its prefab and placement point.", this);
            return null;
        }
        var ammo = DataManager.Instance != null ? DataManager.Instance.GetData<AmmoData>("Ammo", ammoId) : null;
        if (ammo == null || minePrefab.GetComponent<ClaymoreMine>() == null)
        {
            Debug.LogError($"[Char002ClaymoreSkill] Missing ClaymoreMine component or AmmoData {ammoId}.", this);
            return null;
        }
        // This root-relative point also works on a dedicated server that does not animate the hand.
        var mine = Instantiate(minePrefab, placementPoint.position, placementPoint.rotation);
        mine.transform.localScale = Vector3.Scale(minePrefab.transform.localScale, placementPoint.lossyScale);
        mine.Spawn(true);
        mine.GetComponent<ClaymoreMine>().Arm(ammo);
        return mine;
    }

    internal void OnSkillStateExit(int token)
    {
        // A cancelled state can exit while a replacement playback is entering.
        if (token == 0 || token != animationToken) return;
        if (!IsPlaying)
        {
            Cancel();
            return;
        }

        // The Animator's exit-time transition has already returned the base layer to Movement.
        Presenter.FinishSkill(animationToken);
        animationToken = 0;
        if (carriedMine != null) carriedMine.enabled = true;
        if (Player != null) Player.SetCinematicInputLocked(this, false);
    }

    public void Cancel()
    {
        int token = animationToken;
        animationToken = 0;
        if (token != 0)
        {
            GetComponentInParent<Char002ClaymoreSkillNetwork>()?.CancelPendingPlacement();
            Presenter.CancelFullBody(token, true);
        }
        if (carriedMine != null) carriedMine.enabled = true;
        if (Player != null) Player.SetCinematicInputLocked(this, false);
    }
}

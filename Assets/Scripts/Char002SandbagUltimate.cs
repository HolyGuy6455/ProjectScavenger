using Unity.Netcode;
using UnityEngine;

/// <summary>블루프린트가 착지하면 같은 자리에 실제 모래주머니 엄폐물을 설치합니다.</summary>
public sealed class Char002SandbagUltimate : BaseUltimateSkill
{
    [SerializeField] private NetworkObject sandbagPrefab;
    [Tooltip("우측 후방, 좌측 후방, 전방 순서. 애니메이션과 무관한 루트 기준 설치점입니다.")]
    [SerializeField] private Transform[] placementPoints;
    [SerializeField] private Renderer[] animatedSandbags;

    private int pendingPlacements;

    private void OnEnable()
    {
        var skills = GetComponentInParent<PlayerSkillController>();
        if (skills != null) skills.ActiveActionsCancelled += ClearPendingPlacements;
    }

    private void ClearPendingPlacements() => pendingPlacements = 0;

    public override void OnServerBegin(SkillData data)
    {
        base.OnServerBegin(data);
        pendingPlacements = 0b111;
    }

    public override void OnClientExecute(SkillData data, bool isOwner)
    {
        if (animatedSandbags == null || animatedSandbags.Length != 3
            || System.Array.Exists(animatedSandbags, item => item == null))
        {
            Debug.LogError("[Char002SandbagUltimate] 연출용 모래주머니 메쉬 3개를 연결하세요.", this);
            return;
        }
        int previousVersion = PresentationVersion;
        base.OnClientExecute(data, isOwner);
        if (!IsPresentationPlaying || PresentationVersion == previousVersion) return;
        GetComponentInParent<PlayerController>()?.SetCinematicInputLocked(this, true);
        foreach (var sandbag in animatedSandbags)
            sandbag.forceRenderingOff = false;
    }

    // Char002_ultimate의 우측/좌측/전방 착지 프레임에서 호출합니다.
    public void PlaceSandbag(int index)
    {
        if (!IsPresentationPlaying || index < 0 || index >= 3) return;
        // Animator가 재질과 Transform을 계속 평가해도 연출용 메쉬가 다시 보이지 않습니다.
        animatedSandbags[index].forceRenderingOff = true;
        TriggerAnimationEvent(index);
    }

    public override void OnServerExecute(SkillData data, int eventId)
    {
        var player = GetComponentInParent<PlayerController>();
        if (player == null || !player.IsServer || !player.IsSpawned || eventId < 0 || eventId >= 3) return;
        int bit = 1 << eventId;
        if ((pendingPlacements & bit) == 0) return;
        if (sandbagPrefab == null || placementPoints == null || placementPoints.Length != 3
            || placementPoints[eventId] == null || sandbagPrefab.GetComponent<ObstacleController>() == null
            || data == null || data.funcValue == null || data.funcValue.Length == 0 || data.funcValue[0] <= 0f)
        {
            Debug.LogError("[Char002SandbagUltimate] 모래주머니 Obstacle 프리팹, 설치점 3개와 SkillData의 내구도를 확인하세요.", this);
            return;
        }

        pendingPlacements &= ~bit;
        var point = placementPoints[eventId];
        var obstacle = Instantiate(sandbagPrefab, point.position, point.rotation);
        obstacle.transform.localScale = Vector3.Scale(sandbagPrefab.transform.localScale, point.lossyScale);
        obstacle.Spawn(true);
        obstacle.GetComponent<Health>().SetHealth(data.funcValue[0], data.funcValue[0]);
    }

    protected override void OnUltimateCancelledClient()
    {
        pendingPlacements = 0;
        GetComponentInParent<PlayerController>()?.SetCinematicInputLocked(this, false);
        foreach (var sandbag in animatedSandbags)
            sandbag.forceRenderingOff = true;
    }

    protected override void OnUltimateEndClient()
        => GetComponentInParent<PlayerController>()?.SetCinematicInputLocked(this, false);

    protected override void OnDisable()
    {
        var skills = GetComponentInParent<PlayerSkillController>();
        if (skills != null) skills.ActiveActionsCancelled -= ClearPendingPlacements;
        pendingPlacements = 0;
        base.OnDisable();
        GetComponentInParent<PlayerController>()?.SetCinematicInputLocked(this, false);
    }
}

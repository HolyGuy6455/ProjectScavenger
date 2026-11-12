using System;
using UnityEngine;

/// <summary>
/// 캐릭터 고유 스킬/궁극기(예: char004 투척 조준, char007 오토바이 주행)가
/// PlayerController의 기본 이동·조준·회전 및 액션 제약 일부 또는 전체를
/// 선택적으로 오버라이드할 수 있게 하는 인터페이스입니다.
/// </summary>
public interface ICharacterControlOverride
{
    /// <summary>현재 이 오버라이드가 활성 상태인지 여부입니다.</summary>
    bool IsControlOverrideActive { get; }

    /// <summary>활성 중 캐릭터 교대(1~4번 슬롯 변경)를 차단할지 여부입니다.</summary>
    bool BlocksCharacterSwitch { get; }

    /// <summary>활성 중 구르기(Roll) 실행을 차단할지 여부입니다.</summary>
    bool BlocksRoll { get; }

    /// <summary>활성 중 피격 시 Hit 애니메이션 재생을 억제할지 여부입니다.</summary>
    bool BlocksHitAnimation { get; }

    /// <summary>사망 시 전용 종료 연출(예: 오토바이 하차)이 끝날 때까지 사망 애니메이션 완료 판정을 지연할지 여부입니다.</summary>
    bool IsDeferringDeathAnimation { get; }

    /// <summary>
    /// 이동 물리 및 이동 애니메이션 입력을 오버라이드합니다.
    /// false를 반환하면 PlayerController의 기본 8방향 이동 로직을 그대로 사용합니다.
    /// </summary>
    bool TryOverrideMovement(
        PlayerController player,
        Rigidbody rb,
        Vector2 rawMoveInput,
        float baseMoveSpeed,
        float fixedDeltaTime,
        out Vector3 worldMoveDirection,
        out Vector2 animInput);

    /// <summary>
    /// 우측 스틱 조준·사격 및 캐릭터 회전 물리를 오버라이드합니다.
    /// false를 반환하면 PlayerController의 기본 사격 조준 및 회전 로직을 그대로 사용합니다.
    /// </summary>
    bool TryOverrideAimAndRotation(
        PlayerController player,
        Rigidbody rb,
        Vector3 worldMoveDirection,
        Vector2 rotationInput,
        float baseRotationSpeed,
        float fixedDeltaTime);

    /// <summary>
    /// 구르기(Roll)가 시작되기 직전에 호출됩니다. (비차단 준비 상태 정리 등)
    /// </summary>
    void OnBeforeRoll(PlayerController player);

    /// <summary>
    /// Player 루트 Rigidbody에서 물리 충돌(OnCollisionEnter / OnCollisionStay)이 발생했을 때 호출됩니다.
    /// </summary>
    void OnCharacterCollision(PlayerController player, Collision collision);

    /// <summary>
    /// 캐릭터 사망 시 기본 즉시 중단(InterruptPresentation + PlayDeath) 대신
    /// 전용 종료 시퀀스(예: UltimateEnd 후 Death)를 수행할지 시도합니다.
    /// true를 반환하면 전용 종료 시퀀스가 끝난 뒤 playDefaultDeath 콜백을 호출해야 합니다.
    /// </summary>
    bool TryHandleCustomDeathSequence(PlayerController player, Action playDefaultDeath);
}

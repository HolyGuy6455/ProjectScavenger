char004 연막 / 화염 지대 — 시각 효과 프리팹

프리팹
Assets/Resources/model/Characters/char004/Prefabs/Char004_SmokeArea.prefab
Assets/Resources/model/Characters/char004/Prefabs/Char004_FireArea.prefab

두 프리팹 모두 Transform, ParticleSystem, ParticleSystemRenderer만 사용한다.
피해, 시야 차단, 투척, 네트워크, 지대 수명 관리는 연결하지 않았다.
프리팹을 씬에 놓고 Particle System 미리보기의 Play로 확인할 수 있다.

Inspector 조절
- 전체 크기: 루트 Transform > Scale (Main > Scaling Mode = Hierarchy)
- 발생 반경: Shape > Radius (현재 2). 파티클 자체의 크기만큼 실제 보이는 가장자리는 더 넓다.
- 밀도: Emission > Rate over Time. 처음 등장량은 Bursts > Count.
- 연막 높이: Shape > Scale > Z. Shape가 X축 -90도로 회전되어 Z가 위쪽 높이에 해당한다.
- 구름/불꽃 크기: Main > Start Size.
- 떠오르는 속도: Velocity over Lifetime > Y.
- 불꽃 재생: Texture Sheet Animation > Cycles. 4x4 16프레임, 시작 프레임 랜덤, 프레임 간 보간.

재생
Play On Awake와 Looping이 켜져 있다. 처음에는 투명하게 등장하고 이후 반복된다.
Main > Duration은 반복 구간이며 지대의 게임 내 지속 시간이 아니다.
나중에 ParticleSystem.Stop(true, StopEmitting)으로 방출을 중단하면 남은 입자들이 자연스럽게 사라진다.
현재 최대 잔여 시간: 연막 약 4.6초, 화염 약 1.9초.

연막 바닥 경계
URP Soft Particles 사용. 현재 PC 파이프라인에서 Depth Texture가 켜져 있다.
Mobile 파이프라인은 Depth Texture가 꺼져 있으므로 모바일 적용 시 카메라 또는 파이프라인의 Depth Texture를 켜야 같은 부드러운 경계를 얻는다.
이번 작업에서 카메라/파이프라인/씬 설정은 변경하지 않았다.

생성 이미지
Textures/Char004_Flame_4x4.png — 16프레임 불꽃 시트
Textures/Char004_Smoke_Puff.png — 흰 구름 파티클
이미지는 내장 image_gen으로 생성했다. RGBA 원본을 프로젝트에 보관한다.
Unity에서는 투명도 유지, Bilinear, Clamp, 최대1024, 압축 없음, mipmap 없음으로 사용한다.

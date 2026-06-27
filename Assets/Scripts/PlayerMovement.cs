using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 쿼터뷰 캐릭터 이동 스크립트
/// Unity Input System을 통해 키보드(WASD/화살표), 게임패드(XInput), 안드로이드 가상 조이스틱을 지원합니다.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotationSpeed = 720f;
    
    [Header("Combat")]
    [SerializeField] private Shooter shooter;

    private Rigidbody _rb;
    private PlayerInputActions _inputActions;
    private Vector2 _keyboardInput;
    private Vector2 _joystickInput;
    private Vector2 _joystickRotationInput;
    private bool _isShooting = false;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _inputActions = new PlayerInputActions();
    }

    private void OnEnable()
    {
        _inputActions.Player.Enable();
        _inputActions.Player.Move.performed += OnMove;
        _inputActions.Player.Move.canceled += OnMove;
    }

    private void OnDisable()
    {
        _inputActions.Player.Move.performed -= OnMove;
        _inputActions.Player.Move.canceled -= OnMove;
        _inputActions.Player.Disable();
    }

    private void Start()
    {
        // 임시 테스트 코드: 시작할 때 DataManager에서 gun_002 데이터를 가져와 장착합니다.
        if (shooter != null)
        {
            GunData startingGun = DataManager.Instance.GetData<GunData>("Gun", "gun_002");
            if (startingGun != null)
            {
                shooter.EquipGun(startingGun);
                Debug.Log($"[{gameObject.name}] {startingGun.gunName} 장착 완료!");
            }
            else
            {
                Debug.LogWarning("DataManager에서 gun_002 데이터를 찾을 수 없습니다.");
            }
        }
    }

    private void OnMove(InputAction.CallbackContext ctx)
    {
        _keyboardInput = ctx.ReadValue<Vector2>();
    }

    /// <summary>
    /// 안드로이드 가상 조이스틱 등 외부에서 입력값을 주입할 때 사용합니다.
    /// </summary>
    public void SetMoveInput(Vector2 input)
    {
        _joystickInput = input;
    }

    /// <summary>
    /// 안드로이드 가상 회전 조이스틱 등 외부에서 회전값을 주입할 때 사용합니다.
    /// </summary>
    public void SetRotationInput(Vector2 input)
    {
        _joystickRotationInput = input;
    }

    private void FixedUpdate()
    {
        // 1. 이동 처리
        // 키보드 입력과 조이스틱 입력을 합산한 후, 최대 크기가 1을 넘지 않도록 제한합니다.
        Vector2 combinedInput = _keyboardInput + _joystickInput;
        if (combinedInput.sqrMagnitude > 1f)
        {
            combinedInput.Normalize();
        }

        Vector3 moveDir = new Vector3(combinedInput.x, 0f, combinedInput.y);
        _rb.MovePosition(_rb.position + moveDir * (moveSpeed * Time.fixedDeltaTime));

        // 2. 회전 처리
        Vector3 lookDir = Vector3.zero;

        // 우측 회전 조이스틱 입력이 있는 경우 우선 처리 (발사 방향)
        if (_joystickRotationInput.sqrMagnitude > 0.001f)
        {
            lookDir = new Vector3(_joystickRotationInput.x, 0f, _joystickRotationInput.y);

            // 우측 조이스틱 조작 시 총기 발사
            if (shooter != null)
            {
                if (!_isShooting)
                {
                    _isShooting = true;
                    shooter.SetTrigger(true, lookDir);
                }
                else
                {
                    shooter.UpdateAimDirection(lookDir);
                }
            }
        }
        else
        {
            // 우측 조이스틱 입력을 놓았을 때 총기 발사 중지
            if (shooter != null && _isShooting)
            {
                _isShooting = false;
                shooter.SetTrigger(false);
                shooter.Reload();
            }

            // 우측 조이스틱 입력은 없지만 이동 입력이 있는 경우 이동 방향으로 회전
            if (combinedInput.sqrMagnitude > 0.001f)
            {
                lookDir = new Vector3(combinedInput.x, 0f, combinedInput.y);
            }
        }

        if (lookDir.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(lookDir, Vector3.up);
            _rb.MoveRotation(Quaternion.RotateTowards(_rb.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime));
        }
    }
}

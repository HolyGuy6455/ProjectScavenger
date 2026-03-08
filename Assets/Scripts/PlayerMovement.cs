using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 쿼터뷰 캐릭터 이동 스크립트
/// Unity Input System을 통해 키보드(WASD/화살표), 게임패드(XInput), 안드로이드 가상 조이스틱을 지원합니다.
/// 안드로이드 가상 조이스틱 UI는 IInputProvider를 구현하거나 SetMoveInput()을 직접 호출해 연동하세요.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;

    private Rigidbody _rb;
    private PlayerInputActions _inputActions;
    private Vector2 _moveInput;

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

    private void OnMove(InputAction.CallbackContext ctx)
    {
        _moveInput = ctx.ReadValue<Vector2>();
    }

    /// <summary>
    /// 안드로이드 가상 조이스틱 등 외부에서 입력값을 주입할 때 사용합니다.
    /// </summary>
    public void SetMoveInput(Vector2 input)
    {
        _moveInput = input;
    }

    private void FixedUpdate()
    {
        Vector3 moveDir = new Vector3(_moveInput.x, 0f, _moveInput.y).normalized;
        _rb.MovePosition(_rb.position + moveDir * (moveSpeed * Time.fixedDeltaTime));
    }
}

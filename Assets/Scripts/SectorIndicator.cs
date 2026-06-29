using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class SectorIndicator : MonoBehaviour
{
    [SerializeField] private int segments = 30; // 부채꼴을 그릴 때 사용할 폴리곤 갯수 (클수록 부드러움)
    private Mesh _mesh;
    private MeshRenderer _meshRenderer;

    private void Awake()
    {
        _mesh = new Mesh();
        GetComponent<MeshFilter>().mesh = _mesh;
        _meshRenderer = GetComponent<MeshRenderer>();
        
        // 머티리얼이 없다면 임시로 반투명한 빨간색 머티리얼을 생성합니다.
        if (_meshRenderer.sharedMaterial == null || _meshRenderer.sharedMaterial.name == "Default-Material")
        {
            Material mat = new Material(Shader.Find("Sprites/Default"));
            mat.color = new Color(1f, 0f, 0f, 0.2f); // 반투명 빨간색
            _meshRenderer.material = mat;
        }
    }

    /// <summary>
    /// 부채꼴 메쉬를 생성하여 그립니다.
    /// </summary>
    public void DrawSector(float radius, float angle, Vector3 direction)
    {
        _meshRenderer.enabled = true;
        
        // 바라보는 방향으로 오브젝트 회전
        if (direction != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
        }

        int verticesCount = segments + 2;
        Vector3[] vertices = new Vector3[verticesCount];
        int[] triangles = new int[segments * 3];

        // 중심점 (플레이어 발 밑)
        vertices[0] = Vector3.zero;

        float halfAngle = angle * 0.5f;
        float angleStep = angle / segments;

        // 원호 부분 정점 계산
        for (int i = 0; i <= segments; i++)
        {
            float currentAngle = -halfAngle + (angleStep * i);
            float rad = currentAngle * Mathf.Deg2Rad;

            // X, Z 평면 상의 좌표 (Y는 0)
            vertices[i + 1] = new Vector3(Mathf.Sin(rad), 0, Mathf.Cos(rad)) * radius;
        }

        // 삼각형 인덱스 구성
        for (int i = 0; i < segments; i++)
        {
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = i + 1;
            triangles[i * 3 + 2] = i + 2;
        }

        _mesh.Clear();
        _mesh.vertices = vertices;
        _mesh.triangles = triangles;
        _mesh.RecalculateNormals();
    }

    /// <summary>
    /// 인디케이터를 숨깁니다.
    /// </summary>
    public void Hide()
    {
        _meshRenderer.enabled = false;
        _mesh.Clear();
    }
}

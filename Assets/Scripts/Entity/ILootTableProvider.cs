public interface ILootTableProvider
{
    /// <summary>
    /// 전리품을 드롭할 때 사용할 루트 테이블 번호를 반환합니다.
    /// </summary>
    int GetLootTableId();
}

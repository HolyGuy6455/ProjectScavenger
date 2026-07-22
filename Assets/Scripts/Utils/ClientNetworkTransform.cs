using Unity.Netcode.Components;
using UnityEngine;

namespace Utils
{
    /// <summary>
    /// 클라이언트 권한(Client-Authoritative) 이동을 허용하는 커스텀 NetworkTransform 입니다.
    /// NGO 기본 NetworkTransform은 서버 권한이므로, 이 클래스를 대신 사용해야 딜레이 없는 이동이 가능합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class ClientNetworkTransform : NetworkTransform
    {
        protected override bool OnIsServerAuthoritative()
        {
            return false;
        }
    }
}

using Scavenger.Network;
using UnityEngine;
using TMPro;

namespace Scavenger.UI
{
    public class UI_PartyListItem : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI usernameText;

        public void Initialize(UserInfo user)
        {
            if (usernameText != null)
            {
                // 본인 이름이면 텍스트에 '(나)'를 붙이고 노란색으로 강조
                if (NetworkAuthManager.Instance != null && user.username == NetworkAuthManager.Instance.Username)
                {
                    usernameText.text = $"{user.username} <color=yellow>(나)</color>";
                }
                else
                {
                    usernameText.text = user.username;
                }
            }
        }
    }
}

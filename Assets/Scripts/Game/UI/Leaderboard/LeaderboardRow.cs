using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Leaderboard
{
    public class LeaderboardRow : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI rankLabel;
        [SerializeField] private Image colorSwatch;
        [SerializeField] private TextMeshProUGUI nameLabel;
        [SerializeField] private TextMeshProUGUI percentageLabel;
        [SerializeField] private Image backgroundImage;

        [SerializeField] private Color normalBackground = new(0f, 0f, 0f, 0.35f);
        [SerializeField] private Color localBackground = new(1f, 1f, 1f, 0.18f);
        

        public void Populate(int rank, in LeaderboardEntry entry)
        {
            rankLabel.text = rank.ToString();
            nameLabel.text = Truncate(entry.Name, 12);
            nameLabel.fontStyle = entry.IsLocal ? FontStyles.Bold : FontStyles.Normal;
            percentageLabel.text = $"{entry.Percentage:F1}%";
            colorSwatch.color = entry.Color;
            backgroundImage.color = entry.IsLocal ? localBackground : normalBackground;
        }

        private static string Truncate(string s, int max)
        {
            return s.Length <= max ? s : s.Substring(0, max - 1) + "…";
        }
    }
}
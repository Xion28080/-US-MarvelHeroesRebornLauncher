using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MHRebornLauncher.Models;

namespace MHRebornLauncher;

public partial class PendingRewardsWindow : Window
{
    public PendingRewardsWindow(IReadOnlyList<CommunityGoalRewardClaimDashboard> claims)
    {
        InitializeComponent();

        var groupedClaims = (claims ?? Array.Empty<CommunityGoalRewardClaimDashboard>())
            .Where(claim => claim != null)
            .GroupBy(claim => string.IsNullOrWhiteSpace(claim.GoalId)
                ? (string.IsNullOrWhiteSpace(claim.GoalName) ? "Community Goal" : claim.GoalName)
                : claim.GoalId,
                StringComparer.OrdinalIgnoreCase);

        foreach (var goalClaims in groupedClaims)
            ClaimsPanel.Children.Add(CreateGoalCard(goalClaims.ToList()));
    }

    private static Border CreateGoalCard(IReadOnlyList<CommunityGoalRewardClaimDashboard> claims)
    {
        CommunityGoalRewardClaimDashboard first = claims[0];
        StackPanel content = new();
        content.Children.Add(new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(first.GoalName) ? "Community Goal" : first.GoalName,
            Foreground = Brushes.White,
            FontSize = 18,
            FontWeight = FontWeights.Bold,
            TextWrapping = TextWrapping.Wrap
        });

        bool hasParticipation = claims.Any(c => IsParticipationReward(c.RewardSource));
        CommunityGoalRewardClaimDashboard? rankedClaim = claims.FirstOrDefault(c => !IsParticipationReward(c.RewardSource));
        if (hasParticipation && rankedClaim != null)
        {
            content.Children.Add(new TextBlock
            {
                Text = $"You earned the participation reward plus the {FormatRewardSource(rankedClaim.RewardSource).ToLowerInvariant()}.",
                Foreground = new SolidColorBrush(Color.FromRgb(199, 207, 220)),
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 5, 0, 13)
            });
        }
        else
        {
            content.Children.Add(new TextBlock
            {
                Text = "You are eligible for the following Community Goal reward.",
                Foreground = new SolidColorBrush(Color.FromRgb(199, 207, 220)),
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 5, 0, 13)
            });
        }

        foreach (CommunityGoalRewardClaimDashboard claim in claims.OrderBy(c => IsParticipationReward(c.RewardSource) ? 0 : 1))
        {
            content.Children.Add(new TextBlock
            {
                Text = FormatRewardSource(claim.RewardSource),
                Foreground = RewardSourceColor(claim.RewardSource),
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 2, 0, 7)
            });

            DashboardReward reward = claim.Reward ?? new DashboardReward();
            if (reward.G > 0)
                content.Children.Add(CreateRewardRow("CURRENCY", $"{reward.G:N0} G", Color.FromRgb(250, 204, 21)));

            foreach (DashboardRewardItem item in reward.Items ?? [])
            {
                string category = string.IsNullOrWhiteSpace(item.Category) ? "ITEM" : item.Category.ToUpperInvariant();
                string name = string.IsNullOrWhiteSpace(item.Name) ? "Item Reward" : item.Name;
                Color accent = category.Contains("COSTUME", StringComparison.OrdinalIgnoreCase)
                    ? Color.FromRgb(244, 114, 182)
                    : Color.FromRgb(192, 132, 252);
                content.Children.Add(CreateRewardRow(category, name, accent));
            }

            if (reward.G <= 0 && (reward.Items == null || reward.Items.Count == 0))
                content.Children.Add(new TextBlock
                {
                    Text = "Reward details will be available when you enter the game.",
                    Foreground = new SolidColorBrush(Color.FromRgb(151, 161, 180)),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 0, 8)
                });
        }

        return new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(20, 29, 44)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(54, 81, 109)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(7),
            Padding = new Thickness(16),
            Margin = new Thickness(0, 0, 0, 12),
            Child = content
        };
    }

    private static bool IsParticipationReward(string? source)
    {
        string value = (source ?? string.Empty).Trim();
        return value.Length == 0 || value.Equals("Community", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("Participation", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("Completion", StringComparison.OrdinalIgnoreCase);
    }

    private static Brush RewardSourceColor(string? source)
    {
        string value = (source ?? string.Empty).ToUpperInvariant();
        if (value.Contains("RANK 1") || value.Contains("RANK1") || value.Contains("TOP1"))
            return new SolidColorBrush(Color.FromRgb(250, 204, 21));
        if (value.Contains("2-3") || value.Contains("2–3"))
            return new SolidColorBrush(Color.FromRgb(192, 132, 252));
        if (value.Contains("4-10") || value.Contains("4–10"))
            return new SolidColorBrush(Color.FromRgb(34, 211, 238));
        return new SolidColorBrush(Color.FromRgb(34, 211, 238));
    }

    private static Border CreateRewardRow(string category, string name, Color accent)
    {
        Grid grid = new();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(96) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        grid.Children.Add(new TextBlock
        {
            Text = category,
            Foreground = new SolidColorBrush(accent),
            FontSize = 9,
            FontWeight = FontWeights.Bold,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0)
        });

        TextBlock value = new()
        {
            Text = name,
            Foreground = Brushes.White,
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap
        };
        Grid.SetColumn(value, 1);
        grid.Children.Add(value);

        return new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(14, 21, 32)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(120, accent.R, accent.G, accent.B)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(5),
            Padding = new Thickness(11, 8, 11, 8),
            Margin = new Thickness(0, 0, 0, 7),
            Child = grid
        };
    }

    private static string FormatRewardSource(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
            return "COMMUNITY COMPLETION REWARD";

        return source.Trim().ToUpperInvariant() switch
        {
            "RANK1" or "RANK 1" or "TOP1" => "#1 CONTRIBUTOR REWARD",
            "RANK2-3" or "RANKS2-3" or "RANK 2-3" => "#2–#3 CONTRIBUTOR REWARD",
            "RANK4-10" or "RANKS4-10" or "RANK 4-10" => "#4–#10 CONTRIBUTOR REWARD",
            "COMMUNITY" => "COMMUNITY COMPLETION REWARD",
            _ => source.Trim().ToUpperInvariant()
        };
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}

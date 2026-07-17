namespace MHRebornLauncher.Models;

public sealed class PlayerDashboardResponse
{
    public bool Success { get; set; }
    public string Error { get; set; } = "";
    public DashboardNotice? Notice { get; set; }
    public CommunityGoalDashboard? CommunityGoal { get; set; }
    public AccountDashboard Account { get; set; } = new();
    public List<CommunityGoalRewardClaimDashboard> RewardClaims { get; set; } = [];
    public List<CommunityGoalHistoryDashboard> CommunityGoalHistory { get; set; } = [];
    public string PreviewMode { get; set; } = "live";
    public string CommunityGoalsUrl { get; set; } = "https://play.omeganode.org/community-goals.php";
    public string RefreshedAtUtc { get; set; } = "";
}

public sealed class DashboardNotice
{
    public string Title { get; set; } = "Server Notice";
    public string Message { get; set; } = "";
    public string Severity { get; set; } = "info";
    public string Url { get; set; } = "";
}

public sealed class CommunityGoalDashboard
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "Community Goal";
    public string Description { get; set; } = "";
    public string GoalType { get; set; } = "";
    public int CurrentCount { get; set; }
    public int TargetCount { get; set; }
    public double Percent { get; set; }
    public long EndTimeUtc { get; set; }
    public DashboardReward CommunityReward { get; set; } = new();
    public List<DashboardRankReward> RankRewards { get; set; } = [];
    public List<DashboardContributor> TopContributors { get; set; } = [];
    public int PlayerContribution { get; set; }
    public int PlayerRank { get; set; }
}

public sealed class DashboardReward
{
    public int G { get; set; }
    public List<DashboardRewardItem> Items { get; set; } = [];
}

public sealed class DashboardRewardItem
{
    public string Prototype { get; set; } = "";
    public string Name { get; set; } = "Item Reward";
    public string Category { get; set; } = "Item";
}

public sealed class DashboardRankReward
{
    public int RankStart { get; set; }
    public int RankEnd { get; set; }
    public DashboardReward Reward { get; set; } = new();
}

public sealed class DashboardContributor
{
    public int Rank { get; set; }
    public string PlayerName { get; set; } = "";
    public int ContributionCount { get; set; }
}


public sealed class AccountDashboard
{
    public string PlayerName { get; set; } = "";
    public string EmailAddress { get; set; } = "";
    public string Rank { get; set; } = "User";
    public string Status { get; set; } = "Active";
    public bool RecoveryEmailSet { get; set; }
    public bool RecoveryEmailVerified { get; set; }
    public string RecoveryEmailMasked { get; set; } = "";
    public bool TwoFactorAvailable { get; set; }
    public bool TwoFactorEnabled { get; set; }
    public string TwoFactorStatus { get; set; } = "Not configured";
    public string CreatedAtUtc { get; set; } = "";
    public string LastLoginUtc { get; set; } = "";
    public bool IsAdministrator { get; set; }
    public bool PreviewEnabled { get; set; }
}

public sealed class CommunityGoalRewardClaimDashboard
{
    public string GoalId { get; set; } = "";
    public string GoalName { get; set; } = "Community Goal";
    public string RewardSource { get; set; } = "Community";
    public string Status { get; set; } = "Pending";
    public long CreatedAtUtc { get; set; }
    public long DeliveredAtUtc { get; set; }
    public DashboardReward Reward { get; set; } = new();
}

public sealed class CommunityGoalHistoryDashboard
{
    public string GoalId { get; set; } = "";
    public string Name { get; set; } = "Community Goal";
    public string Status { get; set; } = "Completed";
    public long EndTimeUtc { get; set; }
    public int FinalCount { get; set; }
    public int TargetCount { get; set; }
    public int PlayerContribution { get; set; }
    public int PlayerRank { get; set; }
}

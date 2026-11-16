using TrailHub.Notifications.Domain.Groups;

namespace TrailHub.UnitTests;

public sealed class MembershipDiffTests
{
    [Fact]
    public void The_first_snapshot_has_no_joins()
    {
        Assert.Empty(MembershipDiff.Joined(null, ["anna", "ben"]));
    }

    [Fact]
    public void New_member_ids_are_joins()
    {
        Assert.Equal(["cleo"], MembershipDiff.Joined(["anna", "ben"], ["anna", "ben", "cleo"]));
    }

    [Fact]
    public void Leaving_is_not_a_join()
    {
        Assert.Empty(MembershipDiff.Joined(["anna", "ben"], ["anna"]));
    }

    [Fact]
    public void Someone_who_left_and_came_back_joined()
    {
        Assert.Equal(["ben"], MembershipDiff.Joined(["anna"], ["anna", "ben"]));
    }
}

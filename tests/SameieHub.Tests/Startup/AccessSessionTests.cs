using SameieHub.Tests.Fakes;
using SameieHub.Web.Hosting;

namespace SameieHub.Tests.Startup;

public class AccessSessionTests
{
    private readonly ManualTimeProvider _clock = new(new DateTime(2026, 11, 27, 9, 0, 0, DateTimeKind.Utc));

    private AccessSession CreateSession() => new(_clock);

    [Fact]
    public void ALaunchCode_Has256BitsOfRandomness_AndEveryCodeIsDifferent()
    {
        var session = CreateSession();

        var codes = Enumerable.Range(0, 50).Select(_ => session.CreateLaunchCode()).ToList();

        Assert.Equal(50, codes.Distinct().Count());
        Assert.All(codes, code =>
        {
            Assert.Equal(32, Convert.FromBase64String(code.Replace('-', '+').Replace('_', '/') + "=").Length);
            Assert.DoesNotContain('=', code);
        });
    }

    [Fact]
    public void ALaunchCode_WorksOnce_AndGivesASession()
    {
        var session = CreateSession();
        var code = session.CreateLaunchCode();

        Assert.True(session.TryRedeemLaunchCode(code, out var token));
        Assert.True(session.IsValidSession(token));
        Assert.False(session.TryRedeemLaunchCode(code, out var second));
        Assert.Equal(string.Empty, second);
    }

    [Fact]
    public void ALaunchCode_ExpiresAfterSixtySeconds()
    {
        var session = CreateSession();
        var code = session.CreateLaunchCode();

        _clock.Advance(TimeSpan.FromSeconds(61));

        Assert.False(session.TryRedeemLaunchCode(code, out _));
    }

    [Fact]
    public void ALaunchCode_StillWorksJustBeforeItExpires()
    {
        var session = CreateSession();
        var code = session.CreateLaunchCode();

        _clock.Advance(TimeSpan.FromSeconds(59));

        Assert.True(session.TryRedeemLaunchCode(code, out _));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-code")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public void AWrongCode_IsRefused_AndDoesNotUseUpARealCode(string? wrong)
    {
        var session = CreateSession();
        var code = session.CreateLaunchCode();

        Assert.False(session.TryRedeemLaunchCode(wrong, out var token));
        Assert.Equal(string.Empty, token);
        Assert.True(session.TryRedeemLaunchCode(code, out _));
    }

    [Fact]
    public void ACodeFromAnotherRun_IsRefused_AndSoIsASessionFromAnotherRun()
    {
        var before = CreateSession();
        var code = before.CreateLaunchCode();
        before.TryRedeemLaunchCode(before.CreateLaunchCode(), out var oldToken);
        var afterRestart = CreateSession();

        Assert.False(afterRestart.TryRedeemLaunchCode(code, out _));
        Assert.False(afterRestart.IsValidSession(oldToken));
    }

    [Fact]
    public void ASession_IsValidForEightHours_ThenItIsNot()
    {
        var session = CreateSession();
        session.TryRedeemLaunchCode(session.CreateLaunchCode(), out var token);

        _clock.Advance(TimeSpan.FromHours(7.9));
        Assert.True(session.IsValidSession(token));

        _clock.Advance(TimeSpan.FromHours(0.2));
        Assert.False(session.IsValidSession(token));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("garbage")]
    public void ASessionTokenThatWasNeverIssued_IsNotValid(string? token)
    {
        Assert.False(CreateSession().IsValidSession(token));
    }

    [Fact]
    public void ASessionToken_IsNotAcceptedAsALaunchCode_AndTheOtherWayAround()
    {
        var session = CreateSession();
        var code = session.CreateLaunchCode();
        session.TryRedeemLaunchCode(session.CreateLaunchCode(), out var token);

        Assert.False(session.TryRedeemLaunchCode(token, out _));
        Assert.False(session.IsValidSession(code));
    }

    [Fact]
    public void ManyUnusedCodes_DoNotMakeTheProgramKeepThemAll_AndTheNewestStillWorks()
    {
        var session = CreateSession();
        for (var number = 0; number < 200; number++)
        {
            session.CreateLaunchCode();
        }

        var newest = session.CreateLaunchCode();

        Assert.True(session.TryRedeemLaunchCode(newest, out _));
    }

    [Fact]
    public void HasValidSession_IsTrueOnlyWhileASessionHasNotExpired()
    {
        var clock = new SameieHub.Tests.Fakes.ManualTimeProvider(new DateTime(2026, 11, 27, 9, 0, 0, DateTimeKind.Utc));
        var session = new AccessSession(clock);
        Assert.False(session.HasValidSession());

        Assert.True(session.TryRedeemLaunchCode(session.CreateLaunchCode(), out _));
        Assert.True(session.HasValidSession());

        clock.Advance(AccessSession.SessionLifetime + TimeSpan.FromSeconds(1));
        Assert.False(session.HasValidSession());
    }
}

using CXMTCode.Plugins.Common.Notification;

namespace CXMTCode.Tests;

public class EmailNotificationTests
{
    [Fact]
    public async Task DryRun_when_disabled()
    {
        var cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();
        var svc = new EmailNotificationService(cfg);
        svc.Enabled.Should().BeFalse();

        var r = await svc.SendAsync(new[] { "u@example.com" }, "subject", "body");
        r.Success.Should().BeTrue();
        r.Skipped.Should().BeTrue();
    }

    [Fact]
    public async Task Empty_recipients_returns_skipped()
    {
        var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Smtp:Host"]    = "smtp.test",
            ["Smtp:Enabled"] = "true"
        }).Build();
        var svc = new EmailNotificationService(cfg);
        var r = await svc.SendAsync(Array.Empty<string>(), "subject", "body");
        r.Success.Should().BeTrue();
        r.Skipped.Should().BeTrue();
        r.Message.Should().Contain("收件人");
    }
}

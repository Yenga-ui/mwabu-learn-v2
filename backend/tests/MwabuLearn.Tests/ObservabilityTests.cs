using System.Diagnostics;
using MwabuLearn.Api.Operations;

namespace MwabuLearn.Tests;

public class ObservabilityTests
{
    [Fact]
    public void ExportProcessorRemovesSensitiveTagsNamesAndStatusDescriptions()
    {
        using var activity = new Activity("SELECT secret FROM users?token=secret").Start();
        activity.SetTag("url.full", "https://api.test/?cursor=secret");
        activity.SetTag("db.statement", "password=secret");
        activity.SetTag("http.request.method", "GET");
        activity.SetStatus(ActivityStatusCode.Error, "secret exception");
        activity.SetBaggage("email", "private@example.test");
        var processor = new SafeActivityProcessor();
        processor.OnStart(activity);
        processor.OnEnd(activity);
        Assert.Null(activity.GetTagItem("url.full"));
        Assert.Null(activity.GetTagItem("db.statement"));
        Assert.Empty(activity.Baggage);
        Assert.Null(activity.StatusDescription);
        Assert.Equal("backend operation", activity.DisplayName);
        Assert.Equal("GET", activity.GetTagItem("http.request.method"));
    }

    [Fact]
    public void ExceptionPayloadSpansAreNotRecordedForExport()
    {
        using var activity = new Activity("query").Start();
        activity.ActivityTraceFlags = ActivityTraceFlags.Recorded;
        activity.AddEvent(new ActivityEvent("exception", tags: new ActivityTagsCollection { ["exception.message"] = "secret" }));
        new SafeActivityProcessor().OnEnd(activity);
        Assert.False(activity.Recorded);
    }
}

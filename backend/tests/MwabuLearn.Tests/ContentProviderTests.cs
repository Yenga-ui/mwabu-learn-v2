using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;
using MwabuLearn.Application.Lessons;
using MwabuLearn.Infrastructure.Lessons;
using MwabuLearn.Infrastructure.Lessons.Providers;
namespace MwabuLearn.Tests;
public sealed class ContentProviderTests
{
    [Theory]
    [InlineData("visual-explanation")]
    [InlineData("worked-example")]
    [InlineData("discussion-prompts")]
    [InlineData("activity-suggestions")]
    [InlineData("learner-explanation")]
    public async Task Classroom_outlines_use_bounded_non_executable_structured_output(string kind)
    {
        Assert.Contains(kind, ContentGeneration.Kinds);
        using var http = new HttpClient(new Handler(async (request, ct) => {
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct)); var schema = body.RootElement.GetProperty("format");
            Assert.False(schema.GetProperty("additionalProperties").GetBoolean());
            var steps = schema.GetProperty("properties").GetProperty("steps"); Assert.Equal(12, steps.GetProperty("maxItems").GetInt32());
            Assert.False(steps.GetProperty("items").GetProperty("additionalProperties").GetBoolean());
            Assert.False(schema.GetProperty("properties").TryGetProperty("script", out _));
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"done\":true,\"done_reason\":\"stop\",\"message\":{\"content\":\"{}\"},\"prompt_eval_count\":20,\"eval_count\":30}") };
        }));
        await new ContentAiProvider(http, Options.Create(new LessonAiOptions { Enabled = true, DataProcessingApproved = true })).Generate("Synthetic curriculum", kind, "test-only", default);
    }
    private sealed class Handler(Func<HttpRequestMessage,CancellationToken,Task<HttpResponseMessage>> send):HttpMessageHandler
    {protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)=>send(request,ct);}
    [Theory]
    [InlineData("Ollama")]
    [InlineData("OpenAI")]
    public async Task Adapter_uses_strict_declarative_schema_and_reports_actual_usage(string provider)
    {
        var calls=0;var settings=new LessonAiOptions {Provider=provider,Enabled=true,DataProcessingApproved=true,Model="synthetic-test-model",ApiKey="synthetic-test-key-only-not-a-credential",InputUsdPerMillionTokens=1,OutputUsdPerMillionTokens=1};
        using var http=new HttpClient(new Handler(async(request,ct)=>{
            calls++;Assert.Equal(provider=="Ollama"?"127.0.0.1":"api.openai.com",request.RequestUri!.Host);
            using var json=JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));var root=json.RootElement;
            var schema=provider=="Ollama"?root.GetProperty("format"):root.GetProperty("text").GetProperty("format").GetProperty("schema");
            Assert.False(schema.GetProperty("additionalProperties").GetBoolean());Assert.Equal("quiz",schema.GetProperty("properties").GetProperty("kind").GetProperty("enum")[0].GetString());
            if(provider=="OpenAI"){Assert.False(root.GetProperty("store").GetBoolean());Assert.True(root.GetProperty("text").GetProperty("format").GetProperty("strict").GetBoolean());}
            else {Assert.False(root.GetProperty("stream").GetBoolean());Assert.Null(request.Headers.Authorization);}
            object response=provider=="Ollama"?new{done=true,done_reason="stop",message=new{content="{}"},prompt_eval_count=20,eval_count=30}:(object)new{status="completed",output=new[]{new{content=new[]{new{type="output_text",text="{}"}}}},usage=new{input_tokens=20,output_tokens=30}};
            return new(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(response))};
        }));
        var result=await new ContentAiProvider(http,Options.Create(settings)).Generate("Synthetic curriculum only","quiz",settings.Model,default);Assert.Equal(20,result.InputTokens);Assert.Equal(30,result.OutputTokens);Assert.Equal(1,calls);
    }
    [Fact]
    public async Task Personal_data_is_rejected_before_transport_and_ambiguous_http_failures_are_not_retried()
    {
        var calls=0;using var http=new HttpClient(new Handler((request,ct)=>{calls++;return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadGateway));}));
        var provider=new ContentAiProvider(http,Options.Create(new LessonAiOptions{Enabled=true,DataProcessingApproved=true}));
        await Assert.ThrowsAsync<PlannerException>(()=>provider.Generate("learner@identity.test","quiz","synthetic",default));Assert.Equal(0,calls);
        await Assert.ThrowsAsync<PlannerException>(()=>provider.Generate("Synthetic curriculum","quiz","synthetic",default));Assert.Equal(1,calls);
    }
}

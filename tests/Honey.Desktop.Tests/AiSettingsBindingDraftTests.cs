using Honey.Desktop.Settings;
using Honey.Integrations.Ai;
using Honey.Integrations.Security;

namespace Honey.Desktop.Tests;

public sealed class AiSettingsBindingDraftTests
{
    [Fact]
    public void ReadBindingId_保留已保存绑定且清除后置空()
    {
        var draft = new AiSettingsBindingDraft("binding-a");

        Assert.Equal("binding-a", draft.BindingId);

        draft.Clear();

        Assert.Null(draft.BindingId);
    }

    [Fact]
    public void 保留绑定可解析匹配配置但编辑端点后仍拒绝()
    {
        var validated = AiEndpointValidator.StrictValidate(
            "https://example.com/v1",
            "model");
        var secret = new BoundAiSecret(
            "sk-secret",
            "binding-a",
            BoundAiSecret.CurrentConfigVersion,
            validated.CanonicalEndpoint,
            validated.Model);
        var draft = new AiSettingsBindingDraft("binding-a");

        Assert.True(AiConfigurationResolver.Resolve(
            true,
            "https://example.com/v1/",
            "model",
            draft.BindingId,
            secret).Available);
        Assert.False(AiConfigurationResolver.Resolve(
            true,
            "https://other.example/v1",
            "model",
            draft.BindingId,
            secret).Available);
    }
}

namespace LayeredChat.Tests;

public sealed class OrchestrationForwardMappingTests
{
    [Fact]
    public void ConnectorOptions_round_trips_telemetry_verbosity()
    {
        var dto = new LayeredChatTurnRequestDto
        {
            OrchestrationRegistryKey = "k",
            UserMessageContent = "u",
            SystemInstructionText = "s",
            Session = new OrchestrationSessionContextDto(),
            ConnectorOptions = new LlmRequestOptionsDto { TelemetryVerbosity = OrchestrationTelemetryVerbosity.Minimal }
        };

        var back = OrchestrationForwardMapping.FromDto(dto);

        Assert.NotNull(back.ConnectorOptions);
        Assert.Equal(OrchestrationTelemetryVerbosity.Minimal, back.ConnectorOptions!.TelemetryVerbosity);

        var again = OrchestrationForwardMapping.ToDto(back);
        Assert.NotNull(again.ConnectorOptions);
        Assert.Equal(OrchestrationTelemetryVerbosity.Minimal, again.ConnectorOptions!.TelemetryVerbosity);
    }
}

using System.Text.Json;

namespace Nara.PlaybookEngine;

internal sealed class FakeWindowsAdapter(string statePath)
{
    internal void Precheck(FakeWindowsState state, SettingOperation operation)
    {
        JsonElement current = CurrentValue(state, operation.Setting);
        bool expected = JsonElement.DeepEquals(current, operation.Expected);
        bool alreadyDesired = JsonElement.DeepEquals(current, operation.Desired);
        JsonSupport.Require(expected || alreadyDesired, $"Precheck mismatch for {operation.Setting}.");
    }

    internal bool Apply(FakeWindowsState state, PlaybookAction action)
    {
        bool changed = false;
        foreach (SettingOperation operation in action.Operations)
        {
            JsonElement current = CurrentValue(state, operation.Setting);
            if (JsonElement.DeepEquals(current, operation.Desired))
            {
                continue;
            }

            SetValue(state, operation);
            changed = true;
        }

        if (changed)
        {
            state.Revision++;
            JsonSupport.WriteAtomically(statePath, JsonSupport.SerializeIndented(state));
        }

        return changed;
    }

    internal void Verify(FakeWindowsState state, PlaybookAction action)
    {
        foreach (SettingOperation operation in action.Operations)
        {
            JsonElement current = CurrentValue(state, operation.Setting);
            JsonSupport.Require(JsonElement.DeepEquals(current, operation.Desired), $"Verification failed for {operation.Setting}.");
        }
    }

    internal void Restore(byte[] checkpoint) => JsonSupport.WriteAtomically(statePath, checkpoint);

    private static JsonElement CurrentValue(FakeWindowsState state, string setting) => setting switch
    {
        "ui.animations" => JsonSerializer.SerializeToElement(state.Settings.UiAnimations),
        "ui.transparency" => JsonSerializer.SerializeToElement(state.Settings.UiTransparency),
        "ai.runtimeMode" => JsonSerializer.SerializeToElement(state.Settings.AiRuntimeMode),
        "ai.idleUnloadMinutes" => JsonSerializer.SerializeToElement(state.Settings.AiIdleUnloadMinutes),
        _ => throw new InvalidDataException($"Setting is not supported by the fake adapter: {setting}")
    };

    private static void SetValue(FakeWindowsState state, SettingOperation operation)
    {
        switch (operation.Setting)
        {
            case "ui.animations":
                state.Settings.UiAnimations = operation.Desired.GetBoolean();
                break;
            case "ui.transparency":
                state.Settings.UiTransparency = operation.Desired.GetBoolean();
                break;
            case "ai.runtimeMode":
                state.Settings.AiRuntimeMode = operation.Desired.GetString()
                    ?? throw new InvalidDataException("AI runtime mode cannot be null.");
                break;
            case "ai.idleUnloadMinutes":
                state.Settings.AiIdleUnloadMinutes = operation.Desired.GetInt32();
                break;
            default:
                throw new InvalidDataException($"Setting is not supported by the fake adapter: {operation.Setting}");
        }
    }
}

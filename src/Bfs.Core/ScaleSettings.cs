namespace Bfs.Core;

public record ScaleSettings(
    string Mode,
    string Port,
    int Baud,
    int DataBits,
    int StopBits,
    string Parity,
    string Protocol,
    string Extract)
{
    public static ScaleSettings Simulated() =>
        new("sim", "", 9600, 8, 1, "None", "Line", "");
}

public interface IScaleController
{
    string Status { get; }
    string? LastError { get; }
    ScaleSettings Settings { get; }
    IReadOnlyList<string> ListPorts();
    void Apply(ScaleSettings settings);
}

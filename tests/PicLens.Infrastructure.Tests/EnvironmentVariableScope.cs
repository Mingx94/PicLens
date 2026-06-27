namespace PicLens.Infrastructure.Tests;

internal sealed class EnvironmentVariableScope : IDisposable
{
    private readonly string name;
    private readonly string? previousValue;

    private EnvironmentVariableScope(string name, string? value)
    {
        this.name = name;
        Value = value;
        previousValue = Environment.GetEnvironmentVariable(name);
        Environment.SetEnvironmentVariable(name, value);
    }

    public string? Value { get; }

    public static EnvironmentVariableScope Set(string name, string? value) => new(name, value);

    public void Dispose() => Environment.SetEnvironmentVariable(name, previousValue);
}

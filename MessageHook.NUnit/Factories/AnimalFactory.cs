using MessageHook.Tests.Models;

namespace MessageHook.NUnit.Factories;

public static class AnimalFactory
{
    private static readonly Random _random = new();

    public static Animal Create(string name = "Rex")
        => new() { Id = _random.Next(1000, 9999999), Name = name };
}

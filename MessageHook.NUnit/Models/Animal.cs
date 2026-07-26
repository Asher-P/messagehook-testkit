namespace MessageHook.Tests.Models;

public class Animal
{
    public int Id { get; set; }
    public string Name { get; set; }

    // Topic A carries only Id and Name. The echo service adds an "IsChanged" field to the topic-B message on the
    // way out; it is not part of the produced schema, so it is deliberately absent here.
}

namespace MessageHook.Core.Messaging.FilterService;

public interface IFilterService
{
    void AddFilter(string MessageHookId);
    string Filter(string obj);
}
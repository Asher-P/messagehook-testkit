using Microsoft.Extensions.Logging;

namespace MessageHook.Core.Messaging.FilterService;

public class FilterService : IFilterService
{
    private readonly ILogger<FilterService> _logger;
    private HashSet<string> _whiteListKeys;
    public HashSet<string> MessageList = new HashSet<string>();
    public int ReadCounter = 0;
    private Dictionary<string, Predicate<string>> filterDict = new Dictionary<string, Predicate<string>>();

    public FilterService(ILogger<FilterService> logger)
    {
        _logger = logger;
        _whiteListKeys = new HashSet<string>();
    }

    // public void Add(string newHash)
    // {
    //     _whiteListKeys.Add(newHash);
    // }


    public void ClearFilters()
    {
        ReadCounter = 0;
        MessageList = new HashSet<string>();
        _whiteListKeys = new();
    }


    /// <summary>
    /// Start run list of conditions and return true if all conditions are true
    /// </summary>
    /// <param name="obj">The object you want to run list of checks on him</param>
    /// <returns>
    /// return true if the obj pass all predicates
    /// return false if there is no have any checks
    /// return false if one of the check list is false
    /// </returns>
    private string InvokeFilter(string obj)
    {

        foreach (var (key, predicate) in filterDict)
        {
            if (predicate != null && predicate.GetInvocationList().Any())
            {
                if (predicate.Invoke(obj))
                {
                    return key;
                }
            }
            else
            {
                throw new Exception("Filter Is not exist");
            }
        }
        return null;
    }

    public void AddFilter(string MessageHookId)
    {
        filterDict[MessageHookId] = x => x == MessageHookId;
    }

    public string Filter(string obj)
    {
        return InvokeFilter(obj);
    }
}
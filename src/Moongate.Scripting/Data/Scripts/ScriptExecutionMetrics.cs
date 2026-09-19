namespace Moongate.Scripting.Data.Scripts;

/// <summary>Counters since startup. Snapshot; not live.</summary>
/// <param name="FilesLoaded">Files whose chunk ran, counting a reload again. A file served from the loader's cache is not counted.</param>
/// <param name="CallsStarted">Calls to <see cref="Moongate.Scripting.Interfaces.IScriptEngine.Call"/>, including those that failed to find the function.</param>
/// <param name="CoroutinesResumed">Coroutine resumes, counting the first one that starts each coroutine and every later resume from the timer wheel.</param>
/// <param name="CoroutinesFinished">Coroutines that ran to their end.</param>
/// <param name="Errors">Coroutines that failed, for any reason including a budget abort.</param>
/// <param name="BudgetAborts">Units of execution stopped by the instruction budget: coroutine resumes and top-level chunks alike.</param>
/// <param name="ActiveCoroutines">Coroutines alive right now: started and neither finished nor failed, most of them parked on a <c>wait</c>.</param>
/// <param name="MemoryCapHits">Calls refused because their result would have exceeded the string cap; each one raised a script error in the caller.</param>
public sealed record ScriptExecutionMetrics(
    int FilesLoaded,
    long CallsStarted,
    long CoroutinesResumed,
    long CoroutinesFinished,
    long Errors,
    long BudgetAborts,
    int ActiveCoroutines,
    long MemoryCapHits
);

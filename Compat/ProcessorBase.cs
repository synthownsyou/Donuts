namespace Donuts.Compat;

public abstract class ProcessorBase<T>
{
    private ProcessorBase<T> _next;

    public ProcessorBase<T> SetNext(ProcessorBase<T> next)
    {
        _next = next;
        return next;
    }

    public virtual bool Process(T input)
    {
        return _next == null || _next.Process(input);
    }
}

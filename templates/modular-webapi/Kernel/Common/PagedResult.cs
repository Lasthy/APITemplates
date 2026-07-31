namespace Kernel.Common;

public record PagedResult<T> : Result<IEnumerable<T>>
{
	public int Page { get; }
	public int PageSize { get; }
	
    internal PagedResult(int page, IEnumerable<T> value, bool isSuccess, Exception? error = null, params string[] messages) : base(value, isSuccess, error, messages)
    {
		Page = page;
		PageSize = value.Count();
    }
}

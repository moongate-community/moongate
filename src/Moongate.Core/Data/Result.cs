namespace Moongate.Core.Data;

public class Result<TData>
{
    public TData? Value { get; }

    public bool IsSuccess { get; }

    public string? ErrorMessage { get; }

    public Result(TData? value, bool isSuccess, string? errorMessage = null)
    {
        Value = value;
        IsSuccess = isSuccess;
        ErrorMessage = errorMessage;
    }

    public static Result<TData> Success(TData value)
    {
        return new Result<TData>(value, true);
    }

    public static Result<TData> Failure(string errorMessage)
    {
        return new Result<TData>(default, false, errorMessage);
    }

    public static Builder CreateBuilder() => new Builder();

    public class Builder
    {
        private TData? _value;
        private bool _isSuccess;
        private string? _errorMessage;

        public Builder WithValue(TData? value)
        {
            _value = value;
            return this;
        }

        public Builder Success()
        {
            _isSuccess = true;
            _errorMessage = null;
            return this;
        }

        public Builder Failure(string errorMessage)
        {
            _isSuccess = false;
            _errorMessage = errorMessage;
            return this;
        }

        public Result<TData> Build()
        {
            return new Result<TData>(_value, _isSuccess, _errorMessage);
        }
    }
}
